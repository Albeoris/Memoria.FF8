using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278MirrorMeshTests
{
    [Fact]
    public void Accepted_quad_keeps_uvs_depth_bias_and_nonsequential_mode_links()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 0x1000 };
        NativeBattleCommonPacketWrite result = Draw(source, Mesh(1000, false));
        Assert.Equal(0x1038, result.Process.ByteCursor);
        Assert.Equal(new[] { 0x1028, 0x1000, 0x1030 }, result.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        byte[] lengths = new byte[256];
        lengths[0x2C] = 9;
        lengths[0xE1] = 1;
        ImmutableArray<NativeBattleLogicalCommand> commands = result.Ordering.Traverse(lengths.ToImmutableArray());
        Assert.Equal(new byte[] { 0xE1, 0x2C, 0xE1 }, commands.Select(command => command.Opcode));
        Assert.All(commands, command => Assert.Equal("off_B6D070", command.Packet.Bank));
        Assert.Equal(0xE1000420u, commands[0].Words[0]);
        Assert.Equal(0xE1000620u, commands[2].Words[0]);
        Assert.Equal(0x2C787878u, commands[1].Words[0]);
        Assert.Equal(0x01020304u, commands[1].Words[2]);
        Assert.Equal(0x11223344u, commands[1].Words[4]);
        Assert.Equal(0x55667788u, commands[1].Words[6]);
        Assert.Equal(0x5566u, commands[1].Words[8]);
        Assert.All(commands, command => Assert.Equal(47, command.OrderingBucket));
        Assert.Throws<NotSupportedException>(() => source.ReadCommonPacket(0x1000, 4));
        NativeBattlePrimitiveProcess independent = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(source))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Draw(independent, Mesh(1000, false))));
    }

    [Fact]
    public void First_three_near_rejection_keeps_prefix_without_reading_fourth_vertex_or_allocating_modes()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 0x1000 };
        NativeBattleCommonPacketWrite result = Draw(source, Mesh(0, true));
        Assert.Equal(0x1000, result.Process.ByteCursor);
        Assert.Empty(result.Ordering.Proxies);
        Assert.Equal(0x09000000u, BinaryPrimitives.ReadUInt32LittleEndian(result.Process.ReadCommonPacket(0x1000, 4)));
        Assert.Equal(0x2C787878u, BinaryPrimitives.ReadUInt32LittleEndian(result.Process.ReadCommonPacket(0x1004, 4)));
        Assert.Equal(0x01020304u, BinaryPrimitives.ReadUInt32LittleEndian(result.Process.ReadCommonPacket(0x100C, 4)));
        Assert.Throws<NotSupportedException>(() => result.Process.ReadCommonPacket(0x1008, 4));
        Assert.Throws<NotSupportedException>(() => result.Process.ReadCommonPacket(0x1028, 16));
    }

    private static NativeBattleCommonPacketWrite Draw(NativeBattlePrimitiveProcess process, NativeBattleStaticMeshResource mesh)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return NativeBattleCarbuncle278MirrorMesh.Append6839B0(process, NativeBattleLogicalOrdering.Clear(), mesh, matrix, new(matrix, 200, 160, 112), 120);
    }

    private static NativeBattleStaticMeshResource Mesh(short depth, bool invalidFourth)
    {
        byte[] bytes = new byte[80];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] coordinates = [-10, -10, 10, -10, -10, 10, 10, 10];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), coordinates[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), coordinates[index * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), depth);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60 + index * 2), (ushort)(index == 3 && invalidFourth ? 0xFFF0 : index * 2));
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0x01020304);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x11223344);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0x55667788);
        return new(0, 0x10C0D08, bytes.ToImmutableArray());
    }
}
