using System;
using System.Collections.Generic;
using System.Linq;

namespace Nemesis
{
    public enum TileKind { Floor, Cover, Wall, Barrel }
    public enum BattleResult { Ongoing, Captured, Escaped, Defeat }

    public struct Pos : IEquatable<Pos>
    {
        public int X, Y;
        public Pos(int x, int y) { X = x; Y = y; }
        public bool Equals(Pos o) => X == o.X && Y == o.Y;
        public override bool Equals(object o) => o is Pos p && Equals(p);
        public override int GetHashCode() => X * 31 + Y;
        public static bool operator ==(Pos a, Pos b) => a.Equals(b);
        public static bool operator !=(Pos a, Pos b) => !a.Equals(b);
        /// <summary>Chebyshev distance: diagonals count as one step for range.</summary>
        public int Dist(Pos o) => Math.Max(Math.Abs(X - o.X), Math.Abs(Y - o.Y));
        public override string ToString() => $"({X},{Y})";
    }

    public class PowerState
    {
        public Power Def;
        public int Cooldown;
        public PowerState(Power def) { Def = def; }
    }

    public class Unit
    {
        public int Id;
        public string Name;
        public Side Side;
        public string Archetype;
        public Hero Hero;           // heroes
        public Villain Villain;     // bosses
        public string HenchType;    // henchmen
        public bool IsBoss => Villain != null;
        public bool IsTurret;
        public bool IsHench => HenchType != null;
        public int MaxHp, Hp, Move, DamageBonus, MeleeBonus, Armor;
        public List<PowerState> Powers = new List<PowerState>();
        public Pos Pos;
        public int Stun, Burn, Shield, Mark, Momentum;
        public bool Moved, Acted, StunnedNow, Out, Fled;
        public HashSet<DamageType> Resists = new HashSet<DamageType>();
        public HashSet<DamageType> Weaks = new HashSet<DamageType>();
        public bool StunImmune;
        public double EscapeBonus;
        public string GrudgeHeroId;
        public Look Look;
        public int KOdBy = -1;
        public Dictionary<int, int> DamageFrom = new Dictionary<int, int>();
        public Dictionary<DamageType, int> DamageByType = new Dictionary<DamageType, int>();
        public bool Alive => !Out;
        public bool CanAct => !Out && !StunnedNow && !Acted;
        public bool CanMove => !Out && !StunnedNow && !Moved && Move > 0;
        public PowerState PowerById(string id) => Powers.FirstOrDefault(p => p.Def.Id == id);
    }

    public class BattleEvent
    {
        public string Kind;     // turn, move, hit, miss, ko, stun, burn, mark, shield, heal, knockback, slam, explode, summon, escape, escape-fail, taunt, teamup, power, surrender, end
        public int Actor = -1, Target = -1;
        public int Amount;
        public string Text;
        public string Sfx;
        public string Power;
        public Pos From, To;
        public DamageType Type;
        public int Round;
    }

    /// <summary>What a battle needs to start: who fights, where, and when in the series.</summary>
    public class BattleSetup
    {
        public List<Hero> Heroes = new List<Hero>();
        public List<Villain> Bosses = new List<Villain>();
        public List<string> Henchmen = new List<string>();
        public PlaceDef Place = Content.Places[0];
        public int Issue = 1;
        public ulong Seed = 1;
    }

    /// <summary>A fight on a 9×6 grid. Heroes act in any order (move once, act once each), then the villains'
    /// side plays. Damage is deterministic, so the UI can preview every hit; randomness only decides AI ties,
    /// escapes and the map.</summary>
    public class Battle
    {
        public const int W = 9, H = 6;
        public const int MaxRounds = 10;
        public TileKind[,] Tiles = new TileKind[W, H];
        public List<Unit> Units = new List<Unit>();
        public List<BattleEvent> Log = new List<BattleEvent>();
        public int Round = 1;
        public Side Turn = Side.Heroes;
        public BattleResult Result = BattleResult.Ongoing;
        public PlaceDef Place;
        public int Issue;
        public Rng Rng;
        int nextId;
        readonly HashSet<int> taunted = new HashSet<int>();

        public IEnumerable<Unit> Heroes => Units.Where(u => u.Side == Side.Heroes && !u.IsTurret);
        public IEnumerable<Unit> Bosses => Units.Where(u => u.IsBoss);
        public Unit UnitAt(Pos p) => Units.FirstOrDefault(u => u.Alive && u.Pos == p);
        public Unit Get(int id) => Units.FirstOrDefault(u => u.Id == id);
        public static bool InBounds(Pos p) => p.X >= 0 && p.Y >= 0 && p.X < W && p.Y < H;
        public TileKind Tile(Pos p) => Tiles[p.X, p.Y];
        public bool Over => Result != BattleResult.Ongoing;

