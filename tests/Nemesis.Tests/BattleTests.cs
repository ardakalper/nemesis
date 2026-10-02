using System.Text.Json;
using Nemesis;
using Xunit;

namespace Nemesis.Tests;

public class BattleTests
{
    // A fight with a clear floor and units placed by hand.
    static Battle Arena(string villainArchetype = "brute", params string[] heroIds)
    {
        var roster = Content.Roster();
        var heroes = (heroIds.Length > 0 ? heroIds : new[] { "bulwark" }).Select(id => roster.First(h => h.Id == id)).ToList();
        var v = Rivalry.Create(new Rng(7), new List<Villain>(), 1, 1, villainArchetype);
        v.Traits.Clear();
        var b = Battle.Create(new BattleSetup { Heroes = heroes, Bosses = new List<Villain> { v }, Seed = 3 });
        for (int x = 0; x < Battle.W; x++) for (int y = 0; y < Battle.H; y++) b.Tiles[x, y] = TileKind.Floor;
        return b;
    }
    static Unit Boss(Battle b) => b.Bosses.First();
    static Unit H(Battle b, string id) => b.Heroes.First(h => h.Hero.Id == id);

    [Fact]
    public void Rng_is_deterministic_and_in_range()
    {
        var a = new Rng(42); var c = new Rng(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextU64(), c.NextU64());
        var r = new Rng(1);
        for (int i = 0; i < 1000; i++) { int n = r.Range(3, 7); Assert.InRange(n, 3, 7); }
        Assert.Equal(Rng.Hash("nemesis"), Rng.Hash("nemesis"));
        Assert.NotEqual(Rng.Hash("nemesis"), Rng.Hash("Nemesis"));
    }

