#pragma warning disable 1591

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Sanakan.Config;
using Sanakan.Database.Models;
using Sanakan.Extensions;
using Sanakan.Services.Executor;
using Sanakan.Services.PocketWaifu.Fight;
using Shinden;
using Shinden.Models;

namespace Sanakan.Services.PocketWaifu
{
    public enum FightWinner
    {
        Card1, Card2, Draw
    }

    public enum BodereBonus
    {
        None, Minus, Plus
    }

    public enum HaremType
    {
        Rarity, Cage, Affection, Attack, Defence, Health, Tag, NoTag
    }

    public class Waifu
    {
        private static CharacterIdUpdate CharId = new CharacterIdUpdate();

        private IConfig _config;
        private ImageProcessing _img;
        private ShindenClient _shClient;

        public Waifu(ImageProcessing img, ShindenClient client, IConfig config)
        {
            _img = img;
            _config = config;
            _shClient = client;
        }

        public List<Card> GetListInRightOrder(IEnumerable<Card> list, HaremType type, string tag)
        {
            switch (type)
            {
                case HaremType.Health:
                    return list.OrderByDescending(x => x.GetHealthWithPenalty()).ToList();

                case HaremType.Affection:
                    return list.OrderByDescending(x => x.Affection).ToList();

                case HaremType.Attack:
                    return list.OrderByDescending(x => x.GetAttackWithBonus()).ToList();

                case HaremType.Defence:
                    return list.OrderByDescending(x => x.GetDefenceWithBonus()).ToList();

                case HaremType.Cage:
                    return list.Where(x => x.InCage).ToList();

                case HaremType.Tag:
                    return list.Where(x => x.Tags != null).Where(x => x.Tags.Contains(tag, StringComparison.CurrentCultureIgnoreCase)).ToList();

                case HaremType.NoTag:
                    return list.Where(x => x.Tags == null || (x.Tags != null && !x.Tags.Contains(tag, StringComparison.CurrentCultureIgnoreCase))).ToList();

                default:
                case HaremType.Rarity:
                    return list.OrderBy(x => x.Rarity).ToList();
            }
        }

        public Embed GetGMwKView(IEmote emote, Rarity max)
        {
            var time = DateTime.Now.AddMinutes(3);
            return new EmbedBuilder
            {
                Color = EMType.Error.Color(),
                Description = $"**Grupowa Masakra w Kisielu**\n\nRozpoczęcie: `{time.ToShortTimeString()}:{time.Second.ToString("00")}`\n"
                    + $"Wymagana minimalna liczba graczy: `5`\nMaksymalna jakość karty: `{max}`\n\nAby dołączyć kliknij na reakcje {emote}"
            }.Build();
        }

        public Rarity RandomizeRarity()
        {
            var num = Fun.GetRandomValue(1000);
            if (num < 5)   return Rarity.SS;
            if (num < 25)  return Rarity.S;
            if (num < 75)  return Rarity.A;
            if (num < 175) return Rarity.B;
            if (num < 370) return Rarity.C;
            if (num < 620) return Rarity.D;
            return Rarity.E;
        }

        public List<Rarity> GetExcludedArenaRarity(Rarity cardRarity)
        {
            var excudled = new List<Rarity>();

            switch (cardRarity)
            {
                case Rarity.SSS:
                    excudled.Add(Rarity.A);
                    excudled.Add(Rarity.B);
                    excudled.Add(Rarity.C);
                    excudled.Add(Rarity.D);
                    excudled.Add(Rarity.E);
                    break;

                case Rarity.SS:
                    excudled.Add(Rarity.B);
                    excudled.Add(Rarity.C);
                    excudled.Add(Rarity.D);
                    excudled.Add(Rarity.E);
                    break;

                case Rarity.S:
                    excudled.Add(Rarity.C);
                    excudled.Add(Rarity.D);
                    excudled.Add(Rarity.E);
                    break;

                case Rarity.A:
                    excudled.Add(Rarity.D);
                    excudled.Add(Rarity.E);
                    break;

                case Rarity.B:
                    excudled.Add(Rarity.E);
                    break;

                case Rarity.C:
                    excudled.Add(Rarity.SS);
                    break;

                case Rarity.D:
                    excudled.Add(Rarity.SS);
                    excudled.Add(Rarity.S);
                    break;

                default:
                case Rarity.E:
                    excudled.Add(Rarity.SS);
                    excudled.Add(Rarity.S);
                    excudled.Add(Rarity.A);
                    break;
            }

            return excudled;
        }

