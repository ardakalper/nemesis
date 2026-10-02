using System;
using System.Collections.Generic;
using System.Linq;

namespace Nemesis
{
    /// <summary>One thing that happened after a fight, for the aftermath page.</summary>
    public class Note
    {
        public string Kind;      // captured, escaped, adapted, grudge, scarred, rank, triumph, ko-hero, promoted, injured, levelup, breakout
        public string VillainId;
        public string HeroId;
        public string Text;
    }

    /// <summary>The nemesis system. Villains are born with an origin, remember every fight, adapt to what hurt
    /// them, hold grudges against the hero who hurt them most, scar, rise in rank, break out of prison — and a
    /// nameless henchman who puts a hero down gets promoted to a villain with a name.</summary>
    public static class Rivalry
    {
        public const string Prison = "the Iron Vault";

        // ---------- birth ----------
        public static Villain Create(Rng rng, IEnumerable<Villain> gallery, int series, int issue, string archetype = null)
        {
            var taken = new HashSet<string>(gallery.Select(g => g.Name));
            archetype ??= rng.Pick(Content.VillainArchetypes);
            // a new villain shouldn't sound like an old one: prefer a core word nobody in the gallery has used
            var all = Content.Cores[archetype];
            var fresh = all.Where(c => !taken.Any(n => n == c || n.EndsWith(" " + c))).ToArray();
            var cores = fresh.Length > 0 ? fresh : all;
            string name = null;
            for (int i = 0; i < 40 && (name == null || taken.Contains(name)); i++)
            {
                var core = rng.Pick(cores);
                name = rng.Chance(0.65) ? $"{rng.Pick(Content.Prefixes)} {core}" : core;
            }
            var real = $"{rng.Pick(Content.FirstNames)} {rng.Pick(Content.LastNames)}";
            var v = new Villain
            {
                Id = $"v{series}-{issue}-{rng.Range(1000, 9999)}",
                Name = name,
                Archetype = archetype,
                RealName = real,
                Rank = issue >= 4 ? 2 : 1,
                Origin = $"Once a {rng.Pick(Content.Jobs[archetype])}, {real} {rng.Pick(Content.Accidents[archetype])}. Now {name} {rng.Pick(Content.Goals[archetype])}.",
                Look = Vary(Content.VillainLooks[archetype], rng),
                DebutSeries = series,
                DebutIssue = issue,
            };
            if (rng.Chance(0.6)) v.Traits.Add(rng.Pick(new[] { "weak-fire", "weak-cold", "weak-energy", "weak-psychic" }));
            if (rng.Chance(0.35)) v.Traits.Add(rng.Pick(new[] { "quick", "armored", "slippery" }));
            v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "debut", Text = $"First appeared in issue #{issue}." });
            return v;
        }

        static readonly string[][] Palettes =
        {
            new[] { "#4d4d4d", "#d62828" }, new[] { "#2b2d42", "#e5e5e5" }, new[] { "#3c096c", "#9ef01a" }, new[] { "#9d0208", "#ffd60a" },
            new[] { "#1d3557", "#a8dadc" }, new[] { "#111111", "#f9f871" }, new[] { "#2d6a4f", "#ff4d6d" }, new[] { "#5f0f40", "#0f4c5c" },
        };
        static Look Vary(Look b, Rng rng)
        {
            var pal = rng.Chance(0.5) ? new[] { b.Primary, b.Secondary } : rng.Pick(Palettes);
            return new Look { Primary = pal[0], Secondary = pal[1], Skin = rng.Pick(new[] { "#f1c9a5", "#c68b62", "#8d5a3b", "#e8b896", "#a8714f", "#b5e3b0" }), Mask = b.Mask, Emblem = rng.Range(0, 5), Hair = rng.Range(0, 4), Cape = b.Cape, Extra = b.Extra };
        }

        // A henchman who knocked out a hero gets a name.
        public static Villain Promote(Rng rng, IEnumerable<Villain> gallery, Unit hench, Hero victim, string boss, int series, int issue, string place)
        {
            var taken = new HashSet<string>(gallery.Select(g => g.Name));
            var nick = Content.HenchNicknames.Where(n => !taken.Contains(n)).DefaultIfEmpty($"Goon {rng.Range(10, 99)}").First();
            var def = Content.Henchmen[hench.HenchType];
            var real = $"{rng.Pick(Content.FirstNames)} {rng.Pick(Content.LastNames)}";
            var v = new Villain
            {
                Id = $"p{series}-{issue}-{rng.Range(1000, 9999)}",
                Name = nick,
                Epithet = $"the {victim.Name}-Breaker",
                Archetype = def.PromotesTo,
                RealName = real,
                Rank = 1,
                Promoted = true,
                PromotedFrom = hench.Name,
                GrudgeHeroId = victim.Id,
                Origin = $"{real} was just another {def.Name.ToLowerInvariant()} on {boss}'s payroll, until the night at {place} when {victim.Name} hit the floor. Now the papers have a name for them.",
                Look = new Look { Primary = hench.Look.Primary, Secondary = rng.Pick(new[] { "#d62828", "#ffd60a", "#00b2c8" }), Skin = rng.Pick(new[] { "#f1c9a5", "#c68b62", "#8d5a3b" }), Mask = 1, Emblem = rng.Range(0, 5), Hair = rng.Range(0, 4), Extra = 0 },
                DebutSeries = series,
                DebutIssue = issue,
                HeroKOs = 1,
            };
            v.Traits.Add("hero-breaker");
            v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "promoted", HeroId = victim.Id, Place = place, Text = $"As {hench.Name}, knocked out {victim.Name} at {place}." });
            return v;
        }

        // ---------- after a fight ----------
        static readonly Dictionary<DamageType, string> ScarBy = new Dictionary<DamageType, string>
        {
            [DamageType.Fire] = "burn scars down one side of the face",
            [DamageType.Energy] = "a forked scar like a lightning strike",
            [DamageType.Cold] = "two frostbitten fingers",
            [DamageType.Psychic] = "a twitch in the left eye that never stops",
            [DamageType.Physical] = "a nose that never set straight",
        };

        /// <summary>Applies what happened in a battle to the villains and heroes involved, and says what changed.</summary>
        public static List<Note> Aftermath(Battle b, List<Villain> gallery, int series, int issue, Rng rng)
        {
            var notes = new List<Note>();
            var place = b.Place.Name;
            Hero HeroOf(int id) => b.Get(id)?.Hero;

            foreach (var u in b.Bosses.ToList())
            {
                var v = u.Villain;
                v.Encounters++;
                // the hero who hurt them most, and how
                var top = u.DamageFrom.Where(kv => HeroOf(kv.Key) != null).OrderByDescending(kv => kv.Value).FirstOrDefault();
                var topHero = top.Value > 0 ? HeroOf(top.Key) : null;
                var topType = u.DamageByType.OrderByDescending(kv => kv.Value).Select(kv => (DamageType?)kv.Key).FirstOrDefault();

                foreach (var h in b.Heroes.Where(h => h.KOdBy == u.Id))
                {
                    v.HeroKOs++;
                    v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "ko-hero", HeroId = h.Hero.Id, Place = place, Text = $"Knocked out {h.Name} at {place}." });
                    notes.Add(new Note { Kind = "ko-hero", VillainId = v.Id, HeroId = h.Hero.Id, Text = $"{v.Name} put {h.Name} on the floor. It won't be forgotten." });
                    if (!v.Has("hero-breaker")) v.Traits.Add("hero-breaker");
                }

                if (b.Result == BattleResult.Defeat)
                {
                    v.Wins++;
                    RankUp(v, notes);
                    if (!v.Has("triumphant")) v.Traits.Add("triumphant");
                    v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "triumph", Place = place, Text = $"Beat the team at {place}." });
                    notes.Add(new Note { Kind = "triumph", VillainId = v.Id, Text = $"{v.Name} beat you at {place}. The city is talking about nothing else." });
                }
                else if (u.Fled)
                {
                    v.Escapes++;
                    v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "escaped", HeroId = topHero?.Id, Place = place, Text = $"Escaped the team at {place}." });
                    notes.Add(new Note { Kind = "escaped", VillainId = v.Id, Text = $"{v.Name} got away. They will be back, and they will have learned." });
                    // adapt to whatever hurt them most
                    if (topType.HasValue && u.DamageByType[topType.Value] >= 3)
                    {
                        var trait = Content.ResistTraitFor(topType.Value);
                        if (!v.Has(trait))
                        {
                            v.Traits.Add(trait);
                            v.Traits.RemoveAll(t => Content.Traits[t].Weak == topType.Value);
                            v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "adapted", Place = place, Text = $"Came back with {Content.Traits[trait].Name.ToLowerInvariant()}." });
                            notes.Add(new Note { Kind = "adapted", VillainId = v.Id, Text = $"Next time {v.Name} will be ready for {topType.Value.ToString().ToLowerInvariant()}: {Content.Traits[trait].Name}." });
                        }
                    }
                    if (topHero != null && top.Value >= 3 && v.GrudgeHeroId != topHero.Id)
                    {
                        v.GrudgeHeroId = topHero.Id;
                        notes.Add(new Note { Kind = "grudge", VillainId = v.Id, HeroId = topHero.Id, Text = $"{v.Name} now holds a grudge against {topHero.Name}." });
                    }
                    if (u.Hp <= u.MaxHp * 0.2 && topType.HasValue && v.Scars.Count < 3 && !v.Scars.Contains(ScarBy[topType.Value]))
                    {
                        v.Scars.Add(ScarBy[topType.Value]);
                        notes.Add(new Note { Kind = "scarred", VillainId = v.Id, Text = $"{v.Name} escaped with {ScarBy[topType.Value]}." });
                    }
                    RankUp(v, notes);
                }
                else
                {
                    v.Captures++;
                    v.Status = VillainStatus.Captured;
                    v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "captured", HeroId = topHero?.Id, Place = place, Text = $"Captured at {place}{(topHero != null ? $", mostly thanks to {topHero.Name}" : "")}." });
                    notes.Add(new Note { Kind = "captured", VillainId = v.Id, Text = $"{v.Name} goes to {Prison}. For now." });
                    if (topHero != null && v.GrudgeHeroId == null) v.GrudgeHeroId = topHero.Id;
                }
            }

            // a henchman who floored a hero gets a name (one per fight, the first)
            var bossName = b.Bosses.FirstOrDefault()?.Name ?? "somebody";
            foreach (var h in b.Heroes.Where(h => h.Out && h.KOdBy >= 0))
            {
                var by = b.Get(h.KOdBy);
                if (by == null || !by.IsHench) continue;
                var p = Promote(rng, gallery, by, h.Hero, bossName, series, issue, place);
                gallery.Add(p);
                notes.Add(new Note { Kind = "promoted", VillainId = p.Id, HeroId = h.Hero.Id, Text = $"{by.Name} put {h.Name} down — and the papers have given them a name: {p.FullName}." });
                break;
            }

            // heroes
            foreach (var h in b.Heroes)
            {
                var hero = h.Hero;
                if (h.Out)
                {
                    hero.TimesDown++;
                    hero.Injured = b.Get(h.KOdBy)?.IsBoss == true ? 2 : 1;
                    notes.Add(new Note { Kind = "injured", HeroId = hero.Id, Text = $"{hero.Name} is hurt and sits out the next {(hero.Injured == 1 ? "issue" : $"{hero.Injured} issues")}." });
                }
                hero.Xp += b.Result == BattleResult.Defeat ? 1 : 2;
                while (hero.Xp >= hero.Level * 3)
                {
                    hero.Xp -= hero.Level * 3;
                    hero.Level++;
                    notes.Add(new Note { Kind = "levelup", HeroId = hero.Id, Text = $"{hero.Name} reaches level {hero.Level} (+1 health)." });
                }
            }
            return notes;
        }

        static void RankUp(Villain v, List<Note> notes)
        {
            if (v.Rank >= 3) return;
            v.Rank++;
            notes.Add(new Note { Kind = "rank", VillainId = v.Id, Text = $"{v.Name} is now {RankName(v.Rank)}." });
        }

        public static string RankName(int rank) => rank >= 3 ? "an archvillain" : rank == 2 ? "a supervillain" : "a lieutenant";

        // Captured villains don't stay captured.
        public static bool TryBreakout(Villain v, Rng rng, int series, int issue)
        {
            if (v.Status != VillainStatus.Captured) return false;
            // at least one issue behind bars before the walls start leaking
            var caught = Last(v, "captured");
            if (caught != null && caught.Series == series && caught.Issue >= issue - 1) return false;
            if (!rng.Chance(0.15 + 0.1 * v.Rank)) return false;
            ForceBreakout(v, rng, series, issue);
            return true;
        }

        public static void ForceBreakout(Villain v, Rng rng, int series, int issue)
        {
            v.Status = VillainStatus.AtLarge;
            var upgrade = !v.Has("hardened") ? "hardened" : !v.Has("cybernetic") ? "cybernetic" : null;
            if (upgrade != null) v.Traits.Add(upgrade);
            if (string.IsNullOrEmpty(v.Epithet) || v.Epithet == "Reborn") v.Epithet = "Unchained";
            v.History.Add(new HistoryEntry { Series = series, Issue = issue, Kind = "breakout", Place = Prison, Text = $"Broke out of {Prison}{(upgrade != null ? $" with {Content.Traits[upgrade].Name.ToLowerInvariant()}" : "")}." });
        }

        // ---------- what they say ----------
        static Hero Named(List<Hero> heroes, string id) => heroes.FirstOrDefault(h => h != null && h.Id == id);
        static HistoryEntry Last(Villain v, string kind) => v.History.LastOrDefault(h => h.Kind == kind);

        public static string TauntIntro(Villain v, List<Hero> present, Rng rng)
        {
            var options = new List<string>();
            var grudge = Named(present, v.GrudgeHeroId);
            if (v.Promoted && v.Encounters == 0 && grudge != null)
                return $"Remember me, {grudge.Name}? I was \"{v.PromotedFrom}\" to you. Learn the new name: {v.Name}.";
            if (grudge != null)
            {
                var lastPlace = v.History.LastOrDefault(h => h.HeroId == grudge.Id)?.Place;
                options.Add(lastPlace != null ? $"{grudge.Name}! I've thought about you every night since {lastPlace}." : $"{grudge.Name}. Every night, I see your face.");
                options.Add($"Step aside, the rest of you. My business is with {grudge.Name}.");
            }
            var adapted = Last(v, "adapted");
            if (adapted != null)
            {
                var resist = v.Traits.Select(t => Content.Traits[t]).FirstOrDefault(t => t.Resist.HasValue);
                if (resist != null) options.Add($"Go on, try the {resist.Resist.Value.ToString().ToLowerInvariant()} again. I had this made with you in mind.");
            }
            if (Last(v, "breakout") != null && v.Status == VillainStatus.AtLarge) options.Add($"{Prison}? I've had tighter shoes.");
            if (v.Scars.Count > 0) options.Add($"You gave me {v.Scars[0]}. I've come to return the favour.");
            if (v.Wins > 0) options.Add("Back for another beating? I hoped you would be.");
            var ko = Last(v, "ko-hero");
            var koHero = ko != null ? Named(present, ko.HeroId) : null;
            if (koHero != null) options.Add($"How's the back, {koHero.Name}? Still feel it when it rains?");
            if (options.Count > 0) return rng.Pick(options);
            switch (v.Archetype)
            {
                case "brute": return rng.Pick(new[] { "I'm gonna fold you up like a lawn chair!", "Big words. Small heroes." });
                case "marksman": return rng.Pick(new[] { "Don't move. Actually, do. It's more fun.", "I never miss. Ask anyone. Oh, you can't." });
                case "mastermind": return rng.Pick(new[] { "Everything you're about to do, I planned for.", "Pawns. All of you." });
                case "pyro": return rng.Pick(new[] { "Smell that? That's the city warming up.", "Let's turn up the heat!" });
                case "cryo": return rng.Pick(new[] { "Chill. This will all be over soon.", "The cold doesn't care how brave you are." });
                case "blitz": return rng.Pick(new[] { "Catch me if you — oh. You can't.", "Blink and you'll miss it. You will blink." });
                default: return rng.Pick(new[] { "Pick a card. Any card. Wrong!", "Is this the real me? Guess!" });
            }
        }

        public static string TauntHit(Villain v, Hero h, Rng rng)
        {
            var place = v.History.LastOrDefault(e => e.HeroId == h.Id)?.Place;
            return rng.Pick(place != null
                ? new[] { $"That's for {place}, {h.Name}!", $"Remember {place}? I do." }
                : new[] { $"Feel that, {h.Name}? There's more.", $"I've waited so long for this, {h.Name}." });
        }

        public static string TauntHurt(Villain v, Rng rng) => rng.Pick(v.Escapes > 0
            ? new[] { "I've slipped out of worse than this!", "You won't hold me. You never do." }
            : new[] { "This isn't over!", "You'll pay for that!", "Not like this… not like this!" });

        public static string TauntEscape(Villain v, Rng rng) => rng.Pick(new[] { "Until next time, heroes!", "You'll never take me alive!", "Remember this face!" });

        // ---------- the cover ----------
        public static string IssueTitle(List<Villain> vs, bool finale, Rng rng)
        {
            var v = vs[0];
            if (finale && vs.Count > 1) return $"{vs[0].Name} & {vs[1].Name}: Team-Up of Terror!";
            if (finale) return rng.Pick(new[] { $"The Last Stand of {v.Name}", $"{v.Name}: The Final Reckoning" });
            if (v.Promoted && v.Encounters == 0) return rng.Pick(new[] { $"Who Is {v.Name}?", $"The Rise of {v.Name}" });
            if (Last(v, "breakout") != null && v.History.Last().Kind == "breakout") return rng.Pick(new[] { "Breakout!", $"{v.Name} Unchained" });
            if (v.Encounters > 0) return rng.Pick(new[] { $"The Return of {v.Name}!", $"{v.Name} Strikes Back!", $"{v.Name} Again!" });
            var word = v.Name.Split(' ').Last();
            return rng.Pick(new[] { $"Enter {v.Name}!", $"The {word} Gambit", $"Night of {v.Name}" });
        }

        public static string Headline(List<Villain> vs, List<Hero> roster, bool finale)
        {
            var v = vs[0];
            var grudge = Named(roster, v.GrudgeHeroId);
            if (finale) return "FINAL ISSUE! EVERYTHING ENDS HERE!";
            if (v.Promoted && v.Encounters == 0) return $"FROM NAMELESS GOON TO {v.Epithet?.ToUpperInvariant() ?? "SUPERVILLAIN"}!";
            var adapted = v.Traits.Select(t => Content.Traits[t]).LastOrDefault(t => t.Resist.HasValue);
            if (v.Encounters > 0 && adapted != null) return $"{v.Name.ToUpperInvariant()} IS BACK — AND {adapted.Resist.Value.ToString().ToUpperInvariant()} CAN'T STOP {(v.Name.StartsWith("The ") ? "IT" : "THEM")}!";
            if (grudge != null) return $"{v.Name.ToUpperInvariant()} WANTS {grudge.Name.ToUpperInvariant()}. NOBODY ELSE WILL DO.";
            if (v.Status == VillainStatus.AtLarge && Last(v, "breakout") != null) return $"NO CELL CAN HOLD {v.Name.ToUpperInvariant()}!";
            return $"INTRODUCING {v.Name.ToUpperInvariant()}!";
        }
    }
}
