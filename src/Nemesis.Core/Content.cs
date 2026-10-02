using System.Collections.Generic;

namespace Nemesis
{
    public class Power
    {
        public string Id;
        public string Name;
        public string Text;
        public Targeting Target = Targeting.Enemy;
        public int Range = 1;          // 1 = melee
        public int Damage;
        public DamageType Type = DamageType.Physical;
        public int Hits = 1;
        public int Area;               // radius around the target tile (0 = single target)
        public int Cooldown;
        public bool Signature;         // costs momentum
        public int Stun, Burn, Shield, Heal, Knockback, Mark;
        public bool Dash;              // move next to the target first
        public bool Teleport;          // Tile power: blink there
        public int Summon;             // Self/Tile power: how many goons (villains) or a turret (heroes)
        public bool Cleanse;
        public bool AllAllies;         // Self power that affects the whole team
        public string[] Sfx;
    }

    public class ArchetypeDef
    {
        public string Id;
        public string Name;
        public int Hp;
        public int Move;
        public string[] Powers;
        public string Text;
    }

    public class Trait
    {
        public string Id;
        public string Name;
        public string Text;
        public DamageType? Resist;
        public DamageType? Weak;
        public int Armor;
        public int Move;
        public int Melee;
        public int Damage;
        public int Hp;
        public double Escape;
        public bool StunImmune;
    }

    public class HenchDef
    {
        public string Id;
        public string Name;
        public int Hp;
        public int Move;
        public string Power;
        public string PromotesTo;
    }

    public class PlaceDef
    {
        public string Id;
        public string Name;
        public int Cover;
        public int Walls;
        public int Barrels;
        public string Floor;   // colour of the ground in the UI
    }

    public class UpgradeDef
    {
        public string Id;
        public string Name;
        public string Text;
    }

    /// <summary>All the game's data: powers, archetypes, the starting roster, villain parts, places.</summary>
    public static class Content
    {
        public const int Signature = 3;   // momentum a signature move costs
        public const int MaxMomentum = 5;

        static Power P(string id, string name, string text) => new Power { Id = id, Name = name, Text = text };

        public static readonly Dictionary<string, Power> Powers = new Dictionary<string, Power>();
        static void Add(Power p) => Powers[p.Id] = p;

        static readonly string[] Thud = { "WHAM!", "KRAK!", "POW!", "THWACK!", "BAM!" };
        static readonly string[] Zap = { "ZZAP!", "FZZT!", "KRZZT!", "ZWEEE!" };
        static readonly string[] Burn = { "FWOOSH!", "KA-BOOM!", "WHOOMPH!" };
        static readonly string[] Ice = { "KRSSH!", "SHNK!", "KRAKKLE!" };
        static readonly string[] Mind = { "VRMMM!", "MMNNG!", "WHUMMM!" };