        public Rarity RandomizeRarity(List<Rarity> rarityExcluded)
        {
            if (rarityExcluded == null) return RandomizeRarity();
            if (rarityExcluded.Count < 1) return RandomizeRarity();

            var list = new List<RarityChance>()
            {
                new RarityChance(5,    Rarity.SS),
                new RarityChance(25,   Rarity.S ),
                new RarityChance(75,   Rarity.A ),
                new RarityChance(175,  Rarity.B ),
                new RarityChance(370,  Rarity.C ),
                new RarityChance(650,  Rarity.D ),
                new RarityChance(1000, Rarity.E ),
            };

            var ex = list.Where(x => rarityExcluded.Any(c => c == x.Rarity)).ToList();
            foreach(var e in ex) list.Remove(e);

            var num = Fun.GetRandomValue(1000);
            foreach(var rar in list)
            {
                if (num < rar.Chance)
                    return rar.Rarity;
            }
            return list.Last().Rarity;
        }

        public ItemType RandomizeItemFromFight()
        {
            var num = Fun.GetRandomValue(1000);
            if (num < 7) return ItemType.BetterIncreaseUpgradeCnt;
            if (num < 15) return ItemType.IncreaseUpgradeCnt;
            if (num < 40) return ItemType.AffectionRecoveryGreat;
            if (num < 95) return ItemType.AffectionRecoveryBig;
            if (num < 165) return ItemType.CardParamsReRoll;
            if (num < 255) return ItemType.DereReRoll;
            if (num < 475) return ItemType.AffectionRecoveryNormal;
            return ItemType.AffectionRecoverySmall;
        }

        public ItemWithCost[] GetItemsWithCost()
        {
            return new ItemWithCost[]
            {
                new ItemWithCost(10,    ItemType.AffectionRecoverySmall.ToItem()),
                new ItemWithCost(35,    ItemType.AffectionRecoveryNormal.ToItem()),
                new ItemWithCost(225,   ItemType.AffectionRecoveryBig.ToItem()),
                new ItemWithCost(50,    ItemType.DereReRoll.ToItem()),
                new ItemWithCost(100,   ItemType.CardParamsReRoll.ToItem()),
                new ItemWithCost(3000,  ItemType.IncreaseUpgradeCnt.ToItem()),
                new ItemWithCost(100,   ItemType.RandomBoosterPackSingleE.ToItem()),
                new ItemWithCost(1400,  ItemType.RandomTitleBoosterPackSingleE.ToItem()),
                new ItemWithCost(800,   ItemType.RandomNormalBoosterPackB.ToItem()),
                new ItemWithCost(1400,  ItemType.RandomNormalBoosterPackA.ToItem()),
                new ItemWithCost(2000,  ItemType.RandomNormalBoosterPackS.ToItem()),
                new ItemWithCost(2600,  ItemType.RandomNormalBoosterPackSS.ToItem()),
            };
        }

        public double GetExpToUpgrade(Card toUp, Card toSac, bool wild = false)
        {
            double rExp = 30f / (wild ? 100f : 30f);

            if (toUp.Character == toSac.Character)
                rExp = 30f / 5f;

            var sacVal = (int) toSac.Rarity;
            var upVal = (int) toUp.Rarity;
            var diff = upVal - sacVal;

            if (diff < 0)
            {
                diff = -diff;
                for (int i = 0; i < diff; i++) rExp /= 2;
            }
            else if (diff > 0)
            {
                for (int i = 0; i < diff; i++) rExp *= 1.5;
            }

            return rExp;
        }

