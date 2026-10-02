using System;
using System.Collections.Generic;
using System.Linq;

namespace Nemesis
{
    /// <summary>The next issue, decided before the player picks a team: who, where, and what the cover says.</summary>
    public class IssuePlan
    {
        public int Number { get; set; }
        public string Title { get; set; }
        public string Headline { get; set; }
        public string PlaceId { get; set; }
        public List<string> VillainIds { get; set; } = new List<string>();
        public List<string> Henchmen { get; set; } = new List<string>();
        public List<string> Breakouts { get; set; } = new List<string>();
        public string Intro { get; set; }
        public ulong Seed { get; set; }
        public bool Finale { get; set; }
    }

    public class UpgradeOffer
    {
        public string HeroId { get; set; }
        public string UpgradeId { get; set; }
    }

    /// <summary>A run: six issues with one team name, a roster of six heroes and three hope.</summary>
    public class Series
    {
        public int Number { get; set; }
        public string TeamName { get; set; }
        public ulong Seed { get; set; }
        public int Issue { get; set; } = 1;
        public int Hope { get; set; } = 3;
        public int Score { get; set; }
        public List<Hero> Roster { get; set; } = new List<Hero>();
        public IssuePlan Next { get; set; }
        public List<UpgradeOffer> Offers { get; set; } = new List<UpgradeOffer>();
        public List<Note> LastNotes { get; set; } = new List<Note>();
        public bool Over { get; set; }
        public bool Won { get; set; }
    }

    /// <summary>Everything that persists: the rogues gallery outlives every series, and so do the covers.</summary>
    public class Universe
    {
        public int Version { get; set; } = 1;
        public List<Villain> Gallery { get; set; } = new List<Villain>();
        public List<IssueRecord> Covers { get; set; } = new List<IssueRecord>();
        public int SeriesPlayed { get; set; }
        public Series Current { get; set; }
        public int BestScore { get; set; }
        public Villain Find(string id) => Gallery.FirstOrDefault(v => v.Id == id);
    }

    public static class Campaign
    {
        public const int IssuesPerSeries = 6;
        public const int TeamSize = 3;

        public static Series NewSeries(Universe u, ulong seed, string teamName = null)
        {
            var rng = new Rng(seed);
            var s = new Series
            {
                Number = u.SeriesPlayed + 1,
                Seed = seed,
                TeamName = string.IsNullOrWhiteSpace(teamName) ? rng.Pick(Content.TeamNames) : teamName.Trim(),
                Roster = Content.Roster(),
            };
            u.SeriesPlayed++;
            u.Current = s;
            s.Next = Plan(u, s);
            return s;
        }

        static Rng IssueRng(Series s, string label) => new Rng(s.Seed ^ ((ulong)s.Issue * 0x9E3779B97F4A7C15UL)).Fork(label);

        /// <summary>Decides the next issue: breakouts, who returns, who debuts.</summary>
        public static IssuePlan Plan(Universe u, Series s)
        {
            var rng = IssueRng(s, "plan");
            var plan = new IssuePlan { Number = s.Issue, Seed = rng.NextU64(), Finale = s.Issue == IssuesPerSeries };
            foreach (var v in u.Gallery.Where(v => v.Status == VillainStatus.Captured).ToList())
                if (Rivalry.TryBreakout(v, rng, s.Number, s.Issue)) plan.Breakouts.Add(v.Id);

            var atLarge = u.Gallery.Where(v => v.Status == VillainStatus.AtLarge).ToList();
            var bosses = new List<Villain>();
            if (plan.Finale)
            {
                // the biggest nemesis comes back for the finale, even from prison
                var top = u.Gallery.Where(v => v.Status != VillainStatus.Retired).OrderByDescending(v => v.Threat).FirstOrDefault();
                if (top == null) { top = Rivalry.Create(rng, u.Gallery, s.Number, s.Issue); top.Rank = 3; u.Gallery.Add(top); }
                if (top.Status == VillainStatus.Captured) { Rivalry.ForceBreakout(top, rng, s.Number, s.Issue); plan.Breakouts.Add(top.Id); }
                bosses.Add(top);
                var second = u.Gallery.Where(v => v.Status == VillainStatus.AtLarge && v != top).OrderByDescending(v => v.Threat).FirstOrDefault();
                if (second != null) bosses.Add(second);
            }
            else
            {
                var returning = atLarge.Where(v => v.Encounters > 0 || v.Promoted || plan.Breakouts.Contains(v.Id)).ToList();
                // from the second issue on, or from the first issue of a later series, old faces come back
                if ((s.Issue > 1 || s.Number > 1) && returning.Count > 0 && rng.Chance(0.7))
                {
                    // weighted by how much of a nemesis each one has become
                    int total = returning.Sum(v => v.Threat + 1), roll = rng.Range(1, total);
                    foreach (var v in returning) { roll -= v.Threat + 1; if (roll <= 0) { bosses.Add(v); break; } }
                }
                if (bosses.Count == 0)
                {
                    var fresh = Rivalry.Create(rng, u.Gallery, s.Number, s.Issue);
                    u.Gallery.Add(fresh);
                    bosses.Add(fresh);
                }
            }
            plan.VillainIds = bosses.Select(b => b.Id).ToList();
            var place = rng.Pick(Content.Places);
            plan.PlaceId = place.Id;
            int hench = Math.Min(5, 2 + s.Issue / 2) - (bosses.Count - 1);
            for (int i = 0; i < hench; i++)
                plan.Henchmen.Add(s.Issue >= 3 && rng.Chance(0.3) ? "bruiser" : rng.Chance(0.5) ? "gunman" : "goon");
            plan.Title = Rivalry.IssueTitle(bosses, plan.Finale, rng);
            plan.Headline = Rivalry.Headline(bosses, s.Roster, plan.Finale);
            plan.Intro = Rivalry.TauntIntro(bosses[0], s.Roster, rng);
            return plan;
        }

