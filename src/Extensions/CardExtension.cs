﻿#pragma warning disable 1591

using System;
using System.Threading.Tasks;
using Sanakan.Database.Models;
using Sanakan.Services.PocketWaifu;

namespace Sanakan.Extensions
{
    public static class CardExtension
    {
        public static string GetString(this Card card, bool withoutId = false, bool withUpgrades = false, bool nameAsUrl = false)
        {
            string idStr = withoutId ? "" : $"**[{card.Id}]** ";
            string upgCnt = withUpgrades ? $"_(U:{card.UpgradesCnt})_" : "";
            string name = nameAsUrl ? $"[{card.Name}]({card.GetCharacterUrl()})" : card.Name; 
            
            return $"{idStr} {name} **{card.Rarity}** ❤{card.Health} 🔥{card.Attack} 🛡{card.Defence} {upgCnt}";
        }

        public static string GetCharacterUrl(this Card card) => Shinden.API.Url.GetCharacterURL(card.Character);

        public static string GetDesc(this Card card)
        {
            return $"*{card.Title ?? "????"}*\n\n"
                + $"**Życie[P]:** {card.GetHealthWithPenalty()}\n"
                + $"**Relacja:** {card.GetAffectionString()}\n"
                + $"**Doświadczenie:** {card.ExpCnt.ToString("F")}\n"
                + $"**Dostępne ulepszenia:** {card.UpgradesCnt}\n\n"
                + $"**W klatce:** {card.InCage.GetYesNo()}\n"
                + $"**Aktywna:** {card.Active.GetYesNo()}\n"
                + $"**Możliwość wymiany:** {card.IsTradable.GetYesNo()}\n\n"
                + $"**Arena:** **W**: {card?.ArenaStats?.Wins ?? 0} **L**: {card?.ArenaStats?.Loses ?? 0} **D**: {card?.ArenaStats?.Draws ?? 0}\n\n"
                + $"**WID:** {card.Id}\n"
                + $"**Pochodzenie:** {card.Source.GetString()}\n\n";
        }

        public static int GetHealthWithPenalty(this Card card)
        {
            var percent = card.Affection * 5d / 100d;
            var newHealth = (int) (card.Health + (card.Health * percent));
            return newHealth < 10 ? 10 : newHealth;
        }

        public static string GetString(this CardSource source)
        {
            switch (source)
            {
                case CardSource.Activity:        return "Aktywność";
                case CardSource.Safari:          return "Safari";
                case CardSource.Shop:            return "Sklepik";
                case CardSource.GodIntervention: return "Czity";
                case CardSource.Api:             return "Nieznane";
                case CardSource.Merge:           return "Stara baza";

                default:
                case CardSource.Other: return "Inne";
            }
        }

        public static string GetYesNo(this bool b) => b ? "Tak" : "Nie";

        public static bool IsUnusable(this Card card) => card.GetAffectionString() == "Nienawiść";

        public static string GetAffectionString(this Card card)
        {
            if (card.Affection <= -5) return "Nienawiść";
            if (card.Affection <= -4) return "Zawiść";
            if (card.Affection <= -3) return "Wrogość";
            if (card.Affection <= -2) return "Złośliwość";
            if (card.Affection <= -1) return "Chłodność";
            if (card.Affection >= 50) return "Obsesyjna miłość";
            if (card.Affection >= 5)  return "Miłość";
            if (card.Affection >= 4)  return "Zauroczenie";
            if (card.Affection >= 3)  return "Przyjaźń";
            if (card.Affection >= 2)  return "Fascynacja";
            if (card.Affection >= 1)  return "Zaciekawienie";
            return "Obojętność";
        }

        public static double ExpToUpgrade(this Card card)
        {
            switch (card.Rarity)
            {
                case Rarity.SSS: return 10000;
                case Rarity.SS:  return 100;

                default: return 30;
            }
        }

        public static int GetAttackMin(this Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.SSS: return 92;
                case Rarity.SS:  return 90;
                case Rarity.S:   return 80;
                case Rarity.A:   return 65;
                case Rarity.B:   return 50;
                case Rarity.C:   return 32;
                case Rarity.D:   return 20;

                case Rarity.E:
                default: return 1;
            }
        }

        public static int GetDefenceMin(this Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.SSS: return 80;
                case Rarity.SS:  return 77;
                case Rarity.S:   return 68;
                case Rarity.A:   return 60;
                case Rarity.B:   return 50;
                case Rarity.C:   return 32;
                case Rarity.D:   return 15;
                
                case Rarity.E:
                default: return 1;
            }
        }

        public static int GetHealthMin(this Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.SSS: return 100;
                case Rarity.SS:  return 90;
                case Rarity.S:   return 80;
                case Rarity.A:   return 70;
                case Rarity.B:   return 60;
                case Rarity.C:   return 50;
                case Rarity.D:   return 40;

                case Rarity.E:
                default: return 30;
            }
        }

        public static int GetHealthMax(this Card card)
        {
            return 300 - (card.Attack + card.Defence);
        }

        public static int GetAttackMax(this Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.SSS: return 100;
                case Rarity.SS:  return 99;
                case Rarity.S:   return 96;
                case Rarity.A:   return 87;
                case Rarity.B:   return 84;
                case Rarity.C:   return 68;
                case Rarity.D:   return 50;
                
                case Rarity.E:
                default: return 35;
            }
        }

        public static int GetDefenceMax(this Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.SSS: return 92;
                case Rarity.SS:  return 90;
                case Rarity.S:   return 78;
                case Rarity.A:   return 75;
                case Rarity.B:   return 70;
                case Rarity.C:   return 65;
                case Rarity.D:   return 53;
                
                case Rarity.E:
                default: return 38;
            }
        }

        public static async Task<CardInfo> GetCardInfoAsync(this Card card, Shinden.ShindenClient client)
        {
            var response = await client.GetCharacterInfoAsync(card.Character);
            if (!response.IsSuccessStatusCode())
                throw new Exception($"Couldn't get card info!");

            return new CardInfo
            {
                Info = response.Body,
                Card = card
            };
        }
    }
}
