using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129BodyTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Scratch = Base + 0x3B70;
    private static readonly NativeBattleBreath129Resources Resources = new(null!, null!, null!, null!, null!, null!, null!, [], [], [], [], [], Enumerable.Range(0, 256).Select(value => (ushort)Math.Floor(Math.Sqrt(value) * 512)).ToImmutableArray(), [], 0);

    [Fact]
    public void Body_capture_uses_current_bones_cumulative_indices_and_preserves_scratch_padding()
    {
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        frame = WriteBytes(frame, Scratch, NativeBattleEffectArena.Clear().MarkUnknown(14, 2), 16);
        (frame, uint temporary) = Allocate(frame, 52);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(0, BitConverter.GetBytes(Scratch)).Write(4, BitConverter.GetBytes(0x910000u)), 52);
        NativeBattleBreath129Frame actual = NativeBattleBreath129Body.Capture6B74F0(frame, temporary, Source());
        NativeBattleEffectArena context = Context(actual, temporary, 52);
        Assert.Equal((ushort)2, Word(context, 44));
        Assert.Equal((ushort)7, Word(context, 28));
        Assert.Equal((ushort)930, Word(context, 42));
        Assert.Equal((ushort)100, Word(context, 30));
        Assert.Equal((ushort)3, Word(ReadBytes(actual, Scratch + 0x8000, 2), 0));
        Assert.Equal((ushort)9, Word(ReadBytes(actual, Scratch + 0x8002, 2), 0));
        Assert.Equal((ushort)15, Word(ReadBytes(actual, Scratch + 0x8004, 2), 0));
        NativeBattleEffectArena centroids = ReadBytes(actual, Scratch + 0x8006, 24);
        Assert.Equal((ushort)200, Word(centroids, 0));
        Assert.Equal((ushort)1015, Word(centroids, 12));
        Assert.Equal(new ushort[] { 3, 4, 5 }, Enumerable.Range(0, 3).Select(corner => Word(centroids, 18 + corner * 2)));
        Assert.Equal((ushort)1000, Word(ReadBytes(actual, Scratch + 3 * 16 + 8, 2), 0));
        Assert.Throws<NotSupportedException>(() => ReadBytes(actual, Scratch + 14, 2).Read(0, 2));
        Assert.Equal(Scratch, actual.State.ReadDword(0x252117C));
        Assert.Equal(new int?[] { 1030, 0, 0 }, actual.Memory.ScriptedColorRegisters!.Ir);
        Assert.Equal((byte)1, actual.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
    }

    [Fact]
    public void Body_wrapper_publishes_once_draws_two_randoms_per_pair_and_copies_local_triangles_only()
    {
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        int publications = 0;
        NativeBattleBreath129Inputs inputs = NativeBattleBreath129LowerTests.Inputs() with
        {
            ReadModel = (_, actor) =>
            {
                Assert.Equal(0x1D972C0u, actor);
                return new(0, 3) { ActiveStagedResourceRow100 = 0x910000 };
            },
            PublishBody508C90 = (current, body) =>
            {
                Assert.Equal(0x910000u, body);
                Assert.Equal(NativeBattleTemporaryMemory.Address + 52, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
                publications++;
                return new(current, Source());
            }
        };
        NativeBattleBreath129Frame actual = NativeBattleBreath129Body.Capture6B73F0(frame, 0x1D972C0, Base + 0x1000, 4, Resources, inputs);
        Assert.Equal(1, publications);
        NativeCrtRandomState random = frame.Random;
        for (int draw = 0; draw < 8; draw++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Random);
        Assert.Equal(Scratch, actual.State.ReadDword(0x252117C));
        Assert.Equal(Base + 0x10C0, Dword(Context(actual, NativeBattleTemporaryMemory.Address, 52), 48));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        for (int triangle = 0; triangle < 8; triangle++)
        {
            NativeBattleEffectArena output = ReadBytes(actual, Base + 0x1000 + (uint)triangle * 24, 24);
            ushort bone = Word(output, 6);
            Assert.Contains(bone, new ushort[] { 0, 1 });
            Assert.Equal(bone == 0 ? (ushort)100 : (ushort)0, Word(output, 0));
            Assert.Equal(bone == 0 ? (ushort)300 : (ushort)20, Word(output, 16));
        }
        Assert.NotNull(actual.Memory.GeometryRegisters!.Lzcs);
        Assert.NotNull(actual.Memory.GeometryRegisters.Lzcr);
    }

    [Fact]
    public void Centroid_wraps_before_division_and_quad_fourth_does_not_enter_persistent_triangle()
    {
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        (frame, uint temporary) = Allocate(frame, 52);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(0, BitConverter.GetBytes(Scratch)).Write(20, BitConverter.GetBytes(Scratch + 0x8100)), 52);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)(10 + vertex))).Write(6, BitConverter.GetBytes((short)vertex)).Write(8, BitConverter.GetBytes((short)30000));
            frame = WriteBytes(frame, Scratch + (uint)vertex * 16, row, 16);
        }
        NativeBattleBreath129Frame actual = NativeBattleBreath129Body.Centroid6B7910(frame, temporary, 0, 1, 2, 3);
        Assert.Equal(unchecked((ushort)-2768), Word(ReadBytes(actual, Scratch + 0x8100, 12), 0));
        Assert.Equal(new ushort[] { 0, 1, 2 }, Enumerable.Range(0, 3).Select(index => Word(ReadBytes(actual, Scratch + 0x8100, 12), 6 + index * 2)));
        actual = WriteBytes(actual, Scratch + 0x8000, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)128)), 2);
        actual = PublishContext(actual, temporary, Context(actual, temporary, 52).Write(48, BitConverter.GetBytes(Base + 0x1000)), 52);
        NativeBattleBreath129Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBreath129Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleBreath129Body.Copy6B7BA0(actual, temporary, 0)), NativeBattleTestSnapshots.Json(NativeBattleBreath129Body.Copy6B7BA0(restored, temporary, 0)));
        NativeBattleBreath129Frame copied = NativeBattleBreath129Body.Copy6B7BA0(actual, temporary, 0);
        Assert.Equal(new ushort[] { 10, 11, 12 }, Enumerable.Range(0, 3).Select(index => Word(ReadBytes(copied, Base + 0x1000, 24), index * 8)));
    }

    [Fact]
    public void Capture_rejects_exhausted_current_scratch_bank_instead_of_creating_a_new_bank()
    {
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        (frame, uint temporary) = Allocate(frame, 52);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(0, BitConverter.GetBytes(Base + 0x1BB70 - 8)).Write(4, BitConverter.GetBytes(0x910000u)), 52);
        Assert.Throws<InvalidDataException>(() => NativeBattleBreath129Body.Capture6B74F0(frame, temporary, Source()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Legacy_absolute_reader_and_relative_reader_preserve_origin_alignment_and_the_same_capture(int alignment)
    {
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 3);
        for (int vertex = 0; vertex < 3; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + vertex * 6), (short)(100 + vertex * 100));
        }
        int header = ((32 + alignment + 3) & ~3) - alignment;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(header), 1);
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(header + 12 + corner * 2), (short)corner);
        }
        uint origin = 0x900000u + (uint)alignment;
        ImmutableArray<NativeBattlePackedMatrix32> bones = [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)];
        NativeBattleBreath129BodySource legacy = new(0x910000, origin, (address, length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan(checked((int)(address - origin)), length)), bones);
        NativeBattleBreath129BodySource relative = new(0x910000, (offset, length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan(offset, length)), bones) { GeometryAlignment = (byte)alignment };
        NativeBattleBreath129Frame a = Capture(legacy);
        NativeBattleBreath129Frame b = Capture(relative);
        Assert.Equal(NativeBattleTestSnapshots.Json(a), NativeBattleTestSnapshots.Json(b));
        Assert.Equal((ushort)1, Word(Context(b, NativeBattleTemporaryMemory.Address, 52), 44));
        Assert.Throws<InvalidDataException>(() => (relative with { GeometryAlignment = 4 }).AlignGeometryCursor(32));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Logical_body_geometry_beyond_guest_capacity_rebinds_after_snapshot_and_captures_current_source_without_numeric_projection(bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleBreath129BodySource original = Source();
        byte[] compact = original.ReadGeometry(0, 128).Read(0, 128);
        int padding = layout.BankLength + 4;
        byte[] bytes = new byte[padding + compact.Length];
        compact.AsSpan(0, 12).CopyTo(bytes);
        compact.AsSpan(12).CopyTo(bytes.AsSpan(padding + 12));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), padding + 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), padding + 64);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(0, bytes.ToImmutableArray(), 0, bytes.Length);
        NativeBattleResourcePointer pointer = new(new(0, 0) { Layout = layout, ResourceOnly = true }, null, bytes.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) }, Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(16, pointer.Cpu) with { Lengths = cpu.Rows[0].Lengths.SetItem(16, bytes.Length) }) };
        BoundBody restored = NativeBattleTestSnapshots.RestoreJson<BoundBody>(NativeBattleTestSnapshots.Json(new BoundBody(NativeBattleBreath129LowerTests.Frame(), cpu, pointer, original.BoneMatrices)));
        Assert.Throws<NotSupportedException>(() => restored.Geometry.Cpu!.PhysicalAddress);
        int publications = 0;
        NativeBattleBreath129Inputs inputs = NativeBattleBreath129LowerTests.Inputs() with
        {
            ReadModel = (_, _) => new(0, 3) { ActiveResourceRow100 = 0 },
            PublishBody508C90 = (current, row) =>
            {
                Assert.Equal(0x1D99774u, row);
                publications++;
                return new(current, RelativeSource(restored, row));
            }
        };
        NativeBattleBreath129Frame legacyStart = NativeBattleTestSnapshots.Independent(restored.Frame);
        NativeBattleBreath129Frame changedStart = NativeBattleTestSnapshots.Independent(restored.Frame);
        NativeBattleBreath129Frame actual = NativeBattleBreath129Body.Capture6B73F0(restored.Frame, 0x1D972C0, Base + 0x1000, 4, Resources, inputs);
        NativeBattleBreath129Inputs legacyInputs = inputs with { PublishBody508C90 = (current, row) => new(current, original with { BodyRowAddress = row }) };
        NativeBattleBreath129Frame legacy = NativeBattleBreath129Body.Capture6B73F0(legacyStart, 0x1D972C0, Base + 0x1000, 4, Resources, legacyInputs);
        Assert.Equal(1, publications);
        Assert.Equal(NativeBattleTestSnapshots.Json(legacy), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(restored.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        int vertexOffset = padding + 18;
        NativeBattleModelCpuBank changed = restored.Cpu.Cpu.Banks[0].Write(vertexOffset, BitConverter.GetBytes((short)777));
        BoundBody replacement = restored with { Cpu = restored.Cpu with { Cpu = restored.Cpu.Cpu with { Banks = restored.Cpu.Cpu.Banks.SetItem(0, changed) } } };
        NativeBattleBreath129Inputs changedInputs = inputs with { PublishBody508C90 = (current, row) => new(current, RelativeSource(replacement, row)) };
        NativeBattleBreath129Frame changedCapture = NativeBattleBreath129Body.Capture6B73F0(changedStart, 0x1D972C0, Base + 0x1000, 4, Resources, changedInputs);
        Assert.Equal((ushort)777, Word(ReadBytes(changedCapture, Scratch, 2), 0));
        Assert.Equal((ushort)100, Word(ReadBytes(actual, Scratch, 2), 0));
        Assert.Equal((ushort)100, Word(RelativeSource(restored, 0x1D99774).ReadGeometry(vertexOffset, 2), 0));
        Assert.Throws<InvalidDataException>(() => RelativeSource(restored, 0x1D99774).ReadGeometry(bytes.Length, 1));
    }

    private static NativeBattleBreath129Frame Capture(NativeBattleBreath129BodySource source)
    {
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        (frame, uint temporary) = Allocate(frame, 52);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(0, BitConverter.GetBytes(Scratch)).Write(4, BitConverter.GetBytes(source.BodyRowAddress)), 52);
        return NativeBattleBreath129Body.Capture6B74F0(frame, temporary, source);
    }

    private static NativeBattleBreath129BodySource RelativeSource(BoundBody bound, uint row)
    {
        NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(bound.Cpu, bound.Frame.Staging!, bound.Geometry, bound.Geometry.Length!.Value);
        return new(row, (offset, length) => NativeBattleEffectArena.Clear().Write(0, view.Read(offset, length)), bound.Bones) { GeometryAlignment = (byte)(bound.Geometry.Cpu!.Offset & 3) };
    }

    private sealed record BoundBody(NativeBattleBreath129Frame Frame, NativeBattleModelCpuResources Cpu, NativeBattleResourcePointer Geometry, ImmutableArray<NativeBattlePackedMatrix32> Bones);

    private static NativeBattleBreath129BodySource Source()
    {
        byte[] bytes = new byte[128];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 64);
        for (int item = 0; item < 2; item++)
        {
            int begin = item == 0 ? 12 : 64;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(begin), 1);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(begin + 2), (short)item);
            int count = item == 0 ? 3 : 4;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(begin + 4), (short)count);
            for (int vertex = 0; vertex < count; vertex++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(begin + 6 + vertex * 6), (short)(item == 0 ? 100 + vertex * 100 : vertex * 10));
            }
            int header = item == 0 ? 36 : 96;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(header + item * 2), 1);
            for (int corner = 0; corner < count; corner++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(header + 12 + corner * 2), (short)corner);
            }
        }
        return new(0x910000, 0x900000, (address, length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan(checked((int)(address - 0x900000)), length)), [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1000 }, null)]);
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
}
