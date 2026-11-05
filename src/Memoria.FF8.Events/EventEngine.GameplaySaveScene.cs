using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private GameplaySaveFieldScene CaptureGameplaySaveFieldScene()
    {
        if (Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.NativeFieldLoadVersion != 1 || Scene.ActorBackgroundScroll is not null || Scene.BackgroundScrollPositions.Count != 0 || Scene.BackgroundShades.Count != 0 || Scene.BackgroundShadeLoops.Count != 0 || Scene.SoundVolumeTransition is not null || Scene.AllSoundVolumeTransition is not null || Scene.MusicCrossfadeFrames != 0 || Scene.NativeFieldParticles?.Emitters.Any(value => value != 0) == true || Scene.NativeParticleSlots.Count != 0 || Scene.DrawPointInteraction is not null || Scene.NativeTutorial is not null || Scene.NativeMenuTutorial is not null || Scene.TripleTriadRequest is not null || Scene.WorldTransitionRequest is not null || Scene.WorldJumpWaits.Count != 0 || Scene.NativeDoorPendingEvents?.Values.Any(value => value != 0) == true || Scene.LoadedSoundEffects.Values.Any(sound => sound.Status == SoundResourceStatus.Pending))
        {
            throw new NotSupportedException("Gameplay field continuation does not yet represent the active scroll, shade, particle, modal, sound-I/O or door-event owner.");
        }
        GameplaySaveFieldTrailSample? Sample(PartyTraceSample? item) => item is null ? null : new(item.X, item.Y, item.Z, item.Triangle, item.Direction, item.Animation, item.FirstFlag, item.SecondFlag);
        GameplaySaveFieldScene result = new()
        {
            CameraIndex = Scene.CameraIndex,
            BackgroundClear = Scene.BackgroundClearValue,
            BackgroundStates = new(backgroundStates),
            BackgroundAnimations = backgroundAnimations.Select(pair => new GameplaySaveFieldBackgroundAnimation(pair.Key, pair.Value.First, pair.Value.Last, pair.Value.Speed, pair.Value.Complete, pair.Value.Counter, pair.Value.Position, pair.Value.Active, pair.Value.Loop, pair.Value.NativeFlag404)).ToArray(),
            LockedTriangles = Scene.LockedTriangles.ToArray(),
            ScrollLayers = Scene.BackgroundScrollLayers.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldScroll(pair.Value.OffsetX, pair.Value.OffsetY, pair.Value.ScaleX, pair.Value.ScaleY)),
            LayerZeroCurrent = Scene.NativeLayerZeroScrollCurrent is { } current ? new(current.X, current.Y) : null,
            ScrollCurrents = Scene.NativeBackgroundScrollCurrents?.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldScrollCurrent(pair.Value.X, pair.Value.Y)),
            PreviousField = Scene.PreviousFieldWord84,
            PreviousWorldEntity = Scene.PreviousWorldEntityByte86,
            FieldLoadReason = Scene.FieldLoadReasonByte87,
            PartyBindings = Scene.FieldPartyNativeActorIds.ToArray(),
            Trail = Scene.PartyTrace is { } trail ? new(trail.Cursor, trail.FirstOffset, trail.SecondOffset, trail.FirstTrail.Select(Sample).ToArray(), trail.SecondTrail.Select(Sample).ToArray()) : null,
            PlayerPosition = Scene.NativePlayerPosition is { } player ? new(player.X, player.Y, player.Z) : null,
            PartyTraceVersion = Scene.NativePartyTraceVersion,
            PlayerMovementVersion = Scene.NativePlayerMovementVersion,
            PlayerEarlyRetry = Scene.NativePlayerEarlyRetryState,
            FieldLoadVersion = Scene.NativeFieldLoadVersion,
            DoorDriverVersion = Scene.NativeDoorDriverVersion,
            DoorInitializationFrame = Scene.NativeDoorInitializationFrame,
            DoorPendingEvents = Scene.NativeDoorPendingEvents?.ToDictionary(),
            DrawPointVersion = Scene.DrawPointFieldStateVersion,
            DrawPointEnabled = Scene.DrawPointEnabled,
            DrawPointDisplayParameter = Scene.DrawPointDisplayParameter,
            DrawPointDisplayMask = Scene.NativeDrawPointDisplayMask,
            DrawPointIndex = Scene.DrawPointIndex,
            ParticleRandomPhase = Scene.NativeFieldParticleRandomPhase,
            SoundStopMask = Scene.NativeFieldSoundStopMask,
            DialogueFocus = Scene.NativeDialogueFocus,
            AskPreviousFocus = Scene.NativeAskPreviousFocus,
            AaskPreviousFocus = Scene.NativeAaskPreviousFocus,
            Music = Scene.MusicProcess is { } music ? new(music.Pending, music.Requested213, music.Complete214, music.Bank201, music.Bank202, music.SelectionAuthority, music.Buffer0, music.Buffer1) : null,
            LoadedSounds = Scene.LoadedSoundEffects.Values.Select(sound => new GameplaySaveFieldSound(sound.Index, sound.NativeResourceName)).ToArray(),
            LadderSpeed = Scene.NativeLadderSpeed490E,
            PreparedField = Scene.PreparedField,
            BattleResult = Scene.NativeBattleResult,
            MessageStyles = Scene.MessageChannelStyles.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldMessageStyle(pair.Value.Flags, pair.Value.Mode)),
            MessageTiming = Scene.MessageChannelTiming.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldMessageTiming(pair.Value.Speed16, pair.Value.Counter20)),
            MessageWindows = Scene.DialogueWindows.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldMessageWindow(pair.Value.Channel, pair.Value.X, pair.Value.Y, pair.Value.Width, pair.Value.Height))
        };
        result.Validate();
        return result;
    }

    private void RestoreGameplaySaveFieldScene(GameplaySaveFieldScene saved, GameplaySaveFieldScripts scripts, GameplaySaveSceneFacts facts)
    {
        saved.Validate();
        NativeFieldRuntimeProfile profile = NativeRuntimeProfile ?? throw new NotSupportedException("Gameplay continuation requires current verified field resources.");
        if (!profile.IsVerified || services is not GameSession session || session.State.FieldLoadContext?.Origin != NativeFieldLoadOrigin.GameplayContinuation || defaultsStarted || Frame != 0)
        {
            throw new InvalidOperationException("Gameplay continuation requires a fresh explicit continuation allocation before script initialization.");
        }
        // Scene values are restored without recreating old opcode receipts or executing INIT/default scripts.
        NativeFieldCallbackClock clock = new(1, scripts.Clock.Ordinary, scripts.Clock.Movie, scripts.Clock.Menu, scripts.Clock.Loader, scripts.Clock.Battle, scripts.Clock.Initialization, scripts.Clock.RandomBattle, scripts.Clock.MenuBattle);
        clock.Validate(scripts.Frame);
        long? logicalAllocationClock = session.ExamTimer is { } timer ? checked(timer.ClockCalls - clock.TimerCalls) : null;
        if (logicalAllocationClock < 0)
        {
            throw new InvalidDataException("Gameplay continuation callback timebase exceeds the saved timer owner.");
        }
        Scene.NativeFieldInitialization = new(1, profile.ExecutableHash, FieldId, programHash, profile.FieldClock, NativeFieldInitializationStage.Completed, logicalAllocationClock) { Origin = NativeFieldInitializationOrigin.GameplayContinuation };
        Scene.FieldCallbackClock = clock;
        Scene.CameraIndex = saved.CameraIndex;
        Scene.BackgroundClearValue = saved.BackgroundClear;
        backgroundStates.Clear();
        foreach ((int key, int value) in saved.BackgroundStates)
        {
            backgroundStates.Add(key, value);
        }
        backgroundAnimations.Clear();
        foreach (GameplaySaveFieldBackgroundAnimation animation in saved.BackgroundAnimations)
        {
            backgroundAnimations.Add(animation.Id, new BackgroundAnimation { NativeVersion = 1, First = animation.First, Last = animation.Last, Speed = animation.Speed, Complete = animation.Complete, Counter = animation.Counter, Position = animation.Position, Active = animation.Active, Loop = animation.Loop, NativeFlag404 = animation.Flag404 });
        }
        Scene.LockedTriangles.Clear();
        Scene.LockedTriangles.UnionWith(saved.LockedTriangles);
        Scene.BackgroundScrollLayers.Clear();
        foreach ((int layer, GameplaySaveFieldScroll scroll) in saved.ScrollLayers)
        {
            Scene.BackgroundScrollLayers.Add(layer, new(scroll.OffsetX, scroll.OffsetY, scroll.ScaleX, scroll.ScaleY));
        }
        Scene.BackgroundScrollPositions.Clear();
        Scene.NativeLayerZeroScrollCurrent = saved.LayerZeroCurrent is { } current ? new(1, current.X, current.Y) : null;
        Scene.NativeBackgroundScrollCurrents = saved.ScrollCurrents?.ToDictionary(pair => pair.Key, pair => new NativeBackgroundScrollCurrent(1, pair.Value.X, pair.Value.Y));
        Scene.NormalBackgroundScrollCenter = null;
        Scene.PreviousFieldWord84 = saved.PreviousField;
        Scene.PreviousWorldEntityByte86 = saved.PreviousWorldEntity;
        Scene.FieldLoadReasonByte87 = saved.FieldLoadReason;
        Scene.FieldPartyNativeActorIds.Clear();
        Scene.FieldPartyNativeActorIds.AddRange(saved.PartyBindings);
        RecordPartyBindingMemory();
        PartyTraceSample? Sample(GameplaySaveFieldTrailSample? item) => item is null ? null : new(item.X, item.Y, item.Z, item.Triangle, item.Direction, item.Animation, item.FirstFlag, item.SecondFlag);
        Scene.PartyTrace = saved.Trail is { } trail ? new(trail.Cursor, trail.FirstOffset, trail.SecondOffset, trail.First.Select(Sample).ToArray(), trail.Second.Select(Sample).ToArray()) : null;
        Scene.NativePlayerPosition = saved.PlayerPosition is { } player ? new(player.X, player.Y, player.Z) : null;
        Scene.NativePartyTraceVersion = saved.PartyTraceVersion;
        Scene.NativePlayerMovementVersion = saved.PlayerMovementVersion;
        Scene.NativePlayerEarlyRetryState = saved.PlayerEarlyRetry;
        Scene.NativeFieldLoadVersion = saved.FieldLoadVersion;
        Scene.NativeFieldEntryStack = null;
        Scene.FieldLoadFirstRecordZero = null;
        Scene.FieldLoadSecondRecordZero = null;
        Scene.NativeDoorDriverVersion = saved.DoorDriverVersion;
        Scene.NativeDoorInitializationFrame = saved.DoorInitializationFrame;
        Scene.NativeDoorPendingEvents = saved.DoorPendingEvents?.ToDictionary();
        Scene.DrawPointFieldStateVersion = saved.DrawPointVersion;
        Scene.DrawPointEnabled = saved.DrawPointEnabled;
        Scene.DrawPointDisplayParameter = saved.DrawPointDisplayParameter;
        Scene.NativeDrawPointDisplayMask = saved.DrawPointDisplayMask;
        Scene.DrawPointIndex = saved.DrawPointIndex;
        Scene.NativeFieldParticleRandomPhase = saved.ParticleRandomPhase;
        if (Scene.NativeFieldParticles is { } particles && saved.ParticleRandomPhase is byte phase)
        {
            Scene.NativeFieldParticles = particles with { RandomPhase = phase };
            Scene.NativeFieldParticleSprites = [];
        }
        Scene.NativeFieldSoundStopMask = saved.SoundStopMask;
        Scene.NativeLadderSpeed490E = saved.LadderSpeed;
        Scene.PreparedField = saved.PreparedField;
        Scene.NativeBattleResult = saved.BattleResult;
        Scene.NativeDialogueFocus = saved.DialogueFocus;
        Scene.NativeAskPreviousFocus = saved.AskPreviousFocus;
        Scene.NativeAaskPreviousFocus = saved.AaskPreviousFocus;
        Scene.MusicProcess = saved.Music is { } music ? new(1, music.Pending, music.Requested, music.Complete, music.Bank201, music.Bank202, new(1, NativeMusicBootstrap.GameplaySaveRule, profile.ExecutableHash)) { SelectionAuthority = music.Authority, Buffer0 = music.Buffer0, Buffer1 = music.Buffer1 } : null;
        Scene.LoadedSoundEffects.Clear();
        foreach (GameplaySaveFieldSound sound in saved.LoadedSounds)
        {
            SoundResourceStatus status = LoadSoundResource?.Invoke(sound.Index) ?? SoundResourceStatus.Muted;
            if (status == SoundResourceStatus.Pending)
            {
                throw new NotSupportedException("Gameplay continuation cannot publish an unresolved sound resource.");
            }
            Scene.LoadedSoundEffects.Add(sound.Index, new(sound.Index, sound.NativeResourceName, status));
        }
        Scene.MessageChannelStyles.Clear();
        foreach ((int channel, GameplaySaveFieldMessageStyle style) in saved.MessageStyles)
        {
            Scene.MessageChannelStyles.Add(channel, new(style.Flags, style.Mode));
        }
        Scene.MessageChannelTiming.Clear();
        foreach ((int channel, GameplaySaveFieldMessageTiming timing) in saved.MessageTiming)
        {
            Scene.MessageChannelTiming.Add(channel, new(timing.Speed, timing.Counter));
        }
        Scene.DialogueWindows.Clear();
        foreach ((int channel, GameplaySaveFieldMessageWindow window) in saved.MessageWindows)
        {
            Scene.DialogueWindows.Add(channel, new(window.Channel, window.X, window.Y, window.Width, window.Height));
        }
        Scene.Party.Clear();
        Scene.Party.AddRange(facts.FieldParty.Select(identity => (int)identity));
        Scene.Names.Clear();
        foreach ((int subject, string name) in facts.Names)
        {
            Scene.Names.Add(subject, name);
        }
        Scene.UnlockedTutorials.Clear();
        Scene.UnlockedTutorials.UnionWith(facts.UnlockedTutorials);
        Scene.PartyPower = facts.PartyPower;
        Scene.MenuEnabled = facts.MenuEnabled;
        Scene.RunEnabled = facts.RunEnabled;
        Scene.MapJumpDisabled = facts.MapJumpDisabled;
        Scene.PlaceId = facts.PlaceId;
        Scene.NativeSalaryMovementUnits = facts.PreviousSalaryMovement;
        if (facts.Music is { } sceneMusic)
        {
            Scene.LoadedMusic = sceneMusic.LoadedMusic;
            Scene.MusicPlaying = sceneMusic.Playing;
            Scene.MusicVolume = sceneMusic.Volume;
            Scene.MusicCrossfadeFrames = 0;
            Scene.MusicChannel108 = sceneMusic.PrimaryChannel is { } first ? new(first.ChannelId, first.MusicId, first.ChoiceMask) : null;
            Scene.MusicChannel112 = sceneMusic.SecondaryChannel is { } second ? new(second.ChannelId, second.MusicId, second.ChoiceMask) : null;
            Scene.NativeBattleMusic203 = sceneMusic.BattleMusic;
            Scene.SoundMusicVolume197 = sceneMusic.MusicVolume;
            Scene.SoundMusicVolume198 = sceneMusic.SoundVolume;
        }
        Frame = scripts.Frame;
        pendingArrival = null;
    }
}
