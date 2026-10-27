using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaAnimatedAnchorTests
{
    [Fact]
    public void Curaga_original_mode4_wrapper_reads_current_channels_and_preserves_unwritten_padding_and_fourth_DWORD()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(4, 0, false)]);
        NativeBattleCuragaFrame frame = fixture.Frame;
        byte[] bank = NativeBattleStorageSnapshots.Capture(frame.Memory.CuragaExecutable!.Data!).Data.ToArray();
        uint address = NativeBattleCuragaExecutableState.Address;
        int[] fields = [0x100, 0x108, 0x104, 0x10C, 0x110, 0x114, 0x118, 0x11C, 0x120, 0x124, 0x128];
        for (int index = 0; index < fields.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(0x104 + fields[index]), address + (uint)(0x2000 + index * 4));
        }
        bank[0x2008] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x2028), 4096);
        frame = frame with { Memory = frame.Memory with { CuragaExecutable = new(1, bank.ToImmutableArray()) } };
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Task).WriteDword(0x34 + 0x1D4, 100u << 16).WriteDword(0x34 + 0x1D8, 200u << 16).WriteDword(0x34 + 0x1DC, 300u << 16);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(fixture.Task, task).Pools } };
        NativeBattleCuragaNodeAllocation allocation = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0, 0, 0);
        int node = allocation.ArenaOffset!.Value;
        frame = allocation.Frame;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(node + 0x58, [0xEF, 0xBE, 0xAD, 0xDE]).MarkUnknown(node + 0x2C + 18, 2) } };
        frame = NativeBattleCuragaNodeKinematics.InitializeEmitter882990(frame, fixture.Task, node, _ => throw new InvalidOperationException());
        Assert.Equal(new uint[] { 100u << 16, 200u << 16, 300u << 16, 0xDEADBEEF }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(node + 0x4C + index * 4, 4))));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(node + 0x2C + 18, 2));
        NativeBattleCuragaFrame clone = RoundTrip(frame);
        Assert.Equal(SnapshotJson(NativeBattleCuragaNodeKinematics.FollowAnimatedAnchor8822A0(frame, fixture.Task, node)), SnapshotJson(NativeBattleCuragaNodeKinematics.FollowAnimatedAnchor8822A0(clone, fixture.Task, node)));
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
