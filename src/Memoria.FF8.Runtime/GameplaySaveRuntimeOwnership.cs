namespace Memoria.FF8.Runtime;

internal enum GameplayRuntimeRestoration
{
    Composite,
    SelectedResources,
    FreshProcessReset,
    DeferredProducer,
    SelectedEnvironment,
    LogicalContinuation,
    Derived,
    RejectedActiveOperation,
    CompatibilityGate
}

internal sealed record GameplayRuntimeMemberOwnership(GameplayRuntimeRestoration Restoration, string Source);

/// <summary>Restoration authority of excluded process owners. Composite members require their own partition; homogeneous child graphs inherit their parent's authority and have a separately frozen recursive schema.</summary>
internal static class GameplaySaveRuntimeOwnership
{
    internal static IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership> Roots { get; } = CreateRoots();
    internal static IReadOnlyDictionary<Type, IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership>> Members { get; } = CreateMembers();

    private static IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership> CreateRoots()
    {
        Dictionary<string, GameplayRuntimeMemberOwnership> roots = new(StringComparer.Ordinal);
        void Add(GameplayRuntimeRestoration restoration, string source, params string[] names)
        {
            foreach (string name in names)
            {
                roots.Add(name, new(restoration, source));
            }
        }
        Add(GameplayRuntimeRestoration.Composite, "GameplaySaveRuntimeOwnership.Members", nameof(GameState.NativeBattleMemory), nameof(GameState.FieldBars), nameof(GameState.FieldShake), nameof(GameState.GlobalSoundProcess), nameof(GameState.WorldProcess), nameof(GameState.SalaryPresentation));
        Add(GameplayRuntimeRestoration.FreshProcessReset, "GameSession.CreateColdProcessState", nameof(GameState.NativeFieldDebugRequested), nameof(GameState.MovieSeconds));
        Add(GameplayRuntimeRestoration.Derived, "EventEngine.RestoreGameplaySaveFieldScene selects the prepared movie index from its logical source", nameof(GameState.MovieIndex));
        Add(GameplayRuntimeRestoration.FreshProcessReset, "NativeVibrationProcessState.FromColdProcess; normal player load creates a new vibration process", nameof(GameState.VibrationProcess));
        Add(GameplayRuntimeRestoration.FreshProcessReset, "NativeFieldButtonInput.FromGameplayContinuation; physical input is sampled by the new host", nameof(GameState.FieldButtonInput));
        Add(GameplayRuntimeRestoration.LogicalContinuation, "GameplaySaveFieldScene.PreparedMovieSource; EventEngine.RestoreGameplaySaveFieldScene", nameof(GameState.MovieSource));
        Add(GameplayRuntimeRestoration.RejectedActiveOperation, "GameWorld.CaptureGameplaySave rejects movie scenes; normal load has no active decoder", nameof(GameState.MoviePlayback));
        Add(GameplayRuntimeRestoration.Derived, "GameWorld inspection builds DebugSceneView from current actors", nameof(GameState.DebugView));
        return roots.AsReadOnly();
    }

