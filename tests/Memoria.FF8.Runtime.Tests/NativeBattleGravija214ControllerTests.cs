using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGravija214Controller;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGravija214ControllerTests
{
    [Fact]
    public void Entry_copies_source_resets_root_and_defers_COMMON_secondary_reset()
    {
        var inactive = Inactive();
        inactive = inactive with { Memory = inactive.Memory with { Arena = inactive.Memory.Arena!.Write(0x700, new byte[] { 0xAB, 0xCD }) } };
        var frame = Initialize(inactive, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(Resources().EntryCopySeed.Data.ToArray(), frame.State.Read(CopiedMeshAddress, EntryCopyLength));
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame.Memory.Arena!.Read(0x700, 2));
        Assert.Equal(ModelAddressFor(3), frame.State.ReadDword(CasterGlobal));
        Assert.Equal(0x1D99A78u, frame.State.ReadDword(ParametersGlobal));
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame.Memory.Arena!.Read(0x700, 2));
    }

    [Theory]
    [InlineData(0u, 0x11970u, 1u)]
    [InlineData(1u, 0x1970u, 0u)]
    [InlineData(0x80000000u, 0x1970u, 0u)]
    public void Root_parity_uses_full_DWORD_without_graphics_pause(uint old, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, old));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Old_one_clears_framebuffer_and_origin_then_builds_wrapping_mean_with_frame_publications()
    {
        var packets = Packets(2);
        var operations = new List<NativeBattleGravija214Operation>();
        var frame = Initialize(Inactive(), Resources(), Inputs(operations, packets));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        var view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 42 }, null);
        List<uint> models = [];
        var inputs = Inputs(operations, packets) with { ReadAnchor571400 = (current, model) =>
        {
            models.Add(model);
            return new(current with { Memory = current.Memory with { SharedEffectView2793E58 = view } }, PointView(new(30000, -30000, 13)));
        } };
        frame = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        Assert.Equal(new[] { ModelAddressFor(0), ModelAddressFor(1) }, models);
        Assert.Equal(view, frame.Memory.SharedEffectView2793E58);
        Assert.IsType<NativeBattleGravija214Clear45BEE0>(operations[0]);
        Assert.Equal(new NativeBattleGravija214SetOrigin45B5E0(576, 256), operations[1]);
        Assert.Equal(new NativeSoftwareDrawingRectangle(576, 256, 320, 256), ((NativeBattleGravija214Clear45BEE0)operations[0]).Rectangle);
        Assert.Equal(3, frame.Effect.Pools.Length);
        Assert.Equal(28, frame.Effect.Pools[1].Stride);
        Assert.Equal(64, frame.Effect.Pools[1].Slots.Length);
        Assert.Equal((ushort)1, Read(frame, Pools[2].Bank).ReadWord(12));
        Assert.Equal(unchecked((ushort)-2768), Read(frame, Pools[2].Bank).ReadWord(40));
        Assert.Equal((ushort)2768, Read(frame, Pools[2].Bank).ReadWord(42));
        Assert.Equal((ushort)13, Read(frame, Pools[2].Bank).ReadWord(44));
        Assert.Equal(ModelAddressFor(1), Read(frame, Pools[2].Bank).ReadDword(24));
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(0, 1));
    }

    [Fact]
    public void Zero_contact_mean_fails_instead_of_supplying_a_position()
    {
        var packets = SetCount(Packets(1), 0);
        var frame = Initialize(Inactive(), Resources(), Inputs([], packets));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        Assert.Throws<NotSupportedException>(() => AdvanceTask(frame, RootBank, Resources(), Inputs([], packets)));
    }

    [Fact]
    public void Age_eighteen_births_sixty_four_particles_with_six_CRT_draws_each_and_partial_payloads()
    {
        var frame = MainAt(18);
        var expected = frame.Random;
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([])).Frame;
        for (int index = 0; index < 64; index++)
        {
            var task = actual.Effect.Pools[1].Slots[index];
            expected = expected.Draw(out ushort life).Draw(out ushort angle).Draw(out ushort x).Draw(out ushort y).Draw(out ushort z).Draw(out ushort radius);
            Assert.Equal((ushort)(50 - (life & 7)), task.ReadWord(14));
            Assert.Equal(angle, task.ReadWord(16));
            Assert.Equal(x, task.ReadWord(20));
            Assert.Equal(y, task.ReadWord(22));
            Assert.Equal(z, task.ReadWord(24));
            Assert.Equal((ushort)((radius & 1023) + 1500), task.ReadWord(18));
            Assert.Equal((ushort)0, task.ReadWord(26));
        }
        Assert.Equal(expected, actual.Random);
        Assert.Equal((ushort)0, Read(actual, Pools[2].Bank).ReadWord(14));
    }

    [Fact]
    public void Exhausted_particle_appends_consume_no_CRT_draws()
    {
        var frame = MainAt(18);
        for (int index = 0; index < 64; index++)
        {
            (frame, _) = Append(frame, Pools[1].Descriptor, 0x69DF00);
        }
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)33, 15)]
    [InlineData((ushort)34, 16)]
    [InlineData((ushort)52, 34)]
    [InlineData((ushort)53, 35)]
    [InlineData((ushort)67, 49)]
    public void Scale_uses_native_sine_plateau_then_cosine_and_contacts_only_at_sixty_seven(ushort age, int time)
    {
        var frame = MainAt(age);
        List<NativeBattleGravija214Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs(operations)).Frame;
        int expected = time < 16 ? NativeBattleCameraMath.Sine(time << 6) : time <= 34 ? 4096 : NativeBattleCameraMath.Cosine((time - 34) << 6);
        Assert.Equal(unchecked((ushort)expected), Read(actual, Pools[2].Bank).ReadWord(14));
        Assert.Equal(age == 67 ? 1 : 0, operations.OfType<NativeBattleGravija214ContactsAll506BA0>().Count());
        if (age == 67)
        {
            Assert.Equal(1, Assert.Single(operations.OfType<NativeBattleGravija214ContactsAll506BA0>()).HeaderIndex);
        }
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Reload_waits_on_only_the_physical_completion_BYTE_and_keeps_age_seventy_five()
    {
        var frame = MainAt(74);
        uint main = Pools[2].Bank;
        frame = Write(frame, main, Read(frame, main).MarkPayloadUnknown(19, 1));
        List<NativeBattleGravija214Operation> operations = [];
        var actual = AdvanceTask(frame, main, Resources(), Inputs(operations));
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)75, Read(actual.Frame, main).ReadWord(12));
        Assert.Equal(new NativeBattleGravija214CommonTextureLoad508630(ArenaAddress + 0x1970, main + 18), Assert.Single(operations));
        actual = AdvanceTask(actual.Frame, main, Resources(), Inputs(operations));
        Assert.Single(operations);
        Assert.Equal((ushort)75, Read(actual.Frame, main).ReadWord(12));
        var ready = Write(actual.Frame, main, Read(actual.Frame, main).WriteByte(18, 0x80));
        Assert.Equal(2, AdvanceTask(ready, main, Resources(), Inputs([])).Result);
        Assert.Throws<NotSupportedException>(() => Read(ready, main).ReadByte(19));
    }

    [Fact]
    public void Root_completion_transfers_still_active_particle_owner_and_warm_entry_recopies_source()
    {
        var frame = MainAt(75);
        uint main = Pools[2].Bank;
        frame = Write(frame, main, Read(frame, main).WriteByte(18, 1));
        (frame, uint child) = Append(frame, Pools[1].Descriptor, 0x69DF00);
        frame = Write(frame, child, Read(frame, child).WriteWord(14, 50).WriteWord(18, 1500));
        frame = frame with { State = frame.State.WriteWord(CopiedMeshAddress + 8, 0xCAFE) };
        var actual = Advance(frame, Resources(), Inputs([]));
        Assert.Empty(actual.Effect.Pools);
        Assert.NotNull(actual.State.RootBankStorage);
        Assert.Equal(1, actual.Memory.Arena!.Read(0, 1)[0] & 1);
        actual = JsonSerializer.Deserialize<NativeBattleGravija214Frame>(JsonSerializer.Serialize(actual))!;
        actual = Initialize(actual, Resources(), Inputs([]));
        Assert.Single(actual.Effect.Pools);
        Assert.Equal(Resources().EntryCopySeed.Data.ToArray(), actual.State.Read(CopiedMeshAddress, EntryCopyLength));
        Assert.Equal(1, actual.Memory.Arena!.Read(0, 1)[0] & 1);
    }

    [Fact]
    public void Warp_minimum_dimensions_use_quarter_depth_and_keep_unwritten_matrix_bytes()
    {
        var frame = MainAt(26);
        frame = frame with { State = frame.State.WriteWord(MatrixAddress + 16, 0x2345).WriteWord(MatrixAddress + 18, 0x6789).WriteDword(MatrixAddress + 28, 0x12345678) };
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([])).Frame;
        ushort expected = checked((ushort)(0x18000 / (300 >> 2)));
        Assert.Equal((ushort)1310, expected);
        Assert.Equal(expected, actual.State.ReadWord(MatrixAddress));
        Assert.Equal(expected, actual.State.ReadWord(MatrixAddress + 8));
        Assert.Equal((ushort)0x2345, actual.State.ReadWord(MatrixAddress + 16));
        Assert.Equal((ushort)0x6789, actual.State.ReadWord(MatrixAddress + 18));
        Assert.Equal(0x12345678u, actual.State.ReadDword(MatrixAddress + 28));
    }

    [Fact]
    public void Mean_projection_requires_actual_task_packed_W_even_when_XYZ_are_known()
    {
        var frame = MainAt(26);
        frame = Write(frame, Pools[2].Bank, Read(frame, Pools[2].Bank).MarkPayloadUnknown(46, 2));
        Assert.Throws<NotSupportedException>(() => AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([])));
    }

    private static NativeBattleGravija214Frame Active()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        return AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
    }

    private static NativeBattleGravija214Frame MainAt(ushort age)
    {
        var frame = Active();
        return Write(frame, Pools[2].Bank, Read(frame, Pools[2].Bank).WriteWord(12, age));
    }

    private static NativeBattleGravija214Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(214, 213, new(214, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleGravija214State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleGravija214Inputs Inputs(List<NativeBattleGravija214Operation> operations, NativeBattleActionPacketsState? packets = null)
    {
        packets ??= Packets(1);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, address) => Model(address), (current, address) => new(current, PointView(new(100, 200, 300))), _ => packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        }) { ReadOrderingTable = _ => 0x1D8E058 };
    }

    private static NativeBattleGraphicsModel Model(uint address)
    {
        int actor = checked((int)((address - 0x1D972C0) / 156));
        return new(actor, 2) { Rotation14 = 0, MinimumY146 = -100, Transform = new(new(0, 0, 0), new((short)(100 + actor), (short)(200 + actor), (short)(3000 + actor)), (short)(444 + actor), 777, 4096) };
    }

    private static uint ModelAddressFor(int actor) => 0x1D972C0u + (uint)(actor * 156);

    private static NativeBattleActionPacketsState Packets(int count)
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 309, 214, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 309, 214, null));
        for (int index = 0; index < count; index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)index, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(checked((byte)count));
        return packets.WritePlayerRepeatMode(1, 1);
    }

    private static NativeBattleActionPacketsState SetCount(NativeBattleActionPacketsState packets, byte count)
    {
        NativeBattleActionHeaderSlot header = packets.Headers[1];
        return packets with { Headers = packets.Headers.SetItem(1, header with { Payload = header.Payload.SetItem(16, count), UnknownByteMask = header.UnknownByteMask & ~(1u << 16) }) };
    }

    private static NativeBattleGravija214Resources Resources() => NativeBattleGravija214StateTests.Resources();
}
