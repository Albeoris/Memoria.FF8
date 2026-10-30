using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Controller;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSnipeLaser302ControllerTests
{
    [Fact]
    public void Group19_actual_main_owners_keep_native_clock_rng_and_operations_while_capturing_each_copy()
    {
        List<NativeBattleSnipeLaser302Operation> nativeOperations = [];
        var native = AdvanceTask(MainAt(35, 0), Pools[4].Bank, Resources(), Inputs(nativeOperations)).Frame;
        string nativeState = NativeBattleTestSnapshots.Json(native);
        string nativeLog = string.Join("\n", nativeOperations.Select(operation => JsonSerializer.Serialize(operation, operation.GetType())));
        List<NativeBattleSnipeLaser302Operation> capturedOperations = [];
        var captured = AdvanceTask(MainAt(35, 0), Pools[4].Bank, Resources(), Inputs(capturedOperations) with { CapturePresentation = true }).Frame;
        Assert.Equal(nativeState, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativeLog, string.Join("\n", capturedOperations.Select(operation => JsonSerializer.Serialize(operation, operation.GetType()))));
        Assert.All(nativeOperations, operation => Assert.Null(operation.PilotIdentity));
        var draws = capturedOperations.OfType<NativeBattleSnipeLaser302DrawStatic88>().ToArray();
        Assert.NotEmpty(draws);
        var programmes = capturedOperations.OfType<NativeBattleSnipeLaser302DrawMesh7043B0>().ToArray();
        Assert.NotEmpty(programmes);
        Assert.All(programmes, programme => Assert.NotNull(programme.PilotIdentity));
        long expectedB = unchecked((((long)ProgrammeBAddress << 32) | 972u) * 397);
        long expectedC = unchecked((((long)ProgrammeCAddress << 32) | 1204u) * 397);
        Assert.Contains(programmes, programme => programme.PilotIdentity!.Value.ResourceGeneration == expectedB);
        Assert.Contains(programmes, programme => programme.PilotIdentity!.Value.ResourceGeneration == expectedC);
        Assert.All(programmes, programme => Assert.Equal((ushort)302, programme.PilotIdentity!.Value.Effect));
        Assert.All(draws, draw =>
        {
            Assert.Equal((ushort)302, draw.PilotIdentity!.Value.Effect);
            Assert.Equal((long)Resources().Mesh.Address, draw.PilotIdentity.Value.ResourceGeneration);
        });

    }

    [Fact]
    public void Entry_has_only_root_owner_and_defers_common_reset()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(ModelAddress(3), frame.State.ReadDword(CasterGlobal));
        Assert.Equal((byte)0xA5, frame.Memory.Arena!.Read(0x8C0, 1)[0]);
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
    }

    [Theory]
    [InlineData(0u, 0x11710u, 1u)]
    [InlineData(1u, 0x1710u, 0u)]
    [InlineData(0x80000000u, 0x1710u, 0u)]
    public void Full_DWORD_parity_has_no_graphics_pause(uint parity, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, parity));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Old_one_builds_two_clones_same_target_with_captured_F1_and_A_stays_unadvanced()
    {
        int captures = 0;
        int anchors = 0;
        var inputs = Inputs([]) with
        {
            ReadBone502170 = (current, model, selector, displacement) =>
            {
                captures++;
                Assert.Equal(ModelAddress(0), model);
                Assert.Equal(0xF1, selector);
                Assert.Equal(0, displacement);
                return new(current, PointView(new(11, -333, 55)));
            },
            ReadAnchor571400 = (current, model) =>
            {
                anchors++;
                return new(current, PointView(new(-100, -500, -1000), 777));
            }
        };
        var frame = Initialize(Inactive(), Resources(), inputs);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        Assert.Equal(2, captures);
        Assert.Equal(1, anchors);
        Assert.Equal((byte)2, Read(actual, RootBank).ReadByte(14));
        Assert.Equal((ushort)1, Read(actual, Pools[4].Bank).ReadWord(12));
        Assert.Equal(unchecked((ushort)-1), Read(actual, Pools[4].Bank + 1636).ReadWord(12));
        for (int index = 0; index < 2; index++)
        {
            var task = actual.Effect.Pools[4].Slots[index];
            Assert.Equal((ushort)index, task.ReadWord(14));
            Assert.Equal(ModelAddress(0), task.ReadDword(16));
            Assert.Equal(PointView(new(11, -333, 55), 0).Read(0, 8), TaskView(task, 20, 8).Read(0, 8));
            Assert.Equal(ProgrammeAAddress, task.ReadDword(28));
            Assert.Equal(0u, task.ReadDword(32));
            Assert.Equal(ProgrammeBAddress, task.ReadDword(972));
            Assert.Equal(ProgrammeCAddress, task.ReadDword(1204));
        }
        Assert.Equal(frame.Random, actual.Random);
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(0x8C0, 1));
    }

    [Theory]
    [InlineData((ushort)65534, (ushort)65535)]
    [InlineData((ushort)65535, (ushort)0)]
    public void Negative_age_skips_all_pose_and_GTE_producers(ushort age, ushort expected)
    {
        var frame = MainAt(age, 1);
        var inputs = Inputs([]) with
        {
            ReadCurrentBoneMatrix = (_, _, _) => throw new InvalidOperationException("Negative age must not read a bone."),
            ReadAnchor571400 = (_, _) => throw new InvalidOperationException("Negative age must not read an anchor.")
        };
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), inputs).Frame;
        Assert.Equal(expected, Read(actual, Pools[4].Bank).ReadWord(12));
        Assert.Equal(frame.Memory.GeometryRegisters, actual.Memory.GeometryRegisters);
        Assert.Equal(frame.Memory.ScriptedColorRegisters, actual.Memory.ScriptedColorRegisters);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)5)]
    [InlineData((ushort)15)]
    [InlineData((ushort)35)]
    [InlineData((ushort)44)]
    public void Visible_B_advances_while_A_remains_only_initialized(ushort age)
    {
        var frame = MainAt(age, 0);
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(0u, Read(actual, Pools[4].Bank).ReadDword(32));
        Assert.Equal(1u, Read(actual, Pools[4].Bank).ReadDword(976));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)0, 1)]
    [InlineData((ushort)1, 0)]
    public void Only_WORD_zero_clone_contacts_at_old_43_from_current_physical_pointer(ushort clone, int count)
    {
        var frame = MainAt(43, clone);
        var packets = Packets();
        var header = packets.Headers[1];
        packets = packets with { Headers = packets.Headers.SetItem(1, header with { Payload = header.Payload.SetItem(16, 0) }) };
        List<NativeBattleSnipeLaser302Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs(operations) with { ReadPackets = _ => packets }).Frame;
        Assert.Equal(count, operations.OfType<NativeBattleSnipeLaser302Contact506690>().Count());
        if (count != 0)
        {
            Assert.Equal(1, Assert.Single(operations.OfType<NativeBattleSnipeLaser302Contact506690>()).ContactIndex);
        }
        Assert.Equal((ushort)44, Read(actual, Pools[4].Bank).ReadWord(12));
    }

    [Theory]
    [InlineData((ushort)36)]
    [InlineData((ushort)37)]
    public void Clone_zero_bursts_eight_particles_with_three_CRT_each_and_signed_acceleration(ushort age)
    {
        var frame = MainAt(age, 0);
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs([])).Frame;
        var random = frame.Random;
        for (int index = 0; index < 8; index++)
        {
            var particle = actual.Effect.Pools[2].Slots[index];
            Assert.Equal(PointView(new(11, -333, 55), 0).Read(0, 8), TaskView(particle, 12, 8).Read(0, 8));
            for (int axis = 0; axis < 3; axis++)
            {
                random = random.Draw(out ushort value);
                short velocity = unchecked((short)((value & 511) - 256));
                Assert.Equal(unchecked((ushort)velocity), particle.ReadWord(20 + axis * 2));
                Assert.Equal(unchecked((ushort)(-velocity / 12)), particle.ReadWord(28 + axis * 2));
            }
        }
        Assert.Equal(random, actual.Random);
    }

    [Fact]
    public void Failed_particle_appends_consume_no_CRT()
    {
        var frame = MainAt(36, 0);
        for (int index = 0; index < 16; index++)
        {
            (frame, _) = Append(frame, Pools[2].Descriptor, 0x66D6D0);
        }
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)49, 0, (ushort)50)]
    [InlineData((ushort)50, 2, (ushort)50)]
    public void Main_retires_at_OLD_fifty_without_final_age_increment(ushort age, int result, ushort final)
    {
        var frame = MainAt(age, 1);
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs([]));
        Assert.Equal(result, actual.Result);
        Assert.Equal(final, Read(actual.Frame, Pools[4].Bank).ReadWord(12));
    }

    [Fact]
    public void Header_BYTE17_gate_is_distinct_from_contact_count_and_can_reject_all_births()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1).WriteByte(14, 1));
        var actual = Advance(frame, Resources(), Inputs([]));
        Assert.Empty(actual.Effect.Pools);
        Assert.NotNull(actual.State.RootBankStorage);
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Equal(frame.Random, actual.Random);
        var cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleSnipeLaser302Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Single(Initialize(cold, Resources(), Inputs([])).Effect.Pools);
    }

    private static NativeBattleSnipeLaser302Frame MainAt(ushort age, ushort clone)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        frame = AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
        return Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(12, age).WriteWord(14, clone));
    }

    private static NativeBattleSnipeLaser302Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0x8C0, new byte[] { 0xA5 }), ModuleSpriteBillboard21DFED0 = Identity() };
        NativeBattleOriginalEffectState effect = new(302, 301, new(302, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleSnipeLaser302State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleSnipeLaser302Inputs Inputs(List<NativeBattleSnipeLaser302Operation> operations)
    {
        var packets = Packets();
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, model) => Model(model), (_, _, _) => Bone(), (_, _) => Identity(), (current, _, _, _) => new(current, PointView(new(11, -333, 55))), (current, _) => new(current, PointView(new(-100, -500, -1000), 777)), _ => packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattleActionPacketsState Packets()
    {
        var packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[32 * 20], new byte[49 * 24]);
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 155, 302, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 155, 302, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private static NativeBattleSnipeLaser302Resources Resources() => NativeBattleSnipeLaser302StateTests.Resources();
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattlePackedMatrix32 Bone() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1000, Y = 2000, Z = 3000 }, null);
    private static uint ModelAddress(int actor) => 0x1D972C0u + (uint)(actor * 156);
    private static NativeBattleGraphicsModel Model(uint address)
    {
        int actor = checked((int)((address - 0x1D972C0) / 156));
        return new(actor, 2) { Rotation14 = 0, MinimumY146 = -100, Transform = new(new(0, 0, 0), new(100, 200, 1000), (short)(444 + actor), 777, 4096) };
    }
}