        public static PlaceDef PlaceOf(IssuePlan p) => Content.Places.First(x => x.Id == p.PlaceId);
        public static IEnumerable<Hero> Available(Series s) => s.Roster.Where(h => h.Injured == 0);

        public static Battle StartBattle(Universe u, Series s, IList<string> heroIds)
        {
            if (s.Over) throw new InvalidOperationException("the series is over");
            var team = heroIds.Distinct().Select(id => s.Roster.First(h => h.Id == id)).ToList();
            if (team.Count < 1 || team.Count > TeamSize) throw new ArgumentException($"pick 1 to {TeamSize} heroes");
            if (team.Any(h => h.Injured > 0)) throw new ArgumentException("an injured hero can't fight");
            var plan = s.Next;
            return Battle.Create(new BattleSetup
            {
                Heroes = team,
                Bosses = plan.VillainIds.Select(u.Find).ToList(),
                Henchmen = plan.Henchmen,
                Place = PlaceOf(plan),
                Issue = s.Issue,
                Seed = plan.Seed,
            });
        }

        /// <summary>Closes an issue: the nemesis system runs, the cover is filed, and the next issue is planned.</summary>
        public static List<Note> Finish(Universe u, Series s, Battle b)
        {
            if (!b.Over) throw new InvalidOperationException("the battle isn't over");
            var plan = s.Next;
            var rng = IssueRng(s, "aftermath");
            foreach (var h in s.Roster) if (h.Injured > 0) h.Injured--; // the bench heals first; new injuries come next
            var notes = Rivalry.Aftermath(b, u.Gallery, s.Number, s.Issue, rng);
            var bosses = plan.VillainIds.Select(u.Find).ToList();
            string outcome = b.Result == BattleResult.Captured ? "captured" : b.Result == BattleResult.Escaped ? "escaped" : "defeat";
            u.Covers.Add(new IssueRecord
            {
                Series = s.Number, Number = s.Issue, Title = plan.Title, Headline = plan.Headline, Place = PlaceOf(plan).Name, TeamName = s.TeamName,
                VillainIds = plan.VillainIds.ToList(), HeroIds = b.Heroes.Select(h => h.Hero.Id).ToList(), Outcome = outcome, Seed = (int)(plan.Seed & 0x7fffffff),
            });
            s.Score += b.Result == BattleResult.Captured ? 100 + 50 * bosses.Max(v => v.Rank) : b.Result == BattleResult.Escaped ? 60 : 0;
            s.Score += 10 * b.Units.Count(x => x.Side == Side.Villains && x.Out && !x.Fled);
            if (b.Result == BattleResult.Defeat)
            {
                s.Hope--;
                notes.Add(new Note { Kind = "hope", Text = s.Hope > 0 ? $"The city's hope falters. {s.Hope} left." : "The city has given up on you." });
            }
            s.LastNotes = notes;
            s.Issue++;
            if (s.Hope <= 0 || s.Issue > IssuesPerSeries)
            {
                s.Over = true;
                s.Won = s.Hope > 0;
                if (s.Won) s.Score += 200 + 100 * s.Hope;
                u.BestScore = Math.Max(u.BestScore, s.Score);
                s.Next = null;
                s.Offers.Clear();
                return notes;
            }
            // nobody can fight? patch up whoever is least hurt
            if (Available(s).Count() < TeamSize)
                foreach (var h in s.Roster.Where(h => h.Injured > 0).OrderBy(h => h.Injured).Take(TeamSize - Available(s).Count())) h.Injured = 0;
            s.Offers = Offer(s, rng);
            s.Next = Plan(u, s);
            return notes;
        }

        static List<UpgradeOffer> Offer(Series s, Rng rng)
        {
            var heroes = s.Roster.ToList();
            rng.Shuffle(heroes);
            var offers = new List<UpgradeOffer>();
            foreach (var h in heroes.Take(3))
            {
                var options = Content.Upgrades.Where(x => !(x.Id == "cooldown" && h.CooldownCut >= 1) && !(x.Id == "momentum" && h.StartMomentum >= 2)).ToList();
                offers.Add(new UpgradeOffer { HeroId = h.Id, UpgradeId = rng.Pick(options).Id });
            }
            return offers;
        }

        public static bool TakeUpgrade(Series s, int index)
        {
            if (index < 0 || index >= s.Offers.Count) return false;
            var o = s.Offers[index];
            var h = s.Roster.First(x => x.Id == o.HeroId);
            switch (o.UpgradeId)
            {
                case "hp": h.BonusHp += 4; break;
                case "move": h.BonusMove += 1; break;
                case "damage": h.BonusDamage += 1; break;
                case "momentum": h.StartMomentum = 2; break;
                case "cooldown": h.CooldownCut = 1; break;
            }
            h.Upgrades.Add(o.UpgradeId);
            s.Offers.Clear();
            return true;
        }
    }
}
