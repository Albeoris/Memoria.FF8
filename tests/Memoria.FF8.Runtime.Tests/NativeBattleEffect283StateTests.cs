using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect283StateTests
{
    [Fact]
    public void Cold_owner_contains_only_exact_sparse_globals_and_separate_root()
    {
        NativeBattleEffect283State state = NativeBattleEffect283State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[12], state.Read(BaseAddress, 12));
        Assert.Equal(new byte[8], state.Read(CursorGlobal, 8));
        Assert.Equal(new byte[32], state.Read(0x2507E70, 32));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Equal(0x648, MainLength);
        Assert.Equal(MainLength, 12 + 0x18F * 4);
        Assert.Equal(MainProgrammeWorkspaceOffset + MainProgrammeWorkspaceLength, SecondaryProgrammeWorkspaceOffset);
        Assert.Equal(MainLength, SecondaryProgrammeWorkspaceOffset + SecondaryProgrammeWorkspaceLength);
        Assert.Equal(8, FirstPacketOffset - ScratchOffset);
        Assert.Equal(PacketBankBytes, SecondPacketOffset - FirstPacketOffset);
        foreach (uint address in new[] { BaseAddress + 12, MainDescriptor, RootDescriptor, RootBank, CursorGlobal - 1, ArenaGlobal + 4, ArenaAddress })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
    }

    [Fact]
    public void Unknown_import_and_partial_JSON_preserve_knownness()
    {
        NativeBattleEffect283State unknown = NativeBattleEffect283State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.RootBankStorage!.Read(0, 20));
        NativeBattleEffect283State state = unknown.WriteWord(CasterGlobal, 3) with { RootBankStorage = null };
        NativeBattleEffect283State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect283State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal((ushort)3, restored.ReadWord(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(CasterGlobal));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(12, 1) }).Validate());
    }

    [Fact]
    public void Warp_factor_twenty_and_extended_distance_share_physical_bytes()
    {
        byte[] warp = new byte[WarpTablesLength];
        warp[19 * 256] = 0xCD;
        warp[19 * 256 + 1] = 0xAB;
        warp[^1] = 0x67;
        NativeBattleEffect283Resources resources = Resources() with { WarpTables21377 = warp.ToImmutableArray() };
        Assert.Equal((ushort)0xABCD, resources.ReadWarpFactor(20, 0));
        Assert.Equal((byte)0xCD, resources.ReadDistance(0));
        Assert.Equal((byte)0x67, resources.ReadDistance(16512));
        Assert.Throws<InvalidDataException>(() => resources.ReadDistance(16513));
        Assert.Throws<InvalidDataException>(() => resources.ReadWarpFactor(0, 0));
        Assert.Throws<InvalidDataException>(() => resources.ReadWarpFactor(20, 128));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(WarpTablesAddress - 1, 2));
    }

    internal static NativeBattleEffect283Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        W(camera, 0, 2);
        W(camera, 2, 8);
        W(camera, 4, 32);
        W(camera, 6, CameraLength);
        NativeBattleEffect283Resources resources = new(Source(MainProgrammeAddress, MainProgrammeLength, 5, 17, 60), Source(SecondaryProgrammeAddress, SecondaryProgrammeLength, 2, 4, 36), new("FF8_EN:1094FC0", camera.ToImmutableArray()), 0x61B71, [0x00005000, 0x00500000, 0x00500050, 0x00000050, 0x00005000, 0x00500000, 0x00500050, 0x00000050], new byte[WarpTablesLength].ToImmutableArray());
        resources.Validate();
        return resources;
    }

    private static NativeBattleGfAnimationSource Source(uint address, int length, int geometries, int nodes, int frames)
    {
        byte[] bytes = new byte[length];
        D(bytes, 0, (uint)geometries);
        D(bytes, 4, 256);
        for (int index = 0; index < geometries; index++)
        {
            D(bytes, 8 + index * 4, 64);
        }
        D(bytes, 64, 20);
        D(bytes, 68, 1);
        D(bytes, 72, 1);
        D(bytes, 256, (uint)nodes);
        D(bytes, 260, (uint)frames);
        for (int index = 0; index < nodes; index++)
        {
            D(bytes, 264 + index * 4, (uint)(8 + nodes * 4 + index * 8));
        }
        return new(address, bytes.ToImmutableArray());
    }

    private static void W(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)value));
    private static void D(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
