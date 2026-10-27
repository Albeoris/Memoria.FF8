using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleResonance49Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResonance49ProgrammeTests
{
    [Fact]
    public void Initial_delay_reads_no_source_bone_model_or_temporary()
    {
        var frame = Frame(3, 19);
        var actual = InitializeProgramme(frame, 0x2646BF0, null!, null!);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(actual));
    }

    [Theory]
    [InlineData(false, 3, 724)]
    [InlineData(true, 4, 712)]
    public void Initializer_uses_current_actor_F0_and_exact_clear_before_first_draw(bool second, int actor, int clear)
    {
        var frame = Frame(3, 20);
        var task = Read(frame, 0x2646BF0).WriteDword(0x74, second ? 0x17B656Cu : 0x17B329Cu).WriteDword(0x78, (uint)clear).WriteByte(0x2C, (byte)actor).WriteWord(0x22, 0xBEEF).MarkPayloadUnknown(0x94, 724);
        frame = Write(frame, 0x2646BF0, task);
        int draws = 0;
        var actual = InitializeProgramme(frame, 0x2646BF0, Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleResonance49DrawMesh104>(operation);
            Assert.Equal(0x2030u, U(Context(current, draw.TemporaryAddress, 104), 20));
            draws++;
            return current;
        }) with
        {
            ReadBone502170 = (current, selected, selector, distance) =>
            {
                Assert.Equal(actor, selected);
                Assert.Equal(0xF0, selector);
                Assert.Equal(4096, distance);
                return new(current, PointView(new(10, 20, 30)));
            },
            ReadModel = (_, selected) => new(selected, 0) { Rotation14 = 0 }
        });
        Assert.Equal(1, draws);
        task = Read(actual, 0x2646BF0);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)0xBEEF, task.ReadWord(0x22));
        Assert.Equal(1u, task.ReadDword(0x98));
        Assert.Equal((ushort)0xFF80, task.ReadWord(0x88));
        if (second)
        {
            Assert.Throws<NotSupportedException>(() => task.ReadWord(0x94 + 712));
        }
    }

    [Fact]
    public void Programme_uses_actual_remaining_count_and_preserves_known_suffix_across_JSON()
    {
        var frame = Frame(3, 20);
        frame = Write(frame, 0x2646BF0, Read(frame, 0x2646BF0).WriteDword(0x74, 0x17B656C).WriteDword(0x78, 712).WriteWord(0x94 + 712, 0xCAFE));
        var resources = Resources();
        frame = InitializeProgramme(frame, 0x2646BF0, resources, Inputs());
        Assert.Equal((ushort)0xCAFE, Read(frame, 0x2646BF0).ReadWord(0x94 + 712));
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleResonance49Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var next = DrawProgramme(frame, 0x2646BF0, resources, Inputs());
        Assert.Equal(0, next.Remaining);
        Assert.Equal(NativeBattleTestSnapshots.Json(next.Frame), NativeBattleTestSnapshots.Json(DrawProgramme(restored, 0x2646BF0, resources, Inputs()).Frame));
    }

    [Fact]
    public void Node_shape_interpolation_preserves_scratch_W_and_retained_temporary_color_when_fade_zero()
    {
        var frame = Frame(3, 20);
        frame = frame with { State = frame.State.WriteWord(0x2647D26, 0xBEEF) };
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, NativeBattleTemporaryMemory.Address + 8, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0xCAFEBABEu)), 4) };
        var resources = Resources();
        var node = Node(ratio: 1024);
        var actual = DrawProgrammeNode49(frame, resources.ProgrammeA, node, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), -128, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleResonance49DrawMesh104>(operation);
            Assert.Equal(0x2647D20u, draw.VertexAddress);
            Assert.Equal((ushort)150, current.State.ReadWord(0x2647D20));
            Assert.Equal((ushort)0xBEEF, current.State.ReadWord(0x2647D26));
            Assert.Equal(0xCAFEBABEu, U(Context(current, draw.TemporaryAddress, 104), 8));
            return current;
        }));
        Assert.Equal(1024u, actual.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    public void Endpoint_shape_selection_uses_original_rows_without_touching_scratch(int ratio)
    {
        var frame = Frame(3, 20);
        frame = frame with { State = frame.State.WriteDword(0x2647D20, 0x12345678) };
        var resources = Resources();
        var actual = DrawProgrammeNode49(frame, resources.ProgrammeA, Node(ratio), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), 0, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleResonance49DrawMesh104>(operation);
            Assert.Equal(0x17B329Cu + 32u + 12u + (ratio == 0 ? 0u : 32u), draw.VertexAddress);
            return current;
        }));
        Assert.Equal(0x12345678u, actual.State.ReadDword(0x2647D20));
    }

    [Fact]
    public void Node_translation_uses_full_MAC_and_header8000_retains_local_rotation()
    {
        var frame = Frame(3, 20);
        var resources = Resources();
        var node = Node(0, 0x8000, new(32767, 0, 0));
        var basis = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [32767, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 100 };
        var actual = DrawProgrammeNode49(frame, resources.ProgrammeA, node, NativeBattlePackedMatrix32.FromMatrix(basis, null), 0, resources, Inputs((current, _) =>
        {
            Assert.Equal(4096, BitConverter.ToInt16(current.Memory.GeometryRegisters!.Rotation20.Take(2).Select(value => value!.Value).ToArray()));
            int expected = NativePrecision53.RoundInteger(32767 * (32767 / 4096d));
            Assert.Equal(expected + 100, current.Memory.GeometryRegisters.Translation[0]);
            Assert.Equal(short.MaxValue, current.Memory.ScriptedColorRegisters!.Ir[0]);
            return current;
        }));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Invisible_node_exits_before_any_resource_or_temporary_read()
    {
        byte[] data = new byte[44];
        Assert.Null(DrawProgrammeNode49(null!, null!, new(0, 0, data.ToImmutableArray()), null!, 0, null!, null!));
    }

    internal static NativeBattleResonance49Frame Frame(int poolIndex, ushort age = 0)
    {
        var pools = NativeBattleResonance49Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        var pool = pools[poolIndex];
        (pool, _) = pool.Append(poolIndex == 3 ? 0x821C50u : 0x824260u);
        pools = pools.SetItem(poolIndex, pool);
        var effect = new NativeBattleOriginalEffectState(49, 48, new(49, 0x821540, 0x821520), null, 0x26466C0) { Pools = pools };
        var frame = new NativeBattleResonance49Frame(NativeBattleResonance49MeshContext104Tests.Memory(2000), effect, new(123), NativeBattleResonance49State.FromColdProcess() with { InactiveBanks = Enumerable.Repeat<NativeBattleEffectArena?>(null, 6).ToImmutableArray() }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        var task = Read(frame, pool.BankAddress).WriteWord(0x24, age);
        if (poolIndex == 3)
        {
            task = task.WriteDword(0x74, 0x17B329C).WriteDword(0x78, 724).WriteByte(0x2C, 3).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
        }
        return Write(frame, pool.BankAddress, task);
    }

    internal static NativeBattleResonance49Inputs Inputs(Func<NativeBattleResonance49Frame, NativeBattleResonance49Operation, NativeBattleResonance49Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, actor) => new(actor, 0) { Rotation14 = 0 }, (current, _, _, _) => new(current, PointView(new(0, 0, 0))), (_, _) => NativeBattleEffectArena.Clear(), _ => 1, execute ?? ((current, _) => current));
    internal static NativeBattleResonance49Resources Resources() => new(Source(0x17B329C), Source(0x17B656C), null!, 0);
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleGfAnimationNode Node(int ratio, uint header = 0, NativeBattleVector point = default)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), point.X);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), point.Z);
        for (int at = 24; at <= 28; at += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(at), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        return new(0, 0, data.ToImmutableArray());
    }
    private static NativeBattleGfAnimationSource Source(uint address)
    {
        byte[] data = new byte[288];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 256);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8 + index * 4), 32);
        }
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(32), 76);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(36), 4);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40), 2);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int at = 44 + (shape * 4 + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(at), (short)(100 + shape * 200));
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(120), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(124), 0x2E112233);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(128 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(256), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(260), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(264), 16);
        return new(address, data.ToImmutableArray());
    }
}
