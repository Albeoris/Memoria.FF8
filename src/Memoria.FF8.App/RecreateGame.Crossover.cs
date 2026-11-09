using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private CombinedSessionSave? crossover;
    private Ff7Scene? ff7Scene;
    private bool InFf7 => crossover?.ActiveGame == SessionGame.Ff7;

    private void InitializeCrossover()
    {
        string? path = options.CrossoverStatePath ?? options.StatePath;
        if (path is not null)
        {
            RestoreCrossover(CrossoverSaveAdapter.ReadEnvelope(path));
        }
    }

    private string? Ff8WorldSwitchUnavailable()
    {
        if (string.IsNullOrWhiteSpace(options.Ff7GameDirectory))
        {
            return "Select a FINAL FANTASY VII installation in the launcher.";
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

    private byte[] CaptureFf8Payload()
    {
        GameplaySaveComponentDocument document = (world ?? throw new InvalidOperationException("No FFVIII game is active.")).CaptureGameplaySave();
        document = document with { AudioPresentation = fieldAudio?.CapturePlaybackPositions() ?? document.AudioPresentation };
        using MemoryStream destination = new();
        GameplaySaveCodec.Write(destination, document);
        return destination.ToArray();
    }

    private Ff7Scene PrepareFf7Scene(CombinedSessionSave state)
    {
        Ff7Installation installation = Ff7Installation.Discover(options.Ff7GameDirectory ?? throw new InvalidOperationException("Select a FINAL FANTASY VII installation before loading this world."));
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        string tools = movieClock.ToolsDirectory;
        Ff7Scene scene = new(GraphicsDevice, installation, tools, new Ff7ProjectionDepth(native.FieldNearPlane, native.FieldFarPlane), movie => Ff7Scene.ResolveMovie(installation, tools, movie), state, audioEnabled: options.AudioEnabled);
        try
        {
            scene.Continue(state);
            scene.SaveRequested += SaveCrossoverFromFf7;
            scene.WorldSwitchRequested += SwitchToFf8;
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
        WorldSwitch transaction = new(current);
        Ff7Scene? prepared = null;
        try
        {
            transaction.ToFf7(new(true), payload, CrossoverSaveAdapter.Ff8Codec, () =>
            {
                using Ff7Session firstVisit = Ff7Session.NewGame(Ff7Installation.Discover(options.Ff7GameDirectory!));
                return firstVisit.Capture();
            }, target => prepared = PrepareFf7Scene(current with { ActiveGame = SessionGame.Ff7, Ff8Payload = payload, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec, Ff7 = target }));
            ff7Scene?.Dispose();
            ff7Scene = prepared;
            crossover = transaction.State;
            gameMenus?.Close();
            fieldAudio?.SetFocusPaused(true);
            Window.Title = "Memoria - Final Fantasy VII";
        }
        catch
        {
            prepared?.Dispose();
            throw;
        }
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
            prepared = RestoreFf8Payload(outgoing with { Ff8Payload = payload, Ff8PayloadFormat = codec });
            ValidatePlayerState(prepared);
        });
        RestorePlayerState(prepared!);
        crossover = transaction.State with { Mappings = outgoing.Mappings };
        ff7Scene?.Dispose();
        ff7Scene = null;
        fieldAudio?.SetFocusPaused(false);
        Window.Title = "Memoria - Final Fantasy VIII";
    }

    private void RestoreCrossover(CombinedSessionSave? saved)
    {
        Ff7Scene? prepared = saved?.ActiveGame == SessionGame.Ff7 ? PrepareFf7Scene(saved) : null;
        ff7Scene?.Dispose();
        ff7Scene = prepared;
        crossover = saved;
        fieldAudio?.SetFocusPaused(InFf7);
    }

    private void SaveCrossoverFromFf7(CombinedSessionSave saved)
    {
        if (ff7Scene?.CanSave != true)
        {
            throw new InvalidOperationException("Saving is unavailable at the current FFVII boundary.");
        }
        CombinedSessionCodec.Save(options.CrossoverSavePath, saved);
        crossover = saved;
    }

    private bool UpdateCrossover(GameTime time)
    {
        if (!InFf7)
        {
            return false;
        }
        ff7Scene!.Update(time, physicalInput.ReadKeyboard());
        if (options.ExitAfterSeconds is double seconds && time.TotalGameTime.TotalSeconds >= seconds)
        {
            Exit();
        }
        presentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
        return true;
    }

    private bool DrawCrossover(GameTime time)
    {
        if (!InFf7)
        {
            return false;
        }
        GraphicsDevice.Clear(Color.Black);
        ff7Scene!.Draw(batch, GraphicsDevice.Viewport.Bounds);
        CapturePresentedFrame(time);
        base.Draw(time);
        return true;
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
