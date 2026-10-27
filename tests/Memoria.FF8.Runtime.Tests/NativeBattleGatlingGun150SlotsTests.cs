using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun150SlotsTests
{
    [Fact]
    public void Continuous_birth_uses_original_size_radius_speed_and_gravity_values()
    {
        Fixture fixture = new();
        var (frame, address) = fixture.Task(0x60E820, 23);
        frame = frame with { State = ClearHeads(NativeBattleGatlingGun150State.FromUnknown()).WriteView(CasterPointGlobal, Packed(100, 200, 1000), 8) };
        var actual = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)2089, actual.Frame.State.ReadWord(ParticleBBase + 6));
        Assert.Equal(new short[] { 125, 339, 908 }, Enumerable.Range(0, 3).Select(axis => Signed(actual.Frame.State, ParticleBBase + 8 + (uint)axis * 2)));
        Assert.Equal(new short[] { 11, -53, -40 }, Enumerable.Range(0, 3).Select(axis => Signed(actual.Frame.State, ParticleBBase + 16 + (uint)axis * 2)));
        Assert.Equal(3, Enumerable.Range(0, ParticleCount).Count(index => actual.Frame.State.ReadDword(ParticleBBase + (uint)(index * ParticleStride)) != 0));
    }

    [Theory]
    [InlineData(0x60E820u, 3, 21, false)]
    [InlineData(0x60EDA0u, 10, 40, false)]
    [InlineData(0x60F330u, 12, 84, true)]
    public void Births_consume_exact_random_sequence_and_preserve_unwritten_packed_fields(uint callback, int count, int draws, bool bankA)
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun150Frame frame, uint address) = fixture.Task(callback, 0);
        frame = frame with { State = ClearHeads(NativeBattleGatlingGun150State.FromUnknown()).WriteView(CasterPointGlobal, Packed(100, 200, 1000), 8) };
        (frame, int result) = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result);
        Assert.Empty(fixture.Draws);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        uint bank = bankA ? ParticleABase : ParticleBBase;
        Assert.Equal(count, Enumerable.Range(0, ParticleCount).Count(index => frame.State.ReadDword(bank + (uint)(index * ParticleStride)) != 0));
        for (int index = 0; index < count; index++)
        {
            uint row = bank + (uint)(index * ParticleStride);
            Assert.Equal((ushort)0, frame.State.ReadWord(row + 4));
            Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 14));
            if (callback != 0x60F330)
            {
                Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 22));
            }
            if (callback == 0x60EDA0)
            {
                Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(row + 18));
                Assert.Equal((ushort)200, frame.State.ReadWord(row + 10));
            }
        }
        Assert.Equal((ushort)1, NativeBattleGatlingGun150Controller.Read(frame, address).ReadWord(12));
    }

    [Theory]
    [InlineData(0x60E820u, 0, false)]
    [InlineData(0x60EDA0u, 4, false)]
    [InlineData(0x60F330u, 1, true)]
    public void Paused_callback_draws_with_actual_flags_and_unknown_W_then_preserves_record_age_and_random(uint callback, ushort flags, bool bankA)
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun150Frame frame, uint address) = fixture.Task(callback, 0);
        uint row = bankA ? ParticleABase : ParticleBBase;
        frame = fixture.Row(frame, row, callback == 0x60EDA0);
        string before = NativeBattleTestSnapshots.Json(frame.State);
        (frame, int result) = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs(0x201));
        Assert.Equal(0, result);
        Draw draw = Assert.Single(fixture.Draws);
        Assert.Equal(flags, draw.Flags);
        Assert.Equal((ushort)7, draw.Phase);
        Assert.Equal(callback == 0x60F330 ? 0xDC234Cu : 0xDC21A0u, draw.Source);
        Assert.Equal(callback == 0x60F330 ? -123 : 0, draw.Angle);
        Assert.False(draw.SourceWKnown);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(frame.State));
        Assert.Equal(new NativeCrtRandomState(1), frame.Random);
        Assert.Equal((ushort)0, NativeBattleGatlingGun150Controller.Read(frame, address).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(0x60E820u, 1048, -7, -10, 7)]
    [InlineData(0x60EDA0u, 1056, -7, -10, 7)]
    [InlineData(0x60F330u, 1024, -6, -7, 6)]
    public void Live_update_uses_callback_specific_scale_motion_and_signed_damping(uint callback, ushort scale, short vx, short vy, short vz)
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun150Frame frame, uint address) = fixture.Task(callback, 10);
        uint row = callback == 0x60F330 ? ParticleABase : ParticleBBase;
        frame = fixture.Row(frame, row, callback == 0x60EDA0);
        (frame, int result) = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result);
        Assert.Equal(scale, frame.State.ReadWord(row + 6));
        Assert.Equal((ushort)92, frame.State.ReadWord(row + 8));
        Assert.Equal((ushort)(callback == 0x60EDA0 ? 200 : 190), frame.State.ReadWord(row + 10));
        Assert.Equal((ushort)1008, frame.State.ReadWord(row + 12));
        Assert.Equal(vx, Signed(frame.State, row + 16));
        Assert.Equal(vz, Signed(frame.State, row + 20));
        if (callback == 0x60EDA0)
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
    [InlineData(0x60E820u, 24)]
    [InlineData(0x60EDA0u, 15)]
    [InlineData(0x60F330u, 14)]
    public void Dead_draw_increments_phase_clears_only_head_and_can_finish_without_motion(uint callback, ushort age)
    {
        Fixture fixture = new() { Dead = true };
        (NativeBattleGatlingGun150Frame frame, uint address) = fixture.Task(callback, age);
        uint row = callback == 0x60F330 ? ParticleABase : ParticleBBase;
        frame = fixture.Row(frame, row, callback == 0x60EDA0);
        (frame, int result) = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs());
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
        (NativeBattleGatlingGun150Frame frame, uint address) = fixture.Task(0x60EDA0, 8);
        frame = fixture.Row(frame, ParticleBBase, true);
        fixture.AfterDraw = current => current with { State = current.State.WriteWord(ParticleBBase + 8, 500).WriteWord(ParticleBBase + 16, 16) };
        (frame, _) = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal((ushort)516, frame.State.ReadWord(ParticleBBase + 8));
        Assert.Equal((ushort)14, frame.State.ReadWord(ParticleBBase + 16));
        fixture.AfterDraw = null;
        NativeBattleGatlingGun150Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGatlingGun150Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var expected = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs());
        var actual = NativeBattleGatlingGun150Controller.AdvanceSlots(restored, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
    }

    [Fact]
    public void Full_bank_consumes_no_birth_random_and_pause_does_not_require_unselected_rows()
    {
        Fixture fixture = new();
        (NativeBattleGatlingGun150Frame frame, uint address) = fixture.Task(0x60EDA0, 0);
        NativeBattleGatlingGun150State state = ClearHeads(NativeBattleGatlingGun150State.FromUnknown());
        for (int index = 0; index < ParticleCount; index++)
        {
            state = state.WriteDword(ParticleBBase + (uint)(index * ParticleStride), 2);
        }
        frame = frame with { State = state };
        (frame, _) = NativeBattleGatlingGun150Controller.AdvanceSlots(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Empty(fixture.Draws);
        Assert.Equal(new NativeCrtRandomState(1), frame.Random);
        Assert.Equal((ushort)1, NativeBattleGatlingGun150Controller.Read(frame, address).ReadWord(12));
    }

    private static NativeBattleGatlingGun150State ClearHeads(NativeBattleGatlingGun150State state)
    {
        for (int index = 0; index < ParticleCount; index++)
        {
            state = state.WriteDword(ParticleABase + (uint)(index * ParticleStride), 0).WriteDword(ParticleBBase + (uint)(index * ParticleStride), 0);
        }
        return state;
    }

    private static short Signed(NativeBattleGatlingGun150State state, uint address) => unchecked((short)state.ReadWord(address));
    private static NativeBattleEffectArena Packed(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
    private sealed record Draw(uint Source, ushort Flags, ushort Phase, int Angle, bool SourceWKnown);

    private sealed class Fixture
    {
        public NativeBattleGatlingGun150Resources Resources { get; } = new(null!, null!, null!, new(0, 0xDC1FA4, 0, 0, []), new(0, 0xDC21A0, 0, 0, []), new(0, 0xDC234C, 0, 0, []), null!, 0x61AD6);
        public List<Draw> Draws { get; } = [];
        public bool Dead { get; init; }
        public Func<NativeBattleGatlingGun150Frame, NativeBattleGatlingGun150Frame>? AfterDraw { get; set; }

        public (NativeBattleGatlingGun150Frame Frame, uint Address) Task(uint callback, ushort age)
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            NativeBattleOriginalEffectState effect = new(150, 150, new(150, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [root, children] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = Camera() };
            NativeBattleGatlingGun150Frame frame = new(memory, effect, new(1), NativeBattleGatlingGun150State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            (frame, uint address) = NativeBattleGatlingGun150Controller.Append(frame, callback);
            NativeBattleModulePoolTask task = NativeBattleGatlingGun150Controller.Read(frame, address).WriteWord(12, age).WriteWord(14, 1).WriteWord(16, 100).WriteWord(18, 200).WriteWord(20, 1000).MarkPayloadUnknown(22, 2);
            return (NativeBattleGatlingGun150Controller.Write(frame, address, task), address);
        }

        public NativeBattleGatlingGun150Frame Row(NativeBattleGatlingGun150Frame frame, uint row, bool unknownY)
        {
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 24).Write(0, BitConverter.GetBytes(1u)).Write(4, BitConverter.GetBytes((ushort)7)).Write(6, BitConverter.GetBytes((ushort)1024));
            retained = NativeBattleEffectNodeContextMemory.Copy(Packed(100, 200, 1000), 0, retained, 8, 8).Write(16, BitConverter.GetBytes((short)-8)).Write(20, BitConverter.GetBytes((short)8)).Write(22, BitConverter.GetBytes((short)-123));
            if (!unknownY)
            {
                retained = retained.Write(18, BitConverter.GetBytes((short)-10));
            }
            return frame with { State = frame.State.WriteView(row, retained, 24) };
        }

        public NativeBattleGatlingGun150Inputs Inputs(uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => Camera(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            NativeBattleGatlingGun150DrawSprite180 draw = Assert.IsType<NativeBattleGatlingGun150DrawSprite180>(operation);
            NativeBattleFileStaging staging = frame.Staging!;
            ushort wordFlags = BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(draw.TemporaryAddress + 0x24, 2));
            int angle = draw.SourceAddress == 0xDC234C ? BinaryPrimitives.ReadInt32LittleEndian(staging.Read(draw.TemporaryAddress + 8, 4)) : 0;
            Draws.Add(new(draw.SourceAddress, wordFlags, BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(draw.TemporaryAddress + 4, 2)), angle, frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]));
            if ((wordFlags & 4) != 0)
            {
                Assert.Equal(0x404040u, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(draw.TemporaryAddress + 0x1C, 4)));
            }
            frame = frame with { Staging = staging.Write(draw.TemporaryAddress + 0x28, BitConverter.GetBytes(Dead ? (short)-1 : (short)0)) };
            return AfterDraw?.Invoke(frame) ?? frame;
        });

        private static NativeBattlePackedMatrix32 Camera() => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 0xCAFE);
    }
}
