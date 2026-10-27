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