        // ---------- setup ----------
        public static Battle Create(BattleSetup s)
        {
            var b = new Battle { Place = s.Place, Issue = s.Issue, Rng = new Rng(s.Seed) };
            b.MakeMap();
            var heroSpots = new List<Pos>();
            for (int y = 0; y < H; y++) for (int x = 0; x < 2; x++) heroSpots.Add(new Pos(x, y));
            var villainSpots = new List<Pos>();
            for (int y = 0; y < H; y++) for (int x = W - 2; x < W; x++) villainSpots.Add(new Pos(x, y));
            b.Rng.Shuffle(heroSpots);
            b.Rng.Shuffle(villainSpots);
            foreach (var h in s.Heroes) b.Spawn(b.MakeHero(h), heroSpots);
            foreach (var v in s.Bosses) b.Spawn(b.MakeBoss(v), villainSpots);
            foreach (var t in s.Henchmen) b.Spawn(b.MakeHench(t), villainSpots);
            b.Emit(new BattleEvent { Kind = "turn", Text = "Round 1" });
            return b;
        }

        void Spawn(Unit u, List<Pos> spots)
        {
            var p = spots.First(q => Tile(q) == TileKind.Floor && UnitAt(q) == null);
            u.Pos = p;
            Units.Add(u);
        }

        void MakeMap()
        {
            for (int attempt = 0; ; attempt++)
            {
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) Tiles[x, y] = TileKind.Floor;
                void Scatter(TileKind k, int n)
                {
                    for (int i = 0, tries = 0; i < n && tries < 200; tries++)
                    {
                        var p = new Pos(Rng.Range(2, W - 3), Rng.Range(0, H - 1));
                        if (Tiles[p.X, p.Y] != TileKind.Floor) continue;
                        Tiles[p.X, p.Y] = k; i++;
                    }
                }
                Scatter(TileKind.Wall, Place.Walls);
                Scatter(TileKind.Cover, Place.Cover);
                Scatter(TileKind.Barrel, Place.Barrels);
                // the two sides must be able to reach each other
                var reach = Flood(new Pos(0, 0));
                int floors = 0, reached = 0;
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (Tiles[x, y] == TileKind.Floor) { floors++; if (reach.Contains(new Pos(x, y))) reached++; }
                if (reached == floors || attempt > 50) break;
            }
        }

