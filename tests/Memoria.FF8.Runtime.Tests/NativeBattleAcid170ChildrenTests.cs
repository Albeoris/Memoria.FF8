using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAcid170Controller;
using static Memoria.FF8.Runtime.NativeBattleAcid170Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid170ChildrenTests
{
    private static readonly NativeBattleAcid170Resources Resources = new(Mesh(), null!, null!, null!, Endpoint(), Enumerable.Range(0, 16).Select(index => index <= 12 ? 1 : 999).ToImmutableArray(), null!, 0);

    [Fact]
    public void Curve_leading_pause_skips_all_inputs_RNG_births_and_physical_updates()
    {
        var frame = Frame(0x5DCAF0, 0);
        var actual = AdvanceCurve(frame, ChildBank, Resources, Inputs() with { GraphicsFlags = 0x201 });
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(actual.Frame));
        Assert.Equal(0, actual.Result);
    }

    [Fact]
    public void Curve_initializes_current_caster_bone_and_recipient_anchor_retaining_W_and_four_RNGs()
    {
        var frame = Frame(0x5DCAF0, 0);
        frame = frame with { State = frame.State.WriteWord(0x23B0FDE, 0xBEEF) };
        var inputs = Inputs() with
        {
            ReadBone502170 = (current, actor, selector, distance) =>
            {
                Assert.Equal(3, actor);
                Assert.Equal(41, selector);
                Assert.Equal(1536, distance);
                return new(current, Point(100, -500, 300).MarkUnknown(6, 2));
            },
            ReadAnchor571400 = (current, actor) =>
            {
                Assert.Equal(0, actor);
                return new(current, Point(900, 20, 800).Write(6, BitConverter.GetBytes((ushort)1234)));
            }
        };
        var actual = AdvanceCurve(frame, ChildBank, Resources, inputs);
        var random = frame.Random;
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Equal((ushort)0xBEEF, actual.Frame.State.ReadWord(0x23B0FDE));
        Assert.Equal((ushort)0xBEEF, actual.Frame.State.ReadWord(0x23B0FEE));
        Assert.Equal((ushort)1234, actual.Frame.State.ReadWord(0x23B1006));
        Assert.Equal((ushort)0, actual.Frame.State.ReadWord(0x23B0FE2));
        Assert.Equal((ushort)1, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(12, 5)]
    [InlineData(13, 2)]
    public void Curve_birth_ages_keep_same_callback_pool_order_and_actual_recipient_body(int age, int births)
    {
        var frame = Frame(0x5DCAF0, (ushort)age);
        var actual = AdvanceCurve(frame, ChildBank, Resources, Inputs() with
        {
            ReadAnchor571400 = (current, actor) => new(current, Point(10, 20, 30).Write(6, BitConverter.GetBytes((ushort)444))),
            ReadModel = (_, actor) =>
            {
                Assert.Equal(0, actor);
                return new(actor, 4);
            }
        });
        var active = actual.Frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0).ToArray();
        Assert.Equal(births + 1, active.Length);
        if (age == 12)
        {
            var body = Assert.Single(active, task => task.Callback == 0x5DE200);
            Assert.Equal((ushort)444, body.ReadWord(18));
            Assert.Equal((ushort)20, body.ReadWord(22));
            Assert.Equal((ushort)4, body.ReadWord(26));
        }
    }

    [Theory]
    [InlineData(0, 0x2033u, 0, false)]
    [InlineData(8, 0x20F3u, 0, false)]
    [InlineData(15, 0x20F3u, 3584, false)]
    [InlineData(16, 0x24F3u, 4096, true)]
    [InlineData(23, 0x24F3u, 512, true)]
    [InlineData(24, 0x24F3u, 0, true)]
    [InlineData(31, 0x24F3u, 3584, true)]
    public void Selected_mesh_uses_exact_92_context_morph_W_masks_and_phase_dispatch(int age, uint flags, int cue, bool custom)
    {
        var frame = Frame(0x5DD030, (ushort)age);
        frame = frame with { State = frame.State.WriteWord(0x23B10F6, 0xBEEF) };
        int calls = 0;
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            uint temporary = Assert.IsType<NativeBattleAcid170DrawMesh92>(operation).TemporaryAddress;
            var context = Context(current, temporary, 92);
            Assert.Equal(flags, Dword(context, 28));
            Assert.Equal(0x23B10F0u, Dword(context, 4));
            if (age >= 8)
            {
                Assert.Equal((uint)cue, Dword(context, 12));
            }
            if (custom)
            {
                Assert.Equal(32u, Dword(context, 16));
            }
            Assert.Equal(NativeBattleTemporaryMemory.Address + 92, Cursor(current));
            calls++;
            return current;
        }) with { GraphicsFlags = 0x201 });
        Assert.Equal(1, calls);
        Assert.Equal((ushort)0xBEEF, actual.Frame.State.ReadWord(0x23B10F6));
        int ratio = NativeBattleCameraMath.Sine(age * 1024 / 32);
        Assert.Equal(unchecked((ushort)(100 + (300 * ratio >> 12))), actual.Frame.State.ReadWord(0x23B10F0));
        Assert.Equal((ushort)age, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Fact]
    public void Positive_puddle_delay_is_pause_gated_before_any_camera_or_draw_inputs()
    {
        var frame = Frame(0x5DE0B0, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 2));
        var inputs = Inputs() with { ReadView = _ => throw new InvalidOperationException() };
        var paused = AdvanceMesh(frame, ChildBank, Resources, inputs with { GraphicsFlags = 0x201 });
        Assert.Equal((ushort)2, Read(paused.Frame, ChildBank).ReadWord(14));
        var actual = AdvanceMesh(frame, ChildBank, Resources, inputs);
        Assert.Equal((ushort)1, Read(actual.Frame, ChildBank).ReadWord(14));
        Assert.Equal((ushort)0, Read(actual.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(0, 1, 4)]
    [InlineData(28, 3, 5)]
    [InlineData(32, 0, 5)]
    public void Curve_sprite_draws_only_physical_28_samples_then_updates_speed(int index, int draws, int speed)
    {
        var frame = Frame(0x5DCF20, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(24, (ushort)index).WriteWord(26, 5));
        int calls = 0;
        var actual = AdvanceSprite(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            Assert.IsType<NativeBattleAcid170DrawSprite180>(operation);
            calls++;
            return current;
        }));
        Assert.Equal(draws, calls);
        Assert.Equal((ushort)speed, Read(actual.Frame, ChildBank).ReadWord(26));
        Assert.Equal(index + 5 >= 32 ? 2 : 0, actual.Result);
    }

    [Theory]
    [InlineData(27, 2187, 0)]
    [InlineData(28, 2187, 0)]
    [InlineData(33, 367, 0)]
    [InlineData(34, -1, 2)]
    public void Body_draws_actual_recipient_172_before_pause_and_restores_saved_bit_after_34(int age, int strength, int result)
    {
        var frame = Frame(0x5DE200, (ushort)age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(26, 4));
        List<NativeBattleAcid170Operation> calls = [];
        var actual = AdvanceBody(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            calls.Add(operation);
            if (operation is NativeBattleAcid170DrawBody172 draw)
            {
                Assert.Equal(0, draw.Actor);
                var context = Context(current, draw.TemporaryAddress, 172);
                Assert.Equal((uint)strength, Dword(context, 0x3C));
                Assert.Equal(6250000u, Dword(context, 0x40));
                Assert.Equal(NativeBattleTemporaryMemory.Address + 172, Cursor(current));
            }
            return current;
        }));
        Assert.Equal(result, actual.Result);
        Assert.Equal(age < 34 ? 2 : 1, calls.Count);
        if (age == 34)
        {
            Assert.Equal(new NativeBattleAcid170ActorFlags(0, 4, 4), calls[0]);
        }
    }

    [Theory]
    [InlineData(12, 5)]
    [InlineData(13, 0)]
    public void Drip_births_consume_only_table_indices_zero_through_12_and_five_CRT_per_birth(int age, int draws)
    {
        var frame = Frame(0x5DDE90, (ushort)age);
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Equal(age == 12 ? 1u : 0u, actual.Frame.State.ReadDword(0x23AF848));
        Assert.Equal(2, actual.Result);
    }

    [Fact]
    public void Raw_first_age_births_16_rows_with_six_CRT_each_retaining_W_suffix_and_current_register_owner()
    {
        var frame = Frame(0x5DEA10, 0);
        frame = frame with { State = frame.State.WriteWord(0x23AF856, 0xBEEF).WriteWord(0x23AF85E, 0xCAFE) };
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        var random = frame.Random;
        for (int index = 0; index < 96; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Equal(16, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(0x23AF848 + (uint)(index * 24)) != 0));
        Assert.Equal((ushort)0xBEEF, actual.Frame.State.ReadWord(0x23AF856));
        Assert.Equal((ushort)0xCAFE, actual.Frame.State.ReadWord(0x23AF85E));
        Assert.Equal(0, actual.Result);
    }

    private static NativeBattleAcid170Frame Frame(uint callback, ushort age)
    {
        var root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        var child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(callback);
        NativeBattleOriginalEffectState effect = new(170, 169, new(170, Entry, Initializer), null, RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleAcid170Frame frame = new(memory, effect, new(123), NativeBattleAcid170State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteDword(CasterGlobal, 3).WriteDword(0x23AF820, 0).WriteWord(0x23B1344, unchecked((ushort)-1000)).WriteWord(0x23B0FDA, unchecked((ushort)-1000)).WriteWord(0x23B0FE0, 400).WriteWord(0x23B0FE4, 500) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(14, callback is 0x5DDE90 or 0x5DEA10 ? (ushort)1 : (ushort)0).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 77).WriteWord(0x18, 0).WriteWord(0x1A, 7).WriteWord(0x1C, 4096));
    }

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[8 + 73 * 8];
        for (int index = 0; index < 73; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), 100);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), 200);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), 300);
        }
        return new(0, 0xD6904C, bytes.ToImmutableArray());
    }
    private static ImmutableArray<byte> Endpoint()
    {
        byte[] bytes = new byte[73 * 8];
        for (int index = 0; index < 73; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8), 400);
        }
        return bytes.ToImmutableArray();
    }
    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
    private static NativeBattleAcid170Inputs Inputs(Func<NativeBattleAcid170Frame, NativeBattleAcid170Operation, NativeBattleAcid170Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleAcid170DrawSprite180 draw ? Feedback(current, draw.TemporaryAddress, 0) : current));
    private static NativeBattleAcid170Frame Feedback(NativeBattleAcid170Frame frame, uint temporary, short value) => PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0x28, BitConverter.GetBytes(value)), 180);
    private static uint Cursor(NativeBattleAcid170Frame frame) => BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
}