        static Content()
        {
            // ---------- heroes ----------
            Add(new Power { Id = "haymaker", Name = "Haymaker", Text = "A punch that sends them flying.", Damage = 4, Knockback = 1, Sfx = Thud });
            Add(new Power { Id = "ground-pound", Name = "Ground Pound", Text = "Shakes everyone next to you off their feet.", Target = Targeting.Self, Area = 1, Damage = 3, Stun = 1, Cooldown = 3, Sfx = new[] { "KRA-THOOM!" } });
            Add(new Power { Id = "meteor-slam", Name = "Meteor Slam", Text = "Leap onto a foe and crater everything around them.", Range = 4, Dash = true, Area = 1, Damage = 5, Signature = true, Sfx = new[] { "KA-THOOOM!" } });

            Add(new Power { Id = "rapid-strikes", Name = "Rapid Strikes", Text = "Two blows before they blink.", Damage = 2, Hits = 2, Sfx = new[] { "WHAP-WHAP!", "BAP-BAP!" } });
            Add(new Power { Id = "blur-dash", Name = "Blur Dash", Text = "Cross the room and hit on arrival.", Range = 5, Dash = true, Damage = 3, Knockback = 1, Cooldown = 2, Sfx = new[] { "ZOOM—WHAM!" } });
            Add(new Power { Id = "cyclone", Name = "Cyclone", Text = "Run circles until everyone nearby is dizzy.", Target = Targeting.Self, Area = 2, Damage = 3, Stun = 1, Signature = true, Sfx = new[] { "WHIRRRR!" } });

            Add(new Power { Id = "bolt", Name = "Solar Bolt", Text = "A beam of light.", Range = 6, Damage = 3, Type = DamageType.Energy, Sfx = Zap });
            Add(new Power { Id = "nova", Name = "Nova", Text = "A burst that sets things alight.", Target = Targeting.Tile, Range = 5, Area = 1, Damage = 3, Type = DamageType.Fire, Burn = 2, Cooldown = 3, Sfx = Burn });
            Add(new Power { Id = "supernova", Name = "Supernova", Text = "Everything near the target burns.", Target = Targeting.Tile, Range = 6, Area = 2, Damage = 5, Type = DamageType.Fire, Signature = true, Sfx = new[] { "KRA-KOOOM!" } });

            Add(new Power { Id = "mind-spike", Name = "Mind Spike", Text = "A thought, sharpened.", Range = 5, Damage = 3, Type = DamageType.Psychic, Sfx = Mind });
            Add(new Power { Id = "dominate", Name = "Dominate", Text = "Freeze a mind in place for two turns.", Range = 4, Damage = 1, Type = DamageType.Psychic, Stun = 2, Cooldown = 3, Sfx = new[] { "...!" } });
            Add(new Power { Id = "mass-panic", Name = "Mass Panic", Text = "Everyone near the target forgets how to fight.", Target = Targeting.Tile, Range = 6, Area = 2, Damage = 2, Type = DamageType.Psychic, Stun = 1, Signature = true, Sfx = new[] { "AAAAAH!" } });

            Add(new Power { Id = "taser", Name = "Wrist Taser", Text = "Shocks and tags the target: tagged villains take more damage and can't slip away.", Range = 4, Damage = 3, Type = DamageType.Energy, Mark = 2, Sfx = Zap });
            Add(new Power { Id = "turret", Name = "Pocket Turret", Text = "Deploy a turret that fires at the nearest foe every turn.", Target = Targeting.Tile, Range = 2, Summon = 1, Cooldown = 4, Sfx = new[] { "KLIK-KLAK!" } });
            Add(new Power { Id = "orbital", Name = "Orbital Ping", Text = "Call down the satellite on anyone, anywhere.", Range = 99, Damage = 7, Type = DamageType.Energy, Signature = true, Sfx = new[] { "SKRAAAAAM!" } });

            Add(new Power { Id = "hex-bolt", Name = "Hex Bolt", Text = "A cold curse that tags the target.", Range = 5, Damage = 2, Type = DamageType.Cold, Mark = 2, Sfx = Ice });
            Add(new Power { Id = "ward", Name = "Ward", Text = "Shield an ally and clear their head.", Target = Targeting.Ally, Range = 4, Shield = 4, Cleanse = true, Cooldown = 2, Sfx = new[] { "SHIMMM!" } });
            Add(new Power { Id = "second-dawn", Name = "Second Dawn", Text = "Heal the whole team.", Target = Targeting.Self, AllAllies = true, Heal = 5, Shield = 2, Signature = true, Sfx = new[] { "HMMMMMM!" } });

            Add(new Power { Id = "turret-shot", Name = "Turret Shot", Range = 4, Damage = 2, Type = DamageType.Energy, Sfx = new[] { "PEW!" } });

            // ---------- villains ----------
            Add(new Power { Id = "smash", Name = "Smash", Damage = 4, Knockback = 1, Sfx = Thud });
            Add(new Power { Id = "quake", Name = "Quake", Target = Targeting.Self, Area = 1, Damage = 3, Stun = 1, Cooldown = 3, Sfx = new[] { "KRA-THOOM!" } });
            Add(new Power { Id = "snipe", Name = "Snipe", Range = 7, Damage = 4, Type = DamageType.Energy, Sfx = new[] { "KRAK-OW!", "PTANG!" } });
            Add(new Power { Id = "ricochet", Name = "Ricochet", Target = Targeting.Tile, Range = 6, Area = 1, Damage = 3, Type = DamageType.Energy, Cooldown = 3, Sfx = new[] { "PING-PANG-PONG!" } });
            Add(new Power { Id = "neural-whip", Name = "Neural Whip", Range = 4, Damage = 3, Type = DamageType.Psychic, Sfx = Mind });
            Add(new Power { Id = "call-goons", Name = "Call the Boys", Target = Targeting.Self, Summon = 2, Cooldown = 4, Sfx = new[] { "OI! OVER HERE!" } });
            Add(new Power { Id = "mind-crush", Name = "Mind Crush", Range = 5, Damage = 2, Type = DamageType.Psychic, Stun = 1, Cooldown = 3, Sfx = new[] { "SKREEEE!" } });
            Add(new Power { Id = "flame-lash", Name = "Flame Lash", Range = 4, Damage = 3, Type = DamageType.Fire, Burn = 2, Sfx = Burn });
            Add(new Power { Id = "firestorm", Name = "Firestorm", Target = Targeting.Tile, Range = 5, Area = 1, Damage = 3, Type = DamageType.Fire, Burn = 1, Cooldown = 3, Sfx = new[] { "KA-FWOOOM!" } });
            Add(new Power { Id = "ice-lance", Name = "Ice Lance", Range = 5, Damage = 3, Type = DamageType.Cold, Sfx = Ice });
            Add(new Power { Id = "blizzard", Name = "Blizzard", Target = Targeting.Tile, Range = 5, Area = 1, Damage = 2, Type = DamageType.Cold, Stun = 1, Cooldown = 3, Sfx = new[] { "WHOOOOSH!" } });
            Add(new Power { Id = "flurry", Name = "Flurry", Damage = 2, Hits = 2, Sfx = new[] { "BAP-BAP-BAP!" } });
            Add(new Power { Id = "hit-and-run", Name = "Hit and Run", Range = 5, Dash = true, Damage = 3, Cooldown = 2, Sfx = new[] { "VWOOSH—SMAK!" } });
            Add(new Power { Id = "jinx", Name = "Jinx", Range = 5, Damage = 2, Type = DamageType.Psychic, Mark = 2, Sfx = new[] { "HEH HEH!" } });
            Add(new Power { Id = "vanish", Name = "Now You See Me", Target = Targeting.Tile, Range = 5, Teleport = true, Shield = 3, Cooldown = 3, Sfx = new[] { "POOF!" } });
            Add(new Power { Id = "smoke-and-mirrors", Name = "Smoke and Mirrors", Target = Targeting.Tile, Range = 5, Area = 1, Damage = 1, Type = DamageType.Psychic, Stun = 1, Cooldown = 3, Sfx = new[] { "PFFSSHH!" } });

            Add(new Power { Id = "punch", Name = "Punch", Damage = 2, Sfx = Thud });
            Add(new Power { Id = "shoot", Name = "Shoot", Range = 5, Damage = 2, Type = DamageType.Energy, Sfx = new[] { "BLAM!", "BRAKKA!" } });
            Add(new Power { Id = "slam", Name = "Slam", Damage = 3, Knockback = 1, Sfx = Thud });
        }