        public FightWinner GetFightWinner(CardInfo card1, CardInfo card2)
        {
            var diffInSex = BodereBonus.None;
            if (card1.Info.Gender != Sex.NotSpecified && card2.Info.Gender != Sex.NotSpecified)
            {
                diffInSex = BodereBonus.Minus;
                if (card1.Info.Gender != card2.Info.Gender)
                    diffInSex = BodereBonus.Plus;
            }

            var FAcard1 = GetFA(card1, card2, diffInSex);
            var FAcard2 = GetFA(card2, card1, diffInSex);

            var c1Health = card1.Card.GetHealthWithPenalty();
            var c2Health = card2.Card.GetHealthWithPenalty();
            var atkTk1 = c1Health / FAcard2;
            var atkTk2 = c2Health / FAcard1;

            var winner = FightWinner.Draw;
            if (atkTk1 > atkTk2 + 0.3) winner = FightWinner.Card1;
            if (atkTk2 > atkTk1 + 0.3) winner = FightWinner.Card2;

            // kamidere && deredere
            if (winner == FightWinner.Draw)
                return CheckKamidereAndDeredere(card1, card2);

            return winner;
        }

        private FightWinner CheckKamidereAndDeredere(CardInfo card1, CardInfo card2)
        {
            if (card1.Card.Dere == Dere.Kamidere)
            {
                if (card2.Card.Dere == Dere.Kamidere)
                    return FightWinner.Draw;

                return FightWinner.Card1;
            }

            if (card2.Card.Dere == Dere.Kamidere)
            {
                return FightWinner.Card2;
            }

            bool card1Lose = false;
            bool card2Lose = false;

            if (card1.Card.Dere == Dere.Deredere)
                card1Lose = Fun.TakeATry(2);

            if (card2.Card.Dere == Dere.Deredere)
                card2Lose = Fun.TakeATry(2);

            if (card1Lose && card2Lose) return FightWinner.Draw;
            if (card1Lose) return FightWinner.Card2;
            if (card2Lose) return FightWinner.Card1;

            return FightWinner.Draw;
        }

        public double GetFA(CardInfo target, CardInfo enemy, BodereBonus bodere)
        {
            double atk1 = target.Card.GetAttackWithBonus();
            double def1 = target.Card.GetDefenceWithBonus();
            if (!target.Info.HasImage)
            {
                atk1 -= atk1 * 20 / 100;
                def1 -= def1 * 20 / 100;
            }

            TryApplyDereBonus(target.Card.Dere, ref atk1, ref def1, bodere);
            if (atk1 < 1) atk1 = 1;
            if (def1 < 1) def1 = 1;

            double atk2 = enemy.Card.GetAttackWithBonus();
            double def2 = enemy.Card.GetDefenceWithBonus();
            if (!enemy.Info.HasImage)
            {
                atk2 -= atk2 * 20 / 100;
                def2 -= def2 * 20 / 100;
            }

            TryApplyDereBonus(enemy.Card.Dere, ref atk2, ref def2, bodere);
            if (atk2 < 1) atk2 = 1;
            if (def2 < 1) def2 = 1;
            if (def2 > 99) def2 = 99;

            return atk1 * (100 - def2) / 100;
        }

        private void TryApplyDereBonus(Dere dere, ref double atk, ref double def, BodereBonus bodere)
        {
            if (dere == Dere.Bodere)
            {
                switch (bodere)
                {
                    case BodereBonus.Minus:
                        atk -= atk / 10;
                    break;

                    case BodereBonus.Plus:
                        atk += atk / 10;
                    break;

                    default:
                    break;
                }
            }
            else if (Fun.TakeATry(5))
            {
                var tenAtk = atk / 10;
                var tenDef = def / 10;

                switch(dere)
                {
                    case Dere.Yandere:
                        atk += tenAtk;
                    break;

                    case Dere.Dandere:
                        def += tenDef;
                    break;

                    case Dere.Kuudere:
                        atk -= tenAtk;
                        def += tenAtk;
                    break;

                    case Dere.Mayadere:
                        def -= tenDef;
                        atk += tenDef;
                    break;

                    default:
                    break;
                }
            }
        }

        public int RandomizeAttack(Rarity rarity)
            => Fun.GetRandomValue(rarity.GetAttackMin(), rarity.GetAttackMax() + 1);

        public int RandomizeDefence(Rarity rarity)
            => Fun.GetRandomValue(rarity.GetDefenceMin(), rarity.GetDefenceMax() + 1);

        public int RandomizeHealth(Card card)
            => Fun.GetRandomValue(card.Rarity.GetHealthMin(), card.GetHealthMax() + 1);

        public Dere RandomizeDere()
        {
            var allDere = Enum.GetValues(typeof(Dere)).Cast<Dere>();
            return Fun.GetOneRandomFrom(allDere);
        }

