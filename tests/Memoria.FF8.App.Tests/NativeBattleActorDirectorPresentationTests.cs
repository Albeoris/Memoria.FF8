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
                    Assert.Equal(JsonSerializer.Serialize(original.GatlingGun305Resources?.Camera ?? original.Degenerator306Resources?.Camera ?? original.BadBreath300Resources?.Camera ?? original.MicroMissiles299Resources?.Camera ?? original.WhiteWind297Resources?.Camera ?? original.DeathResources?.Animation ?? original.Curse47Resources?.Animation ?? original.Doom8Resources?.Camera ?? original.Absorb56Resources?.Animation ?? original.Devour62Resources?.Animation ?? original.MeltingBubble43Resources?.Animation ?? original.WindBlast29Resources?.Animation ?? original.Effect60Resources?.Animation ?? original.PsychoBlast23Resources?.Animation ?? original.Effect20Resources?.Animation ?? original.Raldo19Resources?.Animation ?? original.DrinkMagic37Resources?.Animation ?? original.MightyGuardResources?.Animation ?? original.ActorDirectorResources!.Animation), JsonSerializer.Serialize(current.NativeEffectMemory!.Textures!.Animation!.Source));
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
                if ((visibleChangedRgb == 0 || effectId is 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 73 or 74 or 78 or 297 or 299 or 300 or 305 or 306) && after?.ReturnedDescriptor is > 0 && Director(current).Effect == effectId)
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
                            peakOwnership = effectId == 19
                                ? JsonSerializer.Serialize(new { Effect = effectId, current.Frame, NativeCallback = current.NativeGraphics.Callback, Ownership = "Matched common packets before the complete first actor allocation", Packets = owned.OrderBy(packet => packet.Bank).ThenBy(packet => packet.Buffer).ThenBy(packet => packet.ByteOffset).ToArray() })
                                : JsonSerializer.Serialize(new { Effect = effectId, current.Frame, NativeCallback = current.NativeGraphics.Callback, PrivateBank = packetState.PacketBank, PrivateStart = packetState.SecondaryDrawCursor!.Value - packetState.DrawStep, PrivateEnd = packetState.PrimaryDrawCursor!.Value, Packets = owned.OrderBy(packet => packet.Bank).ThenBy(packet => packet.Buffer).ThenBy(packet => packet.ByteOffset).ToArray() });
                            if (effectId is not (8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 73 or 74 or 78 or 297 or 299 or 300 or 305 or 306))
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
            if (effectId is not (19 or 23 or 29 or 37 or 43 or 297 or 299 or 300))
            {
                Assert.True(stageChanged, "The original actor-director stage fade never changed the retained four-group color/depth owner.");
            }
            if (effectId is 19 or 23 or 29 or 43 or 297 or 299 or 300)
            {
                Assert.False(stageChanged);
            }
            Assert.Equal(1, completions);
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
            if (effectId is 8 or 19 or 20 or 23 or 29 or 37 or 43 or 47 or 56 or 60 or 62 or 73 or 74 or 78 or 297 or 299 or 300 or 305 or 306 && peakSnapshot is not null)
            {
                File.WriteAllText(Path.Combine(output!, "visible-packet-ownership.json"), peakOwnership ?? throw new InvalidDataException("The peak node packet ownership was not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "visible", peakSnapshot, peakPixels ?? throw new InvalidDataException("The peak node pixels were not retained."));
                NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", peakWithout ?? throw new InvalidDataException("The peak node control was not retained."), peakPlain ?? throw new InvalidDataException("The peak node control pixels were not retained."));
                ColdNext("peak", peakSnapshot);
            }
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = effectId == 305 ? "Original Gatling Gun command15/action8/crisis1/sequence21 with declared seven-HP damage post-formula input and retained logical HP/status; no physical damage formula, accuracy, ordinary command or AI admission" : effectId == 306 ? "Original Degenerator command15/action4/crisis1/sequence21 with declared Amount0 post-formula contact; native MISS/non-MISS body and visibility branch, actual logical HP/status retained; no removal/status formula, accuracy, ordinary command or AI admission" : effectId == 300 ? "Original Bad Breath command15/action10/crisis1/sequence21 with declared Amount0 MISS post-formula input; current HP/status retained; no status, accuracy, formula, ordinary command or AI admission" : effectId == 299 ? "Original Micro Missiles command15/action6/crisis1/sequence21 with declared seven-HP damage post-formula input; current HP/status retained; no percentage formula, ordinary command or AI admission" : effectId == 297 ? "Original White Wind command15/action11/sequence21 with declared seven-HP healing post-formula input; current HP/status retained; no ordinary command, AI or formula admission" : effectId == 74 ? "Original Level Death command15/action3/sequence21 with explicitly declared selected post-formula contact; no level comparison, KO/status result, ordinary command, AI or formula admission" : request.ControlledDrainContact ? "Controlled original Absorb presentation with paired declared drain halves and original target resistance; no ordinary formula or inventory admission" : effectId == 62 ? "Controlled original Devour action104 with actual target outcome; no ordinary formula or inventory admission" : "Controlled actor-director action104 and seven-HP contact; no ordinary action or formula admission", Effect = effectId, exported.Selection, action.Command, action.ActionId, callbacks, DiagnosticMaximumCallbacks = 4096, contacts, contactGlyphs, secondaryContactGlyphs, completions, visibleChangedRgb, cameraChanged, stageChanged, actor6Activated, effect20VibrationHandle, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

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
        DirectedNodePacketState state = Director(current);
        if (state.Effect is 8 or 20 or 23 or 29 or 43 or 47 or 56 or 60 or 62 or 297 or 299 or 300 or 305 or 306 && (state.PrimaryDrawCursor is null || state.SecondaryDrawCursor is null))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> common = state.CommonMeshes ? NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, resolveResource: state.Effect == 306 ? (model, resource) => ResolveDegenerator306Resource(current, model, resource) : null) : ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
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

    private static NativeBattleGraphicsResource ResolveDegenerator306Resource(BattleSnapshot snapshot, NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        return NativeBattleVictoryResources.Resolve(model, resource, ReadPointer, Read);

        NativeBattleResourcePointer? ReadPointer(uint address)
        {
            if (address >= NativeBattleDegenerator306Profile.BindingSave && address < NativeBattleDegenerator306Profile.BindingSave + 32)
            {
                return snapshot.NativeEffectMemory!.Degenerator306!.ReadResourcePointer(address);
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
            uint address = pointer.StagingAddress ?? throw new NotSupportedException("Degenerator reference model requires its actual addressed resource owner.");
            return address >= NativeBattleDegenerator306Profile.ArenaAddress && address < NativeBattleDegenerator306Profile.ArenaAddress + NativeBattleEffectArena.Length ? snapshot.NativeEffectMemory!.Arena!.Read(checked((int)(address - NativeBattleDegenerator306Profile.ArenaAddress)), length) : snapshot.NativeFileStaging!.Read(address, length);
        }
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
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
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
