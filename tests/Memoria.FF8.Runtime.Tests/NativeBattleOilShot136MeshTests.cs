using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilShot136MeshTests
{
    private const uint ArenaAddress = 0x20DFAB8;
    private const uint WorkingAddress = ArenaAddress + 0x400;

    [Fact]
    public void Preprocess_scans_beyond_vertex_Z_and_writes_only_108_W_words_without_reseeding()
    {
        byte[] source = Mesh();
        for (int index = 0; index < 108; index++)
        {
            Put16(source, 12 + index * 8, unchecked((short)(index < 2 ? 0 : index - 2)));
            Put16(source, 14 + index * 8, -2345);
        }
        Put32(source, 0x36C, 210);
        ImmutableArray<byte> actual = NativeBattleOilShot136Mesh.Preprocess6AD430(source.ToImmutableArray());
        Assert.Equal((short)2048, I16(actual, 14 + 107 * 8));
        Assert.Equal((short)0, I16(actual, 14));
        for (int index = 0; index < source.Length; index++)
        {
            bool w = index >= 14 && index < 8 + 108 * 8 && (index - 14) % 8 < 2;
            if (!w)
            {
                Assert.Equal(source[index], actual[index]);
            }
        }
        Assert.Equal(actual, NativeBattleOilShot136Mesh.Preprocess6AD430(actual));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 4096)]
    [InlineData(1024, 0, 0, 4096, 0)]
    [InlineData(1024, 1024, -4096, 0, 0)]
    public void Direction_has_native_signed_trig_product_order(int first, int second, short x, short y, short z)
    {
        Assert.Equal(new NativeBattleVector(x, y, z), NativeBattleOilShot136Mesh.Direction6ADFA0(first, second));
    }

    [Fact]
    public void Deformation_uses_canonical_saturated_IR_and_inclusive_table_endpoint_preserving_selected_W()
    {
        byte[] mesh = Mesh();
        for (int index = 0; index < 108; index++)
        {
            Put16(mesh, 8 + index * 8, 100);
            Put16(mesh, 10 + index * 8, 40);
            Put16(mesh, 12 + index * 8, 900);
            Put16(mesh, 14 + index * 8, 4096);
        }
        NativeBattleOilShot136Frame frame = Frame(mesh);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000 };
        frame = NativeBattleOilShot136Controller.LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(identity, 0xCAFE));
        int selected = checked((int)(WorkingAddress - ArenaAddress)) + MorphOffset;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(selected + 6, 2).Write(selected + 14, BitConverter.GetBytes((ushort)0xBEEF)) } };
        int[] table = new int[2049];
        table[2048] = 65536;
        NativeBattleOilShot136Resources resources = Resources(mesh) with { DeformationTable = table.ToImmutableArray() };
        NativeBattleOilShot136Frame actual = NativeBattleOilShot136Mesh.Deform6ADFF0(frame, 3, resources);
        Assert.Equal(short.MaxValue, ArenaWord(actual, selected));
        Assert.Equal((short)39, ArenaWord(actual, selected + 2));
        Assert.Equal((short)900, ArenaWord(actual, selected + 4));
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(selected + 6, 2));
        Assert.Equal(unchecked((short)0xBEEF), ArenaWord(actual, selected + 14));
        Assert.Equal(40, actual.Memory.ScriptedColorRegisters!.Ir[1]!.Value);
        Assert.Equal(40100, actual.Memory.ScriptedColorRegisters.Mac[0]!.Value);
        Assert.Equal(frame.State.ReadMesh(), actual.State.ReadMesh());
        Assert.Equal(frame.Random, actual.Random);
        NativeBattleOilShot136Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleOilShot136Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(actual), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Missing_current_mesh_fails_at_deformation_without_cold_hydration()
    {
        byte[] mesh = Mesh();
        NativeBattleOilShot136Frame frame = Frame(mesh);
        frame = frame with { State = frame.State with { Mesh = null, PendingColdSources = false } };
        Assert.Throws<NotSupportedException>(() => NativeBattleOilShot136Mesh.Deform6ADFF0(frame, 1, Resources(mesh)));
    }

    [Theory]
    [InlineData(65536, -3, 42)]
    [InlineData(2147483647, 3, -16343)]
    public void Deformation_uses_signed_low32_product_then_SAR17(int value, int factor, short expected)
    {
        byte[] mesh = Mesh();
        NativeBattleOilShot136Frame frame = NativeBattleOilShot136Controller.LoadMatrix(Frame(mesh), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0));
        for (int index = 0; index < 108; index++)
        {
            Put16(mesh, 10 + index * 8, 40);
        }
        frame = frame with { State = frame.State with { Mesh = mesh.ToImmutableArray() } };
        int[] table = new int[2049];
        table[0] = value;
        NativeBattleOilShot136Frame actual = NativeBattleOilShot136Mesh.Deform6ADFF0(frame, factor, Resources(mesh) with { DeformationTable = table.ToImmutableArray() });
        Assert.Equal(expected, ArenaWord(actual, checked((int)(WorkingAddress - ArenaAddress)) + MorphOffset + 2));
    }

    private static NativeBattleOilShot136Frame Frame(byte[] mesh)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleOilShot136State state = NativeBattleOilShot136State.FromColdProcess().WriteDword(WorkingGlobal, WorkingAddress) with { Mesh = mesh.ToImmutableArray(), PendingColdSources = false };
        NativeBattleOriginalEffectState effect = new(136, 135, new(136, Entry, Initializer), null, RootDescriptor);
        return new(memory, effect, new(123), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleOilShot136Resources Resources(byte[] mesh)
    {
        return new(new(MainActAddress, []), new(SecondaryActAddress, []), new(ImpactActAddress, []), new(EmitterActAddress, []), new(LargeParticleActAddress, []), new(LargeImpactActAddress, []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, MeshAddress, mesh.ToImmutableArray()), new(0, MatrixAddress, 0, 0, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0).Bytes), new("unused", []), 0, new byte[8].ToImmutableArray(), new int[2049].ToImmutableArray());
    }

    private static byte[] Mesh()
    {
        byte[] result = new byte[MeshBytes];
        Put32(result, 0, 0x368);
        Put32(result, 4, 108);
        return result;
    }

    private static short ArenaWord(NativeBattleOilShot136Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(offset, 2));
    private static short I16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static void Put16(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