        public Card GenerateNewCard(ICharacterInfo character, Rarity rarity)
        {
            var card = new Card
            {
                Title = character?.Relations?.OrderBy(x => x.Id)?.FirstOrDefault()?.Title ?? "????",
                Defence = RandomizeDefence(rarity),
                ArenaStats = new CardArenaStats(),
                Attack = RandomizeAttack(rarity),
                CreationDate = DateTime.Now,
                Name = character.ToString(),
                Source = CardSource.Other,
                Character = character.Id,
                Dere = RandomizeDere(),
                RarityOnStart = rarity,
                IsTradable = true,
                UpgradesCnt = 2,
                Rarity = rarity,
                Tags = null,
            };

            card.Health = RandomizeHealth(card);
            return card;
        }

        public Card GenerateNewCard(ICharacterInfo character)
            => GenerateNewCard(character, RandomizeRarity());

        public Card GenerateNewCard(ICharacterInfo character, List<Rarity> rarityExcluded)
            => GenerateNewCard(character, RandomizeRarity(rarityExcluded));

        private int ScaleNumber(int oMin, int oMax, int nMin, int nMax, int value)
        {
            var m = (double)(nMax - nMin)/(double)(oMax - oMin);
            var c = (oMin * m) - nMin;

            return (int)((m * value) - c);
        }

        public int GetAttactAfterLevelUp(Rarity oldRarity, int oldAtk)
        {
            var newRarity = oldRarity - 1;
            var newMax = newRarity.GetAttackMax();
            var newMin = newRarity.GetAttackMin();
            var range = newMax - newMin;

            var oldMax = oldRarity.GetAttackMax();
            var oldMin = oldRarity.GetAttackMin();

            var relNew = ScaleNumber(oldMin, oldMax, newMin, newMax, oldAtk);
            var relMin = relNew - (range * 6 / 100);
            var relMax = relNew + (range * 8 / 100);

            var nAtk = Fun.GetRandomValue(relMin, relMax + 1);
            if (nAtk > newMax) nAtk = newMax;
            if (nAtk < newMin) nAtk = newMin;

            return nAtk;
        }

        public int GetDefenceAfterLevelUp(Rarity oldRarity, int oldDef)
        {
            var newRarity = oldRarity - 1;
            var newMax = newRarity.GetDefenceMax();
            var newMin = newRarity.GetDefenceMin();
            var range = newMax - newMin;

            var oldMax = oldRarity.GetDefenceMax();
            var oldMin = oldRarity.GetDefenceMin();

            var relNew = ScaleNumber(oldMin, oldMax, newMin, newMax, oldDef);
            var relMin = relNew - (range * 6 / 100);
            var relMax = relNew + (range * 8 / 100);

            var nDef = Fun.GetRandomValue(relMin, relMax + 1);
            if (nDef > newMax) nDef = newMax;
            if (nDef < newMin) nDef = newMin;

            return nDef;
        }

        private int GetDmgDeal(CardInfo c1, CardInfo c2)
        {
            var bonus = BodereBonus.None;
            if (c1.Info.Gender != Sex.NotSpecified && c2.Info.Gender != Sex.NotSpecified)
            {
                if (c1.Info.Gender != c2.Info.Gender) bonus = BodereBonus.Plus;
                else bonus = BodereBonus.Minus;
            }

            var dmg = GetFA(c1, c2, bonus);
            if (dmg < 1) dmg = 1;

            return (int)dmg;
        }

        public string GetDeathLog(FightHistory fight, List<PlayerInfo> players)
        {
            string deathLog = "";
            for (int i = 0; i < fight.Rounds.Count; i++)
            {
                var dead = fight.Rounds[i].Cards.Where(x => x.Hp <= 0);
                if (dead.Count() > 0)
                {
                    deathLog += $"**Runda {i + 1}**:\n";
                    foreach (var d in dead)
                    {
                        var thisCard = players.First(x => x.Cards.Any(c => c.Id == d.CardId)).Cards.First(x => x.Id == d.CardId);
                        deathLog += $"❌ {thisCard.GetString(true, false, true, true)}\n";
                    }
                    deathLog += "\n";
                }
            }
            return deathLog;
        }