    private static IReadOnlyDictionary<Type, IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership>> CreateMembers()
    {
        Dictionary<Type, Dictionary<string, GameplayRuntimeMemberOwnership>> types = [];
        void Add<T>(GameplayRuntimeRestoration restoration, string source, params string[] names)
        {
            if (!types.TryGetValue(typeof(T), out Dictionary<string, GameplayRuntimeMemberOwnership>? members))
            {
                types.Add(typeof(T), members = new(StringComparer.Ordinal));
            }
            foreach (string name in names)
            {
                members.Add(name, new(restoration, source));
            }
        }
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.SelectedResources, "NativeBattleRetainedMemoryReader.ReadColdProcess; NativeExecutionStackSelection.Apply", nameof(NativeBattleRetainedMemory.EffectMemory));
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.SelectedResources, "NativeBattleRetainedMemoryReader.ReadColdProcess", "ActionMemory", "ActionPackets", "ActionScratch", "AllocationControl", "ColdExecutableSha256", "EffectIoResult", "ElementAttack", "FieldTexturePlaneCacheFresh", "FileStaging", "ModelColors40", "ModelCpuResources", "ModelSequenceLocals", "ModelStatusTimers", "ModelTransformRows", "ModelWorldMatrices64", "PartyStatusMirrors", "PresentationStatusMemory");
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.FreshProcessReset, "NativeBattleRetainedMemory.FromColdProcess; selected model/stage/file/camera loaders bind current resources on demand", "Camera", "FileIo", "ModelTextureCache", "Stage");
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.DeferredProducer, "NativeFieldTextureAnimations field allocation; NativeBattleModelLoader current model load", "FieldTextureAnimations", "ModelLoader");
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.DeferredProducer, "BattleSession.RetainNativeSceneReplaySource; NativeBattleRetainedMemory.Capture retains completed scene sources between battles, while a normal gameplay load starts without prior framebuffer copies", nameof(NativeBattleRetainedMemory.SceneReplaySources));
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.Derived, "NativeBattleStorageGraphProjection.Retained reconstructs canonical storage aliases", "StorageGraph");
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.FreshProcessReset, "GameSession.CreateColdProcessState; current field music/vibration publication supplies canonical owners", "MusicProcess", "VibrationProcess");
        Add<NativeFieldBarsState>(GameplayRuntimeRestoration.LogicalContinuation, "GameplaySaveFieldPresentation.Capture/RestoreBars", "First", "Second");
        Add<NativeFieldBarsState>(GameplayRuntimeRestoration.FreshProcessReset, "NativeFieldBarsState.CreateHidden; AdjacentMirrorWord is a physical neighboring alias, not bar semantics", "AdjacentMirrorWord", "Version");
        Add<NativeFieldShake>(GameplayRuntimeRestoration.LogicalContinuation, "GameplaySaveFieldPresentation.Capture/RestoreShake; NativeShakeTables reads selected lookup bytes", "X", "Y");
        Add<NativeFieldShake>(GameplayRuntimeRestoration.SelectedResources, "NativeFieldShake.FromColdProcess", "ExecutableHash", "Version");
        Add<NativeGlobalSoundProcess>(GameplayRuntimeRestoration.Composite, "GameplaySaveRuntimeOwnership.Members", "MusicPlayback");
        Add<NativeGlobalSoundProcess>(GameplayRuntimeRestoration.FreshProcessReset, "NativeGlobalSoundProcess.FromBufferlessColdProcess; device voice allocations are replayed from logical field ambience", "Backend", "OriginalEffectIds", "Rotation", "Rule", "Spu", "Version");
        Add<NativeMusicPlaybackState>(GameplayRuntimeRestoration.FreshProcessReset, "NativeMusicPlaybackState.FromColdProcess for new loads; GameplaySaveSessionLoader preserves musicProcessHistory for same-process handoff", "BattleFirstLoadB8B7D4");
        Add<NativeMusicPlaybackState>(GameplayRuntimeRestoration.LogicalContinuation, "GameplaySaveMusicPlayback.Capture/Restore", "ConcertMask", "CurrentMusicId0", "CurrentMusicId1", "Gain0", "Gain1", "Mode0", "Mode1");
        Add<NativeSalaryPresentation>(GameplayRuntimeRestoration.SelectedResources, "NativeSalaryPresentation.FromColdProcess; current schema identity", "Version", "Rule");
        Add<NativeSalaryPresentation>(GameplayRuntimeRestoration.LogicalContinuation, "GameplaySaveSalaryPresentation.Capture/Restore; paid gil and salary countdown belong to Economy/SeeD", "Phase", "RankBlend98", "Scale9A", "OldRank9C", "NewRank9D", "Delay9E", "LeadingBlank9F", "OldRankGlyphs", "NewRankGlyphs", "OldPayGlyphs", "NewPayGlyphs");
        Add<NativeWorldProcess>(GameplayRuntimeRestoration.LogicalContinuation, "GameplaySaveWorldUi.Capture; WorldMapScene.RestoreGameplayWorldUi", "FuelMessageMovementCallbacks2045C78");
        Add<NativeWorldProcess>(GameplayRuntimeRestoration.FreshProcessReset, "NativeWorldProcess.FromColdProcess; GameplaySaveWorldResume.Complete allocates a new normal-load world", "CameraCooldown", "CarCameraFollowSpeed", "CompanionWaterStepEdges", "ForcedAnimationC76628", "GroundSelection", "InputHistory", "MainLoopCallbacks", "MainLoopCounter", "MountWaterStepEdges", "PreviousContactC76784", "Rule", "TextureAnimation", "VehicleManualCameraVelocity", "Version", "VerticalBias204DADC", "WaterStepEdges2045C7C");
        return types.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership>)pair.Value.AsReadOnly()).AsReadOnly();
    }
}
