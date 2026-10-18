using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshSwordTrailResourcesTests
{
    [Fact]
    public void Original_trail_colors_are_authenticated_cached_and_required_when_imported()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshSwordTrailResources resources = game.ReadNativeGilgameshSwordTrailResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshSwordTrailResources());
        Assert.Equal(new uint[] { 0x3A602020, 0x3A407070, 0x3A903030, 0x3A303090 }, resources.Colors.ToArray());
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGilgameshSwordTrailResources>(JsonSerializer.Serialize(resources))));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleGilgameshSwordTrailResources>("{}"));
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors.SetItem(3, 0x3A303091) }).Validate());
    }

    [Fact]
    public void Original_selected_sword_pose_drives_ten_history_samples_with_exact_json_continuation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleEffectOwnedModelResource> resources = game.ReadNativeGilgameshModelResources().StaticModels;
        NativeBattleGilgameshSwordTrailResources trailResources = game.ReadNativeGilgameshSwordTrailResources();
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        NativeBattleScriptedGraphicsMemory registers = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();
        NativeBattleModulePoolTask swordTask = new NativeBattleModulePoolTask(1, 0, null, 0x594FD0, new byte[24].ToImmutableArray()).WriteWord(14, 3);
        NativeBattleGilgameshModelMemory models = NativeBattleGilgameshSwords.Step594FD0(NativeBattleGilgameshModelMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), swordTask, resources, view, projection, registers, 0x201).ModelMemory;
        NativeBattleGilgameshSwordTrailState state = NativeBattleGilgameshSwordTrailState.FromColdProcess();
        state.Validate();
        Assert.Empty(state.Memory.Regions);
        Assert.Empty(state.Memory.UnknownRegions);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x5943D0, new byte[24].ToImmutableArray());
        for (int frame = 0; frame < 10; frame++)
        {
            NativeBattleGilgameshSwordTrailStep step = Run(state, task);
            Assert.Equal(frame + 1, step.GatheredCount);
            Assert.Equal(frame == 9 ? 2 : 0, step.Result);
            Assert.Equal((ushort)(frame + 1), step.UpdatedTask.ReadWord(12));
            Assert.Throws<NotSupportedException>(() => step.State.Memory.Read(NativeBattleGilgameshSwordTrailState.History + frame * 20 + 10, 2));
            Assert.Throws<NotSupportedException>(() => step.State.Memory.Read(NativeBattleGilgameshSwordTrailState.GatherA + 6, 2));
            if (frame == 0)
            {
                Assert.Null(step.PoolCursorAdvance);
                Assert.Empty(step.Packets.Attempts);
            }
            else
            {
                Assert.Equal(step.Packets.AllocationBytes + 36, step.PoolCursorAdvance);
                Assert.InRange(step.Packets.Attempts.Length, 30, 60);
            }
            if (frame == 4)
            {
                NativeBattleGilgameshSwordTrailState restored = JsonSerializer.Deserialize<NativeBattleGilgameshSwordTrailState>(JsonSerializer.Serialize(step.State))!;
                Assert.Equal(JsonSerializer.Serialize(Run(step.State, step.UpdatedTask)), JsonSerializer.Serialize(Run(restored, step.UpdatedTask)));
            }
            state = step.State;
            task = step.UpdatedTask;
            NativeBattleGilgameshModelOwner owner = models.Read(0x21FF6C4);
            models = models.Write(owner.Address, NativeBattleCactuarRawModel.Advance5A8FD0(owner.State, resources[3], new(owner.ControlAddress, 4)).State);
        }

        NativeBattleGilgameshSwordTrailStep Run(NativeBattleGilgameshSwordTrailState current, NativeBattleModulePoolTask currentTask) => NativeBattleGilgameshSwordTrail.Step5943D0(current, currentTask, 3, Bone, view, projection, registers, NativeBattleGteColorInterpolation.CodeConstants45F270, trailResources, 0);

        NativeBattleVector Bone(int index, byte selector, short fraction)
        {
            NativeBattleCactuarRawModelState model = models.Read(0x21FF4F0u + (uint)index * 0x9C).State;
            ReadOnlySpan<byte> bytes = model.Model.AsSpan();
            NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(offset => BinaryPrimitives.ReadInt16LittleEndian(model.Model.AsSpan().Slice(0x40 + offset * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x54)), BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x58)), BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x5C)), 4096);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources[index].PoseSource, model.DecodedPose!, matrix, selector, fraction);
        }
    }
}