        public IExecutable GetExecutableGMwK(FightHistory history, List<PlayerInfo> players)
        {
            return new Executable("GMwK", new Task(() =>
            {
                using (var db = new Database.UserContext(_config))
                {
                    bool isWinner = history.Winner != null;
                    foreach (var p in players)
                    {
                        var u = db.GetUserOrCreateAsync(p.User.Id).Result;
                        var stat = new CardPvPStats
                        {
                            Type = FightType.BattleRoyale,
                            Result = isWinner ? FightResult.Lose : FightResult.Draw
                        };

                        if (isWinner)
                        {
                            if (u.Id == history.Winner.User.Id)
                                stat.Result = FightResult.Win;
                        }

                        u.GameDeck.PvPStats.Add(stat);
                    }

                    db.SaveChanges();
                }
            }));
        }

        public async Task<FightHistory> MakeFightAsync(List<PlayerInfo> players, bool oneCard = false)
        {
            var totalCards = new List<CardInfo>();

            foreach (var player in players)
            {
                foreach (var card in player.Cards)
                {
                    card.Health = card.GetHealthWithPenalty();
                    totalCards.Add(await card.GetCardInfoAsync(_shClient));
                }
            }

            var rounds = new List<RoundInfo>();
            bool fight = true;

            while (fight)
            {
                var round = new RoundInfo();
                totalCards = totalCards.Shuffle().ToList();

                foreach (var card in totalCards)
                {
                    if (card.Card.Health <= 0)
                        continue;

                    var enemies = totalCards.Where(x => x.Card.Health > 0 && x.Card.GameDeckId != card.Card.GameDeckId);
                    if (enemies.Count() > 0)
                    {
                        var target = Fun.GetOneRandomFrom(enemies);
                        var dmg = GetDmgDeal(card, target);
                        target.Card.Health -= dmg;

                        var hpSnap = round.Cards.FirstOrDefault(x => x.CardId == target.Card.Id);
                        if (hpSnap == null)
                        {
                            round.Cards.Add(new HpSnapshot
                            {
                                CardId = target.Card.Id,
                                Hp = target.Card.Health
                            });
                        }
                        else hpSnap.Hp = target.Card.Health;

                        round.Fights.Add(new AttackInfo
                        {
                            Dmg = dmg,
                            AtkCardId = card.Card.Id,
                            DefCardId = target.Card.Id
                        });
                    }
                }

                rounds.Add(round);

                if (oneCard)
                {
                    fight = totalCards.Count(x => x.Card.Health > 0) > 1;
                }
                else
                {
                    var alive = totalCards.Where(x => x.Card.Health > 0);
                    var one = alive.FirstOrDefault();
                    if (one == null) break;

                    fight = alive.Any(x => x.Card.GameDeckId != one.Card.GameDeckId);
                }
            }

            PlayerInfo winner = null;
            var win = totalCards.Where(x => x.Card.Health > 0).FirstOrDefault();

            if (win != null)
                winner = players.FirstOrDefault(x => x.Cards.Any(c => c.GameDeckId == win.Card.GameDeckId));

            return new FightHistory(winner) { Rounds = rounds };
        }

        public Embed GetActiveList(IEnumerable<Card> list)
        {
            var embed = new EmbedBuilder()
            {
                Color = EMType.Info.Color(),
                Description = "**Twoje aktywne karty to**:\n\n",
            };

            foreach(var card in list)
                embed.Description += card.GetString(false, false, true) + "\n";

            return embed.Build();
        }

        public async Task<ICharacterInfo> GetRandomCharacterAsync()
        {
            int check = 2;
            if (CharId.IsNeedForUpdate())
            {
                var characters = await _shClient.Ex.GetAllCharactersFromAnimeAsync();
                if (!characters.IsSuccessStatusCode()) return null;

                CharId.Update(characters.Body);
            }

            ulong id = Fun.GetOneRandomFrom(CharId.Ids);
            var response = await _shClient.GetCharacterInfoAsync(id);

            while (!response.IsSuccessStatusCode())
            {
                id = Fun.GetOneRandomFrom(CharId.Ids);
                response = await _shClient.GetCharacterInfoAsync(id);

                await Task.Delay(TimeSpan.FromSeconds(2));

                if (check-- == 0)
                    return null;
            }
            return response.Body;
        }

