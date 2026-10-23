using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect83LowerTests
{
    [Theory]
    [InlineData(1, 24)]
    [InlineData(3, 40)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    public void Selected_groups_preserve_zero_area_and_depth_bias_before_shift(int group, int length)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleEffect83Mesh104Step result = NativeBattleEffect83Mesh104.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh.Address), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(length, result.Batch.AllocationBytes);
        Assert.Equal(group == 4 ? 205 : 206, primitive.Bucket);
        Assert.Equal(group == 4 ? 755u : 756u, U(result.Context104, 0x38));
        Assert.Equal(0u, U(result.Context104, 0x30));
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context104, 0x2C));
        Assert.Equal(mesh.Address + 8, U(result.Context104, 4));
        Assert.Equal(0xDEADBEEFu, U(result.Context104, 0x40));
        string json = JsonSerializer.Serialize(result);
        NativeBattleEffect83Mesh104Step restored = JsonSerializer.Deserialize<NativeBattleEffect83Mesh104Step>(json)!;
        Assert.Equal(result.Batch.AllocationBytes, restored.Batch.AllocationBytes);
        Assert.Equal(result.Memory.GeometryRegisters!.Sxy.ToArray(), restored.Memory.GeometryRegisters!.Sxy.ToArray());
    }

    [Fact]
    public void Morph_uses_current_signed_shape_alias_and_executes_zero_factor_arithmetic()
    {
        const uint basis = NativeBattleEffect83Profile.NodeSourceAddress;
        byte[] bank = new byte[NativeBattleEffect83Profile.NodeSourceLength];
        uint mesh = basis + 0x200;
        uint a = basis + 0x400;
        uint b = basis + 0x800;
        uint channel = basis + 0x1000;
        BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(0x204), 2);
        foreach (int offset in new[] { 0x400, 0x800 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(offset + 0x1CA), -451);
        }
        for (int vertex = 0; vertex < 2; vertex++)
        {
            int offset = vertex * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x408 + offset), (short)(100 + vertex));
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x40A + offset), -20);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x40C + offset), 30);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x808 + offset), 900);
            BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x208 + offset + 6), 119);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(0x1000), 4096);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleEffect83Frame frame = new(Memory(), null!, new(1), NativeBattleEffect83State.FromColdProcess()) { Staging = staging };
        NativeBattleEffect83MorphStep result = NativeBattleEffect83Morph.Execute79BEC0(frame, bank.ToImmutableArray(), mesh, a, b, channel);
        Assert.Equal(new short[] { 100, -20, 30, 119, 101, -20, 30, 119 }, Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Bank.AsSpan().Slice(0x208 + index * 2))));
        Assert.Equal(0u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 101, -20, 30 }, result.Frame.Memory.ScriptedColorRegisters.Ir.ToArray());
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(4096, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 4, 4)));
        Assert.Null(frame.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void Null_shape_skips_the_native_temporary_and_register_consumers()
    {
        NativeBattleEffect83Frame frame = new(Memory(), null!, new(1), NativeBattleEffect83State.FromColdProcess());
        ImmutableArray<byte> bank = new byte[NativeBattleEffect83Profile.NodeSourceLength].ToImmutableArray();
        NativeBattleEffect83MorphStep result = NativeBattleEffect83Morph.Execute79BEC0(frame, bank, 0, 0, 1, uint.MaxValue);
        Assert.Same(frame, result.Frame);
        Assert.Equal(bank, result.Bank);
    }

    [Fact]
    public void Selected_morph_hook_bypasses_the_generic_geometry_clock_ratio_read()
    {
        const uint basis = 0x10000000;
        byte[] bytes = new byte[2048];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, basis + 256);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), basis + 512);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), basis + 1024);
        bytes[32] = 1;
        byte[] source = new byte[344];
        source[0x16] = 5;
        source[0x1B] = 255;
        for (int field = 0xC8; field <= 0x144; field += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(field), basis + 128);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x130), basis + 33);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x134), basis + 32);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x138), uint.MaxValue);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 676, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteDword(0x34 + 0x22C, basis).WriteDword(0x34 + 0x230, basis + 16);
        pool = pool with { Slots = [task] };
        NativeBattleOriginalEffectState effect = new(83, 82, new(83, NativeBattleEffect83Profile.Entry, NativeBattleEffect83Profile.Initializer), null, null) { Pools = [pool] };
        NativeBattleEffectNodeMemory memory = new(Memory(), effect, 0x200, 0, 0x10E0, 0x10E0, 0x10680);
        NativeBattleEffectNodeBank bank = new(basis, bytes.ToImmutableArray());
        int calls = 0;
        NativeBattleEffectNodeInputs inputs = new(NativeBattleCuragaMatrices.Identity8DD770(), _ => throw new InvalidOperationException(), (_, _, _) => null, (_, _) => null)
        {
            SelectedMorph = (mesh, first, second, channel) =>
            {
                calls++;
                Assert.Equal(basis + 256, mesh);
                Assert.Equal(basis + 512, first);
                Assert.Equal(basis + 1024, second);
                Assert.Equal(uint.MaxValue, channel);
            }
        };
        new NativeBattleEffectNodeLifecycle(memory, bank, inputs).AdvanceDrawable(0x10E0, source);
        Assert.Equal(1, calls);
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectNodeLifecycle(memory, bank, inputs with { SelectedMorph = null }).AdvanceDrawable(0x10E0, source));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Context(uint address) => NativeBattleEffectArena.Clear().MarkUnknown(0, 104).Write(0, BitConverter.GetBytes(address)).Write(0x10, BitConverter.GetBytes(256)).Write(0x14, BitConverter.GetBytes(0x30u)).Write(0x18, new byte[4]).Write(0x40, BitConverter.GetBytes(0xDEADBEEFu));
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
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (uint)(group == 1 ? 0x28 : group == 3 ? 0x2C : group == 4 ? 0x30 : 0x38) << 24 | 0x112233);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 6), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 8), 4);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 6);
                cursor += lengths[group];
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