        public static readonly Dictionary<string, ArchetypeDef> Archetypes = new Dictionary<string, ArchetypeDef>
        {
            ["brick"] = new ArchetypeDef { Id = "brick", Name = "Brick", Hp = 19, Move = 3, Powers = new[] { "haymaker", "ground-pound", "meteor-slam" }, Text = "Tough, slow, hits like a truck." },
            ["speedster"] = new ArchetypeDef { Id = "speedster", Name = "Speedster", Hp = 13, Move = 6, Powers = new[] { "rapid-strikes", "blur-dash", "cyclone" }, Text = "Everywhere at once, fragile if caught." },
            ["blaster"] = new ArchetypeDef { Id = "blaster", Name = "Blaster", Hp = 14, Move = 4, Powers = new[] { "bolt", "nova", "supernova" }, Text = "Long range and things on fire." },
            ["mentalist"] = new ArchetypeDef { Id = "mentalist", Name = "Mentalist", Hp = 13, Move = 4, Powers = new[] { "mind-spike", "dominate", "mass-panic" }, Text = "Stops a fight before it starts." },
            ["gadgeteer"] = new ArchetypeDef { Id = "gadgeteer", Name = "Gadgeteer", Hp = 15, Move = 4, Powers = new[] { "taser", "turret", "orbital" }, Text = "Tags villains so they can't run." },
            ["mystic"] = new ArchetypeDef { Id = "mystic", Name = "Mystic", Hp = 14, Move = 4, Powers = new[] { "hex-bolt", "ward", "second-dawn" }, Text = "Keeps the team standing." },

            ["brute"] = new ArchetypeDef { Id = "brute", Name = "Brute", Hp = 18, Move = 3, Powers = new[] { "smash", "quake" } },
            ["marksman"] = new ArchetypeDef { Id = "marksman", Name = "Marksman", Hp = 13, Move = 4, Powers = new[] { "snipe", "ricochet" } },
            ["mastermind"] = new ArchetypeDef { Id = "mastermind", Name = "Mastermind", Hp = 14, Move = 3, Powers = new[] { "neural-whip", "call-goons", "mind-crush" } },
            ["pyro"] = new ArchetypeDef { Id = "pyro", Name = "Pyromaniac", Hp = 14, Move = 4, Powers = new[] { "flame-lash", "firestorm" } },
            ["cryo"] = new ArchetypeDef { Id = "cryo", Name = "Cryomancer", Hp = 14, Move = 4, Powers = new[] { "ice-lance", "blizzard" } },
            ["blitz"] = new ArchetypeDef { Id = "blitz", Name = "Speed Demon", Hp = 12, Move = 6, Powers = new[] { "flurry", "hit-and-run" } },
            ["trickster"] = new ArchetypeDef { Id = "trickster", Name = "Trickster", Hp = 12, Move = 4, Powers = new[] { "jinx", "vanish", "smoke-and-mirrors" } },
        };
        public static readonly string[] HeroArchetypes = { "brick", "speedster", "blaster", "mentalist", "gadgeteer", "mystic" };
        public static readonly string[] VillainArchetypes = { "brute", "marksman", "mastermind", "pyro", "cryo", "blitz", "trickster" };

