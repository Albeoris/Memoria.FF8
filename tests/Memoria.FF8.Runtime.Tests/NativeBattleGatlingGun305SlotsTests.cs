using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun305SlotsTests
{
    [Theory]
    [InlineData(0x5A5DA0u, 3, 21, false)]
    [InlineData(0x5A6330u, 10, 40, false)]
    [InlineData(0x5A68C0u, 12, 84, true)]
    public void Births_consume_exact_random_sequence_and_preserve_unwritten_packed_fields(uint callback, int count, int draws, bool bankA)
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun305Frame frame, uint address) = fixture.Task(callback, 0);
        frame = frame with { State = NativeBattleGatlingGun305State.FromUnknown().ClearSlotHeads().WriteView(AnchorGlobal, Packed(100, 200, 1000), 8) };
        (frame, int result) = NativeBattleGatlingGun305Slots.Advance(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result);
        Assert.Empty(fixture.Draws);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        uint bank = bankA ? SlotBankA : SlotBankB;
        Assert.Equal(count, Enumerable.Range(0, SlotCount).Count(index => frame.State.ReadDword(bank + (uint)(index * SlotStride)) != 0));
        for (int index = 0; index < count; index++)
        {
            uint row = bank + (uint)(index * SlotStride);
            Assert.Equal((ushort)0, frame.State.ReadWord(row + 4));
            Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 14));
            if (callback != 0x5A68C0)
            {
                Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 22));
            }
            if (callback == 0x5A6330)
            {
                Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 18));
                Assert.Equal((ushort)200, frame.State.ReadWord(row + 10));
            }
        }
        Assert.Equal((ushort)1, NativeBattleGatlingGun305Controller.Read(frame, address).ReadWord(12));
    }

    [Theory]
    [InlineData(0x5A5DA0u, 4, false)]
    [InlineData(0x5A6330u, 4, false)]
    [InlineData(0x5A68C0u, 1, true)]
    public void Paused_callback_draws_with_actual_flags_and_unknown_W_then_preserves_record_age_and_random(uint callback, ushort flags, bool bankA)
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun305Frame frame, uint address) = fixture.Task(callback, 0);
        uint row = bankA ? SlotBankA : SlotBankB;
        frame = fixture.Row(frame, row, callback == 0x5A6330);
        string before = NativeBattleTestSnapshots.Json(frame.State);
        (frame, int result) = NativeBattleGatlingGun305Slots.Advance(frame, address, fixture.Resources, fixture.Inputs(0x201));
        Assert.Equal(0, result);
        Draw draw = Assert.Single(fixture.Draws);
        Assert.Equal(flags, draw.Flags);
        Assert.Equal((ushort)7, draw.Phase);
        Assert.Equal(callback == 0x5A68C0 ? 0xCED504u : 0xCED358u, draw.Source);
        Assert.Equal(callback == 0x5A68C0 ? -123 : 0, draw.Angle);
        Assert.False(draw.SourceWKnown);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(frame.State));
        Assert.Equal(new NativeCrtRandomState(1), frame.Random);
        Assert.Equal((ushort)0, NativeBattleGatlingGun305Controller.Read(frame, address).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(0x5A5DA0u, 1048, -7, -10, 7)]
    [InlineData(0x5A6330u, 1056, -7, -10, 7)]
    [InlineData(0x5A68C0u, 1024, -6, -7, 6)]
    public void Live_update_uses_callback_specific_scale_motion_and_signed_damping(uint callback, ushort scale, short vx, short vy, short vz)
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun305Frame frame, uint address) = fixture.Task(callback, 10);
        uint row = callback == 0x5A68C0 ? SlotBankA : SlotBankB;
        frame = fixture.Row(frame, row, callback == 0x5A6330);
        (frame, int result) = NativeBattleGatlingGun305Slots.Advance(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result);
        Assert.Equal(scale, frame.State.ReadWord(row + 6));
        Assert.Equal((ushort)92, frame.State.ReadWord(row + 8));
        Assert.Equal((ushort)(callback == 0x5A6330 ? 200 : 190), frame.State.ReadWord(row + 10));
        Assert.Equal((ushort)1008, frame.State.ReadWord(row + 12));
        Assert.Equal(vx, Signed(frame.State, row + 16));
        Assert.Equal(vz, Signed(frame.State, row + 20));
        if (callback == 0x5A6330)
        {
            Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 18));
        }
        else
        {
            Assert.Equal(vy, Signed(frame.State, row + 18));
        }
        Assert.Equal((ushort)8, frame.State.ReadWord(row + 4));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 14));
    }

    [Theory]
    [InlineData(0x5A5DA0u, 24)]
    [InlineData(0x5A6330u, 15)]
    [InlineData(0x5A68C0u, 14)]
    public void Dead_draw_increments_phase_clears_only_head_and_can_finish_without_motion(uint callback, ushort age)
    {
        Fixture fixture = new() { Dead = true };
        (NativeBattleGatlingGun305Frame frame, uint address) = fixture.Task(callback, age);
        uint row = callback == 0x5A68C0 ? SlotBankA : SlotBankB;
        frame = fixture.Row(frame, row, callback == 0x5A6330);
        (frame, int result) = NativeBattleGatlingGun305Slots.Advance(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result);
        Assert.Equal(0u, frame.State.ReadDword(row));
        Assert.Equal((ushort)8, frame.State.ReadWord(row + 4));
        Assert.Equal((ushort)1024, frame.State.ReadWord(row + 6));
        Assert.Equal((ushort)100, frame.State.ReadWord(row + 8));
        Assert.Equal((short)-8, Signed(frame.State, row + 16));
    }

    [Fact]
    public void Draw_returned_canonical_state_is_updated_and_JSON_resume_has_identical_next_step()
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun305Frame frame, uint address) = fixture.Task(0x5A6330, 8);
        frame = fixture.Row(frame, SlotBankB, true);
        fixture.AfterDraw = current => current with { State = current.State.WriteWord(SlotBankB + 8, 500).WriteWord(SlotBankB + 16, 16) };
        (frame, _) = NativeBattleGatlingGun305Slots.Advance(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal((ushort)516, frame.State.ReadWord(SlotBankB + 8));
        Assert.Equal((ushort)14, frame.State.ReadWord(SlotBankB + 16));
        fixture.AfterDraw = null;
        NativeBattleGatlingGun305Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGatlingGun305Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var expected = NativeBattleGatlingGun305Slots.Advance(frame, address, fixture.Resources, fixture.Inputs());
        var actual = NativeBattleGatlingGun305Slots.Advance(restored, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
    }

    [Fact]
    public void Full_bank_consumes_no_birth_random_and_pause_does_not_require_unselected_rows()
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun305Frame frame, uint address) = fixture.Task(0x5A6330, 0);
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromUnknown().ClearSlotHeads();
        for (int index = 0; index < SlotCount; index++)
        {
            state = state.WriteDword(SlotBankB + (uint)(index * SlotStride), 2);
        }
        frame = frame with { State = state };
        (frame, _) = NativeBattleGatlingGun305Slots.Advance(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Empty(fixture.Draws);
        Assert.Equal(new NativeCrtRandomState(1), frame.Random);
        Assert.Equal((ushort)1, NativeBattleGatlingGun305Controller.Read(frame, address).ReadWord(12));
    }

    private static short Signed(NativeBattleGatlingGun305State state, uint address) => unchecked((short)state.ReadWord(address));
    private static NativeBattleEffectArena Packed(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
    private sealed record Draw(uint Source, ushort Flags, ushort Phase, int Angle, bool SourceWKnown);

    private sealed class Fixture
    {
        public NativeBattleGatlingGun305Resources Resources { get; } = new(new("unused", []), new("unused", []), new(0, 0, []), new(0, 0, []), new(0, 0, []), new(0, 0xCED358, 0, 0, []), new(0, 0xCED15C, 0, 0, []), new(0, 0xCED504, 0, 0, []), 0x61AD6);
        public List<Draw> Draws { get; } = [];
        public bool Dead { get; init; }
        public Func<NativeBattleGatlingGun305Frame, NativeBattleGatlingGun305Frame>? AfterDraw { get; set; }

        public (NativeBattleGatlingGun305Frame Frame, uint Address) Task(uint callback, ushort age)
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            NativeBattleOriginalEffectState effect = new(305, 305, new(305, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [root, children] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = Camera() };
            NativeBattleGatlingGun305Frame frame = new(memory, effect, new(1), NativeBattleGatlingGun305State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            (frame, uint address) = NativeBattleGatlingGun305Controller.Append(frame, callback);
            NativeBattleModulePoolTask task = NativeBattleGatlingGun305Controller.Read(frame, address).WriteWord(12, age).WriteWord(14, 1).WriteWord(16, 100).WriteWord(18, 200).WriteWord(20, 1000).MarkPayloadUnknown(22, 2);
            return (NativeBattleGatlingGun305Controller.Write(frame, address, task), address);
        }

        public NativeBattleGatlingGun305Frame Row(NativeBattleGatlingGun305Frame frame, uint row, bool unknownY)
        {
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 24).Write(0, BitConverter.GetBytes(1u)).Write(4, BitConverter.GetBytes((ushort)7)).Write(6, BitConverter.GetBytes((ushort)1024));
            retained = NativeBattleEffectNodeContextMemory.Copy(Packed(100, 200, 1000), 0, retained, 8, 8).Write(16, BitConverter.GetBytes((short)-8)).Write(20, BitConverter.GetBytes((short)8)).Write(22, BitConverter.GetBytes((short)-123));
            if (!unknownY)
            {
                retained = retained.Write(18, BitConverter.GetBytes((short)-10));
            }
            return frame with { State = frame.State.WriteView(row, retained, 24) };
        }

        public NativeBattleGatlingGun305Inputs Inputs(uint flags = 0) => new(flags, NativeBattleActionPacketsState.Fresh(), _ => throw new InvalidOperationException(), _ => Camera(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            NativeBattleGatlingGun305DrawSprite180 draw = Assert.IsType<NativeBattleGatlingGun305DrawSprite180>(operation);
            NativeBattleFileStaging staging = frame.Staging!;
            ushort wordFlags = BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(draw.TemporaryAddress + 0x24, 2));
            int angle = draw.SourceAddress == 0xCED504 ? BinaryPrimitives.ReadInt32LittleEndian(staging.Read(draw.TemporaryAddress + 8, 4)) : 0;
            Draws.Add(new(draw.SourceAddress, wordFlags, BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(draw.TemporaryAddress + 4, 2)), angle, frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]));
            if (draw.SourceAddress != 0xCED504)
            {
                Assert.Equal(0x404040u, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(draw.TemporaryAddress + 0x1C, 4)));
            }
            frame = frame with { Staging = staging.Write(draw.TemporaryAddress + 0x28, BitConverter.GetBytes(Dead ? (short)-1 : (short)0)) };
            return AfterDraw?.Invoke(frame) ?? frame;
        });

        private static NativeBattlePackedMatrix32 Camera() => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 0xCAFE);
    }
}
