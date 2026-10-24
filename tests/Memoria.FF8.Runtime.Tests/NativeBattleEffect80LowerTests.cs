using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80LowerTests
{
    [Theory]
    [InlineData(3, 40)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    [InlineData(7, 52)]
    public void Selected_groups_keep_zero_area_depth_bias_and_context_tail(int group, int bytes)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleEffect80Mesh104Step result = NativeBattleEffect80Mesh104.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh.Address), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(bytes, result.Batch.AllocationBytes);
        NativeBattleGeometryPrimitive packet = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(group == 4 ? 205 : 206, packet.Bucket);
        Assert.Equal(0u, U(result.Context104, 0x30));
        Assert.Equal(0xDEADBEEFu, U(result.Context104, 0x40));
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context104, 0x2C));
        NativeBattleEffect80Mesh104Step restored = JsonSerializer.Deserialize<NativeBattleEffect80Mesh104Step>(JsonSerializer.Serialize(result))!;
        Assert.Equal(result.Memory.GeometryRegisters!.Sxy.ToArray(), restored.Memory.GeometryRegisters!.Sxy.ToArray());
    }

    [Theory]
    [InlineData(-86, 0)]
    [InlineData(-43, 0)]
    [InlineData(-307, 5892)]
    public void Morph_reads_signed_second_shape_alias_without_clamping_and_preserves_w(int alias, short ratio)
    {
        const uint basis = 0x1750660;
        byte[] bank = new byte[17612];
        BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(0x204), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0xA00 + 0x1CA), (short)alias);
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x808), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0xA08), 8192);
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x20E), 119);
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x1000 + alias * 2), ratio);
        NativeBattleEffect80Frame frame = new(Memory(), new(80, 79, new(80, NativeBattleEffect80Profile.Entry, NativeBattleEffect80Profile.Initializer), null, null) { Pools = [] }, new(123), NativeBattleEffect80State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattleEffect80MorphStep result = NativeBattleEffect80Morph.Execute7B2E40(frame, bank.ToImmutableArray(), basis, basis + 0x200, basis + 0x800, basis + 0xA00, basis + 0x1000);
        Assert.Equal((short)(4096 + ratio), BinaryPrimitives.ReadInt16LittleEndian(result.Bank.AsSpan().Slice(0x208)));
        Assert.Equal((short)119, BinaryPrimitives.ReadInt16LittleEndian(result.Bank.AsSpan().Slice(0x20E)));
        Assert.Equal(unchecked((uint)(int)ratio), result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Context(uint address) => NativeBattleActorWaveMesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 104), address, 0, 0, null).Write(0x10, BitConverter.GetBytes(256)).Write(0x40, BitConverter.GetBytes(0xDEADBEEFu));
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[40 + 32 + lengths[group]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        int cursor = 40;
        for (int index = 0; index < 8; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), index == group ? 1 : 0);
            cursor += 4;
            if (index == group)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (uint)(group == 3 ? 0x2C : group == 4 ? 0x30 : group == 5 ? 0x38 : 0x3C) << 24 | 0x112233);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 6), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 8), 4);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 6);
                cursor += lengths[group];
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
