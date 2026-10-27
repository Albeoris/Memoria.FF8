using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect70Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect70ControllerTests
{
    [Theory]
    [InlineData(0, 0x1AE9C, 0x2AE9C)]
    [InlineData(1, 0xAE9C, 0x1AE9C)]
    [InlineData(255, 0xAE9C, 0x1AE9C)]
    public void Root_selects_actual_byte_packet_banks_before_start(byte parity, uint first, uint second)
    {
        NativeBattleEffect70Frame frame = Active();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(14, parity));
        NativeBattleEffect70Step step = AdvanceRoot(frame, RootBank, null!, new Harness().Inputs());
        Assert.Equal(ArenaAddress + first, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(ArenaAddress + second, step.Frame.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal((ushort)1, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Single(step.Frame.Effect.Pools);
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(1, 59, -361, -13641, 8304, -365, -16445, 538)]
    [InlineData(-1, 59, -361, -13641, -8304, -365, -16445, -538)]
    [InlineData(1, 213, 6818, -7714, 445, 702, 99, 459)]
    [InlineData(-1, 213, 6818, -7714, -445, 702, 99, -459)]
    public void Camera_direct_endpoints_keep_target_and_eye_physical_order(int side, int age, short ex, short ey, short ez, short tx, short ty, short tz)
    {
        Harness harness = new();
        NativeBattleEffect70Frame frame = Active();
        frame = frame with { State = frame.State.WriteDword(SideGlobal, unchecked((uint)side)) };
        _ = AdvanceCamera(frame, age, harness.Inputs());
        NativeBattleCameraPose pose = Assert.Single(harness.Operations.OfType<NativeBattleEffect70CameraPose>()).Pose;
        Assert.Equal(new NativeBattleCameraPoint(ex, ey, ez), pose.Eye);
        Assert.Equal(new NativeBattleCameraPoint(tx, ty, tz), pose.Target);
        Assert.Null(pose.FocalLength);
        if (age == 213)
        {
            Assert.Equal((ushort)512, Assert.Single(harness.Operations.OfType<NativeBattleEffect70CameraFocal>()).Value);
        }
    }

    [Fact]
    public void Camera_first_interpolation_reaches_endpoint_with_native_divisor38()
    {
        Harness harness = new();
        _ = AdvanceCamera(Active(), 58, harness.Inputs());
        NativeBattleCameraPose pose = Assert.Single(harness.Operations.OfType<NativeBattleEffect70CameraPose>()).Pose;
        Assert.Equal(new NativeBattleCameraPoint(-102, -8385, 2438), pose.Target);
        Assert.Equal(new NativeBattleCameraPoint(418, 1741, 10573), pose.Eye);
    }

    [Fact]
    public void Common_pool_transfer_reinitializes_headers_but_retains_payload_and_unknownness()
    {
        NativeBattleEffect70Frame frame = Active();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(MainBankOffset + 12, [0xA5]).MarkUnknown(MainBankOffset + 13, 1) } };
        frame = InitializeCommonPool(frame, 1);
        uint main = Pools[1].Bank;
        Assert.Equal((byte)0xA5, Read(frame, main).ReadByte(12));
        Assert.Throws<NotSupportedException>(() => Read(frame, main).ReadByte(13));
        Assert.Equal((byte)0xA5, frame.Memory.Arena!.Read(MainBankOffset + 12, 1)[0]);
        frame = InitializeCommonPool(frame, 2);
        (frame, uint child) = Append(frame, 2, 0x6E0100);
        frame = PutWord(frame, child + 16, 0x4567);
        frame = InitializeCommonPool(frame, 2);
        Assert.Null(frame.Effect.Pools[2].Head);
        Assert.Equal((ushort)0x4567, Read(frame, child).ReadWord(16));
        Assert.Equal((ushort)0, Read(frame, child).Flags);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    public void Terminal_main_waits_for_actual_io_without_increment_and_keeps_orphan_bytes(int ready, int result)
    {
        Harness harness = new() { Ready = ready };
        NativeBattleEffect70Frame frame = Common();
        (frame, uint child) = Append(frame, 2, 0x6E0100);
        frame = PutWord(frame, child + 16, 0x4567);
        frame = PutWord(frame, Pools[1].Bank + 12, 262);
        NativeBattleEffect70Step step = AdvanceMain(frame, Pools[1].Bank, null!, harness.Inputs());
        Assert.Equal(result, step.Result);
        Assert.Equal((ushort)262, Read(step.Frame, Pools[1].Bank).ReadWord(12));
        Assert.Null(step.Frame.Effect.Pools[2].Head);
        Assert.Null(step.Frame.Effect.Pools[2].Tail);
        Assert.NotEqual(0, Read(step.Frame, child).Flags & 1);
        Assert.Equal((ushort)0x4567, Read(step.Frame, child).ReadWord(16));
        Assert.IsType<NativeBattleEffect70IoAdvance534210>(harness.Operations[0]);
        Assert.Equal((ushort)512, Assert.Single(harness.Operations.OfType<NativeBattleEffect70CameraFocal>()).Value);
        Assert.Equal(1, harness.LastIoIndex);
    }

    [Fact]
    public void Root_release_copies_actual_orphan_flags_and_payload_back_to_common()
    {
        Harness harness = new() { Ready = 1 };
        NativeBattleEffect70Frame frame = Common();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 1).WriteWord(12, 263));
        frame = PutWord(frame, Pools[1].Bank + 12, 262);
        (frame, uint child) = Append(frame, 2, 0x6E0100);
        frame = PutWord(frame, child + 16, 0x4567);
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.Equal((ushort)0x4567, BitConverter.ToUInt16(frame.Memory.Arena!.Read(16, 2)));
        Assert.NotEqual(0, BitConverter.ToUInt16(frame.Memory.Arena.Read(0, 2)) & 1);
        Assert.Equal((ushort)0xA55A, frame.State.ReadWord(ViewAddress + 18));
    }

    [Fact]
    public void Orbit_spark_birth_consumes_three_shared_crt_draws_per_row()
    {
        NativeBattleEffect70Frame frame = NativeBattleEffect70Particles.InitializeSparks(Active());
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 12; draw++)
        {
            expected = expected.Draw(out _);
        }
        frame = BirthOrbitSparks(frame);
        Assert.Equal(expected, frame.Random);
        Assert.Equal((short)-4096, ReadShort(frame, ArenaAddress + SparkOffset + 6));
        Assert.Equal((short)4096, ReadShort(frame, ArenaAddress + SparkOffset + 22));
        Assert.Equal(0x1324B98u, ReadDword(frame, ArenaAddress + SparkOffset));
        Assert.Equal((byte)80, frame.Memory.Arena!.Read(SparkOffset + 15, 1)[0]);
        Assert.Equal(ArenaAddress + SparkOffset + 64, frame.State.ReadDword(SparkCursorGlobal));
    }

    [Fact]
    public void Restore_changes_only_saved_visibility_bit_on_current_enabled_models()
    {
        Harness harness = new();
        NativeBattleEffect70Frame frame = Active();
        frame = frame with { State = frame.State.WriteDword(SavedFlagsAddress, 4).WriteDword(SavedFlagsAddress + 4, 4) };
        _ = RestoreFlags(frame, harness.Inputs());
        NativeBattleEffect70ModelFlags operation = Assert.Single(harness.Operations.OfType<NativeBattleEffect70ModelFlags>());
        Assert.Equal(new NativeBattleEffect70ModelFlags(0, 0xFFFB, 4, true), operation);
    }

    private static NativeBattleEffect70Frame Common()
    {
        NativeBattleEffect70Frame frame = InitializeCommonPool(InitializeCommonPool(Active(), 1), 2);
        (frame, _) = Append(frame, 1, MainCallback);
        return ClearBytes(frame, Pools[1].Bank + 12, 0x1150);
    }

    private static NativeBattleEffect70Frame Active()
    {
        NativeBattleOriginalEffectState effect = new(70, 69, new(70, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0));
        NativeBattleEffect70State state = NativeBattleEffect70State.FromColdProcess().DetachRootBank().WriteDword(ArenaGlobal, ArenaAddress).WriteDword(SideGlobal, 1).WriteWord(ViewAddress + 18, 0xA55A);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect with { Pools = [root], ReturnedDescriptor = RootDescriptor }, NativeCrtRandomState.FromColdProcess(), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public int Ready { get; init; }
        public int LastIoIndex { get; private set; } = -1;
        public List<NativeBattleEffect70Operation> Operations { get; } = [];

        public NativeBattleEffect70Inputs Inputs() => new(new(0x00808080, 0x32808080, 1, 0), frame => throw new NotSupportedException(), frame => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), frame => new(null, 100, 160, 108), (frame, actor) => new(actor, actor == 0 ? (ushort)2 : (ushort)1), (frame, actor) => throw new NotSupportedException(), frame => 0, (frame, source) => throw new NotSupportedException(), (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        })
        {
            ReadSavedFocal = frame => 512,
            SelectIo5342C0 = (frame, index) =>
            {
                LastIoIndex = index;
                return new(frame, Ready);
            }
        };
    }
}
