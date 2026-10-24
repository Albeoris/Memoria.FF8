using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleActorDirectorPresentationTests
{
    [Fact]
    public void Selected_original_actor_director_component_completes_controlled_contact_and_cold_boundaries()
    {
        RunSelected([16, 61, 81, 82], "MEMORIA_FF8_ACTOR_DIRECTOR");
    }

    internal static void RunSelected(ushort[] supportedEffects, string variablePrefix, Func<BattleAssets, ushort, NativeEffectFixtureRequest>? fixtureFactory = null, Func<GameAssets, BattleAssets, NativeEffectFixtureRequest, BattleSnapshot>? preparedSourceFactory = null)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selection = Environment.GetEnvironmentVariable(variablePrefix + "_EFFECT");
        string? output = Environment.GetEnvironmentVariable(variablePrefix + "_EVIDENCE");
        Assert.SkipUnless(directory is not null && selection is not null && output is not null, "Select installed original resources, one actor-director effect and a distinct evidence directory.");
        ushort effectId = ushort.Parse(selection!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(effectId, supportedEffects);
        Assert.True(Directory.Exists(directory));
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        if (effectId == 210)
        {
            NativeBattleEffect210PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 209)
        {
            NativeBattleEffect209PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 207)
        {
            NativeBattleEffect207PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 174)
        {
            NativeBattleEffect174PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 173)
        {
            NativeBattleEffect173PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 167)
        {
            NativeBattleEffect167PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 166)
        {
            NativeBattleEffect166PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 140)
        {
            NativeBattleEffect140PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 76)
        {
            NativeBattleEffect76PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 68)
        {
            NativeBattleEffect68PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 66)
        {
            NativeBattleEffect66PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 65)
        {
            NativeBattleEffect65PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 64)
        {
            NativeBattleEffect64PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 80)
        {
            NativeBattleEffect80PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 344)
        {
            NativeBattleEffect344PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 70)
        {
            NativeBattleEffect70PresentationTests.RunComponent(directory!, output!);
            return;
        }
        if (effectId == 69)
        {
            NativeBattleEffect69PresentationTests.RunComponent(directory!, output!);
            return;
        }
        BattleSession? battle = null;
        BattleSnapshot? prior = null;
        BattleScenePresentation? scene = null;
        int callbacks = 0;
        int contacts = 0;
        int completions = 0;
        int contactGlyphs = 0;
        int secondaryContactGlyphs = 0;
        bool stageChanged = false;
        int visibleChangedRgb = 0;
        bool initialized = false;
        bool reflectMainRestored = false;
        bool reflectTargetRestored = false;
        bool reflectHeadRestored = false;
        bool reflectTailRestored = false;
        bool reflectBonesRestored = false;
        bool reflectMorphRestored = false;
        HashSet<uint> reflectParticlesRestored = [];
        HashSet<int> reflectPacketBanks = [];
        NativeBattlePoseSource? reflectPoseSource = null;
        string? reflectBonePublication = null;
        bool dischargeWorldRestored = false;
        bool dischargeBoneRestored = false;
        bool dischargeActRestored = false;
        bool dischargeMorphRestored = false;
        bool dischargeFlashRestored = false;
        bool dischargeScreenFlashRestored = false;
        HashSet<int> dischargePacketBanks = [];
        bool breath131MeshesRestored = false;
        bool breath131DeformRestored = false;
        bool breath131TargetRestored = false;
        bool breath131ParticlesRestored = false;
        bool breath131ColorRestored = false;
        string? breath131ScratchW = null;
        HashSet<int> breath131PacketBanks = [];
        bool salivaSourcesRestored = false;
        bool salivaMainRestored = false;
        bool salivaSecondaryRestored = false;
        bool salivaImpactRestored = false;
        bool salivaCurveRestored = false;
        bool salivaRibbonRestored = false;
        bool salivaCustomRestored = false;
        bool salivaSimpleRestored = false;
        bool salivaMovingRestored = false;
        bool salivaMorphRestored = false;
        string? salivaScratchW = null;
        HashSet<int> salivaPacketBanks = [];
        bool oilOpeningRestored = false;
        HashSet<string> breath157Boundaries = [];
        NativeBattleEffect79PresentationObserver effect79 = new();
        NativeBattleEffect83PresentationObserver effect83 = new();
        NativeBattleEffect289PresentationObserver effect289 = new();
        NativeBattleEffect315PresentationObserver effect315 = new();
        NativeBattleEffect303PresentationObserver effect303 = new();
        NativeBattleEffect283PresentationObserver effect283 = new();
        HashSet<uint> effect298Callbacks = [];
        HashSet<int> effect298Banks = [];
        HashSet<uint> effect298Programmes = [];
        HashSet<string> effect298Markers = [];
        HashSet<string> effect298Domains = [];
        List<object> effect298Observations = [];
        bool effect298CountersChanged = false;
        bool effect298OffsetsVerified = false;
        bool effect298ChunksSeen = false;
        bool effect298SmokeSeen = false;
        bool effect298IoQueued = false;
        int? effect298VibrationHandle = null;
        HashSet<uint> megaSpark279Callbacks = [];
        HashSet<int> megaSpark279Banks = [];
        HashSet<string> megaSpark279PacketDomains = [];
        HashSet<int> megaSpark279BFrames = [];
        HashSet<int> megaSpark279AFrames = [];
        bool megaSpark279RowsSeen = false;
        bool megaSpark279AttachmentsSeen = false;
        bool megaSpark279ProgrammesSeen = false;
        bool megaSpark279SpritesSeen = false;
        HashSet<uint> boomerang304Callbacks = [];
        HashSet<int> boomerang304Banks = [];
        HashSet<string> boomerang304Markers = [];
        bool boomerang304ProgrammeSeen = false;
        bool boomerang304TrailSeen = false;
        bool boomerang304ParticlesSeen = false;
        bool boomerang304MorphSeen = false;
        HashSet<uint> snipe302Callbacks = [];
        HashSet<int> snipe302Banks = [];
        HashSet<uint> snipe302Programmes = [];
        HashSet<string> snipe302Markers = [];
        Dictionary<int, byte[]> snipe302InitializedA = [];
        bool snipe302ClonesSeen = false;
        bool snipe302ParticlesSeen = false;
        bool snipe302SpritesSeen = false;
        bool snipe302MorphSeen = false;
        bool snipe302BeamWindowSeen = false;
        HashSet<uint> enemyMicro284Callbacks = [];
        HashSet<int> enemyMicro284Banks = [];
        HashSet<uint> enemyMicro284Programmes = [];
        HashSet<ushort> enemyMicro284Variants = [];
        HashSet<string> enemyMicro284Markers = [];
        bool enemyMicro284SixBorn = false;
        bool enemyMicro284ParticlesSeen = false;
        bool enemyMicro284TrailsSeen = false;
        bool enemyMicro284ReactionSeen = false;
        bool enemyMicro284MorphSeen = false;
        HashSet<uint> miniPulse286Callbacks = [];
        HashSet<int> miniPulse286Banks = [];
        HashSet<uint> miniPulse286Programmes = [];
        HashSet<string> miniPulse286Markers = [];
        bool miniPulse286SpriteSeen = false;
        bool miniPulse286MorphSeen = false;
        bool miniPulse286WindowSeen = false;
        HashSet<uint> evilEye282Callbacks = [];
        HashSet<int> evilEye282Banks = [];
        HashSet<uint> evilEye282Programmes = [];
        HashSet<string> evilEye282Markers = [];
        bool evilEye282SpriteSeen = false;
        bool evilEye282MorphSeen = false;
        bool evilEye282FlashSeen = false;
        HashSet<uint> shotgun281Callbacks = [];
        HashSet<int> shotgun281Banks = [];
        HashSet<uint> shotgun281Sprites = [];
        HashSet<uint> shotgun281Programmes = [];
        HashSet<int> shotgun281Ratios = [];
        HashSet<uint> shotgun281Headers = [];
        HashSet<string> shotgun281Markers = [];
        bool shotgun281SzSeen = false;
        bool shotgun281MuzzleSeen = false;
        bool shotgun281MorphSeen = false;
        bool shotgun281RecipientSeen = false;
        bool shotgun281FlashSeen = false;
        List<object> shotgun281Observations = [];
        HashSet<uint> gravija214Callbacks = [];
        HashSet<int> gravija214Banks = [];
        HashSet<string> gravija214Checkpoints = [];
        bool gravija214EntryCopied = false;
        bool gravija214Preprocessed = false;
        bool gravija214WarpSeen = false;
        bool gravija214ProgrammeSzSeen = false;
        bool gravija214SpriteSeen = false;
        bool gravija214ReloadPending = false;
        List<object> gravija214Observations = [];
        HashSet<uint> junk44Callbacks = [];
        HashSet<int> junk44PacketBanks = [];
        HashSet<string> junk44Checkpoints = [];
        HashSet<byte> junk44Opcodes = [];
        bool junk44IoQueued = false;
        bool junk44IoCompleted = false;
        bool junk44RestoredPosition = false;
        HashSet<string> stare45Boundaries = [];
        HashSet<uint> stare45Callbacks = [];
        HashSet<int> stare45TimBanks = [];
        HashSet<int> stare45Bones = [];
        HashSet<uint> stare45ProgrammesCompleted = [];
        HashSet<string> stare45Protocol = [];
        HashSet<byte> stare45Opcodes = [];
        List<object> stare45Observations = [];
        NativeBattleVector? stare45TargetF1 = null;
        bool stare45ModePairSeen = false;
        bool stare45MorphSeen = false;
        HashSet<string> sigh46Boundaries = [];
        HashSet<uint> sigh46Callbacks = [];
        HashSet<int> sigh46PacketBanks = [];
        HashSet<int> sigh46MaterialRows = [];
        HashSet<int> sigh46ParticleSlots = [];
        HashSet<int> sigh46ParticleFrames = [];
        HashSet<uint> sigh46ParticleSprites = [];
        List<object> sigh46Observations = [];
        bool sigh46EmittersSeen = false;
        bool sigh46GeometrySeen = false;
        bool sigh46VramInitialized = false;
        bool sigh46VramWrapped = false;
        bool sigh46Gt4Seen = false;
        bool sigh46CommonPending = false;
        bool sigh46CommonCompleted = false;
        bool sigh46NodesDrainedBeforeReload = false;
        HashSet<string> magma48Boundaries = [];
        HashSet<uint> magma48Callbacks = [];
        HashSet<int> magma48PacketBanks = [];
        HashSet<uint> magma48ChangedXYZ = [];
        HashSet<uint> magma48ChangedUV = [];
        HashSet<byte> magma48Opcodes = [];
        List<object> magma48Observations = [];
        bool magma48EmittersSeen = false;
        bool magma48GeometrySeen = false;
        bool magma48ColorPairsSeen = false;
        bool magma48DepthPacketsSeen = false;
        HashSet<string> resonance49Boundaries = [];
        HashSet<uint> resonance49Callbacks = [];
        HashSet<int> resonance49ProgrammeActors = [];
        HashSet<int> resonance49CompletedProgrammes = [];
        HashSet<int> resonance49PacketBanks = [];
        HashSet<int> resonance49EnvironmentParities = [];
        HashSet<int> resonance49ProducerEnvironmentParities = [];
        HashSet<byte> resonance49ProducerBuffers = [];
        HashSet<byte> resonance49ScreenOpcodes = [];
        List<object> resonance49Observations = [];
        bool resonance49ScreenPairsSeen = false;
        bool resonance49ProgrammePacketsSeen = false;
        bool resonance49CommonPending = false;
        bool resonance49CommonCompleted = false;
        HashSet<string> blade12Boundaries = [];
        HashSet<uint> blade12Callbacks = [];
        HashSet<int> blade12PacketBanks = [];
        HashSet<int> blade12MorphAges = [];
        HashSet<int> blade12RandomAges = [];
        HashSet<uint> blade12SpriteSources = [];
        List<object> blade12Observations = [];
        int blade12NativeRandomDraws = 0;
        bool blade12Object7Hidden = false;
        bool blade12Object7Visible = false;
        HashSet<string> acid170Boundaries = [];
        HashSet<uint> acid170Callbacks = [];
        HashSet<int> acid170PacketBanks = [];
        HashSet<uint> acid170ParticleFlags = [];
        HashSet<int> acid170BodyAges = [];
        HashSet<int> acid170MeshAges = [];
        HashSet<byte> acid170E1Groups = [];
        List<object> acid170Observations = [];
        ushort? acid170OriginalFlags = null;
        bool acid170BodyRestored = false;
        int acid170BodyChangedRgb = 0;
        ImmutableHashSet<NativeBattlePrimitiveReference> acid170BodyPackets = ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        HashSet<string> morph172Boundaries = [];
        HashSet<uint> morph172Callbacks = [];
        HashSet<int> morph172PacketBanks = [];
        HashSet<int> morph172BodyAges = [];
        List<object> morph172Observations = [];
        bool morph172BodyRestored = false;
        HashSet<string> poison171Boundaries = [];
        HashSet<uint> poison171Callbacks = [];
        HashSet<int> poison171PacketBanks = [];
        HashSet<uint> poison171ParticleBanks = [];
        HashSet<int> poison171BodyAges = [];
        List<object> poison171Observations = [];
        ushort? poison171OriginalFlags = null;
        bool poison171BodyRestored = false;
        HashSet<string> additional181Boundaries = [];
        HashSet<uint> additional181Callbacks = [];
        HashSet<int> additional181PacketBanks = [];
        HashSet<uint> additional181ParticleFlags = [];
        HashSet<int> additional181BodyAges = [];
        List<object> additional181Observations = [];
        bool additional181FlashSeen = false;
        bool additional181FlashCompleted = false;
        HashSet<string> additional182Boundaries = [];
        HashSet<int> additional182PacketBanks = [];
        HashSet<uint> additional182Callbacks = [];
        HashSet<uint> additional182ParticleBanks = [];
        HashSet<byte> additional182F4Opcodes = [];
        List<object> additional182Observations = [];
        ushort? additional182OriginalFlags = null;
        bool additional182DpcsPublished = false;
        HashSet<string> blaster186Boundaries = [];
        HashSet<int> blaster186PacketBanks = [];
        HashSet<uint> blaster186Callbacks = [];
        HashSet<uint> blaster186VertexSelections = [];
        HashSet<byte> blaster186Opcodes = [];
        List<object> blaster186Observations = [];
        string? blaster186FramesW = null;
        byte[]? blaster186MatrixPadding = null;
        byte? blaster186WaiterHighByte = null;
        bool blaster186TargetDisplaced = false;
        HashSet<string> cannon151Boundaries = [];
        HashSet<int> cannon151PacketBanks = [];
        HashSet<uint> cannon151Callbacks = [];
        HashSet<uint> cannon151ParticleBanks = [];
        HashSet<byte> cannon151Opcodes = [];
        List<object> cannon151Observations = [];
        string? cannon151SamplesW = null;
        byte[]? cannon151MatrixPadding = null;
        byte? cannon151WaiterHighByte = null;
        HashSet<string> chain190Boundaries = [];
        HashSet<int> chain190PacketBanks = [];
        HashSet<uint> chain190Callbacks = [];
        HashSet<uint> chain190ParticleBanks = [];
        HashSet<byte> chain190Opcodes = [];
        HashSet<byte> chain190PaddingOpcodes = [];
        List<object> chain190Observations = [];
        bool chain190HasPositionedRecords = false;
        bool chain190HasBoneRecords = false;
        HashSet<string> gatling150Boundaries = [];
        HashSet<int> gatling150PacketBanks = [];
        HashSet<uint> gatling150Callbacks = [];
        HashSet<uint> gatling150ParticleBanks = [];
        HashSet<byte> gatling150Opcodes = [];
        HashSet<byte> gatling150PaddingOpcodes = [];
        List<object> gatling150Observations = [];
        HashSet<string> effect183Boundaries = [];
        HashSet<string> sonic139Boundaries = [];
        HashSet<int> sonic139PacketBanks = [];
        HashSet<byte> sonic139Opcodes = [];
        HashSet<string> breath129Boundaries = [];
        HashSet<int> breath129PacketBanks = [];
        HashSet<uint> breath129Callbacks = [];
        HashSet<byte> breath129Opcodes = [];
        List<object> breath129Observations = [];
        HashSet<int> effect183PacketBanks = [];
        HashSet<int> breath157PacketBanks = [];
        HashSet<uint> breath157Callbacks = [];
        HashSet<uint> breath157Tags = [];
        HashSet<byte> breath157Opcodes = [];
        HashSet<short> breath157TravelIndices = [];
        List<object> breath157PoseObservations = [];
        string? breath157Rotation = null;
        string? breath157PointW = null;
        HashSet<string> cannon156Boundaries = [];
        HashSet<int> cannon156PacketBanks = [];
        string? cannon156SamplesW = null;
        HashSet<uint> cannon156Callbacks = [];
        HashSet<uint> cannon156RecordBanks = [];
        HashSet<byte> cannon156Opcodes = [];
        HashSet<short> cannon156MainIndices = [];
        HashSet<string> sand155Boundaries = [];
        HashSet<int> sand155PacketBanks = [];
        string? sand155SelectedW = null;
        string? sand155SamplesW = null;
        List<object> sand155MeshObservations = [];
        HashSet<string> gastric128Boundaries = [];
        HashSet<int> gastric128PacketBanks = [];
        int gastric128CachedVertices = 0;
        bool gastric128LinkedBody = false;
        string? gastric128CacheBaseline = null;
        List<object> gastric128PointMasks = [];
        HashSet<string> acid132Boundaries = [];
        HashSet<int> acid132PacketBanks = [];
        List<object> acid132PointMasks = [];
        HashSet<string> oilShotBoundaries = [];
        HashSet<int> oilShotPacketBanks = [];
        bool oilMainRestored = false;
        bool oilImpactRestored = false;
        bool oilCustomRestored = false;
        bool oilSecondaryRestored = false;
        bool oilParticlesRestored = false;
        bool oilMorphRestored = false;
        int oilOrdinaryNodes = 0;
        int oilDefaultNodes = 0;
        HashSet<int> oilPacketBanks = [];
        bool meltEyeCurvesRestored = false;
        bool meltEyeSlotsARestored = false;
        bool meltEyeSlotsBRestored = false;
        bool meltEyeHeadRestored = false;
        bool meltEyeStreamRestored = false;
        bool meltEyeImpactRestored = false;
        bool meltEyeImpactBirthRestored = false;
        bool meltEyeImpactSpriteRestored = false;
        bool meltEyeForkRestored = false;
        bool meltEyeFeedbackRestored = false;
        bool energyBomberMainRestored = false;
        bool energyBomberMorphRestored = false;
        bool energyBomberTrailRestored = false;
        bool energyBomberImpactRestored = false;
        bool energyBomberSpritesRestored = false;
        bool effect101ActRestored = false;
        bool effect101MorphRestored = false;
        bool effect101ScaleRestored = false;
        bool effect101VisibleRestored = false;
        bool effect101FlagsRestored = false;
        bool petrifyActRestored = false;
        bool beamLaserPrimaryRestored = false;
        bool beamLaserBeamRestored = false;
        bool beamLaserEndcapRestored = false;
        bool beamLaserRingRestored = false;
        bool beamLaserStaticRestored = false;
        bool beamLaserMovingRestored = false;
        bool beamLaserMorphRestored = false;
        bool beamLaserFlagsRestored = false;
        bool beamLaserStationaryObserved = false;
        bool beamLaserChildObserved = false;
        NativeBattleEffectArena? beamLaserMorphBaseline = null;
        bool hypnotizeMainRestored = false;
        bool hypnotizeAnchoredRestored = false;
        bool hypnotizeFadingRestored = false;
        bool hypnotizeSpriteRestored = false;
        bool needleRecordsRestored = false;
        bool needleSparksRestored = false;
        bool laserBankRestored = false;
        bool laserBindingRestored = false;
        bool laserBeamRestored = false;
        bool laserParticlesRestored = false;
        bool eerieMainRestored = false;
        bool eerieTravelRestored = false;
        bool eerieImpactRestored = false;
        bool liveRestored = false;
        bool cameraChanged = false;
        bool actor6Activated = false;
        bool devourMuted = false;
        bool textureLoadPendingRestored = false;
        bool targetDisplaced = false;
        bool targetPositionRestored = false;
        bool targetRestored = false;
        int? targetPositionRestoreCallback = null;
        int? targetRotationRestoreCallback = null;
        bool secondaryBankRestored = false;
        bool windBlastBankRestored = false;
        bool absorbBankRestored = false;
        bool meltingResourcesRestored = false;
        bool doomReaperRestored = false;
        bool doomBankRestored = false;
        bool curseIoRestored = false;
        bool curseBankRestored = false;
        bool levelDeathAuraRestored = false;
        bool levelDeathDarkRestored = false;
        bool microMissilesCasterRestored = false;
        bool microMissilesMissileRestored = false;
        bool microMissilesImpactRestored = false;
        bool badBreathCasterRestored = false;
        bool badBreathDeformRestored = false;
        bool badBreathTargetRestored = false;
        string? badBreathScratchW = null;
        bool degeneratorCasterRestored = false;
        bool degeneratorBodyRestored = false;
        bool degeneratorEmitterRestored = false;
        bool pulsarCasterRestored = false;
        bool pulsarActRestored = false;
        bool pulsarPuffsRestored = false;
        bool pulsarSecondaryRestored = false;
        bool pulsarRisingRestored = false;
        bool pulsarBodyRestored = false;
        bool pulsarPositionRestored = false;
        bool pulsarIoRestored = false;
        NativeBattleEffectArena? pulsarSavedPosition = null;
        bool rayCasterRestored = false;
        bool rayCasterHiddenRestored = false;
        bool rayParticlesRestored = false;
        bool rayGt3Restored = false;
        bool rayTargetDisplaced = false;
        bool rayTargetRestored = false;
        bool ultraCasterRestored = false;
        bool ultraEmittersRestored = false;
        bool ultraWarpRestored = false;
        bool electroCasterRestored = false;
        bool electroWorldRestored = false;
        bool electroBoneRestored = false;
        bool electroActRestored = false;
        bool electroFlashRestored = false;
        bool electroScreenFlashRestored = false;
        bool acidCasterRestored = false;
        bool acidTrailRestored = false;
        bool acidBodyRestored = false;
        bool acidActRestored = false;
        bool gatlingCasterRestored = false;
        bool gatlingSlotARestored = false;
        bool gatlingSlotBRestored = false;
        int meltingAnchorChecks = 0;
        NativeBattlePoseSource? meltingPoseSource = null;
        List<object> meltingAnchorObservations = [];
        int? effect20VibrationHandle = null;
        BattleSnapshot? peakSnapshot = null;
        BattleSnapshot? peakWithout = null;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        string? peakOwnership = null;
        List<string> restoredBoundaries = [];
        string boundary = "original-resources";
        try
        {
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            BattleNativeAction? originalAction = assets.NativeActions.Where(row => row.MagicId == effectId).OrderBy(row => row.Command).ThenBy(row => row.ActionId).ThenBy(row => row.CrisisLevel).FirstOrDefault();
            if (effectId is 81 or 82)
            {
                ushort kernelIndex = effectId == 81 ? (ushort)335 : (ushort)337;
                originalAction = Assert.Single(assets.NativeActions, row => row.MagicId == effectId && row.Command == 8 && row.KernelSection == 3 && row.KernelIndex == kernelIndex);
            }
            BattleNativeAction action = originalAction ?? Assert.Single(assets.NativeActions, row => row.Command == 2 && row.ActionId == 1);
            BattleEncounter formation = assets.LoadEncounter(94);
            BattleEnemyPlacement enemy = NativeBattleEffectFixtureExporter.SelectEnemy(formation);
            NativeBattleEffectBinding original = game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1];
            string fixture = Path.Combine(output!, "fixture");
            boundary = "export-controlled-component";
            byte caster = 0;
            byte contactActor = 3;
            NativeEffectFixtureRequest request = new($"actor-director{effectId}", effectId, action.Command, action.ActionId, contactActor, 800, 240) { EncounterId = formation.Id, CasterActorIndex = caster, PartyModelId = 0, CrisisLevel = action.CrisisLevel, ControlledShellEffect = originalAction is null ? action.MagicId : null };
            if (fixtureFactory is not null)
            {
                request = fixtureFactory(assets, effectId);
                Assert.Equal(effectId, request.Effect);
                action = NativeBattleEffectFixtureExporter.SelectAction(assets.NativeActions, request);
                Assert.Equal(effectId, action.MagicId);
                Assert.Null(request.ControlledShellEffect);
                originalAction = action;
                formation = assets.LoadEncounter(request.EncounterId);
                enemy = NativeBattleEffectFixtureExporter.SelectEnemy(formation, request.PreserveOriginalFormationEnemies);
                caster = request.CasterActorIndex;
                contactActor = request.TargetActorIndex;
            }
            BattleSnapshot source;
            NativeEffectFixtureExportResult exported;
            if (preparedSourceFactory is not null)
            {
                source = preparedSourceFactory(game, assets, request);
                exported = new(request.Id, effectId, "fixture-only", "external-standalone-controlled-packet", null, [], new(true, action.Command, action.ActionId, action.MagicId, action.KernelSection, action.KernelIndex, effectId)) { Scenario = effectId == 74 ? "Original Level Death action with explicitly declared selected post-formula contact; no ordinary formula or command admission" : "Explicit standalone continuation; controlled seven-HP post-formula contact; no ordinary command or formula admission" };
                NativeBattleRestorationPresentationTests.Publish(output!, "prepared-source", source, null);
            }
            else
            {
                exported = NativeBattleEffectFixtureExporter.Export(request, directory!, fixture);
                File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported));
                Assert.True(exported.Status != "blocked", exported.FirstBlocker);
                source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
            }
            File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            if (effectId is 44 or 282 or 286)
            {
                caster = source.NativeActionPackets!.Headers[0].ReadByte(0);
                Assert.InRange(caster, (byte)3, (byte)6);
            }
            prior = source;
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(source.Definition.NativeEffectCatalog[effectId - 1]));
            int descriptor = Assert.Single(source.NativeGraphics!.Descriptors.Queue, index => source.NativeGraphics.Descriptors.Packets[index].Command == 104);
            uint headerOffset = checked(source.NativeGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u);
            Assert.Equal(0u, headerOffset % 20);
            NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[checked((int)(headerOffset / 20))];
            Assert.Equal(caster, header.ReadByte(0));
            Assert.Equal(action.Command, header.ReadByte(1));
            Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
            Assert.Equal(effectId, NativeBattleActionGraphics.Word(header, 6));
            if (effectId == 79)
            {
                Assert.Equal((byte)4, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)15, header.ReadByte(2));
                Assert.Equal(new NativeBattleEffectInitializerResource("mag078.tim", NativeBattleEffect79Profile.InitializerGlobal), original.InitializerResource);
                Assert.Equal(NativeBattleEffect79Profile.TextureLength, original.InitializerAsset!.Data.Length);
                Assert.Same(game.ReadNativeEffect79Resources(), original.Effect79Resources);
                Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect79State.FromColdProcess()), JsonSerializer.Serialize(source.NativeEffectMemory!.Effect79));
            }
            if (effectId == 83)
            {
                Assert.Equal((byte)4, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)16, header.ReadByte(2));
                Assert.Equal(new NativeBattleEffectInitializerResource("mag082.tim", NativeBattleEffect83Profile.InitializerGlobal), original.InitializerResource);
                Assert.Equal(NativeBattleEffect83Profile.TextureLength, original.InitializerAsset!.Data.Length);
                Assert.Same(game.ReadNativeEffect83Resources(), original.Effect83Resources);
                Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect83State.FromColdProcess()), JsonSerializer.Serialize(source.NativeEffectMemory!.Effect83));
            }
            if (effectId == 289)
            {
                Assert.Equal((byte)4, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal(new NativeBattleEffectInitializerResource("mag288.tim", NativeBattleEffect289Profile.InitializerGlobal), original.InitializerResource);
                Assert.Equal(NativeBattleEffect289Profile.TextureLength, original.InitializerAsset!.Data.Length);
                Assert.Same(game.ReadNativeEffect289Resources(), original.Effect289Resources);
                Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect289State.FromColdProcess()), JsonSerializer.Serialize(source.NativeEffectMemory!.Effect289));
            }
            if (effectId == 315)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal((byte)3, source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value).Primary.ActorIndex);
                Assert.Equal(new NativeBattleEffectInitializerResource("mag314.tim", NativeBattleEffect315Profile.InitializerGlobal), original.InitializerResource);
                Assert.Equal(NativeBattleEffect315Profile.TextureLength, original.InitializerAsset!.Data.Length);
                Assert.Same(game.ReadNativeEffect315Resources(), original.Effect315Resources);
                Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect315State.FromColdProcess()), JsonSerializer.Serialize(source.NativeEffectMemory!.Effect315));
                List<object> loaded = [];
                foreach ((byte index, int modelId, byte nativeId) in new[] { ((byte)3, 75, (byte)91), ((byte)4, 34, (byte)50) })
                {
                    NativeBattleGraphicsModel model = source.NativeGraphics.Models[index];
                    BattleCombatantDefinition definition = source.Definition.Combatants.Single(actor => actor.Id == source.NativeGraphics.ActorIds[index]);
                    Assert.Equal(modelId, definition.ModelId);
                    Assert.Equal(index - 3, definition.SceneSlot);
                    Assert.Null(definition.NativeAi);
                    Assert.Equal(nativeId, model.ModelId4);
                    Assert.NotNull(model.ActiveResourceRow100);
                    Assert.Null(model.ActiveStagedResourceRow100);
                    Assert.NotNull(model.GeometryWorldMatrix);
                    NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
                    NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Effect315 requires both actual loaded phase-pair pose resources.");
                    ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
                    Assert.Equal(pose.BoneCount, bones.Length);
                    Assert.NotEmpty(resource.Geometry!.Value);
                    loaded.Add(new { ActorIndex = index, definition.ModelId, definition.SceneSlot, pose.BoneCount, MaximumVertices = resource.Geometry.Value.Max(mesh => mesh.Vertices.Length), model.Flags0, model.Flags8, model.Transform, model.Sequence, model.SequenceState, model.ActiveResourceRow100, model.GeometryWorldMatrix, CurrentBones = bones });
                }
                NativeBattleParticipantSnapshot casterParticipant = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 3).NativeParticipant!;
                Assert.True(casterParticipant.Active);
                Assert.False(casterParticipant.Hidden);
                Assert.False(casterParticipant.Suspended);
                Assert.False(casterParticipant.Untargetable);
                NativeBattleParticipantSnapshot recipient = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 4).NativeParticipant!;
                Assert.True(recipient.Active);
                Assert.True(recipient.Hidden);
                Assert.True(recipient.Suspended);
                Assert.True(recipient.Untargetable);
                Assert.False(recipient.AllocationPendingStatistics);
                Assert.Empty(source.NativeAllocations!);
                File.WriteAllText(Path.Combine(output!, "original-loaded-pair.json"), JsonSerializer.Serialize(new { Scope = "Fresh original formation104/stage19 loaded pair; model75 physical3 action151 sequence13 with explicit self3 header matching selector200, model34 physical4 latent activation recipient. No ordinary death-AI, KO or formula admission.", Loaded = loaded, Caster = casterParticipant, Recipient = recipient, Header = header }));
            }
            if (effectId == 8)
            {
                Assert.Equal((byte)4, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)11, header.ReadByte(2));
                Assert.Equal((byte?)0x8A, source.NativeGraphics.Models[caster].ModelId4);
                Assert.True(source.NativeEffectMemory!.Doom8ColdSourcesPending);
                Assert.Null(source.NativeEffectMemory.Doom8);
                Assert.NotNull(original.Doom8Resources);
            }
            if (effectId == 47)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)15, header.ReadByte(2));
                Assert.Equal((byte?)0x46, source.NativeGraphics.Models[caster].ModelId4);
                Assert.True(source.NativeEffectMemory!.Curse47ColdSourcesPending);
                Assert.Null(source.NativeEffectMemory.Curse47);
                Assert.NotNull(original.Curse47Resources);
            }
            if (effectId == 297)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal(3, request.PartyModelId);
                Assert.True(request.HealingContact);
                Assert.NotNull(source.NativeEffectMemory!.WhiteWind297);
                Assert.Equal(0u, source.NativeEffectMemory.WhiteWind297!.ReadDword(NativeBattleWhiteWind297Profile.WorkingGlobal));
                Assert.NotNull(original.WhiteWind297Resources);
            }
            if (effectId == 129)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)12, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)102, action.ActionId);
                Assert.NotNull(original.Breath129Resources);
                Assert.True(source.NativeEffectMemory!.Breath129!.PendingColdSources);
                Assert.Null(source.NativeEffectMemory.Breath129.MutableSources);
            }
            if (effectId == 139)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)11, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)182, action.ActionId);
                Assert.NotNull(original.Sonic139Resources);
                Assert.True(source.NativeEffectMemory!.Sonic139!.PendingColdSources);
                Assert.Null(source.NativeEffectMemory.Sonic139.OriginalFrames);
            }
            if (effectId == 298)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)118, source.NativeGraphics!.Models[caster].ModelId4);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.NotNull(original.Effect298Resources);
                Assert.NotNull(source.NativeEffectMemory!.Effect298!.RootBankStorage);
                Assert.Equal(45, source.NativeGraphics.Models[caster].CurrentBodyBoneMatrices!.Matrices.Length);
            }
            if (effectId == 279)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)46, source.NativeGraphics!.Models[caster].ModelId4);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.NotNull(original.MegaSpark279Resources);
                Assert.NotNull(source.NativeEffectMemory!.MegaSpark279!.RootBankStorage);
            }
            if (effectId == 304)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)95, source.NativeGraphics!.Models[caster].ModelId4);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.BoomerangSword304Resources);
                Assert.NotNull(source.NativeEffectMemory!.BoomerangSword304!.RootBankStorage);
                Assert.Equal((byte)13, header.ReadByte(2));
            }
            if (effectId == 302)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal(8, BattleSession.Restore(source).GetCombatantDefinition(source.NativeGraphics!.ActorIds[caster]!).ModelId);
                Assert.NotNull(original.SnipeLaser302Resources);
                Assert.NotNull(source.NativeEffectMemory!.SnipeLaser302!.RootBankStorage);
                Assert.Equal((byte)13, header.ReadByte(2));
            }
            if (effectId == 284)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)17, source.NativeGraphics!.Models[caster].ModelId4);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.EnemyMicroMissiles284Resources);
                Assert.NotNull(source.NativeEffectMemory!.EnemyMicroMissiles284!.RootBankStorage);
                Assert.Equal((byte)13, header.ReadByte(2));
            }
            if (effectId == 286)
            {
                Assert.Equal((byte)126, source.NativeGraphics!.Models[caster].ModelId4);
                Assert.Equal(110, BattleSession.Restore(source).GetCombatantDefinition(source.NativeGraphics.ActorIds[caster]!).ModelId);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.MiniPulseCannon286Resources);
                Assert.NotNull(source.NativeEffectMemory!.MiniPulseCannon286!.RootBankStorage);
                Assert.Equal((byte)12, header.ReadByte(2));
            }
            if (effectId == 282)
            {
                Assert.Equal((byte)110, source.NativeGraphics!.Models[caster].ModelId4);
                Assert.Equal(94, BattleSession.Restore(source).GetCombatantDefinition(source.NativeGraphics.ActorIds[caster]!).ModelId);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.EvilEye282Resources);
                Assert.NotNull(source.NativeEffectMemory!.EvilEye282!.RootBankStorage);
                Assert.Equal((byte)12, header.ReadByte(2));
            }
            if (effectId == 281)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.Shotgun281Resources);
                Assert.NotNull(source.NativeEffectMemory!.Shotgun281!.RootBankStorage);
            }
            if (effectId == 214)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.Gravija214Resources);
                Assert.NotNull(source.NativeEffectMemory!.Gravija214!.RootBankStorage);
                Assert.Equal(new byte[NativeBattleGravija214Profile.EntryCopyLength], source.NativeEffectMemory!.Gravija214!.Read(NativeBattleGravija214Profile.CopiedMeshAddress, NativeBattleGravija214Profile.EntryCopyLength));
            }
            if (effectId == 44)
            {
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.Junk44Resources);
                Assert.NotNull(source.NativeEffectMemory!.Junk44!.RootBankStorage);
                Assert.Equal((byte)15, header.ReadByte(2));
            }
            if (effectId == 45)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.Stare45Resources);
                Assert.All(source.NativeEffectMemory!.Stare45!.InactiveBanks, bank => Assert.NotNull(bank));
                Assert.Equal((byte)0, header.ReadByte(17));
            }
            if (effectId == 46)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.Sigh46Resources);
                Assert.Equal(false, source.NativeEffectMemory!.Sigh46!.SourcesLoaded);
                Assert.All(source.NativeEffectMemory.Sigh46.InactiveBanks, bank => Assert.NotNull(bank));
                Assert.Equal((byte)0, header.ReadByte(17));
            }
            if (effectId == 48)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.MagmaBreath48Resources);
                Assert.Equal(false, source.NativeEffectMemory!.MagmaBreath48!.SourcesLoaded);
                Assert.All(source.NativeEffectMemory.MagmaBreath48.InactiveBanks, bank => Assert.NotNull(bank));
                Assert.Equal((byte)0, header.ReadByte(17));
            }
            if (effectId == 49)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.NotNull(original.Resonance49Resources);
                Assert.Equal(6, source.NativeEffectMemory!.Resonance49!.InactiveBanks.Length);
                Assert.All(source.NativeEffectMemory.Resonance49.InactiveBanks, bank => Assert.NotNull(bank));
                Assert.Equal((byte)1, header.ReadByte(16));
                Assert.Equal((byte)0, header.ReadByte(17));
            }
            if (effectId == 12)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)15, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)7, action.ActionId);
                Assert.NotNull(original.BladeShot12Resources);
                NativeBattleBladeShot12State state = source.NativeEffectMemory!.BladeShot12!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildABankStorage);
                Assert.NotNull(state.ChildBBankStorage);
                Assert.Equal(false, state.VertexSourcesLoaded);
                blade12Observations.Add(new { Boundary = "original-caster-object7-and-recipient", Caster = source.NativeGraphics.Models[3], Recipient = source.NativeGraphics.Models[0] });
            }
            if (effectId == 170)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)15, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)35, action.ActionId);
                Assert.NotNull(original.Acid170Resources);
                NativeBattleAcid170State state = source.NativeEffectMemory!.Acid170!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                acid170OriginalFlags = source.NativeGraphics.Models[0].Flags0;
                acid170Observations.Add(new { Boundary = "distinct-original-caster-and-recipient", Caster = source.NativeGraphics.Models[3], Recipient = source.NativeGraphics.Models[0] });
            }
            if (effectId == 172)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)53, action.ActionId);
                Assert.NotNull(original.Morph172Resources);
                NativeBattleMorph172State state = source.NativeEffectMemory!.Morph172!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                morph172Observations.Add(new { Boundary = "original-header-retained-without-contact", Header = header, Contact = source.NativeActionPackets!.ReadContact(header.FirstContactIndex!.Value), Model = source.NativeGraphics.Models[3] });
            }
            if (effectId == 171)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)32, action.ActionId);
                Assert.NotNull(original.PoisonGas171Resources);
                NativeBattlePoisonGas171State state = source.NativeEffectMemory!.PoisonGas171!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                Assert.Equal(0u, state.ReadDword(NativeBattlePoisonGas171Profile.WorkingGlobal));
                poison171OriginalFlags = source.NativeGraphics.Models[3].Flags0;
                poison171Observations.Add(new { Boundary = "original-caster-current42bones", Model = source.NativeGraphics.Models[3] });
            }
            if (effectId == 181)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)24, header.ReadByte(2));
                Assert.Equal((byte)31, action.Command);
                Assert.Equal((ushort)8, action.ActionId);
                Assert.NotNull(original.Additional181Resources);
                NativeBattleAdditional181State state = source.NativeEffectMemory!.Additional181!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                Assert.Equal(0u, state.ReadDword(NativeBattleAdditional181Profile.WorkingGlobal));
                additional181Observations.Add(new { Boundary = "original-caster-and-target", Caster = source.NativeGraphics.Models[0], Target = source.NativeGraphics.Models[3] });
            }
            if (effectId == 182)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)11, header.ReadByte(2));
                Assert.Equal((byte)29, action.Command);
                Assert.Equal((ushort)10, action.ActionId);
                Assert.NotNull(original.Additional182Resources);
                NativeBattleAdditional182State state = source.NativeEffectMemory!.Additional182!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                Assert.Equal(0u, state.ReadDword(NativeBattleAdditional182Profile.WorkingGlobal));
                additional182OriginalFlags = source.NativeGraphics.Models[contactActor].Flags0;
                additional182Observations.Add(new { Boundary = "original-model-baseline", Model = source.NativeGraphics.Models[contactActor] });
            }
            if (effectId == 186)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)122, action.ActionId);
                Assert.NotNull(original.Blaster186Resources);
                NativeBattleBlaster186State state = source.NativeEffectMemory!.Blaster186!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                Assert.Equal(0u, state.ReadDword(NativeBattleBlaster186Profile.WorkingGlobal));
                blaster186FramesW = Blaster186FrameWordLanes(state);
                blaster186MatrixPadding = state.Read(NativeBattleBlaster186Profile.MatrixAddress + 18, 14);
            }
            if (effectId == 151)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)16, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)69, action.ActionId);
                Assert.NotNull(original.CannonBlow151Resources);
                NativeBattleCannonBlow151State state = source.NativeEffectMemory!.CannonBlow151!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                Assert.Equal(0u, state.ReadDword(NativeBattleCannonBlow151Profile.WorkingGlobal));
                cannon151SamplesW = Cannon151WordLanes(state);
                cannon151MatrixPadding = state.Read(NativeBattleCannonBlow151Profile.CurveMatrixAddress + 18, 2);
            }
            if (effectId == 190)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)154, action.ActionId);
                Assert.NotNull(original.ChainGun190Resources);
                NativeBattleChainGun190State state = source.NativeEffectMemory!.ChainGun190!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                Assert.Equal(0u, state.ReadDword(NativeBattleChainGun190Profile.CursorGlobal));
                Assert.Equal(new byte[4], state.ReadPacket(NativeBattleChainGun190Profile.PacketBase, 4));
                Assert.Equal((byte)1, header.ReadByte(16));
            }
            if (effectId == 150)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)68, action.ActionId);
                Assert.NotNull(original.GatlingGun150Resources);
                NativeBattleGatlingGun150State state = source.NativeEffectMemory!.GatlingGun150!;
                Assert.NotNull(state.RootBankStorage);
                Assert.NotNull(state.ChildBankStorage);
                Assert.Equal(0u, state.ReadDword(NativeBattleGatlingGun150Profile.CursorGlobal));
                Assert.Equal(new byte[4], state.ReadPacket(NativeBattleGatlingGun150Profile.PacketBase, 4));
            }
            if (effectId == 183)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)23, header.ReadByte(2));
                Assert.Equal((byte)23, action.Command);
                Assert.Equal((ushort)11, action.ActionId);
                Assert.NotNull(original.Effect183Resources);
                NativeBattleEffect183State retained = source.NativeEffectMemory!.Effect183!;
                Assert.NotNull(retained.RootBankStorage);
                Assert.NotNull(retained.ChildBankStorage);
                Assert.Equal(0u, retained.ReadDword(NativeBattleEffect183Profile.CursorGlobal));
                Assert.Equal(new byte[4], retained.ReadPacket(NativeBattleEffect183Profile.PacketBase, 4));
            }
            if (effectId == 157)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.NotNull(original.Breath157Resources);
                NativeBattleBreath157State breath = source.NativeEffectMemory!.Breath157!;
                Assert.NotNull(breath.RootBankStorage);
                Assert.NotNull(breath.ChildBankStorage);
                Assert.Equal(0u, breath.ReadDword(NativeBattleBreath157Profile.WorkspaceGlobal));
                breath157Rotation = Breath157Rotation(breath);
                breath157PointW = Breath157PointW(breath);
            }
            if (effectId == 156)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)17, header.ReadByte(2));
                Assert.NotNull(original.WildCannonBlow156Resources);
                NativeBattleWildCannonBlow156State cannon = source.NativeEffectMemory!.WildCannonBlow156!;
                Assert.NotNull(cannon.RootBankStorage);
                Assert.NotNull(cannon.ChildBankStorage);
                Assert.Equal(0u, cannon.ReadDword(NativeBattleWildCannonBlow156Profile.WorkspaceGlobal));
                cannon156SamplesW = Cannon156WordLanes(cannon, NativeBattleWildCannonBlow156Profile.SamplesAddress, 128);
            }
            if (effectId == 155)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.NotNull(original.SandStorm155Resources);
                NativeBattleSandStorm155State sand = source.NativeEffectMemory!.SandStorm155!;
                Assert.NotNull(sand.RootBankStorage);
                Assert.NotNull(sand.ChildBankStorage);
                Assert.Equal(0u, sand.ReadDword(NativeBattleSandStorm155Profile.CursorGlobal));
                sand155SelectedW = Sand155WordLanes(sand, NativeBattleSandStorm155Profile.SelectedVerticesAddress, NativeBattleSandStorm155Profile.VertexCount);
                sand155SamplesW = Sand155WordLanes(sand, NativeBattleSandStorm155Profile.SamplesAddress, NativeBattleSandStorm155Profile.SampleCount);
            }
            if (effectId == 128)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.NotNull(original.GastricJuice128Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.GastricJuice128!.ReadDword(NativeBattleGastricJuice128Profile.WorkingGlobal));
            }
            if (effectId == 132)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)12, header.ReadByte(2));
                Assert.NotNull(original.DisolvingAcid132Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.DisolvingAcid132!.ReadDword(NativeBattleDisolvingAcid132Profile.WorkingGlobal));
            }
            if (effectId == 136)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.NotNull(original.OilShot136Resources);
                Assert.True(source.NativeEffectMemory!.OilShot136!.PendingColdSources);
                Assert.Equal(0u, source.NativeEffectMemory.OilShot136.ReadDword(NativeBattleOilShot136Profile.WorkingGlobal));
            }
            if (effectId == 138)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)16, header.ReadByte(2));
                Assert.NotNull(original.Saliva138Resources);
                Assert.True(source.NativeEffectMemory!.Saliva138!.PendingColdSources);
                Assert.Equal(0u, source.NativeEffectMemory.Saliva138.ReadDword(NativeBattleSaliva138Profile.WorkingGlobal));
            }
            if (effectId == 137)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)12, header.ReadByte(2));
                Assert.NotNull(original.OilBlast137Resources);
                Assert.True(source.NativeEffectMemory!.OilBlast137!.ColdMatrixSourcePending);
                Assert.Equal(0u, source.NativeEffectMemory.OilBlast137.ReadDword(NativeBattleOilBlast137Profile.WorkingGlobal));
            }
            if (effectId == 158)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal((byte?)40, source.NativeGraphics.Models[caster].ModelId4);
                Assert.NotNull(source.NativeEffectMemory!.MeltEye158);
                Assert.Equal(0u, source.NativeEffectMemory.MeltEye158!.ReadDword(NativeBattleMeltEye158Profile.CursorGlobal));
                Assert.NotNull(original.MeltEye158Resources);
            }
            if (effectId == 217)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)16, header.ReadByte(2));
                Assert.Equal(new byte?[] { 128, 129 }, source.NativeGraphics.Models.Skip(3).Take(2).Select(model => model.ModelId4).ToArray());
                Assert.NotNull(source.NativeEffectMemory!.EnergyBomber217);
                Assert.Equal(0u, source.NativeEffectMemory.EnergyBomber217!.ReadDword(NativeBattleEnergyBomber217Profile.WorkingGlobal));
                Assert.NotNull(original.EnergyBomber217Resources);
            }
            if (effectId == 101)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal((byte?)125, source.NativeGraphics.Models[caster].ModelId4);
                Assert.NotNull(source.NativeEffectMemory!.Effect101);
                Assert.Equal(0u, source.NativeEffectMemory.Effect101!.ReadDword(NativeBattleEffect101Profile.WorkingGlobal));
                Assert.NotNull(original.Effect101Resources);
            }
            if (effectId == 134)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)176, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.BeamLaser134Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.BeamLaser134!.ReadDword(NativeBattleBeamLaser134Profile.WorkingGlobal));
            }
            if (effectId == 133)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)11, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)76, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.Hypnotize133Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.Hypnotize133!.ReadDword(NativeBattleHypnotize133Profile.WorkingGlobal));
            }
            if (effectId is 177 or 178)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal(effectId == 177 ? (ushort)25 : (ushort)230, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.NeedlesFamilyResources);
                NativeBattleNeedlesFamilyState needlesSource = effectId == 177 ? source.NativeEffectMemory!.ThousandNeedles177! : source.NativeEffectMemory!.TenThousandNeedles178!;
                Assert.Equal(effectId, needlesSource.Effect);
                Assert.Equal(0u, needlesSource.ReadDword(needlesSource.Profile.CursorGlobal));
                Assert.All(Enumerable.Range(0, 300), index => Assert.Equal(0u, needlesSource.ReadDword(needlesSource.Profile.NeedleBank + (uint)(56 * index))));
                Assert.All(Enumerable.Range(0, 300), index => Assert.Equal(0u, needlesSource.ReadDword(needlesSource.Profile.SparkBank + (uint)(24 * index))));
            }
            if (effectId == 71)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)0, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.LaserEye71Resources);
                Assert.True(source.NativeEffectMemory!.LaserEye71!.ColdSourcePending);
                Assert.Null(source.NativeEffectMemory.LaserEye71.ExecutableBytes);
            }
            if (effectId == 131)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)184, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.BadBreath131Resources);
                Assert.True(source.NativeEffectMemory!.BadBreath131!.PendingColdSources);
                Assert.Null(source.NativeEffectMemory!.BadBreath131!.MeshA);
                Assert.Null(source.NativeEffectMemory!.BadBreath131!.MeshB);
            }
            if (effectId == 130)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)185, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.EerieSoundWave130Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.EerieSoundWave130!.ReadDword(NativeBattleEerieSoundWave130Profile.WorkingGlobal));
            }
            if (effectId == 127)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)29, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.PetrifyStare127Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.PetrifyStare127!.ReadDword(NativeBattlePetrifyStare127Profile.WorkingGlobal));
            }
            if (effectId == 260)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)15, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.ShockwavePulsar260Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.ShockwavePulsar260!.ReadDword(NativeBattleShockwavePulsar260Profile.WorkingGlobal));
            }
            if (effectId == 307)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)14, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.RayBomb307Resources);
                Assert.Equal(0u, source.NativeEffectMemory!.RayBomb307!.ReadDword(NativeBattleRayBomb307Profile.WorkingGlobal));
            }
            if (effectId == 184)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)1, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(original.UltraWaves184Resources);
                Assert.True(source.NativeEffectMemory!.UltraWaves184!.ColdSourcesPending);
                Assert.Equal(0u, source.NativeEffectMemory.UltraWaves184.ReadCursor());
            }
            if (effectId == 135)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)15, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)177, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(source.NativeEffectMemory!.ReflectBeam135);
                Assert.NotNull(original.ReflectBeam135Resources);
                Assert.Equal(new byte[32], source.NativeEffectMemory.ReflectBeam135!.Read(NativeBattleReflectBeam135Profile.ScaleMatrixGlobal, 32));
                reflectPoseSource = assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource ?? throw new InvalidDataException("Reflect Beam requires its original caster skeleton.");
            }
            if (effectId == 126)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)15, header.ReadByte(2));
                Assert.Equal((byte)8, action.Command);
                Assert.Equal((ushort)28, action.ActionId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.True(source.NativeEffectMemory!.ElectricDischarge126!.ColdSourcesPending);
                Assert.Null(source.NativeEffectMemory.ElectricDischarge126.FlashThreshold);
                Assert.Equal(0u, source.NativeEffectMemory.ElectricDischarge126.ReadDword(NativeBattleElectricDischarge126Profile.WorkingGlobal));
                Assert.NotNull(original.ElectricDischarge126Resources);
            }
            if (effectId == 275)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)2, action.ActionId);
                Assert.Equal(3, request.PartyModelId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(source.NativeEffectMemory!.Electrocute275);
                Assert.True(source.NativeEffectMemory.Electrocute275!.ColdSourcesPending);
                Assert.Null(source.NativeEffectMemory.Electrocute275.FlashThreshold);
                Assert.Equal(0u, source.NativeEffectMemory.Electrocute275.ReadDword(NativeBattleElectrocute275Profile.WorkingGlobal));
                Assert.NotNull(original.Electrocute275Resources);
            }
            if (effectId == 317)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)7, action.ActionId);
                Assert.Equal(3, request.PartyModelId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(source.NativeEffectMemory!.Acid317);
                Assert.Equal(0u, source.NativeEffectMemory.Acid317!.ReadDword(NativeBattleAcid317Profile.WorkingGlobal));
                Assert.NotNull(original.Acid317Resources);
            }
            if (effectId == 305)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)8, action.ActionId);
                Assert.Equal(3, request.PartyModelId);
                Assert.Equal((ushort?)7, request.ControlledContactValue);
                Assert.NotNull(source.NativeEffectMemory!.GatlingGun305);
                Assert.Equal(0u, source.NativeEffectMemory.GatlingGun305!.ReadDword(NativeBattleGatlingGun305Profile.CursorGlobal));
                Assert.Equal(4, source.NativeEffectMemory.GatlingGun305.ReadPacket(NativeBattleGatlingGun305Profile.PacketBase, 4).Length);
                Assert.NotNull(original.GatlingGun305Resources);
            }
            if (effectId == 306)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)4, action.ActionId);
                Assert.Equal(3, request.PartyModelId);
                Assert.Equal((ushort?)0, request.ControlledContactValue);
                Assert.Equal(0u, source.NativeEffectMemory!.Degenerator306!.ReadDword(NativeBattleDegenerator306Profile.WorkingGlobal));
                Assert.NotNull(original.Degenerator306Resources);
            }
            if (effectId == 300)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)10, action.ActionId);
                Assert.Equal(3, request.PartyModelId);
                Assert.Equal((ushort?)0, request.ControlledContactValue);
                NativeBattleBadBreath300State state = source.NativeEffectMemory!.BadBreath300!;
                Assert.True(state.PendingColdSources);
                Assert.Null(state.MeshA);
                Assert.Null(state.MeshB);
                Assert.Equal(0u, state.ReadDword(NativeBattleBadBreath300Profile.WorkingGlobal));
                Assert.NotNull(original.BadBreath300Resources);
            }
            if (effectId == 299)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal((byte)15, action.Command);
                Assert.Equal((ushort)6, action.ActionId);
                Assert.Equal(3, request.PartyModelId);
                Assert.False(request.HealingContact);
                Assert.NotNull(source.NativeEffectMemory!.MicroMissiles299);
                Assert.Equal(0u, source.NativeEffectMemory.MicroMissiles299!.ReadDword(NativeBattleMicroMissiles299Profile.WorkingGlobal));
                Assert.NotNull(original.MicroMissiles299Resources);
            }
            if (effectId == 74)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((byte)21, header.ReadByte(2));
                Assert.Equal(3, request.PartyModelId);
                Assert.Equal((ushort?)0, request.ControlledContactValue);
                Assert.True(source.NativeEffectMemory!.LevelDeath74!.PendingColdSources);
                Assert.NotSame(source.NativeEffectMemory.Death14, source.NativeEffectMemory.LevelDeath74);
                Assert.Equal((ushort)74, original.DeathResources!.Effect);
            }
            if (effectId == 19)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)8, header.ReadByte(1));
                Assert.True(request.PreserveOriginalFormationEnemies);
                Assert.Equal((byte)0x6F, source.NativeGraphics.Models[caster].ModelId4);
                Assert.Equal(new byte?[] { 0x70, 0x70, 0x70 }, source.NativeGraphics.Models.Skip(4).Take(3).Select(model => model.ModelId4).ToArray());
                Assert.Equal((ushort)2, (ushort)(source.NativeGraphics.Models[caster].Flags0 & 6));
            }
            if (effectId == 20)
            {
                Assert.True(request.PreserveOriginalFormationEnemies);
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)12, header.ReadByte(2));
                Assert.Equal((byte)0x54, source.NativeGraphics.Models[caster].ModelId4);
                Assert.Equal((byte)0x5D, source.NativeGraphics.Models[6].ModelId4);
                NativeBattleParticipantSnapshot initial = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 6).NativeParticipant!;
                Assert.True(initial.Hidden);
                Assert.True(initial.Suspended);
                Assert.True(initial.Untargetable);
            }
            if (effectId == 60)
            {
                Assert.True(request.PreserveOriginalFormationEnemies);
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)13, header.ReadByte(2));
                Assert.Equal(new byte?[] { 0x99, 0x98 }, source.NativeGraphics.Models.Skip(3).Take(2).Select(model => model.ModelId4).ToArray());
                Assert.Equal(3, source.Combatants.Length);
                Assert.All(source.Combatants, actor =>
                {
                    Assert.True(actor.NativeParticipant!.Active);
                    Assert.False(actor.NativeParticipant.Hidden);
                    Assert.False(actor.NativeParticipant.Suspended);
                    Assert.False(actor.NativeParticipant.Untargetable);
                });
                Assert.Null(source.NativeEffectMemory!.RequireEffect60().Nodes);
                Assert.Equal(0u, source.NativeEffectMemory.Effect60PrimaryExecutable!.InitializationFlag);
                Assert.Equal(0u, source.NativeEffectMemory.Effect60SecondaryExecutable!.InitializationFlag);
            }
            if (effectId == 29)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)14, header.ReadByte(2));
                Assert.Equal((byte)0x13, source.NativeGraphics.Models[caster].ModelId4);
                Assert.Equal(2, source.Combatants.Length);
                Assert.Null(source.NativeEffectMemory!.RequireWindBlast29().Nodes);
                Assert.Equal(0u, source.NativeEffectMemory.WindBlast29Executable!.InitializationFlag);
            }
            if (effectId == 43)
            {
                Assert.Equal((byte)3, caster);
                Assert.Equal((byte)0, contactActor);
                Assert.Equal((byte)12, header.ReadByte(2));
                Assert.Equal((byte)0x35, source.NativeGraphics.Models[caster].ModelId4);
                Assert.Equal(2, source.Combatants.Length);
                Assert.Null(source.NativeEffectMemory!.RequireMeltingBubble43().Nodes);
                Assert.Equal(1u, source.NativeEffectMemory.MeltingBubble43Executable!.InitializationFlag);
                Assert.Equal(260, source.NativeEffectMemory.MeltingBubble43Executable.Data!.Value.Length);
                meltingPoseSource = assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource ?? throw new InvalidDataException("Melting Bubble requires its original caster skeleton.");
            }
            if (effectId == 62)
            {
                Assert.Equal((byte)0, caster);
                Assert.Equal((byte)3, contactActor);
                Assert.Equal((ushort?)0, request.ControlledContactValue);
                Assert.Equal((ushort)2, (ushort)(source.NativeGraphics.Models[contactActor].Flags0 & 6));
            }
            Assert.Equal(originalAction is not null, exported.Selection!.OriginalKernelEffectReference);
            Assert.Equal(action.MagicId, exported.Selection.ShellEffect);
            NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
            Assert.Equal(checked((byte)(header.FirstContactIndex!.Value + 1)), source.NativeActionPackets.ContactCount);
            Assert.Equal(contactActor, contact.Primary.ActorIndex);
            Assert.Equal(request.ControlledContactValue ?? (ushort)7, contact.Primary.Amount6);
            if (effectId == 297)
            {
                Assert.Equal((byte)1, contact.Primary.HitFlags3);
                Assert.Equal((ushort)7, contact.Primary.Amount6);
                NativeBattleStatusSnapshot retained = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0).NativeStatus!;
                Assert.Equal(retained.Saved, contact.Primary.SavedStatuses4);
                Assert.Equal(retained.Battle, contact.Primary.BattleStatuses8);
            }
            if (effectId is 306 or 182)
            {
                Assert.Contains(contact.Primary.HitFlags3, new byte[] { 0, 4 });
                Assert.Equal((ushort)0, contact.Primary.Amount6);
                NativeBattleStatusSnapshot retained = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == contactActor).NativeStatus!;
                Assert.Equal(retained.Saved, contact.Primary.SavedStatuses4);
                Assert.Equal(retained.Battle, contact.Primary.BattleStatuses8);
            }
            if (effectId == 300)
            {
                Assert.Equal((byte)4, contact.Primary.HitFlags3);
                Assert.Equal((ushort)0, contact.Primary.Amount6);
                NativeBattleStatusSnapshot retained = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == contactActor).NativeStatus!;
                Assert.Equal(retained.Saved, contact.Primary.SavedStatuses4);
                Assert.Equal(retained.Battle, contact.Primary.BattleStatuses8);
            }
            if (effectId == 74)
            {
                Assert.Contains(contact.Primary.HitFlags3, new byte[] { 0, 4 });
                Assert.Equal((ushort)0, contact.Primary.Amount6);
            }
            if (request.ControlledDrainContact)
            {
                Assert.Equal((byte)0, contact.Secondary.ActorIndex);
                byte[] resistances = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == contactActor).NativeStatus!.Resistances ?? throw new InvalidDataException("The controlled drain target requires its actual resistance owner.");
                Assert.Equal(40, resistances.Length);
                int percent = Math.Max(0, action.StatusAttack - resistances[23]);
                Assert.Equal(checked((ushort)Math.Min(9999, contact.Primary.Amount6 * percent / 100)), contact.Secondary.Amount6);
                Assert.Equal((byte)1, contact.Secondary.HitFlags3);
            }
            else
            {
                Assert.Equal((byte)255, contact.Secondary.ActorIndex);
            }
            NativeBattleEffectTask? expectedText = NativeBattleEffectsState.Clear().PrependDamageText(contact.Primary.ActorIndex, contact.Primary.ContactFlags, contact.Primary.Amount6).Slots.SingleOrDefault(task => (task.Flags0 & 1) != 0);
            NativeBattleEffectTask? expectedSecondaryText = request.ControlledDrainContact ? NativeBattleEffectsState.Clear().PrependDamageText(contact.Secondary.ActorIndex, contact.Secondary.ContactFlags, contact.Secondary.Amount6).Slots.SingleOrDefault(task => (task.Flags0 & 1) != 0) : null;
            int logicalCasterHp = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == caster).CurrentHp;
            string target = source.NativeGraphics!.ActorIds[contactActor]!;
            int logicalHp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
            string? originalTargetFootprint = effectId == 60 ? TargetFootprint(source.NativeGraphics.Models[contactActor]) : null;
            string? originalTargetPosition = effectId is 60 or 101 or 186 ? TargetPositionFootprint(source.NativeGraphics.Models[contactActor]) : null;
            string? originalMeltingChannels = effectId == 43 ? JsonSerializer.Serialize(source.NativeEffectMemory!.RequireMeltingBubble43().Channels) : null;
            int initialSounds = source.Events.Count(item => item.NativeModuleSound is not null);
            string initialCamera = JsonSerializer.Serialize(source.NativeCamera);
            string initialStage = StageColors(source);
            Assert.Equal(request.PartyModelId, source.Definition.Combatants.Single(actor => actor.IsPlayer).ModelId);
            Assert.NotNull(source.NativeModelCpuResources);
            File.WriteAllText(Path.Combine(output!, "original-binding.json"), JsonSerializer.Serialize(new { action.Command, action.ActionId, action.MagicId, action.CrisisLevel, action.KernelSection, action.KernelIndex, action.SupplementalKernelIndex, Formation = formation.Id, formation.StageId, Enemy = enemy.ModelId, enemy.Slot, enemy.Level, Caster = caster, Target = contactActor, OriginalKernelReference = originalAction is not null, RequestedEffect = effectId }));
            battle = BattleSession.Restore(source);
            battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(battle.Capture()));
            scene = new(assets, source.Definition, true);
            ColdNext("queued", source);
            if (effectId == 182)
            {
                additional182Boundaries.Add("queued-before-entry-old0");
                additional182Observations.Add(new { Boundary = "queued-before-entry-old0", ColdSnapshot = "queued.battle.json", Scope = "Original queued action104 before the entry and first old0 run in the same graphics frame" });
            }
            // This is a diagnostic liveness guard, never a claim about an effect's native duration.
            for (; callbacks < 4096; callbacks++)
            {
                boundary = "native-callback";
                acid170BodyPackets = ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
                prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleOriginalEffectState? after = Selected(current);
                Assert.Equal(logicalHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.NotNull(current.NativeCamera?.Projection?.View);
                if (effectId == 129 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleBreath129State breath = current.NativeEffectMemory!.Breath129!;
                    uint working = breath.ReadDword(NativeBattleBreath129Profile.WorkingGlobal);
                    NativeBattleEffectTaskPool? mainPool = after.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBreath129Profile.MainDescriptor);
                    NativeBattleModulePoolTask? main = mainPool?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBreath129Profile.MainCallback);
                    if (main is not null)
                    {
                        string? phase = main.ReadWord(12) == 1 ? "both-native-meshes-preprocessed" : main.ReadWord(12) == 31 ? "first-custom-mesh-deformation" : main.ReadWord(12) == 73 ? "before-main-drain" : null;
                        if (phase is not null && breath129Boundaries.Add(phase))
                        {
                            Assert.False(breath.PendingColdSources);
                            Assert.NotNull(breath.MutableSources);
                            if (phase == "both-native-meshes-preprocessed")
                            {
                                short[] first = Enumerable.Range(0, 176).Select(index => unchecked((short)breath.ReadWord(NativeBattleBreath129Profile.MeshAddress + 14 + (uint)(index * 8)))).ToArray();
                                short[] second = Enumerable.Range(0, 88).Select(index => unchecked((short)breath.ReadWord(NativeBattleBreath129Profile.IndexedVerticesAddress + 14 + (uint)(index * 8)))).ToArray();
                                Assert.Equal(-4096, first.Min());
                                Assert.Equal(-4075, second.Min());
                            }
                            if (phase == "first-custom-mesh-deformation")
                            {
                                int offset = checked((int)(working - 0x20DFAB8u)) + NativeBattleBreath129Profile.SelectedVerticesOffset;
                                (byte[] bytes, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(offset, 176 * 8);
                                for (int vertex = 0; vertex < 176; vertex++)
                                {
                                    Assert.All(known.AsSpan(vertex * 8, 6).ToArray(), Assert.True);
                                }
                                breath129Observations.Add(new { Boundary = phase, Data = bytes, Known = known });
                            }
                            ColdNext(phase, current);
                        }
                    }
                    foreach (NativeBattleEffectTaskPool pool in after.Pools.Skip(1))
                    {
                        foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            if (task.ReadWord(12) > 0 && breath129Callbacks.Add(task.Callback))
                            {
                                string name = "breath-callback-" + task.Callback.ToString("X");
                                breath129Boundaries.Add(name);
                                if (task.Callback == NativeBattleBreath129Profile.TargetCallback)
                                {
                                    Assert.Equal(192, task.ReadBytes(0x128, 192).Length);
                                    breath129Observations.Add(new { Boundary = name, CapturedTriangles = task.ReadBytes(0x128, 192) });
                                }
                                ColdNext(name, current);
                            }
                            if (task.Callback == NativeBattleBreath129Profile.TargetCallback && task.ReadWord(12) == 16 && breath129Boundaries.Add("before-target-old16-contact"))
                            {
                                ColdNext("before-target-old16-contact", current);
                            }
                            if (task.Callback == NativeBattleBreath129Profile.CurveCallback && task.ReadWord(12) >= 3 && breath129Boundaries.Add("linked-points-live"))
                            {
                                uint head = task.ReadDword(0x18);
                                Assert.InRange(head, working + NativeBattleBreath129Profile.PointBankOffset, working + 0x35C0u - 28u);
                                Assert.Equal(0u, (head - working - NativeBattleBreath129Profile.PointBankOffset) % 28);
                                ColdNext("linked-points-live", current);
                            }
                        }
                    }
                    uint cursor = breath.ReadDword(NativeBattleBreath129Profile.CursorGlobal);
                    if (working != 0 && cursor != 0)
                    {
                        NativeBattleModulePoolTask root = after.Pools[0].Slots.Single(task => task.Callback == NativeBattleBreath129Profile.RootCallback);
                        int bank = root.ReadDword(16) == 0 ? 0 : 1;
                        if (breath129PacketBanks.Add(bank))
                        {
                            ColdNext("breath-packet-bank-" + bank, current);
                        }
                        int first = checked((int)(working - 0x20DFAB8u)) + (bank == 0 ? NativeBattleBreath129Profile.FirstPacketOffset : NativeBattleBreath129Profile.SecondPacketOffset);
                        int end = checked((int)(cursor - 0x20DFAB8u));
                        foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.ByteOffset >= first && command.Packet.ByteOffset < end))
                        {
                            breath129Opcodes.Add(command.Opcode);
                        }
                    }
                }
                if (effectId == 139 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleSonic139State sonic = current.NativeEffectMemory!.Sonic139!;
                    NativeBattleEffectTaskPool? mainPool = after.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSonic139Profile.MainDescriptor);
                    NativeBattleModulePoolTask? mainTask = mainPool?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSonic139Profile.MainCallback);
                    string? phase = mainTask is null ? null : mainTask.ReadWord(12) == 1 ? "preprocessed-first-warp" : mainTask.ReadWord(12) == 31 ? "phase30-warp" : mainTask.ReadWord(12) == 70 ? "before-old70-COMMON" : mainTask.ReadWord(12) == 71 && mainTask.ReadByte(0x13) == 0 ? "COMMON-completion-pending" : null;
                    if (phase is not null && sonic139Boundaries.Add(phase))
                    {
                        Assert.False(sonic.PendingColdSources);
                        Assert.NotNull(sonic.OriginalFrames);
                        Assert.NotNull(current.NativeEffectMemory!.GeometryRegisters!.Lzcs);
                        Assert.NotNull(current.NativeEffectMemory!.GeometryRegisters!.Lzcr);
                        if (phase == "preprocessed-first-warp")
                        {
                            uint firstFramePayload = sonic.ReadDword(NativeBattleSonic139Profile.MutableFramesAddress + 12);
                            Assert.NotEqual(BitConverter.ToUInt32(original.Sonic139Resources!.FramesSeed.Data.AsSpan().Slice(12, 4)), firstFramePayload);
                            Assert.Equal((uint)NativeBattleSonic139Profile.VertexCount, sonic.ReadDword(NativeBattleSonic139Profile.CopiedMeshAddress + 4));
                        }
                        ColdNext(phase, current);
                    }
                    NativeBattleEffectTaskPool? targetPool = after.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSonic139Profile.LowerDescriptor);
                    if (targetPool is not null && targetPool.Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSonic139Profile.TargetCallback && task.ReadWord(12) > 0) && sonic139Boundaries.Add("recipient-tint-live"))
                    {
                        ColdNext("recipient-tint-live", current);
                    }
                    uint working = sonic.ReadDword(NativeBattleSonic139Profile.WorkingGlobal);
                    uint cursor = sonic.ReadDword(NativeBattleSonic139Profile.CursorGlobal);
                    if (cursor != 0 && working != 0)
                    {
                        int bank = cursor > working + NativeBattleSonic139Profile.SecondPacketOffset ? 1 : 0;
                        if (sonic139PacketBanks.Add(bank))
                        {
                            ColdNext("sonic-packet-bank-" + bank, current);
                        }
                        int first = checked((int)(working - 0x20DFAB8u)) + (bank == 0 ? NativeBattleSonic139Profile.FirstPacketOffset : NativeBattleSonic139Profile.SecondPacketOffset);
                        int end = checked((int)(cursor - 0x20DFAB8u));
                        foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.ByteOffset >= first && command.Packet.ByteOffset < end))
                        {
                            sonic139Opcodes.Add(command.Opcode);
                        }
                    }
                }
                if (effectId == 79)
                {
                    effect79.Observe(prior, current, ColdNext);
                }
                if (effectId == 83)
                {
                    effect83.Observe(prior, current, ColdNext);
                }
                if (effectId == 289)
                {
                    effect289.Observe(prior, current, ColdNext);
                }
                if (effectId == 315)
                {
                    effect315.Observe(prior, current, ColdNext);
                }
                if (effectId == 283)
                {
                    effect283.Observe(prior, current, ColdNext);
                }
                if (effectId == 303)
                {
                    effect303.Observe(prior, current, ColdNext);
                }
                if (effectId == 298 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleEffect298State state = current.NativeEffectMemory!.Effect298!;
                    NativeBattleEffect298Resources resources = original.Effect298Resources!;
                    Assert.Null(state.RootBankStorage);
                    Assert.Equal(NativeBattleEffect298Profile.ArenaAddress, state.ReadDword(NativeBattleEffect298Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, state.ReadDword(NativeBattleEffect298Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 5);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleEffectTaskPool pool = after.Pools[poolIndex];
                        NativeBattleCureFamilyPool expected = NativeBattleEffect298Profile.Pools[poolIndex];
                        Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                        Assert.Equal(expected.Bank, pool.BankAddress);
                        Assert.Equal(expected.Stride, pool.Stride);
                        Assert.Equal((int)expected.Capacity, pool.Slots.Length);
                        foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            effect298Callbacks.Add(task.Callback);
                            if (poolIndex == 3 && task.Callback == 0x671AB0)
                            {
                                uint address = task.ReadDword(24);
                                int size = address == NativeBattleEffect298Profile.ProgrammeAAddress ? 80 : address == NativeBattleEffect298Profile.ProgrammeBAddress ? 304 : 272;
                                NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(12, size).ToArray().ToImmutableArray());
                                Assert.Equal(address, workspace.ResourceAddress);
                                Assert.InRange(workspace.Frame, 1, address == NativeBattleEffect298Profile.ProgrammeAAddress || address == NativeBattleEffect298Profile.ProgrammeBAddress ? 22 : 18);
                                if (effect298Programmes.Add(address))
                                {
                                    ColdNext("effect298-programme-" + address.ToString("X"), current);
                                }
                            }
                            if (poolIndex == 4 && task.Callback == NativeBattleEffect298Profile.MainCallback)
                            {
                                int age = unchecked((short)task.ReadWord(12));
                                effect298VibrationHandle = unchecked((int)task.ReadDword(16));
                                if (age == 1)
                                {
                                    ushort channel = current.NativeMusicProcess!.ReturnChannel!.Value;
                                    uint selectedChannel = unchecked((uint)(short)channel);
                                    Assert.Equal(0, current.NativeGlobalSoundProcess!.MusicPlayback!.ReadGain(selectedChannel > 1 ? 0 : (int)selectedChannel));
                                    if (selectedChannel > 1)
                                    {
                                        Assert.Equal(0, current.NativeGlobalSoundProcess.MusicPlayback.ReadGain(1));
                                    }
                                    Assert.True(current.NativeVibrationProcess!.IsPlaying(effect298VibrationHandle.Value));
                                }
                                for (int row = 0; row < 45; row++)
                                {
                                    Assert.All(task.ReadBytes(28 + row * 16, 16), value => Assert.Equal((byte)0, value));
                                }
                                effect298OffsetsVerified = true;
                                effect298ChunksSeen |= Enumerable.Range(0, 192).Any(row => task.ReadWord(0x2EC + row * 172 + 6) != 0);
                                effect298SmokeSeen |= Enumerable.Range(0, 128).Any(row => task.ReadWord(0x83EC + row * 12 + 6) != 0);
                                if (age >= 112)
                                {
                                    Assert.False(current.NativeVibrationProcess!.IsPlaying(effect298VibrationHandle.Value));
                                }
                                if (age is 1 or 2 or 50 or 90 or 91 or 111 or 112 or 114 or 115)
                                {
                                    string marker = "effect298-main-" + age;
                                    if (effect298Markers.Add(marker))
                                    {
                                        ColdNext(marker, current);
                                    }
                                }
                            }
                        }
                    }
                    if (before?.Pools.Length == 5)
                    {
                        NativeBattleModulePoolTask oldMain = before.Pools[4].Slots[0];
                        int oldAge = unchecked((short)oldMain.ReadWord(12));
                        if ((oldMain.Flags & 1) != 0 && oldAge is >= 90 and < 115)
                        {
                            Assert.Equal(unchecked((short)(prior.NativeGraphics!.Models[caster].Transform!.Position28.Z - 128)), current.NativeGraphics!.Models[caster].Transform!.Position28.Z);
                        }
                    }
                    if (before?.Pools.Length == 5 && unchecked((short)before.Pools[0].Slots[0].ReadWord(12)) >= 117)
                    {
                        Assert.Equal(JsonSerializer.Serialize(before.Pools.Skip(1)), JsonSerializer.Serialize(after.Pools.Skip(1)));
                    }
                    ushort[] counters = NativeBattleEffect298Profile.ScriptCounterAddresses.Select(address => BinaryPrimitives.ReadUInt16LittleEndian(state.ReadStatic(resources, address, 2))).ToArray();
                    bool changed = NativeBattleEffect298Profile.ScriptCounterAddresses.Select((address, index) => counters[index] != BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(address, 2).AsSpan())).Any(value => value);
                    if (changed && !effect298CountersChanged)
                    {
                        effect298CountersChanged = true;
                        ColdNext("effect298-mutable-script-counters", current);
                    }
                    NativeBattleModulePoolTask root = after.Pools[0].Slots[0];
                    int rootAge = unchecked((short)root.ReadWord(12));
                    if (rootAge is 117 or 118 or 119)
                    {
                        string marker = "effect298-root-" + rootAge;
                        if (effect298Markers.Add(marker))
                        {
                            effect298Observations.Add(new { RootAge = rootAge, Counters = counters, CurrentSeed = state.ReadDword(NativeBattleEffect298Profile.SeedGlobal), Pools = after.Pools.Select(pool => new { pool.DescriptorAddress, Active = pool.Slots.Count(task => (task.Flags & 1) != 0) }), Io = current.NativeEffectMemory.EffectIo, Header = current.NativeEffectMemory.Arena!.Read(NativeBattleEffect298Profile.IoOffset, 16) });
                            ColdNext(marker, current);
                        }
                    }
                    if (current.NativeEffectMemory.EffectIo?.BaseOffset == NativeBattleEffect298Profile.IoOffset)
                    {
                        byte[] io = current.NativeEffectMemory.Arena!.Read(NativeBattleEffect298Profile.IoOffset, 16);
                        if (io[3] != 0)
                        {
                            Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(io.AsSpan(8)));
                            Assert.Equal((short)360, BinaryPrimitives.ReadInt16LittleEndian(io.AsSpan(10)));
                            Assert.Equal(NativeBattleEffect298Profile.ArenaAddress + NativeBattleEffect298Profile.FirstPacketOffset, BinaryPrimitives.ReadUInt32LittleEndian(io.AsSpan(12)));
                            if (!effect298IoQueued)
                            {
                                effect298IoQueued = true;
                                ColdNext("effect298-file360-queued", current);
                            }
                        }
                    }
                    uint cursor = state.ReadDword(NativeBattleEffect298Profile.CursorGlobal);
                    int bank = cursor >= NativeBattleEffect298Profile.ArenaAddress + NativeBattleEffect298Profile.SecondPacketOffset ? 1 : 0;
                    if (effect298Banks.Add(bank))
                    {
                        ColdNext("effect298-packet-bank-" + bank, current);
                    }
                    ImmutableHashSet<NativeBattlePrimitiveReference> prefix = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
                    if (!prefix.IsEmpty && effect298Domains.Add("ordinary-COMMON-before-complete-first-model"))
                    {
                        ColdNext("effect298-current-custom-body-common-prefix", current);
                    }
                    if (Effect298Commands(current).Length != 0)
                    {
                        effect298Domains.Add("effect-FX");
                    }
                }
                if (effectId == 298 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.NotNull(current.NativeEffectMemory!.Effect298!.RootBankStorage);
                    ColdNext("effect298-io-complete-child-banks-retained", current);
                }
                if (effectId == 279 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleMegaSpark279State spark = current.NativeEffectMemory!.MegaSpark279!;
                    Assert.Null(spark.RootBankStorage);
                    Assert.Equal(NativeBattleMegaSpark279Profile.ArenaAddress, spark.ReadDword(NativeBattleMegaSpark279Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, spark.ReadDword(NativeBattleMegaSpark279Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 7);
                    foreach (NativeBattleEffectTaskPool pool in after.Pools)
                    {
                        NativeBattleCureFamilyPool expectedPool = Assert.Single(NativeBattleMegaSpark279Profile.Pools, expected => expected.Descriptor == pool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, pool.BankAddress);
                        Assert.Equal(expectedPool.Stride, pool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, pool.Slots.Length);
                        megaSpark279Callbacks.UnionWith(pool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (after.Pools.Length == 7)
                    {
                        NativeBattleModulePoolTask main = after.Pools[6].Slots[0];
                        if ((main.Flags & 1) != 0)
                        {
                            Assert.Equal((ushort)1, main.ReadWord(14));
                            Assert.Equal(0x1D972C0u + contactActor * 156u, main.ReadDword(20));
                            int age = unchecked((short)main.ReadWord(12));
                            string marker = "mega-spark-main-" + age;
                            if (age is 1 or 15 or 16 or 24 or 25 or 31 or 40 or 41 or 55 or 58 or 59 or 60 or 70 or 84 && !restoredBoundaries.Contains(marker))
                            {
                                ColdNext(marker, current);
                            }
                            if (age >= 16)
                            {
                                int expectedFrame = Math.Min(age - 15, 16);
                                for (int index = 0; index < 3; index++)
                                {
                                    NativeBattleGfAnimationWorkspace workspace = new(main.Payload.AsSpan().Slice(0x75C + index * 328 - 12, 328).ToArray().ToImmutableArray());
                                    Assert.Equal(NativeBattleMegaSpark279Profile.ProgrammeBAddress, workspace.ResourceAddress);
                                    Assert.Equal(expectedFrame, workspace.Frame);
                                    megaSpark279BFrames.Add(workspace.Frame);
                                }
                            }
                            if (age >= 25)
                            {
                                NativeBattleGfAnimationWorkspace workspace = new(main.Payload.AsSpan().Slice(40 - 12, NativeBattleMegaSpark279Profile.ProgrammeAWorkspaceLength).ToArray().ToImmutableArray());
                                Assert.Equal(NativeBattleMegaSpark279Profile.ProgrammeAAddress, workspace.ResourceAddress);
                                Assert.Equal(Math.Min(age - 24, 36), workspace.Frame);
                                megaSpark279AFrames.Add(workspace.Frame);
                            }
                            if (age == 1)
                            {
                                for (int row = 0; row < NativeBattleMegaSpark279Profile.LinkedRowCapacity; row++)
                                {
                                    Assert.Equal((ushort)65535, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(NativeBattleMegaSpark279Profile.LinkedRowsOffset + row * NativeBattleMegaSpark279Profile.LinkedRowStride + 6, 2)));
                                }
                                Assert.Equal(new byte[4], current.NativeEffectMemory.Arena!.Read(NativeBattleMegaSpark279Profile.LinkedHintOffset, 4));
                                for (int index = 0; index < 8; index++)
                                {
                                    Assert.Equal(unchecked((short)NativeBattleCameraMath.Sine((index << 11) / 7)), BinaryPrimitives.ReadInt16LittleEndian(current.NativeEffectMemory.Arena.Read(NativeBattleMegaSpark279Profile.GeneratedSineOffset + index * 2, 2)));
                                }
                            }
                        }
                        foreach ((int index, string name) in new[] { (4, "rows"), (5, "attachments"), (2, "small-programmes"), (3, "large-programmes"), (1, "sprites") })
                        {
                            if (after.Pools[index].Slots.Any(task => (task.Flags & 1) != 0) && !restoredBoundaries.Contains("mega-spark-live-" + name))
                            {
                                ColdNext("mega-spark-live-" + name, current);
                            }
                        }
                        megaSpark279RowsSeen |= after.Pools[4].Slots.Any(task => (task.Flags & 1) != 0);
                        megaSpark279AttachmentsSeen |= after.Pools[5].Slots.Any(task => (task.Flags & 1) != 0);
                        megaSpark279ProgrammesSeen |= after.Pools[2].Slots.Concat(after.Pools[3].Slots).Any(task => (task.Flags & 1) != 0);
                        megaSpark279SpritesSeen |= after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0);
                        uint cursor = spark.ReadDword(NativeBattleMegaSpark279Profile.CursorGlobal);
                        int bank = cursor >= NativeBattleMegaSpark279Profile.ArenaAddress + NativeBattleMegaSpark279Profile.SecondPacketOffset ? 1 : 0;
                        if (megaSpark279Banks.Add(bank))
                        {
                            ColdNext("mega-spark-effect-packet-bank-" + bank, current);
                        }
                        foreach (NativeBattleLogicalCommand command in MegaSpark279Commands(current))
                        {
                            if (IsMegaSpark279Strip(command))
                            {
                                AssertMegaSpark279Strip(current, command);
                                string domain = command.Packet.Bank == "20DFAB8" ? "effect" : "ordinary-COMMON";
                                if (megaSpark279PacketDomains.Add(domain))
                                {
                                    ColdNext("mega-spark-current-strip-" + domain, current);
                                }
                            }
                        }
                    }
                }
                if (effectId == 279 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.MegaSpark279!.RootBankStorage);
                    foreach (int index in new[] { 6, 5 })
                    {
                        NativeBattleCureFamilyPool pool = NativeBattleMegaSpark279Profile.Pools[index];
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            int offset = checked((int)(pool.Bank - NativeBattleMegaSpark279Profile.ArenaAddress)) + slot * pool.Stride;
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(offset, 2)) & 1);
                        }
                    }
                    Assert.Equal(0, current.NativeGraphics!.Models[caster].Flags0 & 0x1000);
                    ColdNext("mega-spark-main-attachments-retired-other-banks-retained", current);
                }
                if (effectId == 304 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleBoomerangSword304State sword = current.NativeEffectMemory!.BoomerangSword304!;
                    Assert.Null(sword.RootBankStorage);
                    Assert.Equal(NativeBattleBoomerangSword304Profile.ArenaAddress, sword.ReadDword(NativeBattleBoomerangSword304Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, sword.ReadDword(NativeBattleBoomerangSword304Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 5);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expectedPool = NativeBattleBoomerangSword304Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        boomerang304Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (after.Pools.Length == 5)
                    {
                        NativeBattleModulePoolTask main = after.Pools[1].Slots[0];
                        if ((main.Flags & 1) != 0)
                        {
                            Assert.Equal((ushort)1, main.ReadWord(14));
                            Assert.Equal(0x1D972C0u + contactActor * 156u, main.ReadDword(32));
                            Assert.Equal(unchecked((ushort)source.NativeGraphics!.Models[contactActor].Transform!.Angle36), main.ReadWord(50));
                            int age = unchecked((short)main.ReadWord(12));
                            if (age is 1 or 4 or 5 or 6 or 24 or 25 or 26 or 30 or 35 or 39 or 40 or 41 or 50 or 58 && boomerang304Markers.Add("main-" + age))
                            {
                                ColdNext("boomerang-main-age-" + age, current);
                            }
                            if (age >= 6)
                            {
                                Assert.Equal(0u, current.NativeGraphics!.Models[caster].SequenceState!.MeshMask124 & 2);
                            }
                        }
                        if (!boomerang304TrailSeen && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0))
                        {
                            ColdNext("boomerang-native-eight-frame-static-trail", current);
                            boomerang304TrailSeen = true;
                        }
                        if (!boomerang304ParticlesSeen && after.Pools[4].Slots.Any(task => (task.Flags & 1) != 0))
                        {
                            ColdNext("boomerang-kind-eight-particles-old-phase-W", current);
                            boomerang304ParticlesSeen = true;
                        }
                        foreach (NativeBattleModulePoolTask programme in after.Pools[3].Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            for (int payloadOffset = 24; payloadOffset < 484; payloadOffset += 2)
                            {
                                _ = programme.ReadWord(payloadOffset);
                            }
                            NativeBattleGfAnimationWorkspace workspace = new(programme.Payload.AsSpan().Slice(12, 460).ToArray().ToImmutableArray());
                            Assert.Equal(NativeBattleBoomerangSword304Profile.ProgrammeAddress, workspace.ResourceAddress);
                            if (!boomerang304ProgrammeSeen && workspace.Frame > 0)
                            {
                                ColdNext("boomerang-current-anchor16-programme", current);
                                boomerang304ProgrammeSeen = true;
                            }
                            _ = workspace.Advance701970(original.BoomerangSword304Resources!.Programme, 0, (retained, node) =>
                            {
                                if (!boomerang304MorphSeen && node.Word40 != node.Word42 && node.Scalar38 is > 0 and < 4096 && current.NativeEffectMemory.Arena!.ReadSnapshot(NativeBattleBoomerangSword304Profile.ScratchOffset, 6).Known.All(known => known))
                                {
                                    ColdNext("boomerang-current-408-byte-morph-owner", current);
                                    boomerang304MorphSeen = true;
                                }
                                return retained;
                            });
                        }
                    }
                    uint cursor = sword.ReadDword(NativeBattleBoomerangSword304Profile.CursorGlobal);
                    int bank = cursor >= NativeBattleBoomerangSword304Profile.ArenaAddress + NativeBattleBoomerangSword304Profile.SecondPacketOffset ? 1 : 0;
                    if (boomerang304Banks.Add(bank))
                    {
                        ColdNext("boomerang-packet-bank-" + bank, current);
                    }
                }
                if (effectId == 304 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.BoomerangSword304!.RootBankStorage);
                    foreach (NativeBattleCureFamilyPool pool in NativeBattleBoomerangSword304Profile.Pools.Skip(1).Take(3))
                    {
                        int start = checked((int)(pool.Bank - NativeBattleBoomerangSword304Profile.ArenaAddress));
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(start + slot * pool.Stride, 2)) & 1);
                        }
                    }
                    ColdNext("boomerang-main-trail-programme-retired-particles-retained", current);
                }
                if (effectId == 302 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleSnipeLaser302State laser = current.NativeEffectMemory!.SnipeLaser302!;
                    Assert.Null(laser.RootBankStorage);
                    Assert.Equal(NativeBattleSnipeLaser302Profile.ArenaAddress, laser.ReadDword(NativeBattleSnipeLaser302Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, laser.ReadDword(NativeBattleSnipeLaser302Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 5);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expectedPool = NativeBattleSnipeLaser302Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        snipe302Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (after.Pools.Length == 5)
                    {
                        NativeBattleModulePoolTask[] mains = after.Pools[4].Slots.ToArray();
                        if (!snipe302ClonesSeen && mains.All(task => (task.Flags & 1) != 0))
                        {
                            Assert.Equal(new ushort[] { 0, 1 }, mains.Select(task => task.ReadWord(14)).ToArray());
                            Assert.Equal((ushort)1, mains[0].ReadWord(12));
                            Assert.Equal(unchecked((ushort)-1), mains[1].ReadWord(12));
                            Assert.All(mains, task => Assert.Equal(0x1D972C0u + contactActor * 156u, task.ReadDword(16)));
                            Assert.Equal(mains[0].ReadBytes(20, 8), mains[1].ReadBytes(20, 8));
                            Assert.Equal((byte)2, after.Pools[0].Slots[0].ReadByte(14));
                            ColdNext("snipe-two-clones-zero-minus-two-and-captured-F1", current);
                            snipe302ClonesSeen = true;
                        }
                        for (int clone = 0; clone < mains.Length; clone++)
                        {
                            NativeBattleModulePoolTask main = mains[clone];
                            if ((main.Flags & 1) == 0)
                            {
                                continue;
                            }
                            for (int payloadOffset = 28; payloadOffset < 972; payloadOffset += 2)
                            {
                                _ = main.ReadWord(payloadOffset);
                            }
                            byte[] initializedProgrammeA = main.Payload.AsSpan().Slice(16, 944).ToArray();
                            Assert.Equal(NativeBattleSnipeLaser302Profile.ProgrammeAAddress, main.ReadDword(28));
                            Assert.Equal(0u, main.ReadDword(32));
                            if (snipe302InitializedA.TryGetValue(clone, out byte[]? retainedA))
                            {
                                Assert.Equal(retainedA, initializedProgrammeA);
                            }
                            else
                            {
                                snipe302InitializedA.Add(clone, initializedProgrammeA);
                            }
                            int age = unchecked((short)main.ReadWord(12));
                            if (age is -1 or 0 or 1 or 5 or 6 or 15 or 16 or 33 or 34 or 36 or 37 or 38 or 43 or 44 or 49 or 50 && snipe302Markers.Add("clone-" + clone + "-" + age))
                            {
                                ColdNext("snipe-clone-" + clone + "-age-" + age, current);
                            }
                            InspectSnipeProgramme(main, 972, 232, NativeBattleSnipeLaser302Profile.ProgrammeBAddress);
                            InspectSnipeProgramme(main, 1204, 432, NativeBattleSnipeLaser302Profile.ProgrammeCAddress);
                        }
                        foreach (NativeBattleModulePoolTask programme in after.Pools[1].Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            InspectSnipeProgramme(programme, 44, 124, NativeBattleSnipeLaser302Profile.ProgrammeDAddress);
                        }
                        if (!snipe302ParticlesSeen && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0))
                        {
                            Assert.All(after.Pools[2].Slots.Where(task => (task.Flags & 1) != 0), task => Assert.InRange(task.ReadWord(18), (ushort)1, (ushort)15));
                            ColdNext("snipe-particle-phase-W-and-signed-acceleration", current);
                            snipe302ParticlesSeen = true;
                        }
                        if (!snipe302SpritesSeen && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0))
                        {
                            ColdNext("snipe-ground-sprite-current-point", current);
                            snipe302SpritesSeen = true;
                        }
                        if (!snipe302BeamWindowSeen && before?.Pools.Length == 5 && before.Pools[4].Slots.Any(task => (task.Flags & 1) != 0 && unchecked((short)task.ReadWord(12)) is >= 15 and <= 44) && SnipeLaser302Commands(current).Length != 0)
                        {
                            Assert.Equal(NativeBattleSnipeLaser302Profile.StaticMeshAddress, original.SnipeLaser302Resources!.StaticMesh.Address);
                            ColdNext("snipe-direct-572200-beam-window-current-packets", current);
                            snipe302BeamWindowSeen = true;
                        }
                    }
                    uint cursor = laser.ReadDword(NativeBattleSnipeLaser302Profile.CursorGlobal);
                    int bank = cursor >= NativeBattleSnipeLaser302Profile.ArenaAddress + NativeBattleSnipeLaser302Profile.SecondPacketOffset ? 1 : 0;
                    if (snipe302Banks.Add(bank))
                    {
                        ColdNext("snipe-packet-bank-" + bank, current);
                    }

                    void InspectSnipeProgramme(NativeBattleModulePoolTask task, int offset, int length, uint address)
                    {
                        for (int payloadOffset = offset; payloadOffset < offset + length; payloadOffset += 2)
                        {
                            _ = task.ReadWord(payloadOffset);
                        }
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(offset - 12, length).ToArray().ToImmutableArray());
                        Assert.Equal(address, workspace.ResourceAddress);
                        if (workspace.Frame == 0)
                        {
                            return;
                        }
                        if (snipe302Programmes.Add(address))
                        {
                            ColdNext("snipe-advanced-programme-" + address.ToString("X"), current);
                        }
                        _ = workspace.Advance701970(original.SnipeLaser302Resources!.ReadProgramme(address), 0, (retained, node) =>
                        {
                            if (!snipe302MorphSeen && node.Word40 != node.Word42 && node.Scalar38 is > 0 and < 4096 && node.Vector24 != new NativeBattleVector(0, 0, 0) && current.NativeEffectMemory.Arena!.ReadSnapshot(NativeBattleSnipeLaser302Profile.ScratchOffset, 6).Known.All(known => known))
                            {
                                ColdNext("snipe-current-392-byte-morph-owner", current);
                                snipe302MorphSeen = true;
                            }
                            return retained;
                        });
                    }
                }
                if (effectId == 302 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.SnipeLaser302!.RootBankStorage);
                    foreach (NativeBattleCureFamilyPool pool in NativeBattleSnipeLaser302Profile.Pools.Skip(1))
                    {
                        int start = checked((int)(pool.Bank - NativeBattleSnipeLaser302Profile.ArenaAddress));
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(start + slot * pool.Stride, 2)) & 1);
                        }
                    }
                    ColdNext("snipe-all-four-banks-retired", current);
                }
                if (effectId == 284 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleEnemyMicroMissiles284State missiles = current.NativeEffectMemory!.EnemyMicroMissiles284!;
                    Assert.Null(missiles.RootBankStorage);
                    Assert.Equal(NativeBattleEnemyMicroMissiles284Profile.ArenaAddress, missiles.ReadDword(NativeBattleEnemyMicroMissiles284Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, missiles.ReadDword(NativeBattleEnemyMicroMissiles284Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 4);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expectedPool = NativeBattleEnemyMicroMissiles284Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        enemyMicro284Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (after.Pools.Length == 4)
                    {
                        NativeBattleModulePoolTask root = after.Pools[0].Slots[0];
                        int age = root.ReadWord(12);
                        if (age is 2 or 3 or 10 or 20 or 30 or 40 or 50 && enemyMicro284Markers.Add("root-" + age))
                        {
                            ColdNext("enemy-micro-root-" + age, current);
                        }
                        NativeBattleModulePoolTask[] activeMissiles = after.Pools[3].Slots.Where(task => (task.Flags & 1) != 0).ToArray();
                        if (!enemyMicro284SixBorn && activeMissiles.Length == 6)
                        {
                            Assert.Equal(new ushort[] { 0, 0, 0, 0, 1, 1 }, activeMissiles.Select(task => task.ReadWord(34)).Order().ToArray());
                            ColdNext("enemy-micro-six-simultaneous-births", current);
                            enemyMicro284SixBorn = true;
                        }
                        foreach (NativeBattleModulePoolTask task in activeMissiles)
                        {
                            ushort variant = task.ReadWord(34);
                            if (variant <= 1)
                            {
                                enemyMicro284Variants.Add(variant);
                            }
                            InspectProgramme(task, 52, 2020, NativeBattleEnemyMicroMissiles284Profile.MissileAddress);
                        }
                        foreach (NativeBattleModulePoolTask task in after.Pools[2].Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            uint address = task.ReadDword(48);
                            Assert.Contains(address, new[] { NativeBattleEnemyMicroMissiles284Profile.ImpactAddress, NativeBattleEnemyMicroMissiles284Profile.WideImpactAddress });
                            InspectProgramme(task, 48, address == NativeBattleEnemyMicroMissiles284Profile.ImpactAddress ? 160 : 276, address);
                        }
                        if (!enemyMicro284TrailsSeen && activeMissiles.Any(task => task.ReadWord(42) >= 1224))
                        {
                            ColdNext("enemy-micro-spiral-trail-window", current);
                            enemyMicro284TrailsSeen = true;
                        }
                        if (!enemyMicro284ParticlesSeen && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0))
                        {
                            ColdNext("enemy-micro-particle-old-phase-W", current);
                            enemyMicro284ParticlesSeen = true;
                        }
                        if (!enemyMicro284ReactionSeen && current.NativeGraphics!.Models[contactActor].Sequence == 4)
                        {
                            ColdNext("enemy-micro-native-recipient-reaction-four", current);
                            enemyMicro284ReactionSeen = true;
                        }
                        if (root.ReadByte(15) == 2 && enemyMicro284Markers.Add("contact-latched"))
                        {
                            Assert.Null(after.Pools[3].Head);
                            ColdNext("enemy-micro-single-contact-latched", current);
                        }
                    }
                    uint cursor = missiles.ReadDword(NativeBattleEnemyMicroMissiles284Profile.CursorGlobal);
                    int bank = cursor >= NativeBattleEnemyMicroMissiles284Profile.ArenaAddress + NativeBattleEnemyMicroMissiles284Profile.SecondPacketOffset ? 1 : 0;
                    if (enemyMicro284Banks.Add(bank))
                    {
                        ColdNext("enemy-micro-packet-bank-" + bank, current);
                    }

                    void InspectProgramme(NativeBattleModulePoolTask task, int offset, int length, uint address)
                    {
                        for (int payloadOffset = offset; payloadOffset < offset + length; payloadOffset += 2)
                        {
                            _ = task.ReadWord(payloadOffset);
                        }
                        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(offset - 12, length).ToArray().ToImmutableArray());
                        Assert.Equal(address, workspace.ResourceAddress);
                        if (enemyMicro284Programmes.Add(address))
                        {
                            ColdNext("enemy-micro-current-programme-" + address.ToString("X"), current);
                        }
                        _ = workspace.Advance701970(original.EnemyMicroMissiles284Resources!.ReadProgramme(address), 0, (retained, node) =>
                        {
                            if (!enemyMicro284MorphSeen && node.Word40 != node.Word42 && node.Scalar38 is > 0 and < 4096 && node.Vector24 != new NativeBattleVector(0, 0, 0))
                            {
                                Assert.All(current.NativeEffectMemory.Arena!.ReadSnapshot(NativeBattleEnemyMicroMissiles284Profile.ScratchOffset, 6).Known, known => Assert.True(known));
                                ColdNext("enemy-micro-current-morph-scratch", current);
                                enemyMicro284MorphSeen = true;
                            }
                            return retained;
                        });
                    }
                }
                if (effectId == 284 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.EnemyMicroMissiles284!.RootBankStorage);
                    foreach (NativeBattleCureFamilyPool pool in NativeBattleEnemyMicroMissiles284Profile.Pools.Skip(1))
                    {
                        int start = checked((int)(pool.Bank - NativeBattleEnemyMicroMissiles284Profile.ArenaAddress));
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(start + slot * pool.Stride, 2)) & 1);
                        }
                    }
                    ColdNext("enemy-micro-all-three-banks-retired", current);
                }
                if (effectId == 286 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleMiniPulseCannon286State cannon = current.NativeEffectMemory!.MiniPulseCannon286!;
                    Assert.Null(cannon.RootBankStorage);
                    Assert.Equal(NativeBattleMiniPulseCannon286Profile.ArenaAddress, cannon.ReadDword(NativeBattleMiniPulseCannon286Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, cannon.ReadDword(NativeBattleMiniPulseCannon286Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 3);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expectedPool = NativeBattleMiniPulseCannon286Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        miniPulse286Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (after.Pools.Length == 3)
                    {
                        NativeBattleModulePoolTask main = after.Pools[2].Slots[0];
                        int age = main.ReadWord(12);
                        if (age is 1 or 24 or 25 or 26 or 33 or 49 or 50 or 53 or 54 or 67 or 68 && miniPulse286Markers.Add("main-" + age))
                        {
                            ColdNext("mini-pulse-main-" + age, current);
                        }
                        if ((main.Flags & 1) != 0)
                        {
                            foreach ((int offset, int length, uint address) in new[] { (28, 556, NativeBattleMiniPulseCannon286Profile.ProgrammeAAddress), (584, 344, NativeBattleMiniPulseCannon286Profile.ProgrammeBAddress), (928, 676, NativeBattleMiniPulseCannon286Profile.ProgrammeCAddress), (1604, 144, NativeBattleMiniPulseCannon286Profile.ProgrammeDAddress) })
                            {
                                for (int payloadOffset = offset; payloadOffset < offset + length; payloadOffset += 2)
                                {
                                    _ = main.ReadWord(payloadOffset);
                                }
                                NativeBattleGfAnimationWorkspace workspace = new(main.Payload.AsSpan().Slice(offset - 12, length).ToArray().ToImmutableArray());
                                Assert.Equal(address, workspace.ResourceAddress);
                                if (miniPulse286Programmes.Add(address))
                                {
                                    ColdNext("mini-pulse-current-programme-" + address.ToString("X"), current);
                                }
                                _ = workspace.Advance701970(original.MiniPulseCannon286Resources!.ReadProgramme(address), 0, (retained, node) =>
                                {
                                    if (!miniPulse286MorphSeen && node.Word40 != node.Word42 && node.Scalar38 is > 0 and < 4096 && node.Vector24 != new NativeBattleVector(0, 0, 0))
                                    {
                                        NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
                                        Assert.All(arena.ReadSnapshot(NativeBattleMiniPulseCannon286Profile.ScratchOffset, 6).Known, known => Assert.True(known));
                                        ColdNext("mini-pulse-current-morph-scratch", current);
                                        miniPulse286MorphSeen = true;
                                    }
                                    return retained;
                                });
                            }
                        }
                        if (!miniPulse286SpriteSeen && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0))
                        {
                            Assert.All(after.Pools[1].Slots.Where(task => (task.Flags & 1) != 0), task =>
                            {
                                Assert.Equal(NativeBattleMiniPulseCannon286Profile.SpriteAddress, task.ReadDword(12));
                                Assert.InRange(task.ReadWord(26), (ushort)1, (ushort)9);
                            });
                            ColdNext("mini-pulse-kind9-sprite-old-phase-W", current);
                            miniPulse286SpriteSeen = true;
                        }
                    }
                    uint cursor = cannon.ReadDword(NativeBattleMiniPulseCannon286Profile.CursorGlobal);
                    int bank = cursor >= NativeBattleMiniPulseCannon286Profile.ArenaAddress + NativeBattleMiniPulseCannon286Profile.SecondPacketOffset ? 1 : 0;
                    if (miniPulse286Banks.Add(bank))
                    {
                        ColdNext("mini-pulse-packet-bank-" + bank, current);
                    }
                    if (!miniPulse286WindowSeen && MiniPulseCannon286Commands(current).Any(command => command.Opcode == 0xE2))
                    {
                        ColdNext("mini-pulse-native-texture-window", current);
                        miniPulse286WindowSeen = true;
                    }
                }
                if (effectId == 286 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.MiniPulseCannon286!.RootBankStorage);
                    Assert.Equal(0, current.NativeGraphics!.Models[caster].Flags0 & 8);
                    foreach (NativeBattleCureFamilyPool pool in NativeBattleMiniPulseCannon286Profile.Pools.Skip(1))
                    {
                        int start = checked((int)(pool.Bank - NativeBattleMiniPulseCannon286Profile.ArenaAddress));
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(start + slot * pool.Stride, 2)) & 1);
                        }
                    }
                    ColdNext("mini-pulse-main-and-sprites-retired", current);
                }
                if (effectId == 282 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleEvilEye282State eye = current.NativeEffectMemory!.EvilEye282!;
                    Assert.Null(eye.RootBankStorage);
                    Assert.Equal(NativeBattleEvilEye282Profile.ArenaAddress, eye.ReadDword(NativeBattleEvilEye282Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, eye.ReadDword(NativeBattleEvilEye282Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 4);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expectedPool = NativeBattleEvilEye282Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        evilEye282Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (after.Pools.Length == 4)
                    {
                        int mainAge = after.Pools[3].Slots[0].ReadWord(12);
                        if (mainAge is 1 or 2 or 3 or 5 or 6 or 10 or 11 or 42 or 48 && evilEye282Markers.Add("main-" + mainAge))
                        {
                            ColdNext("evil-eye-main-" + mainAge, current);
                        }
                        foreach (int programmePool in new[] { 3, 2 })
                        {
                            int workspaceOffset = programmePool == 3 ? 28 : 24;
                            int workspaceLength = programmePool == 3 ? NativeBattleEvilEye282Profile.ProgrammeAWorkspaceLength : NativeBattleEvilEye282Profile.ProgrammeBWorkspaceLength;
                            uint sourceAddress = programmePool == 3 ? NativeBattleEvilEye282Profile.ProgrammeAAddress : NativeBattleEvilEye282Profile.ProgrammeBAddress;
                            foreach (NativeBattleModulePoolTask task in after.Pools[programmePool].Slots.Where(task => (task.Flags & 1) != 0))
                            {
                                for (int offset = workspaceOffset; offset < workspaceOffset + workspaceLength; offset += 2)
                                {
                                    _ = task.ReadWord(offset);
                                }
                                NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(workspaceOffset - 12, workspaceLength).ToArray().ToImmutableArray());
                                Assert.Equal(sourceAddress, workspace.ResourceAddress);
                                _ = workspace.Advance701970(original.EvilEye282Resources!.ReadProgramme(sourceAddress), 0, (retained, node) =>
                                {
                                    if (!evilEye282MorphSeen && node.Word40 != node.Word42 && node.Scalar38 is > 0 and < 4096)
                                    {
                                        NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
                                        Assert.All(arena.ReadSnapshot(NativeBattleEvilEye282Profile.ScratchOffset, 6).Known, known => Assert.True(known));
                                        ColdNext("evil-eye-current-morph-scratch", current);
                                        evilEye282MorphSeen = true;
                                    }
                                    return retained;
                                });
                                if (evilEye282Programmes.Add(sourceAddress))
                                {
                                    ColdNext("evil-eye-current-programme-" + sourceAddress.ToString("X"), current);
                                }
                            }
                        }
                        if (!evilEye282SpriteSeen && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0))
                        {
                            Assert.All(after.Pools[1].Slots.Where(task => (task.Flags & 1) != 0), task => Assert.InRange(task.ReadWord(18), (ushort)1, (ushort)15));
                            ColdNext("evil-eye-current-bone-particles-phase-W", current);
                            evilEye282SpriteSeen = true;
                        }

                    }
                    uint cursor = eye.ReadDword(NativeBattleEvilEye282Profile.CursorGlobal);
                    int bank = cursor >= NativeBattleEvilEye282Profile.ArenaAddress + NativeBattleEvilEye282Profile.SecondPacketOffset ? 1 : 0;
                    if (evilEye282Banks.Add(bank))
                    {
                        ColdNext("evil-eye-packet-bank-" + bank, current);
                    }
                    if (!evilEye282FlashSeen && current.NativeGraphics!.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300))
                    {
                        ColdNext("evil-eye-native-flash", current);
                        evilEye282FlashSeen = true;
                    }
                }
                if (effectId == 282 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.EvilEye282!.RootBankStorage);
                    foreach (NativeBattleCureFamilyPool pool in NativeBattleEvilEye282Profile.Pools.Skip(1))
                    {
                        int start = checked((int)(pool.Bank - NativeBattleEvilEye282Profile.ArenaAddress));
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(start + slot * pool.Stride, 2)) & 1);
                        }
                    }
                    ColdNext("evil-eye-all-three-banks-retired", current);
                }
                if (effectId == 281 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleShotgun281State shotgun = current.NativeEffectMemory!.Shotgun281!;
                    NativeBattleShotgun281Resources shotgunResources = original.Shotgun281Resources!;
                    Assert.Null(shotgun.RootBankStorage);
                    Assert.Equal(NativeBattleShotgun281Profile.ArenaAddress, shotgun.ReadDword(NativeBattleShotgun281Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, shotgun.ReadDword(NativeBattleShotgun281Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 4);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        var expectedPool = NativeBattleShotgun281Profile.Pools[poolIndex];
                        var actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        shotgun281Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (after.Pools.Length == 4)
                    {
                        NativeBattleModulePoolTask main = after.Pools[3].Slots[0];
                        int mainAge = main.ReadWord(12);
                        foreach (NativeBattleModulePoolTask particle in after.Pools[1].Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            uint sprite = particle.ReadDword(12);
                            Assert.Contains(sprite, new[] { NativeBattleShotgun281Profile.SpriteAAddress, NativeBattleShotgun281Profile.SpriteBAddress });
                            Assert.InRange(particle.ReadWord(30), (ushort)1, (ushort)(sprite == NativeBattleShotgun281Profile.SpriteAAddress ? 8 : 10));
                            if (shotgun281Sprites.Add(sprite))
                            {
                                ColdNext("shotgun-kind8-sprite-" + sprite.ToString("X"), current);
                            }
                        }
                        foreach (int programmePool in new[] { 3, 2 })
                        {
                            foreach (NativeBattleModulePoolTask task in after.Pools[programmePool].Slots.Where(task => (task.Flags & 1) != 0))
                            {
                                int length = programmePool == 3 ? 524 : 312;
                                for (int payloadOffset = 20; payloadOffset < 20 + length; payloadOffset += 2)
                                {
                                    _ = task.ReadWord(payloadOffset);
                                }
                                var workspace = new NativeBattleGfAnimationWorkspace(task.Payload.AsSpan().Slice(8, length).ToArray().ToImmutableArray());
                                uint sourceAddress = workspace.ResourceAddress;
                                Assert.Equal(programmePool == 3 ? NativeBattleShotgun281Profile.ProgrammeAAddress : NativeBattleShotgun281Profile.ProgrammeBAddress, sourceAddress);
                                if (shotgun281Programmes.Add(sourceAddress))
                                {
                                    ColdNext("shotgun-current-programme-" + sourceAddress.ToString("X"), current);
                                }
                                _ = workspace.Advance701970(shotgunResources.ReadProgramme(sourceAddress), 0, (retained, node) =>
                                {
                                    shotgun281Ratios.Add(node.Scalar38);
                                    shotgun281Headers.Add(node.Header);
                                    return retained;
                                });
                            }
                        }
                        if (mainAge is 1 or 2 or 3 or 4 or 12 or 13 or 14 && shotgun281Markers.Add("main-" + mainAge))
                        {
                            ColdNext("shotgun-before-main-" + mainAge, current);
                        }
                        if (before?.Pools.Length == 4)
                        {
                            NativeBattleGraphicsModel priorRecipient = prior.NativeGraphics!.Models[contactActor];
                            NativeBattlePoseSource recipientPose = assets.ReadNativeGraphicsResource(BattleSession.Restore(prior).GetCombatantDefinition(prior.NativeGraphics.ActorIds[contactActor]!)).PoseSource!;
                            ImmutableArray<NativeBattleActionMatrix> recipientBones = NativeBattleBodyBoneMatrices.Require(priorRecipient, recipientPose);
                            Assert.Equal(23, recipientBones.Length);
                            NativeBattleActionMatrix recipientWorld = priorRecipient.GeometryWorldMatrix!;
                            for (int slot = 0; slot < after.Pools[2].Slots.Length; slot++)
                            {
                                NativeBattleModulePoolTask impact = after.Pools[2].Slots[slot];
                                if ((impact.Flags & 1) == 0 || (before.Pools[2].Slots[slot].Flags & 1) != 0)
                                {
                                    continue;
                                }
                                short[] actualPoint = [unchecked((short)impact.ReadWord(12)), unchecked((short)impact.ReadWord(14)), unchecked((short)impact.ReadWord(16))];
                                bool eligible = Enumerable.Range(0, recipientBones.Length).Where(bone => BinaryPrimitives.ReadInt16LittleEndian(recipientPose.Skeleton.AsSpan().Slice(16 + bone * 48)) >= 0).Any(bone =>
                                {
                                    NativeBattleActionMatrix currentBone = recipientBones[bone];
                                    (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(recipientWorld, currentBone.X, currentBone.Y, currentBone.Z);
                                    return actualPoint.SequenceEqual(new short[] { unchecked((short)(x + recipientWorld.X)), unchecked((short)(y + recipientWorld.Y)), unchecked((short)(z + recipientWorld.Z)) });
                                });
                                Assert.True(eligible, "The actual impact must use an eligible current recipient bone translation.");
                                Assert.Equal((ushort)0, impact.ReadWord(18));
                                if (!shotgun281RecipientSeen)
                                {
                                    ColdNext("shotgun-current-recipient-eligible-bone", current);
                                    shotgun281RecipientSeen = true;
                                }
                            }
                        }
                        NativeBattleGraphicsModel priorCaster = prior.NativeGraphics!.Models[caster];
                        NativeBattlePoseSource casterPose = assets.ReadNativeGraphicsResource(BattleSession.Restore(prior).GetCombatantDefinition(prior.NativeGraphics.ActorIds[caster]!)).PoseSource!;
                        NativeBattleGraphicsModel composedCaster = NativeBattleBodyBoneMatrices.Compose5095B0(priorCaster, priorCaster.GeometryWorldMatrix!);
                        NativeBattleActionMatrix muzzleBone = NativeBattleBodyBoneMatrices.Require(composedCaster, casterPose)[23];
                        if ((main.Flags & 1) != 0 || before?.Pools.Length == 4 && (before.Pools[3].Slots[0].Flags & 1) != 0)
                        {
                            var expectedMuzzle = NativeBattleGfMatrices.TransformMac460840(muzzleBone, new(-388, -2563, -1896));
                            Assert.Equal(unchecked((uint)expectedMuzzle.X), shotgun.ReadDword(NativeBattleShotgun281Profile.BasisAAddress + 20));
                            Assert.Equal(unchecked((uint)expectedMuzzle.Y), shotgun.ReadDword(NativeBattleShotgun281Profile.BasisAAddress + 24));
                            Assert.Equal(unchecked((uint)expectedMuzzle.Z), shotgun.ReadDword(NativeBattleShotgun281Profile.BasisAAddress + 28));
                            if (!shotgun281MuzzleSeen)
                            {
                                ColdNext("shotgun-current-composed-caster-bone23", current);
                                shotgun281Observations.Add(new { Boundary = "actual-composed-caster-bone23", Bone = muzzleBone, World = priorCaster.GeometryWorldMatrix, Expected = expectedMuzzle });
                                shotgun281MuzzleSeen = true;
                            }
                        }
                        if (!shotgun281MorphSeen && shotgun281Ratios.Any(ratio => ratio > 0 && ratio < 4096))
                        {
                            var scratch = current.NativeEffectMemory.Arena!.ReadSnapshot(NativeBattleShotgun281Profile.ScratchOffset, NativeBattleShotgun281Profile.ScratchLength);
                            var priorScratch = prior.NativeEffectMemory!.Arena!.ReadSnapshot(NativeBattleShotgun281Profile.ScratchOffset, NativeBattleShotgun281Profile.ScratchLength);
                            if (!scratch.Data.SequenceEqual(priorScratch.Data))
                            {
                                for (int vertex = 0; vertex < NativeBattleShotgun281Profile.ScratchLength / 8; vertex++)
                                {
                                    for (int lane = 6; lane < 8; lane++)
                                    {
                                        Assert.Equal(priorScratch.Known[vertex * 8 + lane], scratch.Known[vertex * 8 + lane]);
                                        if (scratch.Known[vertex * 8 + lane])
                                        {
                                            Assert.Equal(priorScratch.Data[vertex * 8 + lane], scratch.Data[vertex * 8 + lane]);
                                        }
                                    }
                                }
                                shotgun281Observations.Add(new { Boundary = "current-morph-scratch-retained-W", scratch.Data, scratch.Known });
                                ColdNext("shotgun-current-morph-scratch", current);
                                shotgun281MorphSeen = true;
                            }
                        }
                    }
                    NativeBattleLogicalCommand[] shotgunCommands = Shotgun281Commands(current);
                    foreach (NativeBattleLogicalCommand command in shotgunCommands)
                    {
                        int bank = command.Packet.ByteOffset >= NativeBattleShotgun281Profile.SecondPacketOffset ? 1 : 0;
                        if (shotgun281Banks.Add(bank))
                        {
                            ColdNext("shotgun-physical-bank-" + bank, current);
                        }
                        if (command.VertexPayload4.Length == 4 && command.VertexPayload4.Any(value => value != 0))
                        {
                            if (!shotgun281SzSeen)
                            {
                                ColdNext("shotgun-current-polygon-SZ", current);
                                shotgun281SzSeen = true;
                            }
                        }
                        for (int word = 0; word < command.Words.Length; word++)
                        {
                            uint mask = Additional182ConsumedMask(command.Opcode, word);
                            var physical = current.NativeEffectMemory.Arena!.ReadSnapshot(command.Packet.ByteOffset + (command.WordOffset + word) * 4, 4);
                            for (int part = 0; part < 4; part++)
                            {
                                byte byteMask = unchecked((byte)(mask >> (part * 8)));
                                if (byteMask != 0)
                                {
                                    Assert.True(physical.Known[part]);
                                    Assert.Equal(unchecked((byte)(command.Words[word] >> (part * 8))) & byteMask, physical.Data[part] & byteMask);
                                }
                            }
                        }
                    }
                    if (!shotgun281FlashSeen && current.NativeGraphics!.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300))
                    {
                        ColdNext("shotgun-native-flash", current);
                        shotgun281FlashSeen = true;
                    }
                }
                if (effectId == 281 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.Shotgun281!.RootBankStorage);
                    for (int slot = 0; slot < 128; slot++)
                    {
                        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(slot * 40, 2)) & 1);
                    }
                    Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(0x1BC8, 2)) & 1);
                    ColdNext("shotgun-root-main-particles-completion", current);
                }
                if (effectId == 214 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleGravija214State gravity = current.NativeEffectMemory!.Gravija214!;
                    NativeBattleGravija214Resources gravityResources = original.Gravija214Resources!;
                    Assert.Null(gravity.RootBankStorage);
                    Assert.Equal(NativeBattleGravija214Profile.ArenaAddress, gravity.ReadDword(NativeBattleGravija214Profile.ArenaGlobal));
                    Assert.True(after.Pools.Length is 1 or 3);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expectedPool = NativeBattleGravija214Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        gravija214Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    if (!gravija214EntryCopied && after.Pools.Length == 1)
                    {
                        Assert.Equal(gravityResources.EntryCopySeed.Data.ToArray(), gravity.Read(NativeBattleGravija214Profile.CopiedMeshAddress, NativeBattleGravija214Profile.EntryCopyLength));
                        ColdNext("gravija-original-entry-copy-before-preprocess", current);
                        gravija214EntryCopied = true;
                    }
                    if (after.Pools.Length == 3)
                    {
                        if (!gravija214Preprocessed)
                        {
                            byte[] copiedMesh = gravity.Read(NativeBattleGravija214Profile.CopiedMeshAddress, NativeBattleGravija214Profile.MeshLength);
                            Assert.False(copiedMesh.SequenceEqual(gravityResources.EntryCopySeed.Data.Take(NativeBattleGravija214Profile.MeshLength)));
                            Assert.Equal(gravityResources.EntryCopySeed.Data.AsSpan().Slice(NativeBattleGravija214Profile.MeshLength).ToArray(), gravity.Read(NativeBattleGravija214Profile.CopiedMeshAddress + NativeBattleGravija214Profile.MeshLength, NativeBattleGravija214Profile.EntryCopyLength - NativeBattleGravija214Profile.MeshLength));
                            Assert.Equal((ushort)0, gravity.ReadWord(NativeBattleGravija214Profile.MatrixAddress + 16));
                            Assert.NotNull(current.NativeEffectMemory!.GeometryRegisters!.Lzcs);
                            Assert.NotNull(current.NativeEffectMemory!.GeometryRegisters!.Lzcr);
                            byte[] renderedVram = current.NativeEffectMemory!.Textures!.ReadVram();
                            List<byte> renderedWindow = [];
                            for (int row = 256; row < 512; row++)
                            {
                                renderedWindow.AddRange(renderedVram.AsSpan(2 * (row * 1024 + 576), 320 * 2).ToArray());
                            }
                            Assert.Contains(renderedWindow, value => value != 0);
                            Assert.NotNull(current.NativeEffectMemory!.PrimitiveProcess!.Software);
                            Assert.NotEmpty(current.NativeGraphics!.PrimitiveFrame!.LastCommands);
                            gravija214Observations.Add(new { Boundary = "actual-post-clear-post-draw-window", X = 576, Y = 256, Width = 320, Height = 256, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(renderedWindow.ToArray())), Software = current.NativeEffectMemory!.PrimitiveProcess!.Software!.Environment, ActualCommands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Length });
                            ColdNext("gravija-current-preprocessed-copy", current);
                            gravija214Preprocessed = true;
                        }
                        NativeBattleModulePoolTask? main = after.Pools[2].Slots.FirstOrDefault(task => (task.Flags & 1) != 0);
                        if (main is not null)
                        {
                            int mainAge = unchecked((short)main.ReadWord(12));
                            string mainBoundary = "gravija-main-" + mainAge;
                            if (mainAge is 1 or 18 or 19 or 67 or 68 or 74 or 75 && gravija214Checkpoints.Add(mainBoundary))
                            {
                                ColdNext(mainBoundary, current);
                            }
                            if (mainAge == 75 && main.ReadByte(0x12) == 0 && !gravija214ReloadPending)
                            {
                                ColdNext("gravija-main74-COMMON-pending", current);
                                gravija214ReloadPending = true;
                            }
                        }
                        if (!gravija214SpriteSeen && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x69DF00))
                        {
                            Assert.Equal(28, after.Pools[1].Stride);
                            Assert.Equal(64, after.Pools[1].Slots.Length);
                            ColdNext("gravija-actual180-sprite-pool", current);
                            gravija214SpriteSeen = true;
                        }
                    }
                    NativeBattleLogicalCommand[] gravityCommands = Gravija214Commands(current);
                    foreach (NativeBattleLogicalCommand command in gravityCommands)
                    {
                        int bank = command.Packet.ByteOffset >= NativeBattleGravija214Profile.SecondPacketOffset ? 1 : 0;
                        if (gravija214Banks.Add(bank))
                        {
                            ColdNext("gravija-packet-bank-" + bank, current);
                        }
                    }
                    NativeBattleLogicalCommand? colorMode = gravityCommands.FirstOrDefault(command => command.Opcode == 0xE1 && command.Words[0] == 0xE1000220);
                    NativeBattleLogicalCommand? textureMode = gravityCommands.FirstOrDefault(command => command.Opcode == 0xE1 && command.Words[0] == 0xE1000240);
                    int? warpTexturedEnd = null;
                    if (colorMode is not null && textureMode is not null)
                    {
                        int actualWarpEnd = AssertGravija214Warp(current, colorMode, textureMode, gravityCommands);
                        warpTexturedEnd = actualWarpEnd;
                        if (!gravija214WarpSeen)
                        {
                            NativeBattleLogicalCommand? firstTexture = gravityCommands.FirstOrDefault(command => command.Packet.ByteOffset >= textureMode.Packet.ByteOffset + 8 && command.Packet.ByteOffset < actualWarpEnd);
                            if (firstTexture is not null)
                            {
                                var retainedClut = current.NativeEffectMemory!.Arena!.ReadSnapshot(firstTexture.Packet.ByteOffset + 14, 2);
                                gravija214Observations.Add(new { Boundary = "actual-warp-retained-CLUT", firstTexture.Packet, retainedClut.Data, retainedClut.Known });
                            }
                            ColdNext("gravija-literal-color-and-textured-warp", current);
                            gravija214WarpSeen = true;
                        }
                    }
                    if (!gravija214ProgrammeSzSeen && gravityCommands.Any(command => (command.Opcode & 0xFC) is 0x2C or 0x38 or 0x3C && command.VertexPayload4.Length == 4 && command.VertexPayload4.Any(value => value != 0)))
                    {
                        ColdNext("gravija-actual7043-programme-SZ", current);
                        gravija214ProgrammeSzSeen = true;
                    }
                    foreach (NativeBattleLogicalCommand command in gravityCommands)
                    {
                        for (int word = 0; word < command.Words.Length; word++)
                        {
                            uint mask = Additional182ConsumedMask(command.Opcode, word);
                            bool warpTexture = textureMode is not null && warpTexturedEnd is int gravityWarpEnd && command.Packet.ByteOffset >= textureMode.Packet.ByteOffset + 8 && command.Packet.ByteOffset < gravityWarpEnd && command.Opcode is 0x26 or 0x2E;
                            if (warpTexture && word == 2)
                            {
                                mask &= 0xFFFF;
                            }
                            var physical = current.NativeEffectMemory.Arena!.ReadSnapshot(command.Packet.ByteOffset + (command.WordOffset + word) * 4, 4);
                            for (int part = 0; part < 4; part++)
                            {
                                byte byteMask = unchecked((byte)(mask >> (part * 8)));
                                if (byteMask != 0)
                                {
                                    Assert.True(physical.Known[part]);
                                    Assert.Equal(unchecked((byte)(command.Words[word] >> (part * 8))) & byteMask, physical.Data[part] & byteMask);
                                }
                            }
                        }
                    }
                }
                if (effectId == 214 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.Gravija214!.RootBankStorage);
                    ColdNext("gravija-root-retired-on-main-completion", current);
                }
                if (effectId == 44 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleJunk44State junk = current.NativeEffectMemory!.Junk44!;
                    Assert.Null(junk.RootBankStorage);
                    Assert.Equal(NativeBattleJunk44Profile.ArenaAddress, junk.ReadDword(NativeBattleJunk44Profile.ArenaGlobal));
                    Assert.Equal(0x1D972C0u + caster * 156u, junk.ReadDword(NativeBattleJunk44Profile.CasterGlobal));
                    Assert.True(after.Pools.Length is 1 or 5);
                    for (int poolIndex = 0; poolIndex < after.Pools.Length; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expectedPool = NativeBattleJunk44Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actualPool = after.Pools[poolIndex];
                        Assert.Equal(expectedPool.Descriptor, actualPool.DescriptorAddress);
                        Assert.Equal(expectedPool.Bank, actualPool.BankAddress);
                        Assert.Equal(expectedPool.Stride, actualPool.Stride);
                        Assert.Equal((int)expectedPool.Capacity, actualPool.Slots.Length);
                        junk44Callbacks.UnionWith(actualPool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    NativeBattleModulePoolTask junkRoot = Assert.Single(after.Pools[0].Slots, task => (task.Flags & 1) != 0);
                    int rootAge = unchecked((short)junkRoot.ReadWord(12));
                    string rootBoundary = "junk-root-" + rootAge;
                    if (rootAge is 1 or 2 or 3 or 64 or 101 or 114 or 115 or 116 && junk44Checkpoints.Add(rootBoundary))
                    {
                        ColdNext(rootBoundary, current);
                    }
                    if (after.Pools.Length == 5)
                    {
                        foreach (var pool in after.Pools.Skip(1))
                        {
                            foreach (var task in pool.Slots.Where(task => (task.Flags & 1) != 0))
                            {
                                string liveBoundary = "junk-live-" + task.Callback.ToString("X");
                                if (junk44Checkpoints.Add(liveBoundary))
                                {
                                    ColdNext(liveBoundary, current);
                                }
                            }
                        }
                        NativeBattleModulePoolTask? main = after.Pools[4].Slots.FirstOrDefault(task => (task.Flags & 1) != 0);
                        if (main is not null && main.ReadWord(12) is 1 or 3 or 63 or 99)
                        {
                            string mainBoundary = "junk-main-" + main.ReadWord(12);
                            if (junk44Checkpoints.Add(mainBoundary))
                            {
                                ColdNext(mainBoundary, current);
                            }
                        }
                        if (before?.Pools.Length == 5 && before.Pools[4].Slots.Any(task => (task.Flags & 1) != 0) && main is null)
                        {
                            Assert.Equal(source.NativeGraphics!.Models[contactActor].Transform!.Position28, current.NativeGraphics!.Models[contactActor].Transform!.Position28);
                            junk44RestoredPosition = true;
                            ColdNext("junk-main-retired-position-restored", current);
                        }
                    }
                    foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => Junk44PacketReferences(current).Contains(item.Command.Packet)).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
                    {
                        NativeBattleLogicalCommand firstCommand = group.First().Command;
                        Assert.All(group, item => Assert.Equal(firstCommand.Words.ToArray(), item.Command.Words.ToArray()));
                        Assert.All(group, item => Assert.Equal(firstCommand.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
                        return firstCommand;
                    }))
                    {
                        junk44Opcodes.Add(command.Opcode);
                        junk44PacketBanks.Add(command.Packet.ByteOffset >= 0x112D4 ? 1 : 0);
                        for (int word = 0; word < command.Words.Length; word++)
                        {
                            uint consumed = Additional182ConsumedMask(command.Opcode, word);
                            uint physicalWord = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory!.Arena!.Read(command.Packet.ByteOffset + (command.WordOffset + word) * 4, 4));
                            Assert.Equal(command.Words[word] & consumed, physicalWord & consumed);
                        }
                    }
                    if (rootAge == 115 && after.Pools.Length == 5)
                    {
                        Assert.All(after.Pools.Skip(1), pool => Assert.DoesNotContain(pool.Slots, task => (task.Flags & 1) != 0));
                    }
                    if (rootAge >= 116)
                    {
                        NativeBattleEffectArena common = current.NativeEffectMemory!.Arena!;
                        Assert.Equal((byte)2, common.Read(0x128F, 1)[0]);
                        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(common.Read(0x1294, 2)));
                        Assert.Equal((short)360, BinaryPrimitives.ReadInt16LittleEndian(common.Read(0x1296, 2)));
                        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(common.Read(0x129C, 2)));
                        Assert.Equal((short)361, BinaryPrimitives.ReadInt16LittleEndian(common.Read(0x129E, 2)));
                        Assert.Equal(NativeBattleJunk44Profile.ArenaAddress + 0x12D4u, BinaryPrimitives.ReadUInt32LittleEndian(common.Read(0x1298, 4)));
                        Assert.Equal(NativeBattleJunk44Profile.ArenaAddress + 0x12D4u, BinaryPrimitives.ReadUInt32LittleEndian(common.Read(0x12A0, 4)));
                        if (!junk44IoQueued)
                        {
                            ColdNext("junk-two-original-files-queued", current);
                            junk44IoQueued = true;
                        }
                        junk44IoCompleted |= common.Read(0x1290, 1)[0] == 2;
                    }
                }
                if (effectId == 44 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    Assert.Empty(after.Pools);
                    Assert.NotNull(current.NativeEffectMemory!.Junk44!.RootBankStorage);
                    junk44IoCompleted |= current.NativeEffectMemory.Arena!.Read(0x1290, 1)[0] == 2;
                    ColdNext("junk-root-and-COMMON-banks-detached", current);
                }
                if (effectId == 45 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleStare45State stare = current.NativeEffectMemory!.Stare45!;
                    Assert.Equal(6, after.Pools.Length);
                    Assert.All(stare.InactiveBanks, bank => Assert.Null(bank));
                    for (int poolIndex = 0; poolIndex < 6; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expected = NativeBattleStare45Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actual = after.Pools[poolIndex];
                        Assert.Equal(expected.Descriptor, actual.DescriptorAddress);
                        Assert.Equal(expected.Bank, actual.BankAddress);
                        Assert.Equal(expected.Stride, actual.Stride);
                        Assert.Equal((int)expected.Capacity, actual.Slots.Length);
                        stare45Callbacks.UnionWith(actual.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    Assert.DoesNotContain(after.Pools[5].Slots, task => (task.Flags & 1) != 0);
                    NativeBattleModulePoolTask stareRoot = Assert.Single(after.Pools[0].Slots, task => (task.Flags & 1) != 0);
                    string rootPhase = "stare-root-phase-" + stareRoot.ReadByte(0x29);
                    if (stare45Boundaries.Add(rootPhase))
                    {
                        ColdNext(rootPhase, current);
                    }
                    string protocol = stare.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x40) + "/" + stare.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x42) + "/" + stare.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x44);
                    if (stare45Protocol.Add(protocol))
                    {
                        stare45Observations.Add(new { Boundary = "actual-object-request-pipeline", Protocol = protocol, Callback = current.NativeGraphics!.Callback });
                        ColdNext("stare-protocol-" + protocol.Replace('/', '-'), current);
                    }
                    if (before?.Pools.Length == 6 && before.Pools[2].Slots.Any(task => (task.Flags & 1) != 0))
                    {
                        NativeBattleStare45State oldState = prior.NativeEffectMemory!.Stare45!;
                        Assert.Equal(oldState.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x42), stare.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x40));
                        Assert.Equal(oldState.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x44), stare.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x42));
                    }
                    if (before?.Pools.Length == 6)
                    {
                        NativeBattlePackedMatrix32 basisCamera = prior.NativeCamera!.CapturePackedView1D97778();
                        NativeBattleActionMatrix camera = basisCamera.ReadMatrix();
                        var up = NativeBattleActionGeometryRules.NormalizeVector56BC50(camera.Rotation[1], camera.Rotation[4], camera.Rotation[7]);
                        var right = NativeBattleActionGeometryRules.NormalizeVector56BC50(up.Y, unchecked(-up.X), 0);
                        var crossed = NativeBattleRayBomb10Geometry.Cross56BBF0(right, up);
                        var forward = NativeBattleActionGeometryRules.NormalizeVector56BC50(crossed.X, crossed.Y, crossed.Z);
                        short[] expectedBasis = [(short)right.X, (short)up.X, (short)forward.X, (short)right.Y, (short)up.Y, (short)forward.Y, (short)right.Z, (short)up.Z, (short)forward.Z];
                        Assert.Equal(expectedBasis, Enumerable.Range(0, 9).Select(index => unchecked((short)stare.ReadWord(NativeBattleStare45Profile.BasisAddress + (uint)index * 2))).ToArray());
                        Assert.Equal(basisCamera.Bytes.AsSpan().Slice(20, 12).ToArray(), stare.Read(NativeBattleStare45Profile.BasisAddress + 20, 12));
                        Assert.Equal(prior.NativeEffectMemory!.Stare45!.Read(NativeBattleStare45Profile.BasisAddress + 18, 2), stare.Read(NativeBattleStare45Profile.BasisAddress + 18, 2));
                    }
                    for (int slot = 0; slot < after.Pools[3].Slots.Length; slot++)
                    {
                        NativeBattleModulePoolTask programme = after.Pools[3].Slots[slot];
                        NativeBattleModulePoolTask? oldProgramme = before?.Pools.Length == 6 ? before.Pools[3].Slots[slot] : null;
                        if (programme.Callback is not (0x836D60 or 0x839590))
                        {
                            continue;
                        }
                        bool casterProgramme = programme.Callback == 0x836D60;
                        uint expectedSource = casterProgramme ? NativeBattleStare45Profile.ProgrammeAAddress : NativeBattleStare45Profile.ProgrammeBAddress;
                        int expectedCache = casterProgramme ? NativeBattleStare45Profile.ProgrammeAWorkspaceLength : NativeBattleStare45Profile.ProgrammeBWorkspaceLength;
                        Assert.Equal(expectedSource, programme.ReadDword(0x74));
                        Assert.Equal((uint)expectedCache, programme.ReadDword(0x78));
                        uint physicalAddress = after.Pools[3].BankAddress + (uint)(slot * after.Pools[3].Stride);
                        if (programme.ReadByte(0x29) == 0)
                        {
                            if (!casterProgramme && programme.ReadWord(0x24) is 14 or 15 && stare45Boundaries.Add("stare-target-before-old" + programme.ReadWord(0x24)))
                            {
                                ColdNext("stare-target-before-old" + programme.ReadWord(0x24), current);
                            }
                            continue;
                        }
                        Assert.Equal(expectedSource, programme.ReadDword(0x94));
                        uint frame = programme.ReadDword(0x98);
                        Assert.InRange(frame, 1u, casterProgramme ? 30u : 40u);
                        if ((programme.Flags & 1) != 0 && casterProgramme)
                        {
                            int bone = programme.ReadWord(0x8A) == 0 ? 21 : 22;
                            NativeBattleVector point = BladeShot12PointOracle(prior, 3, [(byte)bone]);
                            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), unchecked((short)programme.ReadWord(0x62))), new(0, 0, -64));
                            Assert.Equal(new[] { unchecked((ushort)(point.X + offset.X)), unchecked((ushort)(point.Y + offset.Y)), unchecked((ushort)(point.Z + offset.Z)) }, new[] { programme.ReadWord(0x1C), programme.ReadWord(0x1E), programme.ReadWord(0x20) });
                            if (stare45Bones.Add(bone))
                            {
                                stare45Observations.Add(new { Boundary = "actual-caster-current-bone-" + bone, PhysicalTask = physicalAddress, Bone = bone, Point = point, Offset = offset });
                                ColdNext("stare-current-bone-" + bone, current);
                            }
                        }
                        if ((programme.Flags & 1) != 0 && !casterProgramme)
                        {
                            if (stare45TargetF1 is null)
                            {
                                stare45TargetF1 = BladeShot12PointOracle(prior, 0, [0xF1]);
                                Assert.Equal(1u, frame);
                                stare45Boundaries.Add("stare-target-first-delayed-F1");
                                ColdNext("stare-target-first-delayed-F1", current);
                            }
                            NativeBattleVector expected = stare45TargetF1.Value;
                            Assert.Equal(new[] { unchecked((ushort)expected.X), unchecked((ushort)expected.Y), unchecked((ushort)expected.Z) }, new[] { programme.ReadWord(0x1C), programme.ReadWord(0x1E), programme.ReadWord(0x20) });
                        }
                        if (frame is 1 or 15 or 30 or 40 && stare45Boundaries.Add("stare-cache-" + slot + "-frame-" + frame))
                        {
                            ColdNext("stare-cache-" + slot + "-frame-" + frame, current);
                        }
                        if (oldProgramme is not null && (oldProgramme.Flags & 1) != 0 && programme.ReadByte(0x29) == 2 && (programme.ReadByte(0x26) & 1) != 0)
                        {
                            Assert.Equal(casterProgramme ? 30u : 40u, frame);
                            stare45ProgrammesCompleted.Add(physicalAddress);
                        }
                    }
                    if (after.Pools[3].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x836D60) == 2)
                    {
                        Assert.Equal(new ushort[] { 0, 1 }, after.Pools[3].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x836D60).Select(task => task.ReadWord(0x8A)).Order().ToArray());
                    }
                    if (!stare45MorphSeen && stare.Read(NativeBattleStare45Profile.ScratchAddress, NativeBattleStare45Profile.ScratchLength).Any(value => value != 0))
                    {
                        stare45MorphSeen = true;
                        Assert.All(Enumerable.Range(0, 100), vertex => Assert.Equal((ushort)0, stare.ReadWord(NativeBattleStare45Profile.ScratchAddress + (uint)(vertex * 8 + 6))));
                        ColdNext("stare-current800-morph-XYZ6-W-retained", current);
                    }
                    DirectedNodePacketState tim = Director(current);
                    if (tim.PrimaryDrawCursor is int end && tim.SecondaryDrawCursor is int limit)
                    {
                        Assert.Equal(limit - tim.DrawStep, end);
                        if (stare45TimBanks.Add(end - tim.SourceArenaOffset))
                        {
                            ColdNext("stare-TIM-C000-bank-no-packet-writes-" + (end - tim.SourceArenaOffset).ToString("X"), current);
                        }
                    }
                    ImmutableHashSet<NativeBattlePrimitiveReference> programmePackets = Stare45PacketReferences(prior, current);
                    NativeBattleLogicalCommand[] commands = Resonance49Commands(current).Where(command => programmePackets.Contains(command.Packet)).ToArray();
                    foreach (NativeBattleLogicalCommand command in commands)
                    {
                        NativeBattlePrimitiveProcess physical = current.NativeEffectMemory!.PrimitiveProcess! with { PacketBank = command.Packet.Bank, PacketBuffer = command.Packet.Buffer };
                        for (int word = 0; word < command.Words.Length; word++)
                        {
                            uint mask = Additional182ConsumedMask(command.Opcode, word);
                            int offset = command.Packet.ByteOffset + (command.WordOffset + word) * 4;
                            uint actual = mask == 0xFFFF ? BinaryPrimitives.ReadUInt16LittleEndian(physical.ReadCommonPacket(offset, 2)) : BinaryPrimitives.ReadUInt32LittleEndian(physical.ReadCommonPacket(offset, 4));
                            Assert.Equal(command.Words[word] & mask, actual & mask);
                        }
                        stare45Opcodes.Add(command.Opcode);
                        if ((command.Opcode & 0xFC) is 0x2C or 0x3C)
                        {
                            Assert.Equal(4, command.VertexPayload4.Length);
                            Assert.Contains(command.VertexPayload4, depth => depth > 0);
                            int packetBytes = (command.Words.Length + 1) * 4;
                            NativeBattleLogicalCommand mode = Assert.Single(commands, candidate => candidate.Packet.Bank == command.Packet.Bank && candidate.Packet.Buffer == command.Packet.Buffer && candidate.Packet.ByteOffset == command.Packet.ByteOffset + packetBytes && candidate.WordOffset == 1 && candidate.Opcode == 0xE1);
                            Assert.Equal(command.OrderingBucket, mode.OrderingBucket);
                            Assert.Equal(new uint[] { 0xE1000020 }, mode.Words.ToArray());
                            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(physical.ReadCommonPacket(mode.Packet.ByteOffset + 8, 4)));
                            Assert.Equal((byte)2, physical.ReadCommonPacket(mode.Packet.ByteOffset + 3, 1)[0]);
                            Assert.Equal(4, mode.VertexPayload4.Length);
                            stare45ModePairSeen = true;
                        }
                    }
                    if (commands.Length > 0 && stare45Boundaries.Add("stare-actual-COMMON-prefix-and-E1-pairs"))
                    {
                        ColdNext("stare-actual-COMMON-prefix-and-E1-pairs", current);
                    }
                }
                if (effectId == 45 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && stare45Boundaries.Add("stare-all-six-exclusive-banks-detached"))
                {
                    NativeBattleStare45State stare = current.NativeEffectMemory!.Stare45!;
                    Assert.Empty(after.Pools);
                    for (int bank = 0; bank < 6; bank++)
                    {
                        NativeBattleCureFamilyPool pool = NativeBattleStare45Profile.Pools[bank];
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(stare.InactiveBanks[bank]!.Read(slot * pool.Stride, 2)) & 1);
                        }
                    }
                    ColdNext("stare-all-six-exclusive-banks-detached", current);
                }
                if (effectId == 46 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleSigh46State sigh = current.NativeEffectMemory!.Sigh46!;
                    NativeBattleSigh46Resources sighResources = original.Sigh46Resources!;
                    Assert.Equal(true, sigh.SourcesLoaded);
                    Assert.Equal(1u, sigh.ReadDword(NativeBattleSigh46Profile.SourceAddress));
                    Assert.Equal(6, after.Pools.Length);
                    Assert.All(sigh.InactiveBanks, bank => Assert.Null(bank));
                    for (int poolIndex = 0; poolIndex < 6; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expected = NativeBattleSigh46Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actual = after.Pools[poolIndex];
                        Assert.Equal(expected.Descriptor, actual.DescriptorAddress);
                        Assert.Equal(expected.Bank, actual.BankAddress);
                        Assert.Equal(expected.Stride, actual.Stride);
                        Assert.Equal((int)expected.Capacity, actual.Slots.Length);
                        sigh46Callbacks.UnionWith(actual.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    NativeBattleModulePoolTask sighRoot = Assert.Single(after.Pools[0].Slots, task => (task.Flags & 1) != 0);
                    int sighPhase = sighRoot.ReadByte(0x29);
                    string sighBoundary = "sigh-root-phase-" + sighPhase;
                    if (sigh46Boundaries.Add(sighBoundary))
                    {
                        sigh46Observations.Add(new { Boundary = sighBoundary, Root = sighRoot, Globals = sigh.Globals });
                        ColdNext(sighBoundary, current);
                    }
                    if (before?.Pools.Length == 6)
                    {
                        NativeBattleModulePoolTask oldRoot = before.Pools[0].Slots[0];
                        if (oldRoot.ReadByte(0x29) == 5)
                        {
                            Assert.Equal(unchecked((ushort)(oldRoot.ReadWord(0x60) - 1)), sighRoot.ReadWord(0x60));
                        }
                        if (oldRoot.ReadByte(0x29) == 6 && oldRoot.ReadWord(0x5E) == 0)
                        {
                            Assert.Equal((byte)7, sighRoot.ReadByte(0x29));
                        }
                    }
                    if (sighPhase == 7 && !sigh46NodesDrainedBeforeReload)
                    {
                        Assert.Equal((ushort)0, sighRoot.ReadWord(0x5E));
                        Assert.DoesNotContain(after.Pools[2].Slots, task => (task.Flags & 1) != 0);
                        int emitterOffset = checked((int)(sigh.ReadDword(NativeBattleSigh46Profile.EmitterPointerGlobal) - 0x20DFAB8u));
                        int geometryOffset = checked((int)(sigh.ReadDword(NativeBattleSigh46Profile.GeometryPointerGlobal) - 0x20DFAB8u));
                        Assert.All(Enumerable.Range(0, 20), index => Assert.Equal((byte)0, current.NativeEffectMemory.Arena!.Read(emitterOffset + index * 108 + 0x69, 1)[0]));
                        Assert.All(Enumerable.Range(0, 100), index => Assert.Equal((byte)0, current.NativeEffectMemory.Arena!.Read(geometryOffset + index * 672 + 0x1D7, 1)[0]));
                        sigh46NodesDrainedBeforeReload = true;
                        sigh46Observations.Add(new { Boundary = "sigh-node-lists-drained-before-A3-reload", NativeCallback = current.NativeGraphics!.Callback, Emitters = 20, Geometry = 100 });
                    }
                    if (sighPhase == 8 && sighRoot.ReadByte(0x62) == 0)
                    {
                        Assert.Contains(current.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
                        sigh46CommonPending = true;
                    }
                    if (sighPhase == 9)
                    {
                        Assert.Equal((byte)255, sighRoot.ReadByte(0x62));
                        sigh46CommonCompleted = true;
                    }
                    foreach (NativeBattleSigh46Range channel in NativeBattleSigh46Profile.ChannelRegions)
                    {
                        Assert.Equal(new byte[channel.Length], sigh.Read(channel.Address, channel.Length));
                    }
                    uint emitterPointer = sigh.ReadDword(NativeBattleSigh46Profile.EmitterPointerGlobal);
                    uint geometryPointer = sigh.ReadDword(NativeBattleSigh46Profile.GeometryPointerGlobal);
                    if (emitterPointer != 0)
                    {
                        Assert.Equal(emitterPointer + 0x870, geometryPointer);
                        int emitterOffset = checked((int)(emitterPointer - 0x20DFAB8u));
                        int geometryOffset = checked((int)(geometryPointer - 0x20DFAB8u));
                        Assert.InRange(sigh.ReadWord(NativeBattleSigh46Profile.EmitterScanCursorGlobal), (ushort)0, (ushort)19);
                        Assert.InRange(sigh.ReadWord(NativeBattleSigh46Profile.GeometryScanCursorGlobal), (ushort)0, (ushort)99);
                        bool liveEmitter = Enumerable.Range(0, 20).Any(index => current.NativeEffectMemory.Arena!.Read(emitterOffset + index * 108 + 0x69, 1)[0] != 0);
                        bool liveGeometry = Enumerable.Range(0, 100).Any(index => current.NativeEffectMemory.Arena!.Read(geometryOffset + index * 672 + 0x1D7, 1)[0] != 0);
                        if (liveEmitter && !sigh46EmittersSeen)
                        {
                            sigh46EmittersSeen = true;
                            ColdNext("sigh-literal20-emitter-list-live", current);
                        }
                        if (liveGeometry && !sigh46GeometrySeen)
                        {
                            sigh46GeometrySeen = true;
                            ColdNext("sigh-literal100-geometry-list-live", current);
                        }
                    }
                    for (int customSlot = 0; customSlot < after.Pools[3].Slots.Length; customSlot++)
                    {
                        NativeBattleModulePoolTask custom = after.Pools[3].Slots[customSlot];
                        if (custom.Callback != 0x8359C0)
                        {
                            continue;
                        }
                        NativeBattleModulePoolTask? oldCustom = before?.Pools.Length == 6 ? before.Pools[3].Slots[customSlot] : null;
                        if (custom.ReadWord(0x24) == 1 && custom.ReadByte(0x29) == 1 && !sigh46VramInitialized)
                        {
                            NativeBattleGraphicsModel actualCaster = prior.NativeGraphics!.Models[3];
                            short yaw = actualCaster.Rotation14!.Value;
                            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw), new(0, 0, -768));
                            NativeBattleVector position = actualCaster.Transform!.Position28;
                            Assert.Equal(new[] { unchecked((ushort)(position.X + offset.X)), unchecked((ushort)(position.Y + offset.Y)), unchecked((ushort)(position.Z + offset.Z)) }, new[] { custom.ReadWord(0x1C), custom.ReadWord(0x1E), custom.ReadWord(0x20) });
                            AssertSigh46VramCopies(prior, current, [new(512, 256, 64, 256, 704, 256), new(672, 256, 64, 256, 768, 256)]);
                            sigh46VramInitialized = true;
                            ColdNext("sigh-original-VRAM-backup-two64-strips", current);
                        }
                        if (oldCustom is not null && (oldCustom.Flags & 1) != 0 && (custom.ReadByte(0x26) & 4) == 0)
                        {
                            Assert.Equal(unchecked((byte)(oldCustom.ReadByte(0x84) + 8)), custom.ReadWord(0x84));
                            Assert.Equal(unchecked((byte)(oldCustom.ReadByte(0x86) + 6)), custom.ReadWord(0x86));
                            AssertSigh46VramCopies(prior, current, Sigh46WrappedCopies(custom.ReadWord(0x84), custom.ReadWord(0x86)));
                            sigh46VramWrapped = true;
                            int selectedRow = unchecked((short)oldCustom.ReadWord(0x82));
                            Assert.InRange(selectedRow, 0, 16);
                            Assert.Equal((ushort)Math.Min(selectedRow + 1, 16), custom.ReadWord(0x82));
                            foreach (NativeBattleStaticMeshResource seed in sighResources.MeshSeeds)
                            {
                                uint table = seed.Address == NativeBattleSigh46Profile.MeshAAddress ? NativeBattleSigh46Profile.ColorTableAAddress : NativeBattleSigh46Profile.ColorTableBAddress;
                                ImmutableArray<byte> colors = sighResources.ReadColorStream(table, selectedRow);
                                byte[] expected = seed.Bytes.ToArray();
                                for (int face = 0; face < 60; face++)
                                {
                                    int[] lanes = [0, 24, 28, 32];
                                    for (int color = 0; color < 4; color++)
                                    {
                                        colors.AsSpan().Slice(face * 16 + color * 4, 4).CopyTo(expected.AsSpan(656 + face * 36 + lanes[color], 4));
                                    }
                                }
                                Assert.Equal(expected, sigh.ReadMesh(sighResources, seed.Address).Bytes.ToArray());
                            }
                            if (sigh46MaterialRows.Add(selectedRow) && selectedRow is 0 or 8 or 16)
                            {
                                sigh46Observations.Add(new { Boundary = "sigh-current-color-row-" + selectedRow, Row = selectedRow, Custom = custom });
                                ColdNext("sigh-current-color-row-" + selectedRow, current);
                            }
                            if (custom.ReadByte(0x29) == 3 && sigh46Boundaries.Add("sigh-before-native-fade"))
                            {
                                ColdNext("sigh-before-native-fade", current);
                            }
                        }
                    }
                    for (int particleSlot = 0; particleSlot < after.Pools[4].Slots.Length; particleSlot++)
                    {
                        NativeBattleModulePoolTask particle = after.Pools[4].Slots[particleSlot];
                        if ((particle.Flags & 1) == 0 || particle.Callback != 0x836090)
                        {
                            continue;
                        }
                        sigh46ParticleSlots.Add(particleSlot);
                        sigh46ParticleFrames.Add(particle.ReadWord(0x50));
                        Assert.Equal((ushort)14, particle.ReadWord(0x52));
                        Assert.Contains(particle.ReadDword(0x4C), new uint[] { 0x15B82A4, 0x15B8438, 0x15B8BA4 });
                        if (sigh46ParticleSprites.Add(particle.ReadDword(0x4C)))
                        {
                            ColdNext("sigh-current-smoke-resource-" + particle.ReadDword(0x4C).ToString("X"), current);
                        }
                    }
                    DirectedNodePacketState sighPackets = Director(current);
                    if (sighPackets.PrimaryDrawCursor is int sighEnd && sighPackets.SecondaryDrawCursor is int sighLimit)
                    {
                        int sighStart = sighLimit - sighPackets.DrawStep;
                        if (sigh46PacketBanks.Add(sighStart - sighPackets.SourceArenaOffset))
                        {
                            ColdNext("sigh6000-bank-" + (sighStart - sighPackets.SourceArenaOffset).ToString("X"), current);
                        }
                        foreach (NativeBattleLogicalCommand command in Resonance49Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= sighStart && command.Packet.ByteOffset < sighEnd))
                        {
                            byte[] physical = current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                            if ((command.Opcode & 0xFC) == 0x3C)
                            {
                                Assert.Equal(4, command.VertexPayload4.Length);
                                Assert.Contains(command.VertexPayload4, depth => depth > 0);
                                if (!sigh46Gt4Seen)
                                {
                                    sigh46Gt4Seen = true;
                                    sigh46Boundaries.Add("sigh-current-GT4-F0-native-SZ");
                                    ColdNext("sigh-current-GT4-F0-native-SZ", current);
                                }
                            }
                        }
                    }
                    NativeBattleModulePoolTask? sighTarget = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0);
                    if (sighTarget is not null && sighTarget.ReadWord(0x24) is 40 or 41 && sigh46Boundaries.Add("sigh-before-target-old" + sighTarget.ReadWord(0x24)))
                    {
                        ColdNext("sigh-before-target-old" + sighTarget.ReadWord(0x24), current);
                    }
                }
                if (effectId == 46 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && sigh46Boundaries.Add("sigh-all-six-banks-detached-after-COMMON-FF"))
                {
                    NativeBattleSigh46State sigh = current.NativeEffectMemory!.Sigh46!;
                    Assert.Empty(after.Pools);
                    for (int bank = 0; bank < 6; bank++)
                    {
                        NativeBattleCureFamilyPool pool = NativeBattleSigh46Profile.Pools[bank];
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(sigh.InactiveBanks[bank]!.Read(slot * pool.Stride, 2)) & 1);
                        }
                    }
                    Assert.Equal((byte)255, sigh.InactiveBanks[0]!.Read(0x62, 1)[0]);
                    Assert.True(sigh46NodesDrainedBeforeReload);
                    ColdNext("sigh-all-six-banks-detached-after-COMMON-FF", current);
                }
                if (effectId == 48 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleMagmaBreath48State magma = current.NativeEffectMemory!.MagmaBreath48!;
                    NativeBattleMagmaBreath48Resources magmaResources = original.MagmaBreath48Resources!;
                    Assert.Equal(true, magma.SourcesLoaded);
                    Assert.Equal(1u, magma.ReadDword(NativeBattleMagmaBreath48Profile.SourceAddress));
                    Assert.Equal(6, after.Pools.Length);
                    Assert.All(magma.InactiveBanks, bank => Assert.Null(bank));
                    for (int poolIndex = 0; poolIndex < 6; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expected = NativeBattleMagmaBreath48Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actual = after.Pools[poolIndex];
                        Assert.Equal(expected.Descriptor, actual.DescriptorAddress);
                        Assert.Equal(expected.Bank, actual.BankAddress);
                        Assert.Equal(expected.Stride, actual.Stride);
                        Assert.Equal((int)expected.Capacity, actual.Slots.Length);
                        magma48Callbacks.UnionWith(actual.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                    }
                    NativeBattleModulePoolTask magmaRoot = Assert.Single(after.Pools[0].Slots, task => (task.Flags & 1) != 0);
                    int magmaPhase = magmaRoot.ReadByte(0x29);
                    string magmaBoundary = "root-phase-" + magmaPhase;
                    if (magma48Boundaries.Add(magmaBoundary))
                    {
                        magma48Observations.Add(new { Boundary = magmaBoundary, Root = magmaRoot, Globals = magma.Globals });
                        ColdNext(magmaBoundary, current);
                    }
                    if (before?.Pools.Length == 6)
                    {
                        NativeBattleModulePoolTask oldRoot = before.Pools[0].Slots[0];
                        if (oldRoot.ReadByte(0x29) == 5)
                        {
                            Assert.Equal(unchecked((ushort)(oldRoot.ReadWord(0x60) - 1)), magmaRoot.ReadWord(0x60));
                        }
                        if (oldRoot.ReadByte(0x29) == 6 && oldRoot.ReadWord(0x5E) == 0)
                        {
                            Assert.Equal((byte)7, magmaRoot.ReadByte(0x29));
                        }
                    }
                    foreach (NativeBattleMagmaBreath48Range channel in NativeBattleMagmaBreath48Profile.ChannelRegions)
                    {
                        Assert.Equal(new byte[channel.Length], magma.Read(channel.Address, channel.Length));
                    }
                    foreach (NativeBattleStaticMeshResource seed in magmaResources.MeshSeeds)
                    {
                        NativeBattleStaticMeshResource currentMesh = magma.ReadMesh(magmaResources, seed.Address);
                        for (int vertex = 0; vertex < 182; vertex++)
                        {
                            Assert.Equal(seed.Bytes.AsSpan().Slice(14 + vertex * 8, 2).ToArray(), currentMesh.Bytes.AsSpan().Slice(14 + vertex * 8, 2).ToArray());
                        }
                        if (!seed.Bytes.AsSpan().Slice(8, 182 * 8).SequenceEqual(currentMesh.Bytes.AsSpan().Slice(8, 182 * 8)) && magma48ChangedXYZ.Add(seed.Address))
                        {
                            ColdNext("current-XYZ6-W-retained-" + seed.Address.ToString("X"), current);
                        }
                        if (!seed.Bytes.AsSpan().Slice(1464).SequenceEqual(currentMesh.Bytes.AsSpan().Slice(1464)) && magma48ChangedUV.Add(seed.Address))
                        {
                            ColdNext("current-face-UV-" + seed.Address.ToString("X"), current);
                        }
                    }
                    uint emitterPointer = magma.ReadDword(NativeBattleMagmaBreath48Profile.EmitterPointerGlobal);
                    uint geometryPointer = magma.ReadDword(NativeBattleMagmaBreath48Profile.GeometryPointerGlobal);
                    if (emitterPointer != 0)
                    {
                        Assert.Equal(emitterPointer + 0x870, geometryPointer);
                        int emitterOffset = checked((int)(emitterPointer - 0x20DFAB8u));
                        int geometryOffset = checked((int)(geometryPointer - 0x20DFAB8u));
                        Assert.InRange(magma.ReadWord(NativeBattleMagmaBreath48Profile.EmitterScanCursorGlobal), (ushort)0, (ushort)19);
                        Assert.InRange(magma.ReadWord(NativeBattleMagmaBreath48Profile.GeometryScanCursorGlobal), (ushort)0, (ushort)99);
                        bool liveEmitter = Enumerable.Range(0, 20).Any(index => current.NativeEffectMemory.Arena!.Read(emitterOffset + index * 108 + 0x69, 1)[0] != 0);
                        bool liveGeometry = Enumerable.Range(0, 100).Any(index => current.NativeEffectMemory.Arena!.Read(geometryOffset + index * 672 + 0x1D7, 1)[0] != 0);
                        if (liveEmitter && !magma48EmittersSeen)
                        {
                            magma48EmittersSeen = true;
                            ColdNext("literal20-emitter-list-live", current);
                        }
                        if (liveGeometry && !magma48GeometrySeen)
                        {
                            magma48GeometrySeen = true;
                            ColdNext("literal100-geometry-list-live", current);
                        }
                    }
                    NativeBattleModulePoolTask? magmaEngine = after.Pools[3].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.ReadByte(0x29) == 1);
                    if (magmaEngine is not null && before?.Pools.Length == 6)
                    {
                        NativeBattleVector mouth = BladeShot12PointOracle(prior, 3, [0x15]);
                        Assert.Equal(unchecked((uint)(mouth.X << 16)), magmaEngine.ReadDword(0x228));
                        Assert.Equal(unchecked((uint)(mouth.Y << 16)), magmaEngine.ReadDword(0x22C));
                        Assert.Equal(unchecked((uint)(unchecked((short)(mouth.Z + 256)) << 16)), magmaEngine.ReadDword(0x230));
                        Assert.Equal(magmaEngine.ReadDword(0x228), magmaEngine.ReadDword(0x238));
                        Assert.Equal(0u, magmaEngine.ReadDword(0x23C));
                        Assert.Equal(magmaEngine.ReadDword(0x230), magmaEngine.ReadDword(0x240));
                        if (magma48Boundaries.Add("current-caster-bone15-twice"))
                        {
                            NativeBattleVector casterF1 = BladeShot12PointOracle(prior, 3, [0xF1]);
                            NativeBattleVector targetF1 = BladeShot12PointOracle(prior, 0, [0xF1]);
                            NativeBattleVector targetF0 = BladeShot12PointOracle(prior, 0, [0xF0]);
                            Assert.Equal(new[] { unchecked((uint)(casterF1.X << 16)), unchecked((uint)(casterF1.Y << 16)), unchecked((uint)(casterF1.Z << 16)) }, new[] { magmaEngine.ReadDword(0x218), magmaEngine.ReadDword(0x21C), magmaEngine.ReadDword(0x220) });
                            Assert.Equal(new[] { unchecked((uint)(targetF1.X << 16)), unchecked((uint)(targetF1.Y << 16)), unchecked((uint)(targetF1.Z << 16)) }, new[] { magmaEngine.ReadDword(0x108), magmaEngine.ReadDword(0x10C), magmaEngine.ReadDword(0x110) });
                            Assert.Equal(new[] { unchecked((uint)(targetF0.X << 16)), unchecked((uint)(targetF0.Y << 16)), unchecked((uint)(targetF0.Z << 16)) }, new[] { magmaEngine.ReadDword(0x148), magmaEngine.ReadDword(0x14C), magmaEngine.ReadDword(0x150) });
                            magma48Observations.Add(new { Boundary = "current-caster-bone15-twice", Mouth = mouth, CasterF1 = casterF1, TargetF1 = targetF1, TargetF0 = targetF0, Engine = magmaEngine });
                            ColdNext("current-caster-bone15-twice", current);
                        }
                    }
                    DirectedNodePacketState magmaPackets = Director(current);
                    if (magmaPackets.SecondaryDrawCursor is int magmaLimit && magmaPackets.PrimaryDrawCursor is int magmaEnd)
                    {
                        int magmaStart = magmaLimit - magmaPackets.DrawStep;
                        if (magma48PacketBanks.Add(magmaStart - magmaPackets.SourceArenaOffset))
                        {
                            ColdNext("magma6000-bank-" + (magmaStart - magmaPackets.SourceArenaOffset).ToString("X"), current);
                        }
                        NativeBattleLogicalCommand[] commands = Resonance49Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= magmaStart && command.Packet.ByteOffset < magmaEnd).ToArray();
                        foreach (NativeBattleLogicalCommand command in commands)
                        {
                            byte[] physical = current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                            magma48Opcodes.Add(command.Opcode);
                            magma48DepthPacketsSeen |= command.Opcode is >= 0x34 and <= 0x3F && command.VertexPayload4.Any(value => value > 0);
                            if (command.Opcode == 0xE1 && command.Words.Length == 1 && command.Words[0] == 0xE100002A)
                            {
                                Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena.Read(command.Packet.ByteOffset + 8, 4)));
                                NativeBattleLogicalCommand quad = Assert.Single(commands, candidate => candidate.Packet.ByteOffset + 24 == command.Packet.ByteOffset && candidate.WordOffset == 1 && candidate.Opcode == 0x2A);
                                Assert.Equal((int?)7, quad.OrderingBucket);
                                Assert.Equal((int?)7, command.OrderingBucket);
                                Assert.Equal(new[] { 0, 0, 0, 0 }, command.VertexPayload4.ToArray());
                                magma48ColorPairsSeen = true;
                            }
                        }
                        if (commands.Any(command => command.Opcode is >= 0x34 and <= 0x37) && magma48Boundaries.Add("native-GT3-F0-current-SZ"))
                        {
                            Assert.Contains(commands, command => command.Opcode is >= 0x34 and <= 0x37 && command.VertexPayload4.Length == 4);
                            ColdNext("native-GT3-F0-current-SZ", current);
                        }
                    }
                    NativeBattleModulePoolTask? magmaTarget = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0);
                    if (magmaTarget is not null && magmaTarget.ReadWord(0x24) is 28 or 55 && magma48Boundaries.Add("before-target-old" + magmaTarget.ReadWord(0x24)))
                    {
                        ColdNext("before-target-old" + magmaTarget.ReadWord(0x24), current);
                    }
                }
                if (effectId == 48 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && magma48Boundaries.Add("all-six-banks-and-node-lists-drained"))
                {
                    NativeBattleMagmaBreath48State magma = current.NativeEffectMemory!.MagmaBreath48!;
                    Assert.Empty(after.Pools);
                    for (int bank = 0; bank < 6; bank++)
                    {
                        NativeBattleCureFamilyPool pool = NativeBattleMagmaBreath48Profile.Pools[bank];
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(magma.InactiveBanks[bank]!.Read(slot * pool.Stride, 2)) & 1);
                        }
                    }
                    int emitterOffset = checked((int)(magma.ReadDword(NativeBattleMagmaBreath48Profile.EmitterPointerGlobal) - 0x20DFAB8u));
                    int geometryOffset = checked((int)(magma.ReadDword(NativeBattleMagmaBreath48Profile.GeometryPointerGlobal) - 0x20DFAB8u));
                    Assert.All(Enumerable.Range(0, 20), index => Assert.Equal((byte)0, current.NativeEffectMemory.Arena!.Read(emitterOffset + index * 108 + 0x69, 1)[0]));
                    Assert.All(Enumerable.Range(0, 100), index => Assert.Equal((byte)0, current.NativeEffectMemory.Arena!.Read(geometryOffset + index * 672 + 0x1D7, 1)[0]));
                    ColdNext("all-six-banks-and-node-lists-drained", current);
                }
                if (effectId == 49 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleResonance49State state = current.NativeEffectMemory!.Resonance49!;
                    Assert.Equal(6, after.Pools.Length);
                    Assert.All(state.InactiveBanks, bank => Assert.Null(bank));
                    for (int poolIndex = 0; poolIndex < 6; poolIndex++)
                    {
                        NativeBattleCureFamilyPool expected = NativeBattleResonance49Profile.Pools[poolIndex];
                        NativeBattleEffectTaskPool actual = after.Pools[poolIndex];
                        Assert.Equal(expected.Descriptor, actual.DescriptorAddress);
                        Assert.Equal(expected.Bank, actual.BankAddress);
                        Assert.Equal(expected.Stride, actual.Stride);
                        Assert.Equal((int)expected.Capacity, actual.Slots.Length);
                        foreach (NativeBattleModulePoolTask live in actual.Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            resonance49Callbacks.Add(live.Callback);
                        }
                    }
                    if (before?.ReturnedDescriptor is > 0)
                    {
                        Assert.Equal(JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()), JsonSerializer.Serialize(current.NativeEffectMemory.SharedEffectView2793E58));
                    }
                    NativeBattleModulePoolTask root = Assert.Single(after.Pools[0].Slots, task => (task.Flags & 1) != 0);
                    Assert.Equal(NativeBattleResonance49Profile.RootCallback, root.Callback);
                    Assert.Equal((byte)3, root.ReadByte(0x2C));
                    Assert.Equal((byte)0, root.ReadByte(0x2D));
                    Assert.Equal((ushort)1, root.ReadWord(0x5A));
                    Assert.Equal((ushort)0, root.ReadWord(0x58));
                    Assert.Equal((byte)0, root.ReadByte(0x2F));
                    byte rootPhase = root.ReadByte(0x29);
                    string? rootBoundary = rootPhase switch { 1 => "root-after-native-entry-old0", 3 => "before-target-director", 4 => "target-and-programmes-owned", 6 => "before-previous-survivor-drain", 7 => "before-A3-COMMON-request", 8 => "A3-COMMON-pending", 9 => "A3-completion-byte-FF", _ => null };
                    if (rootBoundary is not null && resonance49Boundaries.Add(rootBoundary))
                    {
                        resonance49Observations.Add(new { Boundary = rootBoundary, Root = root, State = state.Globals });
                        ColdNext(rootBoundary, current);
                    }
                    if (rootPhase == 8 && root.ReadByte(0x62) == 0)
                    {
                        Assert.Contains(current.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
                        resonance49CommonPending = true;
                    }
                    if (rootPhase >= 9)
                    {
                        Assert.Equal((byte)255, root.ReadByte(0x62));
                        Assert.Equal((byte)0, root.ReadByte(0x63));
                        resonance49CommonCompleted = true;
                    }
                    for (int slot = 0; slot < after.Pools[3].Slots.Length; slot++)
                    {
                        NativeBattleModulePoolTask task = after.Pools[3].Slots[slot];
                        NativeBattleModulePoolTask? old = before?.Pools.Length == 6 ? before.Pools[3].Slots[slot] : null;
                        if ((task.Flags & 1) == 0)
                        {
                            if (old is not null && (old.Flags & 1) != 0 && old.ReadByte(0x29) == 1 && task.ReadByte(0x29) == 2)
                            {
                                int actor = task.ReadByte(0x2C);
                                Assert.True(task.ReadDword(0x98) >= 60);
                                resonance49CompletedProgrammes.Add(actor);
                                if (resonance49Boundaries.Add("programme-" + actor + "-native-remaining-zero"))
                                {
                                    ColdNext("programme-" + actor + "-native-remaining-zero", current);
                                }
                            }
                            continue;
                        }
                        int selectedActor = task.Callback == 0x821C50 ? 3 : task.Callback == 0x824140 ? 4 : throw new InvalidDataException("Resonance programme selected an unproved callback.");
                        Assert.Equal((byte)selectedActor, task.ReadByte(0x2C));
                        Assert.Equal(selectedActor == 3 ? NativeBattleResonance49Profile.ProgrammeAAddress : NativeBattleResonance49Profile.ProgrammeBAddress, task.ReadDword(0x74));
                        Assert.Equal((uint)(selectedActor == 3 ? 724 : 712), task.ReadDword(0x78));
                        if (task.ReadByte(0x29) == 0 && task.ReadWord(0x24) == 20 && resonance49Boundaries.Add("programme-" + selectedActor + "-before-native-old20"))
                        {
                            ColdNext("programme-" + selectedActor + "-before-native-old20", current);
                        }
                        if (old is not null && old.ReadByte(0x29) == 0 && task.ReadByte(0x29) == 1 && resonance49ProgrammeActors.Add(selectedActor))
                        {
                            NativeBattleVector expectedPoint = Resonance49ProgrammePoint(prior, selectedActor);
                            Assert.Equal(new[] { expectedPoint.X, expectedPoint.Y, expectedPoint.Z }, new[] { unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)) });
                            Assert.Equal(unchecked((ushort)prior.NativeGraphics!.Models[selectedActor].Rotation14!.Value), task.ReadWord(0x62));
                            Assert.Equal(task.ReadDword(0x74), task.ReadDword(0x94));
                            Assert.True(task.ReadDword(0x98) > 0);
                            resonance49Observations.Add(new { Boundary = "actual-programme-actor-" + selectedActor + "-F0-distance4096", Actor = selectedActor, Point = expectedPoint, CurrentModel = prior.NativeGraphics.Models[selectedActor], Task = task });
                            ColdNext("programme-" + selectedActor + "-first-current-F0", current);
                        }
                        if (task.ReadByte(0x29) == 1 && task.ReadDword(0x98) == 30 && resonance49Boundaries.Add("programme-" + selectedActor + "-workspace30"))
                        {
                            ColdNext("programme-" + selectedActor + "-workspace30", current);
                        }
                    }
                    NativeBattleModulePoolTask? reaction = after.Pools[4].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x824970);
                    if (reaction is not null)
                    {
                        Assert.Equal((byte)0, reaction.ReadByte(0x2D));
                        int phase = reaction.ReadByte(0x29);
                        if (resonance49Boundaries.Add("reaction-phase-" + phase))
                        {
                            resonance49Observations.Add(new { Boundary = "reaction-phase-" + phase, TargetSequence = current.NativeGraphics!.Models[0].Sequence, Counter = state.ReadWord(0x26478E6), Task = reaction });
                            ColdNext("reaction-phase-" + phase, current);
                        }
                        if (phase >= 2)
                        {
                            Assert.Equal((ushort)6, reaction.ReadWord(0x78));
                        }
                    }
                    DirectedNodePacketState packetState = Director(current);
                    if (packetState.PrimaryDrawCursor is int end && packetState.SecondaryDrawCursor is int limit)
                    {
                        int start = limit - packetState.DrawStep;
                        if (resonance49PacketBanks.Add(start - packetState.SourceArenaOffset))
                        {
                            ColdNext("programme-C000-bank-" + (start - packetState.SourceArenaOffset).ToString("X"), current);
                        }
                    }
                    Resonance49PacketWitness packets = AssertResonance49Packets(current);
                    resonance49ScreenPairsSeen |= packets.ScreenPairs == 140;
                    resonance49ProgrammePacketsSeen |= packets.PositiveDepthCommands > 0;
                    resonance49ScreenOpcodes.UnionWith(packets.ScreenOpcodes);
                    if (packets.StaticEnvironmentCommands > 0)
                    {
                        int parity = packets.EnvironmentParity ?? throw new InvalidDataException("Resonance environment command lost its actual static-packet parity.");
                        byte producerBuffer = prior.NativeEffectMemory!.PrimitiveProcess!.Buffer80 ?? throw new InvalidDataException("Resonance environment requires its actual caller buffer selector.");
                        int expectedParity = (producerBuffer - 1) & 1;
                        Assert.Equal(expectedParity, parity);
                        resonance49ProducerBuffers.Add(producerBuffer);
                        resonance49ProducerEnvironmentParities.Add(expectedParity);
                        if (resonance49EnvironmentParities.Add(parity))
                        {
                            resonance49Observations.Add(packets);
                            ColdNext("screen-environment-parity-" + parity, current);
                        }
                    }
                    if (packets.ScreenPairs == 140 && resonance49Boundaries.Add("both-screen-channels-140-physical-pairs"))
                    {
                        resonance49Observations.Add(packets);
                        ColdNext("both-screen-channels-140-physical-pairs", current);
                    }
                }
                if (effectId == 49 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && resonance49Boundaries.Add("all-six-hierarchy-banks-detached"))
                {
                    NativeBattleResonance49State state = current.NativeEffectMemory!.Resonance49!;
                    Assert.Empty(after.Pools);
                    Assert.Equal(6, state.InactiveBanks.Length);
                    for (int index = 0; index < 6; index++)
                    {
                        NativeBattleEffectArena bank = state.InactiveBanks[index] ?? throw new InvalidDataException("Resonance lost its retained physical hierarchy bank.");
                        NativeBattleCureFamilyPool pool = NativeBattleResonance49Profile.Pools[index];
                        for (int slot = 0; slot < pool.Capacity; slot++)
                        {
                            Assert.Equal(0, BitConverter.ToUInt16(bank.Read(slot * pool.Stride, 2)) & 1);
                        }
                    }
                    Assert.Equal((byte)255, state.InactiveBanks[0]!.Read(0x62, 1)[0]);
                    resonance49CommonCompleted = true;
                    ColdNext("all-six-hierarchy-banks-detached", current);
                }
                if (effectId == 12 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleBladeShot12State state = current.NativeEffectMemory!.BladeShot12!;
                    Assert.Equal(new uint[] { NativeBattleBladeShot12Profile.RootDescriptor, NativeBattleBladeShot12Profile.ChildADescriptor, NativeBattleBladeShot12Profile.ChildBDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleBladeShot12Profile.RootBank, NativeBattleBladeShot12Profile.ChildABank, NativeBattleBladeShot12Profile.ChildBBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 100, 32, 52 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new[] { 1, 16, 64 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildABankStorage);
                    Assert.Null(state.ChildBBankStorage);
                    Assert.Equal(true, state.VertexSourcesLoaded);
                    Assert.Equal(0x1D972C0u + 156u * caster, state.ReadDword(NativeBattleBladeShot12Profile.CasterGlobal));
                    Assert.Equal(0x1D972C0u + 156u * contactActor, state.ReadDword(NativeBattleBladeShot12Profile.FirstTargetGlobal));
                    NativeBattleModulePoolTask root = Assert.Single(after.Pools[0].Slots, task => (task.Flags & 1) != 0);
                    Assert.Equal(NativeBattleBladeShot12Profile.RootCallback, root.Callback);
                    NativeBattleModulePoolTask? oldRoot = before?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBladeShot12Profile.RootDescriptor)?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0);
                    int age = root.ReadWord(12);
                    string? phase = age switch { 0 => "entry-root0", 1 => "outbound-first-current", 5 => "before-opening-sound", 31 => "outbound-last-before-return", 32 => "before-old32-contact-and-return", 33 => "return-impact-and-children", 42 => "regular-birth-window-completed", 52 => "morph-window-completed", 60 => "before-ending-sound", 62 => "before-old62-object7-set", 63 => "object7-restored-current-mask", 65 => "root65-children-still-owned", _ => null };
                    if (phase is not null && blade12Boundaries.Add(phase))
                    {
                        blade12Observations.Add(new { Boundary = phase, Root = root });
                        ColdNext(phase, current);
                    }
                    uint currentMask = current.NativeGraphics!.Models[caster].SequenceState!.MeshMask124;
                    uint previousMask = prior.NativeGraphics!.Models[caster].SequenceState!.MeshMask124;
                    int oldAge = oldRoot is null ? -1 : oldRoot.ReadWord(12);
                    Assert.Equal(previousMask & ~0x80u, currentMask & ~0x80u);
                    Assert.Equal(oldAge >= 62 ? 0x80u : 0u, currentMask & 0x80);
                    if (oldRoot is null && blade12Boundaries.Add("entry-native-bone7-and-F0-F1"))
                    {
                        blade12Observations.Add(AssertBladeShot12EntryPoints(prior, current, root));
                        ColdNext("entry-native-bone7-and-F0-F1", current);
                    }
                    if (age <= 1 && !blade12Object7Hidden)
                    {
                        blade12Observations.Add(AssertBladeShot12Object7(prior, current, assets, false));
                        blade12Object7Hidden = true;
                    }
                    if (oldAge == 62 && !blade12Object7Visible)
                    {
                        blade12Observations.Add(AssertBladeShot12Object7(prior, current, assets, true));
                        blade12Object7Visible = true;
                        ColdNext("object7-current-physical-model-packets", current);
                    }
                    if (oldAge is >= 32 and <= 61)
                    {
                        NativeBattleVector expectedBone = BladeShot12PointOracle(prior, caster, [7]);
                        Assert.Equal(unchecked((ushort)expectedBone.X), root.ReadWord(0x24));
                        Assert.Equal(unchecked((ushort)(expectedBone.Y - 600)), root.ReadWord(0x26));
                        Assert.Equal(unchecked((ushort)expectedBone.Z), root.ReadWord(0x28));
                        Assert.Equal((ushort)136, root.ReadWord(0x2A));
                    }
                    if (oldAge is >= 32 and <= 41 && blade12RandomAges.Add(oldAge))
                    {
                        int draws = 10 + (oldAge is 32 or 34 or 36 ? 12 : 0);
                        blade12Observations.Add(AssertBladeShot12ScopedRandom(prior, oldAge, draws));
                        blade12NativeRandomDraws += draws;
                    }
                    if (oldAge is 32 or 41 or 51 && blade12MorphAges.Add(oldAge))
                    {
                        int factor = NativeBattleCameraMath.Sine(((oldAge - 32) << 12) / 20 >> 2);
                        NativeBattleBladeShot12Resources resources = original.BladeShot12Resources!;
                        AssertBladeShot12Morph(current, resources, factor);
                        blade12Observations.Add(new { Boundary = "two49-morph-fronts-XYZ6-W", OldAge = oldAge, Factor = factor, First = state.ReadView(NativeBattleBladeShot12Profile.MutableVerticesA, NativeBattleBladeShot12Profile.MutableVerticesLength), Second = state.ReadView(NativeBattleBladeShot12Profile.MutableVerticesB, NativeBattleBladeShot12Profile.MutableVerticesLength) });
                        ColdNext("two49-morph-fronts-old" + oldAge, current);
                    }
                    foreach (NativeBattleEffectTaskPool pool in after.Pools.Skip(1))
                    {
                        foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
                        {
                            Assert.Contains(task.Callback, new uint[] { 0x6E9B50, 0x6E9C10, 0x6E9D60 });
                            if (blade12Callbacks.Add(task.Callback))
                            {
                                blade12Observations.Add(new { Boundary = "native-child-current-phase", Pool = pool.DescriptorAddress, Task = task });
                                ColdNext("blade12-callback-" + task.Callback.ToString("X"), current);
                            }
                            if (task.Callback is 0x6E9B50 or 0x6E9C10)
                            {
                                blade12SpriteSources.Add(task.ReadDword(0x10));
                                Assert.True(task.ReadWord(12) < task.ReadWord(0x1A));
                                if (task.Callback == 0x6E9C10)
                                {
                                    Assert.Equal((ushort)0, task.ReadWord(0x32));
                                    Assert.Equal(new byte[8], state.Read(NativeBattleBladeShot12Profile.SourcePointGlobal, 8));
                                }
                            }
                            if (task.Callback == 0x6E9D60 && task.ReadWord(12) == 1 && oldAge >= 0)
                            {
                                Assert.Equal(unchecked((ushort)(oldAge < 8 ? oldAge << 9 : 4096)), task.ReadWord(0x16));
                            }
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end >= limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (blade12PacketBanks.Add(start))
                        {
                            if (blade12PacketBanks.Count == 1)
                            {
                                Assert.Equal(NativeBattleBladeShot12Profile.PacketBankBytes, start);
                            }
                            ColdNext("blade12-private-bank-" + start.ToString("X"), current);
                        }
                        foreach (var replayGroup in current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "2546100" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                        {
                            NativeBattleLogicalCommand command = replayGroup.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in replayGroup)
                            {
                                Assert.Equal(command.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(command.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            byte[] physical = state.Packets.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                            if ((command.Opcode & 0xFC) is 0x24 or 0x2C or 0x30 or 0x38)
                            {
                                Assert.Contains(command.VertexPayload4, depth => depth > 0);
                            }
                        }
                    }
                }
                if (effectId == 12 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && blade12Boundaries.Add("all-three-banks-detached"))
                {
                    NativeBattleBladeShot12State state = current.NativeEffectMemory!.BladeShot12!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildABankStorage);
                    Assert.NotNull(state.ChildBBankStorage);
                    NativeBattleModulePoolTask oldRoot = Assert.Single(before.Pools[0].Slots, task => (task.Flags & 1) != 0);
                    Assert.True(oldRoot.ReadWord(12) >= 65);
                    Assert.Equal(oldRoot.ReadWord(12), BitConverter.ToUInt16(state.RootBankStorage.Read(12, 2)));
                    foreach ((NativeBattleEffectArena arena, int stride, int count) in new[] { (state.RootBankStorage!, 100, 1), (state.ChildABankStorage!, 32, 16), (state.ChildBBankStorage!, 52, 64) })
                    {
                        for (int slot = 0; slot < count; slot++)
                        {
                            Assert.Equal(0, BitConverter.ToUInt16(arena.Read(slot * stride, 2)) & 1);
                        }
                    }
                    Assert.Equal(0x80u, current.NativeGraphics!.Models[caster].SequenceState!.MeshMask124 & 0x80);
                    ColdNext("all-three-banks-detached", current);
                }
                if (effectId == 172 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleMorph172State state = current.NativeEffectMemory!.Morph172!;
                    Assert.Equal(new uint[] { NativeBattleMorph172Profile.RootDescriptor, NativeBattleMorph172Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleMorph172Profile.RootBank, NativeBattleMorph172Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMorph172Profile.MainCallback);
                    NativeBattleModulePoolTask? oldMain = before?.Pools.SingleOrDefault(item => item.DescriptorAddress == NativeBattleMorph172Profile.ChildDescriptor)?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMorph172Profile.MainCallback);
                    if (main is not null)
                    {
                        int age = main.ReadWord(12);
                        string? phase = age switch { 1 => "after-main-old0-anchor", 2 => "radii-and-two-deformed-meshes", 8 => "before-main-old8-body", 9 => "body-born", 30 => "mid-native-no-contact", 60 => "before-main-old60-retire", _ => null };
                        if (phase is not null && morph172Boundaries.Add(phase))
                        {
                            morph172Observations.Add(new { Boundary = phase, Main = main });
                            ColdNext(phase, current);
                        }
                        if (age >= 2 && morph172Boundaries.Add("physical-radii-and-mutable-XYZ6"))
                        {
                            NativeBattleMorph172Resources resources = original.Morph172Resources!;
                            for (int vertex = 0; vertex < NativeBattleMorph172Profile.VertexCount; vertex++)
                            {
                                byte[] point = resources.MeshA.Bytes.AsSpan().Slice(8 + vertex * 8, 6).ToArray();
                                int x = BinaryPrimitives.ReadInt16LittleEndian(point);
                                int y = BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2));
                                Assert.Equal(unchecked((uint)NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(x * x + y * y))), state.ReadDword(NativeBattleMorph172Profile.RadiiBase + (uint)(vertex * 4)));
                                Assert.Equal(point, state.Read(NativeBattleMorph172Profile.MutableVerticesA + (uint)(vertex * 8), 6));
                                Assert.Equal(point, state.Read(NativeBattleMorph172Profile.MutableVerticesB + (uint)(vertex * 8), 6));
                                Assert.Equal((ushort)0, state.ReadWord(NativeBattleMorph172Profile.MutableVerticesA + (uint)(vertex * 8 + 6)));
                                Assert.Equal((ushort)0, state.ReadWord(NativeBattleMorph172Profile.MutableVerticesB + (uint)(vertex * 8 + 6)));
                            }
                            ColdNext("physical-radii-and-mutable-XYZ6", current);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0 && task.Callback != NativeBattleMorph172Profile.MainCallback))
                    {
                        if (morph172Callbacks.Add(task.Callback))
                        {
                            ColdNext("morph172-callback-" + task.Callback.ToString("X"), current);
                        }
                    }
                    NativeBattleModulePoolTask? body = pool.Slots.FirstOrDefault(task => task.Callback == NativeBattleMorph172Profile.Callback5DA520 && task.ReadWord(12) is >= 1 and <= 56);
                    if (body is not null)
                    {
                        int oldAge = body.ReadWord(12) - 1;
                        if ((body.Flags & 1) != 0)
                        {
                            Assert.Equal(4, current.NativeGraphics!.Models[3].Flags0 & 4);
                        }
                        if (oldAge is 0 or 24 or 48 && morph172BodyAges.Add(oldAge))
                        {
                            morph172Observations.Add(AssertMorph172Body(prior, current, assets, original.Morph172Resources!, oldAge, body));
                            ColdNext("current-body220-old" + oldAge, current);
                        }
                        if ((body.Flags & 1) == 0 && !morph172BodyRestored)
                        {
                            Assert.Equal(0, current.NativeGraphics!.Models[3].Flags0 & 4);
                            morph172BodyRestored = true;
                            ColdNext("body-hide-bit-cleared", current);
                        }
                    }
                    int liveParticle = Enumerable.Range(0, NativeBattleMorph172Profile.ParticleCount).FirstOrDefault(row => state.ReadDword(NativeBattleMorph172Profile.ParticleBase + (uint)(row * 24)) != 0, -1);
                    if (liveParticle >= 0 && morph172Boundaries.Add("physical-particle-record-live"))
                    {
                        morph172Observations.Add(new { Boundary = "physical-particle-record-live", Row = liveParticle, Bytes = state.ReadView(NativeBattleMorph172Profile.ParticleBase + (uint)(liveParticle * 24), 24) });
                        ColdNext("physical-particle-record-live", current);
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (morph172PacketBanks.Add(start))
                        {
                            ColdNext("morph172-private-COMMON-bank-" + start.ToString("X"), current);
                        }
                        foreach (var group in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "239D6FC" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                        {
                            NativeBattleLogicalCommand command = group.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in group)
                            {
                                Assert.Equal(command.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(command.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            byte[] physical = state.Packets.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                        }
                    }
                }
                if (effectId == 172 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && morph172Boundaries.Add("all-banks-drained-detached"))
                {
                    NativeBattleMorph172State state = current.NativeEffectMemory!.Morph172!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(0, 2)) & 1);
                    NativeBattleEffectTaskPool oldChildren = before.Pools.Single(pool => pool.DescriptorAddress == NativeBattleMorph172Profile.ChildDescriptor);
                    if (!morph172BodyRestored)
                    {
                        int bodySlot = Enumerable.Range(0, oldChildren.Slots.Length).Single(index => (oldChildren.Slots[index].Flags & 1) != 0 && oldChildren.Slots[index].Callback == NativeBattleMorph172Profile.Callback5DA520);
                        Assert.Equal((ushort)55, oldChildren.Slots[bodySlot].ReadWord(12));
                        Assert.Equal((ushort)56, BitConverter.ToUInt16(state.ChildBankStorage.Read(bodySlot * 36 + 12, 2)));
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(bodySlot * 36, 2)) & 1);
                        Assert.Equal(0, current.NativeGraphics!.Models[3].Flags0 & 4);
                        morph172BodyRestored = true;
                        morph172Observations.Add(new { Boundary = "body-retirement-and-root-detach-same-callback", BeforeBodyAge = 55, RetainedBodyAge = 56, Flags0 = current.NativeGraphics.Models[3].Flags0 });
                        ColdNext("body-hide-bit-cleared", current);
                    }
                    int mainSlot = Enumerable.Range(0, oldChildren.Slots.Length).Single(index => oldChildren.Slots[index].Callback == NativeBattleMorph172Profile.MainCallback);
                    Assert.Equal((ushort)61, BitConverter.ToUInt16(state.ChildBankStorage.Read(mainSlot * 36 + 12, 2)));
                    for (int slot = 0; slot < NativeBattleMorph172Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * 36, 2)) & 1);
                    }
                    for (uint row = 0; row < NativeBattleMorph172Profile.ParticleCount; row++)
                    {
                        Assert.Equal(0u, state.ReadDword(NativeBattleMorph172Profile.ParticleBase + row * 24));
                    }
                    ColdNext("all-banks-drained-detached", current);
                }
                if (effectId == 170 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleAcid170State state = current.NativeEffectMemory!.Acid170!;
                    Assert.Equal(new uint[] { NativeBattleAcid170Profile.RootDescriptor, NativeBattleAcid170Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleAcid170Profile.RootBank, NativeBattleAcid170Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAcid170Profile.MainCallback);
                    NativeBattleModulePoolTask? oldMain = before?.Pools.SingleOrDefault(item => item.DescriptorAddress == NativeBattleAcid170Profile.ChildDescriptor)?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAcid170Profile.MainCallback);
                    if (main is not null)
                    {
                        int age = main.ReadWord(12);
                        string? phase = age switch { 1 => "before-main-old1", 2 => "curve-and-fixed-bones-born", 14 => "body-and-selected-mesh-born", 46 => "before-main-old46-single-contact", 55 => "before-main-old55-retire", _ => null };
                        if (phase is not null && acid170Boundaries.Add(phase))
                        {
                            acid170Observations.Add(new { Boundary = phase, Main = main });
                            ColdNext(phase, current);
                        }
                        if (age >= 2 && acid170Boundaries.Add("curve28-and-fixed-bone40-38"))
                        {
                            NativeBattleEffectArena control = state.ReadView(NativeBattleAcid170Profile.CurveBase + 16, 32);
                            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(4, control, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 400));
                            NativeBattleEffectArena samples = NativeBattleEffectArena.Clear();
                            for (int sample = 0; sample < 28; sample++)
                            {
                                samples = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(4, work, sample * 4096 / 27, samples, sample * 8);
                            }
                            Assert.Equal(samples.Read(0, 224), state.Read(NativeBattleAcid170Profile.CurveBase + 48, 224));
                            Assert.Equal((ushort)0, state.ReadWord(NativeBattleAcid170Profile.Bone38PointGlobal + 6));
                            Assert.Equal((ushort)0, state.ReadWord(NativeBattleAcid170Profile.Bone40PointGlobal + 6));
                            Assert.Contains(state.Read(NativeBattleAcid170Profile.Bone38PointGlobal, 6), value => value != 0);
                            Assert.Contains(state.Read(NativeBattleAcid170Profile.Bone40PointGlobal, 6), value => value != 0);
                            acid170Observations.Add(new { Boundary = "curve28-and-fixed-bone40-38", Curve = state.ReadView(NativeBattleAcid170Profile.CurveBase, NativeBattleAcid170Profile.CurveLength), Bone38 = state.ReadView(NativeBattleAcid170Profile.Bone38PointGlobal, 8), Bone40 = state.ReadView(NativeBattleAcid170Profile.Bone40PointGlobal, 8) });
                            ColdNext("curve28-and-fixed-bone40-38", current);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0 && task.Callback != NativeBattleAcid170Profile.MainCallback))
                    {
                        if (acid170Callbacks.Add(task.Callback))
                        {
                            ColdNext("acid170-callback-" + task.Callback.ToString("X"), current);
                        }
                    }
                    NativeBattleModulePoolTask? body = pool.Slots.FirstOrDefault(task => task.Callback == NativeBattleAcid170Profile.Callback5DE200 && task.ReadWord(12) is >= 1 and <= 35);
                    if (body is not null)
                    {
                        int oldAge = body.ReadWord(12) - 1;
                        if ((body.Flags & 1) != 0)
                        {
                            Assert.Equal(4, current.NativeGraphics!.Models[0].Flags0 & 4);
                        }
                        if (oldAge is 0 or 16 or 33 && acid170BodyAges.Add(oldAge))
                        {
                            Acid170BodyWitness witness = AssertAcid170Body(prior, current, assets, oldAge, body);
                            acid170Observations.Add(witness.Evidence);
                            acid170BodyPackets = witness.Packets;
                            ColdNext("recipient-body172-and-linked-old" + oldAge, current);
                        }
                        if ((body.Flags & 1) == 0 && !acid170BodyRestored)
                        {
                            Assert.Equal(acid170OriginalFlags!.Value & 4, current.NativeGraphics!.Models[0].Flags0 & 4);
                            acid170BodyRestored = true;
                            ColdNext("recipient-saved-hide-bit-restored", current);
                        }
                    }
                    NativeBattleModulePoolTask? selected = pool.Slots.FirstOrDefault(task => task.Callback == NativeBattleAcid170Profile.Callback5DD030 && task.ReadWord(12) is >= 1 and <= 32);
                    if (selected is not null)
                    {
                        int oldAge = selected.ReadWord(12) - 1;
                        if (oldAge is 0 or 8 or 16 or 24 or 31 && acid170MeshAges.Add(oldAge))
                        {
                            int ratio = NativeBattleCameraMath.Sine((oldAge << 10) / 32);
                            NativeBattleAcid170Resources resources = original.Acid170Resources!;
                            for (int vertex = 0; vertex < NativeBattleAcid170Profile.VertexCount; vertex++)
                            {
                                for (int axis = 0; axis < 3; axis++)
                                {
                                    int offset = vertex * 8 + axis * 2;
                                    int first = BinaryPrimitives.ReadInt16LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(8 + offset));
                                    int second = BinaryPrimitives.ReadInt16LittleEndian(resources.EndpointVertices.AsSpan().Slice(offset));
                                    Assert.Equal(unchecked((ushort)(first + (unchecked((second - first) * ratio) >> 12))), state.ReadWord(NativeBattleAcid170Profile.SelectedVerticesBase + (uint)offset));
                                }
                                Assert.Equal((ushort)0, state.ReadWord(NativeBattleAcid170Profile.SelectedVerticesBase + (uint)(vertex * 8 + 6)));
                            }
                            acid170Observations.Add(new { Boundary = "selected73-native-phase", OldAge = oldAge, Flags = oldAge < 8 || oldAge >= 32 ? 0x2033 : oldAge < 16 ? 0x20F3 : 0x24F3, Ratio = ratio, Bytes = state.ReadView(NativeBattleAcid170Profile.SelectedVerticesBase, NativeBattleAcid170Profile.SelectedVerticesLength) });
                            ColdNext("selected73-phase-old" + oldAge, current);
                        }
                    }
                    foreach (uint flag in new uint[] { 1, 2 })
                    {
                        int row = Enumerable.Range(0, NativeBattleAcid170Profile.ParticleCount).FirstOrDefault(index => (state.ReadDword(NativeBattleAcid170Profile.ParticleBase + (uint)(index * 24)) & flag) != 0, -1);
                        if (row >= 0 && acid170ParticleFlags.Add(flag))
                        {
                            acid170Observations.Add(new { Boundary = "masked-shared-particle-consumer", Flag = flag, Row = row, Bytes = state.ReadView(NativeBattleAcid170Profile.ParticleBase + (uint)(row * 24), 24) });
                            ColdNext("shared-particle-mask-" + flag, current);
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (acid170PacketBanks.Add(start))
                        {
                            ColdNext("acid170-private-COMMON-bank-" + start.ToString("X"), current);
                        }
                        foreach (var group in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                        {
                            NativeBattleLogicalCommand command = group.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in group)
                            {
                                Assert.Equal(command.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(command.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            if (command.Opcode == 0xE1 && command.Words.Length == 1 && command.Words[0] == 0xE1000220)
                            {
                                NativeBattleLogicalCommand polygon = current.NativeGraphics.PrimitiveFrame.LastCommands.Select(item => item.Command).First(item => item.Packet == command.Packet && item.WordOffset == 1);
                                byte primitiveGroup = unchecked((byte)(polygon.Opcode & 0xFC));
                                Assert.Contains(primitiveGroup, new byte[] { 0x24, 0x2C, 0x3C });
                                int words = primitiveGroup switch { 0x24 => 8, 0x2C => 10, _ => 13 };
                                Assert.Equal(words, command.WordOffset);
                                Assert.Equal(unchecked((byte)words), current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + 3, 1)[0]);
                                Assert.Contains(polygon.VertexPayload4, depth => depth > 0);
                                acid170E1Groups.Add(primitiveGroup);
                            }
                            byte[] physical = current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                        }
                    }
                }
                if (effectId == 170 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && acid170Boundaries.Add("all-banks-drained-detached"))
                {
                    NativeBattleAcid170State state = current.NativeEffectMemory!.Acid170!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(0, 2)) & 1);
                    NativeBattleEffectTaskPool oldChildren = before.Pools.Single(pool => pool.DescriptorAddress == NativeBattleAcid170Profile.ChildDescriptor);
                    int mainSlot = Enumerable.Range(0, oldChildren.Slots.Length).Single(index => oldChildren.Slots[index].Callback == NativeBattleAcid170Profile.MainCallback);
                    Assert.Equal((ushort)56, BitConverter.ToUInt16(state.ChildBankStorage.Read(mainSlot * 36 + 12, 2)));
                    for (int slot = 0; slot < NativeBattleAcid170Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * 36, 2)) & 1);
                    }
                    for (uint row = 0; row < NativeBattleAcid170Profile.ParticleCount; row++)
                    {
                        Assert.Equal(0u, state.ReadDword(NativeBattleAcid170Profile.ParticleBase + row * 24));
                    }
                    ColdNext("all-banks-drained-detached", current);
                }
                if (effectId == 171 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattlePoisonGas171State state = current.NativeEffectMemory!.PoisonGas171!;
                    Assert.Equal(new uint[] { NativeBattlePoisonGas171Profile.RootDescriptor, NativeBattlePoisonGas171Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattlePoisonGas171Profile.RootBank, NativeBattlePoisonGas171Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattlePoisonGas171Profile.MainCallback);
                    NativeBattleModulePoolTask? oldMain = before?.Pools.SingleOrDefault(item => item.DescriptorAddress == NativeBattlePoisonGas171Profile.ChildDescriptor)?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattlePoisonGas171Profile.MainCallback);
                    if (main is not null)
                    {
                        int age = main.ReadWord(12);
                        string? phase = age switch { 1 => "before-main-old1", 2 => "body-and-four-smoke-born", 8 => "before-main-old8-mesh", 17 => "before-main-old17-stage-hold", 27 => "before-main-old27-recipients", 30 => "before-main-old30-counted-contact", 42 => "before-main-old42-stage-fade", 50 => "before-main-old50-retire", _ => null };
                        if (phase is not null && poison171Boundaries.Add(phase))
                        {
                            poison171Observations.Add(new { Boundary = phase, Main = main });
                            ColdNext(phase, current);
                        }
                        if (oldMain is not null)
                        {
                            int oldAge = oldMain.ReadWord(12);
                            int? stageValue = oldAge <= 16 ? oldAge << 7 : oldAge >= 42 ? (50 - oldAge) << 8 : null;
                            if (stageValue is int value)
                            {
                                Assert.All(current.NativeStage!.Groups, group =>
                                {
                                    Assert.Equal(unchecked((short)value), group.ColorDepth);
                                    Assert.Equal(0u, group.ColorFlags);
                                });
                            }
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0 && task.Callback != NativeBattlePoisonGas171Profile.MainCallback))
                    {
                        if (poison171Callbacks.Add(task.Callback))
                        {
                            ColdNext("poison171-callback-" + task.Callback.ToString("X"), current);
                        }
                    }
                    NativeBattleModulePoolTask? body = pool.Slots.FirstOrDefault(task => task.Callback == NativeBattlePoisonGas171Profile.Callback5DB620 && task.ReadWord(12) is >= 1 and <= 24);
                    if (body is not null)
                    {
                        int oldAge = body.ReadWord(12) - 1;
                        if ((body.Flags & 1) != 0)
                        {
                            Assert.Equal(4, current.NativeGraphics!.Models[3].Flags0 & 4);
                        }
                        if (oldAge is 0 or 12 or 23 && poison171BodyAges.Add(oldAge))
                        {
                            poison171Observations.Add(AssertPoisonGas171Body(prior, current, assets, oldAge));
                            ColdNext("current-body-E2-pairs-old" + oldAge, current);
                        }
                        if ((body.Flags & 1) == 0 && !poison171BodyRestored)
                        {
                            Assert.Equal(poison171OriginalFlags!.Value & 4, current.NativeGraphics!.Models[3].Flags0 & 4);
                            poison171BodyRestored = true;
                            ColdNext("body-hide-bit-restored", current);
                        }
                    }
                    foreach (uint bank in new uint[] { NativeBattlePoisonGas171Profile.ParticleABase, NativeBattlePoisonGas171Profile.ParticleBBase })
                    {
                        int row = Enumerable.Range(0, NativeBattlePoisonGas171Profile.ParticleCount).FirstOrDefault(index => state.ReadDword(bank + (uint)(index * 24)) != 0, -1);
                        if (row >= 0 && poison171ParticleBanks.Add(bank))
                        {
                            poison171Observations.Add(new { Boundary = "physical-particle-bank", Bank = bank, Row = row, Bytes = state.ReadView(bank + (uint)(row * 24), 24) });
                            ColdNext("particle-bank-" + bank.ToString("X"), current);
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (poison171PacketBanks.Add(start))
                        {
                            ColdNext("poison171-private-COMMON-bank-" + start.ToString("X"), current);
                        }
                        foreach (var group in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                        {
                            NativeBattleLogicalCommand command = group.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in group)
                            {
                                Assert.Equal(command.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(command.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            byte[] physical = current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                        }
                    }
                }
                if (effectId == 171 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && poison171Boundaries.Add("all-banks-drained-detached"))
                {
                    NativeBattlePoisonGas171State state = current.NativeEffectMemory!.PoisonGas171!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(0, 2)) & 1);
                    NativeBattleEffectTaskPool oldChildren = before.Pools.Single(pool => pool.DescriptorAddress == NativeBattlePoisonGas171Profile.ChildDescriptor);
                    int mainSlot = Enumerable.Range(0, oldChildren.Slots.Length).Single(index => oldChildren.Slots[index].Callback == NativeBattlePoisonGas171Profile.MainCallback);
                    Assert.Equal((ushort)51, BitConverter.ToUInt16(state.ChildBankStorage.Read(mainSlot * 36 + 12, 2)));
                    for (int slot = 0; slot < NativeBattlePoisonGas171Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * 36, 2)) & 1);
                    }
                    for (uint row = 0; row < NativeBattlePoisonGas171Profile.ParticleCount; row++)
                    {
                        Assert.Equal(0u, state.ReadDword(NativeBattlePoisonGas171Profile.ParticleABase + row * 24));
                        Assert.Equal(0u, state.ReadDword(NativeBattlePoisonGas171Profile.ParticleBBase + row * 24));
                    }
                    ColdNext("all-banks-drained-detached", current);
                }
                if (effectId == 181 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleAdditional181State state = current.NativeEffectMemory!.Additional181!;
                    Assert.Equal(new uint[] { NativeBattleAdditional181Profile.RootDescriptor, NativeBattleAdditional181Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleAdditional181Profile.RootBank, NativeBattleAdditional181Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional181Profile.MainCallback);
                    NativeBattleModulePoolTask? oldMain = before?.Pools.SingleOrDefault(item => item.DescriptorAddress == NativeBattleAdditional181Profile.ChildDescriptor)?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional181Profile.MainCallback);
                    if (main is not null)
                    {
                        int age = main.ReadWord(12);
                        string? phase = age switch { 1 => "after-main-old0-bone-XYZ", 4 => "before-main-old4-mouth", 6 => "mouth-bursts-completed", 14 => "before-main-old14-flash", 16 => "after-main-old15-anchor", 17 => "rings-and-particles-started", 38 => "before-main-old38-single-contact", 50 => "before-main-old50-retire", _ => null };
                        if (phase is not null && additional181Boundaries.Add(phase))
                        {
                            additional181Observations.Add(new { Boundary = phase, Main = main });
                            ColdNext(phase, current);
                        }
                        if (age == 1)
                        {
                            Assert.Equal((ushort)5, main.ReadWord(14));
                            Assert.Equal((ushort)0, main.ReadWord(22));
                        }
                        if (oldMain?.ReadWord(12) == 15)
                        {
                            Assert.Equal(unchecked((ushort)prior.NativeGraphics!.Models[0].Transform!.Angle36), main.ReadWord(22));
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0 && task.Callback != NativeBattleAdditional181Profile.MainCallback))
                    {
                        if (additional181Callbacks.Add(task.Callback))
                        {
                            ColdNext("additional181-callback-" + task.Callback.ToString("X"), current);
                        }
                    }
                    NativeBattleModulePoolTask? body = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional181Profile.Callback5CB9B0 && task.ReadWord(12) is >= 1 and <= 15);
                    if (body is not null)
                    {
                        int oldAge = body.ReadWord(12) - 1;
                        Assert.Equal(0x804, current.NativeGraphics!.Models[0].Flags0 & 0x804);
                        if (oldAge is 0 or 7 or 14 && additional181BodyAges.Add(oldAge))
                        {
                            List<Additional181Pair> pairs = Additional181Pairs(current);
                            Assert.NotEmpty(pairs);
                            additional181Observations.Add(AssertAdditional181Body(prior, current, assets, oldAge, pairs));
                            ColdNext("caster-and-linked-paired-body-old" + oldAge, current);
                        }
                    }
                    foreach (uint flag in new uint[] { 2, 4 })
                    {
                        int row = Enumerable.Range(0, NativeBattleAdditional181Profile.ParticleCount).FirstOrDefault(index => (state.ReadDword(NativeBattleAdditional181Profile.ParticleBase + (uint)(index * 24)) & flag) != 0, -1);
                        if (row >= 0 && additional181ParticleFlags.Add(flag))
                        {
                            uint address = NativeBattleAdditional181Profile.ParticleBase + (uint)(row * 24);
                            additional181Observations.Add(new { Boundary = "shared-particle-flag" + flag, Row = row, Bytes = state.ReadView(address, 24) });
                            ColdNext("shared-particle-flag" + flag, current);
                        }
                    }
                    NativeBattleModuleTask? flash = current.NativeGraphics!.ModuleEffects.Slots.FirstOrDefault(task => task.Kind == NativeBattleModuleKind.CriticalFlash571300 && BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan()[2..]) == 0x410 && BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan()[16..]) == 255);
                    if (flash is not null)
                    {
                        if ((flash.Flags & 1) != 0 && !additional181FlashSeen)
                        {
                            additional181FlashSeen = true;
                            Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(flash.Payload.AsSpan()));
                            additional181Observations.Add(new { Boundary = "native-flash-0-1-4-255", Flash = flash });
                            ColdNext("native-flash-queued", current);
                        }
                        if (additional181FlashSeen && (flash.Flags & 1) == 0 && !additional181FlashCompleted)
                        {
                            additional181FlashCompleted = true;
                            Assert.Equal((short)6, BinaryPrimitives.ReadInt16LittleEndian(flash.Payload.AsSpan()));
                            ColdNext("native-flash-completed", current);
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (additional181PacketBanks.Add(start))
                        {
                            ColdNext("additional181-private-COMMON-bank-" + start.ToString("X"), current);
                        }
                        foreach (var group in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                        {
                            NativeBattleLogicalCommand command = group.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in group)
                            {
                                Assert.Equal(command.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(command.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            byte[] physical = current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                        }
                    }
                }
                if (effectId == 181 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && additional181Boundaries.Add("all-banks-drained-detached"))
                {
                    NativeBattleAdditional181State state = current.NativeEffectMemory!.Additional181!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(0, 2)) & 1);
                    NativeBattleEffectTaskPool oldChildren = before.Pools.Single(pool => pool.DescriptorAddress == NativeBattleAdditional181Profile.ChildDescriptor);
                    int mainSlot = Enumerable.Range(0, oldChildren.Slots.Length).Single(index => oldChildren.Slots[index].Callback == NativeBattleAdditional181Profile.MainCallback);
                    Assert.Equal((ushort)51, BitConverter.ToUInt16(state.ChildBankStorage.Read(mainSlot * 36 + 12, 2)));
                    for (int slot = 0; slot < NativeBattleAdditional181Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * 36, 2)) & 1);
                    }
                    for (uint row = 0; row < NativeBattleAdditional181Profile.ParticleCount; row++)
                    {
                        Assert.Equal(0u, state.ReadDword(NativeBattleAdditional181Profile.ParticleBase + row * 24));
                    }
                    ColdNext("all-banks-drained-detached", current);
                }
                if (effectId == 182 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleAdditional182State state = current.NativeEffectMemory!.Additional182!;
                    Assert.Equal(new uint[] { NativeBattleAdditional182Profile.RootDescriptor, NativeBattleAdditional182Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleAdditional182Profile.RootBank, NativeBattleAdditional182Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    Assert.Throws<InvalidDataException>(() => state.ReadWord(NativeBattleAdditional182Profile.ChildBank));
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleEffectTaskPool? oldPool = before?.Pools.SingleOrDefault(item => item.DescriptorAddress == NativeBattleAdditional182Profile.ChildDescriptor);
                    bool set = (contact.Primary.HitFlags3 & 4) != 0;
                    uint mainCallback = set ? NativeBattleAdditional182Profile.MainSetCallback : NativeBattleAdditional182Profile.MainClearCallback;
                    int contactAge = set ? 35 : 64;
                    int terminal = set ? 40 : 65;
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == mainCallback);
                    NativeBattleModulePoolTask? oldMain = oldPool?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == mainCallback);
                    if (main is not null)
                    {
                        int age = main.ReadWord(12);
                        string? phase = age is 0 or 1 ? "before-main-old" + age : age == contactAge ? "before-native-contact" : age == terminal ? "before-director-retire" : !set && age is 20 or 24 or 26 ? "before-main-old" + age : null;
                        if (phase is not null && additional182Boundaries.Add(phase))
                        {
                            additional182Observations.Add(new { Boundary = phase, Age = age, Model = current.NativeGraphics!.Models[contactActor], History = state.ReadView(NativeBattleAdditional182Profile.HistoryAddress, NativeBattleAdditional182Profile.HistoryBytes) });
                            ColdNext(phase, current);
                        }
                        if (age == 1)
                        {
                            Assert.Equal((ushort)(additional182OriginalFlags!.Value & 8), main.ReadWord(14));
                            Assert.Equal(0, current.NativeGraphics!.Models[contactActor].Flags0 & 8);
                        }
                    }
                    if (oldPool is null || oldMain?.ReadWord(12) == 0)
                    {
                        NativeBattleAdditional182State oldState = oldPool is null ? source.NativeEffectMemory!.Additional182! : prior.NativeEffectMemory!.Additional182!;
                        foreach ((uint bank, int count) in new (uint, int)[] { (NativeBattleAdditional182Profile.ParticleABase, 120), (NativeBattleAdditional182Profile.ParticleBBase, 100) })
                        {
                            for (int index = 0; index < count; index++)
                            {
                                uint address = bank + (uint)(index * 24);
                                Assert.Equal(0u, state.ReadDword(address));
                                Assert.Equal(JsonSerializer.Serialize(oldState.ReadView(address + 4, 20)), JsonSerializer.Serialize(state.ReadView(address + 4, 20)));
                            }
                        }
                        additional182Boundaries.Add("entry-head-only-120A-100B");
                    }
                    if (oldMain is not null && oldMain.ReadWord(12) == contactAge)
                    {
                        Assert.Equal((ushort)(prior.NativeGraphics!.Models[contactActor].Flags0 | oldMain.ReadWord(14)), current.NativeGraphics!.Models[contactActor].Flags0);
                        Assert.Equal(contactAge + 1, main!.ReadWord(12));
                        additional182Boundaries.Add("current-flags-bit8-OR-contact");
                    }
                    if (oldMain is not null && oldMain.ReadWord(12) == terminal)
                    {
                        NativeBattleModulePoolTask retired = pool.Slots.Single(task => task.Callback == mainCallback);
                        Assert.Equal(terminal + 1, retired.ReadWord(12));
                        Assert.Equal(0, retired.Flags & 1);
                        additional182Boundaries.Add("director-retired-at-" + (terminal + 1));
                        ColdNext("director-retired-children-live", current);
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0 && task.Callback != mainCallback))
                    {
                        if (additional182Callbacks.Add(task.Callback))
                        {
                            ColdNext("additional-callback-" + task.Callback.ToString("X"), current);
                        }
                    }
                    for (int row = 0; row < NativeBattleAdditional182Profile.HistoryCount; row++)
                    {
                        uint address = NativeBattleAdditional182Profile.HistoryAddress + (uint)(row * 36);
                        if (state.ReadDword(address) != 0 && additional182Boundaries.Add("history-current-matrix"))
                        {
                            (byte[] bytes, bool[] known) = state.ReadView(address + 4, 32).ReadSnapshot(0, 32);
                            Assert.All(known.AsSpan(0, 18).ToArray(), Assert.True);
                            Assert.False(known[18]);
                            Assert.False(known[19]);
                            Assert.All(known.AsSpan(20, 12).ToArray(), Assert.True);
                            additional182Observations.Add(new { Boundary = "history-current-matrix", Row = row, Data = bytes, Known = known });
                            ColdNext("history-current-matrix", current);
                        }
                    }
                    if (!set && oldMain?.ReadWord(12) == 26)
                    {
                        Assert.DoesNotContain(oldPool!.Slots, task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional182Profile.Callback5CB130);
                        NativeBattleModulePoolTask history = pool.Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional182Profile.Callback5CB130);
                        Assert.Equal((ushort)1, history.ReadWord(12));
                        int rewritten = 0;
                        for (int row = 0; row < 5; row++)
                        {
                            if (row != rewritten)
                            {
                                uint address = NativeBattleAdditional182Profile.HistoryAddress + (uint)(row * 36);
                                Assert.Equal(0u, state.ReadDword(address));
                                Assert.Equal(JsonSerializer.Serialize(prior.NativeEffectMemory!.Additional182!.ReadView(address + 4, 32)), JsonSerializer.Serialize(state.ReadView(address + 4, 32)));
                            }
                        }
                        additional182Boundaries.Add("history-head-only-clear-with-same-scan-write");
                    }
                    NativeBattleModulePoolTask? oldFade = oldPool?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional182Profile.Callback5CB390);
                    NativeBattleModulePoolTask? fade = pool.Slots.FirstOrDefault(task => task.Callback == NativeBattleAdditional182Profile.Callback5CB390);
                    if (fade is not null && (oldFade is not null || (fade.Flags & 1) != 0))
                    {
                        int oldAge = oldFade?.ReadWord(12) ?? 0;
                        int factor = 4096 - NativeBattleCameraMath.Sine((oldAge << 10) / fade.ReadWord(24));
                        NativeBattleSummonColors colors = source.Definition.NativeGraphics!.Rules.SummonColors!;
                        NativeBattleModelRendererRegisters far = prior.NativeEffectMemory!.PrimitiveProcess!.ModelRendererRegisters!;
                        NativeBattleScriptedDpcsResult normal = NativeBattleScriptedDpcs.Transform45F270(prior.NativeEffectMemory.ScriptedColorRegisters!, 0x32000000, unchecked((uint)factor), Additional182Far(colors.Normal, far), colors);
                        NativeBattleScriptedDpcsResult shadow = NativeBattleScriptedDpcs.Transform45F270(normal.Registers, 0, normal.Registers.Ir0!.Value, Additional182Far(colors.Transition, far), colors);
                        NativeBattleGraphicsModel model = current.NativeGraphics!.Models[contactActor];
                        Assert.Equal(normal.PackedRgb, model.Color44);
                        Assert.Equal(unchecked((byte)((uint)(factor * 255) >> 12)), model.Alpha7);
                        ushort flags = prior.NativeGraphics!.Models[contactActor].Flags0;
                        if (oldAge == 0)
                        {
                            flags |= 0x800;
                        }
                        if (oldAge + 1 >= fade.ReadWord(24))
                        {
                            flags = unchecked((ushort)((flags & ~0x800) | fade.ReadWord(26) | 4));
                            additional182Boundaries.Add("fade-restored-current0800-and-set4");
                        }
                        Assert.Equal(flags, model.Flags0);
                        NativeBattleStageColors stageColors = prior.NativeStage!.ModelColors!;
                        Assert.Equal(stageColors, current.NativeStage!.ModelColors);
                        NativeBattleModelColorOverrides overrides = prior.NativeEffectMemory.ModelColorOverrides!;
                        Assert.All(overrides.Slots, slot => Assert.Equal((byte?)255, slot.Actor));
                        NativeBattleGraphicsModel afterFade = prior.NativeGraphics.Models[contactActor] with { Flags0 = flags, Color44 = normal.PackedRgb, Color40 = (shadow.PackedRgb & 0xFFFFFF) | 0x02000000, Alpha7 = unchecked((byte)((uint)(factor * 255) >> 12)) };
                        NativeBattleModelColorStep afterModelColor = NativeBattleModelColors.Advance50A0A0(afterFade, prior.NativeGraphics.Flags, colors with { Transition = stageColors.Color40, Normal = stageColors.Color44 }, overrides);
                        Assert.Equal(afterModelColor.Model.Color40, model.Color40);
                        if (fade.Next is null && (fade.Flags & 1) != 0)
                        {
                            NativeBattleScriptedColorRegisters actual = current.NativeEffectMemory.ScriptedColorRegisters!;
                            Assert.Equal(shadow.Registers.Mac.ToArray(), actual.Mac.ToArray());
                            Assert.Equal(shadow.Registers.Ir.ToArray(), actual.Ir.ToArray());
                            Assert.Equal(normal.PackedRgb, actual.Rgb[1]);
                            Assert.Equal(shadow.PackedRgb, actual.Rgb[2]);
                            Assert.Equal(shadow.Registers.Flags, actual.Flags);
                            Assert.Equal(shadow.Registers.Material, actual.Material);
                            Assert.Equal(shadow.Registers.Ir0, actual.Ir0);
                            NativeBattleStageGroup finalStageGroup = current.NativeStage!.Groups.Last(group => (group.Flags & 3) == 3);
                            NativeBattleModelRendererRegisters expectedFar = Additional182Far(finalStageGroup.ColorFlags, far);
                            NativeBattleModelRendererRegisters actualFar = current.NativeEffectMemory.PrimitiveProcess!.ModelRendererRegisters!;
                            Assert.Equal(expectedFar.FarRed, actualFar.FarRed);
                            Assert.Equal(expectedFar.FarGreen, actualFar.FarGreen);
                            Assert.Equal(expectedFar.FarBlue, actualFar.FarBlue);
                            additional182DpcsPublished = true;
                        }
                        if (additional182Boundaries.Add("fade-old" + oldAge))
                        {
                            additional182Observations.Add(new { Boundary = "fade-old" + oldAge, Cue = factor, FadeShadow = afterFade.Color40, ModelListColor = afterModelColor.Model.Color40, StageModelColors = stageColors, model.Color44, model.Color40, model.Alpha7, model.Flags0, Registers = current.NativeEffectMemory.ScriptedColorRegisters });
                            ColdNext("fade-old" + oldAge, current);
                        }
                    }
                    foreach ((uint bank, int count) in new (uint, int)[] { (NativeBattleAdditional182Profile.ParticleABase, 120), (NativeBattleAdditional182Profile.ParticleBBase, 100) })
                    {
                        int record = Enumerable.Range(0, count).FirstOrDefault(index => state.ReadDword(bank + (uint)(index * 24)) != 0, -1);
                        if (record >= 0 && additional182ParticleBanks.Add(bank))
                        {
                            additional182Observations.Add(new { Boundary = "particle-bank-live", Bank = bank, Record = record, Bytes = state.ReadView(bank + (uint)(record * 24), 24) });
                            ColdNext("particle-bank-" + bank.ToString("X"), current);
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (additional182PacketBanks.Add(start))
                        {
                            ColdNext("private-COMMON-packet-bank-" + start.ToString("X"), current);
                        }
                        foreach (IGrouping<(NativeBattlePrimitiveReference Packet, int WordOffset), NativeBattleLogicalResolvedCommand> group in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                        {
                            NativeBattleLogicalCommand command = group.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in group)
                            {
                                Assert.Equal(command.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(command.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            byte[] physical = current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                            for (int word = 0; word < command.Words.Length; word++)
                            {
                                uint mask = Additional182ConsumedMask(command.Opcode, word);
                                Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                            }
                            if ((command.Opcode & 0xFC) == 0x28 && additional182F4Opcodes.Add(command.Opcode))
                            {
                                Assert.Equal(1, command.WordOffset);
                                Assert.Equal(5, command.Words.Length);
                                Assert.Equal(4, command.VertexPayload4.Length);
                                Assert.Equal(0x05000000u, BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena.Read(command.Packet.ByteOffset, 4)) & 0xFF000000u);
                                additional182Observations.Add(new { Boundary = "history-group1-F4", command.Opcode, command.Packet, command.WordOffset, Words = command.Words.ToArray(), PhysicalBytes = physical });
                                ColdNext("history-F4-opcode-" + command.Opcode.ToString("X"), current);
                            }
                        }
                    }
                }
                if (effectId == 182 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && additional182Boundaries.Add("all-banks-drained-detached"))
                {
                    NativeBattleAdditional182State state = current.NativeEffectMemory!.Additional182!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    bool set = (contact.Primary.HitFlags3 & 4) != 0;
                    uint callback = set ? NativeBattleAdditional182Profile.MainSetCallback : NativeBattleAdditional182Profile.MainClearCallback;
                    int terminal = set ? 40 : 65;
                    NativeBattleEffectTaskPool oldChildren = before.Pools.Single(pool => pool.DescriptorAddress == NativeBattleAdditional182Profile.ChildDescriptor);
                    int mainSlot = Enumerable.Range(0, oldChildren.Slots.Length).Single(index => oldChildren.Slots[index].Callback == callback);
                    Assert.Equal(terminal + 1, BitConverter.ToUInt16(state.ChildBankStorage.Read(mainSlot * 36 + 12, 2)));
                    additional182Boundaries.Add("director-retired-at-" + (terminal + 1));
                    Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(0, 2)) & 1);
                    for (int slot = 0; slot < NativeBattleAdditional182Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * 36, 2)) & 1);
                    }
                    foreach ((uint bank, int count) in new (uint, int)[] { (NativeBattleAdditional182Profile.ParticleABase, 120), (NativeBattleAdditional182Profile.ParticleBBase, 100) })
                    {
                        for (int index = 0; index < count; index++)
                        {
                            Assert.Equal(0u, state.ReadDword(bank + (uint)(index * 24)));
                        }
                    }
                    ColdNext("all-banks-drained-detached", current);
                }
                if (effectId == 186 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleBlaster186State state = current.NativeEffectMemory!.Blaster186!;
                    Assert.Equal(new uint[] { NativeBattleBlaster186Profile.RootDescriptor, NativeBattleBlaster186Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleBlaster186Profile.RootBank, NativeBattleBlaster186Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new int[] { 2, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    Assert.Throws<InvalidDataException>(() => state.ReadWord(NativeBattleBlaster186Profile.ChildBank));
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBlaster186Profile.MainCallback);
                    if (main is not null)
                    {
                        string? phase = main.ReadWord(12) switch { 1 => "sixteen-morph-frames-before-main-old1", 7 => "before-main-old7-beam", 39 => "before-main-old39-particles", 40 => "before-main-old40-pulse", 60 => "before-main-old60-contact", 65 => "before-main-old65-retire", _ => null };
                        if (phase is not null && blaster186Boundaries.Add(phase))
                        {
                            Assert.Equal(blaster186FramesW, Blaster186FrameWordLanes(state));
                            Assert.Equal(blaster186MatrixPadding, state.Read(NativeBattleBlaster186Profile.MatrixAddress + 18, 14));
                            if (main.ReadWord(12) == 1)
                            {
                                foreach (uint frameAddress in NativeBattleBlaster186Profile.MutableFrameAddresses)
                                {
                                    (byte[] data, bool[] known) = state.ReadView(frameAddress, NativeBattleBlaster186Profile.VertexFrameBytes).ReadSnapshot(0, NativeBattleBlaster186Profile.VertexFrameBytes);
                                    for (int vertex = 0; vertex < NativeBattleBlaster186Profile.VertexCount; vertex++)
                                    {
                                        Assert.All(known.Skip(vertex * 8).Take(6), value => Assert.True(value));
                                    }
                                    blaster186Observations.Add(new { FrameAddress = frameAddress, Data = data, Known = known });
                                }
                            }
                            ColdNext(phase, current);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0 && task.Callback != NativeBattleBlaster186Profile.MainCallback))
                    {
                        if (blaster186Callbacks.Add(task.Callback))
                        {
                            string name = $"callback-{task.Callback:X}-live";
                            blaster186Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    NativeBattleModulePoolTask? morph = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBlaster186Profile.Callback5C0280 && task.ReadWord(12) > 0);
                    if (morph is not null)
                    {
                        int index = (morph.ReadWord(26) - 1) % 20;
                        uint address = original.Blaster186Resources!.VertexPointers[index];
                        blaster186VertexSelections.Add(address);
                        bool mutable = NativeBattleBlaster186Profile.MutableFrameAddresses.Contains(address);
                        string phase = mutable ? "current-mutable-mesh-selected" : "original-immutable-mesh-selected";
                        if (blaster186Boundaries.Add(phase))
                        {
                            NativeBattleEffectArena vertices = mutable ? state.ReadView(address, NativeBattleBlaster186Profile.VertexFrameBytes) : original.Blaster186Resources.ReadSourceVertices(address);
                            (byte[] data, bool[] known) = vertices.ReadSnapshot(0, NativeBattleBlaster186Profile.VertexFrameBytes);
                            blaster186Observations.Add(new { Boundary = phase, SelectionIndex = index, VertexAddress = address, Data = data, Known = known });
                            Assert.Equal(blaster186FramesW, Blaster186FrameWordLanes(state));
                            ColdNext(phase, current);
                        }
                        if (morph.ReadWord(12) > morph.ReadWord(32) + 1 && blaster186Boundaries.Add("selected-mesh-20F3-cue-live"))
                        {
                            blaster186Observations.Add(new { Boundary = "selected-mesh-20F3-cue-live", OldAge = morph.ReadWord(12) - 1, VertexAddress = address, Flags = 0x20F3, Cue = (4096 / morph.ReadWord(34)) * (morph.ReadWord(12) - 1 - morph.ReadWord(32)) });
                            ColdNext("selected-mesh-20F3-cue-live", current);
                        }
                    }
                    NativeBattleModulePoolTask? mesh = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBlaster186Profile.Callback5C0750 && task.ReadWord(12) >= 2);
                    if (mesh is not null)
                    {
                        int oldAge = mesh.ReadWord(12) - 1;
                        string phase = oldAge >= 17 ? "mesh-14F3-UV-page-cue-live" : "mesh-1433-UV-page-live";
                        if (blaster186Boundaries.Add(phase))
                        {
                            blaster186Observations.Add(new { Boundary = phase, OldAge = oldAge, Flags = oldAge >= 16 ? 0x14F3 : 0x1433, TPage = (~(oldAge % 8)) & 1, UV = original.Blaster186Resources!.Colors[oldAge & 7], Cue = oldAge >= 16 ? (int?)((oldAge - 16) << 9) : null });
                            ColdNext(phase, current);
                        }
                    }
                    if (!blaster186TargetDisplaced && TargetPositionFootprint(current.NativeGraphics!.Models[contactActor]) != originalTargetPosition)
                    {
                        blaster186TargetDisplaced = true;
                        ColdNext("recipient-native-Z-displaced", current);
                    }
                    foreach (uint flag in new uint[] { 1, 2 })
                    {
                        int record = Enumerable.Range(0, NativeBattleBlaster186Profile.ParticleCount).FirstOrDefault(index => state.ReadDword(NativeBattleBlaster186Profile.ParticleBase + (uint)(index * NativeBattleBlaster186Profile.ParticleStride)) == flag, -1);
                        string phase = "shared-particle-bank-flag" + flag + "-live";
                        if (record >= 0 && blaster186Boundaries.Add(phase))
                        {
                            var bytes = state.ReadView(NativeBattleBlaster186Profile.ParticleBase + (uint)(record * NativeBattleBlaster186Profile.ParticleStride), NativeBattleBlaster186Profile.ParticleStride).ReadSnapshot(0, NativeBattleBlaster186Profile.ParticleStride);
                            blaster186Observations.Add(new { Boundary = phase, Record = record, bytes.Data, bytes.Known });
                            ColdNext(phase, current);
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (blaster186PacketBanks.Add(start))
                        {
                            ColdNext("effect-arena-packet-bank-" + start.ToString("X"), current);
                        }
                        foreach (IGrouping<(NativeBattlePrimitiveReference Packet, int WordOffset), NativeBattleLogicalResolvedCommand> group in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                        {
                            NativeBattleLogicalCommand physical = group.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in group)
                            {
                                Assert.Equal(physical.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(physical.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            blaster186Opcodes.Add(physical.Opcode);
                        }
                    }
                    if (pool.Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBlaster186Profile.Callback5C0900) && !blaster186Boundaries.Contains("ordinary-COMMON-particle-sprites-live"))
                    {
                        ImmutableHashSet<NativeBattlePrimitiveReference> common = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
                        NativeBattleLogicalCommand[] sprites = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => common.Contains(command.Packet) && (command.Opcode & 0xFC) == 0x2C).ToArray();
                        if (sprites.Length > 0)
                        {
                            Assert.All(sprites, command => Assert.NotEqual("20DFAB8", command.Packet.Bank));
                            blaster186Boundaries.Add("ordinary-COMMON-particle-sprites-live");
                            blaster186Observations.Add(new { Boundary = "ordinary-COMMON-particle-sprites-live", Packets = sprites.Select(command => new { command.Packet, command.WordOffset, command.Opcode, Words = command.Words.ToArray() }).ToArray() });
                            ColdNext("ordinary-COMMON-particle-sprites-live", current);
                        }
                    }
                    NativeBattleModulePoolTask? waiter = after.Pools[0].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBlaster186Profile.CompletionCallback);
                    if (waiter is not null)
                    {
                        Assert.DoesNotContain(pool.Slots, task => (task.Flags & 1) != 0);
                        blaster186WaiterHighByte ??= waiter.ReadByte(15);
                        Assert.Equal(blaster186WaiterHighByte.Value, waiter.ReadByte(15));
                        Assert.Equal((byte)0, waiter.ReadByte(14));
                        string phase = waiter.ReadWord(12) <= 1 ? "children-drained-before-COMMON-old1-request" : "COMMON-request-pending";
                        if (blaster186Boundaries.Add(phase))
                        {
                            if (waiter.ReadWord(12) > 1)
                            {
                                Assert.Contains(current.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
                            }
                            ColdNext(phase, current);
                        }
                    }
                }
                if (effectId == 186 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && blaster186Boundaries.Add("COMMON-FF-completed-all-banks-detached"))
                {
                    NativeBattleBlaster186State state = current.NativeEffectMemory!.Blaster186!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    for (int slot = 0; slot < NativeBattleBlaster186Profile.RootCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(slot * NativeBattleBlaster186Profile.RootStride, 2)) & 1);
                    }
                    Assert.Equal((byte)255, state.RootBankStorage.Read(16 + 14, 1)[0]);
                    Assert.NotNull(blaster186WaiterHighByte);
                    Assert.Equal(blaster186WaiterHighByte.Value, state.RootBankStorage.Read(16 + 15, 1)[0]);
                    for (int slot = 0; slot < NativeBattleBlaster186Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * NativeBattleBlaster186Profile.ChildStride, 2)) & 1);
                    }
                    for (uint record = 0; record < NativeBattleBlaster186Profile.ParticleCount; record++)
                    {
                        Assert.Equal(0u, state.ReadDword(NativeBattleBlaster186Profile.ParticleBase + record * NativeBattleBlaster186Profile.ParticleStride));
                    }
                    Assert.Equal(blaster186FramesW, Blaster186FrameWordLanes(state));
                    Assert.Equal(originalTargetPosition, TargetPositionFootprint(current.NativeGraphics!.Models[contactActor]));
                    ColdNext("COMMON-FF-completed-all-banks-detached", current);
                }
                if (effectId == 151 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleCannonBlow151State state = current.NativeEffectMemory!.CannonBlow151!;
                    Assert.Equal(NativeBattleCannonBlow151Profile.ArenaAddress, state.ReadDword(NativeBattleCannonBlow151Profile.WorkingGlobal));
                    Assert.Equal(new uint[] { NativeBattleCannonBlow151Profile.RootDescriptor, NativeBattleCannonBlow151Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleCannonBlow151Profile.RootBank, NativeBattleCannonBlow151Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new int[] { 2, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    Assert.Throws<InvalidDataException>(() => state.ReadWord(NativeBattleCannonBlow151Profile.RootBank));
                    Assert.Throws<InvalidDataException>(() => state.ReadWord(NativeBattleCannonBlow151Profile.ArenaAddress));
                    Assert.Equal(cannon151SamplesW, Cannon151WordLanes(state));
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleCannonBlow151Profile.MainCallback);
                    if (main is not null)
                    {
                        Assert.Equal((ushort)0, main.ReadWord(14));
                        string? phase = main.ReadWord(12) switch { 1 => "trajectory32-before-main-old1", 14 => "before-main-old14-impact", 30 => "before-main-old30-contact", 40 => "before-main-old40-retire", _ => null };
                        if (phase is not null && cannon151Boundaries.Add(phase))
                        {
                            Assert.Equal(state.Read(NativeBattleCannonBlow151Profile.ControlPointsAddress, 6), state.Read(NativeBattleCannonBlow151Profile.SamplesAddress, 6));
                            Assert.Equal(state.Read(NativeBattleCannonBlow151Profile.ControlPointsAddress + 24, 6), state.Read(NativeBattleCannonBlow151Profile.SamplesAddress + 31 * 8, 6));
                            Assert.Equal(cannon151MatrixPadding, state.Read(NativeBattleCannonBlow151Profile.CurveMatrixAddress + 18, 2));
                            (byte[] data, bool[] known) = state.ReadView(NativeBattleCannonBlow151Profile.SamplesAddress, NativeBattleCannonBlow151Profile.SamplesLength).ReadSnapshot(0, NativeBattleCannonBlow151Profile.SamplesLength);
                            Assert.All(Enumerable.Range(0, 32), index => Assert.All(known.Skip(index * 8).Take(6), value => Assert.True(value)));
                            cannon151Observations.Add(new { Boundary = phase, Index = main.ReadWord(14), Samples = data, Known = known, Matrix = state.ReadView(NativeBattleCannonBlow151Profile.CurveMatrixAddress, 32).ReadSnapshot(0, 32) });
                            ColdNext(phase, current);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0 && task.Callback != NativeBattleCannonBlow151Profile.MainCallback))
                    {
                        if (task.Callback != NativeBattleCannonBlow151Profile.Callback60E1B0)
                        {
                            Assert.Equal((ushort)0, task.ReadWord(14));
                        }
                        if (cannon151Callbacks.Add(task.Callback))
                        {
                            string name = $"callback-{task.Callback:X}-live";
                            cannon151Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    foreach (uint bank in new[] { NativeBattleCannonBlow151Profile.ParticleABase, NativeBattleCannonBlow151Profile.ParticleBBase })
                    {
                        int record = Enumerable.Range(0, NativeBattleCannonBlow151Profile.ParticleCount).FirstOrDefault(index => state.ReadDword(bank + (uint)(index * NativeBattleCannonBlow151Profile.ParticleStride)) != 0, -1);
                        if (record >= 0 && cannon151ParticleBanks.Add(bank))
                        {
                            (byte[] data, bool[] known) = state.ReadView(bank + (uint)(record * NativeBattleCannonBlow151Profile.ParticleStride), NativeBattleCannonBlow151Profile.ParticleStride).ReadSnapshot(0, NativeBattleCannonBlow151Profile.ParticleStride);
                            cannon151Observations.Add(new { ParticleBank = bank, Record = record, Data = data, Known = known });
                            string name = $"particle-bank-{bank:X}-live";
                            cannon151Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int start = limit - packets.DrawStep;
                        if (cannon151PacketBanks.Add(start))
                        {
                            ColdNext("COMMON-packet-bank-" + start.ToString("X"), current);
                        }
                        NativeBattleLogicalResolvedCommand[] submitted = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).ToArray();
                        NativeBattleLogicalCommand[] commands = submitted.GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
                        {
                            NativeBattleLogicalCommand physical = group.First().Command;
                            foreach (NativeBattleLogicalResolvedCommand replay in group)
                            {
                                Assert.Equal(physical.Opcode, replay.Command.Opcode);
                                Assert.Equal(physical.Words.ToArray(), replay.Command.Words.ToArray());
                                Assert.Equal(physical.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                            }
                            return physical;
                        }).ToArray();
                        foreach (NativeBattleLogicalCommand command in commands)
                        {
                            cannon151Opcodes.Add(command.Opcode);
                        }
                        if (pool.Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleCannonBlow151Profile.Callback60DA10) && !cannon151Boundaries.Contains("flags8-native-inset-live"))
                        {
                            NativeBattleLogicalCommand? inset = commands.FirstOrDefault(command => Cannon151InsetSprite(original.CannonBlow151Resources!.SpriteC, command));
                            if (inset is not null)
                            {
                                cannon151Boundaries.Add("flags8-native-inset-live");
                                cannon151Observations.Add(new { Boundary = "flags8-native-inset-live", Sprite = original.CannonBlow151Resources!.SpriteC.Address, Flags = 8, Words = inset.Words.ToArray() });
                                ColdNext("flags8-native-inset-live", current);
                            }
                        }
                        NativeBattleModulePoolTask? scrolling = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleCannonBlow151Profile.Callback60DE40 && task.ReadWord(12) > 0);
                        if (scrolling is not null)
                        {
                            int oldAge = scrolling.ReadWord(12) - 1;
                            NativeBattleLogicalCommand? window = commands.FirstOrDefault(command => command.WordOffset == 1 && command.Opcode == 0xE2 && command.Words[0] == NativeBattleEffectTextureWindows.Encode(new(0, 0, 64, 128)));
                            if (window is not null)
                            {
                                NativeBattleLogicalCommand polygon = Assert.Single(commands, command => command.Packet == window.Packet && command.WordOffset == 2);
                                NativeBattleLogicalCommand reset = Assert.Single(commands, command => command.Packet == window.Packet && command.Opcode == 0xE2 && command.WordOffset > 2);
                                Assert.Equal(2 + polygon.Words.Length, reset.WordOffset);
                                Assert.Equal(0xE2000000u, reset.Words[0]);
                                Assert.Equal(new[] { 0, 0, 0, 0 }, polygon.VertexPayload4.ToArray());
                                AssertCannon151ScrollingUv(original.CannonBlow151Resources!.ScrollingMesh, polygon, oldAge * 14);
                                string? phase = scrolling.ReadWord(12) switch { 1 => "scroll124-first-compound", 8 => "before-scroll-old8-cue", 10 => "scroll124-cue512", _ => null };
                                if (phase is not null && cannon151Boundaries.Add(phase))
                                {
                                    cannon151Observations.Add(new { Boundary = phase, OldAge = oldAge, Flags = oldAge >= 8 ? 0xF3 : 0x33, Cue = oldAge >= 8 ? (int?)((oldAge - 8) << 9) : null, VScroll = oldAge * 14 % 128, Window = window.Words.ToArray(), Polygon = polygon.Words.ToArray(), Reset = reset.Words.ToArray(), Sidecars = polygon.VertexPayload4.ToArray() });
                                    ColdNext(phase, current);
                                }
                            }
                        }
                    }
                    NativeBattleModulePoolTask? waiter = after.Pools[0].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleCannonBlow151Profile.CompletionCallback);
                    if (waiter is not null)
                    {
                        Assert.DoesNotContain(pool.Slots, task => (task.Flags & 1) != 0);
                        cannon151WaiterHighByte ??= waiter.ReadByte(15);
                        Assert.Equal(cannon151WaiterHighByte.Value, waiter.ReadByte(15));
                        Assert.Equal((byte)0, waiter.ReadByte(14));
                        string phase = waiter.ReadWord(12) <= 1 ? "children-drained-before-COMMON-old1-request" : "COMMON-request-pending";
                        if (cannon151Boundaries.Add(phase))
                        {
                            if (waiter.ReadWord(12) > 1)
                            {
                                Assert.Contains(current.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
                            }
                            ColdNext(phase, current);
                        }
                    }
                }
                if (effectId == 151 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && cannon151Boundaries.Add("COMMON-FF-completed-all-banks-detached"))
                {
                    NativeBattleCannonBlow151State state = current.NativeEffectMemory!.CannonBlow151!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    for (int slot = 0; slot < NativeBattleCannonBlow151Profile.RootCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(slot * NativeBattleCannonBlow151Profile.RootStride, 2)) & 1);
                    }
                    Assert.Equal((byte)255, state.RootBankStorage.Read(16 + 14, 1)[0]);
                    Assert.NotNull(cannon151WaiterHighByte);
                    Assert.Equal(cannon151WaiterHighByte.Value, state.RootBankStorage.Read(16 + 15, 1)[0]);
                    for (int slot = 0; slot < NativeBattleCannonBlow151Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * NativeBattleCannonBlow151Profile.ChildStride, 2)) & 1);
                    }
                    foreach (uint bank in new[] { NativeBattleCannonBlow151Profile.ParticleABase, NativeBattleCannonBlow151Profile.ParticleBBase })
                    {
                        for (uint index = 0; index < NativeBattleCannonBlow151Profile.ParticleCount; index++)
                        {
                            Assert.Equal(0u, state.ReadDword(bank + index * NativeBattleCannonBlow151Profile.ParticleStride));
                        }
                    }
                    Assert.DoesNotContain(current.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
                    ColdNext("COMMON-FF-completed-all-banks-detached", current);
                }
                if (effectId == 190 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleChainGun190State state = current.NativeEffectMemory!.ChainGun190!;
                    Assert.Equal(new uint[] { NativeBattleChainGun190Profile.RootDescriptor, NativeBattleChainGun190Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleChainGun190Profile.RootBank, NativeBattleChainGun190Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    Assert.Throws<InvalidDataException>(() => state.ReadWord(NativeBattleChainGun190Profile.RootBank));
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleChainGun190Profile.MainCallback);
                    if (main is not null)
                    {
                        string? phase = main.ReadWord(12) switch { 1 => "twelve-records-after-native-setup", 2 => "before-main-old2-opening-particles", 3 => "before-main-old3-first-shot", 4 => "first-native-shot-issued", 26 => "all-twelve-shots-issued", 30 => "before-main-old30-counted-contact", 36 => "before-main-old36-retire", _ => null };
                        if (main.ReadWord(12) == 1)
                        {
                            for (int index = 0; index < NativeBattleChainGun190Profile.RecordCount; index++)
                            {
                                uint address = NativeBattleChainGun190Profile.RecordsAddress + (uint)(index * NativeBattleChainGun190Profile.RecordStride);
                                Assert.Equal(index < 12 ? 1u : 0u, state.ReadDword(address));
                                if (index < 12)
                                {
                                    int actor = unchecked((int)state.ReadDword(address + 4));
                                    chain190HasPositionedRecords |= actor < 0;
                                    chain190HasBoneRecords |= actor >= 0;
                                    if (actor >= 0)
                                    {
                                        Assert.Equal(0, actor);
                                        Assert.Equal(source.NativeEffectMemory!.ChainGun190!.ReadWord(address + 14), state.ReadWord(address + 14));
                                    }
                                }
                                else
                                {
                                    Assert.Equal(JsonSerializer.Serialize(source.NativeEffectMemory!.ChainGun190!.ReadView(address + 4, 12)), JsonSerializer.Serialize(state.ReadView(address + 4, 12)));
                                }
                            }
                            Assert.Equal(source.NativeEffectMemory!.ChainGun190!.ReadWord(NativeBattleChainGun190Profile.BonePointGlobal + 6), state.ReadWord(NativeBattleChainGun190Profile.BonePointGlobal + 6));
                        }
                        if (main.ReadWord(12) >= 26)
                        {
                            Assert.Equal((ushort)12, main.ReadWord(14));
                        }
                        if (phase is not null && chain190Boundaries.Add(phase))
                        {
                            chain190Observations.Add(new { Boundary = phase, Main = main, Records = state.ReadView(NativeBattleChainGun190Profile.RecordsAddress, NativeBattleChainGun190Profile.RecordsLength), BonePoint = state.ReadView(NativeBattleChainGun190Profile.BonePointGlobal, 8) });
                            ColdNext(phase, current);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0 && task.Callback != NativeBattleChainGun190Profile.MainCallback))
                    {
                        if (chain190Callbacks.Add(task.Callback))
                        {
                            string name = $"callback-{task.Callback:X}-observed";
                            chain190Boundaries.Add(name);
                            chain190Observations.Add(new { Boundary = name, Task = task, Scope = (task.Flags & 1) != 0 ? "Current active task" : "Retained task after its actual same-scan callback release" });
                            ColdNext(name, current);
                        }
                    }
                    if (main is null && pool.Slots.Any(task => (task.Flags & 1) != 0) && chain190Boundaries.Add("main-drained-children-live"))
                    {
                        Assert.Contains(pool.Slots, task => task.Callback == NativeBattleChainGun190Profile.MainCallback && task.ReadWord(12) == 37 && (task.Flags & 1) == 0);
                        ColdNext("main-drained-children-live", current);
                    }
                    foreach (uint bank in new[] { NativeBattleChainGun190Profile.ParticleABase, NativeBattleChainGun190Profile.ParticleBBase })
                    {
                        int record = Enumerable.Range(0, NativeBattleChainGun190Profile.ParticleCount).FirstOrDefault(index => state.ReadDword(bank + (uint)(index * NativeBattleChainGun190Profile.ParticleStride)) != 0, -1);
                        if (record >= 0 && chain190ParticleBanks.Add(bank))
                        {
                            uint address = bank + (uint)(record * NativeBattleChainGun190Profile.ParticleStride);
                            (byte[] bytes, bool[] known) = state.ReadView(address, NativeBattleChainGun190Profile.ParticleStride).ReadSnapshot(0, NativeBattleChainGun190Profile.ParticleStride);
                            chain190Observations.Add(new { Bank = bank, Record = record, Data = bytes, Known = known });
                            string name = $"particle-bank-{bank:X}-live";
                            chain190Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    foreach (IGrouping<(NativeBattlePrimitiveReference Packet, int WordOffset), NativeBattleLogicalResolvedCommand> replayGroup in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "2283F40").GroupBy(item => (item.Command.Packet, item.Command.WordOffset)))
                    {
                        NativeBattleLogicalCommand physical = replayGroup.First().Command;
                        foreach (NativeBattleLogicalResolvedCommand replay in replayGroup)
                        {
                            Assert.Equal(physical.Words.ToArray(), replay.Command.Words.ToArray());
                            Assert.Equal(physical.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int bank = (limit - packets.DrawStep) / NativeBattleChainGun190Profile.PacketBankBytes;
                        Assert.InRange(bank, 0, 1);
                        if (chain190PacketBanks.Add(bank))
                        {
                            ColdNext("fixed-packet-bank-" + bank, current);
                        }
                        foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "2283F40" && command.Packet.ByteOffset >= limit - packets.DrawStep && command.Packet.ByteOffset < end))
                        {
                            chain190Opcodes.Add(command.Opcode);
                            NativeBattlePrimitiveReference reference = command.Packet with { ByteOffset = checked(command.Packet.ByteOffset + command.WordOffset * 4) };
                            byte[] physical = NativeBattleChainGun190PacketArena.Read(state, reference, command.Words.Length * 4);
                            uint[] physicalWords = Enumerable.Range(0, command.Words.Length).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(index * 4, 4))).ToArray();
                            uint[] masks = Enumerable.Range(0, command.Words.Length).Select(index => (command.Opcode & 0xFC, index) switch
                            {
                                (0x24, 6) => 0xFFFFu,
                                (0x2C, 6 or 8) => 0xFFFFu,
                                (0x34, 8) => 0xFFFFu,
                                (0x3C, 8 or 11) => 0xFFFFu,
                                _ => uint.MaxValue
                            }).ToArray();
                            for (int index = 0; index < command.Words.Length; index++)
                            {
                                Assert.Equal(command.Words[index] & masks[index], physicalWords[index] & masks[index]);
                            }
                            if (masks.Any(mask => mask != uint.MaxValue) && chain190PaddingOpcodes.Add(command.Opcode))
                            {
                                chain190Observations.Add(new { Opcode = command.Opcode, Packet = reference, LogicalWords = command.Words.ToArray(), PhysicalWords = physicalWords, ConsumedMasks = masks });
                            }
                        }
                    }
                }
                if (effectId == 190 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && chain190Boundaries.Add("all-pools-and-particles-drained"))
                {
                    NativeBattleChainGun190State state = current.NativeEffectMemory!.ChainGun190!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(0, 2)) & 1);
                    for (int slot = 0; slot < NativeBattleChainGun190Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * NativeBattleChainGun190Profile.ChildStride, 2)) & 1);
                    }
                    foreach (uint bank in new[] { NativeBattleChainGun190Profile.ParticleABase, NativeBattleChainGun190Profile.ParticleBBase })
                    {
                        for (uint index = 0; index < NativeBattleChainGun190Profile.ParticleCount; index++)
                        {
                            Assert.Equal(0u, state.ReadDword(bank + index * NativeBattleChainGun190Profile.ParticleStride));
                        }
                    }
                    ColdNext("all-pools-and-particles-drained", current);
                }
                if (effectId == 150 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleGatlingGun150State state = current.NativeEffectMemory!.GatlingGun150!;
                    Assert.Equal(new uint[] { NativeBattleGatlingGun150Profile.RootDescriptor, NativeBattleGatlingGun150Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleGatlingGun150Profile.RootBank, NativeBattleGatlingGun150Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Null(state.RootBankStorage);
                    Assert.Null(state.ChildBankStorage);
                    Assert.Throws<InvalidDataException>(() => state.ReadWord(NativeBattleGatlingGun150Profile.RootBank));
                    NativeBattleEffectTaskPool pool = after.Pools[1];
                    NativeBattleModulePoolTask? main = pool.Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleGatlingGun150Profile.MainCallback);
                    if (main is not null)
                    {
                        string? phase = main.ReadWord(12) switch { 1 => "before-old1-delayed-five", 11 => "before-old11-delayed-seven", 30 => "before-old30-COMMON-contact", _ => null };
                        if (phase is not null && gatling150Boundaries.Add(phase))
                        {
                            ColdNext(phase, current);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0 && task.Callback != NativeBattleGatlingGun150Profile.MainCallback))
                    {
                        if (gatling150Callbacks.Add(task.Callback))
                        {
                            string name = $"callback-{task.Callback:X}-live";
                            gatling150Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    if (main is null && pool.Slots.Any(task => (task.Flags & 1) != 0) && gatling150Boundaries.Add("main-drained-children-live"))
                    {
                        Assert.Contains(pool.Slots, task => task.Callback == NativeBattleGatlingGun150Profile.MainCallback && task.ReadWord(12) == 37 && (task.Flags & 1) == 0);
                        ColdNext("main-drained-children-live", current);
                    }
                    foreach (uint bank in new[] { NativeBattleGatlingGun150Profile.ParticleABase, NativeBattleGatlingGun150Profile.ParticleBBase })
                    {
                        int record = Enumerable.Range(0, NativeBattleGatlingGun150Profile.ParticleCount).FirstOrDefault(index => state.ReadDword(bank + (uint)(index * NativeBattleGatlingGun150Profile.ParticleStride)) != 0, -1);
                        if (record >= 0 && gatling150ParticleBanks.Add(bank))
                        {
                            uint address = bank + (uint)(record * NativeBattleGatlingGun150Profile.ParticleStride);
                            (byte[] bytes, bool[] known) = state.ReadView(address, NativeBattleGatlingGun150Profile.ParticleStride).ReadSnapshot(0, NativeBattleGatlingGun150Profile.ParticleStride);
                            gatling150Observations.Add(new { Bank = bank, Record = record, Data = bytes, Known = known });
                            string name = $"particle-bank-{bank:X}-live";
                            gatling150Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is int end && packets.SecondaryDrawCursor is int limit && end > limit - packets.DrawStep)
                    {
                        int bank = (limit - packets.DrawStep) / NativeBattleGatlingGun150Profile.PacketBankBytes;
                        Assert.InRange(bank, 0, 1);
                        if (gatling150PacketBanks.Add(bank))
                        {
                            ColdNext("fixed-packet-bank-" + bank, current);
                        }
                        foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "2451E80" && command.Packet.ByteOffset >= limit - packets.DrawStep && command.Packet.ByteOffset < end))
                        {
                            gatling150Opcodes.Add(command.Opcode);
                            NativeBattlePrimitiveReference reference = command.Packet with { ByteOffset = checked(command.Packet.ByteOffset + command.WordOffset * 4) };
                            byte[] physical = NativeBattleGatlingGun150PacketArena.Read(state, reference, command.Words.Length * 4);
                            uint[] physicalWords = Enumerable.Range(0, command.Words.Length).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(index * 4, 4))).ToArray();
                            uint[] masks = Enumerable.Range(0, command.Words.Length).Select(index => (command.Opcode & 0xFC, index) switch
                            {
                                (0x24, 6) => 0xFFFFu,
                                (0x2C, 6 or 8) => 0xFFFFu,
                                (0x34, 8) => 0xFFFFu,
                                (0x3C, 8 or 11) => 0xFFFFu,
                                _ => uint.MaxValue
                            }).ToArray();
                            for (int index = 0; index < command.Words.Length; index++)
                            {
                                Assert.Equal(command.Words[index] & masks[index], physicalWords[index] & masks[index]);
                            }
                            if (masks.Any(mask => mask != uint.MaxValue) && gatling150PaddingOpcodes.Add(command.Opcode))
                            {
                                gatling150Observations.Add(new { Opcode = command.Opcode, Packet = reference, LogicalWords = command.Words.ToArray(), PhysicalWords = physicalWords, ConsumedMasks = masks });
                            }
                        }
                    }
                }
                if (effectId == 150 && before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0 && gatling150Boundaries.Add("all-pools-and-particles-drained"))
                {
                    NativeBattleGatlingGun150State state = current.NativeEffectMemory!.GatlingGun150!;
                    Assert.Empty(after.Pools);
                    Assert.NotNull(state.RootBankStorage);
                    Assert.NotNull(state.ChildBankStorage);
                    Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage.Read(0, 2)) & 1);
                    for (int slot = 0; slot < NativeBattleGatlingGun150Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(state.ChildBankStorage.Read(slot * NativeBattleGatlingGun150Profile.ChildStride, 2)) & 1);
                    }
                    foreach (uint bank in new[] { NativeBattleGatlingGun150Profile.ParticleABase, NativeBattleGatlingGun150Profile.ParticleBBase })
                    {
                        for (uint index = 0; index < NativeBattleGatlingGun150Profile.ParticleCount; index++)
                        {
                            Assert.Equal(0u, state.ReadDword(bank + index * NativeBattleGatlingGun150Profile.ParticleStride));
                        }
                    }
                    ColdNext("all-pools-and-particles-drained", current);
                }
                if (effectId == 183 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleEffectTaskPool pool = Assert.Single(after.Pools, item => item.DescriptorAddress == NativeBattleEffect183Profile.ChildDescriptor);
                    NativeBattleModulePoolTask? director = pool.Slots.FirstOrDefault(item => (item.Flags & 1) != 0 && item.Callback == NativeBattleEffect183Profile.MainCallback);
                    if (director is not null && director.ReadWord(12) == 30 && effect183Boundaries.Add("before-old30-first-contact"))
                    {
                        ColdNext("before-old30-first-contact", current);
                    }
                    foreach (uint callback in new[] { NativeBattleEffect183Profile.MeshACallback, NativeBattleEffect183Profile.MeshBCallback })
                    {
                        NativeBattleModulePoolTask? task = pool.Slots.FirstOrDefault(item => (item.Flags & 1) != 0 && item.Callback == callback && item.ReadWord(12) >= 2);
                        if (task is not null && effect183Boundaries.Add("mesh-" + callback.ToString("X")))
                        {
                            ColdNext("mesh-" + callback.ToString("X"), current);
                        }
                    }
                    NativeBattleEffect183State retained = current.NativeEffectMemory!.Effect183!;
                    uint cursor = retained.ReadDword(NativeBattleEffect183Profile.CursorGlobal);
                    if (cursor > NativeBattleEffect183Profile.PacketBase)
                    {
                        int bank = checked((int)((cursor - NativeBattleEffect183Profile.PacketBase - 1) / NativeBattleEffect183Profile.PacketBankBytes));
                        if (effect183PacketBanks.Add(bank))
                        {
                            ColdNext("fixed-packet-bank-" + bank, current);
                        }
                    }
                }
                cameraChanged |= JsonSerializer.Serialize(current.NativeCamera) != initialCamera;
                stageChanged |= StageColors(current) != initialStage;
                if (effectId == 62 && !devourMuted && current.NativeGlobalSoundProcess!.MusicPlayback?.Gain0 == 0)
                {
                    ColdNext("music-muted", current);
                    devourMuted = true;
                }
                if (effectId == 47 && after?.ReturnedDescriptor is > 0)
                {
                    Assert.False(current.NativeEffectMemory!.Curse47ColdSourcesPending);
                    NativeBattleCurse47State curse = current.NativeEffectMemory.Curse47 ?? throw new InvalidDataException("Curse requires its reached cold-source preparation.");
                    if (!curseBankRestored && curse.NodeProgram is { InitializationFlag: 1, Data: not null } program)
                    {
                        Assert.Equal(0x17B97A4u, program.ResourceAddress);
                        Assert.Equal(6544, program.Data.Value.Length);
                        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(program.Data.Value.AsSpan()));
                        ColdNext("node-bank-initialized", current);
                        curseBankRestored = true;
                    }
                    if (!curseIoRestored && curse.ReadDword(NativeBattleCurse47Io.ControlPointerGlobal) == NativeBattleCurse47Io.ControlAddress)
                    {
                        Assert.Equal(80, curse.IoScript!.Value.Length);
                        ColdNext("io-initialized", current);
                        curseIoRestored = true;
                    }
                }
                if (effectId == 8 && after?.ReturnedDescriptor is > 0)
                {
                    Assert.False(current.NativeEffectMemory!.Doom8ColdSourcesPending);
                    NativeBattleDoom8State doom = current.NativeEffectMemory.Doom8 ?? throw new InvalidDataException("Doom requires its reached cold-source preparation.");
                    if (!doomBankRestored && doom.NodeProgram is { InitializationFlag: 1, Data: not null })
                    {
                        Assert.Equal(17460, doom.NodeProgram.Data.Value.Length);
                        Assert.Equal(0x1818B78u, doom.NodeProgram.ResourceAddress);
                        ColdNext("node-bank-initialized", current);
                        doomBankRestored = true;
                    }
                    if (!doomReaperRestored && doom.Models is { IsDefaultOrEmpty: false } models && models.Any(model => model.DecodedPose is not null))
                    {
                        Assert.True(doom.ReaperSourceLoaded);
                        ColdNext("reaper-initialized", current);
                        doomReaperRestored = true;
                    }
                }
                if (effectId == 157 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 2)
                {
                    NativeBattleBreath157State breath = current.NativeEffectMemory!.Breath157!;
                    Assert.Null(breath.RootBankStorage);
                    Assert.Null(breath.ChildBankStorage);
                    Assert.Equal(NativeBattleBreath157Profile.ArenaAddress, breath.ReadDword(NativeBattleBreath157Profile.WorkspaceGlobal));
                    Assert.Equal(new uint[] { NativeBattleBreath157Profile.RootDescriptor, NativeBattleBreath157Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleBreath157Profile.RootBank, NativeBattleBreath157Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 2, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Throws<InvalidDataException>(() => breath.ReadWord(NativeBattleBreath157Profile.RootBank));
                    Assert.Throws<InvalidDataException>(() => breath.ReadDword(NativeBattleBreath157Profile.ArenaAddress));
                    Assert.Equal(breath157Rotation, Breath157Rotation(breath));
                    Assert.Equal(breath157PointW, Breath157PointW(breath));
                    DirectedNodePacketState packetState = Director(current);
                    if (packetState.SecondaryDrawCursor is { } end)
                    {
                        breath157PacketBanks.Add(end - packetState.DrawStep);
                    }
                    foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command))
                    {
                        if (command.Packet.Bank == "20DFAB8")
                        {
                            breath157Opcodes.Add(command.Opcode);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in after.Pools[1].Slots.Where(task => (task.Flags & 1) != 0))
                    {
                        if (task.Callback == NativeBattleBreath157Profile.TravelCallback && task.ReadWord(12) > 0)
                        {
                            breath157TravelIndices.Add(unchecked((short)task.ReadWord(14)));
                        }
                        if (task.Callback == NativeBattleBreath157Profile.DirectorCallback)
                        {
                            string? name = task.ReadWord(12) == 19 ? "before-old19-emitter-switch" : task.ReadWord(12) == 20 ? "after-old19-emitter-switch" : task.ReadWord(12) == 65 ? "before-old65-first-header-contact" : null;
                            if (name is not null && breath157Boundaries.Add(name))
                            {
                                var poseA = breath.ReadView(NativeBattleBreath157Profile.PoseAAddress, 32).ReadSnapshot(0, 32);
                                var poseB = breath.ReadView(NativeBattleBreath157Profile.PoseBAddress, 32).ReadSnapshot(0, 32);
                                var emitter = breath.ReadView(NativeBattleBreath157Profile.AnchorAddress, 8).ReadSnapshot(0, 8);
                                breath157PoseObservations.Add(new { Boundary = name, PoseA = poseA, PoseB = poseB, Emitter = emitter });
                                ColdNext(name, current);
                            }
                        }
                        if (task.ReadWord(12) > 0 && task.Callback is 0x6043A0 or 0x6044D0 or 0x6049F0 or 0x604B30 or 0x604C80 or 0x604E90 or 0x6050A0 or 0x6052A0 && breath157Callbacks.Add(task.Callback))
                        {
                            string name = $"callback-{task.Callback:X6}-live";
                            breath157Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    uint liveTags = 0;
                    for (int index = 0; index < 100; index++)
                    {
                        liveTags |= breath.ReadDword(NativeBattleBreath157Profile.RecordAddress + (uint)(index * 24));
                    }
                    foreach (uint tag in new uint[] { 1, 2, 4 })
                    {
                        if ((liveTags & tag) != 0 && breath157Tags.Add(tag))
                        {
                            string name = $"shared-particle-tag-{tag}-live";
                            breath157Boundaries.Add(name);
                            Assert.Equal(12, breath.Read(NativeBattleBreath157Profile.ScratchMacAddress, 12).Length);
                            ColdNext(name, current);
                        }
                    }
                    if (breath157TravelIndices.Count == 3 && breath157Boundaries.Add("three-travel-points-W-retained"))
                    {
                        Assert.Equal(new short[] { 0, 1, 2 }, breath157TravelIndices.Order().ToArray());
                        Assert.Equal(24, breath.Read(NativeBattleBreath157Profile.TravelPointsAddress, 24).Length);
                        ColdNext("three-travel-points-W-retained", current);
                    }
                    NativeBattleModulePoolTask? waiter = after.Pools[0].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBreath157Profile.CompletionCallback);
                    if (waiter is not null)
                    {
                        Assert.DoesNotContain(after.Pools[1].Slots, task => (task.Flags & 1) != 0);
                        string name = waiter.ReadWord(12) <= 1 ? "children-drained-COMMON-waiter" : "COMMON-request-pending";
                        if (breath157Boundaries.Add(name))
                        {
                            Assert.Equal((ushort)0, waiter.ReadWord(14));
                            ColdNext(name, current);
                        }
                    }
                }
                if (effectId == 157 && before?.ReturnedDescriptor is > 0 && (after is null || after.ReturnedDescriptor == 0) && breath157Boundaries.Add("COMMON-completed-pools-retained"))
                {
                    NativeBattleBreath157State breath = current.NativeEffectMemory!.Breath157!;
                    Assert.NotNull(breath.RootBankStorage);
                    Assert.NotNull(breath.ChildBankStorage);
                    Assert.Empty(after!.Pools);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(breath.RootBankStorage.Read(slot * 16, 2)) & 1);
                    }
                    Assert.Equal((ushort)255, BitConverter.ToUInt16(breath.RootBankStorage.Read(16 + 14, 2)));
                    for (int slot = 0; slot < 100; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(breath.ChildBankStorage.Read(slot * 36, 2)) & 1);
                    }
                    Assert.Equal(breath157Rotation, Breath157Rotation(breath));
                    ColdNext("COMMON-completed-pools-retained", current);
                }
                if (effectId == 156 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 2)
                {
                    NativeBattleWildCannonBlow156State cannon = current.NativeEffectMemory!.WildCannonBlow156!;
                    Assert.Null(cannon.RootBankStorage);
                    Assert.Null(cannon.ChildBankStorage);
                    Assert.Equal(NativeBattleWildCannonBlow156Profile.ArenaAddress, cannon.ReadDword(NativeBattleWildCannonBlow156Profile.WorkspaceGlobal));
                    Assert.Equal(new uint[] { NativeBattleWildCannonBlow156Profile.RootDescriptor, NativeBattleWildCannonBlow156Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleWildCannonBlow156Profile.RootBank, NativeBattleWildCannonBlow156Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 2, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Throws<InvalidDataException>(() => cannon.ReadWord(NativeBattleWildCannonBlow156Profile.RootBank));
                    Assert.Throws<InvalidDataException>(() => cannon.ReadDword(NativeBattleWildCannonBlow156Profile.ArenaAddress));
                    DirectedNodePacketState packetState = Director(current);
                    if (packetState.SecondaryDrawCursor is { } end)
                    {
                        cannon156PacketBanks.Add(end - packetState.DrawStep);
                    }
                    foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command))
                    {
                        if (command.Packet.Bank == "20DFAB8")
                        {
                            cannon156Opcodes.Add(command.Opcode);
                        }
                    }
                    foreach (NativeBattleModulePoolTask task in after.Pools[1].Slots.Where(task => (task.Flags & 1) != 0))
                    {
                        if (task.Callback == NativeBattleWildCannonBlow156Profile.MainCallback && task.ReadWord(12) > 0)
                        {
                            cannon156MainIndices.Add(unchecked((short)task.ReadWord(14)));
                        }
                        if (task.Callback == NativeBattleWildCannonBlow156Profile.DirectorCallback && task.ReadWord(12) == 36 && cannon156Boundaries.Add("before-old36-header-contacts"))
                        {
                            ColdNext("before-old36-header-contacts", current);
                        }
                        if (task.ReadWord(12) > 0 && task.Callback is 0x605DF0 or 0x606200 or 0x6067B0 or 0x606910 or 0x605F40 or 0x606320 or 0x6063E0 or 0x6065A0 or 0x606A60 && cannon156Callbacks.Add(task.Callback))
                        {
                            string name = $"callback-{task.Callback:X6}-live";
                            cannon156Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    foreach ((uint address, int count) in new (uint, int)[] { (NativeBattleWildCannonBlow156Profile.RecordAAddress, 150), (NativeBattleWildCannonBlow156Profile.RecordBAddress, 150), (NativeBattleWildCannonBlow156Profile.RecordCAddress, 100) })
                    {
                        if (Enumerable.Range(0, count).Any(index => cannon.ReadDword(address + (uint)(index * 24)) != 0) && cannon156RecordBanks.Add(address))
                        {
                            string name = $"particle-bank-{address:X}-live";
                            cannon156Boundaries.Add(name);
                            ColdNext(name, current);
                        }
                    }
                    if (cannon156MainIndices.Count == 4 && cannon156Boundaries.Add("four-physical-curve-matrix-lanes"))
                    {
                        Assert.Equal(new short[] { 0, 1, 2, 3 }, cannon156MainIndices.Order().ToArray());
                        Assert.Equal(128, cannon.Read(NativeBattleWildCannonBlow156Profile.MatricesAddress, 128).Length);
                        Assert.Equal(1024, cannon.Read(NativeBattleWildCannonBlow156Profile.SamplesAddress, 1024).Length);
                        Assert.Equal(cannon156SamplesW, Cannon156WordLanes(cannon, NativeBattleWildCannonBlow156Profile.SamplesAddress, 128));
                        ColdNext("four-physical-curve-matrix-lanes", current);
                    }
                    NativeBattleModulePoolTask? waiter = after.Pools[0].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleWildCannonBlow156Profile.CompletionCallback);
                    if (waiter is not null)
                    {
                        Assert.DoesNotContain(after.Pools[1].Slots, task => (task.Flags & 1) != 0);
                        string name = waiter.ReadWord(12) <= 1 ? "children-drained-COMMON-waiter" : "COMMON-request-pending";
                        if (cannon156Boundaries.Add(name))
                        {
                            Assert.Equal((ushort)0, waiter.ReadWord(14));
                            ColdNext(name, current);
                        }
                    }
                }
                if (effectId == 156 && before?.ReturnedDescriptor is > 0 && (after is null || after.ReturnedDescriptor == 0) && cannon156Boundaries.Add("COMMON-completed-pools-retained"))
                {
                    NativeBattleWildCannonBlow156State cannon = current.NativeEffectMemory!.WildCannonBlow156!;
                    Assert.NotNull(cannon.RootBankStorage);
                    Assert.NotNull(cannon.ChildBankStorage);
                    Assert.Empty(after!.Pools);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(cannon.RootBankStorage.Read(slot * 16, 2)) & 1);
                    }
                    Assert.Equal((ushort)255, BitConverter.ToUInt16(cannon.RootBankStorage.Read(16 + 14, 2)));
                    for (int slot = 0; slot < 100; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(cannon.ChildBankStorage.Read(slot * 36, 2)) & 1);
                    }
                    ColdNext("COMMON-completed-pools-retained", current);
                }
                if (effectId == 155 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 2)
                {
                    NativeBattleSandStorm155State sand = current.NativeEffectMemory!.SandStorm155!;
                    Assert.Null(sand.RootBankStorage);
                    Assert.Null(sand.ChildBankStorage);
                    Assert.Equal(new uint[] { NativeBattleSandStorm155Profile.RootDescriptor, NativeBattleSandStorm155Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleSandStorm155Profile.RootBank, NativeBattleSandStorm155Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Throws<InvalidDataException>(() => sand.ReadWord(NativeBattleSandStorm155Profile.RootBank));
                    Assert.Throws<InvalidDataException>(() => sand.ReadWord(NativeBattleSandStorm155Profile.ChildBank));
                    Assert.Equal(sand155SelectedW, Sand155WordLanes(sand, NativeBattleSandStorm155Profile.SelectedVerticesAddress, NativeBattleSandStorm155Profile.VertexCount));
                    Assert.Equal(sand155SamplesW, Sand155WordLanes(sand, NativeBattleSandStorm155Profile.SamplesAddress, NativeBattleSandStorm155Profile.SampleCount));
                    DirectedNodePacketState packets = Director(current);
                    if (packets.SecondaryDrawCursor is { } end)
                    {
                        sand155PacketBanks.Add(end - packets.DrawStep);
                    }
                    NativeBattleModulePoolTask? director = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSandStorm155Profile.DirectorCallback);
                    if (director is not null && director.ReadWord(12) >= 1 && sand155Boundaries.Add("sorted304-references"))
                    {
                        byte[] rows = sand.Read(NativeBattleSandStorm155Profile.SortedRowsAddress, NativeBattleSandStorm155Profile.VertexCount * NativeBattleSandStorm155Profile.SortStride);
                        HashSet<uint> sources = [];
                        int priorKey = int.MaxValue;
                        for (int index = 0; index < NativeBattleSandStorm155Profile.VertexCount; index++)
                        {
                            int offset = index * NativeBattleSandStorm155Profile.SortStride;
                            uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(rows.AsSpan(offset));
                            uint selectedAddress = BinaryPrimitives.ReadUInt32LittleEndian(rows.AsSpan(offset + 4));
                            int key = BinaryPrimitives.ReadInt32LittleEndian(rows.AsSpan(offset + 8));
                            Assert.True(sources.Add(sourceAddress));
                            Assert.InRange(sourceAddress, NativeBattleSandStorm155Profile.VertexSourceAddress, NativeBattleSandStorm155Profile.VertexSourceAddress + (NativeBattleSandStorm155Profile.VertexCount - 1) * 8);
                            Assert.Equal(0u, (sourceAddress - NativeBattleSandStorm155Profile.VertexSourceAddress) % 8);
                            Assert.Equal(NativeBattleSandStorm155Profile.SelectedVerticesAddress + sourceAddress - NativeBattleSandStorm155Profile.VertexSourceAddress, selectedAddress);
                            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(original.SandStorm155Resources!.Mesh.Bytes.AsSpan().Slice(checked((int)(sourceAddress - NativeBattleSandStorm155Profile.MeshAddress)) + 2)), key);
                            Assert.True(priorKey >= key);
                            priorKey = key;
                        }
                        ColdNext("sorted304-references", current);
                    }
                    if (director is not null && director.ReadWord(12) == 50 && sand155Boundaries.Add("before-old50-bulk-contacts"))
                    {
                        ColdNext("before-old50-bulk-contacts", current);
                    }
                    NativeBattleModulePoolTask? displacement = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSandStorm155Profile.DisplacementCallback && task.ReadWord(12) > 0);
                    if (displacement is not null && sand155Boundaries.Add("selected-XYZ-W-retained"))
                    {
                        for (int vertex = 0; vertex < NativeBattleSandStorm155Profile.VertexCount; vertex++)
                        {
                            Assert.Equal(6, sand.Read(NativeBattleSandStorm155Profile.SelectedVerticesAddress + (uint)(vertex * 8), 6).Length);
                        }
                        ColdNext("selected-XYZ-W-retained", current);
                    }
                    NativeBattleModulePoolTask? mesh = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSandStorm155Profile.MeshCurveCallback);
                    if (mesh is not null)
                    {
                        int age = mesh.ReadWord(12);
                        string? name = age == 2 ? "dual-mesh-fade-in" : age == 8 ? "dual-mesh-steady" : age == 54 ? "dual-mesh-fade-out" : null;
                        if (name is not null && sand155Boundaries.Add(name))
                        {
                            NativeBattleLogicalCommand[] gt4 = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == NativeBattleSandStorm155Profile.PacketBankId && command.Opcode == 0x3E).ToArray();
                            sand155MeshObservations.Add(new { Boundary = name, CurrentAge = age, GT4Packets = gt4.Length, Colors = gt4.Select(command => command.Words[0]).Distinct().Order().ToArray() });
                            ColdNext(name, current);
                        }
                        if (age >= 30 && sand155Boundaries.Add("six-point-curve34-samples"))
                        {
                            Assert.Equal(sand.Read(NativeBattleSandStorm155Profile.ControlPointsAddress, 6), sand.Read(NativeBattleSandStorm155Profile.SamplesAddress, 6));
                            Assert.Equal(sand.Read(NativeBattleSandStorm155Profile.ControlPointsAddress + 5 * 8, 6), sand.Read(NativeBattleSandStorm155Profile.SamplesAddress + 33 * 8, 6));
                            Assert.Contains(Enumerable.Range(0, NativeBattleSandStorm155Profile.SampleCount), sample => sand.Read(NativeBattleSandStorm155Profile.SamplesAddress + (uint)(sample * 8), 6).Any(value => value != 0));
                            ColdNext("six-point-curve34-samples", current);
                        }
                    }
                    foreach ((string name, uint callback, uint records) in new (string, uint, uint)[] { ("spriteA-recordB-live", NativeBattleSandStorm155Profile.SpriteACallback, NativeBattleSandStorm155Profile.RecordBAddress), ("spriteB-recordA-live", NativeBattleSandStorm155Profile.SpriteBCallback, NativeBattleSandStorm155Profile.RecordAAddress) })
                    {
                        if (after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == callback && task.ReadWord(12) > 0) && Enumerable.Range(0, NativeBattleSandStorm155Profile.RecordCount).Any(record => sand.ReadDword(records + (uint)(record * NativeBattleSandStorm155Profile.RecordStride)) != 0) && sand155Boundaries.Add(name))
                        {
                            ColdNext(name, current);
                        }
                    }
                }
                if (effectId == 155 && before?.ReturnedDescriptor is > 0 && (after is null || after.ReturnedDescriptor == 0) && sand155Boundaries.Add("closed-pools-retained-storage"))
                {
                    NativeBattleSandStorm155State sand = current.NativeEffectMemory!.SandStorm155!;
                    Assert.NotNull(sand.RootBankStorage);
                    Assert.NotNull(sand.ChildBankStorage);
                    if (after is not null)
                    {
                        Assert.Empty(after.Pools);
                    }
                    Assert.Equal(0, BitConverter.ToUInt16(sand.RootBankStorage.Read(0, 2)) & 1);
                    for (int slot = 0; slot < NativeBattleSandStorm155Profile.ChildCount; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(sand.ChildBankStorage.Read(slot * NativeBattleSandStorm155Profile.ChildStride, 2)) & 1);
                    }
                    ColdNext("closed-pools-retained-storage", current);
                }
                if (effectId == 128 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 4)
                {
                    NativeBattleGastricJuice128State gastric = current.NativeEffectMemory!.GastricJuice128!;
                    uint working = gastric.ReadDword(NativeBattleGastricJuice128Profile.WorkingGlobal);
                    int arenaOffset = checked((int)(working - NativeBattleGastricJuice128Profile.ArenaAddress));
                    Assert.Equal(new uint[] { NativeBattleGastricJuice128Profile.RootDescriptor, NativeBattleGastricJuice128Profile.MainDescriptor, NativeBattleGastricJuice128Profile.CurveDescriptor, NativeBattleGastricJuice128Profile.SpriteDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 616, 80, 32 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 8, 128 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleGastricJuice128Profile.RootBank, working + NativeBattleGastricJuice128Profile.MainOffset, working + NativeBattleGastricJuice128Profile.CurveOffset, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    int cache = arenaOffset + NativeBattleGastricJuice128Profile.BodyCacheOffset;
                    if (gastric128Boundaries.Add("body-coefficients-captured"))
                    {
                        NativeBattleGraphicsModel model = current.NativeGraphics!.Models[contactActor];
                        NativeBattleGraphicsResource resource = current.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == current.NativeGraphics.ActorIds[contactActor]).Resource;
                        if (model.ActiveStagedResourceRow100 is not null)
                        {
                            resource = NativeBattleVictoryResources.Resolve(current.NativeFileStaging!, current.NativeModelCpuResources!, model, resource);
                        }
                        ImmutableArray<NativeBattleRawMesh> body = resource.Geometry ?? throw new InvalidDataException("Gastric Juice requires its actual captured raw body geometry.");
                        gastric128CachedVertices = body.Sum(mesh => mesh.Vertices.Length);
                        gastric128LinkedBody = model.LinkedTransform is not null;
                        if (gastric128LinkedBody)
                        {
                            ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new InvalidDataException("Gastric Juice requires its actual captured linked geometry.");
                            gastric128CachedVertices += linked.Sum(mesh => mesh.Vertices.Length);
                            Assert.NotNull(model.CurrentLinkedBoneMatrices);
                        }
                        Assert.InRange(gastric128CachedVertices, 1, NativeBattleGastricJuice128Profile.BodyCacheBytes);
                        Assert.NotNull(model.CurrentBodyBoneMatrices);
                        NativeCrtRandomState expected = prior.NativeCrtRandom!;
                        for (int vertex = 0; vertex < gastric128CachedVertices; vertex++)
                        {
                            expected = expected.Draw(out _);
                        }
                        Assert.Equal(expected, current.NativeCrtRandom);
                        (byte[] coefficients, bool[] coefficientKnown) = current.NativeEffectMemory!.Arena!.ReadSnapshot(cache, gastric128CachedVertices);
                        Assert.All(coefficientKnown, Assert.True);
                        int tailLength = NativeBattleGastricJuice128Profile.BodyCacheBytes - gastric128CachedVertices;
                        var priorTail = prior.NativeEffectMemory!.Arena!.ReadSnapshot(cache + gastric128CachedVertices, tailLength);
                        var currentTail = current.NativeEffectMemory.Arena.ReadSnapshot(cache + gastric128CachedVertices, tailLength);
                        Assert.Equal(priorTail.Data, currentTail.Data);
                        Assert.Equal(priorTail.Known, currentTail.Known);
                        gastric128CacheBaseline = JsonSerializer.Serialize(new { coefficients, coefficientKnown });
                        ColdNext("body-coefficients-captured", current);
                    }
                    var currentCoefficients = current.NativeEffectMemory!.Arena!.ReadSnapshot(cache, gastric128CachedVertices);
                    Assert.Equal(gastric128CacheBaseline, JsonSerializer.Serialize(new { coefficients = currentCoefficients.Data, coefficientKnown = currentCoefficients.Known }));
                    DirectedNodePacketState packets = Director(current);
                    gastric128PacketBanks.Add(packets.SecondaryDrawCursor!.Value - packets.DrawStep);
                    NativeBattleModulePoolTask? main = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleGastricJuice128Profile.MainCallback);
                    if (main is not null)
                    {
                        if (main.ReadDword(0x20) == NativeBattleGastricJuice128Profile.MainActAddress && main.ReadDword(0x24) > 0 && gastric128Boundaries.Add("main-act-live"))
                        {
                            ColdNext("main-act-live", current);
                        }
                        if (main.ReadWord(12) == 53 && gastric128Boundaries.Add("before-main-contact-age53"))
                        {
                            ColdNext("before-main-contact-age53", current);
                        }
                        if (main.ReadWord(12) is >= 30 and <= 69 && !NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current).IsEmpty && gastric128Boundaries.Add("deformed-current-body"))
                        {
                            Assert.Equal(4, current.NativeGraphics!.Models[contactActor].Flags0 & 4);
                            Assert.NotNull(current.NativeGraphics.Models[contactActor].CurrentBodyBoneMatrices);
                            ColdNext("deformed-current-body", current);
                        }
                        if (main.ReadDword(0x24) > 0 && !gastric128Boundaries.Contains("shared-act-scratch-live"))
                        {
                            (byte[] bytes, bool[] known) = current.NativeEffectMemory.Arena.ReadSnapshot(arenaOffset + NativeBattleGastricJuice128Profile.MorphOffset, NativeBattleGastricJuice128Profile.MorphBytes);
                            if (Enumerable.Range(0, 64).Any(vertex => known[vertex * 8] && bytes.AsSpan(vertex * 8, 6).ToArray().Any(value => value != 0)))
                            {
                                gastric128Boundaries.Add("shared-act-scratch-live");
                                ColdNext("shared-act-scratch-live", current);
                            }
                        }
                    }
                    NativeBattleModulePoolTask? curve = after.Pools[2].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleGastricJuice128Profile.CurveCallback && task.ReadWord(14) >= 3);
                    if (curve is not null && gastric128Boundaries.Add("linked-trail-points-live"))
                    {
                        uint point = curve.ReadDword(16);
                        HashSet<uint> visited = [];
                        while (point != 0)
                        {
                            Assert.True(visited.Add(point));
                            Assert.InRange(point, working + NativeBattleGastricJuice128Profile.PointsOffset, working + NativeBattleGastricJuice128Profile.BodyCacheOffset - NativeBattleGastricJuice128Profile.PointStride);
                            Assert.Equal(0u, (point - working - NativeBattleGastricJuice128Profile.PointsOffset) % NativeBattleGastricJuice128Profile.PointStride);
                            int offset = checked((int)(point - NativeBattleGastricJuice128Profile.ArenaAddress));
                            (byte[] bytes, bool[] known) = current.NativeEffectMemory.Arena.ReadSnapshot(offset, NativeBattleGastricJuice128Profile.PointStride);
                            Assert.All(known.Take(8), Assert.True);
                            Assert.NotEqual(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6)));
                            gastric128PointMasks.Add(new { Address = point, Life = bytes.AsSpan(6, 2).ToArray(), LifeKnown = known.AsSpan(6, 2).ToArray(), Known = known });
                            point = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena.Read(offset + 36, 4));
                        }
                        Assert.True(visited.Count >= 3);
                        ColdNext("linked-trail-points-live", current);
                    }
                    foreach ((string name, uint callback) in new (string, uint)[] { ("spray-sprite-live", NativeBattleGastricJuice128Profile.SpriteACallback), ("drip-sprite-live", NativeBattleGastricJuice128Profile.SpriteBCallback), ("splash-sprite-live", NativeBattleGastricJuice128Profile.SpriteCCallback) })
                    {
                        if (after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == callback && task.ReadDword(12) > 0) && gastric128Boundaries.Add(name))
                        {
                            ColdNext(name, current);
                        }
                    }
                }
                if (effectId == 128 && gastric128Boundaries.Contains("deformed-current-body") && (current.NativeGraphics!.Models[contactActor].Flags0 & 4) == 0 && gastric128Boundaries.Add("target-body-visibility-restored"))
                {
                    ColdNext("target-body-visibility-restored", current);
                }
                if (effectId == 128 && before?.ReturnedDescriptor is > 0 && (after is null || after.ReturnedDescriptor == 0) && gastric128Boundaries.Add("all-three-pools-drained"))
                {
                    if (after is not null)
                    {
                        Assert.All(after.Pools.SelectMany(pool => pool.Slots), task => Assert.Equal(0, task.Flags & 1));
                    }
                    ColdNext("all-three-pools-drained", current);
                }
                if (effectId == 132 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 6)
                {
                    NativeBattleDisolvingAcid132State acid132 = current.NativeEffectMemory!.DisolvingAcid132!;
                    acid132.Validate();
                    uint working = acid132.ReadDword(NativeBattleDisolvingAcid132Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { NativeBattleDisolvingAcid132Profile.RootDescriptor, NativeBattleDisolvingAcid132Profile.MainDescriptor, NativeBattleDisolvingAcid132Profile.ActDescriptor, NativeBattleDisolvingAcid132Profile.SimpleSpriteDescriptor, NativeBattleDisolvingAcid132Profile.CurveDescriptor, NativeBattleDisolvingAcid132Profile.SpriteDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 20, 0x2D8, 24, 64, 28 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 3, 16, 32, 16, 64 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleDisolvingAcid132Profile.RootBank, working + NativeBattleDisolvingAcid132Profile.MainOffset, working + NativeBattleDisolvingAcid132Profile.ActOffset, working + NativeBattleDisolvingAcid132Profile.SimpleSpriteOffset, working + NativeBattleDisolvingAcid132Profile.CurveOffset, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (acid132Boundaries.Add("scalar-owners-initialized"))
                    {
                        Assert.Throws<InvalidDataException>(() => acid132.ReadWord(NativeBattleDisolvingAcid132Profile.EmissionVectorsAddress));
                        ColdNext("scalar-owners-initialized", current);
                    }
                    DirectedNodePacketState packets = Director(current);
                    acid132PacketBanks.Add(packets.SecondaryDrawCursor!.Value - packets.DrawStep);
                    NativeBattleModulePoolTask? main = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleDisolvingAcid132Profile.MainCallback);
                    if (main is not null && main.ReadWord(12) == 45 && acid132Boundaries.Add("before-main-contact-age45"))
                    {
                        ColdNext("before-main-contact-age45", current);
                    }
                    foreach ((string name, uint sourceAddress) in new (string, uint)[] { ("primary-act-live", NativeBattleDisolvingAcid132Profile.PrimaryActAddress), ("secondary-act-live", NativeBattleDisolvingAcid132Profile.SecondaryActAddress), ("impact-act-live", NativeBattleDisolvingAcid132Profile.ImpactActAddress) })
                    {
                        if (after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(0x18) == sourceAddress && task.ReadDword(0x1C) > 0) && acid132Boundaries.Add(name))
                        {
                            ColdNext(name, current);
                        }
                    }
                    NativeBattleModulePoolTask? curve = after.Pools[4].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleDisolvingAcid132Profile.CurveCallback && task.ReadWord(14) >= 3);
                    if (curve is not null && acid132Boundaries.Add("linked-points-live"))
                    {
                        Assert.NotNull(current.NativeGraphics!.Models[caster].CurrentBodyBoneMatrices);
                        uint head = curve.ReadDword(16);
                        Assert.InRange(head, working + NativeBattleDisolvingAcid132Profile.PointsOffset, working + NativeBattleDisolvingAcid132Profile.MorphOffset - NativeBattleDisolvingAcid132Profile.PointStride);
                        Assert.Equal(0u, (head - working - NativeBattleDisolvingAcid132Profile.PointsOffset) % NativeBattleDisolvingAcid132Profile.PointStride);
                        uint point = head;
                        HashSet<uint> visited = [];
                        while (point != 0)
                        {
                            Assert.True(visited.Add(point));
                            int offset = checked((int)(point - 0x20DFAB8u));
                            (byte[] bytes, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(offset, NativeBattleDisolvingAcid132Profile.PointStride);
                            Assert.All(known.Take(8), Assert.True);
                            Assert.NotEqual(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6)));
                            acid132PointMasks.Add(new { Address = point, W = bytes.AsSpan(6, 2).ToArray(), WKnown = known.AsSpan(6, 2).ToArray(), Known = known });
                            point = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena.Read(offset + 24, 4));
                        }
                        Assert.True(visited.Count >= 3);
                        ColdNext("linked-points-live", current);
                    }
                    bool privateFt4 = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Any(item => item.Command.Packet.Bank == packets.PacketBank && item.Command.Packet.ByteOffset >= packets.SecondaryDrawCursor!.Value - packets.DrawStep && item.Command.Packet.ByteOffset < packets.PrimaryDrawCursor!.Value && item.Command.Opcode == 0x2E);
                    if (privateFt4 && after.Pools[5].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleDisolvingAcid132Profile.SpriteCallback && task.ReadWord(18) > 0) && acid132Boundaries.Add("template683490-live"))
                    {
                        Assert.NotEqual(0u, acid132.ReadDword(NativeBattleDisolvingAcid132Profile.TemporaryGlobal));
                        ColdNext("template683490-live", current);
                    }
                    if (privateFt4 && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleDisolvingAcid132Profile.PrimaryActCallback && task.ReadWord(12) > 0) && acid132Boundaries.Add("regular571C80-sprite-live"))
                    {
                        ColdNext("regular571C80-sprite-live", current);
                    }
                }
                if (effectId == 132 && before?.ReturnedDescriptor is > 0 && (after is null || after.ReturnedDescriptor == 0) && acid132Boundaries.Add("all-physical-pools-drained"))
                {
                    if (after is not null)
                    {
                        Assert.All(after.Pools.SelectMany(pool => pool.Slots), task => Assert.Equal(0, task.Flags & 1));
                    }
                    ColdNext("all-physical-pools-drained", current);
                }
                if (effectId == 136 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 6)
                {
                    NativeBattleOilShot136State oilShot = current.NativeEffectMemory!.OilShot136!;
                    uint working = oilShot.ReadDword(NativeBattleOilShot136Profile.WorkingGlobal);
                    Assert.False(oilShot.PendingColdSources);
                    Assert.Equal(new uint[] { NativeBattleOilShot136Profile.RootDescriptor, NativeBattleOilShot136Profile.MainDescriptor, NativeBattleOilShot136Profile.MovingSpriteDescriptor, NativeBattleOilShot136Profile.EmitterDescriptor, NativeBattleOilShot136Profile.SimpleSpriteDescriptor, NativeBattleOilShot136Profile.LargeParticleDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0x46C, 32, 0xC4, 20, 0x2BC }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 8, 16, 8, 16 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleOilShot136Profile.RootBank, working + NativeBattleOilShot136Profile.MainOffset, working + NativeBattleOilShot136Profile.MovingSpriteOffset, working + NativeBattleOilShot136Profile.EmitterOffset, working + NativeBattleOilShot136Profile.SimpleSpriteOffset, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (oilShotBoundaries.Add("mutable-sources-preprocessed"))
                    {
                        Assert.NotEqual(original.OilShot136Resources!.InitialMesh.Bytes.ToArray(), oilShot.ReadMesh().ToArray());
                        Assert.NotNull(oilShot.ScaleMatrix32);
                        ColdNext("mutable-sources-preprocessed", current);
                    }
                    DirectedNodePacketState packets = Director(current);
                    oilShotPacketBanks.Add(packets.SecondaryDrawCursor!.Value - packets.DrawStep);
                    NativeBattleModulePoolTask? main = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilShot136Profile.MainCallback);
                    if (main is not null)
                    {
                        foreach ((string name, int offset, uint sourceAddress) in new (string, int, uint)[] { ("main-act-live", 0x2C, NativeBattleOilShot136Profile.MainActAddress), ("secondary-act-live", 0xF4, NativeBattleOilShot136Profile.SecondaryActAddress), ("impact-act-live", 0x2E8, NativeBattleOilShot136Profile.ImpactActAddress) })
                        {
                            if (main.ReadDword(offset) == sourceAddress && main.ReadDword(offset + 4) > 0 && oilShotBoundaries.Add(name))
                            {
                                ColdNext(name, current);
                            }
                        }
                        if (main.ReadWord(12) == 82 && oilShotBoundaries.Add("before-main-root-return"))
                        {
                            ColdNext("before-main-root-return", current);
                        }
                        if (main.ReadWord(12) is >= 32 and <= 38 && !oilShotBoundaries.Contains("selected-vertices-live"))
                        {
                            int morph = checked((int)(working - 0x20DFAB8u)) + NativeBattleOilShot136Profile.MorphOffset;
                            (byte[] data, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(morph, NativeBattleOilShot136Profile.MorphBytes);
                            if (Enumerable.Range(0, 108).Any(vertex => known[vertex * 8] && data.AsSpan(vertex * 8, 6).ToArray().Any(value => value != 0)))
                            {
                                oilShotBoundaries.Add("selected-vertices-live");
                                ColdNext("selected-vertices-live", current);
                            }
                        }
                    }
                    if (after.Pools[5].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilShot136Profile.LargeParticleCallback && task.ReadDword(0x1C) == NativeBattleOilShot136Profile.LargeImpactActAddress && task.ReadDword(0x20) > 0) && oilShotBoundaries.Add("large-impact-act-live"))
                    {
                        ColdNext("large-impact-act-live", current);
                    }
                    foreach ((string name, int pool, uint callback, int phase) in new (string, int, uint, int)[] { ("moving-sprite-live", 2, NativeBattleOilShot136Profile.MovingSpriteCallback, 0x1E), ("emitter-act-live", 3, NativeBattleOilShot136Profile.EmitterCallback, 0x20), ("simple-sprite-live", 4, NativeBattleOilShot136Profile.SimpleSpriteCallback, 0x12), ("large-particle-live", 5, NativeBattleOilShot136Profile.LargeParticleCallback, 0x20) })
                    {
                        if (after.Pools[pool].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == callback && task.ReadWord(phase) > 0) && oilShotBoundaries.Add(name))
                        {
                            ColdNext(name, current);
                        }
                    }
                    if (current.NativeGraphics!.PrimitiveFrame!.LastCommands.Any(item => item.Command.Packet.Bank == packets.PacketBank && item.Command.Packet.ByteOffset >= packets.SecondaryDrawCursor!.Value - packets.DrawStep && item.Command.Packet.ByteOffset < packets.PrimaryDrawCursor!.Value && item.Command.Opcode == 0xE2) && oilShotBoundaries.Add("custom-texture-window-live"))
                    {
                        Assert.NotEqual(original.OilShot136Resources!.InitialScaleMatrix32.Bytes.ToArray(), oilShot.Read(NativeBattleOilShot136Profile.MatrixAddress, 32));
                        ColdNext("custom-texture-window-live", current);
                    }
                }
                if (effectId == 136 && before?.Pools.Length == 6 && before.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilShot136Profile.MainCallback && task.ReadWord(12) == 82) && after?.Pools.Length != 6 && oilShotBoundaries.Add("ignored-child-banks-detached"))
                {
                    foreach (NativeBattleEffectTaskPool pool in before.Pools.Skip(2))
                    {
                        int offset = checked((int)(pool.BankAddress - 0x20DFAB8u));
                        for (int slot = 0; slot < pool.Slots.Length; slot++)
                        {
                            Assert.Equal(pool.Slots[slot].Callback, BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory!.Arena!.Read(offset + slot * pool.Stride + 8, 4)));
                        }
                    }
                    ColdNext("ignored-child-banks-detached", current);
                }
                if (effectId == 138 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 5)
                {
                    NativeBattleSaliva138State saliva = current.NativeEffectMemory!.Saliva138!;
                    uint working = saliva.ReadDword(NativeBattleSaliva138Profile.WorkingGlobal);
                    Assert.False(saliva.PendingColdSources);
                    Assert.Equal(new uint[] { NativeBattleSaliva138Profile.RootDescriptor, NativeBattleSaliva138Profile.MainDescriptor, NativeBattleSaliva138Profile.CurveDescriptor, NativeBattleSaliva138Profile.MovingSpriteDescriptor, NativeBattleSaliva138Profile.SimpleSpriteDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 896, 48, 32, 20 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 3, 8, 8 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleSaliva138Profile.RootBank, working + NativeBattleSaliva138Profile.MainOffset, working + NativeBattleSaliva138Profile.CurveOffset, working + NativeBattleSaliva138Profile.MovingSpriteOffset, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (!salivaSourcesRestored)
                    {
                        Assert.NotEqual(original.Saliva138Resources!.InitialMesh.Bytes.ToArray(), saliva.ReadMesh().ToArray());
                        Assert.NotNull(saliva.ScaleMatrix32);
                        ColdNext("mutable-sources-preprocessed", current);
                        salivaSourcesRestored = true;
                    }
                    DirectedNodePacketState packets = Director(current);
                    salivaPacketBanks.Add(packets.SecondaryDrawCursor!.Value - packets.DrawStep);
                    NativeBattleModulePoolTask? main = after.Pools.Single(pool => pool.DescriptorAddress == NativeBattleSaliva138Profile.MainDescriptor).Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSaliva138Profile.MainCallback);
                    if (main is not null)
                    {
                        if (!salivaMainRestored && main.ReadDword(0x2C) == NativeBattleSaliva138Profile.MainActAddress && main.ReadDword(0x30) > 0)
                        {
                            ColdNext("main-act-live", current);
                            salivaMainRestored = true;
                        }
                        if (!salivaSecondaryRestored && main.ReadDword(0xD4) == NativeBattleSaliva138Profile.SecondaryActAddress && main.ReadDword(0xD8) > 0)
                        {
                            ColdNext("secondary-act-live", current);
                            salivaSecondaryRestored = true;
                        }
                        if (!salivaImpactRestored && main.ReadDword(0x268) == NativeBattleSaliva138Profile.ImpactActAddress && main.ReadDword(0x26C) > 0)
                        {
                            ColdNext("impact-act-live", current);
                            salivaImpactRestored = true;
                        }
                    }
                    int morph = checked((int)(working - 0x20DFAB8u)) + NativeBattleSaliva138Profile.MorphOffset;
                    List<object> wLanes = [];
                    for (int vertex = 0; vertex < 108; vertex++)
                    {
                        (byte[] data, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(morph + vertex * 8 + 6, 2);
                        wLanes.Add(new { data, known });
                    }
                    string currentW = JsonSerializer.Serialize(wLanes);
                    salivaScratchW ??= currentW;
                    Assert.Equal(salivaScratchW, currentW);
                    if (!salivaMorphRestored && salivaSecondaryRestored)
                    {
                        (byte[] data, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(morph, NativeBattleSaliva138Profile.MorphBytes);
                        if (Enumerable.Range(0, 108).Any(vertex => known[vertex * 8] && data.AsSpan(vertex * 8, 6).ToArray().Any(value => value != 0)))
                        {
                            ColdNext("selected-vertices-W-retained", current);
                            salivaMorphRestored = true;
                        }
                    }
                    NativeBattleModulePoolTask? curve = after.Pools.Single(pool => pool.DescriptorAddress == NativeBattleSaliva138Profile.CurveDescriptor).Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSaliva138Profile.CurveCallback && task.ReadWord(0x12) > 0);
                    if (!salivaCurveRestored && curve is not null)
                    {
                        ColdNext("curve-live", current);
                        salivaCurveRestored = true;
                    }
                    if (!salivaRibbonRestored && curve is not null && curve.ReadWord(0x12) >= 3 && curve.ReadWord(0x24) >= 3 && curve.ReadDword(0x28) != 0 && current.NativeGraphics!.PrimitiveFrame!.LastCommands.Any(item => item.Command.Packet.Bank == packets.PacketBank && item.Command.Packet.ByteOffset >= packets.SecondaryDrawCursor!.Value - packets.DrawStep && item.Command.Packet.ByteOffset < packets.PrimaryDrawCursor!.Value && item.Command.Opcode == 0x3E && BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(item.Command.Packet.ByteOffset + 12, 4)) == 0x3C14C008))
                    {
                        uint head = curve.ReadDword(0x28);
                        Assert.InRange(head, working + NativeBattleSaliva138Profile.PointsOffset, working + NativeBattleSaliva138Profile.MorphOffset - NativeBattleSaliva138Profile.PointStride);
                        Assert.Equal(0u, (head - working - NativeBattleSaliva138Profile.PointsOffset) % NativeBattleSaliva138Profile.PointStride);
                        ColdNext("linked-points-ribbon-live", current);
                        salivaRibbonRestored = true;
                    }
                    if (!salivaCustomRestored && current.NativeGraphics!.PrimitiveFrame!.LastCommands.Any(item => item.Command.Packet.Bank == packets.PacketBank && item.Command.Packet.ByteOffset >= packets.SecondaryDrawCursor!.Value - packets.DrawStep && item.Command.Packet.ByteOffset < packets.PrimaryDrawCursor!.Value && item.Command.Opcode == 0xE2))
                    {
                        Assert.NotEqual(original.Saliva138Resources!.InitialScaleMatrix32.Bytes.ToArray(), saliva.Read(NativeBattleSaliva138Profile.MatrixAddress, 32));
                        ColdNext("custom-texture-window-live", current);
                        salivaCustomRestored = true;
                    }
                    if (!salivaSimpleRestored && after.Pools.Single(pool => pool.DescriptorAddress == NativeBattleSaliva138Profile.SimpleSpriteDescriptor).Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSaliva138Profile.SimpleSpriteCallback && task.ReadWord(0x12) > 0))
                    {
                        ColdNext("simple-sprite-live", current);
                        salivaSimpleRestored = true;
                    }
                    if (!salivaMovingRestored && after.Pools.Single(pool => pool.DescriptorAddress == NativeBattleSaliva138Profile.MovingSpriteDescriptor).Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSaliva138Profile.MovingSpriteCallback && task.ReadWord(0x1E) > 0))
                    {
                        ColdNext("moving-sprite-live", current);
                        salivaMovingRestored = true;
                    }
                }
                if (effectId == 137 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 4)
                {
                    NativeBattleOilBlast137State oil = current.NativeEffectMemory!.OilBlast137!;
                    uint working = oil.ReadDword(NativeBattleOilBlast137Profile.WorkingGlobal);
                    Assert.False(oil.ColdMatrixSourcePending);
                    Assert.Equal(new uint[] { NativeBattleOilBlast137Profile.RootDescriptor, NativeBattleOilBlast137Profile.MainDescriptor, NativeBattleOilBlast137Profile.SecondaryDescriptor, NativeBattleOilBlast137Profile.ParticleDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleOilBlast137Profile.RootBank, working + NativeBattleOilBlast137Profile.MainOffset, working + NativeBattleOilBlast137Profile.SecondaryOffset, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    DirectedNodePacketState oilPackets = Director(current);
                    oilPacketBanks.Add(oilPackets.SecondaryDrawCursor!.Value - oilPackets.DrawStep);
                    NativeBattleModulePoolTask? oilMain = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilBlast137Profile.MainCallback);
                    if (oilMain is not null)
                    {
                        if (!oilOpeningRestored && oilMain.ReadDword(0x84) == NativeBattleOilBlast137Profile.OpeningActAddress && oilMain.ReadDword(0x88) > 0 && oilMain.ReadDword(0xC4) == NativeBattleOilBlast137Profile.OpeningActAddress && oilMain.ReadDword(0xC8) > 0)
                        {
                            ColdNext("opening-act-pair-live", current);
                            oilOpeningRestored = true;
                        }
                        if (!oilMainRestored && oilMain.ReadDword(0x104) == NativeBattleOilBlast137Profile.MainActAddress && oilMain.ReadDword(0x108) > 0)
                        {
                            ColdNext("main-act-live", current);
                            oilMainRestored = true;
                        }
                        if (!oilImpactRestored && oilMain.ReadDword(0x2A4) == NativeBattleOilBlast137Profile.ImpactActAddress && oilMain.ReadDword(0x2A8) > 0)
                        {
                            ColdNext("impact-act-live", current);
                            oilImpactRestored = true;
                        }
                        if (!oilMorphRestored)
                        {
                            int morph = checked((int)(working - 0x20DFAB8u)) + NativeBattleOilBlast137Profile.MorphOffset;
                            (byte[] bytes, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(morph, NativeBattleOilBlast137Profile.MorphBytes);
                            if (known.All(value => value) && bytes.Any(value => value != 0))
                            {
                                ColdNext("interpolated-morph-live", current);
                                oilMorphRestored = true;
                            }
                        }
                    }
                    if (!oilSecondaryRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilBlast137Profile.SecondaryCallback && task.ReadWord(0x16) > 0))
                    {
                        ColdNext("secondary-static-live", current);
                        oilSecondaryRestored = true;
                    }
                    if (!oilParticlesRestored && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilBlast137Profile.ParticleCallback && task.ReadWord(12) > 0))
                    {
                        ColdNext("particles-live", current);
                        oilParticlesRestored = true;
                    }
                    if (!oilCustomRestored)
                    {
                        int start = oilPackets.SecondaryDrawCursor!.Value - oilPackets.DrawStep;
                        int end = oilPackets.PrimaryDrawCursor!.Value;
                        if (current.NativeGraphics!.PrimitiveFrame!.LastCommands.Any(item => item.Command.Packet.Bank == oilPackets.PacketBank && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end && item.Command.Opcode == 0xE2))
                        {
                            Assert.NotEqual(original.OilBlast137Resources!.InitialMatrix32.Bytes.ToArray(), oil.Read(NativeBattleOilBlast137Profile.MatrixAddress, 32));
                            ColdNext("custom-texture-window-live", current);
                            oilCustomRestored = true;
                        }
                    }
                    (int ordinary, int standard) = ObserveOilBlast137ActBackends(before, after, original.OilBlast137Resources!);
                    oilOrdinaryNodes += ordinary;
                    oilDefaultNodes += standard;
                }
                if (effectId == 158 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 2)
                {
                    NativeBattleMeltEye158State meltEyeState = current.NativeEffectMemory!.MeltEye158!;
                    Assert.Equal(new uint[] { NativeBattleMeltEye158Profile.RootDescriptor, NativeBattleMeltEye158Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleMeltEye158Profile.RootBank, NativeBattleMeltEye158Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    NativeBattleModulePoolTask? meltEyeSpawner = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.SpawnerCallback);
                    if (!meltEyeCurvesRestored && meltEyeSpawner is not null && meltEyeSpawner.ReadWord(14) == 15)
                    {
                        List<object> meltEyeCurveMasks = [];
                        for (int curve = 0; curve < NativeBattleMeltEye158Profile.CurveCount; curve++)
                        {
                            uint meltEyeSamples = NativeBattleMeltEye158Profile.CurveBank + (uint)(curve * NativeBattleMeltEye158Profile.CurveStride) + 0x38;
                            for (int sample = 0; sample < 48; sample++)
                            {
                                uint meltEyePoint = meltEyeSamples + (uint)(sample * 8);
                                Assert.Equal(6, meltEyeState.Read(meltEyePoint, 6).Length);
                                (byte[] meltEyeW, bool[] meltEyeKnownW) = meltEyeState.ReadView(meltEyePoint + 6, 2).ReadSnapshot(0, 2);
                                (byte[] meltEyeOriginalW, bool[] meltEyeOriginalKnownW) = source.NativeEffectMemory!.MeltEye158!.ReadView(meltEyePoint + 6, 2).ReadSnapshot(0, 2);
                                Assert.Equal(meltEyeOriginalW, meltEyeW);
                                Assert.Equal(meltEyeOriginalKnownW, meltEyeKnownW);
                                meltEyeCurveMasks.Add(new { Curve = curve, Sample = sample, W = meltEyeW, KnownW = meltEyeKnownW });
                            }
                        }
                        File.WriteAllText(Path.Combine(output!, "curve-knownness.json"), JsonSerializer.Serialize(meltEyeCurveMasks));
                        ColdNext("fifteen-curves-live", current);
                        meltEyeCurvesRestored = true;
                    }
                    if (!meltEyeSlotsARestored && Enumerable.Range(0, NativeBattleMeltEye158Profile.SlotCountA).Any(index => meltEyeState.ReadDword(NativeBattleMeltEye158Profile.SlotBankA + (uint)(index * NativeBattleMeltEye158Profile.SlotStride)) != 0))
                    {
                        ColdNext("fork-slot-array-live", current);
                        meltEyeSlotsARestored = true;
                    }
                    if (!meltEyeSlotsBRestored && Enumerable.Range(0, NativeBattleMeltEye158Profile.SlotCountB).Any(index => meltEyeState.ReadDword(NativeBattleMeltEye158Profile.SlotBankB + (uint)(index * NativeBattleMeltEye158Profile.SlotStride)) != 0))
                    {
                        ColdNext("stream-slot-array-live", current);
                        meltEyeSlotsBRestored = true;
                    }
                    if (!meltEyeHeadRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.HeadMeshCallback && task.ReadWord(12) > 0))
                    {
                        ColdNext("head-mesh-live", current);
                        meltEyeHeadRestored = true;
                    }
                    if (!meltEyeStreamRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.StreamCallback && task.ReadWord(12) > 0))
                    {
                        ColdNext("curved-stream-live", current);
                        meltEyeStreamRestored = true;
                    }
                    if (!meltEyeImpactBirthRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.PathCallback && task.ReadWord(12) == 16))
                    {
                        ColdNext("before-impact-sprite-birth", current);
                        meltEyeImpactBirthRestored = true;
                    }
                    if (!meltEyeImpactSpriteRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.ImpactSpriteCallback && task.ReadWord(12) == 1))
                    {
                        Assert.True(original.MeltEye158Resources!.SpriteA.ProjectedFrame571C80(0).NextOffset >= 0);
                        ColdNext("impact-sprite-live", current);
                        meltEyeImpactSpriteRestored = true;
                    }
                    if (!meltEyeImpactRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.ImpactMeshCallback && task.ReadWord(12) > 0))
                    {
                        ColdNext("impact-mesh-live", current);
                        meltEyeImpactRestored = true;
                    }
                    if (!meltEyeForkRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.ForkEmitterCallback && task.ReadWord(12) > 0))
                    {
                        ColdNext("fork-emitter-live", current);
                        meltEyeForkRestored = true;
                    }
                    if (!meltEyeFeedbackRestored && prior.NativeGraphics!.OriginalEffect is { Pools.Length: 2 } meltEyeBefore)
                    {
                        for (int slot = 0; slot < meltEyeBefore.Pools[1].Slots.Length; slot++)
                        {
                            NativeBattleModulePoolTask meltEyeOld = meltEyeBefore.Pools[1].Slots[slot];
                            NativeBattleModulePoolTask meltEyeNew = after.Pools[1].Slots[slot];
                            bool meltEyeContinuedRetirement = (meltEyeOld.Flags & 1) != 0 && meltEyeOld.Callback == NativeBattleMeltEye158Profile.ImpactSpriteCallback && meltEyeNew.Callback == meltEyeOld.Callback && (meltEyeNew.Flags & 1) == 0 && meltEyeNew.ReadWord(12) == unchecked((ushort)(meltEyeOld.ReadWord(12) + 1));
                            if (meltEyeContinuedRetirement)
                            {
                                int meltEyePhase = meltEyeOld.ReadWord(12);
                                short meltEyeFeedback = original.MeltEye158Resources!.SpriteA.ProjectedFrame571C80(meltEyePhase).NextOffset;
                                Assert.True(meltEyeFeedback < 0);
                                File.WriteAllText(Path.Combine(output!, "sprite-feedback-retirement.json"), JsonSerializer.Serialize(new { Slot = slot, Before = meltEyeOld, After = meltEyeNew, Phase = meltEyePhase, NativeWord28 = meltEyeFeedback, NativeCondition = "Actual571C80 writes the selected original frame NextOffset to temporary WORD28 before rejection; full603999..6039AE return arithmetic retires only on negative feedback. Current staging may belong to a later callback and is not reread as historical feedback" }));
                                ColdNext("sprite-feedback-retired", current);
                                meltEyeFeedbackRestored = true;
                                break;
                            }
                        }
                    }
                }
                if (effectId == 217 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 3)
                {
                    NativeBattleEnergyBomber217State energyBomberState = current.NativeEffectMemory!.EnergyBomber217!;
                    uint energyBomberWorking = energyBomberState.ReadDword(NativeBattleEnergyBomber217Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { NativeBattleEnergyBomber217Profile.RootDescriptor, NativeBattleEnergyBomber217Profile.MainDescriptor, NativeBattleEnergyBomber217Profile.SpriteDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleEnergyBomber217Profile.RootBank, energyBomberWorking + NativeBattleEnergyBomber217Profile.MainOffset, energyBomberWorking }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0x684, 0x20 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 64 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    NativeBattleModulePoolTask? energyBomberMain = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEnergyBomber217Profile.MainCallback);
                    if (energyBomberMain is not null)
                    {
                        Assert.Equal(NativeBattleEnergyBomber217Profile.MainActAddress, energyBomberMain.ReadDword(0x44));
                        if (!energyBomberMainRestored && energyBomberMain.ReadDword(0x48) > 0)
                        {
                            Assert.Equal(0x398, energyBomberMain.ReadBytes(0x44, 0x398).Length);
                            ColdNext("main-act-live", current);
                            energyBomberMainRestored = true;
                        }
                        if (!energyBomberMorphRestored && energyBomberMain.ReadWord(12) < 67)
                        {
                            int energyBomberMorph = checked((int)(energyBomberWorking - NativeBattleEnergyBomber217Profile.ArenaAddress)) + NativeBattleEnergyBomber217Profile.MorphOffset;
                            (byte[] energyBomberBytes, bool[] energyBomberKnown) = current.NativeEffectMemory.Arena!.ReadSnapshot(energyBomberMorph, NativeBattleEnergyBomber217Profile.MorphBytes);
                            if (Enumerable.Range(0, 120).All(vertex => Enumerable.Range(0, 6).All(component => energyBomberKnown[vertex * 8 + component])))
                            {
                                Assert.Equal(960, energyBomberBytes.Length);
                                ColdNext("main-morph-live", current);
                                energyBomberMorphRestored = true;
                            }
                        }
                        if (!energyBomberTrailRestored && energyBomberMain.ReadWord(12) == 57)
                        {
                            Assert.Equal((ushort)2, energyBomberMain.ReadWord(0x40));
                            Assert.Equal((ushort)0, energyBomberMain.ReadWord(0x42));
                            Assert.Equal(6, energyBomberMain.ReadBytes(0x30, 6).Length);
                            Assert.Equal(6, energyBomberMain.ReadBytes(0x38, 6).Length);
                            ColdNext("cached-trail-live", current);
                            energyBomberTrailRestored = true;
                        }
                        if (!energyBomberImpactRestored && energyBomberMain.ReadWord(12) >= 64 && energyBomberMain.ReadDword(0x3E0) > 0)
                        {
                            Assert.Equal(NativeBattleEnergyBomber217Profile.ImpactActAddress, energyBomberMain.ReadDword(0x3DC));
                            Assert.Equal(0x2A8, energyBomberMain.ReadBytes(0x3DC, 0x2A8).Length);
                            ColdNext("impact-act-live", current);
                            energyBomberImpactRestored = true;
                        }
                    }
                    if (!energyBomberSpritesRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEnergyBomber217Profile.SpriteCallback && task.ReadWord(0x12) > 0))
                    {
                        ColdNext("sprites-live", current);
                        energyBomberSpritesRestored = true;
                    }
                }
                if (effectId == 101 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 2)
                {
                    NativeBattleEffect101State effect101State = current.NativeEffectMemory!.Effect101!;
                    uint effect101Working = effect101State.ReadDword(NativeBattleEffect101Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { NativeBattleEffect101Profile.RootDescriptor, NativeBattleEffect101Profile.MainDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleEffect101Profile.RootBank, effect101Working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0x2D0 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    NativeBattleModulePoolTask? effect101Main = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEffect101Profile.MainCallback);
                    if (effect101Main is not null)
                    {
                        ushort effect101Age = effect101Main.ReadWord(12);
                        Assert.Equal(NativeBattleEffect101Profile.MainActAddress, effect101Main.ReadDword(0x20));
                        if (!effect101ActRestored && effect101Main.ReadDword(0x24) > 0)
                        {
                            Assert.Equal(0x2B0, effect101Main.ReadBytes(0x20, 0x2B0).Length);
                            ColdNext("main-act-live", current);
                            effect101ActRestored = true;
                        }
                        if (!effect101ScaleRestored && current.NativeGraphics!.Models[contactActor].ScaleVectorAddress48 == NativeBattleEffect101Profile.ScaleAddress)
                        {
                            uint effect101Scale = effect101State.ReadDword(NativeBattleEffect101Profile.ScaleAddress);
                            Assert.Equal(effect101Scale, effect101State.ReadDword(NativeBattleEffect101Profile.ScaleAddress + 4));
                            Assert.Equal(effect101Scale, effect101State.ReadDword(NativeBattleEffect101Profile.ScaleAddress + 8));
                            ColdNext("target-pointed-scale", current);
                            effect101ScaleRestored = true;
                        }
                        if (!effect101MorphRestored && effect101Age >= 10 && effect101Age < 30)
                        {
                            NativeBattleEffectArena effect101Arena = current.NativeEffectMemory.Arena!;
                            int effect101Morph = checked((int)(effect101Working - NativeBattleEffect101Profile.ArenaAddress)) + NativeBattleEffect101Profile.MorphOffset;
                            for (int vertex = 0; vertex < 48; vertex++)
                            {
                                Assert.Equal(6, effect101Arena.Read(effect101Morph + vertex * 8, 6).Length);
                            }
                            ColdNext("interpolated-morph-live", current);
                            effect101MorphRestored = true;
                        }
                        if (!effect101VisibleRestored && effect101Age == 24)
                        {
                            Assert.Equal(0, current.NativeGraphics!.Models[contactActor].Flags0 & 4);
                            Assert.NotEqual(source.NativeGraphics!.Models[contactActor].Transform!.Position28, current.NativeGraphics.Models[contactActor].Transform!.Position28);
                            ColdNext("target-body-deformed", current);
                            effect101VisibleRestored = true;
                        }
                        if (!effect101FlagsRestored && effect101Age == 39)
                        {
                            Assert.Equal(0, current.NativeGraphics!.Models[contactActor].Flags0 & 12);
                            Assert.Equal(0u, current.NativeGraphics.Models[contactActor].ScaleVectorAddress48);
                            ColdNext("target-flags-cleared", current);
                            effect101FlagsRestored = true;
                        }
                    }
                }
                if (effectId == 134 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 5)
                {
                    NativeBattleBeamLaser134State beamLaserState = current.NativeEffectMemory!.BeamLaser134!;
                    uint beamLaserWorking = beamLaserState.ReadDword(NativeBattleBeamLaser134Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { 0x2520ED8, 0x2520EC8, 0x2520EB8, 0x2520EA8, 0x2520E98 }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleBeamLaser134Profile.RootBank, beamLaserWorking + 0x720, beamLaserWorking + 0x5E0, beamLaserWorking + 0x420, beamLaserWorking }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0x540, 0x14, 0x1C, 0x84 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 16, 16, 8 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(unchecked((ushort)source.NativeGraphics!.Models[caster].Flags0), beamLaserState.ReadWord(NativeBattleBeamLaser134Profile.SavedCasterFlagsGlobal));
                    Assert.Equal(8, current.NativeGraphics!.Models[caster].Flags0 & 8);
                    NativeBattleModulePoolTask? beamLaserMain = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBeamLaser134Profile.MainCallback);
                    if (beamLaserMain is not null)
                    {
                        Assert.Equal(0x12038DCu, beamLaserMain.ReadDword(0x1C));
                        Assert.Equal(0x12038DCu, beamLaserMain.ReadDword(0x74));
                        Assert.Equal(0x1205270u, beamLaserMain.ReadDword(0xCC));
                        Assert.Equal(0x12065F0u, beamLaserMain.ReadDword(0x2EC));
                        Assert.Equal(0x1204004u, beamLaserMain.ReadDword(0x390));
                        if (!beamLaserPrimaryRestored && beamLaserMain.ReadDword(0x20) > 0 && beamLaserMain.ReadDword(0x78) > 0)
                        {
                            ColdNext("primary-act-pair-live", current);
                            beamLaserPrimaryRestored = true;
                        }
                        if (!beamLaserBeamRestored && beamLaserMain.ReadDword(0xD0) > 0)
                        {
                            ColdNext("beam-act-live", current);
                            beamLaserBeamRestored = true;
                        }
                        if (!beamLaserEndcapRestored && beamLaserMain.ReadDword(0x2F0) > 0)
                        {
                            ColdNext("endcap-act-live", current);
                            beamLaserEndcapRestored = true;
                        }
                        if (!beamLaserRingRestored && beamLaserMain.ReadDword(0x394) > 0)
                        {
                            ColdNext("ring-act-live", current);
                            beamLaserRingRestored = true;
                        }
                        NativeBattleModulePoolTask? beamLaserBeforeMain = prior.NativeGraphics!.OriginalEffect is { Pools.Length: 5 } beamLaserBeforeEffect ? beamLaserBeforeEffect.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBeamLaser134Profile.MainCallback) : null;
                        if (!beamLaserStaticRestored && beamLaserBeforeMain?.ReadWord(12) == 14 && beamLaserMain.ReadWord(12) == 15)
                        {
                            ColdNext("static-mesh-live", current);
                            beamLaserStaticRestored = true;
                        }
                    }
                    NativeBattleEffectArena beamLaserMorph = NativeBattleEffectNodeContextMemory.Copy(current.NativeEffectMemory.Arena!, checked((int)(beamLaserWorking - NativeBattleBeamLaser134Profile.ArenaAddress)) + 0xC60, NativeBattleEffectArena.Clear().MarkUnknown(0, 392), 0, 392);
                    beamLaserMorphBaseline ??= beamLaserMorph;
                    for (int vertex = 0; vertex < 49; vertex++)
                    {
                        NativeBattleEffectArena beamLaserBeforeW = NativeBattleEffectNodeContextMemory.Copy(beamLaserMorphBaseline, vertex * 8 + 6, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, 2);
                        NativeBattleEffectArena beamLaserCurrentW = NativeBattleEffectNodeContextMemory.Copy(beamLaserMorph, vertex * 8 + 6, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, 2);
                        Assert.Equal(JsonSerializer.Serialize(beamLaserBeforeW), JsonSerializer.Serialize(beamLaserCurrentW));
                    }
                    if (!beamLaserMorphRestored && beamLaserRingRestored && Enumerable.Range(0, 49).Any(vertex => beamLaserMorph.Read(vertex * 8, 6).Any(value => value != 0)))
                    {
                        File.WriteAllText(Path.Combine(output!, "morph-knownness.json"), JsonSerializer.Serialize(new { InitialObserved = beamLaserMorphBaseline, Current = beamLaserMorph }));
                        ColdNext("morph-live", current);
                        beamLaserMorphRestored = true;
                    }
                    if (!beamLaserMovingRestored && after.Pools[3].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x6B0D90) >= 4)
                    {
                        ColdNext("moving-sprites-live", current);
                        beamLaserMovingRestored = true;
                    }
                    if (!beamLaserStationaryObserved && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6B0E30 && task.ReadWord(0x12) > 0))
                    {
                        ColdNext("stationary-sprite-live", current);
                        beamLaserStationaryObserved = true;
                    }
                    if (!beamLaserChildObserved && after.Pools[4].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6B0D30 && task.ReadDword(0x2C) == 0x1207150 && task.ReadDword(0x30) > 0))
                    {
                        ColdNext("child-act-live", current);
                        beamLaserChildObserved = true;
                    }
                }
                if (effectId == 134 && prior.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: > 0 } && after?.ReturnedDescriptor == 0)
                {
                    uint beamLaserSavedFlags = current.NativeEffectMemory!.BeamLaser134!.ReadDword(NativeBattleBeamLaser134Profile.SavedCasterFlagsGlobal);
                    int beamLaserPriorFlags = prior.NativeGraphics.Models[caster].Flags0;
                    int beamLaserCurrentFlags = current.NativeGraphics!.Models[caster].Flags0;
                    Assert.Equal((int)(beamLaserSavedFlags & 8), beamLaserCurrentFlags & 8);
                    Assert.Equal(beamLaserPriorFlags & ~8, beamLaserCurrentFlags & ~8);
                    File.WriteAllText(Path.Combine(output!, "caster-flags.json"), JsonSerializer.Serialize(new { Saved = beamLaserSavedFlags, Prior = beamLaserPriorFlags, Current = beamLaserCurrentFlags, PreservedMask = ~8, StationaryObserved = beamLaserStationaryObserved, ChildObserved = beamLaserChildObserved }));
                    beamLaserFlagsRestored = true;
                }
                if (effectId == 133 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 5)
                {
                    NativeBattleHypnotize133State hypnotizeState = current.NativeEffectMemory!.Hypnotize133!;
                    uint hypnotizeWorking = hypnotizeState.ReadDword(NativeBattleHypnotize133Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { 0x2520F50, 0x2520F40, 0x2520F30, 0x2520F20, 0x2520F10 }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { NativeBattleHypnotize133Profile.RootBank, hypnotizeWorking + 0xD40, hypnotizeWorking + 0x7C0, hypnotizeWorking + 0x280, hypnotizeWorking }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0xC4, 0xB0, 0x54, 0x14 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 8, 16, 32 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    if (!hypnotizeMainRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleHypnotize133Profile.MainCallback && task.ReadDword(0x1C) == 0x1207958 && task.ReadDword(0x20) > 0))
                    {
                        ColdNext("main-act-live", current);
                        hypnotizeMainRestored = true;
                    }
                    if (!hypnotizeAnchoredRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6B17C0 && task.ReadDword(0x18) > 0 && original.Hypnotize133Resources!.AnchoredActs.Any(act => act.Address == task.ReadDword(0x14))))
                    {
                        ColdNext("anchored-act-live", current);
                        hypnotizeAnchoredRestored = true;
                    }
                    if (!hypnotizeFadingRestored && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6B1900 && task.ReadDword(0x18) > 0 && original.Hypnotize133Resources!.FadingActs.Any(act => act.Address == task.ReadDword(0x14))))
                    {
                        ColdNext("fading-act-live", current);
                        hypnotizeFadingRestored = true;
                    }
                    if (!hypnotizeSpriteRestored && after.Pools[4].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6B1740 && task.ReadWord(0x12) > 0))
                    {
                        ColdNext("sprite-live", current);
                        hypnotizeSpriteRestored = true;
                    }
                }
                if (effectId is 177 or 178 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleNeedlesFamilyState needlesCurrent = effectId == 177 ? current.NativeEffectMemory!.ThousandNeedles177! : current.NativeEffectMemory!.TenThousandNeedles178!;
                    NativeBattleNeedlesFamilyProfile needlesProfile = needlesCurrent.Profile;
                    Assert.Equal(new uint[] { needlesProfile.RootDescriptor, needlesProfile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new uint[] { needlesProfile.RootBank, needlesProfile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    if (!needleRecordsRestored && Enumerable.Range(0, 300).Any(index => needlesCurrent.ReadDword(needlesProfile.NeedleBank + (uint)(56 * index)) != 0))
                    {
                        ColdNext("needle-records", current);
                        needleRecordsRestored = true;
                    }
                    if (!needleSparksRestored && Enumerable.Range(0, 300).Any(index => needlesCurrent.ReadDword(needlesProfile.SparkBank + (uint)(24 * index)) != 0))
                    {
                        ColdNext("spark-records", current);
                        needleSparksRestored = true;
                    }
                }
                if (effectId == 71 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 6)
                {
                    NativeBattleLaserEye71State laser = current.NativeEffectMemory!.LaserEye71!;
                    Assert.Equal(NativeBattleLaserEye71Profile.Pools.Select(pool => pool.Descriptor).ToArray(), after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 100, 88, 676, 148, 96, 64 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 2, 4, 3, 10, 60, 10 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    if (!laserBankRestored && laser.ExecutableBytes is { } bank && BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan()) == 1)
                    {
                        Assert.False(laser.ColdSourcePending);
                        Assert.Equal(0x60B0, bank.Length);
                        ColdNext("node-bank-initialized", current);
                        laserBankRestored = true;
                    }
                    if (!laserBindingRestored && laser.ReadResourcePointer(NativeBattleLaserEye71Profile.SavedBindingAddress) is not null)
                    {
                        ColdNext("caster-bound", current);
                        laserBindingRestored = true;
                    }
                    if (!laserBeamRestored && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleLaserEye71Controller.BeamCallback && task.ReadWord(0x8C) > 0))
                    {
                        ColdNext("beam-live", current);
                        laserBeamRestored = true;
                    }
                    if (!laserParticlesRestored && after.Pools[4].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleLaserEye71Controller.ParticleCallback))
                    {
                        ColdNext("particles-live", current);
                        laserParticlesRestored = true;
                    }
                }
                if (effectId == 130 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 4)
                {
                    NativeBattleEerieSoundWave130State eerieState = current.NativeEffectMemory!.EerieSoundWave130!;
                    uint eerieWorking = eerieState.ReadDword(NativeBattleEerieSoundWave130Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { NativeBattleEerieSoundWave130Profile.RootDescriptor, NativeBattleEerieSoundWave130Profile.MainDescriptor, NativeBattleEerieSoundWave130Profile.TravelDescriptor, NativeBattleEerieSoundWave130Profile.ImpactDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0x4CC, 0x74, 0xC0 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 12, 3 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleEerieSoundWave130Profile.RootBank, eerieWorking + NativeBattleEerieSoundWave130Profile.MainOffset, eerieWorking + NativeBattleEerieSoundWave130Profile.TravelOffset, eerieWorking }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    NativeBattleModulePoolTask? eerieMain = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEerieSoundWave130Profile.MainCallback);
                    if (!eerieMainRestored && eerieMain is not null && unchecked((int)eerieMain.ReadDword(0x24)) > 0)
                    {
                        Assert.Equal(NativeBattleEerieSoundWave130Profile.MainActAddress, eerieMain.ReadDword(0x20));
                        Assert.Equal(0x4AC, eerieMain.ReadBytes(0x20, 0x4AC).Length);
                        ColdNext("main-act-live", current);
                        eerieMainRestored = true;
                    }
                    NativeBattleModulePoolTask? eerieTravel = after.Pools[2].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEerieSoundWave130Profile.TravelCallback && unchecked((int)task.ReadDword(0x20)) > 0);
                    if (!eerieTravelRestored && eerieTravel is not null)
                    {
                        Assert.Equal(NativeBattleEerieSoundWave130Profile.TravelActAddress, eerieTravel.ReadDword(0x1C));
                        Assert.Equal(0x58, eerieTravel.ReadBytes(0x1C, 0x58).Length);
                        ColdNext("travel-act-live", current);
                        eerieTravelRestored = true;
                    }
                    NativeBattleModulePoolTask? eerieImpact = after.Pools[3].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEerieSoundWave130Profile.ImpactCallback && unchecked((int)task.ReadDword(0x1C)) > 0);
                    if (!eerieImpactRestored && eerieImpact is not null)
                    {
                        Assert.Equal(NativeBattleEerieSoundWave130Profile.ImpactActAddress, eerieImpact.ReadDword(0x18));
                        Assert.Equal(0xA8, eerieImpact.ReadBytes(0x18, 0xA8).Length);
                        Assert.Equal(0x1D972C0u, eerieImpact.ReadDword(0x14));
                        ColdNext("impact-act-live", current);
                        eerieImpactRestored = true;
                    }
                }
                if (effectId == 127 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 2)
                {
                    NativeBattlePetrifyStare127State state = current.NativeEffectMemory!.PetrifyStare127!;
                    uint working = state.ReadDword(NativeBattlePetrifyStare127Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { NativeBattlePetrifyStare127Profile.RootDescriptor, NativeBattlePetrifyStare127Profile.TargetDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0x4BC }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattlePetrifyStare127Profile.RootBank, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    NativeBattleModulePoolTask? targetTask = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattlePetrifyStare127Profile.TargetCallback);
                    if (targetTask is not null)
                    {
                        Assert.Equal(NativeBattlePetrifyStare127Profile.FirstActAddress, targetTask.ReadDword(0x24));
                        Assert.Equal(NativeBattlePetrifyStare127Profile.SecondActAddress, targetTask.ReadDword(0x1EC));
                        if (!petrifyActRestored && targetTask.ReadDword(0x28) > 0 && targetTask.ReadDword(0x1F0) > 0)
                        {
                            Assert.Equal(0x1C8, targetTask.ReadBytes(0x24, 0x1C8).Length);
                            Assert.Equal(0x2D0, targetTask.ReadBytes(0x1EC, 0x2D0).Length);
                            ColdNext("two-act-live", current);
                            petrifyActRestored = true;
                        }
                    }
                }
                if (effectId == 260 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 3)
                {
                    NativeBattleShockwavePulsar260State pulsar = current.NativeEffectMemory!.ShockwavePulsar260!;
                    uint working = pulsar.ReadDword(NativeBattleShockwavePulsar260Profile.WorkingGlobal);
                    int arenaOffset = checked((int)(working - NativeBattleShockwavePulsar260Profile.ArenaAddress));
                    Assert.Equal(new uint[] { NativeBattleShockwavePulsar260Profile.RootDescriptor, NativeBattleShockwavePulsar260Profile.MainDescriptor, NativeBattleShockwavePulsar260Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 0x1298, 0x114 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 12 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleShockwavePulsar260Profile.RootBank, working + 0x48, working + 0x2160 }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    NativeBattleModulePoolTask main = after.Pools[1].Slots[0];
                    NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
                    if (!pulsarCasterRestored && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == working + 0x1C0)
                    {
                        Assert.True(NativeBattleEffectTaskMemory.TryReadPointer(after.Pools, working + 0x1C8, out NativeBattleResourcePointer? animation));
                        Assert.NotNull(animation);
                        Assert.Equal(0x111B3E0u, animation!.StagingAddress);
                        Assert.Equal((ushort)1, main.ReadWord(0xE));
                        pulsarSavedPosition = ShockwavePulsar260SavedPosition(main);
                        pulsarCasterRestored = true;
                        ColdNext("caster-bound", current);
                    }
                    if (!pulsarActRestored && main.ReadWord(12) > 51 && main.ReadDword(0x19C) > 0)
                    {
                        Assert.Equal(640, arena.ReadSnapshot(arenaOffset + NativeBattleShockwavePulsar260Profile.MorphOffset, 640).Data.Length);
                        pulsarActRestored = true;
                        ColdNext("act-live", current);
                    }
                    if (!pulsarPuffsRestored && Enumerable.Range(0, 128).Any(index => arena.Read(arenaOffset + 0x12E0 + index * 16 + 15, 1)[0] != 0))
                    {
                        pulsarPuffsRestored = true;
                        ColdNext("puffs-live", current);
                    }
                    if (!pulsarSecondaryRestored && Enumerable.Range(0, 64).Any(index => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(arenaOffset + 0x1AE0 + index * 16, 2)) != 0))
                    {
                        pulsarSecondaryRestored = true;
                        ColdNext("secondary-particles-live", current);
                    }
                    if (!pulsarRisingRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleShockwavePulsar260Profile.TargetCallback && task.ReadWord(0x10) > 0))
                    {
                        Assert.NotEqual(source.NativeGraphics!.Models[contactActor].Transform!.Position28, current.NativeGraphics!.Models[contactActor].Transform!.Position28);
                        pulsarRisingRestored = true;
                        ColdNext("target-rising", current);
                    }
                    if (!pulsarBodyRestored && (current.NativeGraphics!.Models[contactActor].Flags0 & 4) != 0 && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleShockwavePulsar260Profile.LateTargetCallback && task.ReadWord(0x10) > 0))
                    {
                        ImmutableHashSet<NativeBattlePrimitiveReference> body = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: (model, resource) => ResolveShockwavePulsar260Resource(current, model, resource));
                        if (!body.IsEmpty)
                        {
                            pulsarBodyRestored = true;
                            ColdNext("target-custom-body", current);
                        }
                    }
                    if (!pulsarPositionRestored && pulsarRisingRestored && main.ReadWord(12) >= 215)
                    {
                        AssertShockwavePulsar260Position(current, contactActor, pulsarSavedPosition ?? throw new InvalidDataException("Shockwave Pulsar requires its captured original packed position."));
                        pulsarPositionRestored = true;
                        ColdNext("target-position-restored", current);
                    }
                    if (!pulsarIoRestored && current.NativeEffectMemory.EffectIo?.BaseOffset is int controller && arena.Read(controller + 3, 1)[0] == 2 && arena.Read(controller, 1)[0] != 0)
                    {
                        Assert.Equal(arenaOffset, controller);
                        byte[] first = arena.Read(controller + 8, 8);
                        byte[] second = arena.Read(controller + 16, 8);
                        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(first));
                        Assert.Equal((short)360, BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(2)));
                        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(second));
                        Assert.Equal((short)361, BinaryPrimitives.ReadInt16LittleEndian(second.AsSpan(2)));
                        Assert.Equal(working + 0x2E50, BinaryPrimitives.ReadUInt32LittleEndian(first.AsSpan(4)));
                        Assert.Equal(working + 0x2E50, BinaryPrimitives.ReadUInt32LittleEndian(second.AsSpan(4)));
                        pulsarIoRestored = true;
                        ColdNext("original-texture-io-pending", current);
                    }
                }
                if (effectId == 307 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleRayBomb307State state = current.NativeEffectMemory!.RayBomb307!;
                    uint working = state.ReadDword(NativeBattleRayBomb307Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { NativeBattleRayBomb307Profile.RootDescriptor, NativeBattleRayBomb307Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 2, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleRayBomb307Profile.RootBank, NativeBattleRayBomb307Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (!rayCasterRestored && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == NativeBattleRayBomb307Profile.SavedBindingAddress)
                    {
                        NativeBattleResourcePointer? animation = state.ReadResourcePointer(NativeBattleRayBomb307Profile.SavedBindingAddress + 8);
                        Assert.NotNull(animation);
                        Assert.Equal(NativeBattleRayBomb307Profile.CasterDatAddress + 0x14, animation!.StagingAddress);
                        ColdNext("caster-bound", current);
                        rayCasterRestored = true;
                    }
                    if (!rayCasterHiddenRestored && (current.NativeGraphics!.Models[caster].Flags0 & 8) != 0)
                    {
                        ColdNext("caster-bit8", current);
                        rayCasterHiddenRestored = true;
                    }
                    if (!rayParticlesRestored && Enumerable.Range(0, 100).Any(index => BinaryPrimitives.ReadUInt32LittleEndian(state.Particles.Read(index * 24, 4)) != 0))
                    {
                        ColdNext("particles-live", current);
                        rayParticlesRestored = true;
                    }
                    if (!rayGt3Restored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x5A1AE0 && task.ReadWord(12) > 0))
                    {
                        ColdNext("gt3-sequence", current);
                        rayGt3Restored = true;
                    }
                    NativeBattleModulePoolTask? rayTargetTask = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x5A25E0 && task.ReadWord(14) == contactActor);
                    if (rayTargetTask is not null)
                    {
                        Assert.Equal((ushort)0, rayTargetTask.ReadWord(0x1A));
                        NativeBattleEffectArena actual = NativeBattleRaldo19ModelMemory.Capture(current.NativeGraphics!.Models[contactActor]);
                        NativeBattleEffectArena saved = state.ReadView(NativeBattleRayBomb307Profile.SavedActorsAddress + (uint)contactActor * 20, 20);
                        if (!rayTargetDisplaced && !actual.Read(0x1C, 6).SequenceEqual(saved.Read(12, 6)))
                        {
                            Assert.Equal(0x1020, current.NativeGraphics.Models[contactActor].Flags0 & 0x1020);
                            ColdNext("target-displaced", current);
                            rayTargetDisplaced = true;
                        }
                    }
                    if (!rayTargetRestored && CrossedContactGate(prior, current, effectId))
                    {
                        AssertRayBomb307Restored(current, contactActor);
                        rayTargetRestored = true;
                    }
                    if (!textureLoadPendingRestored)
                    {
                        NativeBattleAuxiliaryTask? pending = current.NativeGraphics!.Auxiliary.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660 && task.Payload[0] == 2 && BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(4)) == working + NativeBattleRayBomb307Profile.TextureRestoreOffset);
                        if (pending is not null)
                        {
                            uint completion = BinaryPrimitives.ReadUInt32LittleEndian(pending.Payload.AsSpan().Slice(8));
                            int slot = checked((int)((completion - NativeBattleRayBomb307Profile.RootBank - 14) / 16));
                            Assert.InRange(slot, 0, 1);
                            Assert.Equal(NativeBattleRayBomb307Profile.RootBank + (uint)slot * 16 + 14, completion);
                            Assert.Equal(NativeBattleRayBomb307Profile.TextureCallback, after.Pools[0].Slots[slot].Callback);
                            Assert.Equal((ushort)0, after.Pools[0].Slots[slot].ReadWord(14));
                            ColdNext("texture-load-pending", current);
                            textureLoadPendingRestored = true;
                        }
                    }
                }
                if (effectId == 184 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleUltraWaves184State state = current.NativeEffectMemory!.UltraWaves184!;
                    Assert.False(state.ColdSourcesPending);
                    Assert.Equal(new uint[] { NativeBattleUltraWaves184Profile.RootDescriptor, NativeBattleUltraWaves184Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 2, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleUltraWaves184Profile.RootBank, NativeBattleUltraWaves184Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (!ultraCasterRestored && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == NativeBattleUltraWaves184Profile.SavedBindingAddress)
                    {
                        NativeBattleEffectArena binding = state.ReadView(NativeBattleUltraWaves184Profile.SavedBindingAddress, 32);
                        NativeBattleResourcePointer? animation = binding.ReadResourcePointer(8);
                        Assert.NotNull(animation);
                        Assert.Equal(NativeBattleUltraWaves184Profile.CasterDatAddress + 0x14, animation!.StagingAddress);
                        ColdNext("caster-bound", current);
                        ultraCasterRestored = true;
                    }
                    if (!ultraEmittersRestored && after.Pools[1].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleUltraWaves184Profile.EmitterCallback) == 4)
                    {
                        ColdNext("four-emitters", current);
                        ultraEmittersRestored = true;
                    }
                    if (!ultraWarpRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleUltraWaves184Profile.WarpCallback && task.ReadWord(12) > 0))
                    {
                        Assert.Equal(NativeBattleUltraWaves184Profile.GridLength, state.Grid.Read(0, NativeBattleUltraWaves184Profile.GridLength).Length);
                        Assert.All(current.NativeStage!.Groups, group => Assert.Equal(0, group.Flags & 2));
                        ColdNext("warp-grid", current);
                        ultraWarpRestored = true;
                    }
                    if (!textureLoadPendingRestored)
                    {
                        NativeBattleAuxiliaryTask? pending = current.NativeGraphics!.Auxiliary.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660 && task.Payload[0] == 2);
                        if (pending is not null)
                        {
                            uint completion = BinaryPrimitives.ReadUInt32LittleEndian(pending.Payload.AsSpan().Slice(8));
                            int slot = checked((int)((completion - NativeBattleUltraWaves184Profile.RootBank - 0xE) / 16));
                            Assert.InRange(slot, 0, 1);
                            Assert.Equal(NativeBattleUltraWaves184Profile.RootBank + (uint)slot * 16 + 0xE, completion);
                            NativeBattleModulePoolTask wait = after.Pools[0].Slots[slot];
                            Assert.Equal(NativeBattleUltraWaves184Profile.WaitCallback, wait.Callback);
                            Assert.Equal((ushort)0, wait.ReadWord(0xE));
                            ColdNext("texture-load-pending", current);
                            textureLoadPendingRestored = true;
                        }
                    }
                }
                if (effectId == 135 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 5)
                {
                    NativeBattleReflectBeam135State reflectState = current.NativeEffectMemory!.ReflectBeam135!;
                    uint reflectWorking = reflectState.ReadDword(NativeBattleReflectBeam135Profile.WorkingGlobal);
                    Assert.Equal(new uint[] { NativeBattleReflectBeam135Profile.RootDescriptor, NativeBattleReflectBeam135Profile.MainDescriptor, NativeBattleReflectBeam135Profile.SecondaryDescriptor, NativeBattleReflectBeam135Profile.TargetDescriptor, NativeBattleReflectBeam135Profile.ParticleDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 736, 620, 812, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 8, 3, 96 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleReflectBeam135Profile.RootBank, reflectWorking + 0x2A64, reflectWorking + 0x1704, reflectWorking + 0xD80, reflectWorking }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    NativeBattleModulePoolTask? reflectMain = after.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleReflectBeam135Profile.MainCallback);
                    if (!reflectMainRestored && reflectMain is not null && reflectMain.ReadDword(0x28) == NativeBattleReflectBeam135Profile.MainActAddress && reflectMain.ReadDword(0x2C) > 0)
                    {
                        ColdNext("main-act", current);
                        reflectMainRestored = true;
                    }
                    if (!reflectTargetRestored && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleReflectBeam135Profile.TargetCallback && task.ReadDword(0x1C) == NativeBattleReflectBeam135Profile.TargetActAddress && task.ReadDword(0x20) > 0))
                    {
                        ColdNext("target-act", current);
                        reflectTargetRestored = true;
                    }
                    NativeBattleModulePoolTask[] reflectSecondary = after.Pools[2].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleReflectBeam135Profile.SecondaryCallback).ToArray();
                    if (!reflectHeadRestored && reflectSecondary.Any(task => task.ReadWord(12) is > 0 and <= 13 && task.ReadDword(0x1C) == NativeBattleReflectBeam135Profile.SecondaryActAddress && task.ReadDword(0x20) > 0))
                    {
                        ColdNext("secondary-head-static", current);
                        reflectHeadRestored = true;
                    }
                    if (!reflectTailRestored && reflectSecondary.Any(task => task.ReadWord(14) != 0 && task.ReadDword(0x214) == NativeBattleReflectBeam135Profile.ImpactActAddress && task.ReadDword(0x218) > 0))
                    {
                        ColdNext("secondary-tail", current);
                        reflectTailRestored = true;
                    }
                    if (!reflectBonesRestored && reflectSecondary.Length != 0)
                    {
                        NativeBattleGraphicsModel model = current.NativeGraphics!.Models[caster];
                        NativeBattlePoseSource pose = reflectPoseSource ?? throw new InvalidDataException("Reflect Beam requires its original caster pose source.");
                        NativeBattleBodyBoneMatrices publication = model.CurrentBodyBoneMatrices ?? throw new InvalidDataException("Reflect Beam lost its actual current bone publication.");
                        publication.Validate();
                        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
                        reflectBonePublication = JsonSerializer.Serialize(new { publication.SkeletonSha256, Count = bones.Length, Scope = "Actual retained publication after native secondary scan/restore; no cross-step pose equivalence claim" });
                        ColdNext("caster-bone-publication", current);
                        reflectBonesRestored = true;
                    }
                    foreach (uint callback in new[] { NativeBattleReflectBeam135Profile.ParticleACallback, NativeBattleReflectBeam135Profile.ParticleBCallback, NativeBattleReflectBeam135Profile.ParticleCCallback })
                    {
                        if (!reflectParticlesRestored.Contains(callback) && after.Pools[4].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == callback && task.ReadWord(18) > 0))
                        {
                            ColdNext("particle-" + callback.ToString("X"), current);
                            reflectParticlesRestored.Add(callback);
                        }
                    }
                    int reflectOffset = checked((int)(reflectWorking - NativeBattleReflectBeam135Profile.ArenaAddress)) + NativeBattleReflectBeam135Profile.MorphOffset;
                    for (int vertex = 0; vertex < 68; vertex++)
                    {
                        (byte[] oldW, bool[] oldKnown) = prior.NativeEffectMemory!.Arena!.ReadSnapshot(reflectOffset + vertex * 8 + 6, 2);
                        (byte[] newW, bool[] newKnown) = current.NativeEffectMemory.Arena!.ReadSnapshot(reflectOffset + vertex * 8 + 6, 2);
                        Assert.Equal(oldKnown, newKnown);
                        for (int lane = 0; lane < 2; lane++)
                        {
                            if (oldKnown[lane])
                            {
                                Assert.Equal(oldW[lane], newW[lane]);
                            }
                        }
                    }
                    if (!reflectMorphRestored)
                    {
                        (byte[] bytes, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(reflectOffset, NativeBattleReflectBeam135Profile.MorphBytes);
                        if (Enumerable.Range(0, 68).All(vertex => known.Skip(vertex * 8).Take(6).All(value => value)) && Enumerable.Range(0, 68).Any(vertex => bytes.Skip(vertex * 8).Take(6).Any(value => value != 0)))
                        {
                            ColdNext("act-morph-544", current);
                            reflectMorphRestored = true;
                        }
                    }
                    DirectedNodePacketState reflectPackets = Director(current);
                    if (reflectPackets.PrimaryDrawCursor is { } cursor && reflectPackets.SecondaryDrawCursor is { } end && cursor > end - reflectPackets.DrawStep)
                    {
                        reflectPacketBanks.Add(end - reflectPackets.DrawStep);
                    }
                }
                if (effectId == 126 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 6)
                {
                    NativeBattleElectricDischarge126State state = current.NativeEffectMemory!.ElectricDischarge126!;
                    Assert.False(state.ColdSourcesPending);
                    Assert.NotNull(state.FlashThreshold);
                    uint working = state.ReadDword(NativeBattleElectricDischarge126Profile.WorkingGlobal);
                    int sourceOffset = checked((int)(working - NativeBattleElectricDischarge126Profile.ArenaAddress));
                    Assert.Equal(new uint[] { NativeBattleElectricDischarge126Profile.RootDescriptor, NativeBattleElectricDischarge126Profile.MainDescriptor, NativeBattleElectricDischarge126Profile.WorldTrailDescriptor, NativeBattleElectricDischarge126Profile.BoneTrailDescriptor, NativeBattleElectricDischarge126Profile.SpriteDescriptor, NativeBattleElectricDischarge126Profile.ActDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 20, 60, 40, 24, 212 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 64, 64, 32, 8 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleElectricDischarge126Profile.RootBank, working + 0x22A0, working + 0x13A0, working + 0x9A0, working + 0x6A0, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    foreach ((int bank, int free) in new[] { (NativeBattleElectricDischarge126Profile.WorldPointsOffset, NativeBattleElectricDischarge126Profile.WorldFreePointerOffset), (NativeBattleElectricDischarge126Profile.BonePointsOffset, NativeBattleElectricDischarge126Profile.BoneFreePointerOffset) })
                    {
                        uint cached = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(sourceOffset + free, 4));
                        if (cached != 0)
                        {
                            Assert.InRange(cached, working + (uint)bank, working + (uint)bank + 511 * 40);
                            Assert.Equal(0u, (cached - working - (uint)bank) % 40);
                        }
                    }
                    if (!dischargeWorldRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleElectricDischarge126Profile.WorldTrailCallback))
                    {
                        ColdNext("world-points", current);
                        dischargeWorldRestored = true;
                    }
                    NativeBattleModulePoolTask? bone = after.Pools[3].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleElectricDischarge126Profile.BoneTrailCallback);
                    if (!dischargeBoneRestored && bone is not null)
                    {
                        NativeBattleResourceRowReference binding = bone.ReadResourceRowReference(36);
                        Assert.Equal(current.NativeGraphics!.Models[caster].ActiveResourceRow100, binding.CpuRow);
                        Assert.Null(binding.StagingAddress);
                        Assert.Throws<NotSupportedException>(() => bone.ReadDword(36));
                        Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(bone))!.ReadResourceRowReference(36)));
                        ColdNext("bone-points-cpu-row", current);
                        dischargeBoneRestored = true;
                    }
                    if (!dischargeActRestored && after.Pools[5].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleElectricDischarge126Profile.ActCallback && task.ReadDword(0x10) > 0))
                    {
                        ColdNext("target-act", current);
                        dischargeActRestored = true;
                    }
                    if (!dischargeMorphRestored && dischargeActRestored)
                    {
                        (byte[] morph, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(sourceOffset + NativeBattleElectricDischarge126Profile.MorphOffset, NativeBattleElectricDischarge126Profile.MorphBytes);
                        if (known.All(value => value) && morph.Any(value => value != 0))
                        {
                            ColdNext("act-morph", current);
                            dischargeMorphRestored = true;
                        }
                    }
                    if (!dischargeScreenFlashRestored && current.NativeGraphics!.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300))
                    {
                        ColdNext("screen-flash", current);
                        dischargeScreenFlashRestored = true;
                    }
                    if (!dischargeFlashRestored && Enumerable.Range(0, 3).Any(actor => (current.NativeGraphics!.Models[actor].Flags0 & 0x800) != 0 && current.NativeGraphics.Models[actor].Color40 != state.ReadDword(NativeBattleElectricDischarge126Profile.SavedColorsGlobal + (uint)actor * 4)))
                    {
                        ColdNext("party-color-flash", current);
                        dischargeFlashRestored = true;
                    }
                    DirectedNodePacketState packets = Director(current);
                    if (packets.PrimaryDrawCursor is { } cursor && packets.SecondaryDrawCursor is { } end && cursor > end - packets.DrawStep)
                    {
                        dischargePacketBanks.Add(end - packets.DrawStep);
                    }
                }
                if (effectId == 275 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 6)
                {
                    NativeBattleElectrocute275State state = current.NativeEffectMemory!.Electrocute275!;
                    Assert.False(state.ColdSourcesPending);
                    Assert.NotNull(state.FlashThreshold);
                    uint working = state.ReadDword(NativeBattleElectrocute275Profile.WorkingGlobal);
                    int sourceOffset = checked((int)(working - NativeBattleElectrocute275Profile.ArenaAddress));
                    Assert.Equal(new uint[] { NativeBattleElectrocute275Profile.RootDescriptor, NativeBattleElectrocute275Profile.MainDescriptor, NativeBattleElectrocute275Profile.WorldTrailDescriptor, NativeBattleElectrocute275Profile.BoneTrailDescriptor, NativeBattleElectrocute275Profile.SpriteDescriptor, NativeBattleElectrocute275Profile.ActDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 52, 60, 40, 24, 212 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 64, 64, 32, 8 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleElectrocute275Profile.RootBank, working + 0x22A0, working + 0x13A0, working + 0x9A0, working + 0x6A0, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    uint savedBinding = after.Pools[1].BankAddress + 0x14;
                    if (!electroCasterRestored && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == savedBinding)
                    {
                        Assert.True(NativeBattleEffectTaskMemory.TryReadPointer(after.Pools, savedBinding + 8, out NativeBattleResourcePointer? pointer));
                        Assert.NotNull(pointer);
                        ColdNext("caster-bound", current);
                        electroCasterRestored = true;
                    }
                    if (!electroWorldRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x6887B0) && Enumerable.Range(0, NativeBattleElectrocute275Profile.PointCount).Any(index => BinaryPrimitives.ReadInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(sourceOffset + NativeBattleElectrocute275Profile.WorldPointsOffset + index * NativeBattleElectrocute275Profile.PointStride + 6, 2)) != -1))
                    {
                        ColdNext("world-points", current);
                        electroWorldRestored = true;
                    }
                    if (!electroBoneRestored && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x689280) && Enumerable.Range(0, NativeBattleElectrocute275Profile.PointCount).Any(index => BinaryPrimitives.ReadInt16LittleEndian(current.NativeEffectMemory.Arena!.Read(sourceOffset + NativeBattleElectrocute275Profile.BonePointsOffset + index * NativeBattleElectrocute275Profile.PointStride, 2)) != -1))
                    {
                        ColdNext("bone-points", current);
                        electroBoneRestored = true;
                    }
                    if (!electroActRestored && after.Pools[5].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleElectrocute275Profile.ActCallback && task.ReadDword(0x10) > 0))
                    {
                        ColdNext("target-act", current);
                        electroActRestored = true;
                    }
                    if (!electroScreenFlashRestored && current.NativeGraphics!.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300))
                    {
                        ColdNext("screen-flash", current);
                        electroScreenFlashRestored = true;
                    }
                    NativeBattleGraphicsModel targetModel = current.NativeGraphics!.Models[contactActor];
                    if (!electroFlashRestored && (targetModel.Flags0 & 0x800) != 0 && targetModel.Color40 is { } flashColor && flashColor != state.ReadDword(NativeBattleElectrocute275Profile.SavedColorsGlobal + (uint)contactActor * 4))
                    {
                        ColdNext("actor-flash", current);
                        electroFlashRestored = true;
                    }
                }
                if (effectId == 317 && after?.ReturnedDescriptor is > 0 && after.Pools.Length == 4)
                {
                    NativeBattleAcid317State state = current.NativeEffectMemory!.Acid317!;
                    uint working = state.ReadDword(NativeBattleAcid317Profile.WorkingGlobal);
                    int sourceOffset = checked((int)(working - NativeBattleAcid317Profile.ArenaAddress));
                    Assert.Equal(new uint[] { NativeBattleAcid317Profile.RootDescriptor, NativeBattleAcid317Profile.TargetDescriptor, NativeBattleAcid317Profile.TrailDescriptor, NativeBattleAcid317Profile.ParticleDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 616, 80, 32 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 1, 8, 128 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleAcid317Profile.RootBank, working + 0x1280, working + 0x1000, working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (!acidCasterRestored && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == working + NativeBattleAcid317Profile.BindingOffset)
                    {
                        Assert.NotNull(current.NativeEffectMemory.Arena!.ReadResourcePointer(sourceOffset + NativeBattleAcid317Profile.BindingOffset + 8));
                        ColdNext("caster-bound", current);
                        acidCasterRestored = true;
                    }
                    if (!acidTrailRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x660AD0 && unchecked((short)task.ReadWord(14)) >= 2))
                    {
                        ColdNext("trail-points", current);
                        acidTrailRestored = true;
                    }
                    NativeBattleModulePoolTask? targetTask = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAcid317Profile.TargetCallback);
                    if (!acidActRestored && targetTask is not null && targetTask.ReadWord(12) is >= 27 and < 33 && targetTask.ReadDword(0x24) > 0)
                    {
                        ColdNext("target-act", current);
                        acidActRestored = true;
                    }
                    if (!acidBodyRestored && targetTask is not null && targetTask.ReadWord(12) is >= 34 and <= 72 && !NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: (model, resource) => ResolveAcid317Resource(current, model, resource)).IsEmpty)
                    {
                        Assert.Equal(4, current.NativeGraphics!.Models[contactActor].Flags0 & 4);
                        ColdNext("deformed-body", current);
                        acidBodyRestored = true;
                    }
                }
                if (effectId == 305 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleGatlingGun305State state = current.NativeEffectMemory!.GatlingGun305!;
                    Assert.Equal(2, after.Pools.Length);
                    Assert.Equal(new uint[] { NativeBattleGatlingGun305Profile.RootDescriptor, NativeBattleGatlingGun305Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleGatlingGun305Profile.RootBank, NativeBattleGatlingGun305Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (!gatlingCasterRestored && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == NativeBattleGatlingGun305Profile.BindingSave)
                    {
                        Assert.NotNull(state.ReadResourcePointer(NativeBattleGatlingGun305Profile.BindingSave + 8));
                        ColdNext("caster-bound", current);
                        gatlingCasterRestored = true;
                    }
                    if (!gatlingSlotARestored && Enumerable.Range(0, NativeBattleGatlingGun305Profile.SlotCount).Any(index => state.ReadDword(NativeBattleGatlingGun305Profile.SlotBankA + (uint)(index * NativeBattleGatlingGun305Profile.SlotStride)) != 0))
                    {
                        ColdNext("slot-bank-a", current);
                        gatlingSlotARestored = true;
                    }
                    if (!gatlingSlotBRestored && Enumerable.Range(0, NativeBattleGatlingGun305Profile.SlotCount).Any(index => state.ReadDword(NativeBattleGatlingGun305Profile.SlotBankB + (uint)(index * NativeBattleGatlingGun305Profile.SlotStride)) != 0))
                    {
                        ColdNext("slot-bank-b", current);
                        gatlingSlotBRestored = true;
                    }
                }
                if (effectId == 306 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleDegenerator306State state = current.NativeEffectMemory!.Degenerator306!;
                    Assert.Equal(2, after.Pools.Length);
                    Assert.Equal(new uint[] { NativeBattleDegenerator306Profile.RootDescriptor, NativeBattleDegenerator306Profile.ChildDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 16, 36 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 2, 100 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    Assert.Equal(new uint[] { NativeBattleDegenerator306Profile.RootBank, NativeBattleDegenerator306Profile.ChildBank }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                    if (!degeneratorCasterRestored && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == NativeBattleDegenerator306Profile.BindingSave)
                    {
                        Assert.NotNull(state.ReadResourcePointer(NativeBattleDegenerator306Profile.BindingSave + 8));
                        ColdNext("caster-bound", current);
                        degeneratorCasterRestored = true;
                    }
                    NativeBattleModulePoolTask? body = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x5A47C0);
                    if ((contact.Primary.HitFlags3 & 4) != 0)
                    {
                        Assert.Null(body);
                        Assert.DoesNotContain(after.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback is 0x5A5540 or 0x5A4D80);
                    }
                    else
                    {
                        if (!degeneratorBodyRestored && body is not null && body.ReadWord(12) > 1 && !NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: (model, resource) => ResolveDegenerator306Resource(current, model, resource)).IsEmpty)
                        {
                            ColdNext("deformed-body", current);
                            degeneratorBodyRestored = true;
                        }
                        if (!degeneratorEmitterRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == 0x5A4D80 && task.ReadWord(12) > 0))
                        {
                            ColdNext("body-slot-emitter", current);
                            degeneratorEmitterRestored = true;
                        }
                    }
                }
                if (effectId == 131 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleBadBreath131State breath131State = current.NativeEffectMemory!.BadBreath131!;
                    uint breath131Working = breath131State.ReadDword(NativeBattleBadBreath131Profile.WorkingGlobal);
                    Assert.False(breath131State.PendingColdSources);
                    Assert.Equal(0x1480, breath131State.ReadMesh(NativeBattleBadBreath131Profile.MeshAAddress).Length);
                    Assert.Equal(0x1480, breath131State.ReadMesh(NativeBattleBadBreath131Profile.MeshBAddress).Length);
                    if (after.Pools.Length == 1)
                    {
                        Assert.Equal(original.BadBreath131Resources!.MeshAInitial.ToArray(), breath131State.MeshA!.Value.ToArray());
                        Assert.Equal(original.BadBreath131Resources.MeshBInitial.ToArray(), breath131State.MeshB!.Value.ToArray());
                    }
                    if (after.Pools.Length == 4)
                    {
                        Assert.Equal(new uint[] { NativeBattleBadBreath131Profile.RootDescriptor, NativeBattleBadBreath131Profile.MainDescriptor, NativeBattleBadBreath131Profile.TargetDescriptor, NativeBattleBadBreath131Profile.ParticleDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                        Assert.Equal(new ushort[] { 20, 52, 324, 24 }, after.Pools.Select(pool => pool.Stride).ToArray());
                        Assert.Equal(new int[] { 1, 1, 3, 64 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                        Assert.Equal(new uint[] { NativeBattleBadBreath131Profile.RootBank, breath131Working + NativeBattleBadBreath131Profile.MainOffset, breath131Working + NativeBattleBadBreath131Profile.TargetOffset, breath131Working }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                        int breath131Scratch = checked((int)(breath131Working - NativeBattleBadBreath131Profile.ArenaAddress)) + NativeBattleBadBreath131Profile.ScratchOffset;
                        string breath131CurrentW = BadBreath131ScratchW(current.NativeEffectMemory.Arena!, breath131Scratch);
                        breath131ScratchW ??= breath131CurrentW;
                        Assert.Equal(breath131ScratchW, breath131CurrentW);
                        if (!breath131MeshesRestored)
                        {
                            Assert.NotEqual(original.BadBreath131Resources!.MeshAInitial.ToArray(), breath131State.MeshA!.Value.ToArray());
                            Assert.NotEqual(original.BadBreath131Resources.MeshBInitial.ToArray(), breath131State.MeshB!.Value.ToArray());
                            Assert.Equal(new byte[4], breath131State.MeshA!.Value.AsSpan().Slice(0x530, 4).ToArray());
                            Assert.Equal(new byte[4], breath131State.MeshB!.Value.AsSpan().Slice(0x530, 4).ToArray());
                            ColdNext("meshes-preprocessed", current);
                            breath131MeshesRestored = true;
                        }
                        NativeBattleModulePoolTask? breath131Main = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath131Profile.MainCallback);
                        if (!breath131DeformRestored && breath131Main is not null && breath131Main.ReadWord(12) >= 16)
                        {
                            Assert.Equal(6, breath131Main.ReadBytes(0x2C, 6).Length);
                            for (int vertex = 0; vertex < 165; vertex++)
                            {
                                Assert.Equal(6, current.NativeEffectMemory.Arena!.Read(breath131Scratch + vertex * 8, 6).Length);
                            }
                            DirectedNodePacketState breath131Span = Director(current);
                            Assert.True(breath131Span.PrimaryDrawCursor > breath131Span.SecondaryDrawCursor - breath131Span.DrawStep);
                            ColdNext("strip-deformation", current);
                            breath131DeformRestored = true;
                        }
                        NativeBattleModulePoolTask[] breath131Targets = after.Pools[2].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath131Profile.TargetCallback).ToArray();
                        if (!breath131TargetRestored && breath131Targets.Any(task => task.ReadDword(0x1C) == NativeBattleBadBreath131Profile.TargetActAddress && task.ReadDword(0x20) > 0))
                        {
                            Assert.All(breath131Targets, task =>
                            {
                                Assert.Equal((byte)0, task.ReadByte(14));
                                Assert.Equal(contactActor, task.ReadByte(15));
                            });
                            ColdNext("target-act", current);
                            breath131TargetRestored = true;
                        }
                        if (!breath131ParticlesRestored && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath131Profile.ParticleCallback && task.ReadWord(12) > 0))
                        {
                            ColdNext("particles-live", current);
                            breath131ParticlesRestored = true;
                        }
                        if (!breath131ColorRestored && breath131Targets.Any(task => task.ReadWord(12) == 41))
                        {
                            NativeBattleModulePoolTask breath131RestoredTarget = Assert.Single(breath131Targets, task => task.ReadWord(12) == 41);
                            Assert.Equal(breath131RestoredTarget.ReadDword(0x18), current.NativeGraphics!.Models[contactActor].Color40);
                            Assert.NotEqual(0, current.NativeGraphics.Models[contactActor].Flags0 & 0x800);
                            ColdNext("target-color-restored", current);
                            breath131ColorRestored = true;
                        }
                        DirectedNodePacketState breath131PacketState = Director(current);
                        if (breath131PacketState.SecondaryDrawCursor.HasValue)
                        {
                            breath131PacketBanks.Add(breath131PacketState.SecondaryDrawCursor.Value - breath131PacketState.DrawStep);
                        }
                    }
                }
                if (effectId == 300 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleBadBreath300State state = current.NativeEffectMemory!.BadBreath300!;
                    Assert.False(state.PendingColdSources);
                    Assert.Equal(5248, state.MeshA!.Value.Length);
                    Assert.Equal(5248, state.MeshB!.Value.Length);
                    Assert.Equal(NativeBattleBadBreath300Profile.ArenaAddress, state.ReadDword(NativeBattleBadBreath300Profile.WorkingGlobal));
                    if (after.Pools.Length == 1)
                    {
                        Assert.Equal(original.BadBreath300Resources!.MeshAInitial.ToArray(), state.MeshA.Value.ToArray());
                        Assert.Equal(original.BadBreath300Resources.MeshBInitial.ToArray(), state.MeshB.Value.ToArray());
                    }
                    string scratchW = BadBreath300ScratchW(current.NativeEffectMemory.Arena!);
                    badBreathScratchW ??= scratchW;
                    Assert.Equal(badBreathScratchW, scratchW);
                    if (after.Pools.Length == 4)
                    {
                        Assert.Equal(new uint[] { NativeBattleBadBreath300Profile.RootDescriptor, NativeBattleBadBreath300Profile.MainDescriptor, NativeBattleBadBreath300Profile.TargetDescriptor, NativeBattleBadBreath300Profile.ParticleDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                        Assert.Equal(new ushort[] { 20, 0x3C, 0x140, 0x18 }, after.Pools.Select(pool => pool.Stride).ToArray());
                        Assert.Equal(new int[] { 1, 1, 4, 32 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                        Assert.Equal(new uint[] { NativeBattleBadBreath300Profile.RootBank, NativeBattleBadBreath300Profile.ArenaAddress + 0x800, NativeBattleBadBreath300Profile.ArenaAddress + 0x300, NativeBattleBadBreath300Profile.ArenaAddress }, after.Pools.Select(pool => pool.BankAddress).ToArray());
                        if (!badBreathCasterRestored)
                        {
                            Assert.Equal((uint?)(NativeBattleBadBreath300Profile.ArenaAddress + NativeBattleBadBreath300Profile.BindingOffset), current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100);
                            Assert.NotNull(current.NativeEffectMemory.Arena!.ReadResourcePointer(NativeBattleBadBreath300Profile.BindingOffset + 8));
                            Assert.DoesNotContain("TargetScaleStack", JsonSerializer.Serialize(state));
                            for (int vertex = 0; vertex < 165; vertex++)
                            {
                                Assert.Equal(original.BadBreath300Resources!.MeshAInitial.AsSpan().Slice(8 + vertex * 8, 6).ToArray(), state.MeshA.Value.AsSpan().Slice(8 + vertex * 8, 6).ToArray());
                                Assert.Equal(original.BadBreath300Resources.MeshBInitial.AsSpan().Slice(8 + vertex * 8, 6).ToArray(), state.MeshB.Value.AsSpan().Slice(8 + vertex * 8, 6).ToArray());
                            }
                            ColdNext("caster-bound", current);
                            badBreathCasterRestored = true;
                        }
                        NativeBattleModulePoolTask? main = after.Pools[1].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath300Profile.MainCallback);
                        if (!badBreathDeformRestored && main is not null && unchecked((short)main.ReadWord(12)) >= 9 && unchecked((int)main.ReadDword(0x30)) > 0)
                        {
                            Assert.Equal(6, main.ReadBytes(0x34, 6).Length);
                            for (int vertex = 0; vertex < 165; vertex++)
                            {
                                Assert.Equal(6, current.NativeEffectMemory.Arena!.Read(NativeBattleBadBreath300Profile.ScratchOffset + vertex * 8, 6).Length);
                            }
                            ColdNext("mouth-and-deform", current);
                            badBreathDeformRestored = true;
                        }
                        if (!badBreathTargetRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath300Profile.TargetCallback) && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath300Profile.ParticleCallback))
                        {
                            Assert.All(after.Pools[2].Slots.Where(task => (task.Flags & 1) != 0), task =>
                            {
                                Assert.Equal(original.BadBreath300Resources!.Target.Address, task.ReadDword(0x18));
                                Assert.Equal((byte)0, task.ReadByte(14));
                                Assert.Equal(contactActor, task.ReadByte(15));
                            });
                            ColdNext("target-and-particles", current);
                            badBreathTargetRestored = true;
                        }
                    }
                }
                if (effectId == 299 && after is { ReturnedDescriptor: > 0, Pools.Length: 4 })
                {
                    NativeBattleMicroMissiles299State state = current.NativeEffectMemory!.MicroMissiles299!;
                    uint working = state.ReadDword(NativeBattleMicroMissiles299Profile.WorkingGlobal);
                    Assert.Equal(NativeBattleMicroMissiles299Profile.ArenaAddress, working);
                    Assert.Equal(new uint[] { NativeBattleMicroMissiles299Profile.RootDescriptor, NativeBattleMicroMissiles299Profile.MissileDescriptor, NativeBattleMicroMissiles299Profile.ImpactDescriptor, NativeBattleMicroMissiles299Profile.ParticleDescriptor }, after.Pools.Select(pool => pool.DescriptorAddress).ToArray());
                    Assert.Equal(new ushort[] { 20, 0x80C, 0x144, 0x24 }, after.Pools.Select(pool => pool.Stride).ToArray());
                    Assert.Equal(new int[] { 1, 6, 6, 96 }, after.Pools.Select(pool => pool.Slots.Length).ToArray());
                    if (!microMissilesCasterRestored)
                    {
                        Assert.Equal((uint?)(working + NativeBattleMicroMissiles299Profile.BindingOffset), current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100);
                        Assert.NotNull(current.NativeEffectMemory.Arena!.ReadResourcePointer(NativeBattleMicroMissiles299Profile.BindingOffset + 8));
                        ColdNext("caster-bound", current);
                        microMissilesCasterRestored = true;
                    }
                    if (!microMissilesMissileRestored && after.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMicroMissiles299Profile.MissileCallback))
                    {
                        Assert.All(after.Pools[1].Slots.Where(task => (task.Flags & 1) != 0), task => Assert.Equal(original.MicroMissiles299Resources!.Missile.Address, task.ReadDword(0x34)));
                        ColdNext("missile-initialized", current);
                        microMissilesMissileRestored = true;
                    }
                    if (!microMissilesImpactRestored && after.Pools[2].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMicroMissiles299Profile.ImpactCallback) && after.Pools[3].Slots.Any(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMicroMissiles299Profile.ParticleCallback))
                    {
                        Assert.All(after.Pools[2].Slots.Where(task => (task.Flags & 1) != 0), task => Assert.Contains(task.ReadDword(0x30), new uint[] { original.MicroMissiles299Resources!.Impact.Address, original.MicroMissiles299Resources.WideImpact.Address }));
                        ColdNext("impact-and-particles", current);
                        microMissilesImpactRestored = true;
                    }
                }
                if (effectId == 43)
                {
                    Assert.Equal(originalMeltingChannels, JsonSerializer.Serialize(current.NativeEffectMemory!.RequireMeltingBubble43().Channels));
                    int observed = AssertMeltingAnchors(prior, current);
                    meltingAnchorChecks += observed;
                    if (observed > 0 && !meltingResourcesRestored)
                    {
                        NativeBattleCuragaExecutableState directoryState = current.NativeEffectMemory.MeltingBubble43Executable!;
                        Assert.Equal(1u, directoryState.InitializationFlag);
                        Assert.Equal(0x15C6080u, directoryState.ResourceAddress);
                        Assert.Equal(260, directoryState.Data!.Value.Length);
                        Assert.Equal(original.MeltingBubble43Resources!.Executable.Data.ToArray(), directoryState.Data.Value.ToArray());
                        Assert.Equal(560, original.MeltingBubble43Resources.ReadResource(0x15C3684).Length);
                        ColdNext("absolute-resources", current);
                        meltingResourcesRestored = true;
                    }
                }
                if (effectId == 56 && !absorbBankRestored && current.NativeEffectMemory!.Absorb56Bank?.Data is { } absorbBank)
                {
                    Assert.Equal(0x4258, absorbBank.Length);
                    Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(absorbBank.AsSpan().Slice(0x4154)));
                    Assert.Equal(0x15A1F10u, BinaryPrimitives.ReadUInt32LittleEndian(absorbBank.AsSpan().Slice(0x4158)));
                    ColdNext("addressed-bank-initialized", current);
                    absorbBankRestored = true;
                }
                if (effectId == 29 && !windBlastBankRestored && current.NativeEffectMemory!.WindBlast29Executable is { InitializationFlag: 1, Data: not null } windBank)
                {
                    Assert.Equal(0x17D27DCu, windBank.ResourceAddress);
                    Assert.Equal(14192, windBank.Data.Value.Length);
                    Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(windBank.Data.Value.AsSpan()));
                    ColdNext("node-bank-initialized", current);
                    windBlastBankRestored = true;
                }
                if (effectId == 74 && after?.ReturnedDescriptor is > 0)
                {
                    NativeBattleDeathState state = current.NativeEffectMemory!.LevelDeath74!;
                    Assert.False(state.PendingColdSources);
                    if (!levelDeathAuraRestored && state.AuraActors is { } aura && BinaryPrimitives.ReadUInt32LittleEndian(aura.AsSpan()) == 1)
                    {
                        ColdNext("aura-bank-initialized", current);
                        levelDeathAuraRestored = true;
                    }
                    if (!levelDeathDarkRestored && state.DarkActors is { } dark && BinaryPrimitives.ReadUInt32LittleEndian(dark.AsSpan()) == 1)
                    {
                        ColdNext("dark-bank-initialized", current);
                        levelDeathDarkRestored = true;
                    }
                }
                if (effectId is 8 or 23 or 47 or 60 or 74 && !textureLoadPendingRestored)
                {
                    NativeBattleAuxiliaryTask? pending = current.NativeGraphics!.Auxiliary.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660 && task.Payload[0] == 2);
                    if (pending is not null)
                    {
                        NativeBattleCureFamilyPool pool = effectId == 74 ? NativeBattleDeathFamilyProfile.ForEffect(74).Pools[0] : effectId == 47 ? NativeBattleCurse47State.Pools[0] : effectId == 8 ? NativeBattleDoom8State.Pools[0] : effectId == 23 ? NativeBattlePsychoBlast23Profile.Pools[0] : NativeBattleEffect60Profile.Pools[0];
                        uint completion = BinaryPrimitives.ReadUInt32LittleEndian(pending.Payload.AsSpan().Slice(8));
                        int rootIndex = checked((int)((completion - pool.Bank - 0x62) / pool.Stride));
                        Assert.InRange(rootIndex, 0, pool.Capacity - 1);
                        Assert.Equal(checked(pool.Bank + (uint)rootIndex * pool.Stride + 0x62u), completion);
                        NativeBattleModulePoolTask root = after!.Pools[0].Slots[rootIndex];
                        Assert.Equal(effectId == 74 ? NativeBattleDeathFamilyProfile.ForEffect(74).Callback(0x8B6A40) : effectId == 47 ? NativeBattleCurse47Controller.RootCallback : effectId == 8 ? NativeBattleDoom8Controller.RootCallback : effectId == 23 ? NativeBattlePsychoBlast23Profile.RootCallback : NativeBattleEffect60Profile.RootCallback, root.Callback);
                        Assert.Equal(1, root.Flags & 1);
                        Assert.Equal((byte)8, root.ReadByte(0x29));
                        Assert.Equal((byte)0, root.ReadByte(0x62));
                        ColdNext("texture-load-pending", current);
                        BattleSnapshot restoredNext = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "texture-load-pending-next.battle.json"));
                        Assert.Equal((byte)255, restoredNext.NativeGraphics!.OriginalEffect!.Pools[0].Slots[rootIndex].ReadByte(0x62));
                        textureLoadPendingRestored = true;
                    }
                }
                if (effectId == 60 && after?.ReturnedDescriptor is > 0)
                {
                    string footprint = TargetFootprint(current.NativeGraphics!.Models[contactActor]);
                    string position = TargetPositionFootprint(current.NativeGraphics.Models[contactActor]);
                    if (!targetDisplaced && position != originalTargetPosition)
                    {
                        Assert.NotEqual(source.NativeGraphics.Models[contactActor].Transform!.Position28, current.NativeGraphics.Models[contactActor].Transform!.Position28);
                        ColdNext("target-displaced", current);
                        targetDisplaced = true;
                    }
                    if (targetDisplaced && !targetPositionRestored && position == originalTargetPosition)
                    {
                        Assert.Contains(after.Pools[4].Slots, task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEffect60Profile.ActPrimaryCallback && task.ReadByte(0x29) == 2);
                        ColdNext("target-position-restored", current);
                        targetPositionRestoreCallback = callbacks;
                        targetPositionRestored = true;
                    }
                    bool restoredRotation = before is { Pools.Length: > 4 } && before.Pools[4].Slots.Select((task, index) => (task, index)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleEffect60Profile.ActPrimaryCallback && item.task.ReadByte(0x29) == 2 && after.Pools[4].Slots[item.index].ReadByte(0x29) == 3);
                    if (!targetRestored && restoredRotation)
                    {
                        Assert.True(targetPositionRestored);
                        Assert.Equal(originalTargetFootprint, footprint);
                        Assert.True(callbacks > targetPositionRestoreCallback!.Value);
                        ColdNext("target-restored", current);
                        targetRotationRestoreCallback = callbacks;
                        targetRestored = true;
                    }
                    if (!secondaryBankRestored && current.NativeEffectMemory!.Effect60SecondaryExecutable is { InitializationFlag: 1, Data: not null })
                    {
                        Assert.Equal(1u, current.NativeEffectMemory.Effect60PrimaryExecutable!.InitializationFlag);
                        Assert.Equal(0x1797CC0u, current.NativeEffectMemory.Effect60PrimaryExecutable.ResourceAddress);
                        Assert.Equal(0x179C998u, current.NativeEffectMemory.Effect60SecondaryExecutable.ResourceAddress);
                        Assert.Equal(19672, current.NativeEffectMemory.Effect60PrimaryExecutable.Data!.Value.Length);
                        Assert.Equal(17260, current.NativeEffectMemory.Effect60SecondaryExecutable.Data!.Value.Length);
                        ColdNext("secondary-bank-initialized", current);
                        secondaryBankRestored = true;
                    }
                }
                if (effectId == 20)
                {
                    if (after is { ReturnedDescriptor: > 0, Pools.Length: > 2 })
                    {
                        NativeBattleModulePoolTask? main = after.Pools[2].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEffect20Profile.MainCallback && task.ReadByte(0x29) > 0);
                        if (main is not null)
                        {
                            effect20VibrationHandle = unchecked((short)main.ReadWord(0x74));
                        }
                    }
                    NativeBattleParticipantSnapshot activated = current.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 6).NativeParticipant!;
                    if (!actor6Activated && !activated.Hidden && !activated.Suspended && !activated.Untargetable)
                    {
                        string actor = current.NativeGraphics!.ActorIds[6]!;
                        Assert.Single(current.Events, item => item.Kind == "Activated" && item.ActorId == actor);
                        actor6Activated = true;
                        ColdNext("actor6-activated", current);
                    }
                }
                if (CrossedContactGate(prior, current, effectId))
                {
                    contacts++;
                    ColdNext(effectId == 56 ? "primary-contact" : "contact-" + contacts, current);
                }
                if (!initialized && after?.ReturnedDescriptor is > 0)
                {
                    Assert.Equal(effectId, Director(current).Effect);
                    Assert.InRange(Director(current).SourceArenaOffset, 0, NativeBattleEffectArena.Length - 2 * Director(current).DrawStep);
                    if (effectId == 260)
                    {
                        Assert.NotNull(current.NativeCamera!.CurrentPose);
                        Assert.NotNull(current.NativeCamera.CapturedPose);
                        Assert.NotNull(current.NativeCamera.FocalLength);
                    }
                    else if (effectId != 183)
                    {
                        Assert.Equal(JsonSerializer.Serialize(original.Effect79Resources?.Camera ?? original.Effect83Resources?.Camera ?? original.Effect289Resources?.Camera ?? original.Effect315Resources?.Camera ?? original.Effect283Resources?.Camera ?? original.Effect303Resources?.Camera ?? original.Effect298Resources?.Camera ?? original.MegaSpark279Resources?.Camera ?? original.BoomerangSword304Resources?.Camera ?? original.SnipeLaser302Resources?.Camera ?? original.EnemyMicroMissiles284Resources?.Camera ?? original.MiniPulseCannon286Resources?.Camera ?? original.EvilEye282Resources?.Camera ?? original.Shotgun281Resources?.Camera ?? original.Gravija214Resources?.Camera ?? original.Junk44Resources?.Camera ?? original.Stare45Resources?.Camera ?? original.Sigh46Resources?.Camera ?? original.MagmaBreath48Resources?.Camera ?? original.Resonance49Resources?.Camera ?? original.BladeShot12Resources?.Camera ?? original.Acid170Resources?.Camera ?? original.Morph172Resources?.Camera ?? original.PoisonGas171Resources?.Camera ?? original.Additional181Resources?.Camera ?? original.ChainGun190Resources?.Camera ?? original.Additional182Resources?.Camera((contact.Primary.HitFlags3 & 4) != 0) ?? original.Blaster186Resources?.Camera ?? original.CannonBlow151Resources?.Camera ?? original.GatlingGun150Resources?.Camera ?? original.Breath129Resources?.Camera ?? original.BadBreath131Resources?.Camera ?? original.Sonic139Resources?.Camera ?? original.ReflectBeam135Resources?.Camera ?? original.Breath157Resources?.Camera ?? original.WildCannonBlow156Resources?.Camera ?? original.SandStorm155Resources?.Camera ?? original.GastricJuice128Resources?.Camera ?? original.DisolvingAcid132Resources?.Camera ?? original.OilShot136Resources?.Camera ?? original.OilBlast137Resources?.Camera ?? original.Saliva138Resources?.Camera ?? original.MeltEye158Resources?.Camera ?? original.EnergyBomber217Resources?.Camera ?? original.Effect101Resources?.Camera ?? original.BeamLaser134Resources?.Camera ?? original.Hypnotize133Resources?.Camera ?? original.NeedlesFamilyResources?.Camera ?? original.LaserEye71Resources?.Camera ?? original.EerieSoundWave130Resources?.Camera ?? original.PetrifyStare127Resources?.Camera ?? original.RayBomb307Resources?.Camera ?? original.UltraWaves184Resources?.Camera ?? original.ElectricDischarge126Resources?.Camera ?? original.Electrocute275Resources?.Camera ?? original.Acid317Resources?.Camera ?? original.GatlingGun305Resources?.Camera ?? original.Degenerator306Resources?.Camera ?? original.BadBreath300Resources?.Camera ?? original.MicroMissiles299Resources?.Camera ?? original.WhiteWind297Resources?.Camera ?? original.DeathResources?.Animation ?? original.Curse47Resources?.Animation ?? original.Doom8Resources?.Camera ?? original.Absorb56Resources?.Animation ?? original.Devour62Resources?.Animation ?? original.MeltingBubble43Resources?.Animation ?? original.WindBlast29Resources?.Animation ?? original.Effect60Resources?.Animation ?? original.PsychoBlast23Resources?.Animation ?? original.Effect20Resources?.Animation ?? original.Raldo19Resources?.Animation ?? original.DrinkMagic37Resources?.Animation ?? original.MightyGuardResources?.Animation ?? original.ActorDirectorResources!.Animation), JsonSerializer.Serialize(current.NativeEffectMemory!.Textures!.Animation!.Source));
                    }
                    if (effectId == 126)
                    {
                        Assert.Equal(new NativeBattleSharedEffectRandomState(0), current.NativeEffectMemory!.SharedEffectRandom);
                        for (int actor = 0; actor < 3; actor++)
                        {
                            Assert.Equal(source.NativeGraphics!.Models[actor].Color40, current.NativeEffectMemory.ElectricDischarge126!.ReadDword(NativeBattleElectricDischarge126Profile.SavedColorsGlobal + (uint)actor * 4));
                        }
                    }
                    initialized = true;
                    ColdNext("initialized", current);
                }
                for (int slot = 0; slot < current.NativeGraphics!.Effects.Slots.Length; slot++)
                {
                    NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[slot];
                    if ((text.Flags0 & 1) == 0 || text.Kind != NativeBattleEffectKind.DamageText5069B0 || text.ModelIndex != contactActor || (prior.NativeGraphics!.Effects.Slots[slot].Flags0 & 1) != 0)
                    {
                        continue;
                    }
                    Assert.NotNull(expectedText);
                    Assert.Equal(expectedText.Glyphs.ToArray(), text.Glyphs.ToArray());
                    Assert.Equal(expectedText.Color, text.Color);
                    Assert.Equal(expectedText.HalfWidth, text.HalfWidth);
                    contactGlyphs++;
                }
                if (request.ControlledDrainContact)
                {
                    Assert.Equal(logicalHp, current.Combatants.Single(actor => actor.Id == target).CurrentHp);
                    Assert.Equal(logicalCasterHp, current.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == caster).CurrentHp);
                    for (int slot = 0; slot < current.NativeGraphics!.Effects.Slots.Length; slot++)
                    {
                        NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[slot];
                        if ((text.Flags0 & 1) == 0 || text.Kind != NativeBattleEffectKind.DamageText5069B0 || text.ModelIndex != caster || (prior.NativeGraphics!.Effects.Slots[slot].Flags0 & 1) != 0)
                        {
                            continue;
                        }
                        Assert.NotNull(expectedSecondaryText);
                        Assert.Equal(expectedSecondaryText.Glyphs.ToArray(), text.Glyphs.ToArray());
                        Assert.Equal(expectedSecondaryText.Color, text.Color);
                        Assert.Equal(expectedSecondaryText.HalfWidth, text.HalfWidth);
                        secondaryContactGlyphs++;
                        ColdNext("secondary-contact", current);
                    }
                }
                if ((visibleChangedRgb == 0 || effectId is 79 or 83 or 289 or 315 or 303 or 298 or 279 or 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 101 or 135 or 131 or 128 or 155 or 156 or 157 or 12 or 49 or 170 or 172 or 171 or 181 or 182 or 183 or 190 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 260) && after?.ReturnedDescriptor is > 0 && Director(current).Effect == effectId)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = ActorDirectorPacketReferences(prior, current).Union(acid170BodyPackets);
                    if (!owned.IsEmpty)
                    {
                        NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        if (effectId == 170 && !acid170BodyPackets.IsEmpty)
                        {
                            NativeBattlePrimitiveFrame withoutBodyFrame = frame with { LastCommands = frame.LastCommands.Where(command => !acid170BodyPackets.Contains(command.Command.Packet)).ToImmutableArray() };
                            BattleSnapshot withoutBody = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = withoutBodyFrame } };
                            byte[] withoutBodyPixels = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(withoutBody), 320, 224).ToArray();
                            acid170BodyChangedRgb = Math.Max(acid170BodyChangedRgb, CountRgbChanges(pixels, withoutBodyPixels));
                        }

                        NativeBattlePrimitiveFrame withoutFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() };
                        BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = withoutFrame } };
                        byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                        int changed = CountRgbChanges(pixels, plain);
                        if (changed > visibleChangedRgb)
                        {
                            visibleChangedRgb = changed;
                            peakSnapshot = current;
                            DirectedNodePacketState packetState = Director(current);
                            peakWithout = without;
                            peakPixels = pixels;
                            peakPlain = plain;
                            peakOwnership = effectId is 79 or 83 or 289 or 315 or 283 or 303 or 298 or 19 or 184 or 45
                                ? JsonSerializer.Serialize(new { Effect = effectId, current.Frame, NativeCallback = current.NativeGraphics.Callback, Ownership = effectId is 79 or 83 or 289 or 315 or 283 or 303 or 298 ? "Actual FX cursor span plus COMMON polygons before the fully matched first ordinary model allocation; ordinary party shadow/body retained" : effectId == 45 ? "Actual COMMON effect prefix before complete first original model allocation, including paired E1 mode packets" : effectId == 184 ? "Fixed native PRIVATE packets, warp quads and capture plus matched COMMON prefix before ordinary actor submission" : "Matched common packets before the complete first actor allocation", Packets = owned.OrderBy(packet => packet.Bank).ThenBy(packet => packet.Buffer).ThenBy(packet => packet.ByteOffset).ToArray() })
                                : JsonSerializer.Serialize(new { Effect = effectId, current.Frame, NativeCallback = current.NativeGraphics.Callback, PrivateBank = packetState.PacketBank, PrivateStart = packetState.SecondaryDrawCursor!.Value - packetState.DrawStep, PrivateEnd = packetState.PrimaryDrawCursor!.Value, Packets = owned.OrderBy(packet => packet.Bank).ThenBy(packet => packet.Buffer).ThenBy(packet => packet.ByteOffset).ToArray() });
                            if (effectId is not (79 or 83 or 289 or 315 or 303 or 298 or 279 or 304 or 302 or 284 or 286 or 282 or 281 or 214 or 44 or 45 or 46 or 48 or 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 101 or 135 or 131 or 128 or 155 or 156 or 157 or 12 or 49 or 170 or 172 or 171 or 181 or 182 or 183 or 190 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 260))
                            {
                                File.WriteAllText(Path.Combine(output!, "visible-packet-ownership.json"), peakOwnership);
                                NativeBattleRestorationPresentationTests.Publish(output!, "visible", current, pixels);
                                NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", without, plain);
                            }
                            if (!liveRestored)
                            {
                                ColdNext("live", current);
                                liveRestored = true;
                            }
                        }
                    }
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                if (initialized && Released(current) && (effectId != 20 || actor6Activated && Effect20ActivationCompleted(current)) && (effectId != 303 || effect303.ActivationCompleted(current)) && (effectId != 315 || effect315.ActivationCompleted(current)))
                {
                    break;
                }
            }
            boundary = "acceptance";
            BattleSnapshot completed = battle.Capture();
            Assert.True(initialized, "The original actor-director module never initialized its selected controller.");
            Assert.True(liveRestored, "No significant original effect packet contribution reached cold continuation.");
            Assert.True(visibleChangedRgb > 0);
            Assert.Equal(effectId is 283 or 303 or 298 or 101 or 172 ? 0 : 1, contacts);
            Assert.Equal(effectId is 283 or 303 or 298 or 101 or 172 || expectedText is null ? 0 : 1, contactGlyphs);
            if (request.ControlledDrainContact)
            {
                Assert.True(absorbBankRestored, "Absorb never materialized its actual retained absolute bank.");
                Assert.Equal(expectedSecondaryText is null ? 0 : 1, secondaryContactGlyphs);
            }
            if (effectId != 183)
            {
                Assert.True(cameraChanged, "The original actor-director camera control never changed its retained camera.");
            }
            if (effectId == 183)
            {
                Assert.Equal(3, effect183Boundaries.Count);
                Assert.Contains("before-old30-first-contact", effect183Boundaries);
                Assert.Equal(new[] { 0, 1 }, effect183PacketBanks.Order().ToArray());
                Assert.False(stageChanged);
                Assert.NotNull(completed.NativeEffectMemory!.Effect183!.RootBankStorage);
                Assert.NotNull(completed.NativeEffectMemory.Effect183.ChildBankStorage);
            }
            if (effectId is not (289 or 315 or 303 or 298 or 279 or 304 or 302 or 284 or 286 or 281 or 214 or 44 or 19 or 23 or 29 or 37 or 43 or 128 or 155 or 156 or 157 or 12 or 49 or 170 or 172 or 181 or 183 or 190 or 150 or 151 or 139 or 131 or 132 or 137 or 138 or 158 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 317 or 184))
            {
                Assert.True(stageChanged, "The original actor-director stage fade never changed the retained four-group color/depth owner.");
            }
            if (effectId is 289 or 315 or 303 or 298 or 279 or 304 or 302 or 284 or 286 or 281 or 214 or 44 or 19 or 23 or 29 or 43 or 128 or 155 or 156 or 157 or 12 or 49 or 170 or 172 or 181 or 183 or 190 or 150 or 151 or 139 or 131 or 132 or 137 or 138 or 158 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 317 or 184)
            {
                Assert.False(stageChanged);
            }
            Assert.Equal(1, completions);
            if (effectId == 79)
            {
                effect79.Accept(completed, output!);
            }
            if (effectId == 83)
            {
                effect83.Accept(completed, output!);
            }
            if (effectId == 289)
            {
                effect289.Accept(completed, output!);
            }
            if (effectId == 315)
            {
                effect315.Accept(completed, output!);
            }
            if (effectId == 283)
            {
                effect283.Accept(completed, output!);
            }
            if (effectId == 303)
            {
                effect303.Accept(completed, output!);
                NativeBattleRestorationPresentationTests.Publish(output!, "activated-original-pair", completed, null);
            }
            if (effectId == 298)
            {
                Assert.True(effect298OffsetsVerified);
                Assert.True(effect298ChunksSeen);
                Assert.True(effect298SmokeSeen);
                Assert.True(effect298CountersChanged);
                Assert.True(effect298IoQueued);
                Assert.True(effect298VibrationHandle is > 0);
                Assert.False(completed.NativeVibrationProcess!.IsPlaying(effect298VibrationHandle!.Value));
                Assert.Equal(new[] { 0, 1 }, effect298Banks.Order().ToArray());
                Assert.Equal(new[] { NativeBattleEffect298Profile.ProgrammeAAddress, NativeBattleEffect298Profile.ProgrammeCAddress, NativeBattleEffect298Profile.ProgrammeDAddress }, effect298Programmes.Order().ToArray());
                Assert.DoesNotContain(NativeBattleEffect298Profile.ProgrammeBAddress, effect298Programmes);
                Assert.Contains("ordinary-COMMON-before-complete-first-model", effect298Domains);
                Assert.Contains("effect-FX", effect298Domains);
                foreach (uint callback in new uint[] { 0x670A20, 0x670C60, 0x672290, 0x671AB0, 0x671F20 })
                {
                    Assert.Contains(callback, effect298Callbacks);
                }
                foreach (string marker in new[] { "effect298-root-117", "effect298-root-118", "effect298-root-119", "effect298-file360-queued", "effect298-mutable-script-counters", "effect298-io-complete-child-banks-retained" })
                {
                    Assert.Contains(marker, restoredBoundaries);
                }
                Assert.Equal(logicalHp, completed.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                Assert.Equal(contact.Encode(), completed.NativeActionPackets!.ReadContact(header.FirstContactIndex!.Value).Encode());
                File.WriteAllText(Path.Combine(output!, "effect298-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = effect298Callbacks.Order(), Banks = effect298Banks.Order(), Programmes = effect298Programmes.Order(), Markers = effect298Markers.Order(), Domains = effect298Domains.Order(), VibrationHandle = effect298VibrationHandle, Counters = completed.NativeEffectMemory!.Effect298!.ScriptCounterWrites, Observations = effect298Observations, CompletionScope = "Root stops child scans at old117, queues actual file360 at old118 and waits at119 for IO; retained child banks are not artificially drained. No controller or original sequence contact." }));
            }
            if (effectId == 279)
            {
                Assert.True(megaSpark279RowsSeen);
                Assert.True(megaSpark279AttachmentsSeen);
                Assert.True(megaSpark279ProgrammesSeen);
                Assert.True(megaSpark279SpritesSeen);
                Assert.Equal(Enumerable.Range(1, 36).ToArray(), megaSpark279AFrames.Order().ToArray());
                Assert.Equal(Enumerable.Range(1, 16).ToArray(), megaSpark279BFrames.Order().ToArray());
                Assert.Equal(new[] { 0, 1 }, megaSpark279Banks.Order().ToArray());
                Assert.Contains("effect", megaSpark279PacketDomains);
                Assert.Contains("ordinary-COMMON", megaSpark279PacketDomains);
                foreach (uint callback in new uint[] { 0x67F090, 0x67F340, 0x67FC30, 0x680520, 0x680170, 0x6800F0 })
                {
                    Assert.Contains(callback, megaSpark279Callbacks);
                }
                foreach (string marker in new[] { "mega-spark-main-1", "mega-spark-main-16", "mega-spark-main-25", "mega-spark-main-31", "mega-spark-main-58", "mega-spark-main-59", "mega-spark-main-60", "mega-spark-main-84", "mega-spark-main-attachments-retired-other-banks-retained" })
                {
                    Assert.Contains(marker, restoredBoundaries);
                }
                Assert.NotNull(completed.NativeEffectMemory!.MegaSpark279!.RootBankStorage);
                File.WriteAllText(Path.Combine(output!, "mega-spark279-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = megaSpark279Callbacks.Order(), Banks = megaSpark279Banks.Order(), PacketDomains = megaSpark279PacketDomains.Order(), ProgrammeAFrames = megaSpark279AFrames.Order(), ProgrammeBFrames = megaSpark279BFrames.Order(), Rows = megaSpark279RowsSeen, Attachments = megaSpark279AttachmentsSeen, Programmes = megaSpark279ProgrammesSeen, Sprites = megaSpark279SpritesSeen }));
            }
            if (effectId == 304)
            {
                Assert.True(boomerang304ProgrammeSeen);
                Assert.True(boomerang304TrailSeen);
                Assert.True(boomerang304ParticlesSeen);
                Assert.True(boomerang304MorphSeen);
                Assert.Equal(new[] { 0, 1 }, boomerang304Banks.Order().ToArray());
                foreach (uint callback in new uint[] { 0x669840, 0x669AF0, 0x66A330, 0x66A3F0, 0x66A730 })
                {
                    Assert.Contains(callback, boomerang304Callbacks);
                }
                Assert.NotNull(completed.NativeEffectMemory!.BoomerangSword304!.RootBankStorage);
                File.WriteAllText(Path.Combine(output!, "boomerang304-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = boomerang304Callbacks.Order().ToArray(), Banks = boomerang304Banks.Order().ToArray(), Markers = boomerang304Markers.Order().ToArray(), Programme = boomerang304ProgrammeSeen, Trail = boomerang304TrailSeen, Particles = boomerang304ParticlesSeen, Morph = boomerang304MorphSeen }));
            }
            if (effectId == 302)
            {
                Assert.True(snipe302ClonesSeen);
                Assert.Equal(2, snipe302InitializedA.Count);
                Assert.True(snipe302ParticlesSeen);
                Assert.True(snipe302MorphSeen);
                Assert.True(snipe302BeamWindowSeen);
                Assert.Equal(new[] { 0, 1 }, snipe302Banks.Order().ToArray());
                Assert.Equal(new[] { NativeBattleSnipeLaser302Profile.ProgrammeCAddress, NativeBattleSnipeLaser302Profile.ProgrammeBAddress, NativeBattleSnipeLaser302Profile.ProgrammeDAddress }, snipe302Programmes.Order().ToArray());
                Assert.DoesNotContain(NativeBattleSnipeLaser302Profile.ProgrammeAAddress, snipe302Programmes);
                foreach (uint callback in new uint[] { 0x66CB00, 0x66CD40, 0x66D670, 0x66D6D0 })
                {
                    Assert.Contains(callback, snipe302Callbacks);
                }
                Assert.Contains("clone-0-43", snipe302Markers);
                Assert.Contains("clone-0-44", snipe302Markers);
                Assert.NotNull(completed.NativeEffectMemory!.SnipeLaser302!.RootBankStorage);
                File.WriteAllText(Path.Combine(output!, "snipe302-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = snipe302Callbacks.Order().ToArray(), Banks = snipe302Banks.Order().ToArray(), AdvancedProgrammes = snipe302Programmes.Order().ToArray(), InitializedOnlyA = snipe302InitializedA, Markers = snipe302Markers.Order().ToArray(), Clones = snipe302ClonesSeen, Particles = snipe302ParticlesSeen, GroundSprites = snipe302SpritesSeen, Morph = snipe302MorphSeen, BeamWindow = snipe302BeamWindowSeen }));
            }
            if (effectId == 284)
            {
                Assert.True(enemyMicro284SixBorn);
                Assert.True(enemyMicro284TrailsSeen);
                Assert.True(enemyMicro284ParticlesSeen);
                Assert.True(enemyMicro284ReactionSeen);
                Assert.True(enemyMicro284MorphSeen);
                Assert.Equal(new[] { 0, 1 }, enemyMicro284Banks.Order().ToArray());
                Assert.Equal(new ushort[] { 0, 1 }, enemyMicro284Variants.Order().ToArray());
                Assert.Equal(new[] { NativeBattleEnemyMicroMissiles284Profile.MissileAddress, NativeBattleEnemyMicroMissiles284Profile.ImpactAddress, NativeBattleEnemyMicroMissiles284Profile.WideImpactAddress }, enemyMicro284Programmes.Order().ToArray());
                foreach (uint callback in new uint[] { 0x67B850, 0x67BBB0, 0x67C240, 0x67C300 })
                {
                    Assert.Contains(callback, enemyMicro284Callbacks);
                }
                Assert.NotNull(completed.NativeEffectMemory!.EnemyMicroMissiles284!.RootBankStorage);
                File.WriteAllText(Path.Combine(output!, "enemy-micro284-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = enemyMicro284Callbacks.Order().ToArray(), Banks = enemyMicro284Banks.Order().ToArray(), Programmes = enemyMicro284Programmes.Order().ToArray(), Markers = enemyMicro284Markers.Order().ToArray(), SixBorn = enemyMicro284SixBorn, Trails = enemyMicro284TrailsSeen, Particles = enemyMicro284ParticlesSeen, ReactionFour = enemyMicro284ReactionSeen, Morph = enemyMicro284MorphSeen }));
            }
            if (effectId == 286)
            {
                Assert.True(miniPulse286SpriteSeen);
                Assert.True(miniPulse286MorphSeen);
                Assert.True(miniPulse286WindowSeen);
                Assert.Equal(new[] { 0, 1 }, miniPulse286Banks.Order().ToArray());
                Assert.Equal(new[] { NativeBattleMiniPulseCannon286Profile.ProgrammeCAddress, NativeBattleMiniPulseCannon286Profile.ProgrammeDAddress, NativeBattleMiniPulseCannon286Profile.ProgrammeAAddress, NativeBattleMiniPulseCannon286Profile.ProgrammeBAddress }, miniPulse286Programmes.Order().ToArray());
                foreach (uint callback in new uint[] { 0x679EC0, 0x67A070, 0x67A7C0 })
                {
                    Assert.Contains(callback, miniPulse286Callbacks);
                }
                Assert.NotNull(completed.NativeEffectMemory!.MiniPulseCannon286!.RootBankStorage);
                File.WriteAllText(Path.Combine(output!, "mini-pulse286-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = miniPulse286Callbacks.Order().ToArray(), Banks = miniPulse286Banks.Order().ToArray(), Programmes = miniPulse286Programmes.Order().ToArray(), Markers = miniPulse286Markers.Order().ToArray(), Sprite = miniPulse286SpriteSeen, Morph = miniPulse286MorphSeen, Window = miniPulse286WindowSeen }));
            }
            if (effectId == 282)
            {
                Assert.True(evilEye282SpriteSeen);
                Assert.True(evilEye282MorphSeen);
                Assert.True(evilEye282FlashSeen);
                Assert.Equal(new[] { 0, 1 }, evilEye282Banks.Order().ToArray());
                Assert.Equal(new[] { NativeBattleEvilEye282Profile.ProgrammeBAddress, NativeBattleEvilEye282Profile.ProgrammeAAddress }, evilEye282Programmes.Order().ToArray());
                foreach (uint callback in new uint[] { 0x67D4C0, 0x67D680, 0x67DB20, 0x67DC60 })
                {
                    Assert.Contains(callback, evilEye282Callbacks);
                }
                Assert.NotNull(completed.NativeEffectMemory!.EvilEye282!.RootBankStorage);
                File.WriteAllText(Path.Combine(output!, "evil-eye282-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = evilEye282Callbacks.Order().ToArray(), Banks = evilEye282Banks.Order().ToArray(), Programmes = evilEye282Programmes.Order().ToArray(), Markers = evilEye282Markers.Order().ToArray(), ParticlePhaseW = evilEye282SpriteSeen, Morph = evilEye282MorphSeen, Flash = evilEye282FlashSeen }));
            }
            if (effectId == 281)
            {
                Assert.True(shotgun281MuzzleSeen);
                Assert.True(shotgun281MorphSeen);
                Assert.True(shotgun281RecipientSeen);
                Assert.True(shotgun281SzSeen);
                Assert.Contains(shotgun281Headers, headerBits => (headerBits & 0x200) != 0);
                Assert.Contains(shotgun281Headers, headerBits => (headerBits & 0x8000) != 0);
                Assert.Contains(shotgun281Headers, headerBits => (headerBits & 0x8200) == 0);
                Assert.True(shotgun281FlashSeen);
                Assert.Equal(new[] { 0, 1 }, shotgun281Banks.Order().ToArray());
                Assert.Equal(new[] { NativeBattleShotgun281Profile.ProgrammeBAddress, NativeBattleShotgun281Profile.ProgrammeAAddress }, shotgun281Programmes.Order().ToArray());
                Assert.Equal(new[] { NativeBattleShotgun281Profile.SpriteBAddress, NativeBattleShotgun281Profile.SpriteAAddress }, shotgun281Sprites.Order().ToArray());
                foreach (uint callback in new uint[] { 0x67DD90, 0x67DF80, 0x67E5F0, 0x67E6B0 })
                {
                    Assert.Contains(callback, shotgun281Callbacks);
                }
                foreach (string marker in new[] { "shotgun-current-composed-caster-bone23", "shotgun-current-recipient-eligible-bone", "shotgun-current-morph-scratch", "shotgun-current-polygon-SZ", "shotgun-root-main-particles-completion" })
                {
                    Assert.Contains(marker, restoredBoundaries);
                }
                Assert.DoesNotContain(completed.NativeGraphics!.ModuleEffects.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300);
                File.WriteAllText(Path.Combine(output!, "shotgun281-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = shotgun281Callbacks.Order().ToArray(), Banks = shotgun281Banks.Order().ToArray(), Programmes = shotgun281Programmes.Order().ToArray(), Sprites = shotgun281Sprites.Order().ToArray(), Ratios = shotgun281Ratios.Order().ToArray(), Headers = shotgun281Headers.Order().ToArray(), Observations = shotgun281Observations }));
            }
            if (effectId == 214)
            {
                Assert.True(gravija214EntryCopied);
                Assert.True(gravija214Preprocessed);
                Assert.True(gravija214WarpSeen);
                Assert.True(gravija214ProgrammeSzSeen);
                Assert.True(gravija214SpriteSeen);
                Assert.True(gravija214ReloadPending);
                Assert.Equal(new[] { 0, 1 }, gravija214Banks.Order().ToArray());
                foreach (uint callback in new uint[] { 0x69D670, 0x69D870, 0x69DF00 })
                {
                    Assert.Contains(callback, gravija214Callbacks);
                }
                foreach (string marker in new[] { "gravija-original-entry-copy-before-preprocess", "gravija-current-preprocessed-copy", "gravija-actual180-sprite-pool", "gravija-literal-color-and-textured-warp", "gravija-actual7043-programme-SZ", "gravija-main74-COMMON-pending", "gravija-root-retired-on-main-completion" })
                {
                    Assert.Contains(marker, restoredBoundaries);
                }
                File.WriteAllText(Path.Combine(output!, "gravija214-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = gravija214Callbacks.Order().ToArray(), PacketBanks = gravija214Banks.Order().ToArray(), Observations = gravija214Observations }));
            }
            if (effectId == 44)
            {
                Assert.True(junk44IoQueued);
                Assert.True(junk44IoCompleted);
                Assert.True(junk44RestoredPosition);
                Assert.Equal(new[] { 0, 1 }, junk44PacketBanks.Order().ToArray());
                foreach (uint callback in new uint[] { 0x6E7B90, 0x6E7E00, 0x6E82C0, 0x6E84D0, 0x6E8810 })
                {
                    Assert.Contains(callback, junk44Callbacks);
                }
                Assert.Contains(junk44Opcodes, opcode => (opcode & 0xFC) == 0x2C);
                Assert.Contains(junk44Opcodes, opcode => (opcode & 0xFC) == 0x3C);
                Assert.Contains("junk-two-original-files-queued", restoredBoundaries);
                Assert.Contains("junk-root-and-COMMON-banks-detached", restoredBoundaries);
                File.WriteAllText(Path.Combine(output!, "junk44-native-observations.json"), JsonSerializer.Serialize(new { Callbacks = junk44Callbacks.Order().ToArray(), PacketBanks = junk44PacketBanks.Order().ToArray(), Opcodes = junk44Opcodes.Order().ToArray(), junk44IoQueued, junk44IoCompleted, junk44RestoredPosition }));
            }
            if (effectId == 45)
            {
                Assert.Equal(new[] { 21, 22 }, stare45Bones.Order().ToArray());
                Assert.Equal(3, stare45ProgrammesCompleted.Count);
                Assert.NotNull(stare45TargetF1);
                Assert.Equal(new[] { 0, 0xC000 }, stare45TimBanks.Order().ToArray());
                Assert.True(stare45ModePairSeen);
                Assert.True(stare45MorphSeen);
                Assert.Contains(stare45Opcodes, opcode => (opcode & 0xFC) == 0x2C);
                Assert.Contains(stare45Opcodes, opcode => (opcode & 0xFC) == 0x3C);
                Assert.DoesNotContain((byte)0xE2, stare45Opcodes);
                foreach (string protocol in new[] { "0/0/1", "0/1/1", "1/1/1" })
                {
                    Assert.Contains(protocol, stare45Protocol);
                }
                foreach (uint callback in new uint[] { 0x8366E0, 0x836990, 0x836AC0, 0x836D60, 0x839590, 0x839690 })
                {
                    Assert.Contains(callback, stare45Callbacks);
                }
                foreach (string name in new[] { "stare-target-before-old14", "stare-target-before-old15", "stare-target-first-delayed-F1", "stare-actual-COMMON-prefix-and-E1-pairs", "stare-all-six-exclusive-banks-detached" })
                {
                    Assert.Contains(name, stare45Boundaries);
                }
                Assert.DoesNotContain(completed.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
                for (int group = 0; group < 4; group++)
                {
                    Assert.Equal((short)0, completed.NativeStage!.Groups[group].ColorDepth);
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags);
                }
                File.WriteAllText(Path.Combine(output!, "stare45-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = stare45Boundaries.Order().ToArray(), Callbacks = stare45Callbacks.Order().ToArray(), Bones = stare45Bones.Order().ToArray(), ProgrammeTasks = stare45ProgrammesCompleted.Order().ToArray(), TimBanksWithoutPackets = stare45TimBanks.Order().ToArray(), Protocol = stare45Protocol.Order().ToArray(), Opcodes = stare45Opcodes.Order().ToArray(), Observations = stare45Observations }));
            }
            if (effectId == 46)
            {
                Assert.Equal(new[] { 0, 0x6000 }, sigh46PacketBanks.Order().ToArray());
                Assert.Equal(Enumerable.Range(0, 17).ToArray(), sigh46MaterialRows.Order().ToArray());
                Assert.Equal(24, sigh46ParticleSlots.Count);
                Assert.Contains(0, sigh46ParticleFrames);
                Assert.Contains(14, sigh46ParticleFrames);
                Assert.True(sigh46EmittersSeen);
                Assert.True(sigh46GeometrySeen);
                Assert.True(sigh46VramInitialized);
                Assert.True(sigh46VramWrapped);
                Assert.True(sigh46Gt4Seen);
                Assert.True(sigh46CommonPending);
                Assert.True(sigh46CommonCompleted);
                Assert.True(sigh46NodesDrainedBeforeReload);
                foreach (uint callback in new uint[] { 0x831CB0, 0x831EC0, 0x831FE0, 0x832100, 0x8359C0, 0x835E90, 0x836090, 0x836420 })
                {
                    Assert.Contains(callback, sigh46Callbacks);
                }
                foreach (string name in new[] { "sigh-root-phase-5", "sigh-root-phase-6", "sigh-root-phase-8", "sigh-before-target-old40", "sigh-before-target-old41", "sigh-current-GT4-F0-native-SZ", "sigh-all-six-banks-detached-after-COMMON-FF" })
                {
                    Assert.Contains(name, sigh46Boundaries);
                }
                for (int group = 0; group < 4; group++)
                {
                    Assert.Equal((short)0, completed.NativeStage!.Groups[group].ColorDepth);
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags);
                }
                File.WriteAllText(Path.Combine(output!, "sigh46-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = sigh46Boundaries.Order().ToArray(), Callbacks = sigh46Callbacks.Order().ToArray(), PacketBanks = sigh46PacketBanks.Order().ToArray(), MaterialRows = sigh46MaterialRows.Order().ToArray(), ParticleSlots = sigh46ParticleSlots.Order().ToArray(), ParticleFrames = sigh46ParticleFrames.Order().ToArray(), ParticleSprites = sigh46ParticleSprites.Order().ToArray(), Observations = sigh46Observations }));
            }
            if (effectId == 48)
            {
                Assert.Equal(new[] { 0, 0x6000 }, magma48PacketBanks.Order().ToArray());
                Assert.Equal(new uint[] { NativeBattleMagmaBreath48Profile.MeshAAddress, NativeBattleMagmaBreath48Profile.MeshBAddress }, magma48ChangedXYZ.Order().ToArray());
                Assert.Equal(new uint[] { NativeBattleMagmaBreath48Profile.MeshAAddress, NativeBattleMagmaBreath48Profile.MeshBAddress }, magma48ChangedUV.Order().ToArray());
                Assert.True(magma48EmittersSeen);
                Assert.True(magma48GeometrySeen);
                Assert.True(magma48ColorPairsSeen);
                Assert.True(magma48DepthPacketsSeen);
                foreach (uint callback in new uint[] { 0x824E40, 0x825050, 0x825170, 0x825220, 0x8253B0, 0x82A6A0 })
                {
                    Assert.Contains(callback, magma48Callbacks);
                }
                foreach (string magmaBoundary in new[] { "root-phase-5", "root-phase-6", "before-target-old28", "before-target-old55", "current-caster-bone15-twice", "native-GT3-F0-current-SZ", "all-six-banks-and-node-lists-drained" })
                {
                    Assert.Contains(magmaBoundary, magma48Boundaries);
                }
                for (int group = 0; group < 4; group++)
                {
                    Assert.Equal((short)0, completed.NativeStage!.Groups[group].ColorDepth);
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags);
                }
                File.WriteAllText(Path.Combine(output!, "magma-breath48-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = magma48Boundaries.Order().ToArray(), Callbacks = magma48Callbacks.Order().ToArray(), PacketBanks = magma48PacketBanks.Order().ToArray(), CurrentXYZ = magma48ChangedXYZ.Order().ToArray(), CurrentUV = magma48ChangedUV.Order().ToArray(), Opcodes = magma48Opcodes.Order().ToArray(), Observations = magma48Observations }));
            }
            if (effectId == 49)
            {
                Assert.Equal(new[] { 3, 4 }, resonance49ProgrammeActors.Order().ToArray());
                Assert.Equal(new[] { 3, 4 }, resonance49CompletedProgrammes.Order().ToArray());
                Assert.Equal(new uint[] { 0x821690, 0x821840, 0x821970, 0x821C50, 0x824140, 0x824260, 0x824970 }, resonance49Callbacks.Order().ToArray());
                Assert.Equal(new[] { 0, 0xC000 }, resonance49PacketBanks.Order().ToArray());
                Assert.NotEmpty(resonance49ProducerBuffers);
                Assert.Equal(resonance49ProducerEnvironmentParities.Order().ToArray(), resonance49EnvironmentParities.Order().ToArray());
                Assert.Equal(new byte[] { 0x2D, 0x2F }, resonance49ScreenOpcodes.Order().ToArray());
                Assert.True(resonance49ScreenPairsSeen);
                Assert.True(resonance49ProgrammePacketsSeen);
                Assert.True(resonance49CommonPending);
                Assert.True(resonance49CommonCompleted);
                foreach (string resonanceBoundary in new[] { "root-after-native-entry-old0", "before-target-director", "target-and-programmes-owned", "reaction-phase-4", "before-A3-COMMON-request", "A3-COMMON-pending", "both-screen-channels-140-physical-pairs", "all-six-hierarchy-banks-detached" })
                {
                    Assert.Contains(resonanceBoundary, resonance49Boundaries);
                }
                Assert.False(stageChanged);
                File.WriteAllText(Path.Combine(output!, "resonance49-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = resonance49Boundaries.Order().ToArray(), Callbacks = resonance49Callbacks.Order().ToArray(), ProgrammeActors = resonance49ProgrammeActors.Order().ToArray(), ProgrammeCompleted = resonance49CompletedProgrammes.Order().ToArray(), C000Banks = resonance49PacketBanks.Order().ToArray(), EnvironmentParities = resonance49EnvironmentParities.Order().ToArray(), ProducerBuffer80 = resonance49ProducerBuffers.Order().ToArray(), ExpectedProducerParities = resonance49ProducerEnvironmentParities.Order().ToArray(), ScreenPairs = resonance49ScreenPairsSeen, CommonPending = resonance49CommonPending, CommonCompleted = resonance49CommonCompleted, Observations = resonance49Observations }));
            }
            if (effectId == 12)
            {
                foreach (string name in new[] { "outbound-first-current", "before-old32-contact-and-return", "return-impact-and-children", "before-ending-sound", "before-old62-object7-set", "object7-restored-current-mask", "entry-native-bone7-and-F0-F1", "all-three-banks-detached" })
                {
                    Assert.Contains(name, blade12Boundaries);
                }
                Assert.Equal(new uint[] { 0x6E9B50, 0x6E9C10, 0x6E9D60 }, blade12Callbacks.Order().ToArray());
                Assert.Equal(new uint[] { 0x13627E4, 0x136298C, 0x1362AEC }, blade12SpriteSources.Order().ToArray());
                Assert.Equal(new[] { 0, NativeBattleBladeShot12Profile.PacketBankBytes }, blade12PacketBanks.Order().ToArray());
                Assert.Equal(new[] { 32, 41, 51 }, blade12MorphAges.Order().ToArray());
                Assert.Equal(Enumerable.Range(32, 10).ToArray(), blade12RandomAges.Order().ToArray());
                Assert.Equal(136, blade12NativeRandomDraws);
                Assert.True(blade12Object7Hidden);
                Assert.True(blade12Object7Visible);
                Assert.False(stageChanged);
                File.WriteAllText(Path.Combine(output!, "blade-shot12-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = blade12Boundaries.Order().ToArray(), Callbacks = blade12Callbacks.Order().ToArray(), PrivateBanks = blade12PacketBanks.Order().ToArray(), MorphAges = blade12MorphAges.Order().ToArray(), ProducerRandomDraws = blade12NativeRandomDraws, ProducerRandomScope = "isolated current effect invocation from original snapshots, never whole Clock ticks", Object7Hidden = blade12Object7Hidden, Object7Visible = blade12Object7Visible, Observations = blade12Observations, CompletionScope = "actual100/32/52 task banks, outbound/return/current object7 mask, source morph/W, native child phases and first old32 contact, terminal oldage>=65 without increment plus actual A/B drain" }));
            }
            if (effectId == 170)
            {
                foreach (string name in new[] { "before-main-old1", "curve-and-fixed-bones-born", "body-and-selected-mesh-born", "before-main-old46-single-contact", "before-main-old55-retire", "curve28-and-fixed-bone40-38", "all-banks-drained-detached" })
                {
                    Assert.Contains(name, acid170Boundaries);
                }
                foreach (uint callback in new uint[] { 0x5DCAF0, 0x5DCF20, 0x5DD030, 0x5DDE90, 0x5DE0B0, 0x5DE200, 0x5DEA10 })
                {
                    Assert.Contains(callback, acid170Callbacks);
                }
                Assert.Equal(new[] { 0, 16, 33 }, acid170BodyAges.Order().ToArray());
                Assert.Equal(new[] { 0, 8, 16, 24, 31 }, acid170MeshAges.Order().ToArray());
                Assert.Equal(new byte[] { 0x24, 0x2C, 0x3C }, acid170E1Groups.Order().ToArray());
                Assert.Equal(new uint[] { 1, 2 }, acid170ParticleFlags.Order().ToArray());
                Assert.Equal(new[] { 0, NativeBattleAcid170Profile.PacketStep }, acid170PacketBanks.Order().ToArray());
                Assert.True(acid170BodyRestored);
                Assert.True(acid170BodyChangedRgb > 0, "The actual matched recipient body and linked packets contributed no rendered RGB.");
                Assert.False(stageChanged);
                File.WriteAllText(Path.Combine(output!, "acid170-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = acid170Boundaries.Order().ToArray(), BodyAges = acid170BodyAges.Order().ToArray(), MeshAges = acid170MeshAges.Order().ToArray(), Callbacks = acid170Callbacks.Order().ToArray(), E1Groups = acid170E1Groups.Order().ToArray(), ParticleMasks = acid170ParticleFlags.Order().ToArray(), BodyChangedRgb = acid170BodyChangedRgb, PacketBanks = acid170PacketBanks.Order().ToArray(), Observations = acid170Observations, CompletionScope = "Actual recipient party0 body23+linked2 custom plane deformation, curve28, selected73 XYZ6/currentE1 compound phases, both masked particle consumers, single firstcontact old46 and main56/full exclusive drain" }));
            }
            if (effectId == 172)
            {
                foreach (string name in new[] { "after-main-old0-anchor", "radii-and-two-deformed-meshes", "before-main-old8-body", "body-born", "mid-native-no-contact", "before-main-old60-retire", "physical-radii-and-mutable-XYZ6", "physical-particle-record-live", "all-banks-drained-detached" })
                {
                    Assert.Contains(name, morph172Boundaries);
                }
                foreach (uint callback in new uint[] { 0x5D9F20, 0x5DA0F0, 0x5DA2C0, 0x5DA520 })
                {
                    Assert.Contains(callback, morph172Callbacks);
                }
                Assert.Equal(new[] { 0, 24, 48 }, morph172BodyAges.Order().ToArray());
                Assert.Equal(new[] { 0, NativeBattleMorph172Profile.PacketBankBytes }, morph172PacketBanks.Order().ToArray());
                Assert.True(morph172BodyRestored);
                Assert.False(stageChanged);
                File.WriteAllText(Path.Combine(output!, "morph172-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = morph172Boundaries.Order().ToArray(), BodyAges = morph172BodyAges.Order().ToArray(), Callbacks = morph172Callbacks.Order().ToArray(), PrivatePacketBanks = morph172PacketBanks.Order().ToArray(), Observations = morph172Observations, CompletionScope = "Current41-bone three-object body220, optional native G3/G4, radii/mutableXYZ6 with retainedW, original header without contacts or stage production, main61 and exclusive task/particle drain" }));
            }
            if (effectId == 171)
            {
                foreach (string name in new[] { "before-main-old1", "body-and-four-smoke-born", "before-main-old8-mesh", "before-main-old17-stage-hold", "before-main-old27-recipients", "before-main-old30-counted-contact", "before-main-old42-stage-fade", "before-main-old50-retire", "all-banks-drained-detached" })
                {
                    Assert.Contains(name, poison171Boundaries);
                }
                foreach (uint callback in new uint[] { 0x5DB620, 0x5DBED0, 0x5DC270, 0x5DC5B0, 0x5DC700 })
                {
                    Assert.Contains(callback, poison171Callbacks);
                }
                Assert.Equal(new[] { 0, 12, 23 }, poison171BodyAges.Order().ToArray());
                Assert.Equal(new[] { 0, NativeBattlePoisonGas171Profile.PacketStep }, poison171PacketBanks.Order().ToArray());
                Assert.Equal(new[] { NativeBattlePoisonGas171Profile.ParticleABase, NativeBattlePoisonGas171Profile.ParticleBBase }, poison171ParticleBanks.Order().ToArray());
                Assert.True(poison171BodyRestored);
                Assert.True(stageChanged);
                File.WriteAllText(Path.Combine(output!, "poison-gas171-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = poison171Boundaries.Order().ToArray(), BodyAges = poison171BodyAges.Order().ToArray(), Callbacks = poison171Callbacks.Order().ToArray(), PacketBanks = poison171PacketBanks.Order().ToArray(), ParticleBanks = poison171ParticleBanks.Order().ToArray(), Observations = poison171Observations, CompletionScope = "Actual current42-bone original body and E2 overlay, hide-bit restoration, counted old30 contacts, stage envelope, main51 and exclusive root/child/two particle bank drain" }));
            }
            if (effectId == 181)
            {
                foreach (string name in new[] { "after-main-old0-bone-XYZ", "before-main-old4-mouth", "mouth-bursts-completed", "before-main-old14-flash", "after-main-old15-anchor", "rings-and-particles-started", "before-main-old38-single-contact", "before-main-old50-retire", "all-banks-drained-detached" })
                {
                    Assert.Contains(name, additional181Boundaries);
                }
                foreach (uint callback in new uint[] { 0x5CB9B0, 0x5CC000, 0x5CC160, 0x5CC2A0, 0x5CC560, 0x5CC720 })
                {
                    Assert.Contains(callback, additional181Callbacks);
                }
                Assert.Equal(new[] { 0, 7, 14 }, additional181BodyAges.Order().ToArray());
                Assert.Equal(new[] { 0, NativeBattleAdditional181Profile.PacketStep }, additional181PacketBanks.Order().ToArray());
                Assert.Equal(new uint[] { 2, 4 }, additional181ParticleFlags.Order().ToArray());
                Assert.True(additional181FlashSeen);
                Assert.True(additional181FlashCompleted);
                Assert.DoesNotContain(completed.NativeGraphics!.ModuleEffects.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300);
                Assert.False(stageChanged);
                File.WriteAllText(Path.Combine(output!, "additional181-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = additional181Boundaries.Order().ToArray(), BodyAges = additional181BodyAges.Order().ToArray(), Callbacks = additional181Callbacks.Order().ToArray(), EffectArenaBanks = additional181PacketBanks.Order().ToArray(), SharedParticleFlags = additional181ParticleFlags.Order().ToArray(), FlashSeen = additional181FlashSeen, FlashCompleted = additional181FlashCompleted, Observations = additional181Observations, CompletionScope = "Actual caster body+linked paired COMMON packets and current bone restore, separately owned C000 static/sprite ranges, first physical contact old38, native main51 and exclusive task/particle drain" }));
            }
            if (effectId == 182)
            {
                bool set = (contact.Primary.HitFlags3 & 4) != 0;
                foreach (string name in new[] { "queued-before-entry-old0", "before-main-old1", "entry-head-only-120A-100B", "before-native-contact", "current-flags-bit8-OR-contact", "before-director-retire", "director-retired-at-" + (set ? 41 : 66), "all-banks-drained-detached" })
                {
                    Assert.Contains(name, additional182Boundaries);
                }
                foreach (uint callback in new uint[] { 0x5CA490, 0x5CA6B0, 0x5CA810 })
                {
                    Assert.Contains(callback, additional182Callbacks);
                }
                Assert.Equal(new[] { 0, NativeBattleAdditional182Profile.PacketStep }, additional182PacketBanks.Order().ToArray());
                Assert.Contains(NativeBattleAdditional182Profile.ParticleBBase, additional182ParticleBanks);
                Assert.NotNull(additional182OriginalFlags);
                Assert.Equal(additional182OriginalFlags.Value & 8, completed.NativeGraphics!.Models[contactActor].Flags0 & 8);
                if (set)
                {
                    Assert.DoesNotContain(NativeBattleAdditional182Profile.Callback5CB390, additional182Callbacks);
                    Assert.DoesNotContain(NativeBattleAdditional182Profile.ParticleABase, additional182ParticleBanks);
                }
                else
                {
                    foreach (string name in new[] { "before-main-old20", "before-main-old24", "before-main-old26", "history-current-matrix", "history-head-only-clear-with-same-scan-write", "fade-old0", "fade-old5", "fade-restored-current0800-and-set4" })
                    {
                        Assert.Contains(name, additional182Boundaries);
                    }
                    foreach (uint callback in new uint[] { 0x5CAED0, 0x5CB130, 0x5CB390 })
                    {
                        Assert.Contains(callback, additional182Callbacks);
                    }
                    Assert.Contains(NativeBattleAdditional182Profile.ParticleABase, additional182ParticleBanks);
                    Assert.Equal(new byte[] { 0x28, 0x2A }, additional182F4Opcodes.Order().ToArray());
                    Assert.True(additional182DpcsPublished, "The actual last-in-scan fade never published both DPCS results and retained GTE controls.");
                    Assert.Equal(additional182OriginalFlags.Value & 0x800, completed.NativeGraphics!.Models[contactActor].Flags0 & 0x800);
                    Assert.Equal(4, completed.NativeGraphics.Models[contactActor].Flags0 & 4);
                }
                File.WriteAllText(Path.Combine(output!, "additional182-native-observations.json"), JsonSerializer.Serialize(new { Branch = set ? "contact-BYTE3-bit4-set" : "contact-BYTE3-bit4-clear", ContactOldAge = set ? 35 : 64, RetiredDirectorAge = set ? 41 : 66, OriginalFlags = additional182OriginalFlags, FinalFlags = completed.NativeGraphics!.Models[contactActor].Flags0, Boundaries = additional182Boundaries.Order().ToArray(), Callbacks = additional182Callbacks.Order().ToArray(), PacketBanks = additional182PacketBanks.Order().ToArray(), ParticleBanks = additional182ParticleBanks.Order().ToArray(), F4Opcodes = additional182F4Opcodes.Order().ToArray(), DpcsPublished = additional182DpcsPublished, Observations = additional182Observations, CompletionScope = "Original branch camera/sound, declared Amount0 glyph policy, no asynchronous COMMON waiter, exclusive root/child and particle drain, retained history and model fields" }));
            }
            if (effectId == 186)
            {
                foreach (string name in new[] { "sixteen-morph-frames-before-main-old1", "before-main-old7-beam", "before-main-old39-particles", "before-main-old40-pulse", "before-main-old60-contact", "before-main-old65-retire", "current-mutable-mesh-selected", "original-immutable-mesh-selected", "selected-mesh-20F3-cue-live", "mesh-1433-UV-page-live", "mesh-14F3-UV-page-cue-live", "shared-particle-bank-flag1-live", "shared-particle-bank-flag2-live", "ordinary-COMMON-particle-sprites-live", "children-drained-before-COMMON-old1-request", "COMMON-request-pending", "COMMON-FF-completed-all-banks-detached" })
                {
                    Assert.Contains(name, blaster186Boundaries);
                }
                foreach (uint callback in new uint[] { 0x5BFC50, 0x5BFCF0, 0x5BFEC0, 0x5C0040, 0x5C0190, 0x5C0280, 0x5C0410, 0x5C0690, 0x5C0750, 0x5C0900 })
                {
                    Assert.Contains(callback, blaster186Callbacks);
                }
                Assert.Equal(new[] { 0, NativeBattleBlaster186Profile.PacketStep }, blaster186PacketBanks.Order().ToArray());
                Assert.Contains(blaster186VertexSelections, address => NativeBattleBlaster186Profile.SourceAddresses.Contains(address));
                Assert.Contains(blaster186VertexSelections, address => NativeBattleBlaster186Profile.MutableFrameAddresses.Contains(address));
                foreach (byte opcode in new byte[] { 0x26, 0x2E, 0x32, 0x3A, 0x3E })
                {
                    Assert.Contains(opcode, blaster186Opcodes);
                }
                Assert.True(blaster186TargetDisplaced);
                Assert.Equal(originalTargetPosition, TargetPositionFootprint(completed.NativeGraphics!.Models[contactActor]));
                Assert.Equal(blaster186FramesW, Blaster186FrameWordLanes(completed.NativeEffectMemory!.Blaster186!));
                File.WriteAllText(Path.Combine(output!, "blaster186-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = blaster186Boundaries.Order().ToArray(), Callbacks = blaster186Callbacks.Order().ToArray(), EffectArenaPacketBanks = blaster186PacketBanks.Order().ToArray(), VertexSelections = blaster186VertexSelections.Order().ToArray(), Opcodes = blaster186Opcodes.Order().ToArray(), FramesW = blaster186FramesW, RecipientZDisplaced = blaster186TargetDisplaced, WaiterRetainedHighByte = blaster186WaiterHighByte, Observations = blaster186Observations, CompletionScope = "Effect-arena private packets and separately matched ordinary COMMON sprites, sixteen current morph banks with retained W, recipient Z restored, actual asynchronous COMMON feedback and complete exclusive task/particle drain" }));
            }
            if (effectId == 151)
            {
                foreach (string name in new[] { "trajectory32-before-main-old1", "before-main-old14-impact", "before-main-old30-contact", "before-main-old40-retire", "scroll124-first-compound", "before-scroll-old8-cue", "scroll124-cue512", "flags8-native-inset-live", "children-drained-before-COMMON-old1-request", "COMMON-request-pending", "COMMON-FF-completed-all-banks-detached" })
                {
                    Assert.Contains(name, cannon151Boundaries);
                }
                foreach (uint callback in new uint[] { 0x60D260, 0x60D3B0, 0x60D670, 0x60D790, 0x60D850, 0x60DA10, 0x60DC20, 0x60DE40, 0x60E060, 0x60E1B0 })
                {
                    Assert.Contains(callback, cannon151Callbacks);
                }
                Assert.Equal(new[] { 0, NativeBattleCannonBlow151Profile.PacketStep }, cannon151PacketBanks.Order().ToArray());
                Assert.Equal(new[] { NativeBattleCannonBlow151Profile.ParticleABase, NativeBattleCannonBlow151Profile.ParticleBBase }, cannon151ParticleBanks.Order().ToArray());
                foreach (byte opcode in new byte[] { 0x26, 0x2E, 0x3A, 0x36, 0x3E, 0xE2 })
                {
                    Assert.Contains(opcode, cannon151Opcodes);
                }
                Assert.Equal(cannon151SamplesW, Cannon151WordLanes(completed.NativeEffectMemory!.CannonBlow151!));
                File.WriteAllText(Path.Combine(output!, "cannon-blow151-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = cannon151Boundaries.Order().ToArray(), Callbacks = cannon151Callbacks.Order().ToArray(), CommonPacketBanks = cannon151PacketBanks.Order().ToArray(), ParticleBanks = cannon151ParticleBanks.Order().ToArray(), Opcodes = cannon151Opcodes.Order().ToArray(), SampleW = cannon151SamplesW, WaiterRetainedHighByte = cannon151WaiterHighByte, Observations = cannon151Observations, CompletionScope = "Actual old1 COMMON request, pending auxiliary work, BYTE14=FF with BYTE15 retained, all root/child/particle flags drained and exclusive task banks detached" }));
            }
            if (effectId == 190)
            {
                foreach (string name in new[] { "twelve-records-after-native-setup", "before-main-old2-opening-particles", "before-main-old3-first-shot", "first-native-shot-issued", "all-twelve-shots-issued", "before-main-old30-counted-contact", "before-main-old36-retire", "main-drained-children-live", "all-pools-and-particles-drained" })
                {
                    Assert.Contains(name, chain190Boundaries);
                }
                foreach (uint callback in new uint[] { 0x5BB5A0, 0x5BB6F0, 0x5BBBE0 })
                {
                    Assert.Contains(callback, chain190Callbacks);
                }
                if (chain190HasPositionedRecords)
                {
                    foreach (uint callback in new uint[] { 0x5BB9B0, 0x5BBAA0, 0x5BBC70 })
                    {
                        Assert.Contains(callback, chain190Callbacks);
                    }
                }
                if (chain190HasBoneRecords)
                {
                    foreach (uint callback in new uint[] { 0x5BBEC0, 0x5BC080, 0x5BC1F0 })
                    {
                        Assert.Contains(callback, chain190Callbacks);
                    }
                    Assert.Contains(NativeBattleChainGun190Profile.ParticleABase, chain190ParticleBanks);
                }
                Assert.True(chain190HasBoneRecords);
                Assert.Equal(new[] { 0, 1 }, chain190PacketBanks.Order().ToArray());
                Assert.Contains(NativeBattleChainGun190Profile.ParticleBBase, chain190ParticleBanks);
                foreach (byte group in new byte[] { 0x24, 0x2C, 0x3C })
                {
                    Assert.Contains(chain190Opcodes, opcode => (opcode & 0xFC) == group);
                }
                Assert.False(stageChanged);
                File.WriteAllText(Path.Combine(output!, "chain-gun190-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = chain190Boundaries.Order().ToArray(), Callbacks = chain190Callbacks.Order().ToArray(), PrivatePacketBanks = chain190PacketBanks.Order().ToArray(), ParticleBanks = chain190ParticleBanks.Order().ToArray(), Opcodes = chain190Opcodes.Order().ToArray(), PositionedRecords = chain190HasPositionedRecords, CurrentBodyBoneRecords = chain190HasBoneRecords, Observations = chain190Observations, CompletionScope = "Actual count1 record layout and conditional current-body bone producers, fixed private packet owner, counted old30 contacts, main retirement and complete exclusive root/child/particle drain without asynchronous COMMON waiter" }));
            }
            if (effectId == 150)
            {
                foreach (string name in new[] { "before-old1-delayed-five", "before-old11-delayed-seven", "before-old30-COMMON-contact", "main-drained-children-live", "all-pools-and-particles-drained" })
                {
                    Assert.Contains(name, gatling150Boundaries);
                }
                foreach (uint callback in new uint[] { 0x60E6D0, 0x60E820, 0x60EAE0, 0x60EBD0, 0x60ED10, 0x60EDA0, 0x60EFF0, 0x60F1C0, 0x60F330 })
                {
                    Assert.Contains(callback, gatling150Callbacks);
                }
                Assert.Equal(new[] { 0, 1 }, gatling150PacketBanks.Order().ToArray());
                Assert.Equal(new[] { NativeBattleGatlingGun150Profile.ParticleABase, NativeBattleGatlingGun150Profile.ParticleBBase }, gatling150ParticleBanks.Order().ToArray());
                foreach (byte opcode in new byte[] { 0x26, 0x2E, 0x3E, 0xE1 })
                {
                    Assert.Contains(opcode, gatling150Opcodes);
                }
                File.WriteAllText(Path.Combine(output!, "gatling-gun150-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = gatling150Boundaries.Order().ToArray(), Callbacks = gatling150Callbacks.Order().ToArray(), PrivatePacketBanks = gatling150PacketBanks.Order().ToArray(), ParticleBanks = gatling150ParticleBanks.Order().ToArray(), Opcodes = gatling150Opcodes.Order().ToArray(), ParticleRecords = gatling150Observations, CompletionScope = "Main age37 frees before surviving children; root waits for native child and particle drain and transfers exclusive task storage back to retained state" }));
            }
            if (effectId == 129)
            {
                Assert.Contains("both-native-meshes-preprocessed", breath129Boundaries);
                Assert.Contains("first-custom-mesh-deformation", breath129Boundaries);
                Assert.Contains("before-target-old16-contact", breath129Boundaries);
                Assert.Contains("linked-points-live", breath129Boundaries);
                Assert.Contains("before-main-drain", breath129Boundaries);
                Assert.Contains(NativeBattleBreath129Profile.TargetCallback, breath129Callbacks);
                Assert.Contains(NativeBattleBreath129Profile.FibreCallback, breath129Callbacks);
                Assert.Contains(NativeBattleBreath129Profile.CurveCallback, breath129Callbacks);
                Assert.Contains(0x6B6130u, breath129Callbacks);
                Assert.Contains((byte)0x3E, breath129Opcodes);
                Assert.Contains((byte)0xE2, breath129Opcodes);
                Assert.Equal(new[] { 0, 1 }, breath129PacketBanks.Order().ToArray());
                NativeBattleBreath129State state = completed.NativeEffectMemory!.Breath129!;
                Assert.Equal(0, BitConverter.ToUInt16(state.RootBankStorage!.Read(0, 2)) & 1);
                int working = checked((int)(state.ReadDword(NativeBattleBreath129Profile.WorkingGlobal) - 0x20DFAB8u));
                foreach ((int offset, int stride, int count) in new[] { (0, 0x18, 9), (0xD8, 0x1E8, 3), (0x690, 0x80, 16), (0xE90, 0x20, 64), (0x1690, 0x330, 1) })
                {
                    for (int slot = 0; slot < count; slot++)
                    {
                        Assert.Equal(0, BitConverter.ToUInt16(completed.NativeEffectMemory.Arena!.Read(working + offset + stride * slot, 2)) & 1);
                    }
                }
                File.WriteAllText(Path.Combine(output!, "breath129-boundaries.json"), JsonSerializer.Serialize(new { Boundaries = breath129Boundaries.Order().ToArray(), PacketBanks = breath129PacketBanks.Order().ToArray(), Callbacks = breath129Callbacks.Order().ToArray(), Opcodes = breath129Opcodes.Order().ToArray(), Observations = breath129Observations }));
            }
            if (effectId == 139)
            {
                Assert.Contains("preprocessed-first-warp", sonic139Boundaries);
                Assert.Contains("phase30-warp", sonic139Boundaries);
                Assert.Contains("recipient-tint-live", sonic139Boundaries);
                Assert.Contains("before-old70-COMMON", sonic139Boundaries);
                Assert.Contains("COMMON-completion-pending", sonic139Boundaries);
                Assert.Contains((byte)0x32, sonic139Opcodes);
                Assert.Contains((byte)0x26, sonic139Opcodes);
                Assert.Contains((byte)0xE1, sonic139Opcodes);
                Assert.Contains((byte)0xE2, sonic139Opcodes);
                Assert.Equal(new[] { 0, 1 }, sonic139PacketBanks.Order().ToArray());
                Assert.False(stageChanged);
                Assert.Equal(source.NativeGraphics!.Models[contactActor].Color40, completed.NativeGraphics!.Models[contactActor].Color40);
                Assert.Equal(source.NativeGraphics.Models[contactActor].Flags0 & 0x0800, completed.NativeGraphics.Models[contactActor].Flags0 & 0x0800);
                NativeBattleSonic139State finalState = completed.NativeEffectMemory!.Sonic139!;
                uint working = finalState.ReadDword(NativeBattleSonic139Profile.WorkingGlobal);
                int arenaOffset = checked((int)(working - 0x20DFAB8u));
                Assert.Equal((byte)255, completed.NativeEffectMemory.Arena!.Read(arenaOffset + NativeBattleSonic139Profile.MainOffset + 0x13, 1)[0]);
                Assert.Equal(0, BitConverter.ToUInt16(finalState.RootBankStorage!.Read(0, 2)) & 1);
                Assert.Equal(0, BitConverter.ToUInt16(completed.NativeEffectMemory.Arena.Read(arenaOffset + NativeBattleSonic139Profile.MainOffset, 2)) & 1);
                for (int slot = 0; slot < 4; slot++)
                {
                    Assert.Equal(0, BitConverter.ToUInt16(completed.NativeEffectMemory.Arena.Read(arenaOffset + slot * 0x298, 2)) & 1);
                }
                Assert.DoesNotContain(completed.NativeGraphics.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
                File.WriteAllText(Path.Combine(output!, "sonic139-boundaries.json"), JsonSerializer.Serialize(new { Boundaries = sonic139Boundaries.Order().ToArray(), PacketBanks = sonic139PacketBanks.Order().ToArray(), Opcodes = sonic139Opcodes.Order().ToArray(), CompletionByte = 255 }));
            }
            if (effectId == 131)
            {
                Assert.True(breath131MeshesRestored);
                Assert.True(breath131DeformRestored);
                Assert.True(breath131TargetRestored);
                Assert.True(breath131ParticlesRestored);
                Assert.True(breath131ColorRestored);
                Assert.Equal(2, breath131PacketBanks.Count);
                Assert.Equal(0, completed.NativeGraphics!.Models[contactActor].Flags0 & 0x800);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "bad-breath131-native-observations.json"), JsonSerializer.Serialize(new { MeshesPreprocessed = breath131MeshesRestored, StripDeformation = breath131DeformRestored, TargetAct = breath131TargetRestored, Particles = breath131ParticlesRestored, FullColor40RestoredBeforeContact = breath131ColorRestored, PrivateBankStarts = breath131PacketBanks.Order().ToArray() }));
            }
            if (effectId == 157)
            {
                foreach (uint callback in new uint[] { 0x6043A0, 0x6044D0, 0x6049F0, 0x604B30, 0x604C80, 0x604E90, 0x6050A0, 0x6052A0 })
                {
                    Assert.Contains(callback, breath157Callbacks);
                }
                foreach (string name in new[] { "before-old19-emitter-switch", "after-old19-emitter-switch", "before-old65-first-header-contact", "three-travel-points-W-retained", "children-drained-COMMON-waiter", "COMMON-request-pending", "COMMON-completed-pools-retained" })
                {
                    Assert.Contains(name, breath157Boundaries);
                }
                Assert.Equal(new uint[] { 1, 2, 4 }, breath157Tags.Order().ToArray());
                Assert.Equal(new[] { 0, NativeBattleBreath157Profile.PacketStep }, breath157PacketBanks.Order().ToArray());
                foreach (byte opcode in new byte[] { 0x26, 0x2E, 0x3A, 0x3E })
                {
                    Assert.Contains(opcode, breath157Opcodes);
                }
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "breath157-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = breath157Boundaries.Order().ToArray(), Callbacks = breath157Callbacks.Order().ToArray(), ParticleTags = breath157Tags.Order().ToArray(), CommonBankStarts = breath157PacketBanks.Order().ToArray(), Opcodes = breath157Opcodes.Order().ToArray(), ScratchRotation = breath157Rotation, TravelPointW = breath157PointW, PoseEmitterBoundaries = breath157PoseObservations }));
            }
            if (effectId == 156)
            {
                foreach (uint callback in new uint[] { 0x605DF0, 0x606200, 0x6067B0, 0x606910, 0x605F40, 0x606320, 0x6063E0, 0x6065A0, 0x606A60 })
                {
                    Assert.Contains(callback, cannon156Callbacks);
                }
                foreach (string name in new[] { "four-physical-curve-matrix-lanes", "before-old36-header-contacts", "children-drained-COMMON-waiter", "COMMON-request-pending", "COMMON-completed-pools-retained" })
                {
                    Assert.Contains(name, cannon156Boundaries);
                }
                Assert.Equal(3, cannon156RecordBanks.Count);
                Assert.Equal(new[] { 0, NativeBattleWildCannonBlow156Profile.PacketStep }, cannon156PacketBanks.Order().ToArray());
                foreach (byte opcode in new byte[] { 0x2E, 0x36, 0x3A, 0x3E })
                {
                    Assert.Contains(opcode, cannon156Opcodes);
                }
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "wild-cannon-blow156-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = cannon156Boundaries.Order().ToArray(), Callbacks = cannon156Callbacks.Order().ToArray(), ParticleBanks = cannon156RecordBanks.Order().ToArray(), CommonBankStarts = cannon156PacketBanks.Order().ToArray(), Opcodes = cannon156Opcodes.Order().ToArray(), SampleW = cannon156SamplesW }));
            }
            if (effectId == 155)
            {
                string[] required = ["sorted304-references", "selected-XYZ-W-retained", "dual-mesh-fade-in", "dual-mesh-steady", "dual-mesh-fade-out", "six-point-curve34-samples", "spriteA-recordB-live", "spriteB-recordA-live", "before-old50-bulk-contacts", "closed-pools-retained-storage"];
                foreach (string name in required)
                {
                    Assert.Contains(name, sand155Boundaries);
                }
                Assert.Equal(2, sand155PacketBanks.Count);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "sand-storm155-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = sand155Boundaries.Order().ToArray(), SelectedW = sand155SelectedW, SampleW = sand155SamplesW, MeshPhases = sand155MeshObservations, PrivateBankStarts = sand155PacketBanks.Order().ToArray(), CompletionScope = "Native root waits for actual child survivors, then both fixed task banks return to unowned retained storage" }));
            }
            if (effectId == 128)
            {
                string[] required = ["body-coefficients-captured", "linked-trail-points-live", "main-act-live", "shared-act-scratch-live", "deformed-current-body", "target-body-visibility-restored", "spray-sprite-live", "drip-sprite-live", "splash-sprite-live", "before-main-contact-age53", "all-three-pools-drained"];
                foreach (string name in required)
                {
                    Assert.Contains(name, gastric128Boundaries);
                }
                Assert.Equal(2, gastric128PacketBanks.Count);
                Assert.Equal(source.NativeGraphics!.Models[contactActor].Flags0 & 4, completed.NativeGraphics!.Models[contactActor].Flags0 & 4);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "gastric-juice128-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = gastric128Boundaries.Order().ToArray(), CachedRawVertices = gastric128CachedVertices, LinkedBodyPresent = gastric128LinkedBody, Cache = gastric128CacheBaseline, PointMasks = gastric128PointMasks, PrivateBankStarts = gastric128PacketBanks.Order().ToArray(), CompletionScope = "Native root waits for all three physical target, curve and particle pools" }));
            }
            if (effectId == 132)
            {
                string[] required = ["scalar-owners-initialized", "linked-points-live", "template683490-live", "regular571C80-sprite-live", "primary-act-live", "secondary-act-live", "impact-act-live", "before-main-contact-age45", "all-physical-pools-drained"];
                foreach (string name in required)
                {
                    Assert.Contains(name, acid132Boundaries);
                }
                Assert.Equal(2, acid132PacketBanks.Count);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "disolving-acid132-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = acid132Boundaries.Order().ToArray(), PointMasks = acid132PointMasks, PrivateBankStarts = acid132PacketBanks.Order().ToArray(), CompletionScope = "Native root waits for all five physical working pools, including the custom sprite scheduler" }));
            }
            if (effectId == 136)
            {
                string[] required = ["mutable-sources-preprocessed", "main-act-live", "secondary-act-live", "impact-act-live", "selected-vertices-live", "moving-sprite-live", "emitter-act-live", "simple-sprite-live", "large-particle-live", "large-impact-act-live", "custom-texture-window-live", "before-main-root-return", "ignored-child-banks-detached"];
                foreach (string name in required)
                {
                    Assert.Contains(name, oilShotBoundaries);
                }
                Assert.Equal(2, oilShotPacketBanks.Count);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "oil-shot-native-observations.json"), JsonSerializer.Serialize(new { Boundaries = oilShotBoundaries.Order().ToArray(), PrivateBankStarts = oilShotPacketBanks.Order().ToArray(), CompletionScope = "Native root tests only main survivors; four ignored child banks retain their physical bytes after detachment" }));
            }
            if (effectId == 138)
            {
                Assert.True(salivaSourcesRestored);
                Assert.True(salivaMainRestored);
                Assert.True(salivaSecondaryRestored);
                Assert.True(salivaImpactRestored);
                Assert.True(salivaCurveRestored);
                Assert.True(salivaRibbonRestored);
                Assert.True(salivaCustomRestored);
                Assert.True(salivaSimpleRestored);
                Assert.True(salivaMovingRestored);
                Assert.True(salivaMorphRestored);
                Assert.Equal(2, salivaPacketBanks.Count);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "saliva-native-observations.json"), JsonSerializer.Serialize(new { MainAct = salivaMainRestored, SecondaryAct = salivaSecondaryRestored, ImpactAct = salivaImpactRestored, Curve = salivaCurveRestored, Linked32BytePoints = salivaRibbonRestored, CustomTextureWindow = salivaCustomRestored, Simple = salivaSimpleRestored, Moving = salivaMovingRestored, ScratchW = salivaScratchW, PrivateBankStarts = salivaPacketBanks.Order().ToArray(), CompletionScope = "Native root waits for main and final simple-sprite survivors" }));
            }
            if (effectId == 137)
            {
                Assert.True(oilOpeningRestored);
                Assert.True(oilMainRestored);
                Assert.True(oilImpactRestored);
                Assert.True(oilCustomRestored);
                Assert.True(oilSecondaryRestored);
                Assert.True(oilParticlesRestored);
                Assert.True(oilMorphRestored);
                Assert.True(oilOrdinaryNodes > 0);
                Assert.True(oilDefaultNodes > 0);
                Assert.Equal(2, oilPacketBanks.Count);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "oil-blast-native-observations.json"), JsonSerializer.Serialize(new { OrdinaryStaticNodes = oilOrdinaryNodes, DefaultActNodes = oilDefaultNodes, PrivateBankStarts = oilPacketBanks.Order().ToArray(), CustomTextureWindow = oilCustomRestored }));
            }
            if (effectId == 158)
            {
                Assert.True(meltEyeCurvesRestored);
                Assert.True(meltEyeSlotsARestored);
                Assert.True(meltEyeSlotsBRestored);
                Assert.True(meltEyeHeadRestored);
                Assert.True(meltEyeStreamRestored);
                Assert.True(meltEyeImpactRestored);
                Assert.True(meltEyeImpactBirthRestored);
                Assert.True(meltEyeImpactSpriteRestored);
                Assert.True(meltEyeForkRestored);
                Assert.True(meltEyeFeedbackRestored);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
            }
            if (effectId == 217)
            {
                Assert.True(energyBomberMainRestored);
                Assert.True(energyBomberMorphRestored);
                Assert.True(energyBomberTrailRestored);
                Assert.True(energyBomberImpactRestored);
                Assert.True(energyBomberSpritesRestored);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                Assert.All(completed.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)0, group.ColorDepth);
                    Assert.Equal(0u, group.ColorFlags);
                });
            }
            if (effectId == 101)
            {
                Assert.True(effect101ActRestored);
                Assert.True(effect101MorphRestored);
                Assert.True(effect101ScaleRestored);
                Assert.True(effect101VisibleRestored);
                Assert.True(effect101FlagsRestored);
                Assert.Equal(originalTargetPosition, TargetPositionFootprint(completed.NativeGraphics!.Models[contactActor]));
                Assert.Equal(source.NativeGraphics!.Models[contactActor].Color44, completed.NativeGraphics.Models[contactActor].Color44);
                Assert.Equal(0u, completed.NativeGraphics.Models[contactActor].ScaleVectorAddress48);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                Assert.All(completed.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)0, group.ColorDepth);
                    Assert.Equal(0u, group.ColorFlags);
                });
            }
            if (effectId == 134)
            {
                Assert.True(beamLaserPrimaryRestored, "Beam Laser never advanced both original primary ACT workspaces.");
                Assert.True(beamLaserBeamRestored, "Beam Laser never advanced its original beam ACT workspace.");
                Assert.True(beamLaserEndcapRestored, "Beam Laser never advanced its original endcap ACT workspace.");
                Assert.True(beamLaserRingRestored, "Beam Laser never advanced its original ring ACT workspace.");
                Assert.True(beamLaserStaticRestored, "Beam Laser never crossed its actual static mesh callback boundary.");
                Assert.True(beamLaserMovingRestored, "Beam Laser never exposed four live native moving sprites.");
                Assert.True(beamLaserMorphRestored, "Beam Laser never exposed its actual49-vertex mutable morph.");
                Assert.True(beamLaserFlagsRestored, "Beam Laser never restored only its native saved caster bit8.");
            }
            if (effectId == 133)
            {
                Assert.True(hypnotizeMainRestored, "Hypnotize never advanced its actual main ACT workspace.");
                Assert.True(hypnotizeAnchoredRestored, "Hypnotize never advanced an actual anchored ACT workspace.");
                Assert.True(hypnotizeFadingRestored, "Hypnotize never advanced an actual fading ACT workspace.");
                Assert.True(hypnotizeSpriteRestored, "Hypnotize never exposed its actual projected sprite phase.");
            }
            if (effectId is 177 or 178)
            {
                Assert.True(needleRecordsRestored, "Needles never exposed its actual active56-byte record bank.");
                Assert.True(needleSparksRestored, "Needles never exposed its actual active24-byte spark bank.");
            }
            if (effectId == 71)
            {
                Assert.True(laserBankRestored, "Laser Eye never materialized its actual mutable node bank.");
                Assert.True(laserBindingRestored, "Laser Eye never exposed its saved original caster binding.");
                Assert.True(laserBeamRestored, "Laser Eye never exposed its actual growing beam child.");
                Assert.True(laserParticlesRestored, "Laser Eye never exposed its actual96-byte particles.");
                Assert.All(completed.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)0, group.ColorDepth);
                    Assert.Equal(0u, group.ColorFlags & 0xFFFFFFu);
                });
                for (int group = 0; group < 4; group++)
                {
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags & 0xFF000000u);
                }
            }
            if (effectId == 130)
            {
                Assert.True(eerieMainRestored, "Eerie Sound Wave never advanced its actual main ACT workspace.");
                Assert.True(eerieTravelRestored, "Eerie Sound Wave never advanced its actual travel ACT workspace.");
                Assert.True(eerieImpactRestored, "Eerie Sound Wave never advanced its actual impact ACT workspace.");
            }
            if (effectId == 127)
            {
                Assert.True(petrifyActRestored, "Petrify Stare never advanced both original ACT workspaces for cold continuation.");
                Assert.All(completed.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)0, group.ColorDepth);
                    Assert.Equal(0u, group.ColorFlags);
                });
            }
            if (effectId == 8)
            {
                Assert.True(doomBankRestored, "Doom never materialized its actual addressed node source.");
                Assert.True(doomReaperRestored, "Doom never reached its actual embedded reaper model producer.");
            }
            if (effectId == 47)
            {
                Assert.True(curseIoRestored, "Curse never reached its actual retained IO initialization for cold continuation.");
                Assert.True(curseBankRestored, "Curse never materialized its actual addressed node source for cold continuation.");
                Assert.Equal(4, completed.NativeStage!.Groups.Length);
                for (int group = 0; group < 4; group++)
                {
                    Assert.Equal((short)0, completed.NativeStage.Groups[group].ColorDepth);
                    Assert.Equal(0u, completed.NativeStage.Groups[group].ColorFlags & 0xFFFFFFu);
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags & 0xFF000000u);
                }
            }
            if (effectId == 260)
            {
                Assert.True(pulsarCasterRestored);
                Assert.True(pulsarActRestored);
                Assert.True(pulsarPuffsRestored);
                Assert.True(pulsarSecondaryRestored);
                Assert.True(pulsarRisingRestored);
                Assert.True(pulsarBodyRestored);
                Assert.True(pulsarPositionRestored);
                Assert.True(pulsarIoRestored);
                AssertShockwavePulsar260Position(completed, contactActor, pulsarSavedPosition ?? throw new InvalidDataException("Shockwave Pulsar requires its native saved position."));
                NativeBattleShockwavePulsar260State pulsar = completed.NativeEffectMemory!.ShockwavePulsar260!;
                Assert.Equal(pulsar.ReadDword(NativeBattleShockwavePulsar260Profile.SavedFlagsAddress + (uint)contactActor * 4) & 4, (uint)(completed.NativeGraphics!.Models[contactActor].Flags0 & 4));
                int io = completed.NativeEffectMemory.EffectIo!.BaseOffset!.Value;
                Assert.Equal(2, completed.NativeEffectMemory.Arena!.Read(io + 4, 1)[0]);
                Assert.Equal(0, completed.NativeEffectMemory.Arena.Read(io, 1)[0]);
                Assert.Equal(original.ShockwavePulsar260Resources!.Files[1].Data.ToArray(), completed.NativeEffectMemory.Arena.Read(checked((int)(pulsar.ReadDword(NativeBattleShockwavePulsar260Profile.WorkingGlobal) - NativeBattleShockwavePulsar260Profile.ArenaAddress)) + 0x2E50, 33824));
                Assert.Equal(completed.NativeCamera!.CapturedPose!.Eye, completed.NativeCamera.CurrentPose!.Eye);
                Assert.Equal(completed.NativeCamera.CapturedPose.Target, completed.NativeCamera.CurrentPose.Target);
                Assert.Equal(completed.NativeCamera.CapturedPose.FocalLength, completed.NativeCamera.FocalLength);
                Assert.DoesNotContain(completed.NativeGraphics.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 307)
            {
                Assert.True(rayCasterRestored);
                Assert.True(rayCasterHiddenRestored);
                Assert.True(rayParticlesRestored);
                Assert.True(rayGt3Restored);
                Assert.True(rayTargetDisplaced);
                Assert.True(rayTargetRestored);
                Assert.True(textureLoadPendingRestored);
                Assert.Equal(0, completed.NativeGraphics!.Models[caster].Flags0 & 8);
                AssertRayBomb307Restored(completed, contactActor);
                for (int group = 0; group < 4; group++)
                {
                    Assert.Equal((short)0, completed.NativeStage!.Groups[group].ColorDepth);
                    Assert.Equal(0u, completed.NativeStage.Groups[group].ColorFlags & 0xFFFFFFu);
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags & 0xFF000000u);
                }
                Assert.DoesNotContain(completed.NativeGraphics.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 184)
            {
                Assert.True(ultraCasterRestored);
                Assert.True(ultraEmittersRestored);
                Assert.True(ultraWarpRestored);
                Assert.All(completed.NativeStage!.Groups, group => Assert.Equal(2, group.Flags & 2));
                Assert.True(textureLoadPendingRestored);
                Assert.DoesNotContain(completed.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 135)
            {
                Assert.True(reflectMainRestored);
                Assert.True(reflectTargetRestored);
                Assert.True(reflectHeadRestored);
                Assert.True(reflectTailRestored);
                Assert.True(reflectBonesRestored);
                Assert.True(reflectMorphRestored);
                Assert.Equal(3, reflectParticlesRestored.Count);
                Assert.Equal(2, reflectPacketBanks.Count);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "reflect-beam135-native-observations.json"), JsonSerializer.Serialize(new { MainAct = reflectMainRestored, TargetAct = reflectTargetRestored, SecondaryHeadStatic = reflectHeadRestored, SecondaryTail = reflectTailRestored, Morph544WPreserved = reflectMorphRestored, ParticleCallbacks = reflectParticlesRestored.Order().ToArray(), PrivateBankStarts = reflectPacketBanks.Order().ToArray(), CurrentBonePublication = reflectBonePublication }));
            }
            if (effectId == 126)
            {
                Assert.True(dischargeWorldRestored);
                Assert.True(dischargeBoneRestored);
                Assert.True(dischargeActRestored);
                Assert.True(dischargeMorphRestored);
                Assert.True(dischargeFlashRestored);
                Assert.True(dischargeScreenFlashRestored);
                Assert.Equal(2, dischargePacketBanks.Count);
                Assert.Equal(JsonSerializer.Serialize(source.Combatants.Single(actor => actor.Id == target).NativeStatus), JsonSerializer.Serialize(completed.Combatants.Single(actor => actor.Id == target).NativeStatus));
                File.WriteAllText(Path.Combine(output!, "electric-discharge-native-observations.json"), JsonSerializer.Serialize(new { PrivateBankStarts = dischargePacketBanks.Order().ToArray(), WorldPointCount = 512, BonePointCount = 512, PointStride = 40, TypedCpuRowBinding = dischargeBoneRestored, MorphBytes = NativeBattleElectricDischarge126Profile.MorphBytes }));
            }
            if (effectId == 275)
            {
                Assert.True(electroCasterRestored);
                Assert.True(electroWorldRestored);
                Assert.True(electroBoneRestored);
                Assert.True(electroActRestored);
                Assert.True(electroFlashRestored, "Electrocute never exposed its actual actor color flash for cold continuation.");
                Assert.True(electroScreenFlashRestored, "Electrocute never exposed its actual native screen flash for cold continuation.");
                Assert.Equal(0, completed.NativeGraphics!.Models[contactActor].Flags0 & 0x800);
                Assert.DoesNotContain(completed.NativeGraphics.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 317)
            {
                Assert.True(acidCasterRestored);
                Assert.True(acidTrailRestored);
                Assert.True(acidActRestored);
                Assert.True(acidBodyRestored, "Acid never exposed its actual hidden-target custom body COMMON packets for cold continuation.");
                Assert.Equal(source.NativeGraphics!.Models[contactActor].Flags0 & 4, completed.NativeGraphics!.Models[contactActor].Flags0 & 4);
                Assert.DoesNotContain(completed.NativeGraphics.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 305)
            {
                Assert.True(gatlingCasterRestored);
                Assert.True(gatlingSlotARestored);
                Assert.True(gatlingSlotBRestored);
                Assert.Equal(NativeBattleGatlingGun305Profile.PacketBytes, 2 * NativeBattleGatlingGun305Profile.PacketBankBytes);
                Assert.DoesNotContain(completed.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 306)
            {
                Assert.True(degeneratorCasterRestored);
                if ((contact.Primary.HitFlags3 & 4) == 0)
                {
                    Assert.True(degeneratorBodyRestored, "Degenerator never exposed its actual deformed actor COMMON packets for cold continuation.");
                    Assert.True(degeneratorEmitterRestored);
                    Assert.Equal(4, completed.NativeGraphics!.Models[contactActor].Flags0 & 4);
                }
                else
                {
                    Assert.False(degeneratorBodyRestored);
                    Assert.False(degeneratorEmitterRestored);
                    Assert.Equal(source.NativeGraphics!.Models[contactActor].Flags0 & 4, completed.NativeGraphics!.Models[contactActor].Flags0 & 4);
                    Assert.DoesNotContain(completed.NativeGraphics.OriginalEffect!.Pools[1].Slots, task => task.Callback is 0x5A47C0 or 0x5A5540 or 0x5A4D80);
                }
                Assert.DoesNotContain(completed.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 300)
            {
                Assert.True(badBreathCasterRestored);
                Assert.True(badBreathDeformRestored);
                Assert.True(badBreathTargetRestored);
                Assert.Equal(StageColors(source), StageColors(completed));
                Assert.DoesNotContain(completed.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 299)
            {
                Assert.True(microMissilesCasterRestored);
                Assert.True(microMissilesMissileRestored);
                Assert.True(microMissilesImpactRestored);
                Assert.Equal(StageColors(source), StageColors(completed));
                Assert.DoesNotContain(completed.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 297)
            {
                Assert.Equal(StageColors(source), StageColors(completed));
                Assert.DoesNotContain(completed.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ModelSequence505C70);
            }
            if (effectId == 74)
            {
                Assert.True(levelDeathAuraRestored);
                NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(74);
                NativeBattleDeathState state = completed.NativeEffectMemory!.LevelDeath74!;
                if ((contact.Primary.HitFlags3 & 4) != 0)
                {
                    Assert.False(levelDeathDarkRestored);
                    Assert.Equal(original.DeathResources!.DarkActors.ToArray(), state.DarkActors!.Value.ToArray());
                    Assert.DoesNotContain(completed.NativeGraphics!.OriginalEffect!.Pools[4].Slots, task => task.Callback == profile.Callback(0x8B78C0) && task.ReadDword(0x30) == profile.DarkAddress);
                    Assert.DoesNotContain(completed.NativeGraphics.OriginalEffect.Pools[6].Slots, task => task.Callback == profile.Callback(0x8BD840));
                }
                else
                {
                    Assert.True(levelDeathDarkRestored);
                    Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(state.DarkActors!.Value.AsSpan()));
                    Assert.Contains(completed.NativeGraphics!.OriginalEffect!.Pools[6].Slots, task => task.Callback == profile.Callback(0x8BD840));
                }
                for (int group = 0; group < completed.NativeStage!.Groups.Length; group++)
                {
                    Assert.Equal((short)0, completed.NativeStage.Groups[group].ColorDepth);
                    Assert.Equal(0u, completed.NativeStage.Groups[group].ColorFlags & 0xFFFFFFu);
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags & 0xFF000000u);
                }
            }
            if (effectId == 29)
            {
                Assert.True(windBlastBankRestored, "Wind Blast never materialized its actual node bank for cold continuation.");
            }
            if (effectId == 43)
            {
                Assert.True(meltingResourcesRestored, "Melting Bubble never reached its actual flag1 directory and anchor-context cold boundary.");
                Assert.True(meltingAnchorChecks > 1, "Melting Bubble never exercised its per-callback caster anchor producer.");
                File.WriteAllText(Path.Combine(output!, "anchor-observations.json"), JsonSerializer.Serialize(meltingAnchorObservations));
            }
            if (effectId is 8 or 23 or 47 or 60 or 74)
            {
                Assert.True(textureLoadPendingRestored, "The selected module never exposed its actual pending texture-load completion callback for cold continuation.");
            }
            if (effectId == 60)
            {
                Assert.True(targetDisplaced, "Effect60 never displaced its actual physical target.");
                Assert.True(targetPositionRestored, "Effect60 never restored the actual target position before ACT completion.");
                Assert.True(targetRestored, "Effect60 never restored its actual packed target position and rotation fields.");
                Assert.True(secondaryBankRestored, "Effect60 never initialized its independently addressed secondary node bank.");
                Assert.Equal(originalTargetFootprint, TargetFootprint(completed.NativeGraphics!.Models[contactActor]));
                File.WriteAllText(Path.Combine(output!, "target-motion.json"), JsonSerializer.Serialize(new { OriginalPhysicalFields = originalTargetFootprint, CompletedPhysicalFields = TargetFootprint(completed.NativeGraphics.Models[contactActor]), PositionRestoredCallback = targetPositionRestoreCallback, RotationRestoredCallback = targetRotationRestoreCallback, Target = contactActor }));
            }
            if (effectId == 20)
            {
                Assert.True(actor6Activated, "Effect20 never reached its actual suspended actor6 activation producer.");
                Assert.True(Effect20ActivationCompleted(completed), "Effect20 left its deferred activation descriptor service pending.");
                Assert.True(effect20VibrationHandle is > 0, "Effect20 never retained its actual vibration handle.");
                Assert.False(completed.NativeVibrationProcess!.IsPlaying(effect20VibrationHandle!.Value));
            }
            if (effectId == 62)
            {
                Assert.True(devourMuted, "Devour never reached its actual muted music owner.");
                Assert.Equal((int?)127, completed.NativeGlobalSoundProcess!.MusicPlayback!.Gain0);
                Assert.Equal(4, completed.NativeStage!.Groups.Length);
                for (int group = 0; group < 4; group++)
                {
                    Assert.Equal((short)0, completed.NativeStage.Groups[group].ColorDepth);
                    Assert.Equal(0u, completed.NativeStage.Groups[group].ColorFlags & 0xFFFFFFu);
                    Assert.Equal(source.NativeStage!.Groups[group].ColorFlags & 0xFF000000u, completed.NativeStage.Groups[group].ColorFlags & 0xFF000000u);
                }
                Assert.Equal(logicalHp, completed.Combatants.Single(actor => actor.Id == target).CurrentHp);
                Assert.Equal(4, completed.NativeGraphics!.Models[contactActor].Flags0 & 4);
                for (int actor = 0; actor < 7; actor++)
                {
                    Assert.Equal(source.NativeGraphics.Models[actor].Flags0 & 0x0800, completed.NativeGraphics.Models[actor].Flags0 & 0x0800);
                    if (actor != contactActor)
                    {
                        Assert.Equal(source.NativeGraphics.Models[actor].Flags0 & 4, completed.NativeGraphics.Models[actor].Flags0 & 4);
                    }
                }
            }
            Assert.True(completed.Events.Count(item => item.NativeModuleSound is not null) > initialSounds, "The selected component published no original module sound.");
            Assert.True(Released(completed), "The original returned task, active effect and action104 descriptor must release through native execution.");
            if (effectId is 79 or 83 or 289 or 315 or 303 or 298 or 279 or 304 or 302 or 284 or 286 or 282 or 281 or 214 or 44 or 45 or 46 or 48 or 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 101 or 135 or 131 or 128 or 155 or 156 or 157 or 12 or 49 or 170 or 172 or 171 or 181 or 182 or 183 or 190 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 184 or 260 && peakSnapshot is not null)
            {
                File.WriteAllText(Path.Combine(output!, "visible-packet-ownership.json"), peakOwnership ?? throw new InvalidDataException("The peak node packet ownership was not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "visible", peakSnapshot, peakPixels ?? throw new InvalidDataException("The peak node pixels were not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", peakWithout ?? throw new InvalidDataException("The peak node control was not retained."), peakPlain ?? throw new InvalidDataException("The peak node control pixels were not retained."));
                ColdNext("peak", peakSnapshot);
            }
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = effectId == 79 ? "Declared original model122/action351/sequence15 death-action renderer and lifecycle against current hiddencompanion3; ordinary death-AI, KO, selector admission and formula execution are excluded; sourceHP/status retained" : effectId == 83 ? "Declared original model122/action336/sequence16 renderer and lifecycle against aliveparty0; original AI criterion0 requires deadparty and ordinary AI/dead-target/KO/formula admission is excluded; source HP/status retained" : effectId == 289 ? "Original activated formation104/stage19 model34 physical4 action131 sequence13, declared party0 contact consistent with original random-party selector; current bone9 and mutable source W, programmes and original root completion retain one active particle. No ordinary AI or formula admission" : effectId == 315 ? "Original formation104/stage19 model75 physical3 action151 sequence13 self-target component, model34 physical4 preserved dormant until actual487670/113 activation; native tasks, current body/palette, dual packet cursors and common release. No ordinary death-AI, KO or formula admission" : effectId == 303 ? "Original formation363 loaded model11 caster and latent model12 recipient; actual phase transition and file360 type1, dual packet cursors, current bones and native activation; no ordinary death AI or formula admission" : effectId == 298 ? "Original unnamed Effect298 command8/action119/sequence14 formation194 model102/native118; current offset body, retained mutable script counters, natural A/C/D programmes and asynchronous file360 completion. Original sequence and native controller have no contact; unused declared packet and logical HP/status retained" : effectId == 279 ? "Original Mega Spark279 command8/action106/sequence14 formation377; current model30/native46 bones, linked rows and attachments, ProgrammeA36 and ProgrammeB16 actual advances, both physical packet domains; declared seven-HP party0 input with logical HP/status retained" : effectId == 304 ? "Original Boomerang Sword304 command8/action159/sequence13 formation133 ordinary model79 current bone23; actual target pointers, trajectory, programme and current particles; declared seven-HP party0 post-formula input with logical HP/status retained, no ordinary formula or AI admission" : effectId == 302 ? "Original Snipe Laser302 command8/action155/sequence13 formation155 ordinary model8 current bone11; two clones0/-2, A initialized only, advanced B/C/D, direct572200 beam and projected sprites, clone0 single old43 contact; declared seven-HP party0 post-formula input with logical HP/status retained, no ordinary formula or AI admission" : effectId == 284 ? "Original enemy Micro Missiles284 command8/action201/sequence13 formation150 ordinary visible model1; six simultaneous missiles, current recipient bones, reaction4, two impact sources and three sprites; declared seven-HP party0 post-formula contact retaining logical HP/status" : effectId == 286 ? "Original Mini Pulse Cannon286 command8/action293/sequence12 formation431 after controlled authentic52 allocation and53 activation; actual model110 current composed bone2, four original programmes, kind9 sprites and texture windows, declared seven-HP party0 post-formula contact retaining logical HP/status" : effectId == 282 ? "Original Evil-Eye282 command8/action152/sequence12 formation436 after controlled authentic dormant scene-slot3 allocation; actual model94 current bone13, declared seven-HP party0 post-formula contact and retained logical HP/status" : effectId == 281 ? "Original Shotgun281 command8/action61/sequence13 formation129 with actual current caster78/bone23 and recipient bones, two original programmes and projected sprites; declared seven-HP physical contact with logical HP/status retained; no ordinary formula or AI admission" : effectId == 214 ? "Original Gravija214 command8/action309/sequence13 formation354 with actual model90, native copiedmesh/preprocessing and programme/warp/currentCOMMON completion; declared party0 seven-HP contact with logical HP/status retained, no percentageformula or AI admission" : effectId == 44 ? "Original Junk44 command8/action226/sequence15; originalformation236 prototype0 selected through controlled genuine native allocation/activation; one declared physical party0 contact and canonicalCOMMON completionfiles, logical HP/status retained; no twenty-AI-death/formula admission" : effectId == 49 ? "Original Resonance49 command8/action121/sequence13 formation147 with distinct actual model93 actors3/4 and party0 physical contact; native programme/sequence retirement and asynchronous COMMON reload; logical HP/status retained, no ordinary formula/targeting/AI admission" : effectId == 12 ? "Original Blade Shot12 command8/action7/sequence15 formation218, actual model9/current31bones/object7 mask80 and recipientF0/F1 publications; declared first seven-HP presentation with logical HP/status retained; no formula/AI admission" : effectId == 170 ? "Original Acid170 command8/action35/sequence15 formation71; distinct caster36/42bones and actual recipient0/body23+linked2, declared first physical contact with logical HP/status retained; no formula/status/AI-turn admission" : effectId == 172 ? "Original Morph172 command8/action53/sequence13 and formation124; actual original41-bone three-object currentbody, header retained without native contact, logical HP/status unchanged; no formula/AI-turn admission" : effectId == 171 ? "Original PoisonGas171 command8/action32/sequence14 and formation71; actual model36 current42-bone body/E2 overlay and declared seven-HP counted contacts, with logical HP/status retained; no formula, status result, accuracy or AI-turn admission" : effectId == 181 ? "Original Additional181 command31/action8/sequence24 and formation94; actualcaster0body+linkedweapon, declared seven-HP first physical post-formula contact with logical HP/status retained; no formula, ordinary command or card-conversion admission" : effectId == 190 ? "Original ChainGun command8/action154/sequence14 and formation155; declared seven-HP counted post-formula presentation with original model8/current party body owners and logical HP/status retained; no formula, accuracy, ordinary command, AI or ordinary AOE admission" : effectId == 182 ? "Original Additional command29/action10/sequence11 and formation94; declared Amount0 contact BYTE3 bit4 selects native director, camera and sound with logical HP/status retained; no formula, accuracy, status, ordinary command, AI or card-conversion admission" : effectId == 186 ? "Original Blaster command8/action122/sequence13 and formation247 with declared seven-HP post-formula contact and logical HP/status retained; no status result, formula, accuracy, ordinary command or AI admission" : effectId == 151 ? "Original Cannon Blow command8/action69/sequence16 and formation125 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 150 ? "Original Gatling Gun command8/action68/sequence14 and formation125 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 129 ? "Original Breath129 command8/action102/sequence12 and formation338 with declared seven-HP post-formula presentation and logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission" : effectId == 139 ? "Original Sonic Wave command8/action182/sequence11 and formation384 with declared seven-HP post-formula presentation and logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission" : effectId == 183 ? "Original effect183 command23/action11/sequence23 and formation94; declared seven-HP post-formula presentation with logical HP/status retained; no level formula, status result, accuracy or ordinary command admission" : effectId == 157 ? "Original Breath command8/action37/sequence14 and formation413 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 156 ? "Original Wild Cannon Blow command8/action70/sequence17 and formation125 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 155 ? "Original Sand Storm command8/action39/sequence14 and formation452 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 128 ? "Original Gastric Juice command8/action77/sequence14 and formation220 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 132 ? "Original Disolving Acid command8/action183/sequence12 and formation376 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 136 ? "Original Oil Shot command8/action180/sequence13 and formation384 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 138 ? "Original Saliva command8/action175/sequence16 and formation378 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 131 ? "Original Enemy Bad Breath command8/action184/sequence14 and formation376 with declared seven-HP post-formula contact and logical HP/status retained; no status formula, accuracy, ordinary command or AI admission" : effectId == 126 ? "Original Electric Discharge command8/action28/sequence15 and formation666 with declared seven-HP post-formula contact and logical HP/status retained; no ordinary command, AI, accuracy or formula admission" : effectId == 137 ? "Original Oil Blast command8/action181/sequence12 and formation384 with declared seven-HP post-formula contact and logical HP/status retained; no ordinary action, accuracy, AI or formula admission" : effectId == 158 ? "Original Melt-Eye command8/action36/sequence13 with declared seven-HP post-formula contact and logical HP/status retained; no magic damage/status formula, accuracy, ordinary command or AI admission" : effectId == 217 ? "Original Energy Bomber command8/action316/sequence16, both formation796 enemies retained, declared seven-HP post-formula contact with logical HP/status retained; no physical formula, accuracy, ordinary command or AI admission" : effectId == 101 ? "Original unnamed Effect101 command8/action290/sequence13; real recipient sequence14 has no contact event, unused declared seven-HP packet, logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 71 ? "Original Laser Eye command15/action0/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no damage formula, accuracy, ordinary command or AI admission" : effectId == 260 ? "Original Shockwave Pulsar command15/action15/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command, GF or AI admission" : effectId == 307 ? "Original Blue Ray-Bomb command15/action14/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 184 ? "Original Blue Ultra Waves command15/action1/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 275 ? "Original Electrocute command15/action2/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage formula, accuracy, ordinary command or AI admission" : effectId == 317 ? "Original Acid command15/action7/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 305 ? "Original Gatling Gun command15/action8/crisis1/sequence21 with declared seven-HP damage post-formula input and retained logical HP/status; no physical damage formula, accuracy, ordinary command or AI admission" : effectId == 306 ? "Original Degenerator command15/action4/crisis1/sequence21 with declared Amount0 post-formula contact; native MISS/non-MISS body and visibility branch, actual logical HP/status retained; no removal/status formula, accuracy, ordinary command or AI admission" : effectId == 300 ? "Original Bad Breath command15/action10/crisis1/sequence21 with declared Amount0 MISS post-formula input; current HP/status retained; no status, accuracy, formula, ordinary command or AI admission" : effectId == 299 ? "Original Micro Missiles command15/action6/crisis1/sequence21 with declared seven-HP damage post-formula input; current HP/status retained; no percentage formula, ordinary command or AI admission" : effectId == 297 ? "Original White Wind command15/action11/sequence21 with declared seven-HP healing post-formula input; current HP/status retained; no ordinary command, AI or formula admission" : effectId == 74 ? "Original Level Death command15/action3/sequence21 with explicitly declared selected post-formula contact; no level comparison, KO/status result, ordinary command, AI or formula admission" : request.ControlledDrainContact ? "Controlled original Absorb presentation with paired declared drain halves and original target resistance; no ordinary formula or inventory admission" : effectId == 62 ? "Controlled original Devour action104 with actual target outcome; no ordinary formula or inventory admission" : "Controlled actor-director action104 and seven-HP contact; no ordinary action or formula admission", Effect = effectId, exported.Selection, action.Command, action.ActionId, callbacks, DiagnosticMaximumCallbacks = 4096, contacts, contactGlyphs, secondaryContactGlyphs, completions, visibleChangedRgb, cameraChanged, stageChanged, actor6Activated, effect20VibrationHandle, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

            NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot)
            {
                return snapshot.NativeGraphics?.OriginalEffect is { } selected && selected.Binding.Effect == effectId ? selected : null;
            }

            void ColdNext(string name, BattleSnapshot snapshot)
            {
                boundary = "cold-" + name;
                BattleSnapshot? previous = prior;
                prior = snapshot;
                NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
                BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
                BattleSession direct = BattleSession.Restore(snapshot);
                cold.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                direct.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                cold.Tick();
                direct.Tick();
                Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
                if (effectId == 43)
                {
                    _ = AssertMeltingAnchors(snapshot, cold.Capture());
                }
                byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
                Assert.Equal(scene.Render(direct, 320, 224).ToArray(), pixels);
                NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
                restoredBoundaries.Add(name);
                prior = previous;
            }

            int AssertMeltingAnchors(BattleSnapshot beforeCallback, BattleSnapshot afterCallback)
            {
                NativeBattleOriginalEffectState? beforeEffect = Selected(beforeCallback);
                NativeBattleOriginalEffectState? afterEffect = Selected(afterCallback);
                if (afterEffect is not { Pools.Length: > 2 })
                {
                    return 0;
                }
                int count = 0;
                for (int slot = 0; slot < afterEffect.Pools[2].Slots.Length; slot++)
                {
                    NativeBattleModulePoolTask task = afterEffect.Pools[2].Slots[slot];
                    NativeBattleModulePoolTask? old = beforeEffect is { Pools.Length: > 2 } ? beforeEffect.Pools[2].Slots[slot] : null;
                    bool previouslyActive = old is not null && (old.Flags & 1) != 0 && old.Callback == NativeBattleMeltingBubble43Profile.GenericCallback;
                    if (task.Callback != NativeBattleMeltingBubble43Profile.GenericCallback || (task.Flags & 1) == 0 && !previouslyActive || task.ReadDword(0x34 + 0x224) != 0x15C6084)
                    {
                        continue;
                    }
                    Assert.Equal((ushort)0, task.ReadWord(0x34 + 0x24));
                    Assert.Equal((ushort)caster, task.ReadWord(0x34 + 0x1E));
                    NativeBattleGraphicsModel model = beforeCallback.NativeGraphics!.Models[caster];
                    NativeBattleVector anchor = NativeBattlePoseMatrices.ReadBone(model, meltingPoseSource ?? throw new InvalidDataException("The caster anchor requires its original pose source."), 240, 0);
                    short z = unchecked((short)(anchor.Z + 256));
                    Assert.Equal(unchecked((uint)(anchor.X << 16)), task.ReadDword(0x34 + 0x1F4));
                    Assert.Equal(unchecked((uint)(anchor.Y << 16)), task.ReadDword(0x34 + 0x1F8));
                    Assert.Equal(unchecked((uint)(z << 16)), task.ReadDword(0x34 + 0x1FC));
                    if (old is not null && old.ReadDword(0x34 + 0x224) == 0x15C6084)
                    {
                        Assert.Equal(old.ReadDword(0x34 + 0x200), task.ReadDword(0x34 + 0x200));
                    }
                    meltingAnchorObservations.Add(new { BeforeFrame = beforeCallback.Frame, AfterFrame = afterCallback.Frame, NativeCallback = afterCallback.NativeGraphics!.Callback, Slot = slot, ContextPhase = task.ReadWord(0x34 + 0x20), X = task.ReadDword(0x34 + 0x1F4), Y = task.ReadDword(0x34 + 0x1F8), Z = task.ReadDword(0x34 + 0x1FC), W = task.ReadDword(0x34 + 0x200) });
                    count++;
                }
                return count;
            }
        }
        catch (Exception error)
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", prior, null);
            }
            if (battle is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            }
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Scenario = "Controlled post-formula component", Effect = effectId, boundary, callbacks, contacts, completions, visibleChangedRgb, initialized, liveRestored, RestoredBoundaries = restoredBoundaries, Error = error.ToString() }));
            throw;
        }
    }


    [Fact]
    public void Concrete_actor_director_standalone_matches_its_actual_pre_submission_model_prefix()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACTOR_DIRECTOR_SNAPSHOT");
        string? expectedPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACTOR_DIRECTOR_NEXT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ACTOR_DIRECTOR_EVIDENCE");
        Assert.SkipUnless(directory is not null && sourcePath is not null && expectedPath is not null && output is not null, "Select original resources, a concrete standalone/next pair and a distinct output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        BattleSnapshot expected = NativeBattleEffectFamilyRunner.LoadSnapshot(expectedPath!);
        ushort effect = source.NativeGraphics!.OriginalEffect!.Binding.Effect;
        Assert.Contains(effect, new ushort[] { 16, 61, 81, 82 });
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
        battle.Tick();
        BattleSnapshot current = battle.Capture();
        NativeBattleRestorationPresentationTests.Publish(output!, "actual-next", current, null);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(current));
        BattleSnapshot prior = source;
        ImmutableHashSet<NativeBattlePrimitiveReference> owned = [];
        List<object> progression = [];
        for (int callback = 1; callback <= 8; callback++)
        {
            List<NativeBattleModelPrefixDiagnostic> matches = [];
            owned = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, matches.Add);
            Assert.Contains(matches, match => match.Start >= 0 && match.BodyPrimitives > 0 && match.MismatchOffset is null);
            progression.Add(new { Callback = callback, current.Frame, NativeCallback = current.NativeGraphics!.Callback, OwnedPackets = owned.Count, Matches = matches });
            if (!owned.IsEmpty || callback == 8)
            {
                break;
            }
            prior = current;
            battle.Tick();
            current = battle.Capture();
        }
        File.WriteAllText(Path.Combine(output!, "prefix-diagnostics.json"), JsonSerializer.Serialize(progression));
        NativeBattleRestorationPresentationTests.Publish(output!, "selected", current, null);
        NativeBattlePrimitiveFrame frame = current.NativeGraphics!.PrimitiveFrame!;
        BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
        byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(battle, 320, 224).ToArray();
        byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
        int changed = CountRgbChanges(pixels, plain);
        NativeBattleRestorationPresentationTests.Publish(output!, "with-effect", current, pixels);
        NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", without, plain);
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "selector-matched", Scope = "Exact standalone next plus bounded at-most-eight callback search with complete model match; a zero effect prefix is recorded as zero and does not satisfy the whole-component RGB gate", Effect = effect, Source = sourcePath, ExpectedNext = expectedPath, current.Frame, NativeCallback = current.NativeGraphics.Callback, changed, Progression = progression, Packets = owned.OrderBy(packet => packet.ByteOffset).ToArray() }));
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> ActorDirectorPacketReferences(BattleSnapshot prior, BattleSnapshot current)
    {
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (prior.NativeGraphics!.Auxiliary.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0) || prior.NativeGraphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0) || graphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        if (graphics.OriginalEffect!.Binding.Effect == 79)
        {
            return NativeBattleEffect79PresentationObserver.EffectCommands(prior, current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 83)
        {
            return NativeBattleEffect83PresentationObserver.EffectCommands(prior, current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 289)
        {
            return NativeBattleEffect289PresentationObserver.EffectCommands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 315)
        {
            return NativeBattleEffect315PresentationObserver.EffectCommands(current).Select(command => command.Packet).ToImmutableHashSet().Union(NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current));
        }
        if (graphics.OriginalEffect!.Binding.Effect == 283)
        {
            return NativeBattleEffect283PresentationObserver.EffectCommands(current).Select(command => command.Packet).ToImmutableHashSet().Union(NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current));
        }
        if (graphics.OriginalEffect!.Binding.Effect == 303)
        {
            return NativeBattleEffect303PresentationObserver.EffectCommands(current).Select(command => command.Packet).ToImmutableHashSet().Union(NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current));
        }
        if (graphics.OriginalEffect!.Binding.Effect == 298)
        {
            return Effect298Commands(current).Select(command => command.Packet).ToImmutableHashSet().Union(NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current));
        }
        if (graphics.OriginalEffect!.Binding.Effect == 279)
        {
            return MegaSpark279Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 304)
        {
            return BoomerangSword304Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 302)
        {
            return SnipeLaser302Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 284)
        {
            return EnemyMicroMissiles284Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 286)
        {
            return MiniPulseCannon286Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 282)
        {
            return EvilEye282Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 281)
        {
            return Shotgun281Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 214)
        {
            return Gravija214Commands(current).Select(command => command.Packet).ToImmutableHashSet();
        }
        if (graphics.OriginalEffect!.Binding.Effect == 44)
        {
            return Junk44PacketReferences(current);
        }
        if (graphics.OriginalEffect!.Binding.Effect == 19)
        {
            return NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
        }
        if (graphics.OriginalEffect.Binding.Effect == 45)
        {
            return Stare45PacketReferences(prior, current);
        }
        if (graphics.OriginalEffect.Binding.Effect == 49)
        {
            return Resonance49PacketReferences(current);
        }
        if (graphics.OriginalEffect!.Binding.Effect == 184)
        {
            ImmutableHashSet<NativeBattlePrimitiveReference> wavesCommon = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: (model, resource) => ResolveBlueBindingResource(current, model, resource, NativeBattleUltraWaves184Profile.SavedBindingAddress, address => current.NativeEffectMemory!.UltraWaves184!.ReadResourcePointer(address)));
            return wavesCommon.Union(graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command.Packet).Where(packet => packet.Buffer == 0 && packet.Bank is "22DB3C4" or "22BE8E8" or "D34868"));
        }
        DirectedNodePacketState state = Director(current);
        if (state.Effect is 45 or 46 or 48 or 135 or 131 or 128 or 155 or 156 or 157 or 12 or 49 or 170 or 172 or 171 or 181 or 182 or 183 or 190 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 101 or 71 or 130 or 127 or 8 or 20 or 23 or 29 or 43 or 47 or 56 or 60 or 62 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 260 && (state.PrimaryDrawCursor is null || state.SecondaryDrawCursor is null))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> common = state.CommonMeshes ? NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: state.Effect == 260 ? (model, resource) => ResolveShockwavePulsar260Resource(current, model, resource) : state.Effect == 317 ? (model, resource) => ResolveAcid317Resource(current, model, resource) : state.Effect == 306 ? (model, resource) => ResolveDegenerator306Resource(current, model, resource) : null) : ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        int secondary = state.SecondaryDrawCursor ?? throw new InvalidDataException("The actor director sprite span requires its current secondary cursor.");
        int start = checked(secondary - state.DrawStep);
        int end = state.PrimaryDrawCursor ?? throw new InvalidDataException("The actor director sprite span requires its actual current primary end.");
        Assert.InRange(start, state.SourceArenaOffset, state.SourceArenaOffset + state.DrawStep);
        Assert.InRange(end, start, secondary);
        IEnumerable<NativeBattlePrimitiveReference> sprites = graphics.PrimitiveFrame!.LastCommands.Select(resolved => resolved.Command).Where(command => command.Packet.Bank == state.PacketBank && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end && (state.Effect == 48 && command.Opcode == 0xE1 || state.Effect == 139 || command.Opcode is >= 0x20 and < 0x80 || (state.Effect is 136 or 137 or 138 or 181 or 182 or 183 or 190 or 150 or 151 or 186 or 139 or 129) && command.Opcode == 0xE2 || state.Effect is 136 or 190 && command.Opcode == 0xE1 || state.Effect == 131 && command.Opcode is 0xE1 or 0xE2)).Select(command => command.Packet);
        if (state.Effect == 172)
        {
            NativeBattlePrimitiveProcess process = current.NativeEffectMemory!.PrimitiveProcess!;
            NativeBattleLogicalCommand[] commands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == process.PacketBank && command.WordOffset == 1).ToArray();
            IEnumerable<NativeBattlePrimitiveReference> overlays = commands.Where(overlay => overlay.Opcode is 0x32 or 0x3A && commands.Any(basis => basis.Packet.Buffer == overlay.Packet.Buffer && basis.Packet.ByteOffset + (overlay.Opcode == 0x32 ? 32 : 40) == overlay.Packet.ByteOffset && (basis.Opcode & 0xFC) == (overlay.Opcode == 0x32 ? 0x24 : 0x2C) && basis.OrderingBucket == overlay.OrderingBucket + 1 && basis.VertexPayload4.SequenceEqual(overlay.VertexPayload4) && Enumerable.Range(0, overlay.Opcode == 0x32 ? 3 : 4).All(corner => basis.Words[1 + corner * 2] == overlay.Words[1 + corner * 2]))).Select(command => command.Packet);
            return common.Union(sprites).Union(overlays);
        }
        if (state.Effect == 171)
        {
            NativeBattlePrimitiveProcess process = current.NativeEffectMemory!.PrimitiveProcess!;
            IEnumerable<NativeBattlePrimitiveReference> body = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == process.PacketBank && command.Opcode == 0xE2 && command.WordOffset == 1 && command.Words[0] == 0xE2000210).Select(command => command.Packet);
            return common.Union(sprites).Union(body);
        }
        if (state.Effect == 181)
        {
            return common.Union(sprites).Union(Additional181Pairs(current).SelectMany(pair => new[] { pair.Base.Packet, pair.Overlay.Packet }));
        }
        return common.Union(sprites);
    }

    private sealed record DirectedNodePacketState(ushort Effect, int SourceArenaOffset, int? PrimaryDrawCursor, int? SecondaryDrawCursor, int DrawStep, bool CommonMeshes, string PacketBank = "20DFAB8");

    private static DirectedNodePacketState Director(BattleSnapshot snapshot)
    {
        ushort effect = snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect;
        if (effect == 79)
        {
            NativeBattleEffect79State state = snapshot.NativeEffectMemory!.Effect79!;
            uint source = state.ReadDword(NativeBattleEffect79Profile.InitializerGlobal);
            uint cursor = state.ReadDword(NativeBattleEffect79Profile.CursorGlobal);
            int offset = source == 0 ? 0 : checked((int)(source - 0x20DFAB8u));
            int start = cursor >= source + 0x8000 ? offset + 0x8000 : offset;
            return new(effect, offset, cursor == 0 ? null : checked((int)(cursor - 0x20DFAB8u)), start + 0x8000, 0x8000, true);
        }
        if (effect == 83)
        {
            NativeBattleEffect83State state = snapshot.NativeEffectMemory!.Effect83!;
            uint source = state.ReadDword(NativeBattleEffect83Profile.InitializerGlobal);
            uint cursor = state.ReadDword(0x25C9F74);
            int offset = source == 0 ? 0 : checked((int)(source - 0x20DFAB8u));
            int start = cursor >= source + 0x8000 ? offset + 0x8000 : offset;
            return new(effect, offset, cursor == 0 ? null : checked((int)(cursor - 0x20DFAB8u)), start + 0x8000, 0x8000, true);
        }
        if (effect == 289)
        {
            uint cursor = snapshot.NativeEffectMemory!.Effect289!.ReadDword(NativeBattleEffect289Profile.CursorGlobal);
            int start = cursor >= NativeBattleEffect289Profile.ArenaAddress + NativeBattleEffect289Profile.SecondPacketOffset ? NativeBattleEffect289Profile.SecondPacketOffset : NativeBattleEffect289Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleEffect289Profile.ArenaAddress)), start + NativeBattleEffect289Profile.PacketBankBytes, NativeBattleEffect289Profile.PacketBankBytes, false);
        }
        if (effect == 315)
        {
            uint cursor = snapshot.NativeEffectMemory!.Effect315!.ReadDword(NativeBattleEffect315Profile.CursorGlobal);
            int start = cursor >= NativeBattleEffect315Profile.ArenaAddress + NativeBattleEffect315Profile.SecondPacketOffset ? NativeBattleEffect315Profile.SecondPacketOffset : NativeBattleEffect315Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleEffect315Profile.ArenaAddress)), start + 0xC000, 0xC000, false);
        }
        if (effect == 283)
        {
            uint cursor = snapshot.NativeEffectMemory!.Effect283!.ReadDword(NativeBattleEffect283Profile.CursorGlobal);
            int start = cursor >= NativeBattleEffect283Profile.ArenaAddress + NativeBattleEffect283Profile.SecondPacketOffset ? NativeBattleEffect283Profile.SecondPacketOffset : NativeBattleEffect283Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleEffect283Profile.ArenaAddress)), start + NativeBattleEffect283Profile.PacketBankBytes, NativeBattleEffect283Profile.PacketBankBytes, false);
        }
        if (effect == 303)
        {
            uint cursor = snapshot.NativeEffectMemory!.Effect303!.ReadDword(NativeBattleEffect303Profile.CursorGlobal);
            int start = cursor >= NativeBattleEffect303Profile.ArenaAddress + NativeBattleEffect303Profile.SecondPacketOffset ? NativeBattleEffect303Profile.SecondPacketOffset : NativeBattleEffect303Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleEffect303Profile.ArenaAddress)), start + NativeBattleEffect303Profile.PacketBankBytes, NativeBattleEffect303Profile.PacketBankBytes, false);
        }
        if (effect == 298)
        {
            uint cursor = snapshot.NativeEffectMemory!.Effect298!.ReadDword(NativeBattleEffect298Profile.CursorGlobal);
            int start = cursor >= NativeBattleEffect298Profile.ArenaAddress + NativeBattleEffect298Profile.SecondPacketOffset ? NativeBattleEffect298Profile.SecondPacketOffset : NativeBattleEffect298Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleEffect298Profile.ArenaAddress)), start + NativeBattleEffect298Profile.PacketBankBytes, NativeBattleEffect298Profile.PacketBankBytes, false);
        }
        if (effect == 279)
        {
            uint cursor = snapshot.NativeEffectMemory!.MegaSpark279!.ReadDword(NativeBattleMegaSpark279Profile.CursorGlobal);
            int start = cursor >= NativeBattleMegaSpark279Profile.ArenaAddress + NativeBattleMegaSpark279Profile.SecondPacketOffset ? NativeBattleMegaSpark279Profile.SecondPacketOffset : NativeBattleMegaSpark279Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleMegaSpark279Profile.ArenaAddress)), start + NativeBattleMegaSpark279Profile.PacketBankBytes, NativeBattleMegaSpark279Profile.PacketBankBytes, false);
        }
        if (effect == 304)
        {
            uint cursor = snapshot.NativeEffectMemory!.BoomerangSword304!.ReadDword(NativeBattleBoomerangSword304Profile.CursorGlobal);
            int start = cursor >= NativeBattleBoomerangSword304Profile.ArenaAddress + NativeBattleBoomerangSword304Profile.SecondPacketOffset ? NativeBattleBoomerangSword304Profile.SecondPacketOffset : NativeBattleBoomerangSword304Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleBoomerangSword304Profile.ArenaAddress)), start + NativeBattleBoomerangSword304Profile.PacketBankBytes, NativeBattleBoomerangSword304Profile.PacketBankBytes, false);
        }
        if (effect == 302)
        {
            uint cursor = snapshot.NativeEffectMemory!.SnipeLaser302!.ReadDword(NativeBattleSnipeLaser302Profile.CursorGlobal);
            int start = cursor >= NativeBattleSnipeLaser302Profile.ArenaAddress + NativeBattleSnipeLaser302Profile.SecondPacketOffset ? NativeBattleSnipeLaser302Profile.SecondPacketOffset : NativeBattleSnipeLaser302Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleSnipeLaser302Profile.ArenaAddress)), start + NativeBattleSnipeLaser302Profile.PacketBankBytes, NativeBattleSnipeLaser302Profile.PacketBankBytes, false);
        }
        if (effect == 284)
        {
            uint cursor = snapshot.NativeEffectMemory!.EnemyMicroMissiles284!.ReadDword(NativeBattleEnemyMicroMissiles284Profile.CursorGlobal);
            int start = cursor >= NativeBattleEnemyMicroMissiles284Profile.ArenaAddress + NativeBattleEnemyMicroMissiles284Profile.SecondPacketOffset ? NativeBattleEnemyMicroMissiles284Profile.SecondPacketOffset : NativeBattleEnemyMicroMissiles284Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleEnemyMicroMissiles284Profile.ArenaAddress)), start + NativeBattleEnemyMicroMissiles284Profile.PacketBankBytes, NativeBattleEnemyMicroMissiles284Profile.PacketBankBytes, false);
        }
        if (effect == 286)
        {
            uint cursor = snapshot.NativeEffectMemory!.MiniPulseCannon286!.ReadDword(NativeBattleMiniPulseCannon286Profile.CursorGlobal);
            int start = cursor >= NativeBattleMiniPulseCannon286Profile.ArenaAddress + NativeBattleMiniPulseCannon286Profile.SecondPacketOffset ? NativeBattleMiniPulseCannon286Profile.SecondPacketOffset : NativeBattleMiniPulseCannon286Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleMiniPulseCannon286Profile.ArenaAddress)), start + NativeBattleMiniPulseCannon286Profile.PacketBankBytes, NativeBattleMiniPulseCannon286Profile.PacketBankBytes, false);
        }
        if (effect == 282)
        {
            uint cursor = snapshot.NativeEffectMemory!.EvilEye282!.ReadDword(NativeBattleEvilEye282Profile.CursorGlobal);
            int start = cursor >= NativeBattleEvilEye282Profile.ArenaAddress + NativeBattleEvilEye282Profile.SecondPacketOffset ? NativeBattleEvilEye282Profile.SecondPacketOffset : NativeBattleEvilEye282Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleEvilEye282Profile.ArenaAddress)), start + NativeBattleEvilEye282Profile.PacketBankBytes, NativeBattleEvilEye282Profile.PacketBankBytes, false);
        }
        if (effect == 281)
        {
            uint cursor = snapshot.NativeEffectMemory!.Shotgun281!.ReadDword(NativeBattleShotgun281Profile.CursorGlobal);
            int start = cursor >= NativeBattleShotgun281Profile.ArenaAddress + NativeBattleShotgun281Profile.SecondPacketOffset ? NativeBattleShotgun281Profile.SecondPacketOffset : NativeBattleShotgun281Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleShotgun281Profile.ArenaAddress)), start + NativeBattleShotgun281Profile.PacketBankBytes, NativeBattleShotgun281Profile.PacketBankBytes, false);
        }
        if (effect == 214)
        {
            uint cursor = snapshot.NativeEffectMemory!.Gravija214!.ReadDword(NativeBattleGravija214Profile.CursorGlobal);
            int start = cursor >= NativeBattleGravija214Profile.ArenaAddress + NativeBattleGravija214Profile.SecondPacketOffset ? NativeBattleGravija214Profile.SecondPacketOffset : NativeBattleGravija214Profile.FirstPacketOffset;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleGravija214Profile.ArenaAddress)), start + NativeBattleGravija214Profile.PacketBankBytes, NativeBattleGravija214Profile.PacketBankBytes, false);
        }
        if (effect == 44)
        {
            NativeBattleJunk44State junk = snapshot.NativeEffectMemory!.Junk44!;
            uint cursor = junk.ReadDword(NativeBattleJunk44Profile.CursorGlobal);
            int start = cursor >= NativeBattleJunk44Profile.ArenaAddress + 0x112D4u ? 0x112D4 : 0x12D4;
            return new(effect, 0, cursor == 0 ? null : checked((int)(cursor - NativeBattleJunk44Profile.ArenaAddress)), start + 0x10000, 0x10000, false);
        }
        if (effect == 45)
        {
            NativeBattleStare45State stare = snapshot.NativeEffectMemory!.Stare45!;
            uint source = stare.ReadDword(NativeBattleStare45Profile.InitializerGlobal);
            uint cursor = stare.ReadDword(NativeBattleStare45Profile.PrimaryCursorGlobal);
            uint limit = stare.ReadDword(NativeBattleStare45Profile.SecondaryCursorGlobal);
            if (source == 0 || cursor == 0 || limit == 0 || snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor == 0)
            {
                return new(effect, 0, null, null, 0xC000, false);
            }
            return new(effect, checked((int)(source - 0x20DFAB8u)), checked((int)(cursor - 0x20DFAB8u)), checked((int)(limit - 0x20DFAB8u)), 0xC000, false);
        }
        if (effect == 46)
        {
            NativeBattleSigh46State sigh = snapshot.NativeEffectMemory!.Sigh46!;
            uint source = sigh.ReadDword(NativeBattleSigh46Profile.InitializerGlobal);
            uint cursor = sigh.ReadDword(NativeBattleSigh46Profile.PrimaryCursorGlobal);
            uint limit = sigh.ReadDword(NativeBattleSigh46Profile.SecondaryCursorGlobal);
            if (source == 0 || cursor == 0 || limit == 0 || snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor == 0)
            {
                return new(effect, 0, null, null, 0x6000, false);
            }
            return new(effect, checked((int)(source - 0x20DFAB8u)), checked((int)(cursor - 0x20DFAB8u)), checked((int)(limit - 0x20DFAB8u)), 0x6000, false);
        }
        if (effect == 48)
        {
            NativeBattleMagmaBreath48State magma = snapshot.NativeEffectMemory!.MagmaBreath48!;
            uint source = magma.ReadDword(NativeBattleMagmaBreath48Profile.InitializerGlobal);
            uint cursor = magma.ReadDword(NativeBattleMagmaBreath48Profile.PrimaryCursorGlobal);
            uint limit = magma.ReadDword(NativeBattleMagmaBreath48Profile.SecondaryCursorGlobal);
            if (source == 0 || cursor == 0 || limit == 0 || snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor == 0)
            {
                return new(effect, 0, null, null, 0x6000, false);
            }
            return new(effect, checked((int)(source - 0x20DFAB8u)), checked((int)(cursor - 0x20DFAB8u)), checked((int)(limit - 0x20DFAB8u)), 0x6000, false);
        }
        if (effect == 49)
        {
            NativeBattleResonance49State state = snapshot.NativeEffectMemory!.Resonance49!;
            uint source = state.ReadDword(NativeBattleResonance49Profile.InitializerGlobal);
            uint cursor = state.ReadDword(NativeBattleResonance49Profile.PrimaryCursorGlobal);
            uint limit = state.ReadDword(NativeBattleResonance49Profile.SecondaryCursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.FirstOrDefault()?.Slots.FirstOrDefault(task => (task.Flags & 1) != 0);
            if (source == 0 || cursor == 0 || limit == 0 || root is null || root.ReadByte(0x29) >= 7 || snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor == 0)
            {
                return new(effect, 0, null, null, 0xC000, false);
            }
            int baseOffset = checked((int)(source - 0x20DFAB8u));
            int end = checked((int)(cursor - 0x20DFAB8u));
            int boundary = checked((int)(limit - 0x20DFAB8u));
            Assert.InRange(boundary - 0xC000, baseOffset, baseOffset + 0xC000);
            Assert.InRange(end, boundary - 0xC000, boundary);
            return new(effect, baseOffset, end, boundary, 0xC000, false);
        }
        if (effect == 129)
        {
            NativeBattleBreath129State state = snapshot.NativeEffectMemory!.Breath129 ?? throw new InvalidDataException("Breath requires its actual current mutable source and arena owners.");
            uint working = state.ReadDword(NativeBattleBreath129Profile.WorkingGlobal);
            uint cursor = state.ReadDword(NativeBattleBreath129Profile.CursorGlobal);
            if (working == 0 || cursor == 0)
            {
                return new(effect, NativeBattleBreath129Profile.FirstPacketOffset, null, null, NativeBattleBreath129Profile.PacketBankBytes, false);
            }
            int source = checked((int)(working - 0x20DFAB8u));
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => task.Callback == NativeBattleBreath129Profile.RootCallback);
            int start = source + (root.ReadDword(16) == 0 ? NativeBattleBreath129Profile.FirstPacketOffset : NativeBattleBreath129Profile.SecondPacketOffset);
            return new(effect, source + NativeBattleBreath129Profile.FirstPacketOffset, checked((int)(cursor - 0x20DFAB8u)), start + NativeBattleBreath129Profile.PacketBankBytes, NativeBattleBreath129Profile.PacketBankBytes, false);
        }
        if (effect == 139)
        {
            NativeBattleSonic139State state = snapshot.NativeEffectMemory!.Sonic139 ?? throw new InvalidDataException("Sonic Wave requires its current source and arena owners.");
            uint working = state.ReadDword(NativeBattleSonic139Profile.WorkingGlobal);
            uint cursor = state.ReadDword(NativeBattleSonic139Profile.CursorGlobal);
            if (working == 0 || cursor == 0)
            {
                return new(effect, NativeBattleSonic139Profile.FirstPacketOffset, null, null, NativeBattleSonic139Profile.PacketBankBytes, false);
            }
            int source = checked((int)(working - 0x20DFAB8u));
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => task.Callback == NativeBattleSonic139Profile.RootCallback);
            int start = source + (root.ReadDword(16) == 0 ? NativeBattleSonic139Profile.FirstPacketOffset : NativeBattleSonic139Profile.SecondPacketOffset);
            return new(effect, source + NativeBattleSonic139Profile.FirstPacketOffset, checked((int)(cursor - 0x20DFAB8u)), start + NativeBattleSonic139Profile.PacketBankBytes, NativeBattleSonic139Profile.PacketBankBytes, false);
        }
        if (effect == 170)
        {
            NativeBattleAcid170State state = snapshot.NativeEffectMemory!.Acid170 ?? throw new InvalidDataException("Acid170 requires its scalar and COMMON arena owner.");
            uint cursor = state.ReadDword(NativeBattleAcid170Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAcid170Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAcid170Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleAcid170Profile.PacketStep, false);
            }
            Assert.Equal(NativeBattleAcid170Profile.ArenaAddress, state.ReadDword(NativeBattleAcid170Profile.WorkingGlobal));
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleAcid170Profile.PacketStep : 0;
            int current = checked((int)(cursor - NativeBattleAcid170Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattleAcid170Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleAcid170Profile.PacketStep, NativeBattleAcid170Profile.PacketStep, false);
        }
        if (effect == 171)
        {
            NativeBattlePoisonGas171State state = snapshot.NativeEffectMemory!.PoisonGas171 ?? throw new InvalidDataException("PoisonGas171 requires its scalar and COMMON arena owner.");
            uint cursor = state.ReadDword(NativeBattlePoisonGas171Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattlePoisonGas171Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattlePoisonGas171Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattlePoisonGas171Profile.PacketStep, false);
            }
            Assert.Equal(NativeBattlePoisonGas171Profile.ArenaAddress, state.ReadDword(NativeBattlePoisonGas171Profile.WorkingGlobal));
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattlePoisonGas171Profile.PacketStep : 0;
            int current = checked((int)(cursor - NativeBattlePoisonGas171Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattlePoisonGas171Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattlePoisonGas171Profile.PacketStep, NativeBattlePoisonGas171Profile.PacketStep, false);
        }
        if (effect == 181)
        {
            NativeBattleAdditional181State state = snapshot.NativeEffectMemory!.Additional181 ?? throw new InvalidDataException("Additional181 requires its scalar and COMMON arena owner.");
            uint cursor = state.ReadDword(NativeBattleAdditional181Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAdditional181Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional181Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleAdditional181Profile.PacketStep, false);
            }
            Assert.Equal(NativeBattleAdditional181Profile.ArenaAddress, state.ReadDword(NativeBattleAdditional181Profile.WorkingGlobal));
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleAdditional181Profile.PacketStep : 0;
            int current = checked((int)(cursor - NativeBattleAdditional181Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattleAdditional181Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleAdditional181Profile.PacketStep, NativeBattleAdditional181Profile.PacketStep, false);
        }
        if (effect == 182)
        {
            NativeBattleAdditional182State state = snapshot.NativeEffectMemory!.Additional182 ?? throw new InvalidDataException("Additional requires its actual scalar and COMMON arena owners.");
            uint cursor = state.ReadDword(NativeBattleAdditional182Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAdditional182Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleAdditional182Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleAdditional182Profile.PacketStep, false);
            }
            Assert.Equal(NativeBattleAdditional182Profile.ArenaAddress, state.ReadDword(NativeBattleAdditional182Profile.WorkingGlobal));
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleAdditional182Profile.PacketStep : 0;
            int current = checked((int)(cursor - NativeBattleAdditional182Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattleAdditional182Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleAdditional182Profile.PacketStep, NativeBattleAdditional182Profile.PacketStep, false);
        }
        if (effect == 186)
        {
            NativeBattleBlaster186State state = snapshot.NativeEffectMemory!.Blaster186 ?? throw new InvalidDataException("Blaster requires its retained scalar and mutable frame owner.");
            uint cursor = state.ReadDword(NativeBattleBlaster186Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBlaster186Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBlaster186Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleBlaster186Profile.PacketStep, true);
            }
            Assert.Equal(NativeBattleBlaster186Profile.ArenaAddress, state.ReadDword(NativeBattleBlaster186Profile.WorkingGlobal));
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleBlaster186Profile.PacketStep : 0;
            int current = checked((int)(cursor - NativeBattleBlaster186Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattleBlaster186Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleBlaster186Profile.PacketStep, NativeBattleBlaster186Profile.PacketStep, true);
        }
        if (effect == 151)
        {
            NativeBattleCannonBlow151State state = snapshot.NativeEffectMemory!.CannonBlow151 ?? throw new InvalidDataException("Cannon Blow requires its actual scalar owner and COMMON arena.");
            uint cursor = state.ReadDword(NativeBattleCannonBlow151Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleCannonBlow151Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleCannonBlow151Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleCannonBlow151Profile.PacketStep, false);
            }
            Assert.Equal(NativeBattleCannonBlow151Profile.ArenaAddress, state.ReadDword(NativeBattleCannonBlow151Profile.WorkingGlobal));
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleCannonBlow151Profile.PacketStep : 0;
            int current = checked((int)(cursor - NativeBattleCannonBlow151Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattleCannonBlow151Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleCannonBlow151Profile.PacketStep, NativeBattleCannonBlow151Profile.PacketStep, false);
        }
        if (effect == 12)
        {
            NativeBattleBladeShot12State state = snapshot.NativeEffectMemory!.BladeShot12 ?? throw new InvalidDataException("Morph requires its actual private physical packet owner.");
            uint cursor = state.ReadDword(NativeBattleBladeShot12Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBladeShot12Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBladeShot12Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleBladeShot12Profile.PacketBankBytes, false, "2546100");
            }
            int start = root.ReadDword(16) != 0 ? NativeBattleBladeShot12Profile.PacketBankBytes : 0;
            int current = checked((int)(cursor - NativeBattleBladeShot12Profile.PacketBase));
            Assert.InRange(current, start, start + NativeBattleBladeShot12Profile.PacketBankBytes);
            return new(effect, 0, current, start + NativeBattleBladeShot12Profile.PacketBankBytes, NativeBattleBladeShot12Profile.PacketBankBytes, false, "2546100");
        }
        if (effect == 172)
        {
            NativeBattleMorph172State state = snapshot.NativeEffectMemory!.Morph172 ?? throw new InvalidDataException("Morph requires its actual private physical packet owner.");
            uint cursor = state.ReadDword(NativeBattleMorph172Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleMorph172Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMorph172Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleMorph172Profile.PacketBankBytes, false, "239D6FC");
            }
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleMorph172Profile.PacketBankBytes : 0;
            int current = checked((int)(cursor - NativeBattleMorph172Profile.PacketBase));
            Assert.InRange(current, start, start + NativeBattleMorph172Profile.PacketBankBytes);
            return new(effect, 0, current, start + NativeBattleMorph172Profile.PacketBankBytes, NativeBattleMorph172Profile.PacketBankBytes, false, "239D6FC");
        }
        if (effect == 190)
        {
            NativeBattleChainGun190State state = snapshot.NativeEffectMemory!.ChainGun190 ?? throw new InvalidDataException("Chain Gun requires its actual private physical packet owner.");
            uint cursor = state.ReadDword(NativeBattleChainGun190Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleChainGun190Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleChainGun190Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleChainGun190Profile.PacketBankBytes, false, "2283F40");
            }
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleChainGun190Profile.PacketBankBytes : 0;
            int current = checked((int)(cursor - NativeBattleChainGun190Profile.PacketBase));
            Assert.InRange(current, start, start + NativeBattleChainGun190Profile.PacketBankBytes);
            return new(effect, 0, current, start + NativeBattleChainGun190Profile.PacketBankBytes, NativeBattleChainGun190Profile.PacketBankBytes, false, "2283F40");
        }
        if (effect == 150)
        {
            NativeBattleGatlingGun150State state = snapshot.NativeEffectMemory!.GatlingGun150 ?? throw new InvalidDataException("Gatling Gun requires its actual fixed packet owner.");
            uint cursor = state.ReadDword(NativeBattleGatlingGun150Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleGatlingGun150Profile.PacketBankBytes, false, "2451E80");
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => task.Callback == NativeBattleGatlingGun150Profile.RootCallback);
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleGatlingGun150Profile.PacketBankBytes : 0;
            Assert.InRange(cursor, NativeBattleGatlingGun150Profile.PacketBase + (uint)start, NativeBattleGatlingGun150Profile.PacketBase + (uint)(start + NativeBattleGatlingGun150Profile.PacketBankBytes));
            return new(effect, 0, checked((int)(cursor - NativeBattleGatlingGun150Profile.PacketBase)), start + NativeBattleGatlingGun150Profile.PacketBankBytes, NativeBattleGatlingGun150Profile.PacketBankBytes, false, "2451E80");
        }
        if (effect == 183)
        {
            NativeBattleEffect183State retained = snapshot.NativeEffectMemory!.Effect183 ?? throw new InvalidDataException("Effect183 requires its original fixed packet owner.");
            uint cursor = retained.ReadDword(NativeBattleEffect183Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleEffect183Profile.PacketBankBytes, false, "22F421C");
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => task.Callback == NativeBattleEffect183Profile.RootCallback);
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleEffect183Profile.PacketBankBytes : 0;
            return new(effect, 0, checked((int)(cursor - NativeBattleEffect183Profile.PacketBase)), start + NativeBattleEffect183Profile.PacketBankBytes, NativeBattleEffect183Profile.PacketBankBytes, false, "22F421C");
        }
        if (effect == 131)
        {
            NativeBattleBadBreath131State breath131State = snapshot.NativeEffectMemory!.BadBreath131 ?? throw new InvalidDataException("Bad Breath requires its actual module owner.");
            uint breath131Working = breath131State.ReadDword(NativeBattleBadBreath131Profile.WorkingGlobal);
            int breath131Source = checked((int)(breath131Working - NativeBattleBadBreath131Profile.ArenaAddress)) + NativeBattleBadBreath131Profile.FirstPacketOffset;
            uint breath131Cursor = breath131State.ReadDword(NativeBattleBadBreath131Profile.CursorGlobal);
            NativeBattleEffectTaskPool? breath131RootPool = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBadBreath131Profile.RootDescriptor);
            NativeBattleModulePoolTask? breath131Root = breath131RootPool?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath131Profile.RootCallback);
            if (breath131Root is null || breath131Cursor == 0)
            {
                return new(effect, breath131Source, null, null, NativeBattleBadBreath131Profile.PacketBankBytes, false);
            }
            uint breath131Start = breath131Working + (uint)(breath131Root.ReadDword(16) == 0 ? NativeBattleBadBreath131Profile.FirstPacketOffset : NativeBattleBadBreath131Profile.SecondPacketOffset);
            Assert.InRange(breath131Cursor, breath131Start, breath131Start + NativeBattleBadBreath131Profile.PacketBankBytes);
            return new(effect, breath131Source, checked((int)(breath131Cursor - NativeBattleBadBreath131Profile.ArenaAddress)), checked((int)(breath131Start + NativeBattleBadBreath131Profile.PacketBankBytes - NativeBattleBadBreath131Profile.ArenaAddress)), NativeBattleBadBreath131Profile.PacketBankBytes, false);
        }
        if (effect == 132)
        {
            NativeBattleDisolvingAcid132State acid132 = snapshot.NativeEffectMemory!.DisolvingAcid132 ?? throw new InvalidDataException("Disolving Acid requires its current retained sources.");
            uint working = acid132.ReadDword(NativeBattleDisolvingAcid132Profile.WorkingGlobal);
            int source = checked((int)(working - 0x20DFAB8u)) + NativeBattleDisolvingAcid132Profile.FirstPacketOffset;
            uint cursor = acid132.ReadDword(NativeBattleDisolvingAcid132Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleDisolvingAcid132Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleDisolvingAcid132Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleDisolvingAcid132Profile.PacketStep, false);
            }
            uint start = working + (uint)(root.ReadDword(16) == 0 ? NativeBattleDisolvingAcid132Profile.FirstPacketOffset : NativeBattleDisolvingAcid132Profile.SecondPacketOffset);
            Assert.InRange(cursor, start, start + NativeBattleDisolvingAcid132Profile.PacketStep);
            return new(effect, source, checked((int)(cursor - 0x20DFAB8u)), checked((int)(start + NativeBattleDisolvingAcid132Profile.PacketStep - 0x20DFAB8u)), NativeBattleDisolvingAcid132Profile.PacketStep, false);
        }
        if (effect == 157)
        {
            NativeBattleBreath157State breath = snapshot.NativeEffectMemory!.Breath157 ?? throw new InvalidDataException("Breath requires its actual scalar carrier.");
            uint cursor = breath.ReadDword(NativeBattleBreath157Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBreath157Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBreath157Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleBreath157Profile.PacketStep, false);
            }
            int start = (root.ReadWord(12) & 1) != 0 ? 0 : NativeBattleBreath157Profile.PacketStep;
            int current = checked((int)(cursor - NativeBattleBreath157Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattleBreath157Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleBreath157Profile.PacketStep, NativeBattleBreath157Profile.PacketStep, false);
        }
        if (effect == 156)
        {
            NativeBattleWildCannonBlow156State cannon = snapshot.NativeEffectMemory!.WildCannonBlow156 ?? throw new InvalidDataException("Wild Cannon Blow requires its actual scalar carrier.");
            uint cursor = cannon.ReadDword(NativeBattleWildCannonBlow156Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleWildCannonBlow156Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleWildCannonBlow156Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleWildCannonBlow156Profile.PacketStep, false);
            }
            int start = (root.ReadWord(12) & 1) != 0 ? 0 : NativeBattleWildCannonBlow156Profile.PacketStep;
            int current = checked((int)(cursor - NativeBattleWildCannonBlow156Profile.ArenaAddress));
            Assert.InRange(current, start, start + NativeBattleWildCannonBlow156Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleWildCannonBlow156Profile.PacketStep, NativeBattleWildCannonBlow156Profile.PacketStep, false);
        }
        if (effect == 155)
        {
            NativeBattleSandStorm155State sand = snapshot.NativeEffectMemory!.SandStorm155 ?? throw new InvalidDataException("Sand Storm requires its actual module carrier.");
            uint cursor = sand.ReadDword(NativeBattleSandStorm155Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSandStorm155Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSandStorm155Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleSandStorm155Profile.PacketStep, false, NativeBattleSandStorm155Profile.PacketBankId);
            }
            int start = (root.ReadWord(12) & 1) != 0 ? 0 : NativeBattleSandStorm155Profile.PacketStep;
            int current = checked((int)(cursor - NativeBattleSandStorm155Profile.PacketBase));
            Assert.InRange(current, start, start + NativeBattleSandStorm155Profile.PacketStep);
            return new(effect, 0, current, start + NativeBattleSandStorm155Profile.PacketStep, NativeBattleSandStorm155Profile.PacketStep, false, NativeBattleSandStorm155Profile.PacketBankId);
        }
        if (effect == 128)
        {
            NativeBattleGastricJuice128State gastric128 = snapshot.NativeEffectMemory!.GastricJuice128 ?? throw new InvalidDataException("Gastric Juice requires its current retained sources.");
            uint working = gastric128.ReadDword(NativeBattleGastricJuice128Profile.WorkingGlobal);
            int source = checked((int)(working - 0x20DFAB8u)) + NativeBattleGastricJuice128Profile.FirstPacketOffset;
            uint cursor = gastric128.ReadDword(NativeBattleGastricJuice128Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleGastricJuice128Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleGastricJuice128Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleGastricJuice128Profile.PacketStep, true);
            }
            uint start = working + (uint)(root.ReadDword(16) == 0 ? NativeBattleGastricJuice128Profile.FirstPacketOffset : NativeBattleGastricJuice128Profile.SecondPacketOffset);
            Assert.InRange(cursor, start, start + NativeBattleGastricJuice128Profile.PacketStep);
            return new(effect, source, checked((int)(cursor - 0x20DFAB8u)), checked((int)(start + NativeBattleGastricJuice128Profile.PacketStep - 0x20DFAB8u)), NativeBattleGastricJuice128Profile.PacketStep, true);
        }
        if (effect == 136)
        {
            NativeBattleOilShot136State oilShot = snapshot.NativeEffectMemory!.OilShot136 ?? throw new InvalidDataException("Oil Shot requires its current retained sources.");
            uint working = oilShot.ReadDword(NativeBattleOilShot136Profile.WorkingGlobal);
            int source = checked((int)(working - 0x20DFAB8u)) + NativeBattleOilShot136Profile.FirstPacketOffset;
            uint cursor = oilShot.ReadDword(NativeBattleOilShot136Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleOilShot136Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilShot136Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleOilShot136Profile.PacketStep, false);
            }
            uint start = working + (uint)(root.ReadDword(16) == 0 ? NativeBattleOilShot136Profile.FirstPacketOffset : NativeBattleOilShot136Profile.SecondPacketOffset);
            Assert.InRange(cursor, start, start + NativeBattleOilShot136Profile.PacketStep);
            return new(effect, source, checked((int)(cursor - 0x20DFAB8u)), checked((int)(start + NativeBattleOilShot136Profile.PacketStep - 0x20DFAB8u)), NativeBattleOilShot136Profile.PacketStep, false);
        }
        if (effect == 138)
        {
            NativeBattleSaliva138State saliva = snapshot.NativeEffectMemory!.Saliva138 ?? throw new InvalidDataException("Saliva requires its current retained sources.");
            uint working = saliva.ReadDword(NativeBattleSaliva138Profile.WorkingGlobal);
            int source = checked((int)(working - 0x20DFAB8u)) + NativeBattleSaliva138Profile.FirstPacketOffset;
            uint cursor = saliva.ReadDword(NativeBattleSaliva138Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSaliva138Profile.RootDescriptor)?.Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleSaliva138Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleSaliva138Profile.PacketStep, false);
            }
            uint start = working + (uint)(root.ReadDword(16) == 0 ? NativeBattleSaliva138Profile.FirstPacketOffset : NativeBattleSaliva138Profile.SecondPacketOffset);
            Assert.InRange(cursor, start, start + NativeBattleSaliva138Profile.PacketStep);
            return new(effect, source, checked((int)(cursor - 0x20DFAB8u)), checked((int)(start + NativeBattleSaliva138Profile.PacketStep - 0x20DFAB8u)), NativeBattleSaliva138Profile.PacketStep, false);
        }
        if (effect == 137)
        {
            NativeBattleOilBlast137State oil = snapshot.NativeEffectMemory!.OilBlast137 ?? throw new InvalidDataException("Oil Blast requires its retained module globals.");
            uint working = oil.ReadDword(NativeBattleOilBlast137Profile.WorkingGlobal);
            int source = checked((int)(working - 0x20DFAB8u)) + NativeBattleOilBlast137Profile.FirstPacketOffset;
            uint cursor = oil.ReadDword(NativeBattleOilBlast137Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleOilBlast137Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleOilBlast137Profile.PacketStep, false);
            }
            uint start = working + (uint)(root.ReadDword(16) == 0 ? NativeBattleOilBlast137Profile.FirstPacketOffset : NativeBattleOilBlast137Profile.SecondPacketOffset);
            Assert.InRange(cursor, start, start + NativeBattleOilBlast137Profile.PacketStep);
            return new(effect, source, checked((int)(cursor - 0x20DFAB8u)), checked((int)(start + NativeBattleOilBlast137Profile.PacketStep - 0x20DFAB8u)), NativeBattleOilBlast137Profile.PacketStep, false);
        }
        if (effect == 158)
        {
            NativeBattleMeltEye158State meltEyeState = snapshot.NativeEffectMemory!.MeltEye158 ?? throw new InvalidDataException("Melt-Eye requires its actual fixed packet owner.");
            uint meltEyeCursor = meltEyeState.ReadDword(NativeBattleMeltEye158Profile.CursorGlobal);
            NativeBattleModulePoolTask? meltEyeRoot = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleMeltEye158Profile.RootCallback);
            if (meltEyeRoot is null || meltEyeCursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleMeltEye158Profile.PacketBankBytes, false, "23DB5BC");
            }
            int meltEyeStart = (meltEyeRoot.ReadWord(12) & 1) == 0 ? NativeBattleMeltEye158Profile.PacketBankBytes : 0;
            Assert.InRange(meltEyeCursor, NativeBattleMeltEye158Profile.PacketBase + (uint)meltEyeStart, NativeBattleMeltEye158Profile.PacketBase + (uint)(meltEyeStart + NativeBattleMeltEye158Profile.PacketBankBytes));
            return new(effect, 0, checked((int)(meltEyeCursor - NativeBattleMeltEye158Profile.PacketBase)), meltEyeStart + NativeBattleMeltEye158Profile.PacketBankBytes, NativeBattleMeltEye158Profile.PacketBankBytes, false, "23DB5BC");
        }
        if (effect == 217)
        {
            NativeBattleEnergyBomber217State energyBomberState = snapshot.NativeEffectMemory!.EnergyBomber217 ?? throw new InvalidDataException("Energy Bomber requires its retained globals.");
            uint energyBomberWorking = energyBomberState.ReadDword(NativeBattleEnergyBomber217Profile.WorkingGlobal);
            int energyBomberSource = checked((int)(energyBomberWorking - NativeBattleEnergyBomber217Profile.ArenaAddress)) + NativeBattleEnergyBomber217Profile.FirstPacketOffset;
            uint energyBomberCursor = energyBomberState.ReadDword(NativeBattleEnergyBomber217Profile.CursorGlobal);
            NativeBattleModulePoolTask? energyBomberRoot = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEnergyBomber217Profile.RootCallback);
            if (energyBomberRoot is null)
            {
                return new(effect, energyBomberSource, null, null, NativeBattleEnergyBomber217Profile.PacketStep, false);
            }
            uint energyBomberStart = energyBomberWorking + (uint)(energyBomberRoot.ReadDword(0x10) == 0 ? NativeBattleEnergyBomber217Profile.FirstPacketOffset : NativeBattleEnergyBomber217Profile.SecondPacketOffset);
            Assert.InRange(energyBomberCursor, energyBomberStart, energyBomberStart + NativeBattleEnergyBomber217Profile.PacketStep);
            return new(effect, energyBomberSource, checked((int)(energyBomberCursor - NativeBattleEnergyBomber217Profile.ArenaAddress)), checked((int)(energyBomberStart + NativeBattleEnergyBomber217Profile.PacketStep - NativeBattleEnergyBomber217Profile.ArenaAddress)), NativeBattleEnergyBomber217Profile.PacketStep, false);
        }
        if (effect == 101)
        {
            NativeBattleEffect101State effect101State = snapshot.NativeEffectMemory!.Effect101 ?? throw new InvalidDataException("Effect101 requires its retained globals.");
            uint effect101Working = effect101State.ReadDword(NativeBattleEffect101Profile.WorkingGlobal);
            int effect101Source = checked((int)(effect101Working - NativeBattleEffect101Profile.ArenaAddress)) + NativeBattleEffect101Profile.FirstPacketOffset;
            uint effect101Cursor = effect101State.ReadDword(NativeBattleEffect101Profile.CursorGlobal);
            NativeBattleModulePoolTask? effect101Root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEffect101Profile.RootCallback);
            if (effect101Root is null)
            {
                return new(effect, effect101Source, null, null, NativeBattleEffect101Profile.PacketStep, false);
            }
            uint effect101Start = effect101Working + (uint)(effect101Root.ReadDword(0x10) == 0 ? NativeBattleEffect101Profile.FirstPacketOffset : NativeBattleEffect101Profile.SecondPacketOffset);
            Assert.InRange(effect101Cursor, effect101Start, effect101Start + NativeBattleEffect101Profile.PacketStep);
            return new(effect, effect101Source, checked((int)(effect101Cursor - NativeBattleEffect101Profile.ArenaAddress)), checked((int)(effect101Start + NativeBattleEffect101Profile.PacketStep - NativeBattleEffect101Profile.ArenaAddress)), NativeBattleEffect101Profile.PacketStep, false);
        }
        if (effect == 134)
        {
            NativeBattleBeamLaser134State beamLaserState = snapshot.NativeEffectMemory!.BeamLaser134 ?? throw new InvalidDataException("Beam Laser requires its actual retained globals.");
            uint beamLaserWorking = beamLaserState.ReadDword(NativeBattleBeamLaser134Profile.WorkingGlobal);
            int beamLaserSource = checked((int)(beamLaserWorking - NativeBattleBeamLaser134Profile.ArenaAddress)) + NativeBattleBeamLaser134Profile.FirstPacketOffset;
            uint beamLaserCursor = beamLaserState.ReadDword(NativeBattleBeamLaser134Profile.CursorGlobal);
            NativeBattleModulePoolTask? beamLaserRoot = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleBeamLaser134Profile.RootCallback);
            if (beamLaserRoot is null || beamLaserCursor == 0)
            {
                return new(effect, beamLaserSource, null, null, NativeBattleBeamLaser134Profile.PacketStep, false);
            }
            uint beamLaserStart = beamLaserWorking + (uint)(beamLaserRoot.ReadDword(0x10) == 0 ? NativeBattleBeamLaser134Profile.FirstPacketOffset : NativeBattleBeamLaser134Profile.SecondPacketOffset);
            Assert.InRange(beamLaserCursor, beamLaserStart, beamLaserStart + NativeBattleBeamLaser134Profile.PacketStep);
            return new(effect, beamLaserSource, checked((int)(beamLaserCursor - NativeBattleBeamLaser134Profile.ArenaAddress)), checked((int)(beamLaserStart + NativeBattleBeamLaser134Profile.PacketStep - NativeBattleBeamLaser134Profile.ArenaAddress)), NativeBattleBeamLaser134Profile.PacketStep, false);
        }
        if (effect == 133)
        {
            NativeBattleHypnotize133State hypnotizeState = snapshot.NativeEffectMemory!.Hypnotize133 ?? throw new InvalidDataException("Hypnotize requires its actual retained globals.");
            uint hypnotizeWorking = hypnotizeState.ReadDword(NativeBattleHypnotize133Profile.WorkingGlobal);
            int hypnotizeSource = checked((int)(hypnotizeWorking - NativeBattleHypnotize133Profile.ArenaAddress)) + NativeBattleHypnotize133Profile.FirstPacketOffset;
            uint hypnotizeCursor = hypnotizeState.ReadDword(NativeBattleHypnotize133Profile.CursorGlobal);
            NativeBattleModulePoolTask? hypnotizeRoot = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleHypnotize133Profile.RootCallback);
            if (hypnotizeRoot is null || hypnotizeCursor == 0)
            {
                return new(effect, hypnotizeSource, null, null, NativeBattleHypnotize133Profile.PacketStep, false);
            }
            uint hypnotizeStart = hypnotizeWorking + (uint)(hypnotizeRoot.ReadDword(0x10) == 0 ? NativeBattleHypnotize133Profile.FirstPacketOffset : NativeBattleHypnotize133Profile.SecondPacketOffset);
            Assert.InRange(hypnotizeCursor, hypnotizeStart, hypnotizeStart + NativeBattleHypnotize133Profile.PacketStep);
            return new(effect, hypnotizeSource, checked((int)(hypnotizeCursor - NativeBattleHypnotize133Profile.ArenaAddress)), checked((int)(hypnotizeStart + NativeBattleHypnotize133Profile.PacketStep - NativeBattleHypnotize133Profile.ArenaAddress)), NativeBattleHypnotize133Profile.PacketStep, false);
        }
        if (effect is 177 or 178)
        {
            NativeBattleNeedlesFamilyState needlesState = effect == 177 ? snapshot.NativeEffectMemory!.ThousandNeedles177! : snapshot.NativeEffectMemory!.TenThousandNeedles178!;
            NativeBattleNeedlesFamilyProfile needlesProfile = needlesState.Profile;
            NativeBattleModulePoolTask? needlesRoot = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == needlesProfile.RootCallback);
            uint needlesCursor = needlesState.ReadDword(needlesProfile.CursorGlobal);
            if (needlesRoot is null || needlesCursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleNeedlesFamilyProfile.PacketStep, false, needlesProfile.PacketBase.ToString("X"));
            }
            int needlesStart = (needlesRoot.ReadWord(12) & 1) == 0 ? NativeBattleNeedlesFamilyProfile.PacketStep : 0;
            int needlesEnd = checked((int)(needlesCursor - needlesProfile.PacketBase));
            Assert.InRange(needlesEnd, needlesStart, needlesStart + NativeBattleNeedlesFamilyProfile.PacketStep);
            return new(effect, 0, needlesEnd, needlesStart + NativeBattleNeedlesFamilyProfile.PacketStep, NativeBattleNeedlesFamilyProfile.PacketStep, false, needlesProfile.PacketBase.ToString("X"));
        }
        if (effect == 71)
        {
            NativeBattleLaserEye71State state = snapshot.NativeEffectMemory!.LaserEye71 ?? throw new InvalidDataException("Laser Eye requires its actual retained globals.");
            int source = checked((int)(state.ReadDword(NativeBattleLaserEye71Profile.InitializerGlobal) - NativeBattleLaserEye71Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleLaserEye71Profile.CursorGlobal);
            uint end = state.ReadDword(NativeBattleLaserEye71Profile.PacketEndGlobal);
            return cursor == 0 || end == 0 ? new(effect, source, null, null, 0x6000, false) : new(effect, source, checked((int)(cursor - NativeBattleLaserEye71Profile.ArenaAddress)), checked((int)(end - NativeBattleLaserEye71Profile.ArenaAddress)), 0x6000, false);
        }
        if (effect == 130)
        {
            NativeBattleEerieSoundWave130State eerieState = snapshot.NativeEffectMemory!.EerieSoundWave130 ?? throw new InvalidDataException("Eerie Sound Wave requires its actual retained globals.");
            uint eerieWorking = eerieState.ReadDword(NativeBattleEerieSoundWave130Profile.WorkingGlobal);
            int eerieSource = checked((int)(eerieWorking - NativeBattleEerieSoundWave130Profile.ArenaAddress)) + NativeBattleEerieSoundWave130Profile.FirstPacketOffset;
            uint eerieCursor = eerieState.ReadDword(NativeBattleEerieSoundWave130Profile.CursorGlobal);
            NativeBattleModulePoolTask? eerieRoot = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleEerieSoundWave130Profile.RootCallback);
            if (eerieRoot is null || eerieCursor == 0)
            {
                return new(effect, eerieSource, null, null, 0x18000, false);
            }
            uint eerieStart = checked(eerieWorking + (uint)(eerieRoot.ReadDword(16) == 0 ? NativeBattleEerieSoundWave130Profile.FirstPacketOffset : NativeBattleEerieSoundWave130Profile.SecondPacketOffset));
            Assert.InRange(eerieCursor, eerieStart, checked(eerieStart + 0x18000));
            return new(effect, eerieSource, checked((int)(eerieCursor - NativeBattleEerieSoundWave130Profile.ArenaAddress)), checked((int)(eerieStart + 0x18000 - NativeBattleEerieSoundWave130Profile.ArenaAddress)), 0x18000, false);
        }
        if (effect == 127)
        {
            NativeBattlePetrifyStare127State state = snapshot.NativeEffectMemory!.PetrifyStare127 ?? throw new InvalidDataException("Petrify Stare requires its actual retained globals.");
            uint working = state.ReadDword(NativeBattlePetrifyStare127Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattlePetrifyStare127Profile.ArenaAddress)) + NativeBattlePetrifyStare127Profile.FirstPacketOffset;
            uint cursor = state.ReadDword(NativeBattlePetrifyStare127Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattlePetrifyStare127Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, source, null, null, 0x18000, false);
            }
            uint start = checked(working + (uint)(root.ReadDword(16) == 0 ? NativeBattlePetrifyStare127Profile.FirstPacketOffset : NativeBattlePetrifyStare127Profile.SecondPacketOffset));
            Assert.InRange(cursor, start, checked(start + 0x18000));
            return new(effect, source, checked((int)(cursor - NativeBattlePetrifyStare127Profile.ArenaAddress)), checked((int)(start + 0x18000 - NativeBattlePetrifyStare127Profile.ArenaAddress)), 0x18000, false);
        }
        if (effect == 260)
        {
            NativeBattleShockwavePulsar260State state = snapshot.NativeEffectMemory!.ShockwavePulsar260 ?? throw new InvalidDataException("Shockwave Pulsar requires its native module globals.");
            uint working = state.ReadDword(NativeBattleShockwavePulsar260Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleShockwavePulsar260Profile.ArenaAddress)) + NativeBattleShockwavePulsar260Profile.FirstPacketOffset;
            uint primary = state.ReadDword(NativeBattleShockwavePulsar260Profile.PrimaryCursorGlobal);
            uint secondary = state.ReadDword(NativeBattleShockwavePulsar260Profile.SecondaryCursorGlobal);
            if (primary == 0 || secondary == 0 || snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor == 0)
            {
                return new(effect, source, null, null, NativeBattleShockwavePulsar260Profile.PacketBankBytes, true);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleShockwavePulsar260Profile.RootCallback);
            uint start = checked(working + (uint)(root.ReadByte(14) == 0 ? NativeBattleShockwavePulsar260Profile.FirstPacketOffset : NativeBattleShockwavePulsar260Profile.SecondPacketOffset));
            Assert.Equal(start + NativeBattleShockwavePulsar260Profile.PacketBankBytes, secondary);
            Assert.InRange(primary, start, secondary);
            return new(effect, source, checked((int)(primary - NativeBattleShockwavePulsar260Profile.ArenaAddress)), checked((int)(secondary - NativeBattleShockwavePulsar260Profile.ArenaAddress)), NativeBattleShockwavePulsar260Profile.PacketBankBytes, true);
        }
        if (effect == 307)
        {
            NativeBattleRayBomb307State state = snapshot.NativeEffectMemory!.RayBomb307 ?? throw new InvalidDataException("Blue Ray-Bomb requires its independent module state.");
            uint working = state.ReadDword(NativeBattleRayBomb307Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleRayBomb307Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleRayBomb307Profile.CursorGlobal);
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleRayBomb307Profile.RootCallback);
            if (root is null || cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleRayBomb307Profile.PacketBankLength, false);
            }
            int start = checked(source + ((root.ReadWord(12) & 1) == 0 ? NativeBattleRayBomb307Profile.PacketBankLength : 0));
            return new(effect, source, checked((int)(cursor - NativeBattleRayBomb307Profile.ArenaAddress)), start + NativeBattleRayBomb307Profile.PacketBankLength, NativeBattleRayBomb307Profile.PacketBankLength, false);
        }
        if (effect == 184)
        {
            NativeBattleUltraWaves184State state = snapshot.NativeEffectMemory!.UltraWaves184 ?? throw new InvalidDataException("Blue Ultra Waves requires its physical module drawing banks.");
            NativeBattleModulePoolTask? root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.SingleOrDefault(task => (task.Flags & 1) != 0 && task.Callback == NativeBattleUltraWaves184Profile.RootCallback);
            if (root is null)
            {
                return new(effect, 0, null, null, NativeBattleUltraWaves184Profile.PacketBankLength, true, "22DB3C4");
            }
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleUltraWaves184Profile.PacketBankLength : 0;
            int cursor = checked((int)(state.ReadCursor() - NativeBattleUltraWaves184Profile.PacketAddress));
            Assert.InRange(cursor, start, start + NativeBattleUltraWaves184Profile.PacketBankLength);
            return new(effect, 0, cursor, start + NativeBattleUltraWaves184Profile.PacketBankLength, NativeBattleUltraWaves184Profile.PacketBankLength, true, "22DB3C4");
        }
        if (effect == 135)
        {
            NativeBattleReflectBeam135State state = snapshot.NativeEffectMemory!.ReflectBeam135 ?? throw new InvalidDataException("Reflect Beam requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleReflectBeam135Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleReflectBeam135Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleReflectBeam135Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source + NativeBattleReflectBeam135Profile.FirstPacketOffset, null, null, NativeBattleReflectBeam135Profile.PacketBankBytes, false);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots[0];
            int start = checked(source + (root.ReadDword(16) == 0 ? NativeBattleReflectBeam135Profile.FirstPacketOffset : NativeBattleReflectBeam135Profile.SecondPacketOffset));
            return new(effect, source + NativeBattleReflectBeam135Profile.FirstPacketOffset, checked((int)(cursor - NativeBattleReflectBeam135Profile.ArenaAddress)), checked(start + NativeBattleReflectBeam135Profile.PacketBankBytes), NativeBattleReflectBeam135Profile.PacketBankBytes, false);
        }
        if (effect == 126)
        {
            NativeBattleElectricDischarge126State state = snapshot.NativeEffectMemory!.ElectricDischarge126 ?? throw new InvalidDataException("Electric Discharge requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleElectricDischarge126Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleElectricDischarge126Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleElectricDischarge126Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source + NativeBattleElectricDischarge126Profile.FirstPacketOffset, null, null, NativeBattleElectricDischarge126Profile.PacketBankBytes, false);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots[0];
            int start = checked(source + (root.ReadDword(16) == 0 ? NativeBattleElectricDischarge126Profile.FirstPacketOffset : NativeBattleElectricDischarge126Profile.SecondPacketOffset));
            return new(effect, source + NativeBattleElectricDischarge126Profile.FirstPacketOffset, checked((int)(cursor - NativeBattleElectricDischarge126Profile.ArenaAddress)), checked(start + NativeBattleElectricDischarge126Profile.PacketBankBytes), NativeBattleElectricDischarge126Profile.PacketBankBytes, false);
        }
        if (effect == 275)
        {
            NativeBattleElectrocute275State state = snapshot.NativeEffectMemory!.Electrocute275 ?? throw new InvalidDataException("Electrocute requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleElectrocute275Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleElectrocute275Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleElectrocute275Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source + NativeBattleElectrocute275Profile.FirstPacketOffset, null, null, NativeBattleElectrocute275Profile.PacketBankBytes, false);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots[0];
            int start = checked(source + (root.ReadDword(16) == 0 ? NativeBattleElectrocute275Profile.FirstPacketOffset : NativeBattleElectrocute275Profile.SecondPacketOffset));
            return new(effect, source + NativeBattleElectrocute275Profile.FirstPacketOffset, checked((int)(cursor - NativeBattleElectrocute275Profile.ArenaAddress)), checked(start + NativeBattleElectrocute275Profile.PacketBankBytes), NativeBattleElectrocute275Profile.PacketBankBytes, false);
        }
        if (effect == 317)
        {
            NativeBattleAcid317State state = snapshot.NativeEffectMemory!.Acid317 ?? throw new InvalidDataException("Acid requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleAcid317Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleAcid317Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleAcid317Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source + NativeBattleAcid317Profile.FirstPacketOffset, null, null, NativeBattleAcid317Profile.PacketBankBytes, true);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots[0];
            int start = checked(source + (root.ReadDword(16) == 0 ? NativeBattleAcid317Profile.FirstPacketOffset : NativeBattleAcid317Profile.SecondPacketOffset));
            return new(effect, source + NativeBattleAcid317Profile.FirstPacketOffset, checked((int)(cursor - NativeBattleAcid317Profile.ArenaAddress)), checked(start + NativeBattleAcid317Profile.PacketBankBytes), NativeBattleAcid317Profile.PacketBankBytes, true);
        }
        if (effect == 305)
        {
            NativeBattleGatlingGun305State state = snapshot.NativeEffectMemory!.GatlingGun305 ?? throw new InvalidDataException("Gatling Gun requires its retained module packet bank.");
            uint cursor = state.ReadDword(NativeBattleGatlingGun305Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, 0, null, null, NativeBattleGatlingGun305Profile.PacketBankBytes, false, "2247790");
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => task.Callback == NativeBattleGatlingGun305Profile.RootCallback);
            int start = (root.ReadWord(12) & 1) == 0 ? NativeBattleGatlingGun305Profile.PacketBankBytes : 0;
            return new(effect, 0, checked((int)(cursor - NativeBattleGatlingGun305Profile.PacketBase)), start + NativeBattleGatlingGun305Profile.PacketBankBytes, NativeBattleGatlingGun305Profile.PacketBankBytes, false, "2247790");
        }
        if (effect == 306)
        {
            NativeBattleDegenerator306State state = snapshot.NativeEffectMemory!.Degenerator306 ?? throw new InvalidDataException("Degenerator requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleDegenerator306Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleDegenerator306Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleDegenerator306Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleDegenerator306Profile.PacketBankBytes, true);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => task.Callback == NativeBattleDegenerator306Profile.RootCallback);
            int start = checked(source + ((root.ReadWord(12) & 1) == 0 ? NativeBattleDegenerator306Profile.PacketBankBytes : 0));
            return new(effect, source, checked((int)(cursor - NativeBattleDegenerator306Profile.ArenaAddress)), checked(start + NativeBattleDegenerator306Profile.PacketBankBytes), NativeBattleDegenerator306Profile.PacketBankBytes, true);
        }
        if (effect == 300)
        {
            NativeBattleBadBreath300State state = snapshot.NativeEffectMemory!.BadBreath300 ?? throw new InvalidDataException("Bad Breath requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleBadBreath300Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleBadBreath300Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleBadBreath300Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleBadBreath300Profile.PacketBankBytes, false);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots[0];
            int start = checked(source + (root.ReadDword(16) == 0 ? NativeBattleBadBreath300Profile.FirstPacketOffset : NativeBattleBadBreath300Profile.SecondPacketOffset));
            return new(effect, source + NativeBattleBadBreath300Profile.FirstPacketOffset, checked((int)(cursor - NativeBattleBadBreath300Profile.ArenaAddress)), checked(start + NativeBattleBadBreath300Profile.PacketBankBytes), NativeBattleBadBreath300Profile.PacketBankBytes, false);
        }
        if (effect == 299)
        {
            NativeBattleMicroMissiles299State state = snapshot.NativeEffectMemory!.MicroMissiles299 ?? throw new InvalidDataException("Micro Missiles requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleMicroMissiles299Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleMicroMissiles299Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleMicroMissiles299Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleMicroMissiles299Profile.PacketBankBytes, false);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots[0];
            int start = checked(source + (root.ReadDword(16) == 0 ? NativeBattleMicroMissiles299Profile.FirstPacketOffset : NativeBattleMicroMissiles299Profile.SecondPacketOffset));
            return new(effect, source + NativeBattleMicroMissiles299Profile.FirstPacketOffset, checked((int)(cursor - NativeBattleMicroMissiles299Profile.ArenaAddress)), checked(start + NativeBattleMicroMissiles299Profile.PacketBankBytes), NativeBattleMicroMissiles299Profile.PacketBankBytes, false);
        }
        if (effect == 297)
        {
            NativeBattleWhiteWind297State state = snapshot.NativeEffectMemory!.WhiteWind297 ?? throw new InvalidDataException("White Wind requires its independent module owner.");
            uint working = state.ReadDword(NativeBattleWhiteWind297Profile.WorkingGlobal);
            int source = checked((int)(working - NativeBattleWhiteWind297Profile.ArenaAddress));
            uint cursor = state.ReadDword(NativeBattleWhiteWind297Profile.CursorGlobal);
            if (cursor == 0)
            {
                return new(effect, source, null, null, NativeBattleWhiteWind297Profile.PacketBankBytes, false);
            }
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots[0];
            int start = checked(source + (root.ReadDword(16) == 0 ? NativeBattleWhiteWind297Profile.FirstPacketOffset : NativeBattleWhiteWind297Profile.SecondPacketOffset));
            return new(effect, source + NativeBattleWhiteWind297Profile.FirstPacketOffset, checked((int)(cursor - NativeBattleWhiteWind297Profile.ArenaAddress)), checked(start + NativeBattleWhiteWind297Profile.PacketBankBytes), NativeBattleWhiteWind297Profile.PacketBankBytes, false);
        }
        if (effect == 74)
        {
            NativeBattleDeathState state = snapshot.NativeEffectMemory!.LevelDeath74 ?? throw new InvalidDataException("Level Death requires its independent module owner.");
            NativeBattleModulePoolTask root = snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots.Single(task => task.Callback == NativeBattleDeathFamilyProfile.ForEffect(74).Callback(0x8B6A40));
            int source = state.Source ?? throw new InvalidDataException("Level Death requires its actual initialized source.");
            int start = checked(source + ((root.ReadWord(0x5C) & 1) == 0 ? 0 : 0xE000));
            return new(effect, source, state.Cursor, checked(start + 0xE000), 0xE000, false);
        }
        if (effect == 8)
        {
            NativeBattleDoom8State state = snapshot.NativeEffectMemory!.Doom8 ?? throw new InvalidDataException("Doom requires its initialized module owner.");
            uint first = state.ReadDword(0x275E82C);
            uint second = state.ReadDword(0x275E828);
            Assert.Equal(checked(first + 0x8000u), second);
            int source = checked((int)(first - 0x20DFAB8u));
            uint cursor = state.ReadDword(0x2758504);
            if (cursor == 0)
            {
                return new(effect, source, null, null, 0x8000, true);
            }
            NativeBattleModulePoolTask root = Assert.Single(snapshot.NativeGraphics.OriginalEffect.Pools[0].Slots, task => (task.Flags & 1) != 0 && task.Callback == NativeBattleDoom8Controller.RootCallback);
            bool odd = ((unchecked((ushort)(root.ReadWord(0x5C) - 1))) & 1) != 0;
            uint selected = odd ? first : second;
            Assert.InRange(cursor, selected, checked(selected + 0x8000u));
            return new(effect, source, checked((int)(cursor - 0x20DFAB8u)), checked((int)(selected + 0x8000u - 0x20DFAB8u)), 0x8000, true);
        }
        if (effect == 47)
        {
            NativeBattleCurse47State state = snapshot.NativeEffectMemory!.Curse47 ?? throw new InvalidDataException("Curse requires its initialized module owner.");
            uint source = state.ReadDword(0x264B3C8);
            uint primary = state.ReadDword(0x264B3CC);
            uint secondary = state.ReadDword(0x264B4D4);
            int offset = checked((int)(source - 0x20DFAB8u));
            return primary == 0 || secondary == 0
                ? new(effect, offset, null, null, 0x4000, true)
                : new(effect, offset, checked((int)(primary - 0x20DFAB8u)), checked((int)(secondary - 0x20DFAB8u)), 0x4000, true);
        }
        if (effect == 56)
        {
            NativeBattleDrain39State state = snapshot.NativeGraphics!.OriginalEffect!.Absorb56 ?? throw new InvalidDataException("Absorb requires its executed private draw state.");
            return new(effect, state.SourceArenaOffset, state.PrimaryDrawCursor, state.SecondaryDrawCursor, 0x6000, false);
        }
        if (effect == 62)
        {
            NativeBattleDevour62State state = snapshot.NativeEffectMemory!.RequireDevour62();
            return new(effect, state.AlternatePrimaryDrawBase ?? throw new InvalidDataException("Devour requires its actual lowest private bank."), state.PrimaryDrawCursor, state.SecondaryDrawCursor, 0x6000, false);
        }
        if (effect == 43)
        {
            NativeBattleCuragaState nodes = snapshot.NativeEffectMemory!.RequireMeltingBubble43().Nodes ?? throw new InvalidDataException("Melting Bubble requires its initialized node cursors.");
            return new(effect, nodes.SourceArenaOffset, nodes.PrimaryDrawCursor, nodes.SecondaryDrawCursor, 0xA000, false);
        }
        if (effect == 29)
        {
            NativeBattleCuragaState nodes = snapshot.NativeEffectMemory!.RequireWindBlast29().Nodes ?? throw new InvalidDataException("Wind Blast requires its initialized node cursors.");
            return new(effect, nodes.SourceArenaOffset, nodes.PrimaryDrawCursor, nodes.SecondaryDrawCursor, 0x8000, false);
        }
        if (effect == 60)
        {
            NativeBattleCuragaState nodes = snapshot.NativeEffectMemory!.RequireEffect60().Nodes ?? throw new InvalidDataException("Effect60 requires its initialized node cursors.");
            return new(effect, nodes.SourceArenaOffset, nodes.PrimaryDrawCursor, nodes.SecondaryDrawCursor, 0x8000, true);
        }
        if (effect == 23)
        {
            NativeBattleCuragaState nodes = snapshot.NativeEffectMemory!.RequirePsychoBlast23().Nodes ?? throw new InvalidDataException("Psycho Blast requires its initialized node cursors.");
            return new(effect, nodes.SourceArenaOffset, nodes.PrimaryDrawCursor, nodes.SecondaryDrawCursor, 0x8000, true);
        }
        if (effect == 20)
        {
            NativeBattleEffect20State state = snapshot.NativeEffectMemory!.RequireEffect20();
            return new(effect, state.SourceArenaOffset ?? throw new InvalidDataException("Effect20 requires its initialized source."), state.PrimaryDrawCursor, state.SecondaryDrawCursor, NativeBattleEffect20Profile.DrawStep, true);
        }
        if (effect == 19)
        {
            NativeBattleCuragaState nodes = snapshot.NativeEffectMemory!.RequireRaldo19().Nodes ?? throw new InvalidDataException("Raldo requires its initialized node cursors.");
            return new(effect, nodes.SourceArenaOffset, nodes.PrimaryDrawCursor, nodes.SecondaryDrawCursor, NativeBattleRaldo19Profile.DrawStep, true);
        }
        if (effect == 37)
        {
            NativeBattleCuragaState nodes = snapshot.NativeEffectMemory!.RequireDrinkMagic37().Nodes ?? throw new InvalidDataException("Drink Magic requires its initialized node cursors.");
            return new(effect, nodes.SourceArenaOffset, nodes.PrimaryDrawCursor, nodes.SecondaryDrawCursor, 0x9000, true);
        }
        if (effect is 73 or 78)
        {
            NativeBattleMightyGuardState state = snapshot.NativeEffectMemory!.RequireMightyGuard(effect);
            return new(effect, state.SourceArenaOffset ?? throw new InvalidDataException("The selected node module requires its actual working source."), state.PrimaryDrawCursor, state.SecondaryDrawCursor, 0x9000, false);
        }
        NativeBattleActorDirectorState director = snapshot.NativeEffectMemory!.RequireActorDirector(effect);
        return new(effect, director.Generic.SourceArenaOffset, director.PrimaryDrawCursor, director.SecondaryDrawCursor, 0x8000, true);
    }

    private static (int Ordinary, int Standard) ObserveOilBlast137ActBackends(NativeBattleOriginalEffectState? before, NativeBattleOriginalEffectState after, NativeBattleOilBlast137Resources resources)
    {
        if (before?.Pools.Length != 4 || after.Pools.Length != 4)
        {
            return (0, 0);
        }
        NativeBattleModulePoolTask prior = before.Pools[1].Slots[0];
        NativeBattleModulePoolTask current = after.Pools[1].Slots[0];
        if ((prior.Flags & 1) == 0 || prior.Callback != NativeBattleOilBlast137Profile.MainCallback || current.Callback != prior.Callback)
        {
            return (0, 0);
        }
        int ordinary = 0;
        int standard = 0;
        foreach ((int offset, int size, NativeBattleGfAnimationSource source, int mode) in new[] { (0x84, 0x40, resources.OpeningAct, 0), (0xC4, 0x40, resources.OpeningAct, 0), (0x104, 0x1A0, resources.MainAct, 0), (0x2A4, 0x2FC, resources.ImpactAct, 1) })
        {
            if (prior.ReadDword(offset) != source.Address || current.ReadDword(offset) != source.Address || current.ReadDword(offset + 4) != prior.ReadDword(offset + 4) + 1)
            {
                continue;
            }
            NativeBattleGfAnimationWorkspace workspace = new(prior.ReadBytes(offset, size).ToImmutableArray());
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (NativeBattleActMeshRendering.Visible(node))
                {
                    if (mode == 1 && node.GeometryIndex is 3 or 4)
                    {
                        standard++;
                    }
                    else
                    {
                        ordinary++;
                    }
                }
                return next;
            });
            Assert.Equal(step.Workspace.Data.ToArray(), current.ReadBytes(offset, size));
        }
        return (ordinary, standard);
    }

    private static string Breath157Rotation(NativeBattleBreath157State state) => JsonSerializer.Serialize(state.ReadView(NativeBattleBreath157Profile.ScratchRotationAddress, 20).ReadSnapshot(0, 20));

    private static string Breath157PointW(NativeBattleBreath157State state)
    {
        List<object> lanes = [];
        for (int index = 0; index < 3; index++)
        {
            var lane = state.ReadView(NativeBattleBreath157Profile.TravelPointsAddress + (uint)(index * 8 + 6), 2).ReadSnapshot(0, 2);
            lanes.Add(new { lane.Data, lane.Known });
        }
        return JsonSerializer.Serialize(lanes);
    }

    private static void AssertCannon151ScrollingUv(NativeBattleStaticMeshResource mesh, NativeBattleLogicalCommand command, int vOffset)
    {
        int selectedGroup = (command.Opcode & 0xFC) switch { 0x24 => 2, 0x2C => 3, 0x3C => 7, _ => throw new InvalidDataException("Cannon Blow scrolling packet leaves its reached primitive groups.") };
        int[] wordOffsets = selectedGroup == 2 ? [2, 4, 6] : selectedGroup == 3 ? [2, 4, 6, 8] : [2, 5, 8, 11];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        bool matched = false;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            cursor += 4;
            if (group == selectedGroup)
            {
                for (int face = 0; face < count; face++)
                {
                    ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(cursor + face * strides[group], strides[group]);
                    uint first = BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]);
                    uint second = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
                    uint[] uv = group == 2 ? [first, second, BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..])] : [first, second, BinaryPrimitives.ReadUInt16LittleEndian(bytes[20..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[22..])];
                    ImmutableArray<byte> v = NativeBattleEffectTextureWindows.OffsetAxisSigned(uv.Select(value => unchecked((byte)(value >> 8))), vOffset % 128, 128);
                    bool equal = true;
                    for (int corner = 0; corner < uv.Length; corner++)
                    {
                        uint expected = (uv[corner] & 0xFFFF00FFu) | (uint)v[corner] << 8;
                        uint mask = corner < 2 ? uint.MaxValue : 0xFFFFu;
                        equal &= (command.Words[wordOffsets[corner]] & mask) == (expected & mask);
                    }
                    matched |= equal;
                }
            }
            cursor += count * strides[group];
        }
        Assert.True(matched, "The current compound scrolling packet did not retain its original source UVs with the actual native signed V scroll.");
    }

    private static bool Cannon151InsetSprite(NativeBattleModuleResource source, NativeBattleLogicalCommand command)
    {
        if (command.WordOffset != 1 || (command.Opcode & 0xFC) != 0x2C || command.Words.Length != 9)
        {
            return false;
        }
        int frames = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8));
        for (int phase = 0; phase < frames; phase++)
        {
            NativeBattleModuleSpriteFrame frame = source.ProjectedFrame571C80(phase);
            for (int offset = 0; offset < frame.Quads.Length; offset += 20)
            {
                ReadOnlySpan<byte> quad = frame.Quads.AsSpan().Slice(offset, 20);
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
                int palette = unchecked((sbyte)source.Bytes[4 + (flags >> 12)]);
                uint clut = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad[6..]) + (palette << 6)));
                byte right = unchecked((byte)Math.Min(quad[0] + quad[4] - 1, 255));
                byte bottom = unchecked((byte)Math.Min(quad[2] + quad[5] - 1, 255));
                if (command.Opcode == quad[3] && command.Words[2] == (uint)(quad[4] | quad[5] << 8) + (clut << 16) && command.Words[4] == (uint)(right | quad[5] << 8) + ((uint)(flags & 0x1FF) << 16) && (command.Words[6] & 0xFFFF) == (uint)(quad[4] | bottom << 8) && (command.Words[8] & 0xFFFF) == (uint)(right | bottom << 8))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static NativeBattleLogicalCommand[] Effect298Commands(BattleSnapshot current)
    {
        uint cursor = current.NativeEffectMemory!.Effect298!.ReadDword(NativeBattleEffect298Profile.CursorGlobal);
        if (cursor == 0)
        {
            return [];
        }
        int end = checked((int)(cursor - NativeBattleEffect298Profile.ArenaAddress));
        int start = end >= NativeBattleEffect298Profile.SecondPacketOffset ? NativeBattleEffect298Profile.SecondPacketOffset : NativeBattleEffect298Profile.FirstPacketOffset;
        Assert.InRange(end, start, start + NativeBattleEffect298Profile.PacketBankBytes);
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).Select(item => item.Command).ToArray();
    }

    private static bool IsMegaSpark279Strip(NativeBattleLogicalCommand command)
    {
        return command.WordOffset == 1 && command.Opcode == 0x3E && command.Words.Length == 12 && command.Words[2] == 0x3D948048 && command.Words[5] == 0x00B68078 && (command.Words[8] & 0xFFFF) == 0x8048 && (command.Words[11] & 0xFFFF) == 0x8078;
    }

    private static NativeBattleLogicalCommand[] MegaSpark279Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 7)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.MegaSpark279!.ReadDword(NativeBattleMegaSpark279Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleMegaSpark279Profile.ArenaAddress));
        int start = end >= NativeBattleMegaSpark279Profile.SecondPacketOffset ? NativeBattleMegaSpark279Profile.SecondPacketOffset : NativeBattleMegaSpark279Profile.FirstPacketOffset;
        NativeBattlePrimitiveProcess common = current.NativeEffectMemory.PrimitiveProcess!;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end || item.Command.Packet.Bank == common.PacketBank && item.Command.Packet.Buffer == common.PacketBuffer && IsMegaSpark279Strip(item.Command)).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static void AssertMegaSpark279Strip(BattleSnapshot current, NativeBattleLogicalCommand command)
    {
        byte[] Read(int offset, int length) => command.Packet.Bank == "20DFAB8" ? current.NativeEffectMemory!.Arena!.Read(offset, length) : current.NativeEffectMemory!.PrimitiveProcess!.ReadCommonPacket(offset, length);
        Assert.Equal((byte)12, Read(command.Packet.ByteOffset + 3, 1)[0]);
        Assert.Equal(4, command.VertexPayload4.Length);
        for (int index = 0; index < command.Words.Length; index++)
        {
            int length = index is 8 or 11 ? 2 : 4;
            int offset = command.Packet.ByteOffset + (command.WordOffset + index) * 4;
            uint actual = length == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(Read(offset, 2)) : BinaryPrimitives.ReadUInt32LittleEndian(Read(offset, 4));
            uint mask = length == 2 ? 0xFFFFu : uint.MaxValue;
            Assert.Equal(command.Words[index] & mask, actual);
        }
    }

    private static NativeBattleLogicalCommand[] BoomerangSword304Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 5)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.BoomerangSword304!.ReadDword(NativeBattleBoomerangSword304Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleBoomerangSword304Profile.ArenaAddress));
        int start = end >= NativeBattleBoomerangSword304Profile.SecondPacketOffset ? NativeBattleBoomerangSword304Profile.SecondPacketOffset : NativeBattleBoomerangSword304Profile.FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static NativeBattleLogicalCommand[] SnipeLaser302Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 5)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.SnipeLaser302!.ReadDword(NativeBattleSnipeLaser302Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleSnipeLaser302Profile.ArenaAddress));
        int start = end >= NativeBattleSnipeLaser302Profile.SecondPacketOffset ? NativeBattleSnipeLaser302Profile.SecondPacketOffset : NativeBattleSnipeLaser302Profile.FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static NativeBattleLogicalCommand[] EnemyMicroMissiles284Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 4)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.EnemyMicroMissiles284!.ReadDword(NativeBattleEnemyMicroMissiles284Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleEnemyMicroMissiles284Profile.ArenaAddress));
        int start = end >= NativeBattleEnemyMicroMissiles284Profile.SecondPacketOffset ? NativeBattleEnemyMicroMissiles284Profile.SecondPacketOffset : NativeBattleEnemyMicroMissiles284Profile.FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static NativeBattleLogicalCommand[] MiniPulseCannon286Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 3)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.MiniPulseCannon286!.ReadDword(NativeBattleMiniPulseCannon286Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleMiniPulseCannon286Profile.ArenaAddress));
        int start = end >= NativeBattleMiniPulseCannon286Profile.SecondPacketOffset ? NativeBattleMiniPulseCannon286Profile.SecondPacketOffset : NativeBattleMiniPulseCannon286Profile.FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static NativeBattleLogicalCommand[] EvilEye282Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 4)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.EvilEye282!.ReadDword(NativeBattleEvilEye282Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleEvilEye282Profile.ArenaAddress));
        int start = end >= NativeBattleEvilEye282Profile.SecondPacketOffset ? NativeBattleEvilEye282Profile.SecondPacketOffset : NativeBattleEvilEye282Profile.FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static NativeBattleLogicalCommand[] Shotgun281Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 4)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.Shotgun281!.ReadDword(NativeBattleShotgun281Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleShotgun281Profile.ArenaAddress));
        int start = end >= NativeBattleShotgun281Profile.SecondPacketOffset ? NativeBattleShotgun281Profile.SecondPacketOffset : NativeBattleShotgun281Profile.FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static NativeBattleLogicalCommand[] Gravija214Commands(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.Length != 3 || effect.Pools[2].Slots[0].ReadWord(12) > 74)
        {
            return [];
        }
        uint cursor = current.NativeEffectMemory!.Gravija214!.ReadDword(NativeBattleGravija214Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleGravija214Profile.ArenaAddress));
        int start = end >= NativeBattleGravija214Profile.SecondPacketOffset ? NativeBattleGravija214Profile.SecondPacketOffset : NativeBattleGravija214Profile.FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            Assert.All(group, item => Assert.Equal(first.Words.ToArray(), item.Command.Words.ToArray()));
            Assert.All(group, item => Assert.Equal(first.VertexPayload4.ToArray(), item.Command.VertexPayload4.ToArray()));
            return first;
        }).ToArray();
    }

    private static int AssertGravija214Warp(BattleSnapshot current, NativeBattleLogicalCommand colorMode, NativeBattleLogicalCommand textureMode, NativeBattleLogicalCommand[] commands)
    {
        NativeBattleEffectArena arena = current.NativeEffectMemory!.Arena!;
        NativeBattleGravija214State state = current.NativeEffectMemory.Gravija214!;
        foreach (NativeBattleLogicalCommand mode in new[] { colorMode, textureMode })
        {
            Assert.Equal(1, mode.WordOffset);
            Assert.Single(mode.Words);
            Assert.Equal(new[] { 0, 0, 0, 0 }, mode.VertexPayload4.ToArray());
            Assert.Equal((byte)1, arena.Read(mode.Packet.ByteOffset + 3, 1)[0]);
            Assert.Equal(mode.Words[0], BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(mode.Packet.ByteOffset + 4, 4)));
        }
        Assert.Equal(colorMode.OrderingBucket, textureMode.OrderingBucket);
        Assert.Equal(colorMode.Packet.ByteOffset + 8 + 24 * 28 + 216 * 36, textureMode.Packet.ByteOffset);
        NativeBattleLogicalCommand[] colors = commands.Where(command => command.Packet.ByteOffset > colorMode.Packet.ByteOffset && command.Packet.ByteOffset < textureMode.Packet.ByteOffset && command.WordOffset == 1).ToArray();
        Assert.Equal(24, colors.Count(command => command.Opcode == 0x32));
        Assert.Equal(216, colors.Count(command => command.Opcode == 0x3A));
        int colorCursor = colorMode.Packet.ByteOffset + 8;
        int textureCursor = textureMode.Packet.ByteOffset + 8;
        uint sourceCursor = NativeBattleGravija214Profile.CopiedMeshAddress + 1936 + 4 * 4;
        for (int group = 0; group < 2; group++)
        {
            bool quad = group == 1;
            int count = checked((int)state.ReadDword(sourceCursor));
            Assert.Equal(quad ? 216 : 24, count);
            sourceCursor += 4;
            int corners = quad ? 4 : 3;
            int sourceStride = quad ? 24 : 20;
            for (int face = 0; face < count; face++)
            {
                NativeBattleEffectArena[] rows = new NativeBattleEffectArena[corners];
                int clip = 255;
                for (int corner = 0; corner < corners; corner++)
                {
                    int index = state.ReadWord(sourceCursor + (uint)(face * sourceStride + 4 + corner * 2));
                    Assert.InRange(index, 0, 720);
                    Assert.Equal(0, index % 3);
                    int rowOffset = NativeBattleGravija214Profile.ProjectedRowsOffset + index * 4;
                    rows[corner] = NativeBattleEffectNodeContextMemory.Copy(arena, rowOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, 12), 0, 12);
                    clip &= rows[corner].Read(8, 1)[0];
                }
                NativeBattleLogicalCommand colored = Assert.Single(commands, command => command.Packet.ByteOffset == colorCursor && command.WordOffset == 1);
                Assert.Equal(quad ? (byte)0x3A : (byte)0x32, colored.Opcode);
                Assert.Equal(colorMode.OrderingBucket, colored.OrderingBucket);
                for (int corner = 0; corner < corners; corner++)
                {
                    Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(rows[corner].Read(4, 4)), colored.Words[1 + corner * 2]);
                }
                colorCursor += quad ? 36 : 28;
                if (clip != 0)
                {
                    continue;
                }
                NativeBattleLogicalCommand textured = Assert.Single(commands, command => command.Packet.ByteOffset == textureCursor && command.WordOffset == 1);
                Assert.Equal(quad ? (byte)0x2E : (byte)0x26, textured.Opcode);
                Assert.Equal(((uint)textured.Opcode << 24) | 0x808080u, textured.Words[0]);
                Assert.Equal(textureMode.OrderingBucket, textured.OrderingBucket);
                int firstU = BinaryPrimitives.ReadUInt16LittleEndian(rows[0].Read(4, 2));
                int x = 576 + firstU;
                ushort page = unchecked((ushort)(((x >> 6) & 15) | (0x40 << 2) | 0x10));
                Assert.Equal(page, unchecked((ushort)(textured.Words[4] >> 16)));
                for (int corner = 0; corner < corners; corner++)
                {
                    Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(rows[corner].Read(0, 4)), textured.Words[1 + corner * 2]);
                    byte u = unchecked((byte)(BinaryPrimitives.ReadUInt16LittleEndian(rows[corner].Read(4, 2)) - (firstU & 0xC0)));
                    byte v = rows[corner].Read(6, 1)[0];
                    Assert.Equal((ushort)(u | v << 8), unchecked((ushort)textured.Words[2 + corner * 2]));
                }
                textureCursor += quad ? 40 : 32;
            }
            sourceCursor += checked((uint)(count * sourceStride));
        }
        Assert.Equal(textureMode.Packet.ByteOffset, colorCursor);
        return textureCursor;
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> Junk44PacketReferences(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        if (effect.ReturnedDescriptor == 0 || effect.Pools.IsEmpty || effect.Pools[0].Slots[0].ReadWord(12) >= 115)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        uint cursor = current.NativeEffectMemory!.Junk44!.ReadDword(NativeBattleJunk44Profile.CursorGlobal);
        int end = checked((int)(cursor - NativeBattleJunk44Profile.ArenaAddress));
        int start = end >= 0x112D4 ? 0x112D4 : 0x12D4;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end).Select(command => command.Packet).ToImmutableHashSet();
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> Stare45PacketReferences(BattleSnapshot prior, BattleSnapshot current)
    {
        ImmutableHashSet<NativeBattlePrimitiveReference> polygons = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
        if (polygons.IsEmpty)
        {
            return polygons;
        }
        NativeBattleLogicalCommand[] commands = Resonance49Commands(current);
        var modes = commands.Where(command => command.Opcode == 0xE1 && command.WordOffset == 1 && command.Words.Length == 1 && command.Words[0] == 0xE1000020 && commands.Any(polygon => polygons.Contains(polygon.Packet) && polygon.Packet.Bank == command.Packet.Bank && polygon.Packet.Buffer == command.Packet.Buffer && polygon.Packet.ByteOffset + (polygon.Words.Length + 1) * 4 == command.Packet.ByteOffset && polygon.OrderingBucket == command.OrderingBucket)).Select(command => command.Packet);
        return polygons.Union(modes);
    }

    private static NativeBattleEffectVramCopy[] Sigh46WrappedCopies(ushort firstPhase, ushort secondPhase)
    {
        List<NativeBattleEffectVramCopy> copies = [];
        Add(704, 64, 512, firstPhase);
        Add(768, 32, 672, secondPhase);
        return copies.ToArray();

        void Add(short sourceX, short width, short destinationX, ushort phase)
        {
            copies.Add(new(sourceX, 256, width, (short)(256 - phase), destinationX, (short)(256 + phase)));
            if (phase != 0)
            {
                copies.Add(new(sourceX, (short)(512 - phase), width, (short)phase, destinationX, 256));
            }
        }
    }

    private static void AssertSigh46VramCopies(BattleSnapshot prior, BattleSnapshot current, NativeBattleEffectVramCopy[] copies)
    {
        NativeBattleEffectTextureState oracle = prior.NativeEffectMemory!.Textures! with { Count = 0 };
        foreach (NativeBattleEffectVramCopy copy in copies)
        {
            oracle = oracle.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
        }
        byte[] expected = oracle.Drain505D20().State.ReadVram();
        byte[] actual = current.NativeEffectMemory!.Textures!.ReadVram();
        foreach (NativeBattleEffectVramCopy copy in copies)
        {
            for (int row = 0; row < copy.Height; row++)
            {
                int offset = 2 * ((copy.DestinationY + row) * 1024 + copy.DestinationX);
                Assert.Equal(expected.AsSpan(offset, copy.Width * 2).ToArray(), actual.AsSpan(offset, copy.Width * 2).ToArray());
            }
        }
    }

    private sealed record Resonance49PacketWitness(int ProgrammeCommands, int PositiveDepthCommands, int StaticEnvironmentCommands, int? EnvironmentParity, int ScreenPairs, byte[] ScreenOpcodes);

    private static NativeBattleVector Resonance49ProgrammePoint(BattleSnapshot prior, int actor)
    {
        BattleSession oracle = BattleSession.Restore(prior);
        NativeBattleGraphicsModel model = prior.NativeGraphics!.Models[actor];
        Assert.Equal(2, model.Flags0 & 2);
        NativeBattleGraphicsResource resource = BladeShot12Invoke<NativeBattleGraphicsResource>(oracle, "NativeGraphicsPoseResource", prior.NativeGraphics, actor);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Resonance programme requires its selected actor's original current skeleton.");
        Assert.Equal(32, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
        Assert.NotNull(model.GeometryWorldMatrix);
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, 0xF0, 4096);
        Assert.True(producer.Enabled);
        NativeBattleEffectMemory observed = BladeShot12Invoke<NativeBattleEffectMemory>(oracle, "ObserveNativeBoneAnchor", prior.NativeEffectMemory!, producer);
        return new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value));
    }

    private static NativeBattleLogicalCommand[] Resonance49Commands(BattleSnapshot current)
    {
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            foreach (NativeBattleLogicalResolvedCommand replay in group)
            {
                Assert.Equal(first.Words.ToArray(), replay.Command.Words.ToArray());
                Assert.Equal(first.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
            }
            return first;
        }).ToArray();
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> Resonance49PacketReferences(BattleSnapshot current)
    {
        NativeBattleLogicalCommand[] commands = Resonance49Commands(current);
        DirectedNodePacketState state = Director(current);
        ImmutableHashSet<NativeBattlePrimitiveReference>.Builder packets = ImmutableHashSet.CreateBuilder<NativeBattlePrimitiveReference>();
        foreach (NativeBattleLogicalCommand command in commands)
        {
            if (command.Packet.Bank == NativeBattleResonance49PacketDelivery.PacketBank || command.Packet.Bank == state.PacketBank && state.PrimaryDrawCursor is int end && state.SecondaryDrawCursor is int limit && command.Packet.ByteOffset >= limit - state.DrawStep && command.Packet.ByteOffset < end)
            {
                packets.Add(command.Packet);
            }
            if (command.Opcode == 0xE1 && command.Words.Length == 1 && command.Words[0] == 0xE1000020)
            {
                NativeBattleLogicalCommand? quad = commands.FirstOrDefault(candidate => candidate.Packet.Bank == command.Packet.Bank && candidate.Packet.Buffer == command.Packet.Buffer && candidate.Packet.ByteOffset + 40 == command.Packet.ByteOffset && candidate.WordOffset == 1 && candidate.Opcode is 0x2D or 0x2F && candidate.OrderingBucket == command.OrderingBucket);
                if (quad is not null)
                {
                    packets.Add(command.Packet);
                    packets.Add(quad.Packet);
                }
            }
        }
        return packets.ToImmutable();
    }

    private static Resonance49PacketWitness AssertResonance49Packets(BattleSnapshot current)
    {
        NativeBattleLogicalCommand[] commands = Resonance49Commands(current);
        DirectedNodePacketState selected = Director(current);
        NativeBattleResonance49State state = current.NativeEffectMemory!.Resonance49!;
        NativeBattlePrimitiveProcess process = current.NativeEffectMemory.PrimitiveProcess!;
        int programmes = 0;
        int positive = 0;
        int environments = 0;
        HashSet<int> parities = [];
        HashSet<byte> screenOpcodes = [];
        int pairs = 0;
        foreach (NativeBattleLogicalCommand command in commands)
        {
            byte[]? physical = null;
            if (command.Packet.Bank == NativeBattleResonance49PacketDelivery.PacketBank)
            {
                uint address = checked(NativeBattleResonance49Profile.BaseAddress + (uint)command.Packet.ByteOffset);
                uint packetBase = address < 0x2647820 ? 0x26472D8u : 0x2647820u;
                parities.Add(checked((int)((address - packetBase) / 64)));
                physical = state.Read(address + (uint)(command.WordOffset * 4), command.Words.Length * 4);
                environments++;
            }
            else if (command.Packet.Bank == selected.PacketBank && selected.PrimaryDrawCursor is int end && selected.SecondaryDrawCursor is int limit && command.Packet.ByteOffset >= limit - selected.DrawStep && command.Packet.ByteOffset < end)
            {
                physical = current.NativeEffectMemory.Arena!.Read(command.Packet.ByteOffset + command.WordOffset * 4, command.Words.Length * 4);
                programmes++;
                positive += command.VertexPayload4.Any(depth => depth > 0) ? 1 : 0;
            }
            if (physical is not null)
            {
                for (int word = 0; word < command.Words.Length; word++)
                {
                    uint mask = Additional182ConsumedMask(command.Opcode, word);
                    Assert.Equal(command.Words[word] & mask, BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4)) & mask);
                }
            }
            if (command.Packet.Bank != process.PacketBank || command.Opcode != 0xE1 || command.Words.Length != 1 || command.Words[0] != 0xE1000020)
            {
                continue;
            }
            NativeBattleLogicalCommand? quad = commands.FirstOrDefault(candidate => candidate.Packet.Bank == command.Packet.Bank && candidate.Packet.Buffer == command.Packet.Buffer && candidate.Packet.ByteOffset + 40 == command.Packet.ByteOffset && candidate.WordOffset == 1 && candidate.Opcode is 0x2D or 0x2F && candidate.OrderingBucket == command.OrderingBucket);
            if (quad is null)
            {
                continue;
            }
            Assert.Equal((int?)5, quad.OrderingBucket);
            Assert.Empty(quad.VertexPayload4);
            Assert.Equal(new[] { 0, 0, 0, 0 }, command.VertexPayload4.ToArray());
            Assert.Equal(new uint[] { 0xE1000020, 0 }, Enumerable.Range(0, 2).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(process.ReadCommonPacket(command.Packet.ByteOffset + 4 + word * 4, 4))).ToArray());
            Assert.Equal(quad.Opcode, process.ReadCommonPacket(quad.Packet.ByteOffset + 7, 1)[0]);
            Assert.Equal((byte)9, process.ReadCommonPacket(quad.Packet.ByteOffset + 3, 1)[0]);
            for (int corner = 0; corner < 4; corner++)
            {
                Assert.Equal(quad.Words[1 + corner * 2], BinaryPrimitives.ReadUInt32LittleEndian(process.ReadCommonPacket(quad.Packet.ByteOffset + 8 + corner * 8, 4)));
                Assert.Equal(quad.Words[2 + corner * 2] & 0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(process.ReadCommonPacket(quad.Packet.ByteOffset + 12 + corner * 8, 2)));
            }
            Assert.Contains(BinaryPrimitives.ReadUInt16LittleEndian(process.ReadCommonPacket(quad.Packet.ByteOffset + 22, 2)), new ushort[] { 0x119, 0x11D });
            screenOpcodes.Add(quad.Opcode);
            pairs++;
        }
        Assert.True(parities.Count <= 1);
        return new(programmes, positive, environments, parities.Count == 0 ? null : parities.Single(), pairs, screenOpcodes.Order().ToArray());
    }

    private static T BladeShot12Invoke<T>(BattleSession session, string method, params object[] arguments) => (T)(typeof(BattleSession).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) ?? throw new MissingMethodException(typeof(BattleSession).FullName, method)).Invoke(session, arguments)!;

    private static NativeBattleVector BladeShot12PointOracle(BattleSnapshot prior, int actor, byte[] selectors)
    {
        NativeBattleGraphicsModel model = prior.NativeGraphics!.Models[actor];
        if ((model.Flags0 & 2) == 0)
        {
            return model.Transform!.Position28;
        }
        BattleSession oracle = BattleSession.Restore(prior);
        NativeBattleGraphicsResource resource = BladeShot12Invoke<NativeBattleGraphicsResource>(oracle, "NativeGraphicsPoseResource", prior.NativeGraphics, actor);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Blade Shot point queries require the original current skeleton.");
        NativeBattleEffectMemory memory = prior.NativeEffectMemory!;
        List<NativeBattleVector> points = [];
        foreach (byte selector in selectors)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, selector, 0);
            Assert.True(producer.Enabled);
            memory = BladeShot12Invoke<NativeBattleEffectMemory>(oracle, "ObserveNativeBoneAnchor", memory, producer);
            points.Add(new(unchecked((short)memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)memory.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)memory.ScriptedColorRegisters.Mac[2]!.Value)));
        }
        return new(unchecked((short)(points.Sum(point => point.X) / points.Count)), unchecked((short)(points.Sum(point => point.Y) / points.Count)), unchecked((short)(points.Sum(point => point.Z) / points.Count)));
    }

    private static object AssertBladeShot12EntryPoints(BattleSnapshot prior, BattleSnapshot current, NativeBattleModulePoolTask root)
    {
        NativeBattleVector caster = BladeShot12PointOracle(prior, 3, [7]);
        NativeBattleVector recipient = BladeShot12PointOracle(prior, 0, [0xF0, 0xF1]);
        Assert.Equal(new[] { caster.X, caster.Y, caster.Z }, new[] { unchecked((short)root.ReadWord(0x1C)), unchecked((short)root.ReadWord(0x1E)), unchecked((short)root.ReadWord(0x20)) });
        Assert.Equal(new[] { recipient.X, recipient.Y, recipient.Z }, new[] { unchecked((short)root.ReadWord(0x24)), unchecked((short)root.ReadWord(0x26)), unchecked((short)root.ReadWord(0x28)) });
        Assert.Equal((ushort)128, root.ReadWord(0x2A));
        Assert.Contains(root.ReadWord(0x22), new ushort[] { 0, 128 });
        return new { Boundary = "entry-current-caster7-recipient-F0-F1", Caster = caster, Recipient = recipient, Age = root.ReadWord(12), SourceWAccumulator = root.ReadWord(0x22), TargetWRate = root.ReadWord(0x2A), CurrentFrame = current.Frame };
    }

    private static object AssertBladeShot12ScopedRandom(BattleSnapshot prior, int oldAge, int draws)
    {
        BattleSession oracle = BattleSession.Restore(prior);
        NativeBattleGraphicsState graphics = prior.NativeGraphics!;
        if (graphics.PrimitiveFrame is not null)
        {
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = NativeBattleLogicalOrdering.Clear() } };
        }
        _ = BladeShot12Invoke<NativeBattleGraphicsState>(oracle, "RunNativeBladeShot12", graphics, false);
        NativeCrtRandomState expected = prior.NativeCrtRandom ?? throw new InvalidDataException("Blade Shot requires the original CRT state.");
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, oracle.Capture().NativeCrtRandom);
        return new { Boundary = "isolated-native-effect-CRT", OldAge = oldAge, Draws = draws, Before = prior.NativeCrtRandom, After = expected };
    }

    private static void AssertBladeShot12Morph(BattleSnapshot current, NativeBattleBladeShot12Resources resources, int factor)
    {
        NativeBattleBladeShot12State state = current.NativeEffectMemory!.BladeShot12!;
        foreach (var pair in new[] { (Destination: NativeBattleBladeShot12Profile.MutableVerticesA, First: resources.SourceA0, Second: resources.SourceA1, Seed: resources.MeshA), (Destination: NativeBattleBladeShot12Profile.MutableVerticesB, First: resources.SourceB0, Second: resources.SourceB1, Seed: resources.MeshB) })
        {
            NativeBattleGeometryRegisterStep registers = new(current.NativeEffectMemory.GeometryRegisters!, current.NativeEffectMemory.ScriptedColorRegisters!);
            for (int vertex = 0; vertex < 49; vertex++)
            {
                int offset = 8 + vertex * 8;
                ushort Word(NativeBattleModuleResource source, int axis) => BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(offset + axis * 2, 2));
                registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - factor)) } }, Word(pair.First, 0), Word(pair.First, 1), Word(pair.First, 2));
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
                registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, Word(pair.Second, 0), Word(pair.Second, 1), Word(pair.Second, 2));
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, NativeBattleGteColorInterpolation.CodeConstants45F270);
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.Equal(unchecked((ushort)registers.Colors.Ir[axis]!.Value), state.ReadWord(pair.Destination + (uint)(vertex * 8 + axis * 2)));
                }
                Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(pair.Seed.Bytes.AsSpan().Slice(offset + 6, 2)), state.ReadWord(pair.Destination + (uint)(vertex * 8 + 6)));
            }
        }
    }

    private static object AssertBladeShot12Object7(BattleSnapshot prior, BattleSnapshot current, BattleAssets assets, bool visible)
    {
        NativeBattleGraphicsModel model = current.NativeGraphics!.Models[3];
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(current.Definition.Combatants.Single(actor => actor.Id == current.NativeGraphics.ActorIds[3]));
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        NativeBattleModelCpuReference pointer = cpu.Rows[model.ActiveResourceRow100!.Value].Require(16);
        byte[] Read(int offset, int length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, pointer with { Offset = pointer.Offset + offset }, length);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(Read, 31);
        Assert.Equal(8, meshes.Length);
        Assert.Equal(34, meshes[7].Vertices.Length);
        NativeBattleGraphicsModel producer = prior.NativeGraphics!.Models[3] with { Color40 = model.Color40, SequenceState = prior.NativeGraphics.Models[3].SequenceState! with { MeshMask124 = model.SequenceState!.MeshMask124 } };
        NativeBattleCameraProjection projection = prior.NativeCamera!.Projection!;
        NativeBattleVector? eye = prior.NativeCamera.CurrentPose?.Eye is { } cameraEye ? new(cameraEye.X, cameraEye.Y, cameraEye.Z) : null;
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.GeometryMatrices(producer, resource.PoseSource!, projection.View!, eye);
        ImmutableArray<NativeBattleGeometryPrimitive> expected = NativeBattleRawGeometryPackets.Emit5099D0([meshes[7]], bones, projection, model.Color40!.Value, 1, null, 17, 4, null, prior.Definition.NativeGraphics!.Rules.SummonColors!, viewport: new(short.MinValue / 8, short.MinValue / 8, ushort.MaxValue / 8, ushort.MaxValue / 8));
        NativeBattlePrimitiveProcess process = current.NativeEffectMemory!.PrimitiveProcess!;
        HashSet<string> actual = current.NativeGraphics.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == process.PacketBank && item.Command.WordOffset == 1).Select(item => Additional181Key(item.Command.Words, item.Command.VertexPayload4, item.Command.OrderingBucket)).ToHashSet();
        string[] expectedKeys = expected.Select(primitive => Additional181Key(primitive.Words.Skip(1), primitive.VertexPayload4, primitive.Bucket)).ToArray();
        int matches = expectedKeys.Count(actual.Contains);
        Assert.Equal(visible ? 0x80u : 0u, model.SequenceState!.MeshMask124 & 0x80);
        if (visible)
        {
            Assert.NotEmpty(expectedKeys);
            Assert.Equal(expectedKeys.Length, matches);
        }
        else
        {
            Assert.Equal(0, matches);
        }
        return new { Boundary = "original-object7-current-CPU-bones-packets", Visible = visible, Objects = meshes.Length, Vertices = meshes[7].Vertices.Length, ExpectedPackets = expectedKeys.Length, ActualMatches = matches, Mask = model.SequenceState.MeshMask124, Source = pointer, PosePhase = "Actual model draw before sequence advance and local-bone republish", DepthCue = (short?)null, ExpandedOrdinaryViewport = true };
    }

    private sealed record Acid170BodyWitness(object Evidence, ImmutableHashSet<NativeBattlePrimitiveReference> Packets);

    private static Acid170BodyWitness AssertAcid170Body(BattleSnapshot prior, BattleSnapshot current, BattleAssets assets, int oldAge, NativeBattleModulePoolTask body)
    {
        NativeBattleGraphicsModel model = prior.NativeGraphics!.Models[0];
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(prior.Definition.Combatants.Single(actor => actor.IsPlayer));
        Assert.Equal((int?)1, model.ActiveResourceRow100);
        Assert.Equal((int?)2, model.LinkedTextureResourceIndex);
        Assert.Null(model.ActiveStagedResourceRow100);
        Assert.Null(model.LinkedStagedResourceRow);
        NativeBattleAcid170State state = current.NativeEffectMemory!.Acid170!;
        short sx = unchecked((short)state.ReadWord(NativeBattleAcid170Profile.CurveBase));
        short sy = unchecked((short)state.ReadWord(NativeBattleAcid170Profile.CurveBase + 2));
        short sz = unchecked((short)state.ReadWord(NativeBattleAcid170Profile.CurveBase + 4));
        short tx = unchecked((short)state.ReadWord(NativeBattleAcid170Profile.CurveBase + 8));
        short ty = unchecked((short)state.ReadWord(NativeBattleAcid170Profile.CurveBase + 10));
        short tz = unchecked((short)state.ReadWord(NativeBattleAcid170Profile.CurveBase + 12));
        var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(sx - tx, sy - ty, sz - tz);
        short nx = unchecked((short)normal.X);
        short ny = unchecked((short)normal.Y);
        short nz = unchecked((short)normal.Z);
        int plane = unchecked(-(unchecked(tx * normal.X + ty * normal.Y + tz * normal.Z) >> 12));
        NativeBattleModelCpuResources cpu = prior.NativeModelCpuResources!;
        int scratchLength = cpu.ScratchMaximum!.Value * 8;
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 172).Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x1C, BitConverter.GetBytes(model.Color40!.Value)).Write(0x20, BitConverter.GetBytes(model.SequenceState!.MeshMask124)).Write(0x74, BitConverter.GetBytes(nx)).Write(0x76, BitConverter.GetBytes(ny)).Write(0x78, BitConverter.GetBytes(nz)).Write(0x7A, BitConverter.GetBytes(unchecked((short)plane))).Write(0x30, BitConverter.GetBytes(unchecked(tx * nx + ty * ny + tz * nz))).Write(0x34, BitConverter.GetBytes(-(int)ny)).Write(0x3C, BitConverter.GetBytes(oldAge < 28 ? 81 * oldAge : 12379 - 364 * oldAge)).Write(0x40, BitConverter.GetBytes(6250000));
        temporary = NativeBattleEffectNodeContextMemory.Copy(state.ReadView(NativeBattleAcid170Profile.CurveBase + 8, 8), 0, temporary, 0x7C, 8);
        temporary = temporary.Write(0x84, BitConverter.GetBytes(body.ReadWord(16))).Write(0x86, BitConverter.GetBytes(body.ReadWord(18))).Write(0x88, BitConverter.GetBytes(body.ReadWord(20)));
        NativeBattlePrimitiveProcess process = current.NativeEffectMemory.PrimitiveProcess!;
        NativeBattleLogicalCommand[] commands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == process.PacketBank).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            foreach (NativeBattleLogicalResolvedCommand replay in group)
            {
                Assert.Equal(first.Words.ToArray(), replay.Command.Words.ToArray());
                Assert.Equal(first.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
            }
            return first;
        }).ToArray();
        Dictionary<string, Queue<NativeBattleLogicalCommand>> available = commands.Where(command => command.WordOffset == 1).GroupBy(command => Additional181Key(command.Words, command.VertexPayload4, command.OrderingBucket)).ToDictionary(group => group.Key, group => new Queue<NativeBattleLogicalCommand>(group));
        NativeBattleEffectMemory memory = prior.NativeEffectMemory!;
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().MarkUnknown(0, scratchLength);
        List<object> sources = [];
        HashSet<NativeBattlePrimitiveReference> matchedPackets = [];
        foreach (bool linked in new[] { false, true })
        {
            int row = linked ? model.LinkedTextureResourceIndex!.Value : model.ActiveResourceRow100!.Value;
            NativeBattleModelCpuReference pointer = cpu.Rows[row].Require(16);
            Assert.Equal(linked ? 1764 : 16012, cpu.Rows[row].Require(20).Offset - pointer.Offset);
            ImmutableArray<NativeBattleActionMatrix> local = linked ? model.CurrentLinkedBoneMatrices!.Matrices : NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource!);
            Assert.Equal(linked ? 2 : 23, local.Length);
            ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattlePoseMatrices.ComposeStageCamera(local, model.GeometryWorldMatrix!, 0).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            temporary = temporary.Write(0x20, BitConverter.GetBytes(linked ? uint.MaxValue : model.SequenceState.MeshMask124)).Write(0x8A, BitConverter.GetBytes((ushort)(linked ? 0 : 1)));
            NativeBattleAcid170ModelStep expected = NativeBattleAcid170ModelDrawing.DrawBody(memory, temporary, scratch, Read, pointer.PhysicalAddress, bones, prior.NativeCamera!.CapturePackedView1D97778(), prior.NativeCamera.Projection!, prior.NativeEffectMemory!.ScriptedGraphics!, scratchAddress, scratchLength);
            int matched = 0;
            foreach (NativeBattleGfGeometryAttempt attempt in expected.Batch.Attempts)
            {
                if (attempt.Primitive is not { } primitive)
                {
                    continue;
                }
                string key = Additional181Key(primitive.Words.Skip(1), primitive.VertexPayload4, primitive.Bucket);
                Assert.True(available.TryGetValue(key, out Queue<NativeBattleLogicalCommand>? matches) && matches.Count > 0, "Actual recipient23/linked2 Acid body differs from its current CPU source, world/local bones, plane/LIGHT*IR or primary-only displacement.");
                NativeBattleLogicalCommand actual = matches!.Dequeue();
                matchedPackets.Add(actual.Packet);
                byte[] physical = process.ReadCommonPacket(actual.Packet.ByteOffset, primitive.Words.Length * 4);
                ImmutableArray<NativeBattleGfPacketByteState> mask = primitive.PhysicalWriteMask!.Value;
                byte[] expectedBytes = primitive.Words.SelectMany(BitConverter.GetBytes).ToArray();
                for (int index = 4; index < expectedBytes.Length; index++)
                {
                    if (mask[index] == NativeBattleGfPacketByteState.Known)
                    {
                        Assert.Equal(expectedBytes[index], physical[index]);
                    }
                }
                matched++;
            }
            Assert.True(matched > 0);
            for (int vertex = 0; vertex < (linked ? 78 : 478); vertex++)
            {
                Assert.Equal(new byte[2], expected.VertexScratch.Read(vertex * 8 + 6, 2));
            }
            memory = expected.Memory;
            temporary = expected.Temporary172;
            scratch = expected.VertexScratch;
            sources.Add(new { Linked = linked, Row = row, CurrentBones = local.Length, MatchedFaces = matched, PrimaryHorizontalDisplacement = !linked, WorldComposition = "model world for primary and linked; camera applied by raw projection" });

            byte[] Read(int offset, int length) => NativeBattleModelEnemyHeap.Read(cpu, prior.NativeFileStaging!, pointer with { Offset = pointer.Offset + offset }, length);
        }
        NativeBattleGraphicsModel restored = current.NativeGraphics.Models[0];
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(restored.SequenceState!, resource.PoseSource!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource!, decoded)), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(restored, resource.PoseSource!)));
        NativeBattleDecodedPose linkedDecoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(restored.SequenceState!, resource.LinkedPoseSource!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.LinkedPoseSource!, linkedDecoded)), JsonSerializer.Serialize(restored.CurrentLinkedBoneMatrices!.Matrices));
        return new(new { OldAge = oldAge, ActualRecipient = 0, DistinctCaster = 3, Normal = new[] { nx, ny, nz }, Plane = plane, Sources = sources, Owner = "actual packet witness at callback prior camera/current world/local bones; no attribution of final frame GTE after later model/stage writers" }, matchedPackets.ToImmutableHashSet());
    }

    private static object AssertMorph172Body(BattleSnapshot prior, BattleSnapshot current, BattleAssets assets, NativeBattleMorph172Resources resources, int oldAge, NativeBattleModulePoolTask body)
    {
        NativeBattleGraphicsModel model = prior.NativeGraphics!.Models[3];
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(prior.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 0));
        Assert.Equal(41, resource.PoseSource!.BoneCount);
        Assert.Equal(3, resource.Geometry!.Value.Length);
        Assert.Null(model.LinkedTransform);
        Assert.Null(model.ActiveStagedResourceRow100);
        NativeBattleModelCpuResources cpu = prior.NativeModelCpuResources!;
        NativeBattleModelCpuReference pointer = cpu.Rows[model.ActiveResourceRow100!.Value].Require(16);
        Assert.Equal(12184, cpu.Rows[model.ActiveResourceRow100.Value].Require(20).Offset - pointer.Offset);
        ImmutableArray<NativeBattleActionMatrix> local = NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource);
        Assert.Equal(41, local.Length);
        ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattlePoseMatrices.ComposeStageCamera(local, model.GeometryWorldMatrix!, 0).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        int scratchLength = cpu.ScratchMaximum!.Value * 8;
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleEffectArena retained = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(prior.NativeFileStaging!, scratchAddress, scratchLength), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, scratchLength), 0, scratchLength);
        NativeBattlePrimitiveProcess process = prior.NativeEffectMemory!.PrimitiveProcess!;
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters! with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
        int cue = oldAge < 8 ? (8 - oldAge) << 9 : oldAge >= 48 ? (oldAge - 48) << 9 : 0;
        NativeBattleSummonColors colors = prior.Definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleScriptedDpcsResult palette = NativeBattleScriptedDpcs.Transform45F270(prior.NativeEffectMemory.ScriptedColorRegisters!, resources.Colors[oldAge % 20], unchecked((uint)cue), far, colors);
        NativeBattleEffectMemory memory = prior.NativeEffectMemory with { ScriptedColorRegisters = palette.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 220).Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x1C, BitConverter.GetBytes(model.Color40!.Value)).Write(0x20, BitConverter.GetBytes(model.SequenceState!.MeshMask124)).Write(0xA8, BitConverter.GetBytes((int)unchecked((short)body.ReadWord(16)))).Write(0xAC, BitConverter.GetBytes((int)unchecked((short)body.ReadWord(18)))).Write(0xB0, BitConverter.GetBytes((int)unchecked((short)body.ReadWord(20)))).Write(0xB4, BitConverter.GetBytes(1000)).Write(0xB8, BitConverter.GetBytes(palette.PackedRgb));
        NativeBattleMorph172ModelStep expected = NativeBattleMorph172ModelDrawing.DrawBody(memory, temporary, retained, Read, pointer.PhysicalAddress, bones, prior.NativeCamera!.CapturePackedView1D97778(), prior.NativeCamera.Projection!, prior.NativeEffectMemory.ScriptedGraphics!, scratchAddress, scratchLength, colors);
        NativeBattlePrimitiveProcess actualProcess = current.NativeEffectMemory!.PrimitiveProcess!;
        NativeBattleLogicalCommand[] commands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == actualProcess.PacketBank).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            foreach (NativeBattleLogicalResolvedCommand replay in group)
            {
                Assert.Equal(first.Words.ToArray(), replay.Command.Words.ToArray());
                Assert.Equal(first.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
            }
            return first;
        }).ToArray();
        Dictionary<string, Queue<NativeBattleLogicalCommand>> available = commands.Where(command => command.WordOffset == 1).GroupBy(command => Additional181Key(command.Words, command.VertexPayload4, command.OrderingBucket)).ToDictionary(group => group.Key, group => new Queue<NativeBattleLogicalCommand>(group));
        int bases = 0;
        int overlays = 0;
        HashSet<byte> opcodes = [];
        foreach (NativeBattleGfGeometryAttempt attempt in expected.Batch.Attempts)
        {
            if (attempt.Primitive is not { } primitive)
            {
                continue;
            }
            string key = Additional181Key(primitive.Words.Skip(1), primitive.VertexPayload4, primitive.Bucket);
            Assert.True(available.TryGetValue(key, out Queue<NativeBattleLogicalCommand>? matches) && matches.Count > 0, "Morph body220 packet differs from its actual source/current bones, retained W, threshold and signed IR0/DPCS contract.");
            NativeBattleLogicalCommand actual = matches!.Dequeue();
            Assert.Equal(primitive.Words.Skip(1).ToArray(), Enumerable.Range(1, primitive.Words.Length - 1).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(actualProcess.ReadCommonPacket(actual.Packet.ByteOffset + word * 4, 4))).ToArray());
            byte opcode = unchecked((byte)(primitive.Words[1] >> 24));
            opcodes.Add(opcode);
            if (opcode is 0x32 or 0x3A)
            {
                overlays++;
            }
            else
            {
                bases++;
            }
        }
        Assert.True(bases > 0);
        Assert.True(overlays > 0);
        for (int vertex = 0; vertex < scratchLength / 8; vertex++)
        {
            (byte[] before, bool[] beforeKnown) = retained.ReadSnapshot(vertex * 8 + 6, 2);
            (byte[] after, bool[] afterKnown) = expected.VertexScratch.ReadSnapshot(vertex * 8 + 6, 2);
            Assert.Equal(beforeKnown, afterKnown);
            Assert.Equal(before, after);
        }
        NativeBattleGraphicsModel restored = current.NativeGraphics.Models[3];
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(restored.SequenceState!, resource.PoseSource);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, decoded)), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(restored, resource.PoseSource)));
        return new { OldAge = oldAge, Cue = cue, Palette = palette.PackedRgb, BaseFaces = bases, OptionalGouraudFaces = overlays, Opcodes = opcodes.Order().ToArray(), Bones = 41, Objects = 3, ScratchW = "retained with original knownness; skin XYZ6 only", Owner = "physical current-body packet witness and callback prior camera/current world/local bones; later model/stage register writers retain ownership" };

        byte[] Read(int offset, int length) => NativeBattleModelEnemyHeap.Read(cpu, prior.NativeFileStaging!, pointer with { Offset = pointer.Offset + offset }, length);
    }

    private static object AssertPoisonGas171Body(BattleSnapshot prior, BattleSnapshot current, BattleAssets assets, int oldAge)
    {
        NativeBattleGraphicsModel model = prior.NativeGraphics!.Models[3];
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(prior.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 0));
        Assert.Equal(42, resource.PoseSource!.BoneCount);
        Assert.Null(model.ActiveStagedResourceRow100);
        Assert.Null(model.LinkedTransform);
        NativeBattleModelCpuResources cpu = prior.NativeModelCpuResources!;
        NativeBattleModelCpuReference pointer = cpu.Rows[model.ActiveResourceRow100!.Value].Require(16);
        Assert.Equal(26308, cpu.Rows[model.ActiveResourceRow100.Value].Require(20).Offset - pointer.Offset);
        ImmutableArray<NativeBattleActionMatrix> local = NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource);
        Assert.Equal(42, local.Length);
        ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattlePoseMatrices.ComposeStageCamera(local, model.GeometryWorldMatrix!, 0).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        int scratchLength = cpu.ScratchMaximum!.Value * 8;
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        int intensity = oldAge < 4 ? oldAge << 5 : oldAge >= 16 ? (24 - oldAge) << 4 : 128;
        uint gray = unchecked((uint)(intensity | intensity << 8 | intensity << 16));
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 176).Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x1C, BitConverter.GetBytes(model.Color40!.Value)).Write(0x20, BitConverter.GetBytes(model.SequenceState!.MeshMask124)).Write(0x3A, BitConverter.GetBytes((ushort)128)).Write(0x6C, BitConverter.GetBytes(gray)).Write(0x84, BitConverter.GetBytes((oldAge * 4) % 128)).Write(0x88, BitConverter.GetBytes(0xE2000210u)).Write(0x8C, BitConverter.GetBytes(0xE2000000u));
        NativeBattlePoisonGas171ModelStep expected = NativeBattlePoisonGas171ModelDrawing.DrawBody(prior.NativeEffectMemory!, temporary, NativeBattleEffectArena.Clear().MarkUnknown(0, scratchLength), Read, pointer.PhysicalAddress, bones, prior.NativeCamera!.CapturePackedView1D97778(), prior.NativeCamera.Projection!, prior.NativeEffectMemory!.ScriptedGraphics!, scratchAddress, scratchLength);
        NativeBattlePrimitiveProcess process = current.NativeEffectMemory!.PrimitiveProcess!;
        NativeBattleLogicalCommand[] commands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == process.PacketBank).GroupBy(item => (item.Command.Packet, item.Command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First().Command;
            foreach (NativeBattleLogicalResolvedCommand replay in group)
            {
                Assert.Equal(first.Words.ToArray(), replay.Command.Words.ToArray());
                Assert.Equal(first.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
            }
            return first;
        }).ToArray();
        Dictionary<string, Queue<NativeBattleLogicalCommand>> bases = commands.Where(command => command.WordOffset == 1 && (command.Opcode & 0xFC) is 0x24 or 0x2C && HasAdjacentOverlay(command)).GroupBy(command => Additional181Key(command.Words, command.VertexPayload4, command.OrderingBucket)).ToDictionary(group => group.Key, group => new Queue<NativeBattleLogicalCommand>(group));
        int faces = 0;
        for (int index = 0; index < expected.Batch.Attempts.Length; index++)
        {
            if (expected.Batch.Attempts[index].Primitive is not { } basis)
            {
                continue;
            }
            NativeBattleGeometryPrimitive overlay = expected.Batch.Attempts[++index].Primitive!;
            string key = Additional181Key(basis.Words.Skip(1), basis.VertexPayload4, basis.Bucket);
            Assert.True(bases.TryGetValue(key, out Queue<NativeBattleLogicalCommand>? matches) && matches.Count > 0, "Current42-bone body face differs from its actual CPU source, prior current world/bones and callback camera.");
            NativeBattleLogicalCommand actual = matches!.Dequeue();
            int overlayOffset = actual.Packet.ByteOffset + basis.Words.Length * 4;
            NativeBattleLogicalCommand[] overlayCommands = commands.Where(command => command.Packet.ByteOffset == overlayOffset).ToArray();
            NativeBattleLogicalCommand window = Assert.Single(overlayCommands, command => command.WordOffset == 1);
            NativeBattleLogicalCommand polygon = Assert.Single(overlayCommands, command => command.WordOffset == 2);
            NativeBattleLogicalCommand reset = Assert.Single(overlayCommands, command => command.WordOffset == overlay.Words.Length - 1);
            Assert.Equal(0xE2000210u, Assert.Single(window.Words));
            Assert.Equal(0xE2000000u, Assert.Single(reset.Words));
            Assert.Equal((byte)(basis.Words.Length == 8 ? 0x26 : 0x2E), polygon.Opcode);
            Assert.Equal(new int[4], polygon.VertexPayload4.ToArray());
            Assert.Equal(overlay.Bucket, polygon.OrderingBucket);
            Assert.Equal(basis.Words.Skip(1).ToArray(), Enumerable.Range(1, basis.Words.Length - 1).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(process.ReadCommonPacket(actual.Packet.ByteOffset + word * 4, 4))).ToArray());
            Assert.Equal(overlay.Words.Skip(1).ToArray(), Enumerable.Range(1, overlay.Words.Length - 1).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(process.ReadCommonPacket(overlayOffset + word * 4, 4))).ToArray());
            faces++;
        }
        Assert.True(faces > 0);
        for (int vertex = 0; vertex < 786; vertex++)
        {
            Assert.Equal(new byte[2], expected.VertexScratch.Read(vertex * 8 + 6, 2));
        }
        NativeBattleGraphicsModel restored = current.NativeGraphics.Models[3];
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(restored.SequenceState!, resource.PoseSource);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, decoded)), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(restored, resource.PoseSource)));
        return new { OldAge = oldAge, Intensity = intensity, VDelta = oldAge * 4 % 128, Faces = faces, CurrentBones = local.Length, SourceBytes = 26308, VertexW = "IR XYZ plus zero W", Owner = "callback prior camera/current world and local bones; later ordinary model/stage writers own final registers", Restored = true };

        bool HasAdjacentOverlay(NativeBattleLogicalCommand basis)
        {
            bool quad = (basis.Opcode & 0xFC) == 0x2C;
            int offset = checked(basis.Packet.ByteOffset + (quad ? 40 : 32));
            NativeBattleLogicalCommand[] adjacent = commands.Where(command => command.Packet.Bank == basis.Packet.Bank && command.Packet.Buffer == basis.Packet.Buffer && command.Packet.ByteOffset == offset).ToArray();
            NativeBattleLogicalCommand? window = adjacent.SingleOrDefault(command => command.WordOffset == 1 && command.Opcode == 0xE2 && command.Words.Length == 1 && command.Words[0] == 0xE2000210);
            NativeBattleLogicalCommand? polygon = adjacent.SingleOrDefault(command => command.WordOffset == 2 && command.Opcode == (quad ? 0x2E : 0x26));
            NativeBattleLogicalCommand? reset = adjacent.SingleOrDefault(command => command.WordOffset == (quad ? 11 : 9) && command.Opcode == 0xE2 && command.Words.Length == 1 && command.Words[0] == 0xE2000000);
            if (window is null || polygon is null || reset is null)
            {
                return false;
            }
            Assert.Equal(0xE2000210u, BinaryPrimitives.ReadUInt32LittleEndian(process.ReadCommonPacket(offset + 4, 4)));
            Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(process.ReadCommonPacket(offset + (quad ? 44 : 36), 4)));
            Assert.Equal(new int[4], polygon.VertexPayload4.ToArray());
            Assert.Equal(Math.Max(17, basis.OrderingBucket!.Value - 1), polygon.OrderingBucket);
            return true;
        }

        byte[] Read(int offset, int length) => NativeBattleModelEnemyHeap.Read(cpu, prior.NativeFileStaging!, pointer with { Offset = pointer.Offset + offset }, length);
    }

    private sealed record Additional181Pair(NativeBattleLogicalCommand Base, NativeBattleLogicalCommand Overlay);

    private static List<Additional181Pair> Additional181Pairs(BattleSnapshot snapshot)
    {
        NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory!.PrimitiveProcess!;
        Dictionary<int, NativeBattleLogicalCommand> commands = [];
        foreach (var group in snapshot.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == process.PacketBank && item.Command.Packet.Buffer == 0 && item.Command.WordOffset == 1).GroupBy(item => item.Command.Packet.ByteOffset))
        {
            NativeBattleLogicalCommand command = group.First().Command;
            foreach (NativeBattleLogicalResolvedCommand replay in group)
            {
                Assert.Equal(command.Words.ToArray(), replay.Command.Words.ToArray());
                Assert.Equal(command.VertexPayload4.ToArray(), replay.Command.VertexPayload4.ToArray());
            }
            commands.Add(group.Key, command);
        }
        List<Additional181Pair> pairs = [];
        foreach (NativeBattleLogicalCommand command in commands.Values.Where(command => (command.Opcode & 0xFC) is 0x24 or 0x2C))
        {
            bool quad = (command.Opcode & 0xFC) == 0x2C;
            int allocation = quad ? 40 : 32;
            if (!commands.TryGetValue(command.Packet.ByteOffset + allocation, out NativeBattleLogicalCommand? overlay) || overlay.Opcode != (quad ? 0x2A : 0x22))
            {
                continue;
            }
            int[] xy = quad ? [1, 3, 5, 7] : [1, 3, 5];
            if (!xy.Select(index => command.Words[index]).SequenceEqual(overlay.Words.Skip(1)))
            {
                continue;
            }
            Assert.Equal((ushort)0, command.Flags20);
            Assert.Equal((ushort)0, overlay.Flags20);
            Assert.Equal(command.VertexPayload4.ToArray(), overlay.VertexPayload4.ToArray());
            NativeBattleGeometryRegisterStep step = new(snapshot.NativeEffectMemory.GeometryRegisters! with { Sz = command.VertexPayload4.Select(value => (uint?)unchecked((uint)value)).ToImmutableArray() }, snapshot.NativeEffectMemory.ScriptedColorRegisters!);
            if (!quad)
            {
                step = step with { Geometry = step.Geometry with { Sz = [0, (uint)command.VertexPayload4[0], (uint)command.VertexPayload4[1], (uint)command.VertexPayload4[2]] } };
            }
            NativeBattleScriptedGraphicsMemory controls = snapshot.NativeEffectMemory.ScriptedGraphics!;
            step = quad ? NativeBattleGeometry.Average45E610(step, controls.Zsf4!.Value) : NativeBattleGeometry.Average45E5C0(step, controls.Zsf3!.Value);
            int depth = unchecked((int)step.Geometry.Otz!.Value) >> 2;
            Assert.Equal((int?)(17 + depth), command.OrderingBucket);
            Assert.Equal((int?)(17 + Math.Max(0, depth - 1)), overlay.OrderingBucket);
            Assert.Equal((byte)(quad ? 9 : 7), process.ReadCommonPacket(command.Packet.ByteOffset + 3, 1)[0]);
            Assert.Equal((byte)(quad ? 5 : 4), process.ReadCommonPacket(overlay.Packet.ByteOffset + 3, 1)[0]);
            pairs.Add(new(command, overlay));
        }
        return pairs;
    }

    private static string Additional181Key(IEnumerable<uint> words, IEnumerable<int> depths, int? bucket)
    {
        uint[] data = words.ToArray();
        byte opcode = (byte)(data[0] >> 24);
        for (int word = 0; word < data.Length; word++)
        {
            data[word] &= Additional182ConsumedMask(opcode, word);
        }
        return string.Join(",", data) + ";" + string.Join(",", depths) + ";" + bucket;
    }

    private static object AssertAdditional181Body(BattleSnapshot prior, BattleSnapshot current, BattleAssets assets, int oldAge, List<Additional181Pair> pairs)
    {
        NativeBattleGraphicsModel model = prior.NativeGraphics!.Models[0];
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(prior.Definition.Combatants.Single(actor => actor.IsPlayer));
        Assert.Equal((int?)1, model.ActiveResourceRow100);
        Assert.Equal((int?)2, model.LinkedTextureResourceIndex);
        Assert.Null(model.ActiveStagedResourceRow100);
        Assert.Null(model.LinkedStagedResourceRow);
        NativeBattleCameraProjection projection = prior.NativeCamera!.Projection!;
        NativeBattlePackedMatrix32 camera = prior.NativeCamera.CapturePackedView1D97778();
        NativeBattleModelCpuResources cpu = prior.NativeModelCpuResources!;
        int scratchLength = cpu.ScratchMaximum!.Value * 8;
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleSummonColors colors = prior.Definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleModelRendererRegisters far = prior.NativeEffectMemory!.PrimitiveProcess!.ModelRendererRegisters! with { FarRed = 240u << 4, FarGreen = 96u << 4, FarBlue = 96u << 4 };
        uint cue = oldAge < 14 ? checked((uint)(oldAge * 292)) : 4096;
        NativeBattleScriptedDpcsResult color = NativeBattleScriptedDpcs.Transform45F270(prior.NativeEffectMemory.ScriptedColorRegisters!, 0, cue, far, colors);
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(prior.NativeFileStaging!, new(prior.NativeEffectMemory.GeometryRegisters!, prior.NativeEffectMemory.ScriptedColorRegisters!), model, projection, prior.NativeEffectMemory.ScriptedGraphics!, prior.Definition.NativeGraphics.Rules.ShadowSource!, prior.Definition.NativeGraphics.PrimitiveCapabilities!.GouraudAlpha, 4121, 16);
        NativeBattlePrimitiveProcess process = current.NativeEffectMemory!.PrimitiveProcess!;
        HashSet<string> commonCommands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == process.PacketBank).Select(item => Additional181Key(item.Command.Words, item.Command.VertexPayload4, item.Command.OrderingBucket)).ToHashSet();
        int shadowFaces = 0;
        foreach (NativeBattleGfGeometryAttempt attempt in shadow.Batch.Attempts)
        {
            if (attempt.Primitive is { } primitive)
            {
                Assert.Contains(Additional181Key(primitive.Words.Skip(1), primitive.VertexPayload4, primitive.Bucket), commonCommands);
                shadowFaces++;
            }
        }
        Dictionary<string, Queue<Additional181Pair>> available = pairs.GroupBy(pair => Additional181Key(pair.Base.Words, pair.Base.VertexPayload4, pair.Base.OrderingBucket)).ToDictionary(group => group.Key, group => new Queue<Additional181Pair>(group));
        List<object> sources = [];
        foreach (bool linked in new[] { false, true })
        {
            int row = linked ? model.LinkedTextureResourceIndex!.Value : model.ActiveResourceRow100!.Value;
            NativeBattleModelCpuReference pointer = cpu.Rows[row].Require(16);
            NativeBattleModelCpuReference end = cpu.Rows[row].Require(20);
            int length = end.Offset - pointer.Offset;
            Assert.Equal(linked ? 1764 : 16012, length);
            ImmutableArray<NativeBattleActionMatrix> local = linked ? model.CurrentLinkedBoneMatrices!.Matrices : NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource!);
            Assert.Equal(linked ? 2 : 23, local.Length);
            ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattlePoseMatrices.ComposeStageCamera(local, model.GeometryWorldMatrix!, 0).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 128).Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x1C, BitConverter.GetBytes(model.Color40!.Value)).Write(0x20, BitConverter.GetBytes(linked ? uint.MaxValue : model.SequenceState!.MeshMask124)).Write(0x5C, BitConverter.GetBytes(color.PackedRgb));
            NativeBattleAdditional181ModelStep expected = NativeBattleAdditional181ModelDrawing.DrawBody(prior.NativeEffectMemory, temporary, NativeBattleEffectArena.Clear().MarkUnknown(0, scratchLength), Read, pointer.PhysicalAddress, bones, camera, projection, prior.NativeEffectMemory.ScriptedGraphics!, scratchAddress, scratchLength);
            int matched = 0;
            for (int index = 0; index < expected.Batch.Attempts.Length; index++)
            {
                if (expected.Batch.Attempts[index].Primitive is not { } basis)
                {
                    continue;
                }
                NativeBattleGeometryPrimitive overlay = expected.Batch.Attempts[++index].Primitive!;
                string key = Additional181Key(basis.Words.Skip(1), basis.VertexPayload4, basis.Bucket);
                Assert.True(available.TryGetValue(key, out Queue<Additional181Pair>? matches) && matches.Count > 0, "The actual original current-body face and paired overlay did not match its canonical CPU bytes, current world/bones and prior camera.");
                Additional181Pair actual = matches!.Dequeue();
                Assert.Equal(Additional181Key(overlay.Words.Skip(1), overlay.VertexPayload4, overlay.Bucket), Additional181Key(actual.Overlay.Words, actual.Overlay.VertexPayload4, actual.Overlay.OrderingBucket));
                foreach ((NativeBattleGeometryPrimitive primitive, NativeBattleLogicalCommand command) in new[] { (basis, actual.Base), (overlay, actual.Overlay) })
                {
                    byte[] physical = current.NativeEffectMemory!.PrimitiveProcess!.ReadCommonPacket(command.Packet.ByteOffset + 4, (primitive.Words.Length - 1) * 4);
                    uint[] words = Enumerable.Range(0, primitive.Words.Length - 1).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(physical.AsSpan(word * 4, 4))).ToArray();
                    Assert.Equal(primitive.Words.Skip(1).ToArray(), words);
                }
                matched++;
            }
            Assert.True(matched > 0);
            int vertices = linked ? 78 : 478;
            for (int vertex = 0; vertex < vertices; vertex++)
            {
                Assert.Equal(new byte[2], expected.VertexScratch.Read(vertex * 8 + 6, 2));
            }
            sources.Add(new { Linked = linked, CpuRow = row, Pointer = pointer, GeometryBytes = length, Bones = local.Length, MatchedPhysicalFacePairs = matched, ScratchWriter = "XYZ from low IR words with physical W0, verified through matched projected XY/SZ packets; later ordinary model scratch is a different writer" });
            byte[] Read(int offset, int count) => NativeBattleModelEnemyHeap.Read(cpu, prior.NativeFileStaging!, pointer with { Offset = checked(pointer.Offset + offset) }, count);
        }
        NativeBattleGraphicsModel restored = current.NativeGraphics!.Models[0];
        NativeBattleDecodedPose bodyPose = NativeBattlePoseMatrices.ReadDecodedPose(restored.SequenceState!, resource.PoseSource!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource!, bodyPose)), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(restored, resource.PoseSource!)));
        NativeBattleDecodedPose linkedPose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(restored.SequenceState!, resource.LinkedPoseSource!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.LinkedPoseSource!, linkedPose)), JsonSerializer.Serialize(restored.CurrentLinkedBoneMatrices!.Matrices));
        Assert.NotNull(restored.GeometryWorldMatrix);
        return new { OldAge = oldAge, Cue = cue, OverlayColor = color.PackedRgb, MatchedShadowFaces = shadowFaces, Sources = sources, Restore = "Current decoded local body and linked bones restored after shadow, world composition and paired COMMON delivery; whole-frame GTE/FAR is not attributed to this callback" };
    }

    private static NativeBattleModelRendererRegisters Additional182Far(uint rgb, NativeBattleModelRendererRegisters far) => far with { FarRed = (rgb & 255) << 4, FarGreen = ((rgb >> 8) & 255) << 4, FarBlue = ((rgb >> 16) & 255) << 4 };

    private static uint Additional182ConsumedMask(byte opcode, int word) => (opcode & 0xFC, word) switch
    {
        (0x24, 6) => 0xFFFFu,
        (0x2C, 6 or 8) => 0xFFFFu,
        (0x34, 8) => 0xFFFFu,
        (0x3C, 8 or 11) => 0xFFFFu,
        _ => uint.MaxValue
    };

    private static string Blaster186FrameWordLanes(NativeBattleBlaster186State state)
    {
        List<object> frames = [];
        foreach (uint address in NativeBattleBlaster186Profile.MutableFrameAddresses)
        {
            (byte[] bytes, bool[] known) = state.ReadView(address, NativeBattleBlaster186Profile.VertexFrameBytes).ReadSnapshot(0, NativeBattleBlaster186Profile.VertexFrameBytes);
            byte[] words = Enumerable.Range(0, 102).SelectMany(index => bytes.Skip(index * 8 + 6).Take(2)).ToArray();
            bool[] masks = Enumerable.Range(0, 102).SelectMany(index => known.Skip(index * 8 + 6).Take(2)).ToArray();
            frames.Add(new { Address = address, Data = words, Known = masks });
        }
        return JsonSerializer.Serialize(frames);
    }

    private static string Cannon151WordLanes(NativeBattleCannonBlow151State state)
    {
        List<object> lanes = [];
        for (int index = 0; index < NativeBattleCannonBlow151Profile.SampleCount; index++)
        {
            var lane = state.ReadView(NativeBattleCannonBlow151Profile.SamplesAddress + (uint)(index * 8 + 6), 2).ReadSnapshot(0, 2);
            lanes.Add(new { lane.Data, lane.Known });
        }
        return JsonSerializer.Serialize(lanes);
    }

    private static string Cannon156WordLanes(NativeBattleWildCannonBlow156State state, uint address, int count)
    {
        List<object> lanes = [];
        for (int index = 0; index < count; index++)
        {
            var lane = state.ReadView(checked(address + (uint)(index * 8 + 6)), 2).ReadSnapshot(0, 2);
            lanes.Add(new { lane.Data, lane.Known });
        }
        return JsonSerializer.Serialize(lanes);
    }

    private static string Sand155WordLanes(NativeBattleSandStorm155State state, uint address, int count)
    {
        List<object> lanes = [];
        for (int index = 0; index < count; index++)
        {
            var lane = state.ReadView(checked(address + (uint)(index * 8 + 6)), 2).ReadSnapshot(0, 2);
            lanes.Add(new { lane.Data, lane.Known });
        }
        return JsonSerializer.Serialize(lanes);
    }

    private static string StageColors(BattleSnapshot snapshot) => JsonSerializer.Serialize(snapshot.NativeStage!.Groups.Select(group => new { group.ColorFlags, group.ColorDepth }));

    private static bool CrossedContactGate(BattleSnapshot prior, BattleSnapshot current, ushort effect)
    {
        if (effect == 79)
        {
            NativeBattleOriginalEffectState? before79 = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? after79 = current.NativeGraphics!.OriginalEffect;
            return before79?.Pools.Length == 10 && after79?.Pools.Length == 10 && prior.NativeEffectMemory!.Effect79!.ReadWord(NativeBattleEffect79Profile.TargetAddress + 0x48) == 1 && before79.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x7B4FF0 && item.task.ReadByte(0x29) == 1 && after79.Pools[1].Slots[item.slot].Callback == 0x7B4FF0 && after79.Pools[1].Slots[item.slot].ReadByte(0x29) == 2);
        }
        if (effect == 83)
        {
            NativeBattleOriginalEffectState? before83 = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? after83 = current.NativeGraphics!.OriginalEffect;
            return before83?.Pools.Length == 8 && after83?.Pools.Length == 8 && prior.NativeEffectMemory!.Effect83!.ReadWord(NativeBattleEffect83Profile.TargetAddress + 0x48) == 1 && before83.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x797A90 && item.task.ReadByte(0x29) == 1 && after83.Pools[1].Slots[item.slot].Callback == 0x797A90 && after83.Pools[1].Slots[item.slot].ReadByte(0x29) == 2);
        }
        if (effect == 289)
        {
            NativeBattleOriginalEffectState? before289 = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? after289 = current.NativeGraphics!.OriginalEffect;
            return before289?.Pools.Length == 4 && after289?.Pools.Length == 4 && before289.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleEffect289Profile.MainCallback && item.task.ReadWord(12) == 15 && after289.Pools[1].Slots[item.slot].Callback == NativeBattleEffect289Profile.MainCallback && after289.Pools[1].Slots[item.slot].ReadWord(12) == 16);
        }
        if (effect == 315)
        {
            NativeBattleOriginalEffectState? before315 = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? after315 = current.NativeGraphics!.OriginalEffect;
            return before315?.Pools.Length == 2 && after315?.Pools.Length == 2 && before315.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x598E70 && unchecked((short)item.task.ReadWord(0xC)) == 112 && after315.Pools[1].Slots[item.slot].Callback == 0x598E70 && unchecked((short)after315.Pools[1].Slots[item.slot].ReadWord(0xC)) == 113);
        }
        if (effect is 283 or 303)
        {
            return false;
        }
        if (effect == 279)
        {
            NativeBattleOriginalEffectState? beforeSpark = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? afterSpark = current.NativeGraphics!.OriginalEffect;
            return beforeSpark?.Pools.Length == 7 && afterSpark?.Pools.Length == 7 && (beforeSpark.Pools[6].Slots[0].Flags & 1) != 0 && beforeSpark.Pools[6].Slots[0].ReadWord(12) == 58 && afterSpark.Pools[6].Slots[0].ReadWord(12) == 59;
        }
        if (effect == 304)
        {
            NativeBattleOriginalEffectState? beforeSword = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? afterSword = current.NativeGraphics!.OriginalEffect;
            return beforeSword?.Pools.Length == 5 && afterSword?.Pools.Length == 5 && (beforeSword.Pools[1].Slots[0].Flags & 1) != 0 && (afterSword.Pools[1].Slots[0].Flags & 1) != 0 && beforeSword.Pools[1].Slots[0].ReadWord(16) < afterSword.Pools[1].Slots[0].ReadWord(16);
        }
        if (effect == 302)
        {
            NativeBattleOriginalEffectState? beforeLaser = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? afterLaser = current.NativeGraphics!.OriginalEffect;
            return beforeLaser?.Pools.Length == 5 && afterLaser?.Pools.Length == 5 && beforeLaser.Pools[4].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.ReadWord(14) == 0 && item.task.ReadWord(12) == 43 && (afterLaser.Pools[4].Slots[item.slot].Flags & 1) != 0 && afterLaser.Pools[4].Slots[item.slot].ReadWord(12) == 44);
        }
        if (effect == 284)
        {
            NativeBattleOriginalEffectState? beforeMissiles = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? afterMissiles = current.NativeGraphics!.OriginalEffect;
            return beforeMissiles?.Pools.Length == 4 && beforeMissiles.Pools[0].Slots[0].ReadByte(15) == 1 && (afterMissiles?.ReturnedDescriptor == 0 || afterMissiles?.Pools.Length == 4 && afterMissiles.Pools[0].Slots[0].ReadByte(15) == 2);
        }
        if (effect == 286)
        {
            NativeBattleOriginalEffectState? cannonBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? cannonAfter = current.NativeGraphics!.OriginalEffect;
            return cannonBefore?.Pools.Length == 3 && cannonAfter?.Pools.Length == 3 && (cannonBefore.Pools[2].Slots[0].Flags & 1) != 0 && cannonBefore.Pools[2].Slots[0].ReadWord(12) == 53 && cannonAfter.Pools[2].Slots[0].ReadWord(12) == 54 && (cannonAfter.Pools[2].Slots[0].Flags & 1) != 0;
        }
        if (effect == 282)
        {
            NativeBattleOriginalEffectState? eyeBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? eyeAfter = current.NativeGraphics!.OriginalEffect;
            return eyeBefore?.Pools.Length == 4 && eyeAfter?.Pools.Length == 4 && (eyeBefore.Pools[3].Slots[0].Flags & 1) != 0 && eyeBefore.Pools[3].Slots[0].ReadWord(12) == 48 && eyeAfter.Pools[3].Slots[0].ReadWord(12) == 49 && (eyeAfter.Pools[3].Slots[0].Flags & 1) == 0;
        }
        if (effect == 281)
        {
            NativeBattleOriginalEffectState? shotgunBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? shotgunAfter = current.NativeGraphics!.OriginalEffect;
            return shotgunBefore?.Pools.Length == 4 && shotgunAfter?.Pools.Length == 4 && (shotgunBefore.Pools[3].Slots[0].Flags & 1) != 0 && shotgunBefore.Pools[3].Slots[0].ReadWord(12) == 13 && shotgunAfter.Pools[3].Slots[0].ReadWord(12) == 14 && (shotgunAfter.Pools[3].Slots[0].Flags & 1) == 0;
        }
        if (effect == 214)
        {
            NativeBattleOriginalEffectState? gravityBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? gravityAfter = current.NativeGraphics!.OriginalEffect;
            return gravityBefore?.Pools.Length == 3 && gravityAfter?.Pools.Length == 3 && gravityBefore.Pools[2].Slots[0].ReadWord(12) == 67 && gravityAfter.Pools[2].Slots[0].ReadWord(12) == 68;
        }
        if (effect == 44)
        {
            NativeBattleOriginalEffectState? beforeJunk = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? afterJunk = current.NativeGraphics!.OriginalEffect;
            if (beforeJunk?.Pools.Length != 5 || afterJunk?.Pools.Length != 5)
            {
                return false;
            }
            return beforeJunk.Pools[2].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x6E84D0 && item.task.ReadDword(0x30) != 0 && afterJunk.Pools[2].Slots[item.slot].ReadDword(0x30) == 0);
        }
        if (effect == 45)
        {
            NativeBattleOriginalEffectState? stareBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? stareAfter = current.NativeGraphics!.OriginalEffect;
            if (stareBefore?.Pools.Length != 6 || stareAfter?.Pools.Length != 6 || prior.NativeEffectMemory!.Stare45!.ReadWord(NativeBattleStare45Profile.EffectObjectAddress + 0x48) != 1)
            {
                return false;
            }
            return stareBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x836990 && item.task.ReadByte(0x29) == 1 && stareAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && stareAfter.Pools[1].Slots[item.slot].ReadByte(0x29) == 2);
        }
        if (effect == 46)
        {
            NativeBattleOriginalEffectState? sighBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? sighAfter = current.NativeGraphics!.OriginalEffect;
            if (sighBefore?.Pools.Length != 6 || sighAfter?.Pools.Length != 6)
            {
                return false;
            }
            return sighBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x831FE0 && item.task.ReadByte(0x29) == 2 && unchecked((short)item.task.ReadWord(0x24)) >= 40 && sighAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && sighAfter.Pools[1].Slots[item.slot].ReadByte(0x29) == 3 && (sighAfter.Pools[1].Slots[item.slot].ReadByte(0x26) & 1) != 0);
        }
        if (effect == 48)
        {
            NativeBattleOriginalEffectState? magmaBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? magmaAfter = current.NativeGraphics!.OriginalEffect;
            if (magmaBefore?.Pools.Length != 6 || magmaAfter?.Pools.Length != 6)
            {
                return false;
            }
            return magmaBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x825170 && item.task.ReadByte(0x29) == 2 && unchecked((short)item.task.ReadWord(0x24)) >= 55 && magmaAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && magmaAfter.Pools[1].Slots[item.slot].ReadByte(0x29) == 3 && (magmaAfter.Pools[1].Slots[item.slot].ReadByte(0x26) & 1) != 0 && magmaAfter.Pools[1].Slots[item.slot].ReadWord(0x24) == unchecked((ushort)(item.task.ReadWord(0x24) + 1)));
        }
        if (effect == 49)
        {
            NativeBattleOriginalEffectState? resonanceBefore = prior.NativeGraphics!.OriginalEffect;
            NativeBattleOriginalEffectState? resonanceAfter = current.NativeGraphics!.OriginalEffect;
            if (resonanceBefore?.Pools.Length != 6 || resonanceAfter?.Pools.Length != 6)
            {
                return false;
            }
            return resonanceBefore.Pools[4].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x824970 && item.task.ReadByte(0x29) == 4 && prior.NativeGraphics.Models[item.task.ReadByte(0x2D)].Sequence != 6 && prior.NativeEffectMemory!.Resonance49!.ReadWord(0x26478E6) >= 75 && resonanceAfter.Pools[4].Slots[item.slot].ReadByte(0x29) == 5);
        }
        if (effect is 298 or 172)
        {
            return false;
        }
        if (effect == 129)
        {
            NativeBattleEffectTaskPool? before129 = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBreath129Profile.TargetDescriptor);
            NativeBattleEffectTaskPool? after129 = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBreath129Profile.TargetDescriptor);
            if (before129 is null || after129 is null)
            {
                return false;
            }
            return before129.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleBreath129Profile.TargetCallback && item.task.ReadWord(12) == 16 && after129.Slots[item.slot].Callback == item.task.Callback && after129.Slots[item.slot].ReadWord(12) == 17);
        }
        if (effect == 139)
        {
            NativeBattleEffectTaskPool? sonicBefore = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSonic139Profile.LowerDescriptor);
            NativeBattleEffectTaskPool? sonicAfter = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSonic139Profile.LowerDescriptor);
            if (sonicBefore is null || sonicAfter is null)
            {
                return false;
            }
            return sonicBefore.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleSonic139Profile.TargetCallback && item.task.ReadWord(12) >= 20 && sonicAfter.Slots[item.slot].Callback == item.task.Callback && (sonicAfter.Slots[item.slot].Flags & 1) == 0);
        }
        if (effect == 12)
        {
            NativeBattleEffectTaskPool? oldPool = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBladeShot12Profile.RootDescriptor);
            NativeBattleEffectTaskPool? newPool = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBladeShot12Profile.RootDescriptor);
            return oldPool is not null && newPool is not null && oldPool.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleBladeShot12Profile.MainCallback && item.task.ReadWord(12) == 32 && newPool.Slots[item.slot].Callback == item.task.Callback && newPool.Slots[item.slot].ReadWord(12) == 33);
        }
        if (effect == 170)
        {
            NativeBattleEffectTaskPool? oldPool = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAcid170Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? newPool = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAcid170Profile.ChildDescriptor);
            return oldPool is not null && newPool is not null && oldPool.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleAcid170Profile.MainCallback && item.task.ReadWord(12) == 46 && newPool.Slots[item.slot].Callback == item.task.Callback && newPool.Slots[item.slot].ReadWord(12) == 47);
        }
        if (effect == 171)
        {
            NativeBattleEffectTaskPool? oldPool = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattlePoisonGas171Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? newPool = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattlePoisonGas171Profile.ChildDescriptor);
            return oldPool is not null && newPool is not null && oldPool.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattlePoisonGas171Profile.MainCallback && item.task.ReadWord(12) == 30 && newPool.Slots[item.slot].Callback == item.task.Callback && newPool.Slots[item.slot].ReadWord(12) == 31);
        }
        if (effect == 181)
        {
            NativeBattleEffectTaskPool? oldPool = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAdditional181Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? newPool = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAdditional181Profile.ChildDescriptor);
            return oldPool is not null && newPool is not null && oldPool.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleAdditional181Profile.MainCallback && item.task.ReadWord(12) == 38 && newPool.Slots[item.slot].Callback == item.task.Callback && newPool.Slots[item.slot].ReadWord(12) == 39);
        }
        if (effect == 182)
        {
            NativeBattleEffectTaskPool? oldPool = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAdditional182Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? newPool = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAdditional182Profile.ChildDescriptor);
            if (oldPool is null || newPool is null)
            {
                return false;
            }
            return oldPool.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && (item.task.Callback == NativeBattleAdditional182Profile.MainSetCallback && item.task.ReadWord(12) == 35 || item.task.Callback == NativeBattleAdditional182Profile.MainClearCallback && item.task.ReadWord(12) == 64) && newPool.Slots[item.slot].Callback == item.task.Callback && newPool.Slots[item.slot].ReadWord(12) == item.task.ReadWord(12) + 1);
        }
        if (effect == 186)
        {
            NativeBattleEffectTaskPool? before186 = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBlaster186Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? after186 = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBlaster186Profile.ChildDescriptor);
            if (before186 is null || after186 is null)
            {
                return false;
            }
            return before186.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleBlaster186Profile.MainCallback && item.task.ReadWord(12) == 60 && after186.Slots[item.slot].Callback == item.task.Callback && after186.Slots[item.slot].ReadWord(12) == 61);
        }
        if (effect == 151)
        {
            NativeBattleEffectTaskPool? before151 = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleCannonBlow151Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? after151 = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleCannonBlow151Profile.ChildDescriptor);
            if (before151 is null || after151 is null)
            {
                return false;
            }
            return before151.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleCannonBlow151Profile.MainCallback && item.task.ReadWord(12) == 30 && after151.Slots[item.slot].Callback == item.task.Callback && after151.Slots[item.slot].ReadWord(12) == 31);
        }
        if (effect == 190)
        {
            NativeBattleEffectTaskPool? before190 = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleChainGun190Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? after190 = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleChainGun190Profile.ChildDescriptor);
            if (before190 is null || after190 is null)
            {
                return false;
            }
            return before190.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleChainGun190Profile.MainCallback && item.task.ReadWord(12) == 30 && after190.Slots[item.slot].Callback == item.task.Callback && after190.Slots[item.slot].ReadWord(12) == 31);
        }
        if (effect == 150)
        {
            NativeBattleEffectTaskPool? before150 = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleGatlingGun150Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? after150 = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleGatlingGun150Profile.ChildDescriptor);
            if (before150 is null || after150 is null)
            {
                return false;
            }
            return before150.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleGatlingGun150Profile.MainCallback && item.task.ReadWord(12) == 30 && after150.Slots[item.slot].Callback == item.task.Callback && after150.Slots[item.slot].ReadWord(12) == 31);
        }
        if (effect == 183)
        {
            NativeBattleEffectTaskPool? before183 = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleEffect183Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? after183 = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleEffect183Profile.ChildDescriptor);
            if (before183 is null || after183 is null)
            {
                return false;
            }
            return before183.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleEffect183Profile.MainCallback && item.task.ReadWord(12) == 30 && after183.Slots[item.slot].Callback == item.task.Callback && after183.Slots[item.slot].ReadWord(12) == 31);
        }
        if (effect == 131)
        {
            return BadBreath131CrossedContactGate(prior, current);
        }
        if (effect == 132)
        {
            NativeBattleEffectTaskPool? acid132Before = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleDisolvingAcid132Profile.MainDescriptor);
            NativeBattleEffectTaskPool? acid132After = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleDisolvingAcid132Profile.MainDescriptor);
            if (acid132Before is null || acid132After is null)
            {
                return false;
            }
            return acid132Before.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleDisolvingAcid132Profile.MainCallback && item.task.ReadWord(12) == 45 && acid132After.Slots[item.slot].Callback == item.task.Callback && (acid132After.Slots[item.slot].Flags & 1) == 0);
        }
        if (effect == 157)
        {
            NativeBattleEffectTaskPool? beforeBreath = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBreath157Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? afterBreath = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBreath157Profile.ChildDescriptor);
            if (beforeBreath is null || afterBreath is null)
            {
                return false;
            }
            return beforeBreath.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleBreath157Profile.DirectorCallback && item.task.ReadWord(12) == 65 && afterBreath.Slots[item.slot].Callback == item.task.Callback && afterBreath.Slots[item.slot].ReadWord(12) == 66);
        }
        if (effect == 156)
        {
            NativeBattleEffectTaskPool? beforeCannon = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleWildCannonBlow156Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? afterCannon = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleWildCannonBlow156Profile.ChildDescriptor);
            if (beforeCannon is null || afterCannon is null)
            {
                return false;
            }
            return beforeCannon.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleWildCannonBlow156Profile.DirectorCallback && item.task.ReadWord(12) == 36 && afterCannon.Slots[item.slot].Callback == item.task.Callback && afterCannon.Slots[item.slot].ReadWord(12) == 37);
        }
        if (effect == 155)
        {
            NativeBattleEffectTaskPool? beforeVertex = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSandStorm155Profile.ChildDescriptor);
            NativeBattleEffectTaskPool? afterVertex = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSandStorm155Profile.ChildDescriptor);
            if (beforeVertex is null || afterVertex is null)
            {
                return false;
            }
            return beforeVertex.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleSandStorm155Profile.DirectorCallback && item.task.ReadWord(12) == 50 && afterVertex.Slots[item.slot].Callback == item.task.Callback && afterVertex.Slots[item.slot].ReadWord(12) == 51);
        }
        if (effect == 128)
        {
            NativeBattleEffectTaskPool? gastric128Before = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleGastricJuice128Profile.MainDescriptor);
            NativeBattleEffectTaskPool? gastric128After = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleGastricJuice128Profile.MainDescriptor);
            if (gastric128Before is null || gastric128After is null)
            {
                return false;
            }
            return gastric128Before.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleGastricJuice128Profile.MainCallback && item.task.ReadWord(12) == 53 && gastric128After.Slots[item.slot].Callback == item.task.Callback && gastric128After.Slots[item.slot].ReadWord(12) == 54);
        }
        if (effect == 136)
        {
            NativeBattleEffectTaskPool? oilShotBefore = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleOilShot136Profile.MainDescriptor);
            NativeBattleEffectTaskPool? oilShotAfter = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleOilShot136Profile.MainDescriptor);
            if (oilShotBefore is null || oilShotAfter is null)
            {
                return false;
            }
            return oilShotBefore.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleOilShot136Profile.MainCallback && item.task.ReadWord(12) == 62 && oilShotAfter.Slots[item.slot].Callback == item.task.Callback && oilShotAfter.Slots[item.slot].ReadWord(12) == 63);
        }
        if (effect == 138)
        {
            NativeBattleEffectTaskPool? salivaBefore = prior.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSaliva138Profile.MainDescriptor);
            NativeBattleEffectTaskPool? salivaAfter = current.NativeGraphics!.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleSaliva138Profile.MainDescriptor);
            if (salivaBefore is null || salivaAfter is null)
            {
                return false;
            }
            return salivaBefore.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleSaliva138Profile.MainCallback && item.task.ReadWord(12) == 40 && salivaAfter.Slots[item.slot].Callback == item.task.Callback && salivaAfter.Slots[item.slot].ReadWord(12) == 41);
        }
        if (effect == 137)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Pools.Length: 4 } oilBefore || current.NativeGraphics!.OriginalEffect is not { Pools.Length: 4 } oilAfter)
            {
                return false;
            }
            return oilBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleOilBlast137Profile.MainCallback && item.task.ReadWord(12) == 45 && oilAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && oilAfter.Pools[1].Slots[item.slot].ReadWord(12) == 46);
        }
        if (effect == 158)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Pools.Length: 2 } meltEyeBefore || current.NativeGraphics!.OriginalEffect is not { Pools.Length: 2 } meltEyeAfter)
            {
                return false;
            }
            return meltEyeBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleMeltEye158Profile.SpawnerCallback && item.task.ReadWord(12) == 40 && meltEyeAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && meltEyeAfter.Pools[1].Slots[item.slot].ReadWord(12) == 41);
        }
        if (effect == 217)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Pools.Length: 3 } energyBomberBefore || current.NativeGraphics!.OriginalEffect is not { Pools.Length: 3 } energyBomberAfter)
            {
                return false;
            }
            return energyBomberBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleEnergyBomber217Profile.MainCallback && item.task.ReadWord(12) == 78 && energyBomberAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && energyBomberAfter.Pools[1].Slots[item.slot].ReadWord(12) == 79);
        }
        if (effect == 101)
        {
            return false;
        }
        if (effect == 134)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Pools.Length: 5 } beamLaserBefore || current.NativeGraphics!.OriginalEffect is not { Pools.Length: 5 } beamLaserAfter)
            {
                return false;
            }
            return beamLaserBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleBeamLaser134Profile.MainCallback && item.task.ReadWord(12) == 26 && beamLaserAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && beamLaserAfter.Pools[1].Slots[item.slot].ReadWord(12) == 27);
        }
        if (effect == 133)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Pools.Length: 5 } hypnotizeBefore || current.NativeGraphics!.OriginalEffect is not { Pools.Length: 5 } hypnotizeAfter)
            {
                return false;
            }
            return hypnotizeBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleHypnotize133Profile.MainCallback && item.task.ReadWord(12) == 34 && hypnotizeAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && hypnotizeAfter.Pools[1].Slots[item.slot].ReadWord(12) == 35);
        }
        if (effect is 177 or 178)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Pools.Length: 2 } needlesBefore || current.NativeGraphics!.OriginalEffect is not { Pools.Length: 2 } needlesAfter)
            {
                return false;
            }
            NativeBattleNeedlesFamilyProfile needlesProfile = NativeBattleNeedlesFamilyProfile.ForEffect(effect);
            ushort needlesContactAge = effect == 177 ? (ushort)50 : (ushort)85;
            return needlesBefore.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == needlesProfile.SchedulerCallback && item.task.ReadWord(12) == needlesContactAge && needlesAfter.Pools[1].Slots[item.slot].Callback == item.task.Callback && needlesAfter.Pools[1].Slots[item.slot].ReadWord(12) == needlesContactAge + 1);
        }
        if (effect == 71)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Pools.Length: 6 } beforeLaser || current.NativeGraphics!.OriginalEffect is not { Pools.Length: 6 } afterLaser)
            {
                return false;
            }
            return beforeLaser.Pools[1].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleLaserEye71Controller.TargetCallback && item.task.ReadByte(0x29) == 2 && unchecked((short)item.task.ReadWord(0x24)) >= 25 && afterLaser.Pools[1].Slots[item.slot].Callback == item.task.Callback && afterLaser.Pools[1].Slots[item.slot].ReadByte(0x29) == 3);
        }
        if (effect == 130)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } eerieBefore || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } eerieAfter || eerieBefore.Pools.Length != 4 || eerieAfter.Pools.Length != 4)
            {
                return false;
            }
            NativeBattleGfAnimationSource eerieSource = eerieAfter.Binding.EerieSoundWave130Resources!.ImpactAct;
            int eerieAnimationOffset = BinaryPrimitives.ReadInt32LittleEndian(eerieSource.Data.AsSpan().Slice(4));
            int eerieDuration = BinaryPrimitives.ReadInt32LittleEndian(eerieSource.Data.AsSpan().Slice(eerieAnimationOffset + 4));
            return eerieBefore.Pools[3].Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleEerieSoundWave130Profile.ImpactCallback && item.task.ReadDword(0x14) == 0x1D972C0 && item.task.ReadDword(0x18) == NativeBattleEerieSoundWave130Profile.ImpactActAddress && eerieDuration - unchecked((int)item.task.ReadDword(0x1C)) == 9 && eerieAfter.Pools[3].Slots[item.slot].Callback == item.task.Callback && eerieDuration - unchecked((int)eerieAfter.Pools[3].Slots[item.slot].ReadDword(0x1C)) == 8);
        }
        if (effect == 127)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } beforeStare || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } afterStare || beforeStare.Pools.Length != 2 || afterStare.Pools.Length != 2)
            {
                return false;
            }
            NativeBattleModulePoolTask previous = beforeStare.Pools[1].Slots[0];
            NativeBattleModulePoolTask next = afterStare.Pools[1].Slots[0];
            return (previous.Flags & 1) != 0 && previous.Callback == NativeBattlePetrifyStare127Profile.TargetCallback && unchecked((short)previous.ReadWord(12)) == 42 && previous.ReadWord(14) == 0 && next.Callback == previous.Callback && unchecked((short)next.ReadWord(12)) == 43;
        }
        if (effect == 260)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } beforePulsar || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } afterPulsar || beforePulsar.Pools.Length != 3 || afterPulsar.Pools.Length != 3)
            {
                return false;
            }
            NativeBattleModulePoolTask previous = beforePulsar.Pools[1].Slots[0];
            NativeBattleModulePoolTask next = afterPulsar.Pools[1].Slots[0];
            return (previous.Flags & 1) != 0 && previous.Callback == NativeBattleShockwavePulsar260Profile.MainCallback && previous.ReadWord(12) == 252 && next.Callback == previous.Callback && next.ReadWord(12) == 253;
        }
        if (effect == 307)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } rayBefore || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } rayAfter)
            {
                return false;
            }
            NativeBattleEffectTaskPool previous = rayBefore.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRayBomb307Profile.ChildDescriptor);
            NativeBattleEffectTaskPool next = rayAfter.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRayBomb307Profile.ChildDescriptor);
            return previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x5A25E0 && item.task.ReadWord(12) == 31 && item.task.ReadWord(0x1A) == 0 && next.Slots[item.slot].Callback == 0x5A25E0 && next.Slots[item.slot].ReadWord(12) == 32);
        }
        if (effect == 184)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } wavesBefore || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } wavesAfter)
            {
                return false;
            }
            NativeBattleEffectTaskPool previous = wavesBefore.Pools.Single(pool => pool.DescriptorAddress == NativeBattleUltraWaves184Profile.ChildDescriptor);
            NativeBattleEffectTaskPool next = wavesAfter.Pools.Single(pool => pool.DescriptorAddress == NativeBattleUltraWaves184Profile.ChildDescriptor);
            return previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleUltraWaves184Profile.MainCallback && item.task.ReadWord(12) == 41 && next.Slots[item.slot].Callback == NativeBattleUltraWaves184Profile.MainCallback && next.Slots[item.slot].ReadWord(12) == 42);
        }
        if (effect == 135)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } reflectBefore || current.NativeGraphics!.OriginalEffect is not { } reflectAfter)
            {
                return false;
            }
            NativeBattleEffectTaskPool? previous = reflectBefore.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleReflectBeam135Profile.TargetDescriptor);
            NativeBattleEffectTaskPool? next = reflectAfter.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleReflectBeam135Profile.TargetDescriptor);
            return previous is not null && next is not null && previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleReflectBeam135Profile.TargetCallback && item.task.ReadWord(12) == 25 && item.slot < next.Slots.Length && (next.Slots[item.slot].Flags & 1) != 0 && next.Slots[item.slot].Callback == NativeBattleReflectBeam135Profile.TargetCallback && next.Slots[item.slot].ReadWord(12) == 26);
        }
        if (effect == 126)
        {
            if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } dischargeBefore || current.NativeGraphics!.OriginalEffect is not { } dischargeAfter)
            {
                return false;
            }
            NativeBattleEffectTaskPool? previous = dischargeBefore.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleElectricDischarge126Profile.MainDescriptor);
            NativeBattleEffectTaskPool? next = dischargeAfter.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleElectricDischarge126Profile.MainDescriptor);
            return previous is not null && next is not null && previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleElectricDischarge126Profile.MainCallback && unchecked((short)item.task.ReadWord(12)) >= 52 && item.slot < next.Slots.Length && (next.Slots[item.slot].Flags & 1) == 0);
        }
        if (effect == 275)
        {
            return Electrocute275CrossedContactGate(prior, current);
        }
        if (effect == 317)
        {
            return Acid317CrossedContactGate(prior, current);
        }
        if (effect == 305)
        {
            return GatlingGun305CrossedContactGate(prior, current);
        }
        if (effect == 306)
        {
            return Degenerator306CrossedContactGate(prior, current);
        }
        if (effect == 300)
        {
            return BadBreath300CrossedContactGate(prior, current);
        }
        if (effect == 299)
        {
            return MicroMissiles299CrossedContactGate(prior, current);
        }
        if (effect == 297)
        {
            return WhiteWind297CrossedContactGate(prior, current);
        }
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } after)
        {
            return false;
        }
        uint callback = effect == 74 ? NativeBattleDeathFamilyProfile.ForEffect(74).Callback(0x8B6C90) : effect == 47 ? NativeBattleCurse47Controller.TargetCallback : effect == 8 ? NativeBattleDoom8Controller.TargetCallback : effect == 56 ? NativeBattleDrainFamilyProfile.ForEffect(56).TargetCallback : effect == 62 ? NativeBattleDevour62Profile.TargetCallback : effect == 43 ? NativeBattleMeltingBubble43Profile.TargetCallback : effect == 29 ? NativeBattleWindBlast29Profile.TargetCallback : effect == 60 ? NativeBattleEffect60Profile.TargetCallback : effect == 23 ? NativeBattlePsychoBlast23Profile.TargetCallback : effect == 20 ? NativeBattleEffect20Profile.TargetCallback : effect == 19 ? NativeBattleRaldo19Profile.TargetCallback : effect == 37 ? NativeBattleDrinkMagic37Profile.TargetCallback : effect is 73 or 78 ? NativeBattleMightyGuardProfile.ForEffect(effect).TargetCallback : NativeBattleActorDirectorProfile.ForEffect(effect).Callback(0x8AB4C0);
        byte phase = effect is 20 or 29 or 37 or 43 or 56 or 62 or 73 or 78 ? (byte)2 : (byte)1;
        bool crossed = before.Pools[1].Slots.Select((task, index) => (task, index)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == callback && item.task.ReadByte(0x29) == phase && after.Pools[1].Slots[item.index].ReadByte(0x29) == phase + 1);
        if (effect == 74 && crossed)
        {
            NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(74);
            Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(prior.NativeEffectMemory!.LevelDeath74!.Module.Read(profile.ModuleOffset(0x2742440), 2)));
        }
        if (effect == 8 && crossed)
        {
            Assert.Equal((ushort)1, prior.NativeEffectMemory!.Doom8!.SharedWord(0x48));
        }
        if (effect == 47 && crossed)
        {
            Assert.Equal((ushort)1, prior.NativeEffectMemory!.Curse47!.SharedWord(0x48));
        }
        if (effect == 19 && crossed)
        {
            Assert.Equal((short)1, prior.NativeEffectMemory!.RequireRaldo19().Word(0x48));
        }
        if (effect == 23 && crossed)
        {
            Assert.Equal((short)1, prior.NativeEffectMemory!.RequirePsychoBlast23().Word(0x48));
        }
        if (effect == 60 && crossed)
        {
            Assert.Equal((short)1, prior.NativeEffectMemory!.RequireEffect60().Word(0x48));
        }
        return crossed;
    }

    private static NativeBattleGraphicsResource ResolveShockwavePulsar260Resource(BattleSnapshot snapshot, NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        uint binding = snapshot.NativeEffectMemory!.ShockwavePulsar260!.ReadDword(NativeBattleShockwavePulsar260Profile.WorkingGlobal) + 0x1C0;
        return ResolveBlueBindingResource(snapshot, model, resource, binding, address => NativeBattleEffectTaskMemory.TryReadPointer(snapshot.NativeGraphics!.OriginalEffect!.Pools, address, out NativeBattleResourcePointer? pointer) ? pointer : snapshot.NativeEffectMemory.Arena!.ReadResourcePointer(checked((int)(address - NativeBattleShockwavePulsar260Profile.ArenaAddress))));
    }

    private static NativeBattleEffectArena ShockwavePulsar260SavedPosition(NativeBattleModulePoolTask main)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, main.Payload.AsSpan().Slice(0x14 - 12, 8));
        foreach (NativeBattleEffectUnknownRegion range in main.UnknownPayloadRanges ?? [])
        {
            int first = Math.Max(0x14, range.Offset);
            int end = Math.Min(0x1C, range.Offset + range.Length);
            if (first < end)
            {
                result = result.MarkUnknown(first - 0x14, end - first);
            }
        }
        return result;
    }

    private static void AssertShockwavePulsar260Position(BattleSnapshot snapshot, byte actor, NativeBattleEffectArena expected)
    {
        NativeBattleEffectArena actual = NativeBattleRaldo19ModelMemory.Capture(snapshot.NativeGraphics!.Models[actor]);
        (byte[] bytes, bool[] known) = expected.ReadSnapshot(0, 8);
        (byte[] current, bool[] currentKnown) = actual.ReadSnapshot(0x1C, 8);
        Assert.Equal(known, currentKnown);
        for (int index = 0; index < 8; index++)
        {
            if (known[index])
            {
                Assert.Equal(bytes[index], current[index]);
            }
        }
    }

    private static NativeBattleGraphicsResource ResolveDegenerator306Resource(BattleSnapshot snapshot, NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource) => ResolveBlueBindingResource(snapshot, model, resource, NativeBattleDegenerator306Profile.BindingSave, address => snapshot.NativeEffectMemory!.Degenerator306!.ReadResourcePointer(address));

    private static NativeBattleGraphicsResource ResolveAcid317Resource(BattleSnapshot snapshot, NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        uint binding = snapshot.NativeEffectMemory!.Acid317!.ReadDword(NativeBattleAcid317Profile.WorkingGlobal) + NativeBattleAcid317Profile.BindingOffset;
        return ResolveBlueBindingResource(snapshot, model, resource, binding, address => snapshot.NativeEffectMemory.Arena!.ReadResourcePointer(checked((int)(address - NativeBattleAcid317Profile.ArenaAddress))));
    }

    private static NativeBattleGraphicsResource ResolveBlueBindingResource(BattleSnapshot snapshot, NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, uint binding, Func<uint, NativeBattleResourcePointer?> readBindingPointer)
    {
        return NativeBattleVictoryResources.Resolve(model, resource, ReadPointer, Read);

        NativeBattleResourcePointer? ReadPointer(uint address)
        {
            if (address >= binding && address < binding + 32)
            {
                return readBindingPointer(address);
            }
            if (address >= NativeBattleDegenerator306Profile.ArenaAddress && address < NativeBattleDegenerator306Profile.ArenaAddress + NativeBattleEffectArena.Length)
            {
                return snapshot.NativeEffectMemory!.Arena!.ReadResourcePointer(checked((int)(address - NativeBattleDegenerator306Profile.ArenaAddress)));
            }
            return snapshot.NativeFileStaging!.ReadResourcePointer(address);
        }

        byte[] Read(NativeBattleResourcePointer pointer, int length)
        {
            if (pointer.Cpu is { } cpu)
            {
                return NativeBattleModelEnemyHeap.Read(snapshot.NativeModelCpuResources!, snapshot.NativeFileStaging!, cpu, length);
            }
            uint address = pointer.StagingAddress ?? throw new NotSupportedException("The reference model requires its actual addressed resource owner.");
            return address >= NativeBattleDegenerator306Profile.ArenaAddress && address < NativeBattleDegenerator306Profile.ArenaAddress + NativeBattleEffectArena.Length ? snapshot.NativeEffectMemory!.Arena!.Read(checked((int)(address - NativeBattleDegenerator306Profile.ArenaAddress)), length) : snapshot.NativeFileStaging!.Read(address, length);
        }
    }

    private static bool Electrocute275CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 275 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 275 } after)
        {
            return false;
        }
        NativeBattleEffectTaskPool? previous = before.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleElectrocute275Profile.MainDescriptor);
        NativeBattleEffectTaskPool? next = after.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleElectrocute275Profile.MainDescriptor);
        return previous is not null && next is not null && previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleElectrocute275Profile.MainCallback && item.task.ReadWord(12) == 44 && next.Slots[item.slot].Callback == NativeBattleElectrocute275Profile.MainCallback && next.Slots[item.slot].ReadWord(12) == 45);
    }

    private static bool Acid317CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 317 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 317 } after)
        {
            return false;
        }
        NativeBattleEffectTaskPool? previous = before.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAcid317Profile.TargetDescriptor);
        NativeBattleEffectTaskPool? next = after.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleAcid317Profile.TargetDescriptor);
        return previous is not null && next is not null && previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleAcid317Profile.TargetCallback && item.task.ReadWord(12) == 57 && next.Slots[item.slot].Callback == NativeBattleAcid317Profile.TargetCallback && next.Slots[item.slot].ReadWord(12) == 58);
    }

    private static bool GatlingGun305CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 305 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 305 } after)
        {
            return false;
        }
        NativeBattleEffectTaskPool previous = Assert.Single(before.Pools, pool => pool.DescriptorAddress == NativeBattleGatlingGun305Profile.ChildDescriptor);
        NativeBattleEffectTaskPool next = Assert.Single(after.Pools, pool => pool.DescriptorAddress == NativeBattleGatlingGun305Profile.ChildDescriptor);
        return previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleGatlingGun305Profile.MainCallback && item.task.ReadWord(12) == 30 && next.Slots[item.slot].Callback == NativeBattleGatlingGun305Profile.MainCallback && next.Slots[item.slot].ReadWord(12) == 31);
    }

    private static bool Degenerator306CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 306 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 306 } after)
        {
            return false;
        }
        NativeBattleEffectTaskPool previous = Assert.Single(before.Pools, pool => pool.DescriptorAddress == NativeBattleDegenerator306Profile.ChildDescriptor);
        NativeBattleEffectTaskPool next = Assert.Single(after.Pools, pool => pool.DescriptorAddress == NativeBattleDegenerator306Profile.ChildDescriptor);
        return previous.Slots.Select((task, slot) => (task, slot)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == NativeBattleDegenerator306Profile.MainCallback && item.task.ReadWord(12) == 117 && next.Slots[item.slot].Callback == NativeBattleDegenerator306Profile.MainCallback && next.Slots[item.slot].ReadWord(12) == 118);
    }

    private static string BadBreath131ScratchW(NativeBattleEffectArena arena, int scratch)
    {
        return string.Join(";", Enumerable.Range(0, 165).Select(vertex =>
        {
            (byte[] data, bool[] known) = arena.ReadSnapshot(scratch + vertex * 8 + 6, 2);
            return string.Join(",", Enumerable.Range(0, 2).Select(index => known[index] ? data[index].ToString("X2", System.Globalization.CultureInfo.InvariantCulture) : "??"));
        }));
    }

    private static bool BadBreath131CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 131 } breath131Before || current.NativeGraphics!.OriginalEffect is not { Binding.Effect: 131 } breath131After)
        {
            return false;
        }
        NativeBattleEffectTaskPool? breath131Previous = breath131Before.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBadBreath131Profile.TargetDescriptor);
        NativeBattleEffectTaskPool? breath131Next = breath131After.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleBadBreath131Profile.TargetDescriptor);
        if (breath131Previous is null || breath131Next is null)
        {
            return false;
        }
        for (int slot = 0; slot < breath131Previous.Slots.Length; slot++)
        {
            NativeBattleModulePoolTask breath131Task = breath131Previous.Slots[slot];
            NativeBattleModulePoolTask breath131Updated = breath131Next.Slots[slot];
            if ((breath131Task.Flags & 1) != 0 && breath131Task.Callback == NativeBattleBadBreath131Profile.TargetCallback && breath131Task.ReadWord(12) == 40 && breath131Updated.Callback == breath131Task.Callback && breath131Updated.ReadWord(12) == 41)
            {
                Assert.Equal((byte)0, breath131Task.ReadByte(14));
                Assert.Equal((byte)0, breath131Task.ReadByte(15));
                Assert.Equal(breath131Task.ReadByte(14), breath131Updated.ReadByte(14));
                Assert.Equal(breath131Task.ReadByte(15), breath131Updated.ReadByte(15));
                return true;
            }
        }
        return false;
    }

    private static string BadBreath300ScratchW(NativeBattleEffectArena arena)
    {
        return JsonSerializer.Serialize(Enumerable.Range(0, 165).Select(vertex =>
        {
            (byte[] data, bool[] known) = arena.ReadSnapshot(NativeBattleBadBreath300Profile.ScratchOffset + vertex * 8 + 6, 2);
            return new { Data = data, Known = known };
        }));
    }

    private static bool BadBreath300CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 300, Pools.Length: 4 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 300, Pools.Length: 4 } after)
        {
            return false;
        }
        NativeBattleEffectTaskPool previous = Assert.Single(before.Pools, pool => pool.DescriptorAddress == NativeBattleBadBreath300Profile.TargetDescriptor);
        NativeBattleEffectTaskPool next = Assert.Single(after.Pools, pool => pool.DescriptorAddress == NativeBattleBadBreath300Profile.TargetDescriptor);
        for (int slot = 0; slot < previous.Slots.Length; slot++)
        {
            NativeBattleModulePoolTask task = previous.Slots[slot];
            NativeBattleModulePoolTask updated = next.Slots[slot];
            if ((task.Flags & 1) != 0 && task.Callback == NativeBattleBadBreath300Profile.TargetCallback && unchecked((short)task.ReadWord(12)) == 30 && unchecked((short)updated.ReadWord(12)) == 31)
            {
                Assert.Equal(NativeBattleBadBreath300Profile.TargetCallback, updated.Callback);
                Assert.Equal(before.Binding.BadBreath300Resources!.Target.Address, updated.ReadDword(0x18));
                Assert.Equal((byte)0, task.ReadByte(14));
                Assert.Equal((byte)3, task.ReadByte(15));
                Assert.Equal(task.ReadByte(14), updated.ReadByte(14));
                Assert.Equal(task.ReadByte(15), updated.ReadByte(15));
                return true;
            }
        }
        return false;
    }

    private static bool MicroMissiles299CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 299, Pools.Length: 4 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 299, Pools.Length: 4 } after)
        {
            return false;
        }
        NativeBattleEffectTaskPool previous = Assert.Single(before.Pools, pool => pool.DescriptorAddress == NativeBattleMicroMissiles299Profile.MissileDescriptor);
        NativeBattleEffectTaskPool next = Assert.Single(after.Pools, pool => pool.DescriptorAddress == NativeBattleMicroMissiles299Profile.MissileDescriptor);
        for (int slot = 0; slot < previous.Slots.Length; slot++)
        {
            NativeBattleModulePoolTask task = previous.Slots[slot];
            NativeBattleModulePoolTask updated = next.Slots[slot];
            if ((task.Flags & 1) != 0 && task.Callback == NativeBattleMicroMissiles299Profile.MissileCallback && task.ReadWord(0x22) == 1 && updated.ReadWord(0x22) == 0xFFFF && unchecked((short)task.ReadWord(0x2A)) < 4096 && unchecked((short)updated.ReadWord(0x2A)) >= 4096)
            {
                NativeBattleEffectTaskPool previousImpacts = Assert.Single(before.Pools, pool => pool.DescriptorAddress == NativeBattleMicroMissiles299Profile.ImpactDescriptor);
                NativeBattleEffectTaskPool impacts = Assert.Single(after.Pools, pool => pool.DescriptorAddress == NativeBattleMicroMissiles299Profile.ImpactDescriptor);
                Assert.Contains(impacts.Slots.Select((impact, index) => (impact, index)), item => (previousImpacts.Slots[item.index].Flags & 1) == 0 && (item.impact.Flags & 1) != 0 && item.impact.Callback == NativeBattleMicroMissiles299Profile.ImpactCallback && item.impact.ReadDword(0x30) == before.Binding.MicroMissiles299Resources!.Impact.Address);
                return true;
            }
        }
        return false;
    }

    private static bool WhiteWind297CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0, Binding.Effect: 297 } before || current.NativeGraphics!.OriginalEffect is not { Binding.Effect: 297 } after)
        {
            return false;
        }
        NativeBattleEffectTaskPool? previous = before.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleWhiteWind297Profile.TargetDescriptor);
        if (previous is null)
        {
            return false;
        }
        NativeBattleEffectTaskPool? retained = after.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleWhiteWind297Profile.TargetDescriptor);
        NativeBattleGfAnimationSource source = before.Binding.WhiteWind297Resources!.Target;
        int animation = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
        int duration = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(animation + 4));
        for (int slot = 0; slot < previous.Slots.Length; slot++)
        {
            NativeBattleModulePoolTask task = previous.Slots[slot];
            if ((task.Flags & 1) == 0 || task.Callback != NativeBattleWhiteWind297Profile.TargetCallback)
            {
                continue;
            }
            ushort flags;
            uint callback;
            uint resource;
            int frame;
            if (retained is not null)
            {
                NativeBattleModulePoolTask next = retained.Slots[slot];
                flags = next.Flags;
                callback = next.Callback;
                resource = next.ReadDword(0x18);
                frame = unchecked((int)next.ReadDword(0x1C));
            }
            else
            {
                Assert.Equal(0u, after.ReturnedDescriptor);
                uint address = checked(previous.BankAddress + (uint)(slot * previous.Stride));
                int offset = checked((int)(address - NativeBattleWhiteWind297Profile.ArenaAddress));
                NativeBattleEffectArena arena = current.NativeEffectMemory!.Arena!;
                flags = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
                callback = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 8, 4));
                resource = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 0x18, 4));
                frame = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 0x1C, 4));
            }
            if ((flags & 1) == 0 && callback == NativeBattleWhiteWind297Profile.TargetCallback && resource == source.Address && duration - frame == 10 && frame == unchecked((int)task.ReadDword(0x1C)) + 1)
            {
                return true;
            }
        }
        return false;
    }

    private static void AssertRayBomb307Restored(BattleSnapshot snapshot, byte actor)
    {
        NativeBattleRayBomb307State state = snapshot.NativeEffectMemory!.RayBomb307!;
        NativeBattleEffectArena saved = state.ReadView(NativeBattleRayBomb307Profile.SavedActorsAddress + (uint)actor * 20, 20);
        NativeBattleGraphicsModel model = snapshot.NativeGraphics!.Models[actor];
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(saved.Read(0, 2)), model.Flags0 & 0x1020);
        NativeBattleEffectArena actual = NativeBattleRaldo19ModelMemory.Capture(model);
        Compare(4, 0x0C);
        Compare(12, 0x1C);

        void Compare(int savedOffset, int actualOffset)
        {
            (byte[] expected, bool[] expectedKnown) = saved.ReadSnapshot(savedOffset, 8);
            (byte[] current, bool[] currentKnown) = actual.ReadSnapshot(actualOffset, 8);
            Assert.Equal(expectedKnown, currentKnown);
            for (int index = 0; index < 8; index++)
            {
                if (expectedKnown[index])
                {
                    Assert.Equal(expected[index], current[index]);
                }
            }
        }
    }

    private static string TargetFootprint(NativeBattleGraphicsModel model)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("The selected target requires its actual packed position owner.");
        Assert.NotNull(model.Rotation12);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.Rotation16);
        return JsonSerializer.Serialize(new { model.Rotation12, model.Rotation14, model.Rotation16, model.RotationPadding18, transform.Position28, transform.Vertical34 });
    }

    private static string TargetPositionFootprint(NativeBattleGraphicsModel model)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("The selected target requires its actual packed position owner.");
        return JsonSerializer.Serialize(new { transform.Position28, transform.Vertical34 });
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && (effect.Binding.Effect is 303 or 298 || effect.Pools.All(pool => effect.Binding.Effect == 260 ? pool.Head is null && pool.Tail is null : pool.Slots.All(task => (task.Flags & 1) == 0)));
    }

    private static bool Effect20ActivationCompleted(BattleSnapshot snapshot)
    {
        NativeBattleDescriptorState descriptors = snapshot.NativeGraphics!.Descriptors;
        return !descriptors.Queue.Any(index => descriptors.Packets[index].Command == 113) && !descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Callback502F30);
    }

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        return changed;
    }
}
