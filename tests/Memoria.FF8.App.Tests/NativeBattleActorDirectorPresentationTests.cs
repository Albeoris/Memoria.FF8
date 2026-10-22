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
        bool petrifyActRestored = false;
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
            if (effectId == 306)
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
            string? originalTargetPosition = effectId == 60 ? TargetPositionFootprint(source.NativeGraphics.Models[contactActor]) : null;
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
                    else
                    {
                        Assert.Equal(JsonSerializer.Serialize(original.Hypnotize133Resources?.Camera ?? original.NeedlesFamilyResources?.Camera ?? original.LaserEye71Resources?.Camera ?? original.EerieSoundWave130Resources?.Camera ?? original.PetrifyStare127Resources?.Camera ?? original.RayBomb307Resources?.Camera ?? original.UltraWaves184Resources?.Camera ?? original.Electrocute275Resources?.Camera ?? original.Acid317Resources?.Camera ?? original.GatlingGun305Resources?.Camera ?? original.Degenerator306Resources?.Camera ?? original.BadBreath300Resources?.Camera ?? original.MicroMissiles299Resources?.Camera ?? original.WhiteWind297Resources?.Camera ?? original.DeathResources?.Animation ?? original.Curse47Resources?.Animation ?? original.Doom8Resources?.Camera ?? original.Absorb56Resources?.Animation ?? original.Devour62Resources?.Animation ?? original.MeltingBubble43Resources?.Animation ?? original.WindBlast29Resources?.Animation ?? original.Effect60Resources?.Animation ?? original.PsychoBlast23Resources?.Animation ?? original.Effect20Resources?.Animation ?? original.Raldo19Resources?.Animation ?? original.DrinkMagic37Resources?.Animation ?? original.MightyGuardResources?.Animation ?? original.ActorDirectorResources!.Animation), JsonSerializer.Serialize(current.NativeEffectMemory!.Textures!.Animation!.Source));
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
                if ((visibleChangedRgb == 0 || effectId is 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 127 or 130 or 133 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 275 or 184 or 307 or 260) && after?.ReturnedDescriptor is > 0 && Director(current).Effect == effectId)
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
                            if (effectId is not (8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 127 or 130 or 133 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 275 or 184 or 307 or 260))
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
            Assert.Equal(1, contacts);
            Assert.Equal(expectedText is null ? 0 : 1, contactGlyphs);
            if (request.ControlledDrainContact)
            {
                Assert.True(absorbBankRestored, "Absorb never materialized its actual retained absolute bank.");
                Assert.Equal(expectedSecondaryText is null ? 0 : 1, secondaryContactGlyphs);
            }
            Assert.True(cameraChanged, "The original actor-director camera control never changed its retained camera.");
            if (effectId is not (19 or 23 or 29 or 37 or 43 or 127 or 130 or 133 or 177 or 178 or 297 or 299 or 300 or 317 or 184))
            {
                Assert.True(stageChanged, "The original actor-director stage fade never changed the retained four-group color/depth owner.");
            }
            if (effectId is 19 or 23 or 29 or 43 or 130 or 133 or 177 or 178 or 297 or 299 or 300 or 317 or 184)
            {
                Assert.False(stageChanged);
            }
            Assert.Equal(1, completions);
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
            if (effectId is 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 71 or 73 or 74 or 78 or 127 or 130 or 133 or 177 or 178 or 297 or 299 or 300 or 305 or 306 or 317 or 275 or 184 or 307 or 184 or 260 && peakSnapshot is not null)
            {
                File.WriteAllText(Path.Combine(output!, "visible-packet-ownership.json"), peakOwnership ?? throw new InvalidDataException("The peak node packet ownership was not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "visible", peakSnapshot, peakPixels ?? throw new InvalidDataException("The peak node pixels were not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", peakWithout ?? throw new InvalidDataException("The peak node control was not retained."), peakPlain ?? throw new InvalidDataException("The peak node control pixels were not retained."));
                ColdNext("peak", peakSnapshot);
            }
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = effectId == 71 ? "Original Laser Eye command15/action0/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no damage formula, accuracy, ordinary command or AI admission" : effectId == 260 ? "Original Shockwave Pulsar command15/action15/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command, GF or AI admission" : effectId == 307 ? "Original Blue Ray-Bomb command15/action14/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 184 ? "Original Blue Ultra Waves command15/action1/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 275 ? "Original Electrocute command15/action2/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage formula, accuracy, ordinary command or AI admission" : effectId == 317 ? "Original Acid command15/action7/crisis1/sequence21 with declared seven-HP post-formula contact and retained logical HP/status; no magic damage or status formula, accuracy, ordinary command or AI admission" : effectId == 305 ? "Original Gatling Gun command15/action8/crisis1/sequence21 with declared seven-HP damage post-formula input and retained logical HP/status; no physical damage formula, accuracy, ordinary command or AI admission" : effectId == 306 ? "Original Degenerator command15/action4/crisis1/sequence21 with declared Amount0 post-formula contact; native MISS/non-MISS body and visibility branch, actual logical HP/status retained; no removal/status formula, accuracy, ordinary command or AI admission" : effectId == 300 ? "Original Bad Breath command15/action10/crisis1/sequence21 with declared Amount0 MISS post-formula input; current HP/status retained; no status, accuracy, formula, ordinary command or AI admission" : effectId == 299 ? "Original Micro Missiles command15/action6/crisis1/sequence21 with declared seven-HP damage post-formula input; current HP/status retained; no percentage formula, ordinary command or AI admission" : effectId == 297 ? "Original White Wind command15/action11/sequence21 with declared seven-HP healing post-formula input; current HP/status retained; no ordinary command, AI or formula admission" : effectId == 74 ? "Original Level Death command15/action3/sequence21 with explicitly declared selected post-formula contact; no level comparison, KO/status result, ordinary command, AI or formula admission" : request.ControlledDrainContact ? "Controlled original Absorb presentation with paired declared drain halves and original target resistance; no ordinary formula or inventory admission" : effectId == 62 ? "Controlled original Devour action104 with actual target outcome; no ordinary formula or inventory admission" : "Controlled actor-director action104 and seven-HP contact; no ordinary action or formula admission", Effect = effectId, exported.Selection, action.Command, action.ActionId, callbacks, DiagnosticMaximumCallbacks = 4096, contacts, contactGlyphs, secondaryContactGlyphs, completions, visibleChangedRgb, cameraChanged, stageChanged, actor6Activated, effect20VibrationHandle, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

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
        if (state.Effect is 71 or 130 or 127 or 8 or 20 or 23 or 29 or 43 or 47 or 56 or 60 or 62 or 297 or 299 or 300 or 305 or 306 or 317 or 275 or 184 or 307 or 260 && (state.PrimaryDrawCursor is null || state.SecondaryDrawCursor is null))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> common = state.CommonMeshes ? NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: state.Effect == 260 ? (model, resource) => ResolveShockwavePulsar260Resource(current, model, resource) : state.Effect == 317 ? (model, resource) => ResolveAcid317Resource(current, model, resource) : state.Effect == 306 ? (model, resource) => ResolveDegenerator306Resource(current, model, resource) : null) : ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        int secondary = state.SecondaryDrawCursor ?? throw new InvalidDataException("The actor director sprite span requires its current secondary cursor.");
        int start = checked(secondary - state.DrawStep);
        int end = state.PrimaryDrawCursor ?? throw new InvalidDataException("The actor director sprite span requires its actual current primary end.");
        Assert.InRange(start, state.SourceArenaOffset, state.SourceArenaOffset + state.DrawStep);
        Assert.InRange(end, start, secondary);
        IEnumerable<NativeBattlePrimitiveReference> sprites = graphics.PrimitiveFrame!.LastCommands.Select(resolved => resolved.Command).Where(command => command.Packet.Bank == state.PacketBank && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end && command.Opcode is >= 0x20 and < 0x80).Select(command => command.Packet);
        return common.Union(sprites);
    }

    private sealed record DirectedNodePacketState(ushort Effect, int SourceArenaOffset, int? PrimaryDrawCursor, int? SecondaryDrawCursor, int DrawStep, bool CommonMeshes, string PacketBank = "20DFAB8");

    private static DirectedNodePacketState Director(BattleSnapshot snapshot)
    {
        ushort effect = snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect;
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

    private static string StageColors(BattleSnapshot snapshot) => JsonSerializer.Serialize(snapshot.NativeStage!.Groups.Select(group => new { group.ColorFlags, group.ColorDepth }));

    private static bool CrossedContactGate(BattleSnapshot prior, BattleSnapshot current, ushort effect)
    {
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