        public static readonly Dictionary<string, HenchDef> Henchmen = new Dictionary<string, HenchDef>
        {
            ["goon"] = new HenchDef { Id = "goon", Name = "Goon", Hp = 5, Move = 4, Power = "punch", PromotesTo = "brute" },
            ["gunman"] = new HenchDef { Id = "gunman", Name = "Gunman", Hp = 4, Move = 3, Power = "shoot", PromotesTo = "marksman" },
            ["bruiser"] = new HenchDef { Id = "bruiser", Name = "Bruiser", Hp = 8, Move = 3, Power = "slam", PromotesTo = "brute" },
        };

        public static readonly Dictionary<string, Trait> Traits = new Dictionary<string, Trait>
        {
            ["fireproof"] = new Trait { Id = "fireproof", Name = "Fireproof Suit", Text = "Fire does half damage.", Resist = DamageType.Fire },
            ["insulated"] = new Trait { Id = "insulated", Name = "Insulated", Text = "Energy does half damage.", Resist = DamageType.Energy },
            ["thermal"] = new Trait { Id = "thermal", Name = "Thermal Lining", Text = "Cold does half damage.", Resist = DamageType.Cold },
            ["iron-will"] = new Trait { Id = "iron-will", Name = "Iron Will", Text = "Psychic does half damage, and stuns don't take.", Resist = DamageType.Psychic, StunImmune = true },
            ["armored"] = new Trait { Id = "armored", Name = "Armour Plating", Text = "Physical hits do 2 less.", Armor = 2 },
            ["quick"] = new Trait { Id = "quick", Name = "Quick Feet", Text = "+2 movement.", Move = 2 },
            ["cybernetic"] = new Trait { Id = "cybernetic", Name = "Cybernetic Arm", Text = "Melee hits do 2 more.", Melee = 2 },
            ["hardened"] = new Trait { Id = "hardened", Name = "Prison-Hardened", Text = "+5 health.", Hp = 5 },
            ["hero-breaker"] = new Trait { Id = "hero-breaker", Name = "Hero-Breaker", Text = "Has put a hero down. Every hit does 1 more.", Damage = 1 },
            ["slippery"] = new Trait { Id = "slippery", Name = "Slippery", Text = "Much harder to keep from escaping.", Escape = 0.25 },
            ["triumphant"] = new Trait { Id = "triumphant", Name = "Triumphant", Text = "Beat the heroes once. +3 health.", Hp = 3 },
            ["weak-fire"] = new Trait { Id = "weak-fire", Name = "Afraid of Fire", Text = "Fire does 50% more.", Weak = DamageType.Fire },
            ["weak-cold"] = new Trait { Id = "weak-cold", Name = "Hates the Cold", Text = "Cold does 50% more.", Weak = DamageType.Cold },
            ["weak-energy"] = new Trait { Id = "weak-energy", Name = "Faulty Wiring", Text = "Energy does 50% more.", Weak = DamageType.Energy },
            ["weak-psychic"] = new Trait { Id = "weak-psychic", Name = "Fragile Mind", Text = "Psychic does 50% more.", Weak = DamageType.Psychic },
        };

