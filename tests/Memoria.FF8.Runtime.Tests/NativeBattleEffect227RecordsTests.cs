using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect227Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect227RecordsTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint First = Arena + 0x61C;

    [Fact]
    public void Allocator_wraps_the_last_free_record_and_scans_from_the_first_when_cached_is_busy()
    {
        NativeBattleEffect227Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(0x2508704, First + 127 * 16) };
        (frame, uint last) = NativeBattleEffect227Records.Allocate(frame);
        Assert.Equal(First + 127 * 16, last);
        Assert.Equal(First, frame.State.ReadDword(0x2508704));
        frame = WriteBytes(frame, First + 15, NativeBattleEffectArena.Clear().Write(0, [1]), 1);
        (frame, uint next) = NativeBattleEffect227Records.Allocate(frame);
        Assert.Equal(First + 16, next);
        Assert.Equal(First + 32, frame.State.ReadDword(0x2508704));
    }

    [Fact]
    public void Full_records_do_not_consume_random_or_query_a_bone()
    {
        NativeBattleEffect227Frame frame = Frame();
        for (int index = 0; index < 128; index++)
        {
            frame = WriteBytes(frame, First + (uint)(index * 16 + 15), NativeBattleEffectArena.Clear().Write(0, [1]), 1);
        }
        Harness harness = new();
        NativeBattleEffect227Frame result = NativeBattleEffect227Records.Birth(frame, 0x1131660, 1, Resources(), harness.Inputs());
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.State, result.State);
        Assert.Equal(0, harness.Queries);
    }

    [Fact]
    public void Birth_preserves_four_random_draws_and_overwrites_only_native_record_fields()
    {
        NativeBattleEffect227Frame frame = Frame();
        NativeCrtRandomState expected = frame.Random.Draw(out _).Draw(out ushort horizontal).Draw(out ushort depth).Draw(out ushort vertical);
        Harness harness = new();
        NativeBattleEffect227Frame result = NativeBattleEffect227Records.Birth(frame, 0x1131660, 1, Resources(), harness.Inputs());
        NativeBattleEffectArena row = ReadBytes(result, First, 16);
        Assert.Equal(expected, result.Random);
        Assert.Equal(1, harness.Queries);
        Assert.Equal((short)123, I16(row, 4));
        Assert.Equal((short)512, I16(row, 10));
        Assert.Equal(unchecked((byte)((horizontal & 3) - 2)), row.Read(12, 1)[0]);
        Assert.Equal(unchecked((byte)((depth & 3) - 2)), row.Read(14, 1)[0]);
        Assert.Equal((byte)((vertical & 3) + 3), row.Read(13, 1)[0]);
        Assert.Equal(18, row.Read(15, 1)[0]);
        Assert.Equal(0x112D2E0u, BinaryPrimitives.ReadUInt32LittleEndian(row.Read(0, 4)));
    }

    [Fact]
    public void Last_life_tick_still_moves_and_retains_unknown_unwritten_bytes_through_json()
    {
        NativeBattleEffect227Frame frame = Frame();
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(4, BitConverter.GetBytes((short)32767)).Write(12, [1, 255, 2, 1]);
        frame = WriteBytes(frame, First, row, 16);
        frame = JsonSerializer.Deserialize<NativeBattleEffect227Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleEffectArena result = ReadBytes(NativeBattleEffect227Records.Advance(frame), First, 16);
        Assert.Equal(unchecked((short)(32767 + 4)), I16(result, 4));
        Assert.Equal(-4, I16(result, 6));
        Assert.Equal(8, I16(result, 8));
        Assert.Equal(0, result.Read(15, 1)[0]);
        Assert.DoesNotContain(true, result.ReadSnapshot(0, 4).Known);
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(1, 48)]
    public void Drawing_consumes_retained_mode_bit_and_releases_one_actual180_byte_temporary(byte mode, int allocation)
    {
        NativeBattleEffect227Frame frame = LiveFrame();
        frame = frame with { Staging = frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x25, [mode]) };
        Harness harness = new();
        NativeBattleEffect227Frame result = NativeBattleEffect227Records.Draw(frame, Resources(), harness.Inputs());
        NativeBattleEffect227DrawBatch draw = Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(harness.Operations));
        Assert.Equal(allocation, draw.Batch.AllocationBytes);
        Assert.All(draw.Batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(19, attempt.Primitive!.Bucket));
        Assert.Equal(mode, result.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x25, 1)[0]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Unknown_retained_mode_bit_fails_instead_of_being_seeded()
    {
        NativeBattleEffect227Frame frame = LiveFrame();
        frame = frame with { Staging = frame.Staging!.MarkUnknown(NativeBattleTemporaryMemory.Address + 0x25, 1) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect227Records.Draw(frame, Resources(), new Harness().Inputs()));
    }

    internal static NativeBattleEffect227Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(227, 226, new(227, 0x6937A0, 0x693780), new(3, 0, 1, 0, 0, 0), 0x2508738) { Pools = [] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffect227State state = NativeBattleEffect227State.FromColdProcess().WriteDword(0x2508768, Arena).WriteDword(0x2508704, First);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
    }

    private static NativeBattleEffect227Frame LiveFrame()
    {
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x112D2E0u)).Write(8, BitConverter.GetBytes((short)2000)).Write(10, BitConverter.GetBytes((short)512)).Write(15, [1]);
        return WriteBytes(Frame(), First, row, 16);
    }

    internal static NativeBattleEffect227Resources Resources()
    {
        byte[] sprite = new byte[40];
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(10), 16);
        BinaryPrimitives.WriteInt32LittleEndian(sprite.AsSpan(16), 1);
        sprite[20] = 8;
        sprite[21] = 128;
        sprite[22] = 8;
        sprite[23] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(36), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(38), 4096);
        return new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(0x112D2E0, new(0, 0x112D2E0, 40, 0, sprite.ToImmutableArray())), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x1131660, new byte[8].ToImmutableArray()));
    }

    private static short I16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));

    internal sealed class Harness
    {
        public int Queries { get; private set; }
        public List<NativeBattleEffect227Operation> Operations { get; } = [];
        public NativeBattleEffect227Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, _, retained) =>
        {
            Queries++;
            return new(frame, retained.Write(0, BitConverter.GetBytes((short)123)));
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