        HashSet<Pos> Flood(Pos start)
        {
            var seen = new HashSet<Pos> { start };
            var q = new Queue<Pos>(); q.Enqueue(start);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var n in Neighbours4(c))
                    if (Tile(n) == TileKind.Floor && seen.Add(n)) q.Enqueue(n);
            }
            return seen;
        }

        static IEnumerable<Pos> Neighbours4(Pos p)
        {
            var d = new[] { new Pos(1, 0), new Pos(-1, 0), new Pos(0, 1), new Pos(0, -1) };
            foreach (var o in d) { var n = new Pos(p.X + o.X, p.Y + o.Y); if (InBounds(n)) yield return n; }
        }
        static IEnumerable<Pos> Neighbours8(Pos p)
        {
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = new Pos(p.X + dx, p.Y + dy);
                if (InBounds(n)) yield return n;
            }
        }

        Unit MakeHero(Hero h)
        {
            var a = Content.Archetypes[h.Archetype];
            var u = new Unit
            {
                Id = nextId++, Name = h.Name, Side = Side.Heroes, Archetype = h.Archetype, Hero = h, Look = h.Look,
                MaxHp = a.Hp + h.BonusHp + (h.Level - 1), Move = a.Move + h.BonusMove, DamageBonus = h.BonusDamage, Momentum = h.StartMomentum,
            };
            u.Hp = u.MaxHp;
            foreach (var id in a.Powers) u.Powers.Add(new PowerState(Content.Powers[id]));
            return u;
        }

        Unit MakeBoss(Villain v)
        {
            var a = Content.Archetypes[v.Archetype];
            var u = new Unit
            {
                Id = nextId++, Name = v.Name, Side = Side.Villains, Archetype = v.Archetype, Villain = v, Look = v.Look,
                MaxHp = a.Hp + 6 + 4 * (v.Rank - 1) + 3 * (Issue - 1), Move = a.Move, DamageBonus = v.Rank - 1, GrudgeHeroId = v.GrudgeHeroId,
            };
            foreach (var tid in v.Traits)
            {
                if (!Content.Traits.TryGetValue(tid, out var t)) continue;
                if (t.Resist.HasValue) u.Resists.Add(t.Resist.Value);
                if (t.Weak.HasValue) u.Weaks.Add(t.Weak.Value);
                u.Armor += t.Armor; u.Move += t.Move; u.MeleeBonus += t.Melee; u.DamageBonus += t.Damage; u.MaxHp += t.Hp;
                u.EscapeBonus += t.Escape; u.StunImmune |= t.StunImmune;
            }
            u.Hp = u.MaxHp;
            foreach (var id in a.Powers) u.Powers.Add(new PowerState(Content.Powers[id]));
            return u;
        }

        int henchCount;
        Unit MakeHench(string type)
        {
            var d = Content.Henchmen[type];
            henchCount++;
            var u = new Unit
            {
                Id = nextId++, Name = $"{d.Name} #{henchCount}", Side = Side.Villains, Archetype = type, HenchType = type,
                MaxHp = d.Hp + Issue / 2, Move = d.Move, DamageBonus = Issue >= 4 ? 1 : 0,
                Look = new Look { Primary = type == "gunman" ? "#3a3a3a" : "#5a4632", Secondary = "#999999", Mask = 1 },
            };
            u.Hp = u.MaxHp;
            u.Powers.Add(new PowerState(Content.Powers[d.Power]));
            return u;
        }

        // ---------- queries ----------
        bool Walkable(Pos p) => InBounds(p) && Tile(p) == TileKind.Floor;

        /// <summary>Tiles the unit can end its move on, with the number of steps.</summary>
        public Dictionary<Pos, int> Reachable(Unit u)
        {
            var dist = new Dictionary<Pos, int> { [u.Pos] = 0 };
            if (!u.CanMove) return dist;
            var q = new Queue<Pos>(); q.Enqueue(u.Pos);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (dist[c] >= u.Move) continue;
                foreach (var n in Neighbours4(c))
                {
                    if (!Walkable(n) || dist.ContainsKey(n)) continue;
                    var o = UnitAt(n);
                    if (o != null && o.Side != u.Side) continue; // can pass allies, not foes
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            foreach (var p in dist.Keys.ToList()) if (p != u.Pos && UnitAt(p) != null) dist.Remove(p);
            return dist;
        }

        /// <summary>Walls block sight; cover and barrels don't (they give cover instead).</summary>
        public bool LineOfSight(Pos a, Pos b)
        {
            int x0 = a.X, y0 = a.Y, x1 = b.X, y1 = b.Y;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                if ((x0 != a.X || y0 != a.Y) && (x0 != b.X || y0 != b.Y) && Tiles[x0, y0] == TileKind.Wall) return false;
                if (x0 == x1 && y0 == y1) return true;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        bool InCover(Pos target, Pos from)
        {
            int sx = Math.Sign(from.X - target.X), sy = Math.Sign(from.Y - target.Y);
            foreach (var d in new[] { new Pos(sx, 0), new Pos(0, sy), new Pos(sx, sy) })
            {
                if (d.X == 0 && d.Y == 0) continue;
                var c = new Pos(target.X + d.X, target.Y + d.Y);
                if (InBounds(c) && (Tile(c) == TileKind.Cover || Tile(c) == TileKind.Barrel)) return true;
            }
            return false;
        }

        public bool Usable(Unit u, PowerState p)
        {
            if (!u.CanAct || p.Cooldown > 0) return false;
            if (p.Def.Signature && u.Momentum < Content.Signature) return false;
            return true;
        }

        /// <summary>Where this power can be aimed from the unit's current tile.</summary>
        public List<Pos> Targets(Unit u, PowerState p) => TargetsFrom(u, p, u.Pos);

        public List<Pos> TargetsFrom(Unit u, PowerState ps, Pos from)
        {
            var p = ps.Def;
            var list = new List<Pos>();
            switch (p.Target)
            {
                case Targeting.Self:
                    list.Add(from);
                    break;
                case Targeting.Enemy:
                    foreach (var e in Units)
                    {
                        if (!e.Alive || e.Side == u.Side) continue;
                        int d = from.Dist(e.Pos);
                        if (d > p.Range) continue;
                        if (p.Dash) { if (DashSpot(u, e.Pos, from) == null) continue; }
                        else if (p.Range > 1 && !LineOfSight(from, e.Pos)) continue;
                        list.Add(e.Pos);
                    }
                    break;
                case Targeting.Ally:
                    foreach (var a in Units)
                        if (a.Alive && a.Side == u.Side && !a.IsTurret && from.Dist(a.Pos) <= p.Range) list.Add(a.Pos);
                    break;
                case Targeting.Tile:
                    for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                    {
                        var t = new Pos(x, y);
                        if (from.Dist(t) > p.Range || !LineOfSight(from, t)) continue;
                        if ((p.Summon > 0 || p.Teleport) && (!Walkable(t) || (UnitAt(t) != null && t != from))) continue;
                        if (p.Teleport && t == from) continue;
                        list.Add(t);
                    }
                    break;
            }
            return list;
        }

        // the free tile next to the target closest to where the dasher stands
        Pos? DashSpot(Unit u, Pos target, Pos from)
        {
            Pos? best = null; int bd = int.MaxValue;
            foreach (var n in Neighbours8(target))
            {
                if (!Walkable(n)) continue;
                var o = UnitAt(n);
                if (o != null && o != u) continue;
                int d = n.Dist(from);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        /// <summary>Every unit the power would touch if aimed at <paramref name="target"/>.</summary>
        public List<Unit> Affected(Unit u, Power p, Pos target, Pos from)
        {
            var center = p.Target == Targeting.Self ? from : target;
            if (p.AllAllies) return Units.Where(a => a.Alive && a.Side == u.Side && !a.IsTurret).ToList();
            if (p.Area > 0)
            {
                bool friendly = p.Target == Targeting.Ally;
                return Units.Where(o => o.Alive && o != u && o.Pos.Dist(center) <= p.Area && (friendly ? o.Side == u.Side : o.Side != u.Side)).ToList();
            }
            var at = Units.FirstOrDefault(o => o.Alive && o.Pos == center && o != u);
            if (p.Target == Targeting.Self) return new List<Unit> { u };
            return at == null ? new List<Unit>() : new List<Unit> { at };
        }

        /// <summary>Damage one hit of this power does to the target (before shields).</summary>
        public int DamageOf(Unit a, Power p, Unit t, Pos from, bool area)
        {
            if (p.Damage <= 0) return 0;
            int d = p.Damage + a.DamageBonus + (p.Range == 1 && !p.Dash && p.Target == Targeting.Enemy ? a.MeleeBonus : 0);
            if (t.Mark > 0) d += 2;
            if (a.GrudgeHeroId != null && t.Hero != null && t.Hero.Id == a.GrudgeHeroId) d += 2;
            if (p.Range > 1 && !p.Dash && !area && from.Dist(t.Pos) > 1 && InCover(t.Pos, from)) d -= 1;
            if (t.Resists.Contains(p.Type)) d /= 2;
            else if (t.Weaks.Contains(p.Type)) d = (d * 3 + 1) / 2;
            if (p.Type == DamageType.Physical) d -= t.Armor;
            return Math.Max(1, d);
        }

        // ---------- actions ----------
        void Emit(BattleEvent e) { e.Round = Round; Log.Add(e); }

        public bool MoveUnit(Unit u, Pos to)
        {
            if (Over || u.Side != Turn || !u.CanMove) return false;
            var r = Reachable(u);
            if (!r.ContainsKey(to) || to == u.Pos) return false;
            Emit(new BattleEvent { Kind = "move", Actor = u.Id, From = u.Pos, To = to });
            u.Pos = to;
            u.Moved = true;
            return true;
        }

        public bool UsePower(Unit u, string powerId, Pos target)
        {
            if (Over || u.Side != Turn) return false;
            var ps = u.PowerById(powerId);
            if (ps == null || !Usable(u, ps) || !Targets(u, ps).Contains(target)) return false;
            Execute(u, ps, target);
            u.Acted = true;
            if (u.Side == Side.Heroes && Heroes.All(h => !h.CanAct && !h.CanMove)) { /* the UI ends the turn */ }
            return true;
        }

        void Execute(Unit u, PowerState ps, Pos target)
        {
            var p = ps.Def;
            var from = u.Pos;
            if (p.Signature) u.Momentum -= Content.Signature;
            if (p.Cooldown > 0) ps.Cooldown = Math.Max(1, p.Cooldown - (u.Hero?.CooldownCut ?? 0)) + 1; // +1: it ticks at the start of our next turn
            var sfx = p.Sfx != null && p.Sfx.Length > 0 ? p.Sfx[Rng.Range(0, p.Sfx.Length - 1)] : null;
            Emit(new BattleEvent { Kind = "power", Actor = u.Id, Power = p.Id, To = target, Text = p.Name, Sfx = sfx, Type = p.Type });
            if (p.Dash)
            {
                var spot = DashSpot(u, target, from).Value;
                if (spot != u.Pos) { Emit(new BattleEvent { Kind = "move", Actor = u.Id, From = u.Pos, To = spot, Text = "dash" }); u.Pos = spot; from = spot; }
            }
            if (p.Teleport)
            {
                Emit(new BattleEvent { Kind = "move", Actor = u.Id, From = u.Pos, To = target, Text = "teleport" });
                u.Pos = target;
            }
            if (p.Summon > 0) { Summon(u, p, target); }
            var hit = Affected(u, p, target, from);
            bool dealt = false;
            foreach (var t in hit)
            {
                if (t.Side != u.Side)
                {
                    for (int h = 0; h < p.Hits && t.Alive; h++)
                    {
                        int d = DamageOf(u, p, t, from, p.Area > 0);
                        if (d > 0) { Hurt(t, d, u, p.Type, sfx); dealt = true; }
                    }
                    if (!t.Alive) continue;
                    if (p.Stun > 0)
                    {
                        if (t.StunImmune) Emit(new BattleEvent { Kind = "resist", Actor = u.Id, Target = t.Id, Text = $"{t.Name} shrugs off the stun" });
                        else { t.Stun = Math.Max(t.Stun, p.Stun); Emit(new BattleEvent { Kind = "stun", Actor = u.Id, Target = t.Id, Amount = p.Stun }); }
                    }
                    if (p.Burn > 0) { t.Burn = Math.Max(t.Burn, p.Burn); Emit(new BattleEvent { Kind = "burn", Target = t.Id, Amount = p.Burn }); }
                    if (p.Mark > 0) { t.Mark = Math.Max(t.Mark, p.Mark); Emit(new BattleEvent { Kind = "mark", Actor = u.Id, Target = t.Id, Amount = p.Mark }); }
                    if (p.Knockback > 0) Knock(u, t, p.Area > 0 ? (p.Target == Targeting.Self ? from : target) : from, p.Knockback);
                }
                else
                {
                    if (p.Heal > 0 && t.Hp < t.MaxHp) { int n = Math.Min(p.Heal, t.MaxHp - t.Hp); t.Hp += n; Emit(new BattleEvent { Kind = "heal", Actor = u.Id, Target = t.Id, Amount = n }); }
                    if (p.Shield > 0) { t.Shield = Math.Min(8, t.Shield + p.Shield); Emit(new BattleEvent { Kind = "shield", Actor = u.Id, Target = t.Id, Amount = p.Shield }); }
                    if (p.Cleanse && (t.Stun > 0 || t.Burn > 0)) { t.Stun = 0; t.Burn = 0; t.StunnedNow = false; Emit(new BattleEvent { Kind = "cleanse", Target = t.Id }); }
                }
            }
            // area blasts set off barrels
            if (p.Area > 0 && p.Damage > 0)
            {
                var center = p.Target == Targeting.Self ? from : target;
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                {
                    var c = new Pos(x, y);
                    if (Tiles[x, y] == TileKind.Barrel && c.Dist(center) <= p.Area) Explode(c, u);
                }
            }
            if (dealt && u.Side == Side.Heroes && !u.IsTurret) u.Momentum = Math.Min(Content.MaxMomentum, u.Momentum + 1);
            CheckEnd();
        }

        void Summon(Unit u, Power p, Pos target)
        {
            if (u.Side == Side.Heroes)
            {
                var t = new Unit
                {
                    Id = nextId++, Name = $"{u.Name}'s Turret", Side = Side.Heroes, Archetype = "turret", IsTurret = true, Pos = target,
                    MaxHp = 4, Hp = 4, Move = 0, DamageBonus = u.DamageBonus, Look = u.Look,
                };
                t.Powers.Add(new PowerState(Content.Powers["turret-shot"]));
                Units.Add(t);
                Emit(new BattleEvent { Kind = "summon", Actor = u.Id, Target = t.Id, To = target });
                return;
            }
            int made = 0;
            foreach (var n in Neighbours8(u.Pos).OrderBy(n => Rng.NextDouble()).ToList())
            {
                if (made >= p.Summon) break;
                if (!Walkable(n) || UnitAt(n) != null) continue;
                var g = MakeHench("goon");
                g.Pos = n;
                g.Acted = g.Moved = true; // they arrive at the end of the turn
                Units.Add(g);
                Emit(new BattleEvent { Kind = "summon", Actor = u.Id, Target = g.Id, To = n });
                made++;
            }
        }

        void Hurt(Unit t, int amount, Unit src, DamageType type, string sfx)
        {
            int absorbed = Math.Min(t.Shield, amount);
            t.Shield -= absorbed;
            int dealt = amount - absorbed;
            t.Hp -= dealt;
            if (src != null)
            {
                t.DamageFrom.TryGetValue(src.Id, out int was); t.DamageFrom[src.Id] = was + dealt;
            }
            t.DamageByType.TryGetValue(type, out int wt); t.DamageByType[type] = wt + dealt;
            Emit(new BattleEvent { Kind = "hit", Actor = src?.Id ?? -1, Target = t.Id, Amount = dealt, Type = type, Sfx = sfx, Text = absorbed > 0 ? $"shield took {absorbed}" : null });
            if (t.Hp <= 0 && !t.Out)
            {
                t.Hp = 0;
                t.Out = true;
                t.KOdBy = src?.Id ?? -1;
                Emit(new BattleEvent { Kind = "ko", Actor = src?.Id ?? -1, Target = t.Id, Sfx = "KO!" });
                if (src?.Hero != null && !t.IsTurret) src.Hero.KOs++;
            }
            // villains with a grudge, or on the ropes, have something to say
            if (src?.Villain != null && t.Hero != null && t.Hero.Id == src.GrudgeHeroId && taunted.Add(src.Id * 100 + 1))
                Emit(new BattleEvent { Kind = "taunt", Actor = src.Id, Target = t.Id, Text = Rivalry.TauntHit(src.Villain, t.Hero, Rng) });
            if (t.Villain != null && t.Alive && t.Hp <= t.MaxHp * 0.4 && taunted.Add(t.Id * 100 + 2))
                Emit(new BattleEvent { Kind = "taunt", Actor = t.Id, Text = Rivalry.TauntHurt(t.Villain, Rng) });
        }

        void Knock(Unit src, Unit t, Pos origin, int n)
        {
            int sx = Math.Sign(t.Pos.X - origin.X), sy = Math.Sign(t.Pos.Y - origin.Y);
            if (sx == 0 && sy == 0) sx = src.Side == Side.Heroes ? 1 : -1;
            for (int i = 0; i < n && t.Alive; i++)
            {
                var to = new Pos(t.Pos.X + sx, t.Pos.Y + sy);
                if (InBounds(to) && Tile(to) == TileKind.Barrel) { Explode(to, src); break; }
                if (!InBounds(to) || Tile(to) != TileKind.Floor || UnitAt(to) != null)
                {
                    Emit(new BattleEvent { Kind = "slam", Actor = src.Id, Target = t.Id });
                    Hurt(t, 2, src, DamageType.Physical, "SLAM!");
                    var other = InBounds(to) ? UnitAt(to) : null;
                    if (other != null && other.Alive) Hurt(other, 1, src, DamageType.Physical, "THUD!");
                    break;
                }
                Emit(new BattleEvent { Kind = "knockback", Actor = src.Id, Target = t.Id, From = t.Pos, To = to });
                t.Pos = to;
            }
        }

        void Explode(Pos c, Unit src)
        {
            if (Tile(c) != TileKind.Barrel) return;
            Tiles[c.X, c.Y] = TileKind.Floor;
            Emit(new BattleEvent { Kind = "explode", Actor = src?.Id ?? -1, To = c, Sfx = "KA-BOOM!" });
            foreach (var u in Units.Where(o => o.Alive && o.Pos.Dist(c) <= 1).ToList()) Hurt(u, 3, src, DamageType.Fire, "KA-BOOM!");
            foreach (var n in Neighbours8(c).ToList()) if (Tile(n) == TileKind.Barrel) Explode(n, src); // chain reaction
        }

        // ---------- team-ups ----------
        /// <summary>Two adjacent heroes who haven't acted can combine on one target in reach of either.</summary>
        public bool CanTeamUp(Unit a, Unit b) => a != b && a.Side == Side.Heroes && b.Side == Side.Heroes && !a.IsTurret && !b.IsTurret
            && a.CanAct && b.CanAct && a.Pos.Dist(b.Pos) == 1 && Turn == Side.Heroes && !Over;

        public List<Pos> TeamUpTargets(Unit a, Unit b)
        {
            if (!CanTeamUp(a, b)) return new List<Pos>();
            var basicA = a.Powers[0].Def; var basicB = b.Powers[0].Def;
            int reach = Math.Max(Math.Max(basicA.Range, basicB.Range), 2);
            return Units.Where(e => e.Alive && e.Side == Side.Villains &&
                ((a.Pos.Dist(e.Pos) <= reach && LineOfSight(a.Pos, e.Pos)) || (b.Pos.Dist(e.Pos) <= reach && LineOfSight(b.Pos, e.Pos)))).Select(e => e.Pos).ToList();
        }

        public static string TeamUpName(Unit a, Unit b)
        {
            var k = new HashSet<string> { a.Archetype, b.Archetype };
            if (k.SetEquals(new[] { "brick", "speedster" })) return "Human Cannonball";
            if (k.SetEquals(new[] { "blaster", "mentalist" })) return "Blinding Flash";
            if (k.Contains("gadgeteer")) return "Tag and Bag";
            if (k.Contains("mystic")) return "Guardian Strike";
            return "Team-Up";
        }

        public int TeamUpDamage(Unit a, Unit b, Unit t)
        {
            var name = TeamUpName(a, b);
            int d = a.Powers[0].Def.Damage + b.Powers[0].Def.Damage + a.DamageBonus + b.DamageBonus + 1 + (name == "Human Cannonball" ? 3 : 0);
            if (t.Mark > 0) d += 2;
            if (t.Resists.Contains(DamageType.Physical)) d /= 2;
            d -= t.Armor;
            return Math.Max(1, d);
        }

        public bool TeamUp(Unit a, Unit b, Pos target)
        {
            if (!TeamUpTargets(a, b).Contains(target)) return false;
            var t = UnitAt(target);
            var name = TeamUpName(a, b);
            Emit(new BattleEvent { Kind = "teamup", Actor = a.Id, Target = b.Id, To = target, Text = name, Sfx = "TEAM-UP!" });
            if (name == "Human Cannonball")
            {
                var thrown = a.Archetype == "speedster" ? a : b;
                var spot = DashSpot(thrown, target, thrown.Pos);
                if (spot != null && spot.Value != thrown.Pos) { Emit(new BattleEvent { Kind = "move", Actor = thrown.Id, From = thrown.Pos, To = spot.Value, Text = "thrown" }); thrown.Pos = spot.Value; }
            }
            Hurt(t, TeamUpDamage(a, b, t), a, DamageType.Physical, name == "Human Cannonball" ? "KA-WHAMMM!" : "KRAKOOM!");
            if (t.Alive)
            {
                if (name == "Blinding Flash" && !t.StunImmune) { t.Stun = Math.Max(t.Stun, 1); Emit(new BattleEvent { Kind = "stun", Actor = a.Id, Target = t.Id, Amount = 1 }); }
                if (name == "Tag and Bag") { t.Mark = Math.Max(t.Mark, 2); Emit(new BattleEvent { Kind = "mark", Actor = a.Id, Target = t.Id, Amount = 2 }); }
            }
            if (name == "Guardian Strike") foreach (var h in new[] { a, b }) { h.Shield = Math.Min(8, h.Shield + 2); Emit(new BattleEvent { Kind = "shield", Actor = a.Id, Target = h.Id, Amount = 2 }); }
            a.Acted = b.Acted = true;
            a.Momentum = Math.Min(Content.MaxMomentum, a.Momentum + 1);
            b.Momentum = Math.Min(Content.MaxMomentum, b.Momentum + 1);
            CheckEnd();
            return true;
        }

        // ---------- turns ----------
        /// <summary>The player is done: turrets fire, the villains play, and a new round starts.</summary>
        public void EndHeroTurn()
        {
            if (Over || Turn != Side.Heroes) return;
            foreach (var t in Units.Where(u => u.IsTurret && u.Alive).ToList())
            {
                var target = Units.Where(e => e.Alive && e.Side == Side.Villains && e.Pos.Dist(t.Pos) <= 4 && LineOfSight(t.Pos, e.Pos)).OrderBy(e => e.Hp).FirstOrDefault();
                if (target != null) { t.Acted = false; Execute(t, t.Powers[0], target.Pos); }
                if (Over) return;
            }
            Turn = Side.Villains;
            StartPhase(Side.Villains);
            if (Over) return;
            var order = Units.Where(u => u.Side == Side.Villains && u.Alive).OrderBy(u => u.IsBoss ? 1 : 0).ThenBy(u => u.Id).ToList();
            foreach (var v in order)
            {
                if (Over) return;
                if (!v.Alive || v.StunnedNow) continue;
                if (v.IsBoss && TryEscape(v)) continue;
                if (v.IsBoss && taunted.Add(v.Id * 100))
                    Emit(new BattleEvent { Kind = "taunt", Actor = v.Id, Text = Rivalry.TauntIntro(v.Villain, Heroes.Select(h => h.Hero).ToList(), Rng) });
                Ai.TakeTurn(this, v);
            }
            if (Over) return;
            // end of the round
            foreach (var u in Units) if (u.Mark > 0) u.Mark--;
            Round++;
            if (Round > MaxRounds)
            {
                foreach (var b in Bosses.Where(b => b.Alive))
                {
                    b.Out = b.Fled = true;
                    Emit(new BattleEvent { Kind = "escape", Actor = b.Id, Text = $"{b.Name} slips away in the chaos.", Sfx = "WHOOSH!" });
                }
                CheckEnd();
                return;
            }
            Turn = Side.Heroes;
            Emit(new BattleEvent { Kind = "turn", Text = $"Round {Round}" });
            StartPhase(Side.Heroes);
        }

        void StartPhase(Side side)
        {
            foreach (var u in Units.Where(u => u.Side == side && u.Alive).ToList())
            {
                u.Moved = u.Acted = false;
                foreach (var p in u.Powers) if (p.Cooldown > 0) p.Cooldown--;
                u.StunnedNow = false;
                if (u.Stun > 0) { u.StunnedNow = true; u.Stun--; }
                if (u.Burn > 0)
                {
                    u.Burn--;
                    int d = u.Resists.Contains(DamageType.Fire) ? 1 : 2;
                    Hurt(u, d, null, DamageType.Fire, "SSSS!");
                }
            }
            CheckEnd();
        }

        /// <summary>A boss on the ropes may run for it, unless stunned or tagged.</summary>
        bool TryEscape(Unit b)
        {
            if (b.Hp > b.MaxHp * 0.3 || b.StunnedNow || b.Mark > 0) return false;
            double chance = Math.Min(0.9, 0.45 + 0.1 * b.Villain.Rank + 0.1 * Math.Min(2, b.Villain.Escapes) + b.EscapeBonus);
            if (Rng.Chance(chance))
            {
                b.Out = b.Fled = true;
                Emit(new BattleEvent { Kind = "escape", Actor = b.Id, Sfx = "POOF!", Text = Rivalry.TauntEscape(b.Villain, Rng) });
                CheckEnd();
                return true;
            }
            Emit(new BattleEvent { Kind = "escape-fail", Actor = b.Id, Text = $"{b.Name} tries to run, but there's nowhere to go!" });
            return false;
        }

        void CheckEnd()
        {
            if (Over) return;
            if (Heroes.All(h => !h.Alive)) { Result = BattleResult.Defeat; Emit(new BattleEvent { Kind = "end", Text = "defeat" }); return; }
            var bosses = Bosses.ToList();
            if (bosses.Count > 0 && bosses.All(b => b.Out))
            {
                Result = bosses.Any(b => !b.Fled) ? BattleResult.Captured : BattleResult.Escaped;
                foreach (var h in Units.Where(u => u.IsHench && u.Alive)) { h.Out = true; Emit(new BattleEvent { Kind = "surrender", Actor = h.Id }); }
                Emit(new BattleEvent { Kind = "end", Text = Result == BattleResult.Captured ? "captured" : "escaped" });
            }
        }

        /// <summary>Let the AI play the heroes' turn (auto-battle), then end it.</summary>
        public void AutoHeroTurn()
        {
            if (Over || Turn != Side.Heroes) return;
            foreach (var h in Heroes.Where(h => h.Alive).OrderBy(h => h.Id).ToList())
            {
                if (Over) return;
                if (!h.StunnedNow) Ai.TakeTurn(this, h);
            }
            EndHeroTurn();
        }

        // for the AI's look-ahead
        internal void ForceAct(Unit u, PowerState ps, Pos target) { Execute(u, ps, target); u.Acted = true; }
        internal void ForceMove(Unit u, Pos to) { if (to != u.Pos) { Emit(new BattleEvent { Kind = "move", Actor = u.Id, From = u.Pos, To = to }); u.Pos = to; } u.Moved = true; }
    }
}
