using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly Lazy<string?> ff7InstallationIssue;
    private CombinedSessionSave? crossover;
    private readonly CombinedSessionSave? initialCrossover;
    private Ff7Scene? ff7Scene;
    private GameAssets? crossoverAssets;
    private CrossoverMappingMenu? crossoverSetup;
    private CharacterAppearanceMapping[]? pendingCrossoverMappings;
    private GameSession? suspendedFf8Session;
    private GameWorld? suspendedFf8World;
    private bool suspendedFf8PayloadChanged;
    private CombinedSessionSave? pendingMixedOriginResume;
    private bool InFf7 => crossover?.ActiveGame == SessionGame.Ff7;

    private void InitializeCrossover()
    {
        if (initialCrossover is not null)
        {
            RestoreCrossover(initialCrossover);
            return;
        }
        string? path = options.CrossoverStatePath ?? options.StatePath;
        if (path is not null)
        {
            RestoreCrossover(CrossoverSaveAdapter.ReadEnvelope(path));
        }
    }

    private string? Ff8WorldSwitchUnavailable()
    {
        if (ff7InstallationIssue.Value is string issue)
        {
            return issue;
        }
        SaveBoundaryState boundary = ReadSaveBoundaryState(ignoreInterface: true);
        if (boundary.Assess() != SaveBoundaryStatus.Ready)
        {
            return "World switching requires a playable save boundary without an active interaction.";
        }
        if (session.State.Scene == SceneKind.Field && (session.State.WorldControls?.Menu209 & 1) != 1)
        {
            return "World switching is available only where saving is allowed.";
        }
        return null;
    }

    internal static string? GetFf7InstallationIssue(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "No FINAL FANTASY VII folder was selected. Select the installation in the launcher's FINAL FANTASY VII tab, then start the game again.";
        }
        try
        {
            Ff7PcExecutableData.Load(Ff7Installation.Discover(directory));
            return null;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return $"{exception.Message} Select a supported installation in the launcher's FINAL FANTASY VII tab, then start the game again.";
        }
    }

    private byte[] CaptureFf8Payload()
    {
        GameplaySaveComponentDocument document = (world ?? throw new InvalidOperationException("No FFVIII game is active.")).CaptureGameplaySave();
        document = document with { AudioPresentation = fieldAudio?.CapturePlaybackPositions() ?? document.AudioPresentation };
        using MemoryStream destination = new();
        GameplaySaveCodec.Write(destination, document);
        return destination.ToArray();
    }

    private void OpenCrossoverSetup()
    {
        OpenCrossoverSetup(InFf7 ? ff7Scene?.Capture() : null);
    }

    private void OpenCrossoverSetup(CombinedSessionSave? outgoing)
    {
        SessionGame destination = InFf7 ? SessionGame.Ff8 : SessionGame.Ff7;
        string? reason;
        if (InFf7)
        {
            reason = ff7Scene?.CanSave != true ? "World switching is available only where saving is allowed." : outgoing?.Ff8Payload is not { Length: > 0 } ? "No suspended FFVIII session is available." : null;
        }
        else
        {
            reason = Ff8WorldSwitchUnavailable();
        }
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
        }
        CrossoverMappingMenu menu = new(GraphicsDevice, Ff7Installation.Discover(options.Ff7GameDirectory!), options.GameDirectory, outgoing?.Mappings ?? pendingCrossoverMappings ?? crossover?.Mappings ?? [], physicalInput.ReadKeyboard(), Mouse.GetState(), text, uiCanvas!, () => CreateUiTheme(), destination);
        menu.Cancelled += CloseCrossoverSetup;
        menu.Confirmed += mappings =>
        {
            try
            {
                if (destination == SessionGame.Ff8)
                {
                    SwitchToFf8((outgoing ?? throw new InvalidOperationException("The outgoing FFVII continuation is absent.")) with { Mappings = mappings });
                }
                else
                {
                    pendingCrossoverMappings = mappings;
                    SwitchToFf7();
                }
                CloseCrossoverSetup();
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or InvalidOperationException)
            {
                menu.SetNotice(exception.Message);
            }
        };
        crossoverSetup?.Dispose();
        crossoverSetup = menu;
        ff7Scene?.SetFocusPaused(true);
    }

    private void CloseCrossoverSetup()
    {
        crossoverSetup?.Dispose();
        crossoverSetup = null;
        pendingCrossoverMappings = null;
        ff7Scene?.SuppressInitialPresses(physicalInput.ReadKeyboard());
        ff7Scene?.SetFocusPaused(focusPaused || updateSave.Paused);
        keyboard.SuppressHeldUiInput();
        drainUiButtons = true;
    }

    private Ff7Scene PrepareFf7Scene(CombinedSessionSave state)
    {
        Ff7Installation installation = Ff7Installation.Discover(options.Ff7GameDirectory ?? throw new InvalidOperationException("Select a FINAL FANTASY VII installation before loading this world."));
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        string tools = movieClock.ToolsDirectory;
        IFf7ActorAppearance? appearance = null;
        if (state.Mappings.Length > 0)
        {
            crossoverAssets ??= new GameAssets(options.GameDirectory, resources: resourceHost!.Resources, modelOverrides: resourceHost);
        }
        appearance = Ff7AppearanceSelection.Create(state.Mappings, installation, crossoverAssets);
        Ff7Scene scene = new(GraphicsDevice, installation, tools, new Ff7ProjectionDepth(native.FieldNearPlane, native.FieldFarPlane), movie => Ff7Scene.ResolveMovie(installation, tools, movie), state, appearance, options.AudioEnabled);
        try
        {
            scene.ConfigureCharacterIdentity(options.GameDirectory);
            scene.ConfigureDialoguePresentation(() => uiPreferences, text, uiCanvas!, () => CreateUiTheme(), preferences =>
            {
                preferences.Save(UiPreferencesPath);
                uiPreferences = preferences;
                gameMenus?.InvalidateLiveData();
            });
            scene.ConfigureSharedControls(keyboard, gameplayClock, profile => profile.Save(new GlobalSettingsPaths(isolatedDirectory: options.SettingsDirectory).ControlsPreferencesPath));
            scene.PlayerSaveRequested += input => UpdatePlayerSaves(input);
            scene.ContinueRequested += () => OpenPlayerSaveMenu(PlayerSaveMenuMode.Load);
            scene.RenderLimit = options.WorldRenderLimit;
            scene.InterpolatePresentation = !options.ControlStep;
            scene.ConfigureAutoPlayCheckpoint(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.SavePath))!, "autoplay", "ff7", "current.ffsave"));
            scene.Continue(state);
            scene.SuppressInitialPresses(physicalInput.ReadKeyboard());
            scene.SaveRequested += saved =>
            {
                if (options.ControlExclusive)
                {
                    SaveCrossoverFromFf7(saved);
                }
                else
                {
                    OpenPlayerSaveMenu(PlayerSaveMenuMode.Save);
                }
            };
            scene.WorldSwitchRequested += OpenCrossoverSetup;
            scene.MixedCombatSettled += saved =>
            {
                crossover = saved;
                suspendedFf8PayloadChanged = true;
            };
            scene.MixedOriginResumeRequested += saved => pendingMixedOriginResume = saved;
            return scene;
        }
        catch
        {
            scene.Dispose();
            throw;
        }
    }

    private void SwitchToFf7()
    {
        string? reason = Ff8WorldSwitchUnavailable();
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
        }
        byte[] payload = CaptureFf8Payload();
        CombinedSessionSave current = crossover ?? new CombinedSessionSave { ActiveGame = SessionGame.Ff8, Ff8Payload = payload, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
        current = current with { Mappings = pendingCrossoverMappings ?? current.Mappings };
        WorldSwitch transaction = new(current);
        Ff7Scene? prepared = null;
        try
        {
            transaction.ToFf7(new(true), payload, CrossoverSaveAdapter.Ff8Codec, () =>
            {
                using Ff7Session firstVisit = Ff7Session.NewGame(Ff7Installation.Discover(options.Ff7GameDirectory!));
                return firstVisit.Capture();
            }, target => prepared = PrepareFf7Scene(current with { ActiveGame = SessionGame.Ff7, Ff8Payload = payload, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec, Ff7 = target }));
            CommitPreparedFf7Scene(transaction.State, prepared!);
        }
        catch
        {
            prepared?.Dispose();
            throw;
        }
    }

    private void CommitPreparedFf7Scene(CombinedSessionSave state, Ff7Scene prepared)
    {
        ff7Scene?.Dispose();
        ff7Scene = prepared;
        crossover = state;
        suspendedFf8Session = session;
        suspendedFf8World = world;
        suspendedFf8PayloadChanged = false;
        gameMenus?.Close();
        fieldAudio?.SetFocusPaused(true);
        Window.Title = "Memoria - Final Fantasy VII";
        ResetElapsedTime();
    }

    private GameState RestoreFf8Payload(CombinedSessionSave saved)
    {
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(saved);
        return GameplaySaveSessionLoader.Restore(document, options.GameDirectory, world?.ValidationAssets, resourceHost!.Resources, resourceHost, () => resourceHost.GameplayPolicy, initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, resourceHost));
    }

    private void SwitchToFf8(CombinedSessionSave outgoing)
    {
        WorldSwitch transaction = new(crossover ?? throw new InvalidOperationException("No combined game is active."));
        GameState? prepared = null;
        transaction.ToFf8(new(ff7Scene?.CanSave == true), outgoing.Ff7!, (payload, codec) =>
        {
            CombinedSessionSave target = outgoing with { Ff8Payload = payload, Ff8PayloadFormat = codec };
            CrossoverSaveAdapter.ReadFf8(target);
            if (!suspendedFf8PayloadChanged && (suspendedFf8Session is not null || suspendedFf8World is not null))
            {
                if (!ReferenceEquals(suspendedFf8Session, session) || !ReferenceEquals(suspendedFf8World, world))
                {
                    throw new InvalidOperationException("The suspended FFVIII session was replaced while its world was inactive.");
                }
            }
            else
            {
                prepared = RestoreFf8Payload(target);
                ValidatePlayerState(prepared);
            }
        });
        if (prepared is not null)
        {
            RestorePlayerState(prepared);
        }
        crossover = transaction.State with { Mappings = outgoing.Mappings, CharacterNameOverrides = outgoing.CharacterNameOverrides };
        ff7Scene?.Dispose();
        ff7Scene = null;
        suspendedFf8Session = null;
        suspendedFf8World = null;
        suspendedFf8PayloadChanged = false;
        fieldAudio?.SetFocusPaused(false);
        Window.Title = "Memoria - Final Fantasy VIII";
        keyboard.SuppressHeldUiInput();
        drainUiButtons = true;
        ResetElapsedTime();
    }

    private void RestoreCrossover(CombinedSessionSave? saved, Ff7Scene? prepared = null)
    {
        prepared ??= saved?.ActiveGame == SessionGame.Ff7 ? PrepareFf7Scene(saved) : null;
        ff7Scene?.Dispose();
        ff7Scene = prepared;
        crossover = saved;
        suspendedFf8Session = null;
        suspendedFf8World = null;
        suspendedFf8PayloadChanged = false;
        fieldAudio?.SetFocusPaused(InFf7);
        Window.Title = InFf7 ? "Memoria - Final Fantasy VII" : "Memoria - Final Fantasy VIII";
    }

    private void SaveCrossoverFromFf7(CombinedSessionSave saved)
    {
        if (ff7Scene?.CanSave != true)
        {
            throw new InvalidOperationException("Saving is unavailable at the current FFVII boundary.");
        }
        CombinedSessionCodec.Save(options.ControlExclusive ? options.SavePath : options.CrossoverSavePath, saved);
        crossover = saved;
    }

    private bool UpdateCrossover(GameTime time)
    {
        if (InFf7 || crossoverSetup is not null)
        {
            controls?.Poll();
        }
        if (crossoverSetup is not null)
        {
            keyboard.NativeButtonsOwnInput = false;
            keyboard.TextEntryEnabled = false;
            keyboard.NamingNavigationEnabled = false;
            keyboard.ControllerPresentation = controller!.Presentation;
            keyboard.ControllerGeneration = controller.Generation;
            var input = keyboard.Read(windowModeHotkey.Filter(physicalInput.ReadKeyboard()), physicalInput.ReadController(), !focusPaused && IsActive);
            crossoverSetup.Update(input, Mouse.GetState(), GraphicsDevice.Viewport.Bounds);
            presentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
            return true;
        }
        if (!InFf7)
        {
            return false;
        }
        if (saveMenu.IsOpen)
        {
            ff7Scene!.SetFocusPaused(true);
            keyboard.NativeButtonsOwnInput = false;
            keyboard.TextEntryEnabled = false;
            keyboard.NamingNavigationEnabled = false;
            keyboard.ControllerPresentation = controller!.Presentation;
            keyboard.ControllerGeneration = controller.Generation;
            InputFrame input = keyboard.Read(windowModeHotkey.Filter(physicalInput.ReadKeyboard()), physicalInput.ReadController(), !focusPaused && IsActive);
            UpdatePlayerSavePointer();
            UpdatePlayerSaveMenuAsync(input);
            presentationPacer.WaitForNextFrame(ff7Scene.PresentationFrameLimit);
            return true;
        }
        ff7Scene!.SetFocusPaused(focusPaused || updateSave.Paused);
        if (updateSave.Paused)
        {
            presentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
            return true;
        }
        if (UpdateFocusPause())
        {
            return true;
        }
        if (!options.ControlStep)
        {
            GamePadState pad = physicalInput.ReadController();
            keyboard.ControllerPresentation = controller!.Presentation;
            keyboard.ControllerGeneration = controller.Generation;
            ff7Scene!.Update(time, windowModeHotkey.Filter(physicalInput.ReadKeyboard()), pad);
        }
        if (pendingMixedOriginResume is { } mixedOrigin)
        {
            pendingMixedOriginResume = null;
            ResumeFf8AfterMixedDefeat(mixedOrigin);
            return true;
        }
        UpdateDiagnosticExit(time);
        presentationPacer.WaitForNextFrame(ff7Scene!.PresentationFrameLimit);
        return true;
    }

    private void ResumeFf8AfterMixedDefeat(CombinedSessionSave saved)
    {
        GameState restored = RestoreFf8Payload(saved);
        ValidatePlayerState(restored);
        RestorePlayerState(restored);
        crossover = saved with { ActiveGame = SessionGame.Ff8 };
        ff7Scene?.Dispose();
        ff7Scene = null;
        suspendedFf8Session = null;
        suspendedFf8World = null;
        suspendedFf8PayloadChanged = false;
        fieldAudio?.SetFocusPaused(false);
        Window.Title = "Memoria - Final Fantasy VIII";
        keyboard.SuppressHeldUiInput();
        drainUiButtons = true;
        ResetElapsedTime();
    }

    private bool DrawCrossover(GameTime time)
    {
        if (crossoverSetup is not null)
        {
            GraphicsDevice.Clear(Color.Black);
            crossoverSetup.Draw(batch, GraphicsDevice.Viewport.Bounds, uiPreferences.UiFontFamily);
            DrawCrossoverOverlays();
            CapturePresentedFrame(time);
            base.Draw(time);
            return true;
        }
        if (!InFf7)
        {
            return false;
        }
        GraphicsDevice.Clear(Color.Black);
        ff7Scene!.Draw(batch, GraphicsDevice.Viewport.Bounds);
        if (Window.Title != ff7Scene.LocationWindowTitle)
        {
            Window.Title = ff7Scene.LocationWindowTitle;
        }
        DrawCrossoverOverlays();
        CapturePresentedFrame(time);
        base.Draw(time);
        return true;
    }

    private void DrawCrossoverOverlays()
    {
        text.BeginFrame();
        batch.Begin();
        if (saveMenu.IsOpen)
        {
            DrawPlayerSaveMenu(GraphicsDevice.Viewport.Bounds, 1);
        }
        DrawGameConfirmation(GraphicsDevice.Viewport.Bounds);
        DrawWindowCloseConfirmation(GraphicsDevice.Viewport.Bounds);
        batch.End();
    }

    private GameState WriteCombinedPlayerSave(string path, Action<GameState>? validateContinuation, DateTime? savedAtUtc)
    {
        string temporary = path + ".ff8-" + Guid.NewGuid().ToString("N");
        try
        {
            GameState savedState = world!.WriteGameplaySave(temporary, validateContinuation, savedAtUtc, fieldAudio?.CapturePlaybackPositions());
            CombinedSessionSave combined = crossover! with { ActiveGame = SessionGame.Ff8, Ff8Payload = File.ReadAllBytes(temporary), Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
            CombinedSessionCodec.Save(path, combined);
            crossover = combined;
            return savedState;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
