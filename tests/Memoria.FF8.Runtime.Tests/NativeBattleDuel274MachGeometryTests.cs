using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274MachGeometryTests
{
    [Fact]
    public void GeometryDeformationAlwaysReadsSavedVerticesAndPreservesCurrentPaddingBeforeExactRestore()
    {
        byte[] image = new byte[624];
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(4), 77);
        BinaryPrimitives.WriteInt16LittleEndian(image.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(image.AsSpan(10), 200);
        BinaryPrimitives.WriteInt16LittleEndian(image.AsSpan(12), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(14), 0xBEEF);
        NativeBattleDuel274Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0), NativeBattleFileStaging.Unknown().Write(0x10E9530, image), new(1));
        byte[] raw = new byte[checked((int)(NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address))];
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(checked((int)(0x110D3B4 - NativeBattleDuel274Resources.Address))), 64);
        NativeBattleDuel274Resources resources = new(raw.ToImmutableArray(), []);
        uint backup = NativeBattleEffectNodeMemory.ArenaAddress + 0x5000;
        frame = NativeBattleDuel274MachGeometry.Backup6D67B0(frame, backup);
        frame = NativeBattleDuel274MachGeometry.Deform68BCD0(frame, resources, backup, 256, 0);
        Assert.Equal(new short[] { 98, 200, 1 }, Xyz(frame));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging.Read(0x10E953E, 2)));
        frame = NativeBattleDuel274MachGeometry.Deform68BCD0(frame, resources, backup, 1280, 1024);
        Assert.Equal(new short[] { 190, -100, 5 }, Xyz(frame));
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleDuel274MachGeometry.Restore6D67B0(frame, backup);
        Assert.Equal(image.AsSpan(8).ToArray(), frame.Staging.Read(0x10E9538, 616));
    }

    [Fact]
    public void BodyMatrixKeepsPivotFixedWhileApplyingNativeScaleBeforeCameraComposition()
    {
        NativeBattleActionMatrix world = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix view = world with { X = 100, Y = 200, Z = 300 };
        NativeBattleActionMatrix result = NativeBattleDuel274Body.Matrix68D1D0(world, view, new(0, 0, 0), new(0, 100, 0), new(4096, 2048, 4096));
        Assert.Equal(new short[] { 4096, 0, 0, 0, 2048, 0, 0, 0, 4096 }, result.Rotation);
        Assert.Equal(110, result.X);
        Assert.Equal(270, result.Y);
        Assert.Equal(330, result.Z);
    }

    private static short[] Xyz(NativeBattleDuel274Frame frame)
    {
        byte[] bytes = frame.Staging.Read(0x10E9538, 6);
        return Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
    }
}
