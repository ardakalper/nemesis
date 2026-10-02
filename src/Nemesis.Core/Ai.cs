using System;
using System.Collections.Generic;
using System.Linq;

namespace Nemesis
{
    /// <summary>Utility AI used by the villains, and by the heroes in auto-battle. For every tile the unit can
    /// reach, every power it can use and every target from there, it scores the outcome (damage that lands,
    /// knockouts, stuns, tags on a boss about to run, heals) plus how safe the tile is, and takes the best.
    /// With nothing to hit, it walks towards the nearest foe along real paths.</summary>
    public static class Ai
    {
        public static void TakeTurn(Battle b, Unit u)
        {
            if (b.Over || !u.Alive || u.StunnedNow) return;
            var reach = b.Reachable(u);
            double bestScore = double.NegativeInfinity;
            Pos bestPos = u.Pos; PowerState bestPower = null; Pos bestTarget = default;
            foreach (var kv in reach)
            {
                var pos = kv.Key;
                double place = Placement(b, u, pos) - kv.Value * 0.02;
                if (u.CanAct)
                    foreach (var ps in u.Powers)
                    {
                        if (!b.Usable(u, ps)) continue;
                        foreach (var t in b.TargetsFrom(u, ps, pos))
                        {
                            double s = Evaluate(b, u, ps.Def, pos, t);
                            if (s <= 0.3) continue;
                            s += place + b.Rng.NextDouble() * 0.01;
                            if (s > bestScore) { bestScore = s; bestPos = pos; bestPower = ps; bestTarget = t; }
                        }
                    }
            }
            if (bestPower == null)
            {
                // nothing worth doing from anywhere: close in on the nearest foe
                var dist = DistanceToFoes(b, u);
                double best = double.NegativeInfinity;
                foreach (var kv in reach)
                {
                    dist.TryGetValue(kv.Key, out int d);
                    if (!dist.ContainsKey(kv.Key)) d = 99;
                    double s = -d + Placement(b, u, kv.Key) * 0.5 + b.Rng.NextDouble() * 0.01;
                    if (s > best) { best = s; bestPos = kv.Key; }
                }
                if (u.CanMove) b.ForceMove(u, bestPos);
                return;
            }
            if (u.CanMove && bestPos != u.Pos) b.ForceMove(u, bestPos);
            b.ForceAct(u, bestPower, bestTarget);
        }

        static double Evaluate(Battle b, Unit u, Power p, Pos from, Pos target)
        {
            double s = 0;
            var hit = b.Affected(u, p, target, from);
            foreach (var t in hit)
            {
                if (t.Side != u.Side)
                {
                    int dmg = b.DamageOf(u, p, t, from, p.Area > 0) * p.Hits;
                    int lands = Math.Max(0, Math.Min(dmg - t.Shield, t.Hp));
                    double v = lands * (t.IsBoss ? 1.5 : 1) * (t.IsTurret ? 0.6 : 1);
                    if (dmg - t.Shield >= t.Hp) v += t.IsBoss ? 12 : u.Side == Side.Villains ? 8 : 5;
                    if (u.GrudgeHeroId != null && t.Hero != null && t.Hero.Id == u.GrudgeHeroId) v *= 1.5;
                    if (p.Stun > 0 && !t.StunImmune && t.Stun == 0) v += t.IsBoss ? 4 : 2.5;
                    if (p.Mark > 0 && t.Mark == 0) v += t.IsBoss && t.Hp <= t.MaxHp * 0.45 ? 6 : 1;
                    if (p.Burn > 0 && t.Burn == 0) v += 1.5;
                    if (p.Knockback > 0) v += 0.5;
                    s += v;
                }
                else
                {
                    int missing = t.MaxHp - t.Hp;
                    if (p.Heal > 0) s += Math.Min(p.Heal, missing) * 0.9;
                    if (p.Shield > 0) s += t.Hp < t.MaxHp * 0.6 ? 2.5 : 0.6;
                    if (p.Cleanse) s += (t.Stun > 0 ? 3 : 0) + (t.Burn > 0 ? 1 : 0);
                }
            }
            if (p.Summon > 0) s += u.Side == Side.Villains ? 4 : 3.5;
            if (p.Teleport) s += Threatened(b, u, u.Pos) * 1.5 - Threatened(b, u, target) * 1.5 + 0.5;
            // signatures are precious: only worth it for a big swing
            if (p.Signature && s < 7) s -= 3;
            return s;
        }

        // how many foes stand next to a tile
        static int Threatened(Battle b, Unit u, Pos p) => b.Units.Count(e => e.Alive && e.Side != u.Side && e.Pos.Dist(p) == 1);

        static double Placement(Battle b, Unit u, Pos p)
        {
            bool ranged = u.Powers.Any(ps => ps.Def.Range > 1 && !ps.Def.Dash && ps.Def.Damage > 0);
            int adj = Threatened(b, u, p);
            double s = ranged ? -1.5 * adj : 0;
            if (u.Hp <= u.MaxHp * 0.35) s -= adj * 1.5; // hurt units keep their distance
            return s;
        }

        // walking distance from every tile to the nearest foe (ignoring other units)
        static Dictionary<Pos, int> DistanceToFoes(Battle b, Unit u)
        {
            var dist = new Dictionary<Pos, int>();
            var q = new Queue<Pos>();
            foreach (var e in b.Units.Where(e => e.Alive && e.Side != u.Side)) { dist[e.Pos] = 0; q.Enqueue(e.Pos); }
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in new[] { new Pos(1, 0), new Pos(-1, 0), new Pos(0, 1), new Pos(0, -1) })
                {
                    var n = new Pos(c.X + d.X, c.Y + d.Y);
                    if (!Battle.InBounds(n) || b.Tile(n) != TileKind.Floor || dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }
    }
}