        public static string ResistTraitFor(DamageType t)
        {
            switch (t)
            {
                case DamageType.Fire: return "fireproof";
                case DamageType.Energy: return "insulated";
                case DamageType.Cold: return "thermal";
                case DamageType.Psychic: return "iron-will";
                default: return "armored";
            }
        }

        public static readonly PlaceDef[] Places =
        {
            new PlaceDef { Id = "rooftops", Name = "the Rooftops", Cover = 6, Walls = 2, Barrels = 1, Floor = "#9aa6b8" },
            new PlaceDef { Id = "docks", Name = "the Docks", Cover = 8, Walls = 1, Barrels = 3, Floor = "#a39a86" },
            new PlaceDef { Id = "power-plant", Name = "the Power Plant", Cover = 5, Walls = 3, Barrels = 2, Floor = "#9fb0a2" },
            new PlaceDef { Id = "museum", Name = "the Museum", Cover = 7, Walls = 3, Barrels = 0, Floor = "#c1ab8e" },
            new PlaceDef { Id = "subway", Name = "the Subway", Cover = 4, Walls = 4, Barrels = 2, Floor = "#8e95a6" },
            new PlaceDef { Id = "chem-plant", Name = "the Chemical Plant", Cover = 5, Walls = 2, Barrels = 4, Floor = "#a7b38f" },
        };

        public static readonly UpgradeDef[] Upgrades =
        {
            new UpgradeDef { Id = "hp", Name = "Tougher", Text = "+4 health" },
            new UpgradeDef { Id = "move", Name = "Faster", Text = "+1 movement" },
            new UpgradeDef { Id = "damage", Name = "Stronger", Text = "+1 damage on every hit" },
            new UpgradeDef { Id = "momentum", Name = "Fired Up", Text = "Start each fight with 2 momentum" },
            new UpgradeDef { Id = "cooldown", Name = "Practised", Text = "Specials recharge a turn sooner" },
        };