    [Fact]
    public void Maps_are_connected_and_sides_start_apart()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var u = new Universe();
            var s = Campaign.NewSeries(u, seed);
            var b = Campaign.StartBattle(u, s, new[] { "bulwark", "kestrel", "noonday" });
            var floors = new List<Pos>();
            for (int x = 0; x < Battle.W; x++) for (int y = 0; y < Battle.H; y++) if (b.Tiles[x, y] == TileKind.Floor) floors.Add(new Pos(x, y));
            // flood from a hero
            var seen = new HashSet<Pos> { b.Heroes.First().Pos };
            var q = new Queue<Pos>(seen);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = new Pos(c.X + d.Item1, c.Y + d.Item2);
                    if (Battle.InBounds(n) && b.Tiles[n.X, n.Y] == TileKind.Floor && seen.Add(n)) q.Enqueue(n);
                }
            }
            Assert.Equal(floors.Count, seen.Count);
            Assert.All(b.Heroes, h => Assert.True(h.Pos.X <= 1));
            Assert.All(b.Units.Where(x => x.Side == Side.Villains), v => Assert.True(v.Pos.X >= Battle.W - 2));
            Assert.Equal(b.Units.Count, b.Units.Select(x => x.Pos).Distinct().Count());
        }
    }

    [Fact]
    public void Movement_respects_range_walls_and_foes()
    {
        var b = Arena();
        var h = H(b, "bulwark");
        h.Pos = new Pos(0, 0); Boss(b).Pos = new Pos(2, 0);
        b.Tiles[0, 1] = TileKind.Wall;
        var r = b.Reachable(h);
        Assert.All(r, kv => Assert.True(kv.Value <= h.Move));
        Assert.DoesNotContain(new Pos(0, 1), r.Keys);       // wall
        Assert.DoesNotContain(new Pos(2, 0), r.Keys);       // occupied by the boss
        Assert.DoesNotContain(new Pos(3, 0), r.Keys);       // can't walk through a foe, and the wall closes the other way
        Assert.True(b.MoveUnit(h, new Pos(1, 0)));
        Assert.False(b.MoveUnit(h, new Pos(1, 1)), "one move per turn");
    }

    [Fact]
    public void Damage_counts_resistances_weaknesses_marks_armour_cover_and_grudges()
    {
        var b = Arena("brute", "noonday", "bulwark");
        var blaster = H(b, "noonday"); var boss = Boss(b);
        var bolt = Content.Powers["bolt"];
        blaster.Pos = new Pos(0, 2); boss.Pos = new Pos(4, 2);
        Assert.Equal(3, b.DamageOf(blaster, bolt, boss, blaster.Pos, false));
        boss.Resists.Add(DamageType.Energy);
        Assert.Equal(1, b.DamageOf(blaster, bolt, boss, blaster.Pos, false)); // 3 / 2
        boss.Resists.Clear(); boss.Weaks.Add(DamageType.Energy);
        Assert.Equal(5, b.DamageOf(blaster, bolt, boss, blaster.Pos, false)); // 3 × 1.5, rounded up
        boss.Weaks.Clear(); boss.Mark = 1;
        Assert.Equal(5, b.DamageOf(blaster, bolt, boss, blaster.Pos, false)); // +2 tagged
        boss.Mark = 0;
        b.Tiles[3, 2] = TileKind.Cover;
        Assert.Equal(2, b.DamageOf(blaster, bolt, boss, blaster.Pos, false)); // −1 in cover
        boss.Armor = 2;
        var brick = H(b, "bulwark");
        Assert.Equal(2, b.DamageOf(brick, Content.Powers["haymaker"], boss, new Pos(3, 2), false)); // 4 − 2 armour
        boss.GrudgeHeroId = "bulwark";
        Assert.Equal(6, b.DamageOf(boss, Content.Powers["smash"], brick, new Pos(1, 0), false)); // 4 + 2 grudge
    }

    [Fact]
    public void Stun_skips_a_turn_burn_ticks_and_a_ward_cleanses()
    {
        var b = Arena("brute", "reverie", "vesper");
        var mind = H(b, "reverie"); var boss = Boss(b); var mystic = H(b, "vesper");
        mind.Pos = new Pos(1, 1); boss.Pos = new Pos(4, 1); mystic.Pos = new Pos(0, 4);
        Assert.True(b.UsePower(mind, "dominate", boss.Pos));
        Assert.Equal(2, boss.Stun);
        var before = boss.Pos;
        b.EndHeroTurn();
        Assert.Equal(before, boss.Pos);                   // stunned: didn't move
        Assert.DoesNotContain(b.Log, e => e.Kind == "hit" && e.Actor == boss.Id);
        mind.Burn = 2; mind.Stun = 1;
        Assert.True(b.UsePower(mystic, "ward", mind.Pos));
        Assert.Equal(0, mind.Burn);
        Assert.Equal(0, mind.Stun);
        Assert.Equal(4, mind.Shield);
        boss.Burn = 1; var hp = boss.Hp;
        b.EndHeroTurn();
        Assert.True(boss.Hp < hp, "burn hurts at the start of its turn");
    }

    [Fact]
    public void Knockback_slams_into_walls_and_barrels_explode_in_chains()
    {
        var b = Arena("brute", "bulwark");
        var brick = H(b, "bulwark"); var boss = Boss(b);
        brick.Pos = new Pos(3, 2); boss.Pos = new Pos(4, 2); b.Tiles[5, 2] = TileKind.Wall;
        int hp = boss.Hp;
        Assert.True(b.UsePower(brick, "haymaker", boss.Pos));
        Assert.Equal(new Pos(4, 2), boss.Pos);
        Assert.Equal(hp - 4 - 2, boss.Hp);               // haymaker + slam
        Assert.Contains(b.Log, e => e.Kind == "slam");

        var c = Arena("brute", "noonday");
        var blaster = H(c, "noonday"); var boss2 = Boss(c);
        blaster.Pos = new Pos(0, 0); boss2.Pos = new Pos(6, 4);
        c.Tiles[5, 3] = TileKind.Barrel; c.Tiles[6, 3] = TileKind.Barrel;
        hp = boss2.Hp;
        blaster.Momentum = 0;
        Assert.True(c.UsePower(blaster, "nova", new Pos(4, 3)));
        Assert.Equal(TileKind.Floor, c.Tiles[5, 3]);
        Assert.Equal(TileKind.Floor, c.Tiles[6, 3]);     // the second barrel went up too
        Assert.Equal(2, c.Log.Count(e => e.Kind == "explode"));
        Assert.True(boss2.Hp <= hp - 3);
    }

    [Fact]
    public void Tagged_or_stunned_villains_cannot_escape_and_low_bosses_try()
    {
        int escapes = 0, blocked = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var b = Battle.Create(new BattleSetup { Heroes = Content.Roster().Take(1).ToList(), Bosses = new List<Villain> { Rivalry.Create(new Rng(seed), new List<Villain>(), 1, 1, "brute") }, Seed = seed });
            var boss = Boss(b);
            boss.Hp = 2; boss.Pos = new Pos(8, 5); b.Heroes.First().Pos = new Pos(0, 0);
            if (seed % 2 == 0) boss.Mark = 2;
            b.EndHeroTurn();
            if (seed % 2 == 0) { Assert.False(boss.Fled); blocked++; }
            else if (boss.Fled) { escapes++; Assert.Equal(BattleResult.Escaped, b.Result); }
        }
        Assert.Equal(15, blocked);
        Assert.InRange(escapes, 5, 15);
    }

    [Fact]
    public void Team_ups_need_two_adjacent_heroes_and_the_cannonball_throws()
    {
        var b = Arena("brute", "bulwark", "kestrel");
        var brick = H(b, "bulwark"); var fast = H(b, "kestrel"); var boss = Boss(b);
        brick.Pos = new Pos(0, 0); fast.Pos = new Pos(3, 3); boss.Pos = new Pos(2, 1);
        Assert.False(b.CanTeamUp(brick, fast));
        fast.Pos = new Pos(1, 0);
        Assert.True(b.CanTeamUp(brick, fast));
        Assert.Equal("Human Cannonball", Battle.TeamUpName(brick, fast));
        int hp = boss.Hp, dmg = b.TeamUpDamage(brick, fast, boss);
        Assert.True(b.TeamUp(brick, fast, boss.Pos));
        Assert.Equal(hp - dmg, boss.Hp);
        Assert.Equal(1, fast.Pos.Dist(boss.Pos));        // thrown next to the target
        Assert.True(brick.Acted && fast.Acted);
        Assert.False(b.CanTeamUp(brick, fast));
    }

    [Fact]
    public void Signatures_cost_momentum_and_cooldowns_recharge()
    {
        var b = Arena("brute", "noonday");
        var blaster = H(b, "noonday"); var boss = Boss(b);
        blaster.Pos = new Pos(0, 0); boss.Pos = new Pos(5, 3);
        Assert.False(b.Usable(blaster, blaster.PowerById("supernova")));
        blaster.Momentum = 3;
        Assert.True(b.UsePower(blaster, "supernova", boss.Pos));
        Assert.Equal(1, blaster.Momentum);               // −3, +1 for landing it
        var nova = blaster.PowerById("nova");
        nova.Cooldown = 2;
        Assert.False(b.Usable(blaster, nova));
    }

    [Fact]
    public void Auto_battles_always_end_and_villains_fight_back()
    {
        var results = new Dictionary<BattleResult, int>();
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var u = new Universe();
            var s = Campaign.NewSeries(u, seed);
            var b = Play.Auto(Campaign.StartBattle(u, s, new[] { "bulwark", "kestrel", "vesper" }));
            Assert.True(b.Over);
            Assert.True(b.Round <= Battle.MaxRounds + 1);
            results[b.Result] = results.GetValueOrDefault(b.Result) + 1;
            Assert.Contains(b.Log, e => e.Kind == "hit" && b.Get(e.Actor)?.Side == Side.Villains);
        }
        Assert.True(results.GetValueOrDefault(BattleResult.Captured) > 0);
        Assert.True(results.GetValueOrDefault(BattleResult.Escaped) > 0);
    }
}