        public async Task<string> GetWaifuProfileImageAsync(Card card, ICharacterInfo character, ITextChannel trashCh)
        {
            using (var cardImage = await _img.GetWaifuCardNoStatsAsync(character, card))
            {
                cardImage.SaveToPath($"./GOut/Profile/P{card.Id}.png");

                using (var stream = cardImage.ToPngStream())
                {
                    var fs = await trashCh.SendFileAsync(stream, $"P{card.Id}.png");
                    var im = fs.Attachments.FirstOrDefault();
                    return im.Url;
                }
            }
        }

        public Embed GetWaifuFromCharacterSearchResult(string title, IEnumerable<Card> cards, SocketGuild guild)
        {
            string contentString = "";
            foreach (var card in cards)
            {
                string favIcon = "";
                string shopIcon = "";
                string tradableIcon = card.IsTradable ? "" : "⛔";
                if (card.Tags != null)
                {
                    favIcon = card.Tags.Contains("ulubione", StringComparison.CurrentCultureIgnoreCase) ? " 💗" : "";
                    shopIcon = card.Tags.Contains("wymiana", StringComparison.CurrentCultureIgnoreCase) ? " 🔄" : "";
                }
                string tags = $"{tradableIcon}{favIcon}{shopIcon}";

                var thU = guild.GetUser(card.GameDeck.UserId);
                if (thU != null) contentString += $"{thU.Mention ?? "????"} **[{card.Id}]** {tags}\n";
            }

            return new EmbedBuilder()
            {
                Color = EMType.Info.Color(),
                Description = $"{title}\n\n{contentString.TrimToLength(1850)}"
            }.Build();
        }

        public List<Embed> GetWaifuFromCharacterTitleSearchResult(IEnumerable<Card> cards, DiscordSocketClient client)
        {
            var list = new List<Embed>();
            var characters = cards.GroupBy(x => x.Character);

            string contentString = "";
            foreach (var cardsG in characters)
            {
                string tempContentString = $"\n**{cardsG.First().GetNameWithUrl()}**\n";
                foreach (var card in cardsG)
                {
                    string favIcon = "";
                    string shopIcon = "";
                    string tradableIcon = card.IsTradable ? "" : "⛔";
                    if (card.Tags != null)
                    {
                        favIcon = card.Tags.Contains("ulubione", StringComparison.CurrentCultureIgnoreCase) ? " 💗" : "";
                        shopIcon = card.Tags.Contains("wymiana", StringComparison.CurrentCultureIgnoreCase) ? " 🔄" : "";
                    }
                    string tags = $"{tradableIcon}{favIcon}{shopIcon}";

                    var user = client.GetUser(card.GameDeckId);
                    var uString = user?.Mention ?? "????";

                    tempContentString += $"{uString}: **[{card.Id}]** {tags}\n";
                }

                if ((contentString.Length + tempContentString.Length) <= 2000)
                {
                    contentString += tempContentString;
                }
                else
                {
                    list.Add(new EmbedBuilder()
                    {
                        Color = EMType.Info.Color(),
                        Description = contentString.TrimToLength(2000)
                    }.Build());

                    contentString = tempContentString;
                }
                tempContentString = "";
            }

            list.Add(new EmbedBuilder()
            {
                Color = EMType.Info.Color(),
                Description = contentString.TrimToLength(2000)
            }.Build());

            return list;
        }

        public Embed GetBoosterPackList(SocketUser user, IList<BoosterPack> packs)
        {
            string packString = "";
            for (int i = 0; i < packs.Count(); i++)
                packString += $"**[{i + 1}]** {packs[i].Name}\n";

            return new EmbedBuilder
            {
                Color = EMType.Info.Color(),
                Description = $"{user.Mention} twoje pakiety:\n\n{packString.TrimToLength(1900)}"
            }.Build();
        }

        public Embed GetItemList(SocketUser user, List<Item> items)
        {
            string packString = "";
            for (int i = 0; i < items.Count(); i++)
                packString += $"**[{i + 1}]** {items[i].Name} x{items[i].Count}\n";

            return new EmbedBuilder
            {
                Color = EMType.Info.Color(),
                Description = $"{user.Mention} twoje przedmioty:\n\n{packString.TrimToLength(1900)}"
            }.Build();
        }