        // The starting roster: six originals, one of each kind.
        public static List<Hero> Roster() => new List<Hero>
        {
            new Hero { Id = "bulwark", Name = "Bulwark", Archetype = "brick", Tagline = "The wall between the city and the worst of it.", Look = new Look { Primary = "#2a5db0", Secondary = "#ffd100", Skin = "#8d5a3b", Mask = 0, Emblem = 0, Hair = 0 } },
            new Hero { Id = "kestrel", Name = "Kestrel", Archetype = "speedster", Tagline = "Already there. Already gone.", Look = new Look { Primary = "#d62828", Secondary = "#ffe066", Skin = "#f1c9a5", Mask = 3, Emblem = 1, Hair = 1 } },
            new Hero { Id = "noonday", Name = "Noonday", Archetype = "blaster", Tagline = "Carries a small sun around. Mostly harmless.", Look = new Look { Primary = "#ffd100", Secondary = "#c8102e", Skin = "#c68b62", Mask = 1, Emblem = 2, Hair = 3 } },
            new Hero { Id = "reverie", Name = "Reverie", Archetype = "mentalist", Tagline = "Knows what you're about to do. Knows you know.", Look = new Look { Primary = "#6a2c91", Secondary = "#00b2c8", Skin = "#e8b896", Mask = 4, Emblem = 3, Hair = 4, Cape = true } },
            new Hero { Id = "patchwork", Name = "Patchwork", Archetype = "gadgeteer", Tagline = "Forty pockets. Forty gadgets. One duct tape.", Look = new Look { Primary = "#2f8f5b", Secondary = "#f2f2f2", Skin = "#f5d0b0", Mask = 3, Emblem = 4, Hair = 2 } },
            new Hero { Id = "vesper", Name = "Vesper", Archetype = "mystic", Tagline = "Reads the stars. They read back.", Look = new Look { Primary = "#13294b", Secondary = "#9ad1ff", Skin = "#a8714f", Mask = 5, Emblem = 5, Hair = 4, Cape = true } },
        };

