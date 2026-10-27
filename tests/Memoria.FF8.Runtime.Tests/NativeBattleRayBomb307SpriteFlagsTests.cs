using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRayBomb307SpriteFlagsTests
{
    [Fact]
    public void Flags_C_preserve_tint_and_only_inset_right_U_bottom_V_without_changing_GTE()
    {
        var fixture = Fixture(2000);
        NativeBattleModuleSpriteDraw ordinary = Draw(fixture, 4);
        NativeBattleModuleSpriteDraw inset = Draw(fixture, 12);
        NativeBattleGeometryPrimitive a = Assert.Single(ordinary.Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        NativeBattleGeometryPrimitive b = Assert.Single(inset.Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        Assert.Equal(0x2C323232u, b.Words[1]);
        Assert.Equal((byte)11, (byte)b.Words[5]);
        Assert.Equal((byte)21, (byte)(b.Words[7] >> 8));
        Assert.Equal((ushort)0x150B, (ushort)b.Words[9]);
        Assert.Equal((byte)12, (byte)a.Words[5]);
        Assert.Equal((byte)22, (byte)(a.Words[7] >> 8));
        Assert.Equal(a.Words[3], b.Words[3]);
        Assert.Equal(a.Words[2], b.Words[2]);
        Assert.Equal(NativeBattleTestSnapshots.Json(ordinary.Memory), NativeBattleTestSnapshots.Json(inset.Memory));
        Assert.Equal(1u, U32(inset.Temporary180, 0xB0));
        Assert.Throws<NotSupportedException>(() => inset.Temporary180.Read(0x1F, 1));
        Assert.Null(inset.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Bit8_does_not_bypass_native_camera_roll_or_read_unwritten_caller_angle()
    {
        var fixture = Fixture(2000);
        short roll = 1024;
        NativeBattleModuleSpriteDraw inset = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary.Write(0x24, BitConverter.GetBytes((ushort)12)), _ => fixture.Resource, Projection, roll);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.RotateZ56D090(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, -roll);
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(expected.Rotation[index], BinaryPrimitives.ReadInt16LittleEndian(inset.Temporary180.Read(0x84 + index * 2, 2)));
        }
        Assert.Throws<NotSupportedException>(() => inset.Temporary180.Read(8, 4));
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(inset.Temporary180.Read(0x96, 2)));
        NativeBattleEffectArena d8 = NativeBattleEffectNodeContextMemory.Copy(fixture.Temporary.Write(0x24, BitConverter.GetBytes((ushort)12)), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 216), 0, 180);
        Assert.Throws<InvalidDataException>(() => NativeBattleProjectedSpriteD8.Draw(new(fixture.Memory, d8), _ => fixture.Resource, Projection, roll, 17));
    }

    [Fact]
    public void Near_rejected_prefix_contains_inset_right_U_but_bottom_V_remains_unwritten()
    {
        var fixture = Fixture(0);
        NativeBattleModuleSpriteDraw result = Draw(fixture, 12);
        NativeBattleGfGeometryAttempt rejected = Assert.Single(result.Batch.Attempts);
        Assert.Null(rejected.Primitive);
        Assert.Equal((byte)11, rejected.Data[20]);
        Assert.Equal((byte)11, rejected.Data[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[20]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[29]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[37]);
        Assert.Equal(1u, U32(result.Temporary180, 0xB0));
        Assert.Equal(0, result.Batch.AllocationBytes);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 255, 255)]
    [InlineData(254, 255, 8, 1, 255, 255)]
    public void Inset_uses_signed_upper_clamp_then_native_BYTE_wrap(int u, int v, int width, int height, byte expectedU, byte expectedV)
    {
        var fixture = Fixture(2000);
        byte[] bytes = fixture.Resource.Bytes.ToArray();
        bytes[18] = (byte)width;
        bytes[20] = (byte)height;
        bytes[22] = (byte)u;
        bytes[23] = (byte)v;
        fixture.Resource = fixture.Resource with { Bytes = bytes.ToImmutableArray() };
        NativeBattleGeometryPrimitive primitive = Assert.Single(Draw(fixture, 12).Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        Assert.Equal(expectedU, (byte)primitive.Words[5]);
        Assert.Equal(expectedV, (byte)(primitive.Words[7] >> 8));
    }

    [Fact]
    public void Inset_and_extended_bit_persist_across_JSON_next_and_empty_frame_is_lazy_about_RGB()
    {
        var fixture = Fixture(2000);
        byte[] bytes = fixture.Resource.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 0x80000001u);
        fixture.Resource = fixture.Resource with { Bytes = bytes.ToImmutableArray() };
        NativeBattleModuleSpriteDraw first = Draw(fixture, 12);
        Assert.Equal((ushort)0x10C, BinaryPrimitives.ReadUInt16LittleEndian(first.Temporary180.Read(0x24, 2)));
        Assert.Equal(52, first.Batch.AllocationBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 1);
        NativeBattleModuleResource positive = fixture.Resource with { Bytes = bytes.ToImmutableArray() };
        NativeBattleModuleSpriteDraw restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleSpriteDraw>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleModuleSpriteDraw Next(NativeBattleModuleSpriteDraw current) => NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, current.Temporary180, _ => positive, Projection, 0);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
        byte[] empty = bytes.AsSpan(0, 18).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(empty.AsSpan(14), 0);
        NativeBattleModuleResource resource = positive with { Bytes = empty.ToImmutableArray() };
        NativeBattleModuleSpriteDraw noQuads = NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary.Write(0x24, BitConverter.GetBytes((ushort)12)).MarkUnknown(0x1C, 4), _ => resource, Projection, 0);
        Assert.Empty(noQuads.Batch.Attempts);
        Assert.Equal(1u, U32(noQuads.Temporary180, 0xB0));
        Assert.Throws<NotSupportedException>(() => noQuads.Temporary180.Read(0x1C, 4));
    }

    private static readonly NativeBattleCameraProjection Projection = new(null, 100, 160, 108);
    private static NativeBattleModuleSpriteDraw Draw((NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary, NativeBattleModuleResource Resource) fixture, ushort flags) => NativeBattleModuleSpriteDrawing.Draw571C80(fixture.Memory, fixture.Temporary.Write(0x24, BitConverter.GetBytes(flags)), _ => fixture.Resource, Projection, 0);
    private static uint U32(NativeBattleEffectArena arena, int at) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at, 4));
    private static (NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary, NativeBattleModuleResource Resource) Fixture(int depth)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = depth }, true, true, 0x1234), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        byte[] bytes = new byte[38];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 1);
        bytes[18] = bytes[20] = 2;
        bytes[19] = 100;
        bytes[21] = 0x2C;
        bytes[22] = 10;
        bytes[23] = 20;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 0x123);
        NativeBattleModuleResource resource = new(0, 0xCE158C, 0, 0, bytes.ToImmutableArray());
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(resource.Address)).Write(4, [0, 0]).Write(0x24, [0, 0]).Write(0x1C, [64, 64, 64]);
        return (memory, temporary, resource);
    }
}
