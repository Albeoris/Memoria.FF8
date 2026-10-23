using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>An isolated practice match. Its collection never writes to a story save.</summary>
internal static class TripleTriadPractice
{
    internal sealed record SavedMatch
    {
        [JsonRequired]
        public required int Version { get; init; }
        [JsonRequired]
        public required Dictionary<int, int> Collection { get; init; }
        [JsonRequired]
        public required int[] Opponent { get; init; }
        [JsonRequired]
        public required TripleTriadSnapshot Match { get; init; }
        [JsonRequired]
        public required uint RandomState { get; init; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        string gameDirectory = LaunchOptions.ConfiguredGameDirectory;
        string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "triple-triad-practice.json");
        string? source = null;
        string? load = null;
        string? preview = null;
        string? player = null;
        string opponent = "0,1,2,3,4";
        TripleTriadRules rules = TripleTriadRules.Open;
        bool autoPlay = false;
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option == "--autoplay")
            {
                autoPlay = true;
                continue;
            }
            if (option is not ("--game-dir" or "--save" or "--state" or "--match" or "--preview" or "--player" or "--opponent" or "--rules") || ++index == args.Length)
            {
                throw new ArgumentException($"Unknown or incomplete Triple Triad argument '{option}'.");
            }
            string value = args[index];
            switch (option)
            {
                case "--game-dir":
                    gameDirectory = Path.GetFullPath(value);
                    break;
                case "--save":
                    savePath = Path.GetFullPath(value);
                    break;
                case "--state":
                    source = Path.GetFullPath(value);
                    break;
                case "--match":
                    load = Path.GetFullPath(value);
                    break;
                case "--preview":
                    preview = Path.GetFullPath(value);
                    break;
                case "--player":
                    player = value;
                    break;
                case "--opponent":
                    opponent = value;
                    break;
                case "--rules":
                    rules = Enum.Parse<TripleTriadRules>(value, true);
                    break;
            }
        }
        if (load is not null && (source is not null || player is not null))
        {
            throw new ArgumentException("Load a practice match or choose a starting collection and deck.");
        }
        if (source is not null && (SamePath(source, savePath) || preview is not null && SamePath(source, preview)) || preview is not null && (SamePath(preview, savePath) || load is not null && SamePath(preview, load)))
        {
            throw new ArgumentException("Practice output must preserve the source story save and use separate paths for JSON and PNG.");
        }
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(gameDirectory);
        SavedMatch? saved = load is null ? null : Read(load, catalog);
        NativeCardInventory? inventory = source is null ? null : GameSession.Load(source).CardInventory ?? throw new InvalidDataException("The story snapshot has no native card collection.");
        inventory?.Validate();
        Dictionary<int, int> collection = saved?.Collection ?? Enumerable.Range(0, 110).ToDictionary(id => id, id => inventory is null ? 1 : Math.Max(0, inventory.AvailableCount(id)));
        int[] opponentHand = saved?.Opponent ?? ParseDeck(opponent);
        TripleTriadController controller = new(catalog.Cards, collection, opponentHand, catalog.Name, saved?.Match.Rules ?? rules);
        new TripleTriadDeckHistory(new GlobalSettingsPaths().TripleTriadDeckHistoryPath).Connect(controller);
        uint randomState = saved?.RandomState ?? inventory?.TripleTriadRandomState ?? 0;
        controller.ChooseOpponentMove = match =>
        {
            TripleTriadNativeAiResult choice = TripleTriadNativeAi.ChooseMove(catalog.Cards, catalog.TradePriorities, match, catalog.AiParameters, 7, 4, randomState, opponentHand);
            randomState = choice.RandomState;
            return choice.Move;
        };
        controller.CreateMatch = selected =>
        {
            TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(controller.Rules, randomState);
            TripleTriadStartingSide starter = TripleTriadPreparation.TossStartingSide(board.RandomState);
            randomState = starter.RandomState;
            return TripleTriadMatch.Create(catalog.Cards, selected, opponentHand, controller.Rules, 1 - starter.NativeSide, board.Elements);
        };
        controller.RestartDraw = match =>
        {
            TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(match.Rules, randomState);
            randomState = board.RandomState;
            match.RestartSuddenDeath(match.CurrentPlayer, board.Elements);
        };
        if (saved is not null)
        {
            controller.RestoreMatch(TripleTriadMatch.Restore(catalog.Cards, saved.Match));
        }
        else if (controller.Rules.HasFlag(TripleTriadRules.Random))
        {
            if (player is not null)
            {
                throw new ArgumentException("Random selects its hand; omit --player.");
            }
            NativeCardInventory practiceCards = inventory ?? NativeCardInventory.CreateNewGame();
            if (inventory is null)
            {
                foreach (int id in collection.Keys)
                {
                    practiceCards = practiceCards.Acquire(id, out _);
                }
            }
            TripleTriadPreparedHand hand = TripleTriadPreparation.SelectRandomHand(practiceCards, randomState);
            randomState = hand.RandomState;
            foreach (int id in hand.Cards)
            {
                controller.ChooseCard(id);
            }
        }
        else if (player is not null || preview is not null || autoPlay)
        {
            int[] hand = player is null ? collection.SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value)).Take(5).ToArray() : ParseDeck(player);
            foreach (int id in hand)
            {
                controller.ChooseCard(id);
            }
            if (controller.Match is null)
            {
                throw new ArgumentException("The selected hand is unavailable in the starting collection.");
            }
        }
        void Save()
        {
            if (controller.Match is not null)
            {
                Write(savePath, new SavedMatch { Version = 1, Collection = collection, Opponent = opponentHand, Match = controller.Match.CaptureSnapshot(), RandomState = randomState });
            }
        }
        controller.Changed += Save;
        if (autoPlay)
        {
            TripleTriadMatch match = controller.Match!;
            while (match.Outcome == TripleTriadOutcome.InProgress)
            {
                if (match.CurrentPlayer == 1)
                {
                    controller.AdvanceOpponent();
                }
                else
                {
                    TripleTriadMove move = match.ChooseMove();
                    controller.Play(move.HandIndex, move.CellIndex);
                }
            }
            Save();
            Console.WriteLine($"Triple Triad practice: {match.Outcome}, {match.PlayerScore}:{match.OpponentScore}, round {match.Round}.");
        }
        using GameResourceHost? resourceHost = preview is not null || !autoPlay ? new GameResourceHost(gameDirectory) : null;
        if (preview is not null)
        {
            using Bitmap bitmap = new(960, 672, PixelFormat.Format32bppArgb);
            using (BattlePreviewCanvas canvas = new(bitmap, gameDirectory, resourceHost?.Resources))
            {
                UiView view = controller.BuildView();
                view.Resize(960, 672);
                view.Draw(canvas, new UiTheme());
            }
            Directory.CreateDirectory(Path.GetDirectoryName(preview)!);
            bitmap.Save(preview, ImageFormat.Png);
            Save();
            Console.WriteLine($"Triple Triad preview: {preview}");
        }
        if (preview is null && !autoPlay)
        {
            using TripleTriadPracticeGame game = new(controller, gameDirectory, Save, () =>
            {
                SavedMatch restored = Read(savePath, catalog);
                if (!collection.OrderBy(pair => pair.Key).SequenceEqual(restored.Collection.OrderBy(pair => pair.Key)) || !opponentHand.SequenceEqual(restored.Opponent))
                {
                    throw new InvalidDataException("Practice collection or opponent changed; open the saved match with --match.");
                }
                controller.RestoreMatch(TripleTriadMatch.Restore(catalog.Cards, restored.Match));
                randomState = restored.RandomState;
            }, resourceHost);
            game.Run();
            Save();
        }
        return 0;
    }

    private static int[] ParseDeck(string value)
    {
        int[] deck = value.Split(',').Select(id => int.Parse(id, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (deck.Length != 5 || deck.Any(id => id is < 0 or >= 110))
        {
            throw new ArgumentException("A hand requires five comma-separated card IDs in 0..109.");
        }
        return deck;
    }

    internal static SavedMatch Read(string path, TripleTriadCatalog catalog)
    {
        SavedMatch saved = JsonSerializer.Deserialize<SavedMatch>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Empty practice match.");
        if (saved.Version != 1 || saved.Collection is null || saved.Collection.Count != 110 || Enumerable.Range(0, 110).Any(id => !saved.Collection.TryGetValue(id, out int count) || count < 0 || count > (id < 77 ? 100 : 1)) || saved.Opponent is not { Length: 5 } || saved.Opponent.Any(id => id is < 0 or >= 110) || saved.Match is null)
        {
            throw new InvalidDataException("Invalid practice collection or opponent hand.");
        }
        _ = TripleTriadMatch.Restore(catalog.Cards, saved.Match);
        int[] originals = saved.Match.InstanceOrder.Select(source => saved.Match.InitialHands[source / 5][source % 5]).ToArray();
        if (!saved.Opponent.SequenceEqual(originals.Skip(5)) || originals.Take(5).GroupBy(id => id).Any(group => saved.Collection.GetValueOrDefault(group.Key) < group.Count()))
        {
            throw new InvalidDataException("Practice match card instances disagree with its collection or original opponent.");
        }
        return saved;
    }

    private static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private static void Write(string path, SavedMatch saved)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(saved, JsonOptions));
        File.Move(temporary, fullPath, true);
    }
}
