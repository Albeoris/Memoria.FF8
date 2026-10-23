using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSandStorm155ControllerTests
{
    [Fact]
    public void Cold_activation_consumes_banks_and_uses_physical_first_contact()
    {
        NativeBattleSandStorm155Frame frame = NativeBattleSandStorm155Controller.Initialize(Frame(), Resources(), Inputs());
        Assert.Null(frame.State.RootBankStorage);
        Assert.Null(frame.State.ChildBankStorage);
        Assert.Equal(2, frame.Effect.Pools.Length);
        Assert.Equal(6u, frame.State.ReadDword(0x23F0800));
        Assert.Equal(0x607BB0u, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(0x606DB0u, frame.Effect.Pools[1].Slots[0].Callback);
    }

    [Fact]
    public void Closed_banks_round_trip_retained_headers_unknown_suffix_and_warm_reentry()
    {
        NativeBattleSandStorm155Frame frame = Frame();
        frame = NativeBattleSandStorm155Controller.Initialize(frame, Resources(), Inputs());
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children = children with { Slots = children.Slots.SetItem(1, children.Slots[1].MarkPayloadUnknown(30, 2) with { State = 0xCAFE, Next = 2, Callback = 0x12345678 }) };
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0] with { Flags = 0 }), Head = null, Tail = null };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        frame = NativeBattleSandStorm155Controller.Advance(frame, Resources(), Inputs());
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(frame.State.ChildBankStorage!.Read(38, 2)));
        Assert.Equal(0x23F1B40u, BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ChildBankStorage.Read(40, 4)));
        Assert.Throws<NotSupportedException>(() => frame.State.ChildBankStorage.Read(66, 2));
        frame = JsonSerializer.Deserialize<NativeBattleSandStorm155Frame>(JsonSerializer.Serialize(frame))!;
        frame = NativeBattleSandStorm155Controller.Initialize(frame, Resources(), Inputs());
        Assert.Null(frame.State.ChildBankStorage);
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[1].State);
        Assert.Null(frame.Effect.Pools[1].Slots[1].Next);
        Assert.Equal(0u, frame.Effect.Pools[1].Slots[1].Callback);
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[1].ReadWord(30));
    }

    [Fact]
    public void Full_stride_initialization_overwrites_unknown_headers_but_missing_owners_require_migration()
    {
        NativeBattleSandStorm155Frame frame = Frame() with { State = NativeBattleSandStorm155State.FromUnknown() };
        frame = NativeBattleSandStorm155Controller.Initialize(frame, Resources(), Inputs());
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[99].ReadWord(30));
        frame = Frame() with { State = NativeBattleSandStorm155State.FromColdProcess() with { ChildBankStorage = null } };
        Assert.Contains("migration", Assert.Throws<NotSupportedException>(() => NativeBattleSandStorm155Controller.Initialize(frame, Resources(), Inputs())).Message);
    }

    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 30)]
    public void Particle_births_consume_exact_CRT_draws_and_preserve_unwritten_record_bytes(bool spiral, int draws)
    {
        NativeBattleSandStorm155Frame frame = Active(spiral ? 0x607920u : 0x6076E0u);
        uint bank = spiral ? 0x23F0828u : 0x23F1188u;
        frame = frame with { State = frame.State.WriteWord(bank + 18, 0xCAFE).WriteWord(bank + 22, 0xBEEF) };
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        var actual = spiral ? NativeBattleSandStorm155Controller.AdvanceSpiral(frame, 0x23F1AF8, Resources(), Inputs()) : NativeBattleSandStorm155Controller.AdvanceSpray(frame, 0x23F1AF8, Resources(), Inputs());
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(spiral ? 5 : 3, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(bank + (uint)(index * 24)) == 1));
        if (!spiral)
        {
            Assert.Equal((ushort)0xCAFE, actual.Frame.State.ReadWord(bank + 18));
            Assert.Equal((ushort)0xBEEF, actual.Frame.State.ReadWord(bank + 22));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Paused_particles_draw_but_do_not_age_move_birth_or_consume_random(bool spiral)
    {
        NativeBattleSandStorm155Frame frame = Active(spiral ? 0x607920u : 0x6076E0u);
        uint bank = spiral ? 0x23F0828u : 0x23F1188u;
        frame = frame with { State = frame.State.WriteDword(bank, 1).WriteWord(bank + 6, 2048) };
        int calls = 0;
        NativeBattleSandStorm155Inputs inputs = Inputs((current, operation) =>
        {
            calls++;
            var draw = Assert.IsType<NativeBattleSandStorm155DrawSprite180>(operation);
            return current with { Staging = current.Staging!.Write(draw.TemporaryAddress + 0x28, BitConverter.GetBytes((short)-1)) };
        }) with { GraphicsFlags = 0x200 };
        var actual = spiral ? NativeBattleSandStorm155Controller.AdvanceSpiral(frame, 0x23F1AF8, Resources(), inputs) : NativeBattleSandStorm155Controller.AdvanceSpray(frame, 0x23F1AF8, Resources(), inputs);
        Assert.Equal(1, calls);
        Assert.Equal(0, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(JsonSerializer.Serialize(frame.State), JsonSerializer.Serialize(actual.Frame.State));
        Assert.Equal(frame.Effect, actual.Frame.Effect);
    }

    [Fact]
    public void Negative_draw_feedback_retires_before_motion_and_births_do_not_count_as_scan_survivors()
    {
        NativeBattleSandStorm155Frame frame = Active(0x6076E0);
        frame = NativeBattleSandStorm155Controller.Write(frame, 0x23F1AF8, NativeBattleSandStorm155Controller.Read(frame, 0x23F1AF8).WriteWord(12, 3));
        frame = frame with { State = frame.State.WriteDword(0x23F1188, 1).WriteWord(0x23F118E, 2048).WriteWord(0x23F1198, 100) };
        NativeBattleSandStorm155Inputs inputs = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleSandStorm155DrawSprite180>(operation);
            return current with { Staging = current.Staging!.Write(draw.TemporaryAddress + 0x28, BitConverter.GetBytes((short)-1)) };
        });
        var actual = NativeBattleSandStorm155Controller.AdvanceSpray(frame, 0x23F1AF8, Resources(), inputs);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)4, NativeBattleSandStorm155Controller.Read(actual.Frame, 0x23F1AF8).ReadWord(12));
        Assert.Equal(3, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(0x23F1188u + (uint)(index * 24)) == 1));
    }

    [Fact]
    public void Director_age_one_consumes_thirteen_CRT_draws_and_initializes_children_before_same_pass()
    {
        NativeBattleSandStorm155Frame frame = Active(0x606DB0);
        frame = NativeBattleSandStorm155Controller.Write(frame, 0x23F1AF8, NativeBattleSandStorm155Controller.Read(frame, 0x23F1AF8).WriteWord(12, 1));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 13; index++)
        {
            expected = expected.Draw(out _);
        }
        List<NativeBattleSandStorm155Operation> operations = [];
        var actual = NativeBattleSandStorm155Controller.AdvanceDirector(frame, 0x23F1AF8, Resources(), Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }));
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(0x607130u, actual.Frame.Effect.Pools[1].Slots[1].Callback);
        Assert.Equal(0x607210u, actual.Frame.Effect.Pools[1].Slots[2].Callback);
        Assert.Equal(1, actual.Frame.Effect.Pools[1].Slots[0].Next);
        Assert.Equal(2, actual.Frame.Effect.Pools[1].Slots[1].Next);
        Assert.Single(operations, operation => operation is NativeBattleSandStorm155Sound);
        Assert.Equal((ushort)2, NativeBattleSandStorm155Controller.Read(actual.Frame, 0x23F1AF8).ReadWord(12));
    }

    [Fact]
    public void Root_alternates_packet_bank_and_advances_its_clock_while_director_is_paused()
    {
        NativeBattleSandStorm155Frame frame = NativeBattleSandStorm155Controller.Initialize(Frame(), Resources(), Inputs());
        NativeBattleSandStorm155Inputs paused = Inputs() with { GraphicsFlags = 1 };
        frame = NativeBattleSandStorm155Controller.Advance(frame, Resources(), paused);
        Assert.Equal(0x23F4230u, frame.State.ReadDword(0x2404230));
        Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        frame = NativeBattleSandStorm155Controller.Advance(frame, Resources(), paused);
        Assert.Equal(0x23FC230u, frame.State.ReadDword(0x2404230));
        Assert.Equal((ushort)2, frame.Effect.Pools[0].Slots[0].ReadWord(12));
    }

    private static NativeBattleSandStorm155Frame Active(uint callback)
    {
        NativeBattleSandStorm155Frame frame = Frame();
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x23F0818, 0x23F0808, 16, 1);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23F1AE8, 0x23F1AF8, 36, 100);
        (children, _) = children.Append(callback);
        return frame with { Effect = frame.Effect with { Pools = [root, children] }, State = frame.State with { RootBankStorage = null, ChildBankStorage = null } };
    }
    private static NativeBattleSandStorm155Frame Frame()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0, tim), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(155, 154, new(155, 0x606CF0, 0x606CD0), new(3, 0, 1, 0, 0, 0), 0x23F0818) { ResourcePointers = [new(0x23F2954, 0)] };
        return new(memory, effect, new(123), NativeBattleSandStorm155State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleSandStorm155Inputs Inputs(Func<NativeBattleSandStorm155Frame, NativeBattleSandStorm155Operation, NativeBattleSandStorm155Frame>? execute = null)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        packets = packets with { Headers = packets.Headers.SetItem(0, packets.Headers[0] with { FirstContactIndex = 2 }), Contacts = packets.Contacts.SetItem(2, new(new(6, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0))) };
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (current, _) => new(current, new(1, 2, 3, 4)), (current, _) => new(current, new(5, 6, 7)), execute ?? ((current, _) => current));
    }
    private static NativeBattleSandStorm155Resources Resources()
    {
        byte[] mesh = new byte[0x27A8];
        BitConverter.GetBytes(0x988).CopyTo(mesh, 0);
        BitConverter.GetBytes(304).CopyTo(mesh, 4);
        BitConverter.GetBytes(224).CopyTo(mesh, 0x994);
        BitConverter.GetBytes(64).CopyTo(mesh, 0x1EA4);
        byte[] camera = new byte[0xD0];
        BitConverter.GetBytes((ushort)0xD0).CopyTo(camera, 6);
        return new(new(0, 0xDB8150, mesh.ToImmutableArray()), Sprite(0xDB7E94, 0x1AC, 16), Sprite(0xDB8040, 0x110, 10), new("FF8_EN:DB7DC4", camera.ToImmutableArray()), 0x61AE5);
    }
    private static NativeBattleModuleResource Sprite(uint address, int length, int count)
    {
        byte[] data = new byte[length];
        BitConverter.GetBytes((ushort)count).CopyTo(data, 8);
        for (int index = 0; index <= count; index++)
        {
            BitConverter.GetBytes((ushort)0x40).CopyTo(data, 10 + index * 2);
        }
        return new(0, address, 0, 0, data.ToImmutableArray());
    }
}
