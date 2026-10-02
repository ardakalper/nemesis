using Nemesis;

namespace Nemesis.Tests;

static class Play
{
    /// <summary>Lets the AI play both sides until the fight ends.</summary>
    public static Battle Auto(Battle b)
    {
        for (int i = 0; i < 40 && !b.Over; i++) b.AutoHeroTurn();
        return b;
    }

    /// <summary>Plays a whole series with the AI picking the healthiest three heroes and the first upgrade.</summary>
    public static Series Series(Universe u, ulong seed, Action<Battle, Series> each = null)
    {
        var s = Campaign.NewSeries(u, seed);
        while (!s.Over)
        {
            var team = Campaign.Available(s).OrderByDescending(h => h.Level).ThenBy(h => h.Id).Take(Campaign.TeamSize).Select(h => h.Id).ToList();
            var b = Auto(Campaign.StartBattle(u, s, team));
            each?.Invoke(b, s);
            Campaign.Finish(u, s, b);
            if (s.Offers.Count > 0) Campaign.TakeUpgrade(s, 0);
        }
        return s;
    }
}
