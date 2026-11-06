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
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.Composite, "GameplaySaveRuntimeOwnership.Members", "EffectMemory");
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.SelectedResources, "NativeBattleRetainedMemoryReader.ReadColdProcess", "ActionMemory", "ActionPackets", "ActionScratch", "AllocationControl", "ColdExecutableSha256", "EffectIoResult", "ElementAttack", "FieldTexturePlaneCacheFresh", "FileStaging", "ModelColors40", "ModelCpuResources", "ModelSequenceLocals", "ModelStatusTimers", "ModelTransformRows", "ModelWorldMatrices64", "PartyStatusMirrors", "PresentationStatusMemory");
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.FreshProcessReset, "NativeBattleRetainedMemory.FromColdProcess; selected model/stage/file/camera loaders bind current resources on demand", "Camera", "FileIo", "ModelTextureCache", "Stage");
        Add<NativeBattleRetainedMemory>(GameplayRuntimeRestoration.DeferredProducer, "NativeFieldTextureAnimations field allocation; NativeBattleModelLoader current model load", "FieldTextureAnimations", "ModelLoader");
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
        Add<NativeWorldProcess>(GameplayRuntimeRestoration.FreshProcessReset, "NativeWorldProcess.FromColdProcess; GameplaySaveWorldResume.Complete allocates a new normal-load world", "CameraCooldown", "CarCameraFollowSpeed", "ForcedAnimationC76628", "GroundSelection", "InputHistory", "MainLoopCallbacks", "MainLoopCounter", "PreviousContactC76784", "Rule", "TextureAnimation", "VehicleManualCameraVelocity", "Version", "VerticalBias204DADC", "WaterStepEdges2045C7C");
        // Effect member lists are explicit. A newly added field must be assigned an authority before the recursive inventory can pass.
        Add<NativeBattleEffectMemory>(GameplayRuntimeRestoration.SelectedResources, "NativeBattleRetainedMemoryReader.ReadColdProcess",
            "Absorb56Bank", "ActionPhaseDEA", "Active", "ActorDirector16", "ActorDirector16Executable", "ActorDirector61", "ActorDirector61Executable", "ActorDirector81", "ActorDirector81Executable", "ActorDirector82", "ActorDirector82Executable", "Aqua55", "Aqua72", "Bio22FourthGeometry", "Bio22Geometry", "Bio22Palette", "Bio22SecondaryGeometry", "Bio22ThirdGeometry", "CuraContextF4", "CuraSceneFade", "CuragaExecutable", "CureContextF4", "Devour62", "DoubleMorph", "DrawStock15Executable", "DrinkMagic37", "DrinkMagic37Executable", "Effect20", "Effect57Executable", "Effect60", "Effect60PrimaryExecutable", "Effect60SecondaryExecutable", "EsunaExecutable", "FullLifeExecutable", "GeometryRegisters", "GuardianCasterBank", "MeltingBubble43", "MeltingBubble43Executable", "MightyGuard73", "MightyGuard78", "ModelColorOverrides", "ModuleSpriteBillboard21DFED0", "Parameter80", "Parameter82", "Potion50ContextF4", "Potion51ContextF4", "Potion51SceneFade", "Potion52Executable", "PrimitiveProcess", "PsychoBlast23", "PsychoBlast23Executable", "QuezacotlCompletion17A8", "QuezacotlExecutableMemory", "QuezacotlPause", "QuezacotlTargetY1774", "Raldo19", "Raldo19Executable", "RecoveryExecutables", "RemedyExecutable", "ReturnedTaskPointer1D96AAC", "Scan", "SelectedIndex", "SequenceCosine", "SequenceParticlePreviousBone", "SequenceSine", "SharedEffectRandom", "Shiva", "SoundPendingFlags", "SoundPublishedFlags", "TripleMorph", "WindBlast29", "WindBlast29Executable");
        Add<NativeBattleEffectMemory>(GameplayRuntimeRestoration.FreshProcessReset, "NativeBattleEffectMemory.FromColdProcess",
            "Acid170", "Acid317", "ActionGeometryGlobals", "Additional181", "Additional182", "AllocationCount", "Allocations", "AngelWing84", "Arena", "BadBreath131", "BadBreath300", "BeamLaser134", "Bio22ActorMemory", "BladeShot12", "Blaster186", "BombGlow179", "BoneMatrixAnchor2795BC0", "BoomerangSword304", "Breath129", "Breath157", "Cactuar199", "CameraAliases", "CannonBlow151", "Carbuncle278", "ChainGun190", "Choco100", "Choco97", "Choco98", "Choco99", "Confuse36ExecutableInitialized", "Control110", "Curse47ColdSourcesPending", "Cursor", "DarkMist13", "Death14", "Degenerator306", "Diablos325", "DisolvingAcid132", "Doom8ColdSourcesPending", "Doomtrain191", "Drain39Bank", "Dribble41", "Duel112", "Duel212", "Duel215", "Duel216", "Duel274", "EerieSoundWave130", "Effect101", "Effect140", "Effect166", "Effect167", "Effect173", "Effect174", "Effect183", "Effect187", "Effect207", "Effect209", "Effect210", "Effect213", "Effect218", "Effect227", "Effect251", "Effect283", "Effect285", "Effect287", "Effect289", "Effect290", "Effect298", "Effect30", "Effect301", "Effect303", "Effect308", "Effect31", "Effect312", "Effect313", "Effect314", "Effect315", "Effect316", "Effect318", "Effect319", "Effect320", "Effect321", "Effect322", "Effect323", "Effect324", "Effect326", "Effect338", "Effect344", "Effect345", "Effect54", "Effect63", "Effect64", "Effect65", "Effect66", "Effect68", "Effect69", "Effect70", "Effect76", "Effect79", "Effect80", "Effect83", "Effect9", "EffectIo", "ElectricDischarge126", "Electrocute275", "EnemyMicroMissiles284", "EnergyBomber217", "EvilEye282", "FatedCircle162", "FullCure280", "FullscreenOverlay", "FullscreenOverlayPause2795914", "FunguarLaser277", "GastricJuice128", "GastricJuice169", "GatlingGun150", "GatlingGun305", "Gilgamesh", "Gravija214", "GroundCapture", "GuardianCameraProcess", "GuardianWorkBuffer", "Holy175", "Hypnotize133", "Junk44", "Kiros262Trail", "LaserEye71", "LevelDeath74", "MagmaBreath48", "MegaSpark279", "MeltEye158", "Meltdown148", "MicroMissiles299", "MiniPulseCannon286", "Moogle96", "Morph172", "NoMercy200", "NodeContextExtension", "NodeScratch2793E30", "OilBlast137", "OilShot136", "Pain34", "Pandemona291", "PcRenderRequest", "PetrifyStare127", "PoisonGas171", "Protect32", "Quake38", "QuezacotlSecondaryHeads", "RayBomb10", "RayBomb307", "Reflect106", "ReflectBeam135", "Regen147", "Renzokuken159", "Renzokuken160", "Resonance49", "Rinoa86", "Rinoa87", "Rinoa88", "Rinoa89", "Rush91MorphScratch", "Saliva138", "SandStorm155", "SandStorm176", "ScriptedColorRegisters", "ScriptedEffects", "ScriptedGraphics", "ScriptedLighting", "ScriptedTarget2796D40", "Shell33", "ShockwavePulsar260", "Shoot42", "Shot188", "Shot192", "Shot193", "Shot194", "Shot195", "Shot196", "Shot197", "Shot198", "Shotgun281", "Sigh46", "Siren95", "Sleep145", "SleepingGas168", "SnipeLaser302", "Sonic139", "Stare45", "StickyWeb153", "Storm11", "Suicide180", "SummonStream", "TenThousandNeedles178", "Textures", "TheEnd85", "ThousandNeedles177", "Thundaga105", "Thundaga105Capture", "Tonberry90", "Tornado146", "UiImmediate", "Ultima149", "UltraWaves154", "UltraWaves184", "Ultrasonic152", "Wall189", "WhiteWind297", "WildCannonBlow156");
        Add<NativeBattleEffectMemory>(GameplayRuntimeRestoration.SelectedEnvironment, "NativeExecutionStackSelection.Apply; GameSession.ReconstructCompletedBattleRuntime",
            "ExecutionStack", "HeroCrtStack");
        Add<NativeBattleEffectMemory>(GameplayRuntimeRestoration.DeferredProducer, "BattleSession.AdvanceNativeOriginalEffectTaskCore; no active battle at gameplay capture",
            "BlastingZone164", "BlizzagaCompanion", "BlizzardFaces", "CarbuncleExecutableMemory", "Cinematic", "CinematicBreakupPayload4", "Confuse36Executable", "Curse47", "DetachedReturnedTask", "Doom8", "Entry21DFEC0", "Entry21DFEC4", "Entry21DFEC8", "Hero309", "Hero310", "LionHeart165", "MagicTextureBufferAddress1D99A88", "QuezacotlCapturedMatrix1780", "Recover93", "RenzokukenSpecials", "RenzokukenVariants", "Reverse94", "RoughDivide163", "Rush91", "Search", "SequenceDeformationMatrix1D99BF8", "SharedCustomMatrix", "SharedEffectView2793E58", "UiImmediateFrame", "Water222");
        Add<NativeBattleEffectMemory>(GameplayRuntimeRestoration.CompatibilityGate, "NativeBattleEffectMemory legacy effect-light setters",
            "LegacyEffectLightBytes", "LegacyEffectLightKnown");
        return types.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership>)pair.Value.AsReadOnly()).AsReadOnly();
    }
}
