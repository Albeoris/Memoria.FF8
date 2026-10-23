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
            Assert.Equal((byte)1, source.NativeActionPackets.ContactCount);
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
                        Assert.NotNull(current.NativeEffectMemory.GeometryRegisters!.Lzcs);
                        Assert.NotNull(current.NativeEffectMemory.GeometryRegisters.Lzcr);
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
                        Assert.Equal(JsonSerializer.Serialize(original.Additional182Resources?.Camera((contact.Primary.HitFlags3 & 4) != 0) ?? original.Blaster186Resources?.Camera ?? original.CannonBlow151Resources?.Camera ?? original.GatlingGun150Resources?.Camera ?? original.Breath129Resources?.Camera ?? original.BadBreath131Resources?.Camera ?? original.Sonic139Resources?.Camera ?? original.ReflectBeam135Resources?.Camera ?? original.Breath157Resources?.Camera ?? original.WildCannonBlow156Resources?.Camera ?? original.SandStorm155Resources?.Camera ?? original.GastricJuice128Resources?.Camera ?? original.DisolvingAcid132Resources?.Camera ?? original.OilShot136Resources?.Camera ?? original.OilBlast137Resources?.Camera ?? original.Saliva138Resources?.Camera ?? original.MeltEye158Resources?.Camera ?? original.EnergyBomber217Resources?.Camera ?? original.Effect101Resources?.Camera ?? original.BeamLaser134Resources?.Camera ?? original.Hypnotize133Resources?.Camera ?? original.NeedlesFamilyResources?.Camera ?? original.LaserEye71Resources?.Camera ?? original.EerieSoundWave130Resources?.Camera ?? original.PetrifyStare127Resources?.Camera ?? original.RayBomb307Resources?.Camera ?? original.UltraWaves184Resources?.Camera ?? original.ElectricDischarge126Resources?.Camera ?? original.Electrocute275Resources?.Camera ?? original.Acid317Resources?.Camera ?? original.GatlingGun305Resources?.Camera ?? original.Degenerator306Resources?.Camera ?? original.BadBreath300Resources?.Camera ?? original.MicroMissiles299Resources?.Camera ?? original.WhiteWind297Resources?.Camera ?? original.DeathResources?.Animation ?? original.Curse47Resources?.Animation ?? original.Doom8Resources?.Camera ?? original.Absorb56Resources?.Animation ?? original.Devour62Resources?.Animation ?? original.MeltingBubble43Resources?.Animation ?? original.WindBlast29Resources?.Animation ?? original.Effect60Resources?.Animation ?? original.PsychoBlast23Resources?.Animation ?? original.Effect20Resources?.Animation ?? original.Raldo19Resources?.Animation ?? original.DrinkMagic37Resources?.Animation ?? original.MightyGuardResources?.Animation ?? original.ActorDirectorResources!.Animation), JsonSerializer.Serialize(current.NativeEffectMemory!.Textures!.Animation!.Source));
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
                if ((visibleChangedRgb == 0 || effectId is 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 101 or 135 or 131 or 128 or 155 or 156 or 157 or 182 or 183 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 260) && after?.ReturnedDescriptor is > 0 && Director(current).Effect == effectId)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = ActorDirectorPacketReferences(prior, current);
                    if (!owned.IsEmpty)
                    {
                        NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
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
                            peakOwnership = effectId is 19 or 184
                                ? JsonSerializer.Serialize(new { Effect = effectId, current.Frame, NativeCallback = current.NativeGraphics.Callback, Ownership = effectId == 184 ? "Fixed native PRIVATE packets, warp quads and capture plus matched COMMON prefix before ordinary actor submission" : "Matched common packets before the complete first actor allocation", Packets = owned.OrderBy(packet => packet.Bank).ThenBy(packet => packet.Buffer).ThenBy(packet => packet.ByteOffset).ToArray() })
                                : JsonSerializer.Serialize(new { Effect = effectId, current.Frame, NativeCallback = current.NativeGraphics.Callback, PrivateBank = packetState.PacketBank, PrivateStart = packetState.SecondaryDrawCursor!.Value - packetState.DrawStep, PrivateEnd = packetState.PrimaryDrawCursor!.Value, Packets = owned.OrderBy(packet => packet.Bank).ThenBy(packet => packet.Buffer).ThenBy(packet => packet.ByteOffset).ToArray() });
                            if (effectId is not (8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 101 or 135 or 131 or 128 or 155 or 156 or 157 or 182 or 183 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 260))
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
                if (initialized && Released(current) && (effectId != 20 || actor6Activated && Effect20ActivationCompleted(current)))
                {
                    break;
                }
            }
            boundary = "acceptance";
            BattleSnapshot completed = battle.Capture();
            Assert.True(initialized, "The original actor-director module never initialized its selected controller.");
            Assert.True(liveRestored, "No significant original effect packet contribution reached cold continuation.");
            Assert.True(visibleChangedRgb > 0);
            Assert.Equal(effectId == 101 ? 0 : 1, contacts);
            Assert.Equal(effectId == 101 || expectedText is null ? 0 : 1, contactGlyphs);
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
            if (effectId is not (19 or 23 or 29 or 37 or 43 or 128 or 155 or 156 or 157 or 183 or 150 or 151 or 139 or 131 or 132 or 137 or 138 or 158 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 317 or 184))
            {
                Assert.True(stageChanged, "The original actor-director stage fade never changed the retained four-group color/depth owner.");
            }
            if (effectId is 19 or 23 or 29 or 43 or 128 or 155 or 156 or 157 or 183 or 150 or 151 or 139 or 131 or 132 or 137 or 138 or 158 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 317 or 184)
            {
                Assert.False(stageChanged);
            }
            Assert.Equal(1, completions);
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
            if (effectId is 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 101 or 135 or 131 or 128 or 155 or 156 or 157 or 182 or 183 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 127 or 130 or 133 or 134 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 184 or 260 && peakSnapshot is not null)
            {
                File.WriteAllText(Path.Combine(output!, "visible-packet-ownership.json"), peakOwnership ?? throw new InvalidDataException("The peak node packet ownership was not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "visible", peakSnapshot, peakPixels ?? throw new InvalidDataException("The peak node pixels were not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", peakWithout ?? throw new InvalidDataException("The peak node control was not retained."), peakPlain ?? throw new InvalidDataException("The peak node control pixels were not retained."));
                ColdNext("peak", peakSnapshot);
            }
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = effectId == 182 ? "Original Additional command29/action10/sequence11 and formation94; declared Amount0 contact BYTE3 bit4 selects native director, camera and sound with logical HP/status retained; no formula, accuracy, status, ordinary command, AI or card-conversion admission" : effectId == 186 ? "Original Blaster command8/action122/sequence13 and formation247 with declared seven-HP post-formula contact and logical HP/status retained; no status result, formula, accuracy, ordinary command or AI admission" : effectId == 151 ? "Original Cannon Blow command8/action69/sequence16 and formation125 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 150 ? "Original Gatling Gun command8/action68/sequence14 and formation125 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 129 ? "Original Breath129 command8/action102/sequence12 and formation338 with declared seven-HP post-formula presentation and logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission" : effectId == 139 ? "Original Sonic Wave command8/action182/sequence11 and formation384 with declared seven-HP post-formula presentation and logical HP/status retained; no formula, accuracy, ordinary command or AI turn admission" : effectId == 183 ? "Original effect183 command23/action11/sequence23 and formation94; declared seven-HP post-formula presentation with logical HP/status retained; no level formula, status result, accuracy or ordinary command admission" : effectId == 157 ? "Original Breath command8/action37/sequence14 and formation413 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 156 ? "Original Wild Cannon Blow command8/action70/sequence17 and formation125 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 155 ? "Original Sand Storm command8/action39/sequence14 and formation452 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 128 ? "Original Gastric Juice command8/action77/sequence14 and formation220 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 132 ? "Original Disolving Acid command8/action183/sequence12 and formation376 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 136 ? "Original Oil Shot command8/action180/sequence13 and formation384 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 138 ? "Original Saliva command8/action175/sequence16 and formation378 with declared seven-HP post-formula contact and logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 131 ? "Original Enemy Bad Breath command8/action184/sequence14 and formation376 with declared seven-HP post-formula contact and logical HP/status retained; no status formula, accuracy, ordinary command or AI admission" : effectId == 126 ? "Original Electric Discharge command8/action28/sequence15 and formation666 with declared seven-HP post-formula contact and logical HP/status retained; no ordinary command, AI, accuracy or formula admission" : effectId == 137 ? "Original Oil Blast command8/action181/sequence12 and formation384 with declared seven-HP post-formula contact and logical HP/status retained; no ordinary action, accuracy, AI or formula admission" : effectId == 158 ? "Original Melt-Eye command8/action36/sequence13 with declared seven-HP post-formula contact and logical HP/status retained; no magic damage/status formula, accuracy, ordinary command or AI admission" : effectId == 217 ? "Original Energy Bomber command8/action316/sequence16, both formation796 enemies retained, declared seven-HP post-formula contact with logical HP/status retained; no physical formula, accuracy, ordinary command or AI admission" : effectId == 101 ? "Original unnamed Effect101 command8/action290/sequence13; real recipient sequence14 has no contact event, unused declared seven-HP packet, logical HP/status retained; no formula, accuracy, ordinary command or AI admission" : effectId == 71 ? "Original Laser Eye command15/action0/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no damage formula, accuracy, ordinary command or AI admission" : effectId == 260 ? "Original Shockwave Pulsar command15/action15/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command, GF or AI admission" : effectId == 307 ? "Original Blue Ray-Bomb command15/action14/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 184 ? "Original Blue Ultra Waves command15/action1/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 275 ? "Original Electrocute command15/action2/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage formula, accuracy, ordinary command or AI admission" : effectId == 317 ? "Original Acid command15/action7/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 305 ? "Original Gatling Gun command15/action8/crisis1/sequence21 with declared seven-HP damage post-formula input and retained logical HP/status; no physical damage formula, accuracy, ordinary command or AI admission" : effectId == 306 ? "Original Degenerator command15/action4/crisis1/sequence21 with declared Amount0 post-formula contact; native MISS/non-MISS body and visibility branch, actual logical HP/status retained; no removal/status formula, accuracy, ordinary command or AI admission" : effectId == 300 ? "Original Bad Breath command15/action10/crisis1/sequence21 with declared Amount0 MISS post-formula input; current HP/status retained; no status, accuracy, formula, ordinary command or AI admission" : effectId == 299 ? "Original Micro Missiles command15/action6/crisis1/sequence21 with declared seven-HP damage post-formula input; current HP/status retained; no percentage formula, ordinary command or AI admission" : effectId == 297 ? "Original White Wind command15/action11/sequence21 with declared seven-HP healing post-formula input; current HP/status retained; no ordinary command, AI or formula admission" : effectId == 74 ? "Original Level Death command15/action3/sequence21 with explicitly declared selected post-formula contact; no level comparison, KO/status result, ordinary command, AI or formula admission" : request.ControlledDrainContact ? "Controlled original Absorb presentation with paired declared drain halves and original target resistance; no ordinary formula or inventory admission" : effectId == 62 ? "Controlled original Devour action104 with actual target outcome; no ordinary formula or inventory admission" : "Controlled actor-director action104 and seven-HP contact; no ordinary action or formula admission", Effect = effectId, exported.Selection, action.Command, action.ActionId, callbacks, DiagnosticMaximumCallbacks = 4096, contacts, contactGlyphs, secondaryContactGlyphs, completions, visibleChangedRgb, cameraChanged, stageChanged, actor6Activated, effect20VibrationHandle, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

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
        if (graphics.OriginalEffect!.Binding.Effect == 19)
        {
            return NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
        }
        if (graphics.OriginalEffect!.Binding.Effect == 184)
        {
            ImmutableHashSet<NativeBattlePrimitiveReference> wavesCommon = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: (model, resource) => ResolveBlueBindingResource(current, model, resource, NativeBattleUltraWaves184Profile.SavedBindingAddress, address => current.NativeEffectMemory!.UltraWaves184!.ReadResourcePointer(address)));
            return wavesCommon.Union(graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command.Packet).Where(packet => packet.Buffer == 0 && packet.Bank is "22DB3C4" or "22BE8E8" or "D34868"));
        }
        DirectedNodePacketState state = Director(current);
        if (state.Effect is 135 or 131 or 128 or 155 or 156 or 157 or 182 or 183 or 150 or 151 or 186 or 139 or 129 or 132 or 136 or 137 or 138 or 158 or 217 or 101 or 71 or 130 or 127 or 8 or 20 or 23 or 29 or 43 or 47 or 56 or 60 or 62 or 297 or 299 or 300 or 305 or 306 or 317 or 126 or 275 or 184 or 307 or 260 && (state.PrimaryDrawCursor is null || state.SecondaryDrawCursor is null))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> common = state.CommonMeshes ? NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: state.Effect == 260 ? (model, resource) => ResolveShockwavePulsar260Resource(current, model, resource) : state.Effect == 317 ? (model, resource) => ResolveAcid317Resource(current, model, resource) : state.Effect == 306 ? (model, resource) => ResolveDegenerator306Resource(current, model, resource) : null) : ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        int secondary = state.SecondaryDrawCursor ?? throw new InvalidDataException("The actor director sprite span requires its current secondary cursor.");
        int start = checked(secondary - state.DrawStep);
        int end = state.PrimaryDrawCursor ?? throw new InvalidDataException("The actor director sprite span requires its actual current primary end.");
        Assert.InRange(start, state.SourceArenaOffset, state.SourceArenaOffset + state.DrawStep);
        Assert.InRange(end, start, secondary);
        IEnumerable<NativeBattlePrimitiveReference> sprites = graphics.PrimitiveFrame!.LastCommands.Select(resolved => resolved.Command).Where(command => command.Packet.Bank == state.PacketBank && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end && (state.Effect == 139 || command.Opcode is >= 0x20 and < 0x80 || (state.Effect is 136 or 137 or 138 or 182 or 183 or 150 or 151 or 186 or 139 or 129) && command.Opcode == 0xE2 || state.Effect == 136 && command.Opcode == 0xE1 || state.Effect == 131 && command.Opcode is 0xE1 or 0xE2)).Select(command => command.Packet);
        return common.Union(sprites);
    }

    private sealed record DirectedNodePacketState(ushort Effect, int SourceArenaOffset, int? PrimaryDrawCursor, int? SecondaryDrawCursor, int DrawStep, bool CommonMeshes, string PacketBank = "20DFAB8");

    private static DirectedNodePacketState Director(BattleSnapshot snapshot)
    {
        ushort effect = snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect;
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
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => effect.Binding.Effect == 260 ? pool.Head is null && pool.Tail is null : pool.Slots.All(task => (task.Flags & 1) == 0));
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
