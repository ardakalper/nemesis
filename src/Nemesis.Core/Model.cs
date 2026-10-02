using System.Collections.Generic;

namespace Nemesis
{
    public enum DamageType { Physical, Energy, Fire, Cold, Psychic }
    public enum Side { Heroes, Villains }
    public enum VillainStatus { AtLarge, Captured, Retired }
    public enum Targeting { Enemy, Ally, Self, Tile }

    /// <summary>How a figure is drawn: costume colours and a few shape choices. The UI turns this into ink.</summary>
    public class Look
    {
        public string Primary { get; set; } = "#1f6fd1";
        public string Secondary { get; set; } = "#ffe200";
        public string Skin { get; set; } = "#f1c9a5";
        public int Mask { get; set; }    // 0 none, 1 domino, 2 cowl, 3 helmet, 4 visor, 5 hood
        public int Emblem { get; set; }  // index into the emblem shapes
        public int Hair { get; set; }    // 0 bald … 4
        public bool Cape { get; set; }
        public int Extra { get; set; }   // horns, antenna, crest… (villains)
    }

    /// <summary>A hero on the roster. Grows over a series: levels, upgrades, injuries.</summary>
    public class Hero
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Archetype { get; set; }
        public string Tagline { get; set; }
        public Look Look { get; set; } = new Look();
        public int Level { get; set; } = 1;
        public int Xp { get; set; }
        public int Injured { get; set; }          // issues left on the bench
        public int BonusHp { get; set; }
        public int BonusMove { get; set; }
        public int BonusDamage { get; set; }
        public int StartMomentum { get; set; }
        public int CooldownCut { get; set; }
        public List<string> Upgrades { get; set; } = new List<string>();
        public int KOs { get; set; }              // villains and henchmen this hero has put down, this series
        public int TimesDown { get; set; }
    }

    /// <summary>A line in a villain's file: everything that ever happened between them and the heroes.</summary>
    public class HistoryEntry
    {
        public int Series { get; set; }
        public int Issue { get; set; }
        public string Kind { get; set; }   // debut, escaped, captured, breakout, promoted, triumph, ko-hero, adapted, scarred
        public string HeroId { get; set; }
        public string Place { get; set; }
        public string Text { get; set; }
    }

    /// <summary>A villain in the rogues gallery. Villains persist across issues and across series, and change
    /// according to what happened to them: they adapt to what hurt them, hold grudges, rise in rank, scar.</summary>
    public class Villain
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Epithet { get; set; }        // "the Hero-Breaker", "Reborn"…
        public string Archetype { get; set; }
        public string RealName { get; set; }
        public string Origin { get; set; }
        public int Rank { get; set; } = 1;         // 1 lieutenant, 2 supervillain, 3 archvillain
        public VillainStatus Status { get; set; } = VillainStatus.AtLarge;
        public List<string> Traits { get; set; } = new List<string>();
        public string GrudgeHeroId { get; set; }
        public List<string> Scars { get; set; } = new List<string>();
        public Look Look { get; set; } = new Look();
        public List<HistoryEntry> History { get; set; } = new List<HistoryEntry>();
        public int Encounters { get; set; }
        public int Escapes { get; set; }
        public int Captures { get; set; }
        public int Wins { get; set; }              // fights the heroes lost
        public int HeroKOs { get; set; }
        public bool Promoted { get; set; }         // rose from a nameless henchman
        public string PromotedFrom { get; set; }   // "Goon #3"
        public int DebutSeries { get; set; }
        public int DebutIssue { get; set; }

        public string FullName => string.IsNullOrEmpty(Epithet) ? Name : $"{Name}, {Epithet}";

        /// <summary>How much of a nemesis this villain has become.</summary>
        public int Threat => Rank * 3 + Escapes * 2 + HeroKOs * 2 + Wins * 2 + (GrudgeHeroId != null ? 2 : 0) + (Promoted ? 1 : 0);

        public bool Has(string trait) => Traits.Contains(trait);
    }

    /// <summary>One finished issue, as it will appear on its cover and in the series log.</summary>
    public class IssueRecord
    {
        public int Series { get; set; }
        public int Number { get; set; }
        public string Title { get; set; }
        public string Headline { get; set; }
        public string Place { get; set; }
        public string TeamName { get; set; }
        public List<string> VillainIds { get; set; } = new List<string>();
        public List<string> HeroIds { get; set; } = new List<string>();
        public string Outcome { get; set; }       // captured, escaped, defeat
        public int Seed { get; set; }
    }
}
