using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCuragaNodeCycleAssetsTests
{
    private const int Emitters = 0xC000;
    private const int Geometry = Emitters + 0x25F8;

    [Fact]
    public void Original_first_birth_spawns_source_five_mesh_and_four_children_in_the_same_walk_with_independent_JSON_continuation()
    {
        var fixture = Fixture();
        NativeBattleCuragaNodeCycleStep step = Advance(fixture.Frame, new(1), true);
        Assert.Equal(new byte[] { 5, 10, 0, 1, 9 }, Enumerable.Range(0, 5).Select(index => step.Frame.Memory.Arena!.Read(Emitters + index * 108 + 0x6A, 1)[0]).ToArray());
        Assert.Equal(new byte[] { 32, 26, 35, 34, 25 }, Enumerable.Range(0, 5).Select(index => step.Frame.Memory.Arena!.Read(Emitters + index * 108 + 0x64, 1)[0]).ToArray());
        Assert.Equal(new byte[] { 5, 1, 1, 0 }, step.Frame.Memory.Arena!.Read(Geometry + 0x1D6, 4));
        Assert.Equal(new byte[] { 1, 0 }, step.Frame.Memory.Arena.Read(Geometry + 0x1CA, 2));
        uint mesh = BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Arena.Read(Geometry + 0x170, 4));
        Assert.Equal(Pointer(step.Frame, 0x84 + 5 * 4), mesh);
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(step.Frame.Memory.CuragaExecutable!.Read(mesh + 4, 4)));
        NativeBattleCuragaFrame restored = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(step.Frame))!;
        NativeBattleCuragaNodeCycleStep next = Advance(step.Frame, step.Random, false);
        NativeBattleCuragaNodeCycleStep cloneNext = Advance(restored, step.Random, false);
        Assert.Equal(next.Random, cloneNext.Random);
        Assert.Equal(JsonSerializer.Serialize(next.Frame.Memory.Arena), JsonSerializer.Serialize(cloneNext.Frame.Memory.Arena));
        Assert.Equal(next.Frame.Memory.CuragaExecutable!.Data!.Value.ToArray(), cloneNext.Frame.Memory.CuragaExecutable!.Data!.Value.ToArray());

        NativeBattleCuragaNodeCycleStep Advance(NativeBattleCuragaFrame frame, NativeCrtRandomState random, bool birth) => NativeBattleCuragaNodeKinematics.AdvanceList8804D0(frame, fixture.Task, random, _ => throw new InvalidOperationException(), birth);
    }

    [Fact]
    public void Original_birth_frames_and_all_private_node_updates_exhaust_their_actual_lists_without_a_fabricated_duration()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame;
        NativeCrtRandomState random = new(1);
        int completedAt = -1;
        for (ushort counter = 0; counter < 200; counter++)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(fixture.Task, hierarchy.Read(fixture.Task).WriteWord(0x34 + 0x18, counter)).Pools } };
            NativeBattleCuragaNodeCycleStep step = NativeBattleCuragaNodeKinematics.AdvanceList8804D0(frame, fixture.Task, random, _ => throw new InvalidOperationException(), true);
            frame = step.Frame;
            random = step.Random;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task);
            if (counter >= 45 && task.ReadWord(0x34 + 0x14) == 0 && task.ReadWord(0x34 + 0x16) == 0)
            {
                completedAt = counter;
                Assert.Equal(0u, task.ReadDword(0x34 + 0x2C));
                break;
            }
        }
        Assert.InRange(completedAt, 45, 199);
    }

    private static (NativeBattleCuragaFrame Frame, uint Task) Fixture()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleCuragaExecutableState executable = NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(game.ReadNativeCuragaResources().Executable!);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x26D7EC0, 0x26D76D0, 676, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x87E1A0, 676);
        NativeBattleModulePoolTask task = root.Hierarchy.Read(root.Address)
            .WriteDword(0x34 + 0x224, NativeBattleCuragaExecutableState.Address + 4)
            .WriteDword(0x34 + 0x228, NativeBattleCuragaExecutableState.Address + 0x44)
            .WriteDword(0x34 + 0x22C, NativeBattleCuragaExecutableState.Address + 0x84)
            .WriteDword(0x34 + 0x230, NativeBattleCuragaExecutableState.Address + 0xC4)
            .WriteWord(0x34 + 0x1C, 1);
        hierarchy = root.Hierarchy.Write(root.Address, task);
        NativeBattleOriginalEffectState effect = new(28, 27, new(28, 0x87DB10, 0x87DAF0), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(27) with { CuragaExecutable = executable };
        NativeBattleCuragaState state = new(0, null, null, Geometry + 0x16F80, NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 }) { Auxiliary25F8 = Emitters, Auxiliary16F80 = Geometry, Counter26D76C0 = 0, Counter26D82E8 = 0 };
        return (new(memory, effect, state), root.Address);
    }

    private static uint Pointer(NativeBattleCuragaFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.CuragaExecutable!.Read(NativeBattleCuragaExecutableState.Address + (uint)offset, 4));
}