        // ---------- villain parts ----------
        public static readonly string[] Prefixes = { "Doctor", "Baron", "Madame", "Professor", "Captain", "Lady", "Mister", "The", "General", "Sister" };
        public static readonly Dictionary<string, string[]> Cores = new Dictionary<string, string[]>
        {
            ["brute"] = new[] { "Rubble", "Ironside", "Granite", "Crowbar", "Boilerplate", "Girder", "Pallet" },
            ["marksman"] = new[] { "Plumbline", "Carom", "Sightline", "Spyglass", "Windage", "Slingshot" },
            ["mastermind"] = new[] { "Abacus", "Marionette", "Stalemate", "Switchboard", "Gridlock", "Ledger" },
            ["pyro"] = new[] { "Sulfur", "Kindling", "Matchstick", "Cinderblock", "Tinderbox", "Ashcan" },
            ["cryo"] = new[] { "Hailstone", "Rime", "Permafrost", "Frostline", "Overcast", "Sleet" },
            ["blitz"] = new[] { "Breakneck", "Skid", "Afterimage", "Redline", "Rush Hour", "Tailspin" },
            ["trickster"] = new[] { "Pinwheel", "Smirk", "Halftone", "Sleight", "Snickersnee", "Mothwing" },
        };
        public static readonly string[] FirstNames = { "Wendell", "Agnes", "Victor", "Marla", "Otto", "Delia", "Rufus", "Ines", "Silas", "Greta", "Leon", "Bettina", "Hollis", "Margo", "Cyrus", "Edith" };
        public static readonly string[] LastNames = { "Price", "Krull", "Vance", "Hobbes", "Moreau", "Stroud", "Pike", "Lund", "Okafor", "Varga", "Rook", "Sallow", "Dunmore", "Quist" };
        public static readonly Dictionary<string, string[]> Jobs = new Dictionary<string, string[]>
        {
            ["brute"] = new[] { "demolition foreman", "prize fighter", "bouncer at the Blue Anchor", "steelworks welder" },
            ["marksman"] = new[] { "carnival sharpshooter", "disgraced police marksman", "biathlon champion" },
            ["mastermind"] = new[] { "chess grandmaster", "city planner", "insurance actuary", "neuroscience professor" },
            ["pyro"] = new[] { "fireworks maker", "boiler-room engineer", "special-effects technician" },
            ["cryo"] = new[] { "cold-storage manager", "glaciologist", "ice-sculpture artist" },
            ["blitz"] = new[] { "bike courier", "getaway driver", "sprinter banned for doping" },
            ["trickster"] = new[] { "stage magician", "con artist", "children's party clown", "forger" },
        };
        public static readonly Dictionary<string, string[]> Accidents = new Dictionary<string, string[]>
        {
            ["brute"] = new[] { "was buried under a collapsing chemical vat", "took an experimental growth serum on a dare" },
            ["marksman"] = new[] { "lost an eye and replaced it with something better", "was fired on live television" },
            ["mastermind"] = new[] { "wired a lab helmet straight into the city grid", "lost everything to a bad bet on the heroes" },
            ["pyro"] = new[] { "walked out of a warehouse fire without a scratch", "swallowed a jar of something that glowed" },
            ["cryo"] = new[] { "spent three nights locked in a freezer at minus forty", "fell through the ice of the reservoir" },
            ["blitz"] = new[] { "was struck by lightning mid-sprint", "drove through a particle accelerator's test tunnel" },
            ["trickster"] = new[] { "found a mirror that showed tomorrow", "made a deal with a man who wasn't there" },
        };
        public static readonly Dictionary<string, string[]> Goals = new Dictionary<string, string[]>
        {
            ["brute"] = new[] { "wants to knock down everything the city ever built", "wants the heroes to know what it feels like to lose" },
            ["marksman"] = new[] { "takes contracts on anyone with a cape", "keeps a list, and works down it" },
            ["mastermind"] = new[] { "wants to run the city like a machine", "is always three moves ahead, or says so" },
            ["pyro"] = new[] { "wants to watch the skyline burn", "says fire is the only honest thing in town" },
            ["cryo"] = new[] { "wants to freeze the city in its best moment, forever", "wants everyone to feel that cold" },
            ["blitz"] = new[] { "robs three banks before lunch, for fun", "can't stand anyone being faster" },
            ["trickster"] = new[] { "steals things that can't be stolen", "thinks the whole city is a joke and wants it to laugh" },
        };
        public static readonly Dictionary<string, Look> VillainLooks = new Dictionary<string, Look>
        {
            ["brute"] = new Look { Primary = "#4d4d4d", Secondary = "#d62828", Mask = 3, Extra = 1 },
            ["marksman"] = new Look { Primary = "#2b2d42", Secondary = "#e5e5e5", Mask = 4, Extra = 2 },
            ["mastermind"] = new Look { Primary = "#3c096c", Secondary = "#9ef01a", Mask = 3, Extra = 3, Cape = true },
            ["pyro"] = new Look { Primary = "#9d0208", Secondary = "#ffd60a", Mask = 2, Extra = 4 },
            ["cryo"] = new Look { Primary = "#cde7f0", Secondary = "#1d3557", Mask = 4, Extra = 5 },
            ["blitz"] = new Look { Primary = "#111111", Secondary = "#f9f871", Mask = 1, Extra = 6 },
            ["trickster"] = new Look { Primary = "#2d6a4f", Secondary = "#ff4d6d", Mask = 2, Extra = 7, Cape = true },
        };
        public static readonly string[] HenchNicknames = { "Knuckles", "Two-Tone", "Big Sal", "Mickey Gears", "Lefty", "Dutch", "Sparky", "Moose", "Tiny", "Fingers", "Crash", "Biscuit" };
        public static readonly string[] TeamNames = { "The Night Shift", "The Late Edition", "The Midnight Union", "The Halftone Five", "Overtime", "The Last Word", "The Spare Change" };
    }
}
