using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDegenerator306SlotsTests
{
    [Fact]
    public void Births_use_five_CRT_draws_each_and_preserve_position_W_and_unwritten_tail()
    {
        Fixture fixture = new(0);
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < 100; index++)
        {
            expected = expected.Draw(out _);
        }
        (NativeBattleDegenerator306Frame frame, int result) = NativeBattleDegenerator306Slots.Advance(fixture.Frame, fixture.Address, Resources, fixture.Inputs());
        Assert.Equal(0, result);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(0, fixture.Draws);
        Assert.Equal((ushort)1, NativeBattleDegenerator306Controller.Read(frame, fixture.Address).ReadWord(12));
        for (int index = 0; index < 20; index++)
        {
            uint address = SlotBank + (uint)(index * SlotStride);
            Assert.Equal(2u, frame.State.ReadDword(address));
            Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(address + 14));
            Assert.InRange(frame.State.ReadWord(address + 6), (ushort)256, (ushort)1023);
            Assert.Throws<NotSupportedException>(() => frame.State.Read(address + 22, 2));
        }
        Assert.Equal(0u, frame.State.ReadDword(SlotBank + 20 * SlotStride));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Paused_live_slots_draw_using_actual_W_and_shared_temporary_without_age_damping_or_random()
    {
        Fixture fixture = new(4);
        NativeBattleDegenerator306Frame before = fixture.Live(-1, -1);
        (NativeBattleDegenerator306Frame frame, int result) = NativeBattleDegenerator306Slots.Advance(before, fixture.Address, Resources, fixture.Inputs(0x201));
        Assert.Equal(0, result);
        Assert.Equal(1, fixture.Draws);
        Assert.Equal(NativeBattleTestSnapshots.Json(before.State), NativeBattleTestSnapshots.Json(frame.State));
        Assert.Equal(before.Random, frame.Random);
        Assert.Equal((ushort)4, NativeBattleDegenerator306Controller.Read(frame, fixture.Address).ReadWord(12));
        Assert.Equal((ushort)0xCAFE, fixture.ObservedW);
    }

    [Fact]
    public void Signed_SAR_damping_wraps_positions_and_terminal_frame_clears_only_head_after_draw()
    {
        Fixture fixture = new(4);
        NativeBattleDegenerator306Frame before = fixture.Live(-1, -1);
        (NativeBattleDegenerator306Frame frame, int result) = NativeBattleDegenerator306Slots.Advance(before, fixture.Address, Resources, fixture.Inputs());
        Assert.Equal(0, result);
        Assert.Equal((ushort)0, frame.State.ReadWord(SlotBank + 6));
        Assert.Equal(unchecked((ushort)-1), frame.State.ReadWord(SlotBank + 8));
        Assert.Equal((ushort)0, frame.State.ReadWord(SlotBank + 16));
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(SlotBank + 14));
        Fixture terminal = new(4) { NextOffset = -1 };
        NativeBattleDegenerator306Frame live = terminal.Live(256, -7);
        (NativeBattleDegenerator306Frame ended, int endResult) = NativeBattleDegenerator306Slots.Advance(live, terminal.Address, Resources, terminal.Inputs());
        Assert.Equal(2, endResult);
        Assert.Equal(0u, ended.State.ReadDword(SlotBank));
        Assert.Equal((ushort)1, ended.State.ReadWord(SlotBank + 4));
        Assert.Equal(live.State.Read(SlotBank + 6, 16), ended.State.Read(SlotBank + 6, 16));
        Assert.Throws<NotSupportedException>(() => ended.State.Read(SlotBank + 22, 2));
    }

    [Fact]
    public void Full_bank_consumes_no_random_and_JSON_next_keeps_retained_partial_slots()
    {
        Fixture fixture = new(0);
        NativeBattleDegenerator306Frame frame = fixture.Frame;
        for (int index = 0; index < SlotCount; index++)
        {
            frame = frame with { State = frame.State.WriteDword(SlotBank + (uint)(index * SlotStride), 1) };
        }
        NativeBattleDegenerator306Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDegenerator306Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var first = NativeBattleDegenerator306Slots.Advance(frame, fixture.Address, Resources, fixture.Inputs());
        var second = NativeBattleDegenerator306Slots.Advance(restored, fixture.Address, Resources, fixture.Inputs());
        Assert.Equal(frame.Random, first.Frame.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Frame), NativeBattleTestSnapshots.Json(second.Frame));
        Assert.Equal(first.Result, second.Result);
    }

    private static NativeBattleDegenerator306Resources Resources => new(null!, null!, null!, null!, null!, null!, null!, null!, new(0, 0xCE62A8, 0, 0, []), [], 0);

    [Fact]
    public void Group21_native_slot_rebirth_changes_generation_without_changing_W_rng_or_native_writes()
    {
        (NativeBattleDegenerator306Frame Frame, List<NativeBattlePilotVisualIdentity> First, List<NativeBattlePilotVisualIdentity> Second) Run(bool capture)
        {
            Fixture fixture = new(0) { NextOffset = -1 };
            List<NativeBattlePilotVisualIdentity> identities = [];
            var inputs = fixture.Inputs() with { CapturePresentation = capture };
            var execute = inputs.Execute;
            inputs = inputs with { Execute = (current, operation) =>
            {
                if (operation.PilotIdentity is { } identity)
                {
                    identities.Add(identity);
                }
                return execute(current, operation);
            } };
            var frame = NativeBattleDegenerator306Slots.Advance(fixture.Frame, fixture.Address, Resources, inputs).Frame;
            Assert.Empty(identities);
            frame = NativeBattleDegenerator306Slots.Advance(frame, fixture.Address, Resources, inputs).Frame;
            var first = identities.ToList();
            identities.Clear();
            frame = NativeBattleDegenerator306Slots.Advance(frame, fixture.Address, Resources, inputs).Frame;
            Assert.Equal((ushort)0xCAFE, fixture.ObservedW);
            return (frame, first, identities);
        }
        var native = Run(false);
        string before = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(true);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(20, captured.First.Count);
        Assert.Equal(20, captured.Second.Count);
        for (int index = 0; index < 20; index++)
        {
            Assert.Equal(captured.First[index].Bank, captured.Second[index].Bank);
            Assert.Equal(captured.First[index].Slot, captured.Second[index].Slot);
            Assert.NotEqual(captured.First[index].Generation, captured.Second[index].Generation);
            Assert.Equal(captured.First[index].ResourceGeneration, captured.Second[index].ResourceGeneration);
        }
    }

    private sealed class Fixture
    {
        public NativeBattleDegenerator306Frame Frame { get; }
        public uint Address { get; }
        public int Draws { get; private set; }
        public ushort ObservedW { get; private set; }
        public short NextOffset { get; init; } = 1;

        public Fixture(ushort age)
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            NativeBattleOriginalEffectState effect = new(306, 306, new(306, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [root, children] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            NativeBattleDegenerator306State state = new NativeBattleDegenerator306State(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleDegenerator306State.Length)).ClearSlotHeads();
            NativeBattleDegenerator306Frame frame = new(memory, effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            (frame, uint address) = NativeBattleDegenerator306Controller.Append(frame, 0x5A4D80);
            Address = address;
            NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, age).WriteDword(0x10, 0).WriteDword(0x14, 0xCAFE03E8);
            Frame = NativeBattleDegenerator306Controller.Write(frame, address, task);
        }

        public NativeBattleDegenerator306Frame Live(short scale, short speed)
        {
            NativeBattleDegenerator306State state = Frame.State.WriteDword(SlotBank, 2).WriteWord(SlotBank + 4, 0).WriteWord(SlotBank + 6, unchecked((ushort)scale)).Write(SlotBank + 8, new byte[6]).WriteWord(SlotBank + 12, 1000).WriteWord(SlotBank + 14, 0xCAFE);
            for (uint axis = 0; axis < 3; axis++)
            {
                state = state.WriteWord(SlotBank + 16 + axis * 2, unchecked((ushort)speed));
            }
            return Frame with { State = state };
        }

        public NativeBattleDegenerator306Inputs Inputs(uint flags = 0) => new(flags, NativeBattleActionPacketsState.Fresh(), _ => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), () => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            NativeBattleDegenerator306DrawSprite180 draw = Assert.IsType<NativeBattleDegenerator306DrawSprite180>(operation);
            Assert.Equal(0xCE62A8u, draw.SourceAddress);
            Assert.Equal((byte)2, draw.DepthShift);
            Draws++;
            ObservedW = BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6));
            return frame with { Staging = frame.Staging!.Write(draw.TemporaryAddress + 0x28, BitConverter.GetBytes(NextOffset)) };
        });
    }
}
