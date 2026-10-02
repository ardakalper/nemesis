using System.Text.Json;
using Microsoft.JSInterop;

namespace Nemesis.Web
{
    public enum Screen { Title, NewSeries, Issue, Team, Battle, Aftermath, SeriesEnd, Gallery, Covers, Help }

    /// <summary>The whole app's state: the universe (saved), the current screen and the fight in progress (not saved:
    /// a reload mid-fight replays the issue from the same seed).</summary>
    public class GameState
    {
        const string Key = "nemesis.universe.v1";
        static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        readonly IJSRuntime js;
        public GameState(IJSRuntime js) { this.js = js; }

        public Universe U { get; private set; } = new Universe();
        public Series S => U.Current;
        public Screen Screen { get; private set; } = Screen.Title;
        public Screen Back { get; private set; } = Screen.Title;
        public Battle Battle { get; private set; }
        public List<string> Picked { get; } = new List<string>();
        public bool Loaded { get; private set; }
        public event Action Changed;

        public bool CanContinue => S != null && !S.Over;
        public Villain Villain(string id) => U.Find(id);

        public async Task Load()
        {
            try
            {
                var json = await js.InvokeAsync<string>("nemesis.load", Key);
                if (!string.IsNullOrEmpty(json)) U = JsonSerializer.Deserialize<Universe>(json, Json) ?? new Universe();
            }
            catch (Exception e) { Console.WriteLine($"Couldn't read the save: {e.Message}"); U = new Universe(); }
            Loaded = true;
            Changed?.Invoke();
        }

        public async Task Save()
        {
            try { await js.InvokeVoidAsync("nemesis.save", Key, JsonSerializer.Serialize(U, Json)); }
            catch (Exception e) { Console.WriteLine($"Couldn't save: {e.Message}"); }
        }

        public void Go(Screen s)
        {
            if (s == Screen.Gallery || s == Screen.Covers || s == Screen.Help)
            {
                if (Screen != Screen.Gallery && Screen != Screen.Covers && Screen != Screen.Help) Back = Screen;
            }
            Screen = s;
            Changed?.Invoke();
        }

        public void Return() => Go(Back == Screen.Battle && Battle == null ? Screen.Title : Back);

        public void Continue()
        {
            if (!CanContinue) { Go(Screen.Title); return; }
            Go(S.Offers.Count > 0 ? Screen.Aftermath : Screen.Issue);
        }

        public async Task NewSeries(string teamName)
        {
            Campaign.NewSeries(U, (ulong)DateTime.UtcNow.Ticks ^ 0x5DEECE66DUL, teamName);
            Battle = null;
            await Save();
            Go(Screen.Issue);
        }

        /// <summary>Goes to the team screen with a sensible default pick.</summary>
        public void Assemble()
        {
            Picked.Clear();
            var bosses = S.Next.VillainIds.Select(U.Find).ToList();
            // heroes whose attacks the villains are weak to go first, then the most experienced
            foreach (var h in Campaign.Available(S).OrderByDescending(h => Fit(h, bosses)).ThenByDescending(h => h.Level).Take(Campaign.TeamSize))
                Picked.Add(h.Id);
            Go(Screen.Team);
        }

        public static int Fit(Hero h, List<Villain> bosses)
        {
            int score = 0;
            var types = Content.Archetypes[h.Archetype].Powers.Select(p => Content.Powers[p]).Where(p => p.Damage > 0).Select(p => p.Type).Distinct().ToList();
            foreach (var v in bosses)
                foreach (var t in v.Traits.Select(x => Content.Traits[x]))
                {
                    if (t.Weak.HasValue && types.Contains(t.Weak.Value)) score += 2;
                    if (t.Resist.HasValue && types.Contains(t.Resist.Value)) score -= 2;
                }
            if (bosses.Any(v => v.GrudgeHeroId == h.Id)) score -= 1;
            return score;
        }

        public void Toggle(string heroId)
        {
            if (Picked.Remove(heroId)) { Changed?.Invoke(); return; }
            if (Picked.Count >= Campaign.TeamSize) Picked.RemoveAt(0);
            Picked.Add(heroId);
            Changed?.Invoke();
        }

        public void StartFight()
        {
            Battle = Campaign.StartBattle(U, S, Picked);
            Go(Screen.Battle);
        }

        public async Task FinishFight()
        {
            Campaign.Finish(U, S, Battle);
            Battle = null;
            Go(Screen.Aftermath);
            await Save();
        }

        public async Task TakeUpgrade(int index)
        {
            Campaign.TakeUpgrade(S, index);
            await Save();
            Changed?.Invoke();
        }

        public async Task NextIssue()
        {
            S.Offers.Clear();
            await Save();
            Go(S.Over ? Screen.SeriesEnd : Screen.Issue);
        }

        public async Task Retire()
        {
            if (S != null && !S.Over)
            {
                S.Over = true;
                U.BestScore = Math.Max(U.BestScore, S.Score);
            }
            Battle = null;
            await Save();
            Go(Screen.Title);
        }

        public async Task Wipe()
        {
            U = new Universe();
            Battle = null;
            await Save();
            Go(Screen.Title);
        }

        // ---------- lookups for the UI ----------
        static readonly Dictionary<string, Hero> Originals = Content.Roster().ToDictionary(h => h.Id);
        public Hero Hero(string id) => S?.Roster.FirstOrDefault(h => h.Id == id) ?? (Originals.TryGetValue(id ?? "", out var h) ? h : null);
        public static Look HeroLook(string id) => Originals.TryGetValue(id ?? "", out var h) ? h.Look : new Look();
        public static string HeroName(string id) => Originals.TryGetValue(id ?? "", out var h) ? h.Name : id;

        /// <summary>The cover for the issue about to start, before anyone knows how it ends.</summary>
        public IssueRecord PendingCover()
        {
            var p = S.Next;
            return new IssueRecord
            {
                Series = S.Number, Number = p.Number, Title = p.Title, Headline = p.Headline, Place = Campaign.PlaceOf(p).Name, TeamName = S.TeamName,
                VillainIds = p.VillainIds.ToList(), HeroIds = (Picked.Count > 0 ? Picked : Campaign.Available(S).Take(3).Select(h => h.Id)).ToList(),
                Outcome = null, Seed = (int)(p.Seed & 0x7fffffff),
            };
        }
    }
}