public class RivalryTests
{
    static Villain Clone(Villain v) => System.Text.Json.JsonSerializer.Deserialize<Villain>(System.Text.Json.JsonSerializer.Serialize(v));

    static Battle Fight(Villain v, params string[] heroIds)
    {
        var roster = Content.Roster();
        var b = Battle.Create(new BattleSetup { Heroes = heroIds.Select(id => roster.First(h => h.Id == id)).ToList(), Bosses = new List<Villain> { v }, Henchmen = new List<string> { "goon" }, Seed = 5 });
        for (int x = 0; x < Battle.W; x++) for (int y = 0; y < Battle.H; y++) b.Tiles[x, y] = TileKind.Floor;
        return b;
    }

    [Fact]
    public void Villains_are_born_with_names_origins_and_unique_names()
    {
        var gallery = new List<Villain>();
        var rng = new Rng(9);
        for (int i = 0; i < 30; i++) gallery.Add(Rivalry.Create(rng, gallery, 1, 1));
        Assert.Equal(gallery.Count, gallery.Select(g => g.Name).Distinct().Count());
        // the first few of each kind don't share a core word ("Matchstick" and "Lady Matchstick")
        var firstSix = new List<Villain>();
        for (int i = 0; i < 6; i++) firstSix.Add(Rivalry.Create(rng, firstSix, 1, 1, "pyro"));
        Assert.Equal(6, firstSix.Select(v => Content.Cores["pyro"].First(c => v.Name == c || v.Name.EndsWith(" " + c))).Distinct().Count());
        Assert.All(gallery, v =>
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Origin));
            Assert.Contains(v.Name, v.Origin);
            Assert.Contains(v.Archetype, Content.VillainArchetypes);
            Assert.Equal("debut", v.History[0].Kind);
        });
    }

    [Fact]
    public void An_escaped_villain_adapts_to_what_hurt_it_and_holds_a_grudge()
    {
        var v = Rivalry.Create(new Rng(1), new List<Villain>(), 1, 1, "brute");
        v.Traits.Clear();
        v.Traits.Add("weak-fire");
        var b = Fight(v, "noonday", "bulwark");
        var boss = b.Bosses.First(); var blaster = b.Heroes.First(h => h.Hero.Id == "noonday");
        blaster.Pos = new Pos(0, 0); boss.Pos = new Pos(4, 0);
        boss.Hp = 9;
        Assert.True(b.UsePower(blaster, "bolt", boss.Pos));
        boss.Hp = 1; boss.DamageByType[DamageType.Fire] = 9; boss.DamageFrom[blaster.Id] = 9;
        boss.Out = boss.Fled = true;
        b.Result = BattleResult.Escaped;
        var gallery = new List<Villain> { v };
        var notes = Rivalry.Aftermath(b, gallery, 1, 2, new Rng(3));
        Assert.Contains("fireproof", v.Traits);
        Assert.DoesNotContain("weak-fire", v.Traits);   // the weakness is gone
        Assert.Equal("noonday", v.GrudgeHeroId);
        Assert.Equal(2, v.Rank);
        Assert.Equal(1, v.Escapes);
        Assert.NotEmpty(v.Scars);
        Assert.Contains(notes, n => n.Kind == "adapted");
        Assert.Contains(notes, n => n.Kind == "grudge");
        // and it says so
        var roster = Content.Roster();
        var line = Rivalry.TauntIntro(v, roster, new Rng(1));
        Assert.True(line.Contains("Noonday") || line.Contains("fire") || line.Contains(v.Scars[0]), line);
        Assert.Contains("FIRE CAN'T STOP", Rivalry.Headline(new List<Villain> { v }, roster, false));
    }

    [Fact]
    public void A_henchman_who_floors_a_hero_gets_a_name()
    {
        var v = Rivalry.Create(new Rng(2), new List<Villain>(), 1, 1, "mastermind");
        var b = Fight(v, "kestrel");
        var hero = b.Heroes.First(); var goon = b.Units.First(x => x.IsHench);
        hero.Hp = 1; hero.Pos = new Pos(3, 3); goon.Pos = new Pos(4, 3);
        b.Bosses.First().Pos = new Pos(8, 0);
        b.Turn = Side.Villains;
        Assert.True(b.UsePower(goon, "punch", hero.Pos));
        Assert.Equal(BattleResult.Defeat, b.Result);
        var gallery = new List<Villain> { v };
        var notes = Rivalry.Aftermath(b, gallery, 1, 1, new Rng(4));
        var promoted = gallery.Single(g => g.Promoted);
        Assert.Equal("the Kestrel-Breaker", promoted.Epithet);
        Assert.Equal("kestrel", promoted.GrudgeHeroId);
        Assert.Equal(goon.Name, promoted.PromotedFrom);
        Assert.Contains("hero-breaker", promoted.Traits);
        Assert.Contains(notes, n => n.Kind == "promoted");
        Assert.Equal(1, b.Heroes.First().Hero.Injured);
        var intro = Rivalry.TauntIntro(promoted, Content.Roster(), new Rng(1));
        Assert.Contains(goon.Name, intro);
        var title = Rivalry.IssueTitle(new List<Villain> { promoted }, false, new Rng(2));
        Assert.True(title.StartsWith("Who Is") || title.StartsWith("The Rise of"), title);
    }

    [Fact]
    public void Captured_villains_go_to_prison_and_come_back_harder()
    {
        var v = Rivalry.Create(new Rng(3), new List<Villain>(), 1, 1, "pyro");
        var b = Fight(v, "bulwark");
        var boss = b.Bosses.First();
        boss.Hp = 0; boss.Out = true; b.Result = BattleResult.Captured;
        Rivalry.Aftermath(b, new List<Villain> { v }, 1, 1, new Rng(1));
        Assert.Equal(VillainStatus.Captured, v.Status);
        // nobody walks out the very next issue
        for (ulong seed = 1; seed <= 200; seed++) Assert.False(Rivalry.TryBreakout(v, new Rng(seed), 1, 2));
        Assert.Contains(Enumerable.Range(1, 200), seed => Rivalry.TryBreakout(Clone(v), new Rng((ulong)seed), 1, 3));
        Rivalry.ForceBreakout(v, new Rng(1), 1, 2);
        Assert.Equal(VillainStatus.AtLarge, v.Status);
        Assert.Contains("hardened", v.Traits);
        Assert.Equal("Unchained", v.Epithet);
        Assert.Equal("breakout", v.History.Last().Kind);
    }
}

