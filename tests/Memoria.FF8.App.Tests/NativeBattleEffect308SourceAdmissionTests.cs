using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect308SourceAdmissionTests
{
    [Fact]
    public void Fresh_intact_trio_has_current_original_rigs_sequences_and_cold_owners_without_advancing()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT308_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT308_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh intact-trio standalone and a new admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(0, source.Frame);
        Assert.Equal(794, source.Definition.EncounterId);
        Assert.Equal(65, source.Definition.StageId);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.True(graphics.EntryReady);
        Assert.Null(graphics.OriginalEffect);
        NativeBattleEffectMemory memory = source.NativeEffectMemory!;
        Assert.Equal(0, memory.Active);
        NativeBattleEffect308State state = memory.Effect308!;
        Assert.NotNull(state);
        Assert.False(state.ScriptSourcesBound);
        Assert.Empty(state.ScriptStorage);
        Assert.Single(state.FixedBanks);
        Assert.Equal(4, state.CommonQueueStorage.Count);
        Assert.Equal(new byte[RootBankLength], state.ReadBank(RootBank).Read(0, RootBankLength));
        foreach (NativeBattleCureFamilyPool pool in CommonPools)
        {
            Assert.Equal(new byte[CommonQueueLength], state.ReadCommonQueue(pool.Descriptor).Read(0, CommonQueueLength));
        }
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        BattleEncounter formation = assets.LoadEncounter(794);
        Assert.Equal(65, formation.StageId);
        Assert.Equal(3, formation.Enemies.Count);
        Assert.Equal(3, source.Combatants.Count(actor => !session.GetCombatantDefinition(actor.Id).IsPlayer && actor.NativeParticipant is { Active: true }));
        List<object> actors = [];
        List<object> sequences = [];
        for (int index = 0; index < 3; index++)
        {
            int physical = index + 3;
            int original = index + 105;
            int boneCount = index == 0 ? 44 : 6;
            string actorId = graphics.ActorIds[physical]!;
            BattleCombatantDefinition definition = session.GetCombatantDefinition(actorId);
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, current => current.Id == actorId);
            Assert.Equal(original, definition.ModelId);
            Assert.Equal(index, definition.SceneSlot);
            Assert.True(definition.InitialActive);
            Assert.False(definition.InitialHidden);
            Assert.False(definition.InitialSuspended);
            Assert.False(definition.InitialUntargetable);
            Assert.True(actor.NativeParticipant!.Active);
            Assert.Equal((byte)physical, actor.NativeParticipant.ActorIndex);
            Assert.False(actor.NativeParticipant.Hidden);
            Assert.False(actor.NativeParticipant.Suspended);
            Assert.False(actor.NativeParticipant.Untargetable);
            BattleEnemyPlacement placement = Assert.Single(formation.Enemies, current => current.Slot == index && current.ModelId == original);
            Assert.True(placement.Enabled);
            Assert.False(placement.Hidden);
            Assert.False(placement.InitiallySuspended);
            Assert.False(placement.Untargetable);
            Assert.Equal(141, placement.Level);
            Assert.Equal(new NativeBattleVector(0, 0, -3500), placement.NativePosition);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.Equal((byte)(original + 16), model.ModelId4);
            Assert.NotEqual(0, model.Flags0 & 2);
            NativeBattleModelTransform transform = model.Transform!;
            Assert.NotNull(transform);
            Assert.Equal(placement.NativePosition, transform.Initial20);
            NativeBattleActionMatrix world = model.GeometryWorldMatrix!;
            Assert.NotNull(world);
            world.Validate();
            NativeBattlePackedMatrix32 packedWorld = RequirePacked(world);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            NativeBattlePoseSource pose = resource.PoseSource!;
            Assert.NotNull(pose);
            Assert.Equal(boneCount, pose.BoneCount);
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
            Assert.Equal(boneCount, bones.Length);
            foreach (NativeBattleActionMatrix bone in bones)
            {
                _ = RequirePacked(bone);
            }
            ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry!.Value;
            NativeBattleRawMesh mesh = Assert.Single(geometry);
            mesh.Validate();
            Assert.Equal(index == 0 ? 679 : 373, mesh.Vertices.Length);
            Assert.Equal(index == 0 ? 1082 : 406, mesh.Polygons.Count(polygon => !polygon.Quad));
            Assert.Equal(index == 0 ? 136 : 168, mesh.Polygons.Count(polygon => polygon.Quad));
            ImmutableArray<NativeBattleRawVertexGroup> packed = mesh.PackedVertexGroups!.Value;
            Assert.Equal(index == 0 ? 33 : 4, packed.Length);
            Assert.All(packed, group =>
            {
                Assert.InRange((int)group.Bone, 0, boneCount - 1);
                Assert.Equal(group.Count * 6 + 6, group.Bytes.Length);
            });
            Assert.Equal(mesh.Vertices.Length, packed.Sum(group => group.Count));
            if (index == 0)
            {
                RequireSequence(resource, physical, 14, 83, [0xC3, 0x2E, 0xE5, 0x7F], [0xE7, 0xF9, 0xA2]);
            }
            else
            {
                RequireSequence(resource, physical, 14, 4, [0x0C, 0x0D, 0xE6, 0xFF], [0xE6, 0xFF]);
                RequireSequence(resource, physical, 18, 15, [0x0E, 0xC1, 0x1E, 0xE5, 0x7F], [0xE7, 0xF9, 0xA2]);
            }
            actors.Add(new { Physical = physical, OriginalModel = original, NativeModel = model.ModelId4, definition.SceneSlot, RawLevel = placement.Level, ResolvedLevel = definition.Level, Position = placement.NativePosition, actor.NativeParticipant, Bones = bones.Length, CurrentWorld = world, PackedWorld = packedWorld, model.CurrentBodyBoneMatrices, model.Sequence, model.SequenceState, Vertices = mesh.Vertices.Length, Triangles = mesh.Polygons.Count(polygon => !polygon.Quad), Quads = mesh.Polygons.Count(polygon => polygon.Quad), PackedGroups = packed.Select(group => new { group.Bone, group.Count, Bytes = group.Bytes.Length }).ToArray() });
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 286 && row.MagicId == 308 && row.KernelSection == 3 && row.KernelIndex == 286);
        Assert.Equal(20, action.RawRecord.Length);
        NativeBattleEffect308Resources resources = game.ReadNativeEffect308Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([308])[307];
        Assert.Same(resources, binding.Effect308Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource(TextureName, InitializerGlobal), binding.InitializerResource);
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        using FileStream evidence = new(Path.Combine(output!, "current-input-admission.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(evidence, new { Status = "passed", Scope = "Read-only fresh intact-trio current source; no callbacks, action queue, GTE preparation, ordinary AI, contact or gameplay", source.Frame, graphics.Callback, source.Definition.EncounterId, source.Definition.StageId, Actors = actors, Sequences = sequences, KernelAction = new { action.Command, action.ActionId, action.MagicId, action.KernelSection, action.KernelIndex, action.Name, action.DamagePresentation, action.AttackType, action.TargetFlags, action.HitCount, action.RawRecord }, ColdState = new { state.ScriptSourcesBound, ScriptOwners = state.ScriptStorage.Count, FixedBanks = state.FixedBanks.Count, CommonQueues = state.CommonQueueStorage.Count }, source.NativeCrtRandom, source.NativeGlobalRandom, source.NativeGlobalRandomCalls, FormulaRandom = source.Random, SnapshotUnchanged = true });

        void RequireSequence(NativeBattleGraphicsResource resource, int physical, byte sequence, int length, byte[] prefix, byte[] tail)
        {
            Assert.True(resource.Contains(sequence));
            int start = resource.SequenceStart(sequence);
            int end = resource.SequenceEnd(sequence);
            byte[] bytes = resource.SequenceSection.AsSpan().Slice(start, end - start).ToArray();
            Assert.Equal(length, bytes.Length);
            Assert.True(bytes.AsSpan().StartsWith(prefix));
            Assert.True(bytes.AsSpan().EndsWith(tail));
            sequences.Add(new { Physical = physical, Sequence = sequence, Bytes = bytes, Length = bytes.Length, SelectedPrefix = prefix, SelectedTail = tail });
        }
    }

    private static NativeBattlePackedMatrix32 RequirePacked(NativeBattleActionMatrix matrix)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        NativeBattleActionMatrix decoded = packed.ReadMatrix();
        Assert.Equal(matrix.Rotation.ToArray(), decoded.Rotation.ToArray());
        Assert.Equal(matrix.X, decoded.X);
        Assert.Equal(matrix.Y, decoded.Y);
        Assert.Equal(matrix.Z, decoded.Z);
        Assert.False(packed.Known[18]);
        Assert.False(packed.Known[19]);
        return packed;
    }

}
