using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAquaPresentationTests
{
    [Fact]
    public void Selected_original_Aqua_component_completes_controlled_contact_and_cold_boundaries()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selection = Environment.GetEnvironmentVariable("MEMORIA_FF8_AQUA_EFFECT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_AQUA_EVIDENCE");
        Assert.SkipUnless(directory is not null && selection is not null && output is not null, "Select installed original resources, one Aqua effect and a distinct evidence directory.");
        ushort effectId = ushort.Parse(selection!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(effectId, new ushort[] { 55, 72 });
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
        bool stageChanged = false;
        bool boundCaster72 = false;
        int visibleChangedRgb = 0;
        bool initialized = false;
        bool liveRestored = false;
        bool cameraChanged = false;
        List<string> restoredBoundaries = [];
        string boundary = "original-resources";
        try
        {
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            (BattleNativeAction action, BattleEncounter formation, BattleEnemyPlacement enemy) = SelectOriginalBinding(assets, effectId);
            NativeBattleEffectBinding original = game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1];
            string fixture = Path.Combine(output!, "fixture");
            boundary = "export-controlled-component";
            byte caster = effectId == 55 ? (byte)3 : (byte)0;
            byte contactActor = effectId == 55 ? (byte)0 : (byte)3;
            NativeEffectFixtureRequest request = new($"aqua{effectId}", effectId, action.Command, action.ActionId, contactActor, 800, 240) { EncounterId = formation.Id, CasterActorIndex = caster, PartyModelId = effectId == 72 ? 3 : 0, CrisisLevel = action.CrisisLevel };
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, fixture);
            File.WriteAllText(Path.Combine(output!, "fixture-result.json"), JsonSerializer.Serialize(exported));
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
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
            Assert.True(exported.Selection!.OriginalKernelEffectReference);
            Assert.Equal(action.MagicId, exported.Selection.ShellEffect);
            NativeBattleContactPacket contact = source.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
            Assert.Equal((byte)1, source.NativeActionPackets.ContactCount);
            Assert.Equal(contactActor, contact.Primary.ActorIndex);
            Assert.Equal((ushort)7, contact.Primary.Amount6);
            Assert.Equal((byte)255, contact.Secondary.ActorIndex);
            NativeBattleEffectTask? expectedText = NativeBattleEffectsState.Clear().PrependDamageText(contact.Primary.ActorIndex, contact.Primary.ContactFlags, contact.Primary.Amount6).Slots.SingleOrDefault(task => (task.Flags0 & 1) != 0);
            string target = source.NativeGraphics!.ActorIds[contactActor]!;
            int logicalHp = source.Combatants.Single(actor => actor.Id == target).CurrentHp;
            int initialSounds = source.Events.Count(item => item.NativeModuleSound is not null);
            string initialCamera = JsonSerializer.Serialize(source.NativeCamera);
            string initialStage = StageColors(source);
            Assert.Equal(effectId == 72 ? 3 : 0, source.Definition.Combatants.Single(actor => actor.IsPlayer).ModelId);
            Assert.NotNull(source.NativeModelCpuResources);
            File.WriteAllText(Path.Combine(output!, "original-binding.json"), JsonSerializer.Serialize(new { action.Command, action.ActionId, action.MagicId, action.CrisisLevel, action.KernelSection, action.KernelIndex, action.SupplementalKernelIndex, Formation = formation.Id, formation.StageId, Enemy = enemy.ModelId, enemy.Slot, enemy.Level, Caster = caster, Target = contactActor, CasterBones = effectId == 55 ? assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource!.BoneCount : (int?)null }));
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
                boundCaster72 |= effectId == 72 && current.NativeGraphics!.Models[caster].ActiveStagedResourceRow100 == 0x25FA3D8 && current.NativeGraphics.Models[caster].LinkedStagedResourceRow is not null;
                if (CrossedContactGate(prior, current, effectId))
                {
                    contacts++;
                    ColdNext("contact-" + contacts, current);
                }
                if (!initialized && after?.ReturnedDescriptor is > 0)
                {
                    Assert.Equal(effectId, Aqua(current).Effect);
                    Assert.NotNull(Aqua(current).Source);
                    Assert.Equal(JsonSerializer.Serialize(original.AquaResources!.Animation), JsonSerializer.Serialize(current.NativeEffectMemory!.Textures!.Animation!.Source));
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
                if (visibleChangedRgb == 0 && after?.ReturnedDescriptor is > 0 && Aqua(current).Effect == effectId)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = AquaPacketReferences(prior, current);
                    if (!owned.IsEmpty)
                    {
                        NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        NativeBattlePrimitiveFrame withoutFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() };
                        BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = withoutFrame } };
                        byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                        visibleChangedRgb = CountRgbChanges(pixels, plain);
                        if (visibleChangedRgb > 0)
                        {
                            NativeBattleRestorationPresentationTests.Publish(output!, "visible", current, pixels);
                            NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", without, plain);
                            ColdNext("live", current);
                            liveRestored = true;
                        }
                    }
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                if (initialized && Released(current))
                {
                    break;
                }
            }
            boundary = "acceptance";
            BattleSnapshot completed = battle.Capture();
            Assert.True(initialized, "The original Aqua module never initialized its selected controller.");
            Assert.True(liveRestored, "No significant original effect packet contribution reached cold continuation.");
            Assert.True(visibleChangedRgb > 0);
            Assert.Equal(1, contacts);
            Assert.Equal(expectedText is null ? 0 : 1, contactGlyphs);
            Assert.True(cameraChanged, "The original Aqua camera control never changed its retained camera.");
            Assert.True(stageChanged, "The original Aqua stage fade never changed the retained four-group color/depth owner.");
            Assert.True(effectId != 72 || boundCaster72, "The Blue Magic component never bound its embedded DAT with the actual linked resource row.");
            Assert.Equal(1, completions);
            Assert.True(completed.Events.Count(item => item.NativeModuleSound is not null) > initialSounds, "The selected component published no original module sound.");
            Assert.True(Released(completed), "The original returned task, active effect and action104 descriptor must release through native execution.");
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Controlled original Aqua action104 and seven-HP contact; no ordinary action or formula admission", Effect = effectId, exported.Selection, action.Command, action.ActionId, callbacks, DiagnosticMaximumCallbacks = 4096, contacts, contactGlyphs, completions, visibleChangedRgb, cameraChanged, stageChanged, boundCaster72, RestoredBoundaries = restoredBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

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
                byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
                Assert.Equal(scene.Render(direct, 320, 224).ToArray(), pixels);
                NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
                restoredBoundaries.Add(name);
                prior = previous;
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


    private static NativeBattleAquaState Aqua(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect == 55 ? snapshot.NativeEffectMemory!.Aqua55! : snapshot.NativeEffectMemory!.Aqua72!;

    private static string StageColors(BattleSnapshot snapshot) => JsonSerializer.Serialize(snapshot.NativeStage!.Groups.Select(group => new { group.ColorFlags, group.ColorDepth }));

    private static bool CrossedContactGate(BattleSnapshot prior, BattleSnapshot current, ushort effect)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } before || current.NativeGraphics!.OriginalEffect is not { ReturnedDescriptor: > 0 } after)
        {
            return false;
        }
        uint callback = NativeBattleAquaProfile.ForEffect(effect).Callback(0x808B20);
        return before.Pools[1].Slots.Select((task, index) => (task, index)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == callback && item.task.ReadByte(0x29) == 1 && after.Pools[1].Slots[item.index].ReadByte(0x29) == 2);
    }

    private static (BattleNativeAction Action, BattleEncounter Formation, BattleEnemyPlacement Enemy) SelectOriginalBinding(BattleAssets assets, ushort effect)
    {
        if (effect == 72)
        {
            BattleNativeAction action = assets.NativeActions.Where(row => row.Command == 15 && row.MagicId == effect).OrderBy(row => row.CrisisLevel).First();
            Assert.Equal(3, action.CharacterId);
            BattleEncounter formation = assets.LoadEncounter(94);
            return (action, formation, NativeBattleEffectFixtureExporter.SelectEnemy(formation));
        }
        BattleNativeAction[] actions = assets.NativeActions.Where(row => row.Command == 8 && row.MagicId == effect).ToArray();
        Assert.NotEmpty(actions);
        Dictionary<(int Model, int Level), NativeBattleAiDefinition> ais = [];
        foreach (BattleEncounter formation in Enumerable.Range(0, 1024).Select(assets.LoadEncounter))
        {
            BattleEnemyPlacement[] enemies = formation.Enemies.Where(enemy => enemy.Enabled && enemy.IsEnemyModel).ToArray();
            if (enemies.Length != 1 || enemies[0].Hidden || enemies[0].InitiallySuspended || enemies[0].Untargetable)
            {
                continue;
            }
            BattleEnemyPlacement enemy = enemies[0];
            if (!ais.TryGetValue((enemy.ModelId, enemy.Level), out NativeBattleAiDefinition? ai))
            {
                ai = assets.ReadEnemyAi(enemy.ModelId, enemy.Level);
                ais.Add((enemy.ModelId, enemy.Level), ai);
            }
            BattleNativeAction? selected = actions.FirstOrDefault(action => ai.Abilities.Any(ability => ability.Command == action.Command && ability.Id == action.ActionId));
            if (selected is not null && assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource!.BoneCount > 24)
            {
                _ = NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, selected);
                return (selected, formation, enemy);
            }
        }
        throw new InvalidDataException("No original single-enemy Aqua action binding with its required caster skeleton was found.");
    }
    internal static ImmutableHashSet<NativeBattlePrimitiveReference> AquaPacketReferences(BattleSnapshot prior, BattleSnapshot current)
    {
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (prior.NativeGraphics!.Auxiliary.Slots.Any(task => (task.Flags & 1) != 0) || prior.NativeGraphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0) || graphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleGraphicsModel? actor = graphics.Models.FirstOrDefault(model => (model.Flags0 & 6) == 2 && model.Flags2 is { } flags && (flags & 0x85) == 0);
        if (actor is null || graphics.Models.Take(actor.Index).Any(model => (model.Flags0 & 6) == 2 && model.Flags2 is { } flags && (flags & 0x84) == 0))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleGraphicsDefinition definition = current.Definition.NativeGraphics!;
        NativeBattleGraphicsResource resource = definition.Bindings.Single(binding => binding.ActorId == graphics.ActorIds[actor.Index]).Resource;
        if (actor.ActiveStagedResourceRow100 is not null)
        {
            resource = NativeBattleVictoryResources.Resolve(current.NativeFileStaging!, current.NativeModelCpuResources!, actor, resource);
        }
        if (resource.Geometry is not { } geometry || resource.PoseSource is not { } pose || actor.CurrentBodyBoneMatrices is null || actor.GeometryBounds52 is not { } bounds || actor.Color40 is not { } color || actor.Color44 is not { } shadowColor || actor.SequenceState?.MeshMask124 is not { } mask || current.NativeCamera?.Projection is not { } projection || prior.NativeCamera?.Projection?.View is not { } view)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        List<(int Offset, NativeBattleGeometryPrimitive Primitive)> expected = [];
        int cursor = 0;
        if ((actor.Flags0 & 0x20) == 0 && (actor.Flags2!.Value & 0x20) == 0 && shadowColor != 0x32000000)
        {
            bool gouraud = definition.PrimitiveCapabilities!.GouraudAlpha;
            NativeBattleShadowGeometry shadow = NativeBattlePoseMatrices.ShadowGeometry5088A0(actor, bounds, view, definition.Rules.ShadowSource!, gouraud);
            NativeBattleCameraProjection shadowProjection = projection with { View = view };
            NativeBattleModuleProjectedVertex[] vertices = shadow.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shadow.CameraMatrix, vertex, shadowProjection)).ToArray();
            NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(vertices, shadowColor, gouraud, 4121, 16);
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                if (attempt.Primitive is { } primitive && (byte)(primitive.Words[1] >> 24) is >= 0x20 and < 0x80)
                {
                    expected.Add((attempt.ByteOffsetRelative, primitive));
                }
            }
            cursor = batch.AllocationBytes;
        }
        ImmutableArray<NativeBattleGeometryPrimitive> body;
        if (resource.LinkedGeometry is { IsEmpty: false } linked && resource.LinkedGeometryUsesBodySkeleton == true)
        {
            if (geometry.Length < 2 || linked.Length != 1)
            {
                return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
            }
            ImmutableArray<NativeBattleGeometryPrimitive>.Builder parts = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
            for (int index = 0; index < geometry.Length; index++)
            {
                if ((mask & (1u << (index & 31))) != 0)
                {
                    parts.AddRange(NativeBattleRawGeometryPackets.Emit5099D0([index == 1 ? linked[0] : geometry[index]], NativeBattleBodyBoneMatrices.Require(actor, pose), projection, color, 1, null, 17, 4, index == 1 ? actor.LinkedTextureSlots : actor.BodyTextureSlots));
                }
            }
            body = parts.ToImmutable();
        }
        else
        {
            body = NativeBattleRawGeometryPackets.Emit5099D0(geometry, NativeBattleBodyBoneMatrices.Require(actor, pose), projection, color, mask, null, 17, 4, resource.GeometryRelocated507550 ? null : actor.BodyTextureSlots);
        }
        foreach (NativeBattleGeometryPrimitive primitive in body)
        {
            expected.Add((cursor, primitive));
            cursor = checked(cursor + primitive.Words.Length * 4);
        }
        if (expected.Count == 0)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleLogicalCommand[] commands = graphics.PrimitiveFrame!.LastCommands.Select(resolved => resolved.Command).Where(command => command.Packet.Bank is "off_B6D070" or "off_B6D094").ToArray();
        foreach (NativeBattleLogicalCommand candidate in commands.Where(command => Matches(command, expected[0].Primitive)))
        {
            int start = candidate.Packet.ByteOffset - expected[0].Offset;
            if (start <= 0 || !expected.All(item => commands.Any(command => command.Packet.Bank == candidate.Packet.Bank && command.Packet.Buffer == candidate.Packet.Buffer && command.Packet.ByteOffset == start + item.Offset && Matches(command, item.Primitive))))
            {
                continue;
            }
            // The original effect runs before ordinary model submission. Match that complete renderer allocation before admitting its preceding effect packets.
            return commands.Where(command => command.Packet.Bank == candidate.Packet.Bank && command.Packet.Buffer == candidate.Packet.Buffer && command.Packet.ByteOffset < start && command.Opcode is >= 0x20 and < 0x80).Select(command => command.Packet).ToImmutableHashSet();
        }
        return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;

        static bool Matches(NativeBattleLogicalCommand command, NativeBattleGeometryPrimitive primitive)
        {
            return command.WordOffset == 1 && command.OrderingBucket == primitive.Bucket && command.Words.AsSpan().SequenceEqual(primitive.Words.AsSpan().Slice(1)) && command.VertexPayload4.AsSpan().SequenceEqual(primitive.VertexPayload4.AsSpan());
        }
    }


    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
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