        public async Task<List<Card>> OpenBoosterPackAsync(BoosterPack pack)
        {
            var cardsFromPack = new List<Card>();

            for (int i = 0; i < pack.CardCnt; i++)
            {
                ICharacterInfo chara = null;
                if (pack.Characters.Count > 0)
                {
                    var id = pack.Characters.First();
                    if (pack.Characters.Count > 1)
                        id = Fun.GetOneRandomFrom(pack.Characters);

                    var res = await _shClient.GetCharacterInfoAsync(id.Character);
                    if (res.IsSuccessStatusCode()) chara = res.Body;
                }
                else if (pack.Title != 0)
                {
                    var res = await _shClient.Title.GetCharactersAsync(pack.Title);
                    if (res.IsSuccessStatusCode())
                    {
                        if (res.Body.Count > 0)
                        {
                            var id = Fun.GetOneRandomFrom(res.Body).CharacterId;
                            if (id.HasValue)
                            {
                                var response = await _shClient.GetCharacterInfoAsync(id.Value);
                                if (response.IsSuccessStatusCode()) chara = response.Body;
                            }
                        }
                    }
                }
                else
                {
                    chara = await GetRandomCharacterAsync();
                }

                if (chara != null)
                {
                    var newCard = GenerateNewCard(chara, pack.RarityExcludedFromPack.Select(x => x.Rarity).ToList());
                    if (pack.MinRarity != Rarity.E && i == pack.CardCnt - 1)
                        newCard = GenerateNewCard(chara, pack.MinRarity);

                    newCard.IsTradable = pack.IsCardFromPackTradable;
                    newCard.Source = pack.CardSourceFromPack;

                    cardsFromPack.Add(newCard);
                }
            }

            return cardsFromPack;
        }

        public async Task<string> GenerateAndSaveCardAsync(Card card, bool small = false)
        {
            var response = await _shClient.GetCharacterInfoAsync(card.Character);
            if (response.Code == System.Net.HttpStatusCode.NotFound) throw new Exception("Character don't exist!");
            if (!response.IsSuccessStatusCode()) throw new Exception("Shinden not responding!");

            string imageLocation = $"./GOut/Cards/{card.Id}.png";
            string sImageLocation = $"./GOut/Cards/Small/{card.Id}.png";

            using (var image = await _img.GetWaifuCardAsync(response.Body, card))
            {
                image.SaveToPath(imageLocation);
                image.SaveToPath(sImageLocation, 133, 0);
            }

            return small ? sImageLocation : imageLocation;
        }

        public void DeleteCardImageIfExist(Card card)
        {
            string imageLocation = $"./GOut/Cards/{card.Id}.png";
            string sImageLocation = $"./GOut/Cards/Small/{card.Id}.png";

            try
            {
                if (File.Exists(imageLocation))
                    File.Delete(imageLocation);

                if (File.Exists(sImageLocation))
                    File.Delete(sImageLocation);
            }
            catch (Exception) {}
        }

        private async Task<string> GetCardUrlIfExistAsync(Card card, bool defaultStr = false, bool force = false)
        {
            string imageUrl = null;
            string imageLocation = $"./GOut/Cards/{card.Id}.png";
            string sImageLocation = $"./GOut/Cards/Small/{card.Id}.png";

            if (!File.Exists(imageLocation) || !File.Exists(sImageLocation) || force)
            {
                if (card.Id != 0)
                    imageUrl = await GenerateAndSaveCardAsync(card);
            }
            else
            {
                imageUrl = imageLocation;
                if ((DateTime.Now - File.GetCreationTime(imageLocation)).TotalHours > 4)
                    imageUrl = await GenerateAndSaveCardAsync(card);
            }

            return defaultStr ? (imageUrl ?? imageLocation) : imageUrl;
        }

        public SafariImage GetRandomSarafiImage()
        {
            SafariImage dImg = null;
            var reader = new Config.JsonFileReader($"./Pictures/Poke/List.json");
            try
            {
                var images = reader.Load<List<SafariImage>>();
                dImg = Fun.GetOneRandomFrom(images);
            }
            catch (Exception) { }

            return dImg;
        }