public class CampaignTests
{
    [Fact]
    public void A_new_series_can_open_with_an_old_enemy()
    {
        int opened = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var u = new Universe();
            Play.Series(u, seed);
            var old = u.Gallery.Where(v => v.Status == VillainStatus.AtLarge).Select(v => v.Id).ToHashSet();
            var s = Campaign.NewSeries(u, seed + 100);
            if (s.Next.VillainIds.Any(old.Contains)) opened++;
        }
        Assert.True(opened >= 3, $"only {opened} of 12 second series opened with a returning villain");
    }

    [Fact]
    public void A_series_runs_six_issues_and_the_rogues_gallery_remembers()
    {
        int returning = 0, finales = 0, teamUps = 0, issues = 0, promotions = 0;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var u = new Universe();
            var s = Play.Series(u, seed, (b, ser) =>
            {
                issues++;
                if (b.Bosses.Any(x => x.Villain.Encounters > 0)) returning++;
                if (ser.Next.Finale) { finales++; if (b.Bosses.Count() == 2) teamUps++; }
            });
            Assert.True(s.Over);
            Assert.InRange(u.Covers.Count, 2, Campaign.IssuesPerSeries);
            Assert.Equal(u.Covers.Count, u.Covers.Select(c => c.Number).Distinct().Count());
            Assert.All(u.Covers, c => { Assert.False(string.IsNullOrWhiteSpace(c.Title)); Assert.NotEmpty(c.VillainIds); });
            promotions += u.Gallery.Count(v => v.Promoted);
            if (s.Won) Assert.Equal(Campaign.IssuesPerSeries, u.Covers.Count);
        }
        Assert.True(returning > issues / 5, $"villains come back: {returning} of {issues}");
        Assert.True(finales > 15 && teamUps > finales / 2, $"finales {finales}, team-ups {teamUps}");
        Assert.True(promotions > 5);
    }

    [Fact]
    public void The_gallery_carries_over_into_the_next_series()
    {
        var u = new Universe();
        Play.Series(u, 11);
        var before = u.Gallery.Select(v => v.Id).ToHashSet();
        bool met = false;
        Play.Series(u, 12, (b, s) => met |= b.Bosses.Any(x => before.Contains(x.Villain.Id)));
        Assert.Equal(2, u.SeriesPlayed);
        Assert.True(met || u.Gallery.All(v => v.Status != VillainStatus.AtLarge), "an old villain shows up again");
    }

    [Fact]
    public void Injured_heroes_sit_out_and_upgrades_apply()
    {
        var u = new Universe();
        var s = Campaign.NewSeries(u, 5);
        s.Roster[0].Injured = 1;
        Assert.Throws<ArgumentException>(() => Campaign.StartBattle(u, s, new[] { s.Roster[0].Id }));
        s.Offers = new List<UpgradeOffer> { new UpgradeOffer { HeroId = "kestrel", UpgradeId = "hp" } };
        Assert.True(Campaign.TakeUpgrade(s, 0));
        Assert.Equal(4, s.Roster.First(h => h.Id == "kestrel").BonusHp);
        Assert.Empty(s.Offers);
    }

    [Fact]
    public void Same_seed_same_series()
    {
        string Run(ulong seed)
        {
            var u = new Universe();
            Play.Series(u, seed);
            return string.Join("|", u.Covers.Select(c => $"{c.Number}:{c.Title}:{c.Outcome}")) + string.Join(",", u.Gallery.Select(v => v.FullName));
        }
        Assert.Equal(Run(77), Run(77));
        Assert.NotEqual(Run(77), Run(78));
    }

    [Fact]
    public void The_universe_saves_and_loads_as_json()
    {
        var u = new Universe();
        Play.Series(u, 3);
        Campaign.NewSeries(u, 4);
        var opts = new JsonSerializerOptions { IncludeFields = true };
        var json = JsonSerializer.Serialize(u, opts);
        var back = JsonSerializer.Deserialize<Universe>(json, opts);
        Assert.Equal(u.Gallery.Count, back.Gallery.Count);
        Assert.Equal(u.Gallery[0].History.Count, back.Gallery[0].History.Count);
        Assert.Equal(u.Covers.Count, back.Covers.Count);
        Assert.Equal(u.Current.Next.Title, back.Current.Next.Title);
        Assert.Equal(JsonSerializer.Serialize(back, opts), json);
        // and a battle can start from the loaded state
        var b = Campaign.StartBattle(back, back.Current, Campaign.Available(back.Current).Take(3).Select(h => h.Id).ToList());
        Assert.False(b.Over);
    }

    [Fact]
    public void Balance_stays_in_a_sensible_window()
    {
        int issues = 0, defeats = 0, rounds = 0;
        for (ulong seed = 100; seed < 140; seed++)
        {
            var u = new Universe();
            Play.Series(u, seed, (b, s) => { issues++; rounds += b.Round; if (b.Result == BattleResult.Defeat) defeats++; });
        }
        double lossRate = (double)defeats / issues, avgRounds = (double)rounds / issues;
        Assert.InRange(lossRate, 0.1, 0.45);
        Assert.InRange(avgRounds, 3.0, 7.0);
    }
}
