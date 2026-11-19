using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Media;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Runs two disposable New Game sessions with a shared clock and player input.</summary>
internal sealed class LocalizationModPreview(ModPreviewResourcesProfile profile, ModCatalogSnapshot catalog, string? mediaToolsDirectory = null, IReadOnlyList<ModPackage>? availablePackages = null) : IDisposable
{
    private const int MaximumActionFrames = 120;
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task<ModPreviewComparison>? active;
    private PreviewSide? before;
    private PreviewSide? after;
    private FfmpegMovieDecoder? decoder;
    private NativeMovieSource? decodedSource;
    private MovieFrame? decodedFrame;
    private double movieFrameRemainder;
    private string? mediaIssue;
    private string? stopped;
    private (int Field, int Channel, int Message, int Object, int Script, int Operation)? choiceOwner;
    private int choice;
    private bool disposed;

    public Task<ModPreviewComparison> RenderAsync(PlayerAction? action, CancellationToken token)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (active is { IsCompleted: false })
            {
                throw new InvalidOperationException("A localization comparison frame is already in progress.");
            }
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            active = Task.Run(() =>
            {
                using (linked)
                {
                    return Render(action, linked.Token);
                }
            });
            return active;
        }
    }

    private ModPreviewComparison Render(PlayerAction? action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (before is null)
        {
            Open(token);
            action ??= new(PlayerActionKind.Wait, Frames: 1);
        }
        PreviewSide original = before!;
        PreviewSide localized = after!;
        if (stopped is null && action is not null)
        {
            Advance(original, localized, action, token);
        }
        if (original.Session.IsMoviePlaying && localized.Session.IsMoviePlaying && original.Session.State.MovieSource == localized.Session.State.MovieSource)
        {
            ReadMovieFrame(original, localized, token);
        }
        else
        {
            ReleaseMovie();
        }
        return new(RenderSide(original), RenderSide(localized));
    }

    private void Open(CancellationToken token)
    {
        ModPackage selected = catalog.Packages.Single(package => package.HasLocalization);
        ModCatalogSnapshot candidates = new(catalog.Generation, new[] { selected }.Concat((availablePackages ?? []).Where(package => !package.Id.Equals(selected.Id, StringComparison.OrdinalIgnoreCase))).ToArray(), [], catalog.Diagnostics);
        PreviewSide original = PreviewSide.Open(profile, mediaToolsDirectory, LocalizationCatalog.Empty);
        try
        {
            LocalizationPreparation localization = LocalizationRuntime.PrepareAuthoringProject(candidates, selected.Id, original.Resources.Source, token, readOnly: true);
            using (localization.InputRevision)
            {
                token.ThrowIfCancellationRequested();
                string language = localization.Layers.Single(layer => layer.PackageId.Equals(selected.Id, StringComparison.OrdinalIgnoreCase)).Project.Manifest.TargetLanguage;
                after = PreviewSide.Open(profile, mediaToolsDirectory, localization.Catalog, original.Clock, language);
            }
            before = original;
        }
        catch
        {
            original.Dispose();
            throw;
        }
    }

    private void Advance(PreviewSide original, PreviewSide localized, PlayerAction action, CancellationToken token)
    {
        if (action.Frames is < 1 or > MaximumActionFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(action), $"Preview input requires between one and {MaximumActionFrames} frames.");
        }
        original.World.EnsureField();
        localized.World.EnsureField();
        if (!AreSynchronized(original, localized))
        {
            return;
        }
        if (original.World.HasPendingBattle)
        {
            AdvanceBattle(original, localized, action);
            return;
        }
        LocalizationTutorialPreview? originalTutorial = original.ReadTutorial();
        LocalizationTutorialPreview? localizedTutorial = localized.ReadTutorial();
        if (originalTutorial is not null || localizedTutorial is not null)
        {
            AdvanceTutorial(originalTutorial, localizedTutorial, action);
            return;
        }
        if (original.Session.IsMoviePlaying)
        {
            AdvanceMovie(original, localized, action);
        }
        else if (!AdvanceFieldInput(original, localized, action) && action.Kind is PlayerActionKind.Wait or PlayerActionKind.Move)
        {
            for (int frame = 0; frame < action.Frames; frame++)
            {
                token.ThrowIfCancellationRequested();
                Step(original, action);
                Step(localized, action);
                if (!AreSynchronized(original, localized) || original.Session.IsMoviePlaying || original.World.HasPendingBattle || original.World.NextTrigger.Kind == PlayerTriggerKind.Tutorial)
                {
                    break;
                }
            }
        }
        original.World.CommitFieldPresentation();
        localized.World.CommitFieldPresentation();
        AreSynchronized(original, localized);
    }

    private bool AdvanceFieldInput(PreviewSide original, PreviewSide localized, PlayerAction action)
    {
        if (original.World.NextTrigger.Kind == PlayerTriggerKind.NativeGameOver)
        {
            stopped = "The localization comparison reached a game over.";
            return true;
        }
        if (original.World.PendingEntityName is not null && action.Kind == PlayerActionKind.Confirm)
        {
            action = new(PlayerActionKind.EnterName);
        }
        DialogueState? first = original.World.CurrentDialogue;
        DialogueState? second = localized.World.CurrentDialogue;
        if (first is not null && second is not null)
        {
            var owner = (original.Session.State.FieldId, first.Channel, first.MessageId, first.ObjectId, first.ScriptId, first.Operation);
            if (choiceOwner != owner)
            {
                choiceOwner = owner;
                choice = (first.InitialChoice ?? first.FirstChoice ?? 0) - (first.FirstChoice ?? 0);
            }
            if (action.Kind == PlayerActionKind.Move && first.FirstChoice is int start && first.LastChoice is int end)
            {
                choice = Math.Clamp(choice + Math.Sign(action.Vertical), 0, end - start);
                return true;
            }
            if (action.Kind is PlayerActionKind.Confirm or PlayerActionKind.Cancel or PlayerActionKind.Choose or PlayerActionKind.EnterName)
            {
                if (!CanReceiveDialogueInput(original, first) || !CanReceiveDialogueInput(localized, second))
                {
                    return true;
                }
                PlayerAction ForDialogue(DialogueState dialogue)
                {
                    if (action.Kind != PlayerActionKind.Confirm)
                    {
                        return action;
                    }
                    if (dialogue.IsNamePrompt)
                    {
                        return new(PlayerActionKind.EnterName);
                    }
                    return dialogue.FirstChoice is int firstChoice ? new(PlayerActionKind.Choose, firstChoice + choice) : action;
                }
                ApplyBoth(original, localized, ForDialogue(first), ForDialogue(second));
                choiceOwner = null;
                return true;
            }
        }
        else
        {
            choiceOwner = null;
        }
        if (action.Kind is not (PlayerActionKind.Wait or PlayerActionKind.Move))
        {
            ApplyBoth(original, localized, action, action);
            return true;
        }
        return false;
    }

    private static bool CanReceiveDialogueInput(PreviewSide side, DialogueState dialogue)
    {
        if (!dialogue.CanConfirm)
        {
            return false;
        }
        if (side.Session.State.Scene == SceneKind.WorldMap)
        {
            return true;
        }
        EventEngine events = side.World.Events;
        return events.CanReceiveDialogueInput(dialogue) && (dialogue.FirstChoice is not null || dialogue.IsNamePrompt || events.Dialogues.Where(window => window.CanConfirm && window.FirstChoice is null && !window.IsNamePrompt).All(events.CanReceiveDialogueInput));
    }

    private void ApplyBoth(PreviewSide original, PreviewSide localized, PlayerAction first, PlayerAction second)
    {
        bool accepted = original.World.Apply(first).Accepted;
        if (localized.World.Apply(second).Accepted != accepted)
        {
            stopped = "The original and translation reacted differently to the same input.";
        }
    }

    private static void Step(PreviewSide side, PlayerAction action)
    {
        GameWorld world = side.World;
        world.EnsureField();
        if (side.Session.State.Scene == SceneKind.Field && world.PendingEntityName is null && world.Events.PartyMenu is null && world.NextTrigger.Kind is not (PlayerTriggerKind.Battle or PlayerTriggerKind.TripleTriad or PlayerTriggerKind.Tutorial or PlayerTriggerKind.NativeGameOver))
        {
            if (action.Kind == PlayerActionKind.Move && world.Events.IsUserControlEnabled && !world.Events.IsMovementBlockedByDialogue)
            {
                world.Apply(action with { Frames = 1 });
            }
            else
            {
                world.Events.Step();
            }
        }
        else
        {
            world.Apply(action with { Frames = 1 });
        }
        world.EnsureField();
    }

    private static bool SameDialogue(DialogueState? first, DialogueState? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }
        return first.Channel == second.Channel && first.MessageId == second.MessageId && first.ObjectId == second.ObjectId && first.ScriptId == second.ScriptId && first.Operation == second.Operation && first.IsNamePrompt == second.IsNamePrompt && first.LastChoice - first.FirstChoice == second.LastChoice - second.FirstChoice;
    }

    private bool AreSynchronized(PreviewSide original, PreviewSide localized)
    {
        GameState first = original.Session.State;
        GameState second = localized.Session.State;
        if (first.Scene == second.Scene && first.FieldId == second.FieldId && first.MovieSource == second.MovieSource && first.MoviePlayback?.FrameIndex == second.MoviePlayback?.FrameIndex && original.World.NextTrigger.Frame == localized.World.NextTrigger.Frame && SameDialogue(original.World.CurrentDialogue, localized.World.CurrentDialogue) && original.World.HasPendingBattle == localized.World.HasPendingBattle && first.PendingBattleEncounter?.Formation == second.PendingBattleEncounter?.Formation)
        {
            return true;
        }
        stopped ??= "The original and translation reached different game states. Reopen the comparison to restart from New Game.";
        return false;
    }

    private void AdvanceBattle(PreviewSide original, PreviewSide localized, PlayerAction action)
    {
        if (original.Battle is null)
        {
            byte seed = (byte)RandomNumberGenerator.GetInt32(256);
            original.Battle = LocalizationBattlePreview.Start(original.World, original.Session.State, seed);
            localized.Battle = LocalizationBattlePreview.Start(localized.World, localized.Session.State, seed);
        }
        original.Battle.Advance(action);
        localized.Battle!.Advance(action);
        if (original.World.Battle is null && localized.World.Battle is null)
        {
            original.ClearBattle();
            localized.ClearBattle();
            original.World.EnsureField();
            localized.World.EnsureField();
        }
        else if (!LocalizationBattlePreview.AreSynchronized(original.Battle, localized.Battle, original.World.Battle is not null, localized.World.Battle is not null))
        {
            stopped = "The original and translation reached different battle states.";
        }
        AreSynchronized(original, localized);
    }

    private void AdvanceTutorial(LocalizationTutorialPreview? original, LocalizationTutorialPreview? localized, PlayerAction action)
    {
        if (original is null || localized is null || !original.IsSynchronizedWith(localized))
        {
            stopped = "The original and translation reached different tutorial states.";
            return;
        }
        bool accepted = original.Advance(action);
        if (localized.Advance(action) != accepted || !original.IsSynchronizedWith(localized))
        {
            stopped = "The original and translation reacted differently to tutorial input.";
        }
    }

    private void AdvanceMovie(PreviewSide original, PreviewSide localized, PlayerAction action)
    {
        if (!HasMediaTools(original.Clock))
        {
            mediaIssue = "The opening movie requires FFmpeg and FFprobe. Set up the media tools to continue.";
            return;
        }
        if (action.Kind is PlayerActionKind.Confirm or PlayerActionKind.Cancel)
        {
            action = new(PlayerActionKind.SkipMovie);
        }
        if (action.Kind is not (PlayerActionKind.Wait or PlayerActionKind.Move or PlayerActionKind.SkipMovie))
        {
            return;
        }
        try
        {
            if (action.Kind == PlayerActionKind.Wait)
            {
                MovieInformation native = original.Clock.Prepare();
                double framesDue = movieFrameRemainder + action.Frames * native.FramesPerSecond / EventEngine.SimulationFps;
                int frames = (int)framesDue;
                movieFrameRemainder = framesDue - frames;
                if (frames != 0)
                {
                    int originalFrames = original.Clock.AdvanceFrames(frames);
                    if (localized.Clock.AdvanceFrames(frames) != originalFrames)
                    {
                        stopped = "The original and translation advanced by different movie frames.";
                    }
                }
            }
            else
            {
                ApplyBoth(original, localized, action, action);
            }
            original.World.EnsureField();
            localized.World.EnsureField();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            stopped = $"The movie timeline is unavailable. {error.Message}";
        }
    }

    private static bool HasMediaTools(MovieClock clock) => MediaToolsInstallation.ToolNames.All(tool => File.Exists(MediaToolPaths.ResolveExecutable(clock.ToolsDirectory, tool)));

    private void ReadMovieFrame(PreviewSide original, PreviewSide localized, CancellationToken token)
    {
        if (!HasMediaTools(original.Clock))
        {
            mediaIssue = "The opening movie requires FFmpeg and FFprobe. Set up the media tools to continue.";
            return;
        }
        NativeMovieSource source = MovieClock.RequireSource(original.Session.State);
        if (source != decodedSource)
        {
            ReleaseMovie();
            decodedSource = source;
            try
            {
                MovieInformation movie = original.Clock.Prepare();
                localized.Clock.Prepare();
                decoder = new(original.Clock.ToolsDirectory, original.Clock.SourcePath, information: movie);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception)
            {
                mediaIssue = $"The movie image is unavailable. {error.Message}";
            }
        }
        if (decoder is null)
        {
            return;
        }
        int target = original.Clock.State.FrameIndex;
        long deadline = Environment.TickCount64 + 5000;
        while (decodedFrame is null || decodedFrame.Index < target)
        {
            token.ThrowIfCancellationRequested();
            while (decoder.TryGetAudio(out _))
            {
            }
            if (decoder.TryGetNextFrame(target / decoder.Information.FramesPerSecond, out MovieFrame? frame))
            {
                decodedFrame = frame;
                mediaIssue = null;
            }
            else if (decoder.VideoFinished || Environment.TickCount64 >= deadline)
            {
                mediaIssue = string.IsNullOrWhiteSpace(decoder.Error) ? "The movie image is unavailable." : $"The movie image is unavailable. {decoder.Error}";
                break;
            }
            else
            {
                token.WaitHandle.WaitOne(5);
            }
        }
    }

    private ModPreviewSide RenderSide(PreviewSide side)
    {
        GameWorld world = side.World;
        if (side.Battle is { } battle && world.Battle is not null)
        {
            side.LastImage = battle.Render();
            return new(stopped ?? "", side.LastImage);
        }
        if (side.ReadTutorial() is { } tutorial)
        {
            side.LastImage = tutorial.Render();
            return new(stopped ?? "", side.LastImage);
        }
        PixelImage? image = null;
        if (side.Session.IsMoviePlaying)
        {
            if (decoder is not null && decodedFrame is not null && world.Events.Scene.NativeMovieMask?.Playback?.DrawRecord is > 0)
            {
                byte[] pixels = world.RenderMovieFrame(decodedFrame.Rgba, decoder.Information.Width, decoder.Information.Height);
                image = CopyImage(world, pixels);
            }
            else if (decoder is not null && decodedFrame is not null)
            {
                image = new(decoder.Information.Width, decoder.Information.Height, MemoryMarshal.Cast<byte, Rgba32>(decodedFrame.Rgba).ToArray());
            }
            else if (world.Field is { } field)
            {
                image = CatalogPreviewPresentation.RenderField(field);
            }
        }
        else if (side.Session.State.Scene == SceneKind.WorldMap || world.CanPresentCurrentField)
        {
            image = CopyImage(world, world.Render(TimeSpan.FromSeconds(world.NextTrigger.Frame / (double)EventEngine.SimulationFps)));
        }
        string? text = ReadDialogue(side);
        side.LastImage = image ?? side.LastImage;
        return new(string.Join("\n\n", new[] { text, stopped, mediaIssue }.Where(value => !string.IsNullOrEmpty(value))), side.LastImage);
    }

    private string? ReadDialogue(PreviewSide side)
    {
        GameWorld world = side.World;
        if (world.PendingEntityName is { } name)
        {
            return name.DefaultName;
        }
        IEnumerable<DialogueState> windows = side.Session.State.Scene == SceneKind.WorldMap
            ? world.CurrentDialogue is { } activeDialogue ? [activeDialogue] : []
            : DialoguePresentation.DrawOrder(world.Events.Dialogues);
        return string.Join("\n\n", windows.Select(native =>
        {
            DialogueState dialogue = side.LocalizeDialogue(native);
            DialogueDocument document = DialogueTextFormatter.Parse(dialogue.Text, world.Events.Scene.Names, world.Events.MessageVariables, terms: world.TextTerms);
            if (world.CurrentDialogue == native && dialogue.FirstChoice is int first && dialogue.LastChoice is int last)
            {
                int selected = choiceOwner is null ? dialogue.InitialChoice ?? first : Math.Clamp(first + choice, first, last);
                return string.Join("\n", document.Pages.SelectMany(page => page.Lines).Select(line => line.SourceLineIndex == selected ? "› " + line.Text : line.Text));
            }
            return document.Text;
        }));
    }

    private static PixelImage CopyImage(GameWorld world, byte[] pixels) => new(world.RenderWidth, world.RenderHeight, MemoryMarshal.Cast<byte, Rgba32>(pixels).ToArray());

    private void ReleaseMovie()
    {
        decoder?.Dispose();
        decoder = null;
        decodedSource = null;
        decodedFrame = null;
        movieFrameRemainder = 0;
        mediaIssue = null;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            lifetime.Cancel();
            if (active is { IsCompleted: false })
            {
                _ = active.ContinueWith(_ => Release(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                return;
            }
        }
        Release();
    }

    private void Release()
    {
        try
        {
            ReleaseMovie();
        }
        finally
        {
            try
            {
                after?.Dispose();
            }
            finally
            {
                before?.Dispose();
                lifetime.Dispose();
            }
        }
    }

    private sealed record PreviewSide(IResourceSource Source, ResourceService Resources, GameAssets Assets, GameSession Session, MovieClock Clock, GameWorld World, string? ContentLanguage) : IDisposable
    {
        public PixelImage? LastImage { get; set; }
        public LocalizationBattlePreview? Battle { get; set; }
        private LocalizationTutorialPreview? tutorial;
        private FieldAssets? dialogueField;
        private string? dialogueScriptHash;

        public DialogueState LocalizeDialogue(DialogueState native)
        {
            if (native.MessageId < 0 || native.IsNamePrompt || Resources.Localization.Values.Count == 0)
            {
                return native;
            }
            if (Session.State.Scene == SceneKind.WorldMap)
            {
                string key = LocalizationSourceExporter.IndexedKey(Resources.Source.Language, "messages_world", native.MessageId);
                return native with { Text = Resources.Localize(key, native.Text) };
            }
            if (World.Field is not { } field || Session.State.Scene != SceneKind.Field)
            {
                return native;
            }
            if (dialogueField != field)
            {
                dialogueField = field;
                dialogueScriptHash = Convert.ToHexString(SHA256.HashData(field.Jsm));
            }
            string fieldKey = FieldLocalizationKey.Create(Resources.Source.Language, field.Name, native.MessageId);
            LocalizationFieldText? translated = Resources.Localization.ResolveField(fieldKey, native.Operation, dialogueScriptHash!, native.FirstChoice, native.LastChoice, native.InitialChoice, native.CancelChoice);
            return RecreateGame.ProjectLocalizedDialogue(native, translated);
        }

        public static PreviewSide Open(ModPreviewResourcesProfile profile, string? tools, LocalizationCatalog localization, MovieClock? synchronizedClock = null, string? contentLanguage = null)
        {
            IResourceSource source = GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
            GameAssets? assets = null;
            MovieClock? clock = null;
            GameWorld? world = null;
            try
            {
                ResourceService resources = new(source, profile.Language);
                if (localization != LocalizationCatalog.Empty)
                {
                    resources.PrepareLocalization(localization).Commit("Apply the selected localization to an isolated preview");
                }
                assets = new(profile.GameDirectory, resources: resources);
                GameSession session = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
                session.StartNewGame();
                clock = synchronizedClock?.CreateSynchronizedPeer(session) ?? new(session, profile.GameDirectory, tools);
                world = new(session, profile.GameDirectory, clock, resources, sharedAssets: assets, expandedBattleAllyCount: 0);
                world.ApplyBattleInput(new(BattleInputKind.ConfigurePlaythrough, FullCombat: true));
                world.ApplyBattleInput(new(BattleInputKind.ConfigureEncounters, RandomEncounters: true));
                world.EnsureField();
                return new(source, resources, assets, session, clock, world, contentLanguage);
            }
            catch
            {
                world?.Dispose();
                clock?.Dispose();
                assets?.Dispose();
                source.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                ClearBattle();
                tutorial?.Dispose();
                World.Dispose();
            }
            finally
            {
                Clock.Dispose();
                Assets.Dispose();
                Source.Dispose();
            }
        }

        public void ClearBattle()
        {
            Battle?.Dispose();
            Battle = null;
        }

        public LocalizationTutorialPreview? ReadTutorial()
        {
            NativeTutorialPlayer? player = World.Tutorial;
            if (!ReferenceEquals(tutorial?.Player, player))
            {
                LocalizationTutorialPreview? previous = tutorial;
                tutorial = player is null ? null : new(World, player, ContentLanguage);
                previous?.Dispose();
            }
            return tutorial;
        }
    }
}