        public async Task<string> GetSafariViewAsync(SafariImage info, ICharacterInfo character, Card card, ITextChannel trashChannel)
        {
            string uri = info != null ? info.Uri(SafariImage.Type.Truth) : SafariImage.DefaultUri(SafariImage.Type.Truth);
            var cardUri = await GetCardUrlIfExistAsync(card);

            using (var cardImage = await _img.GetWaifuCardAsync(cardUri, character, card))
            {
                int posX = info != null ? info.GetX() : SafariImage.DefaultX();
                int posY = info != null ? info.GetY() : SafariImage.DefaultY();
                using (var pokeImage = _img.GetCatchThatWaifuImage(cardImage, uri, posX, posY))
                {
                    using (var stream = pokeImage.ToJpgStream())
                    {
                        var msg = await trashChannel.SendFileAsync(stream, $"poke.jpg");
                        return msg.Attachments.First().Url;
                    }
                }
            }
        }

        public async Task<string> GetSafariViewAsync(SafariImage info, ITextChannel trashChannel)
        {
            string uri = info != null ? info.Uri(SafariImage.Type.Mystery) : SafariImage.DefaultUri(SafariImage.Type.Mystery);
            var msg = await trashChannel.SendFileAsync(uri);
            return msg.Attachments.First().Url;
        }

        public async Task<string> GetArenaViewAsync(DuelInfo info, ITextChannel trashChannel)
        {
            string url = null;
            string imageUrlWinner = await GetCardUrlIfExistAsync(info.Winner.Card, force: true);
            string imageUrlLooser = await GetCardUrlIfExistAsync(info.Loser.Card, force: true);

            DuelImage dImg = null;
            var reader = new Config.JsonFileReader($"./Pictures/Duel/List.json");
            try
            {
                var images = reader.Load<List<DuelImage>>();
                dImg = Fun.GetOneRandomFrom(images);
            }
            catch (Exception) { }

            using (var winner = await _img.GetWaifuCardAsync(imageUrlWinner, info.Winner.Info, info.Winner.Card))
            {
                using (var looser = await _img.GetWaifuCardAsync(imageUrlLooser, info.Loser.Info, info.Loser.Card))
                {
                    using (var img = _img.GetDuelCardImage(info, dImg, winner, looser))
                    {
                        using (var stream = img.ToPngStream())
                        {
                            var msg = await trashChannel.SendFileAsync(stream, $"duel.png");
                            url = msg.Attachments.First().Url;
                        }
                    }
                }
            }

            return url;
        }

        public async Task<Embed> BuildCardViewAsync(Card card, ITextChannel trashChannel, SocketUser owner)
        {
            string imageUrl = await GetCardUrlIfExistAsync(card, true);
            if (imageUrl != null)
            {
                var msg = await trashChannel.SendFileAsync(imageUrl);
                imageUrl = msg.Attachments.First().Url;
            }

            string imgUrls = $"[_obrazek_]({imageUrl})\n[_możesz zmienić obrazek tutaj_]({card.GetCharacterUrl()}/edit_crossroad)";
            string ownerString = ((owner as SocketGuildUser)?.Nickname ?? owner?.Username) ?? "????";

            return new EmbedBuilder
            {
                ImageUrl = imageUrl,
                Color = EMType.Info.Color(),
                Author = new EmbedAuthorBuilder
                {
                    Name = card.Name,
                    Url = card.GetCharacterUrl()
                },
                Footer = new EmbedFooterBuilder
                {
                    Text = $"Należy do: {ownerString}"
                },
                Description = $"{card.GetDesc()}{imgUrls}".TrimToLength(1800)
            }.Build();
        }

        public Embed GetShopView(ItemWithCost[] items)
        {
            string embedString = "";
            for (int i = 0; i < items.Length; i++)
                embedString+= $"**[{i + 1}]** _{items[i].Item.Name}_ - {items[i].Cost} TC\n";

            return new EmbedBuilder
            {
                Color = EMType.Info.Color(),
                Description = $"**Sklepik:**\n\n{embedString}".TrimToLength(2000)
            }.Build();
        }

        public Embed GetItemShopInfo(ItemWithCost item)
        {
            return new EmbedBuilder
            {
                Color = EMType.Info.Color(),
                Description =$"**{item.Item.Name}**\n_{item.Item.Type.Desc()}_",
            }.Build();
        }
    }
}