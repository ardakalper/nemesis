using System.Linq;

namespace Nemesis
{
    /// <summary>Turns battle events into comic captions.</summary>
    public static class Narration
    {
        public static string Caption(Battle b, BattleEvent e)
        {
            string N(int id) => b.Get(id)?.Name ?? "Someone";
            string type = e.Type == DamageType.Physical ? "" : $" {e.Type.ToString().ToLowerInvariant()}";
            switch (e.Kind)
            {
                case "turn": return e.Text;
                case "power": return $"{N(e.Actor)}: {e.Text}!";
                case "teamup": return $"TEAM-UP! {N(e.Actor)} and {N(e.Target)}: {e.Text}!";
                case "move":
                    if (e.Text == "thrown") return $"{N(e.Actor)} goes flying across the room!";
                    if (e.Text == "teleport") return $"{N(e.Actor)} vanishes and reappears.";
                    return null;
                case "hit": return e.Actor >= 0 ? $"{N(e.Target)} takes {e.Amount}{type} damage{(e.Text != null ? $" ({e.Text})" : "")}." : $"{N(e.Target)} burns for {e.Amount}.";
                case "ko": return b.Get(e.Target)?.Side == Side.Heroes ? $"{N(e.Target)} is down!" : $"{N(e.Target)} is out cold!";
                case "stun": return $"{N(e.Target)} is stunned!";
                case "resist": return e.Text + ".";
                case "burn": return $"{N(e.Target)} catches fire!";
                case "mark": return $"{N(e.Target)} is tagged: can't slip away, and takes more damage.";
                case "shield": return $"{N(e.Target)} is shielded ({e.Amount}).";
                case "heal": return $"{N(e.Target)} recovers {e.Amount}.";
                case "cleanse": return $"{N(e.Target)} shakes it off.";
                case "knockback": return $"{N(e.Target)} is knocked back.";
                case "slam": return $"{N(e.Target)} slams into something hard!";
                case "explode": return "A barrel goes up!";
                case "summon": return b.Get(e.Target)?.IsTurret == true ? $"{N(e.Actor)} deploys a turret." : $"{N(e.Target)} answers the call.";
                case "escape": return e.Text != null && e.Text.Contains(" ") && !e.Text.StartsWith(N(e.Actor)) ? $"“{e.Text}” {N(e.Actor)} escapes!" : e.Text ?? $"{N(e.Actor)} escapes!";
                case "escape-fail": return e.Text;
                case "taunt": return $"{N(e.Actor)}: “{e.Text}”";
                case "surrender": return $"{N(e.Actor)} throws up their hands.";
                case "end": return e.Text == "captured" ? "CAPTURED!" : e.Text == "escaped" ? "GOT AWAY!" : "DEFEATED!";
                default: return null;
            }
        }

        /// <summary>A one-line summary of a power for tooltips and buttons.</summary>
        public static string Describe(Power p, Unit u)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (p.Damage > 0) parts.Add($"{p.Damage + (u?.DamageBonus ?? 0)}{(p.Hits > 1 ? $"×{p.Hits}" : "")} {p.Type.ToString().ToLowerInvariant()}");
            if (p.Range > 1 && p.Range < 99) parts.Add($"range {p.Range}");
            if (p.Range >= 99) parts.Add("any range");
            if (p.Area > 0) parts.Add($"area {p.Area}");
            if (p.Stun > 0) parts.Add($"stun {p.Stun}");
            if (p.Burn > 0) parts.Add($"burn {p.Burn}");
            if (p.Mark > 0) parts.Add("tag");
            if (p.Knockback > 0) parts.Add("knockback");
            if (p.Heal > 0) parts.Add($"heal {p.Heal}");
            if (p.Shield > 0) parts.Add($"shield {p.Shield}");
            if (p.Dash) parts.Add("dash");
            if (p.Cooldown > 0) parts.Add($"every {p.Cooldown} turns");
            if (p.Signature) parts.Add($"costs {Content.Signature} momentum");
            return string.Join(" · ", parts.Where(x => x != null));
        }
    }
}
