using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260RootTests
{
    [Fact]
    public void Entry_queues_executed_TIM_and_clears_only_root_DWORDC_before_any_actor_service()
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Frame source = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [], ReturnedDescriptor = null, ResourcePointers = [new(InitializerGlobal, TimOffset)] } };
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260Controller.Initialize(source, fixture.Resources, fixture.Inputs);
        Assert.Equal(1, result.Memory.Textures!.Count);
        Assert.Equal(TimOffset, result.Memory.Textures.Slots[0]!.ArenaOffset);
        Assert.Equal(ArenaAddress, result.State.ReadDword(WorkingGlobal));
        Assert.Equal(0x1D99A78u, result.State.ReadDword(ParametersGlobal));
        Assert.Equal(0x1D972C0u, result.State.ReadDword(CasterGlobal));
        NativeBattleEffectTaskPool root = Assert.Single(result.Effect.Pools);
        Assert.Equal((ushort)16, root.Stride);
        Assert.Equal(0u, root.Slots[0].ReadDword(0xC));
        Assert.Equal(RootCallback, root.Slots[0].Callback);
        Assert.Empty(fixture.Operations);
        Assert.Empty(fixture.Anchors);
    }

    [Theory]
    [InlineData(0, SecondPacketOffset, SecondPacketOffset + PacketBankBytes, 1)]
    [InlineData(1, FirstPacketOffset, SecondPacketOffset, 0)]
    public void Old_BYTEE_selects_native_dual_cursor_bank_before_initialization(int old, int primary, int secondary, int next)
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Frame frame = fixture.SetRoot(fixture.Frame, 0, (byte)old, 0);
        NativeBattleShockwavePulsar260Frame result = fixture.Step(frame);
        Assert.Equal(ArenaAddress + (uint)primary, result.State.ReadDword(PrimaryCursorGlobal));
        Assert.Equal(ArenaAddress + (uint)secondary, result.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal((byte)next, result.Effect.Pools[0].Slots[0].ReadByte(0xE));
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(0xC));
        Assert.Single(result.Effect.Pools);
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Reached_first_view_observes_exact_main_clear_saved_flags_heads_only_particles_and_target_records()
    {
        Fixture fixture = new();
        fixture.CaptureBeforeMain = true;
        Assert.Throws<ProbeException>(() => fixture.Step(fixture.SetRoot(fixture.Frame, 1, 0, 0)));
        NativeBattleShockwavePulsar260Frame captured = Assert.IsType<NativeBattleShockwavePulsar260Frame>(fixture.Captured);
        Assert.Equal(new ushort[] { 16, MainStride, ChildStride }, captured.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { RootBank, ArenaAddress + MainOffset, ArenaAddress + ChildrenOffset }, captured.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new[] { 3, 4 }, fixture.Anchors);
        Assert.Collection(fixture.Operations, operation => Assert.Equal(new NativeBattleShockwavePulsar260BindCaster(0, ArenaAddress + MainOffset + 0x178, 0x111B3CC), operation), operation => Assert.Equal(new NativeBattleShockwavePulsar260RingSequence(0, 1), operation));
        Assert.Equal((ushort)2, captured.Effect.Pools[1].Slots[0].ReadWord(0xE));
        uint record = ArenaAddress + MainOffset + 0x10;
        Assert.Equal(0x1D972C0u + 3 * 156u, NativeBattleShockwavePulsar260Controller.ReadDword(captured, record));
        Assert.Equal((short)103, Word(NativeBattleShockwavePulsar260Controller.ReadView(captured, record + 4, 8), 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleShockwavePulsar260Controller.ReadView(captured, record + 4, 8).Read(6, 2));
        Assert.Equal((short)11, Word(NativeBattleShockwavePulsar260Controller.ReadView(captured, record + 0xC, 8), 0));
        Assert.Equal((short)-22, Word(NativeBattleShockwavePulsar260Controller.ReadView(captured, record + 0xC, 8), 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleShockwavePulsar260Controller.ReadView(captured, record + 0xC, 8).Read(6, 2));
        Assert.Equal(0x800000u / 4608u, NativeBattleShockwavePulsar260Controller.ReadDword(captured, record + 0x18));
        Assert.Equal(0u, NativeBattleShockwavePulsar260Controller.ReadDword(captured, record + 0x1C));
        Assert.Equal(1u, NativeBattleShockwavePulsar260Controller.ReadDword(captured, record + 0x14));
        Assert.Equal((short)35, unchecked((short)captured.State.ReadWord(0x25086A0)));
        Assert.Equal((short)-70, unchecked((short)captured.State.ReadWord(0x25086A2)));
        Assert.Equal((short)105, unchecked((short)captured.State.ReadWord(0x25086A4)));
        for (int actor = 0; actor < 7; actor++)
        {
            Assert.Equal((uint)(actor + 2), captured.State.ReadDword(SavedFlagsAddress + (uint)actor * 4));
        }
        Assert.Equal((byte)0xA5, captured.Memory.Arena!.Read(PuffsOffset, 1)[0]);
        Assert.Equal((byte)0, captured.Memory.Arena.Read(PuffsOffset + 15, 1)[0]);
        Assert.Equal(new byte[] { 0, 0, 0xA5 }, captured.Memory.Arena.Read(SecondaryParticlesOffset, 3));
        Assert.Equal(0, captured.Memory.EffectIo!.BaseOffset);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, captured.Effect.Pools[2].Slots[0].ReadBytes(0xC, 2));
        Assert.Throws<NotSupportedException>(() => captured.Memory.Arena.Read(ChildrenOffset + 12, 1));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, captured.Effect.Pools[1].Slots[0].ReadBytes(0x1294, 4));
    }

    [Fact]
    public void Formation796_uses_only_first_original_contact_and_no_other_target_anchor()
    {
        Fixture fixture = new(796) { CaptureBeforeMain = true };
        Assert.Throws<ProbeException>(() => fixture.Step(fixture.SetRoot(fixture.Frame, 1, 0, 0)));
        Assert.Equal(new[] { 3 }, fixture.Anchors);
        Assert.Equal((ushort)1, fixture.Captured!.Effect.Pools[1].Slots[0].ReadWord(0xE));
        Assert.Equal((short)30, unchecked((short)fixture.Captured.State.ReadWord(0x25086A0)));
    }

    [Fact]
    public void Root_resets_current_factors_then_applies_native_changed_stage_thresholds_only()
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x25086A8, 9000).WriteDword(0x25086B0, 8000).WriteDword(0x25086AC, 4096).WriteDword(0x25086B4, 4097) };
        NativeBattleShockwavePulsar260Frame result = fixture.Step(frame);
        Assert.Collection(fixture.Operations,
            operation => Assert.Equal(new NativeBattleShockwavePulsar260StageWrite(7, 0, 0), operation),
            operation => Assert.Equal(new NativeBattleShockwavePulsar260StageFlags(7, 0, 2), operation),
            operation => Assert.Equal(new NativeBattleShockwavePulsar260StageWrite(8, 0, 0), operation),
            operation => Assert.Equal(new NativeBattleShockwavePulsar260StageFlags(8, 0, 2), operation));
        Assert.Equal(0u, result.State.ReadDword(0x25086AC));
        Assert.Equal(0u, result.State.ReadDword(0x25086B4));
        Assert.NotEqual(0u, result.Effect.ReturnedDescriptor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Copied_view_retains_destination_padding_and_root_retires_from_main_only_after_advancing_live_child(bool knownPadding)
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Frame frame = fixture.StartedWithChild(fixture.Frame);
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
        if (knownPadding)
        {
            view = view.Write(18, [0xAB, 0xCD]);
        }
        frame = frame with { State = frame.State.WriteView(CopiedViewAddress, view, 32) };
        NativeBattleShockwavePulsar260Frame result = fixture.Step(frame);
        NativeBattleEffectArena copied = result.State.ReadView(CopiedViewAddress, 32);
        Assert.Equal((uint)123, Dword(copied, 20));
        Assert.Equal(unchecked((uint)-456), Dword(copied, 24));
        Assert.Equal((uint)789, Dword(copied, 28));
        if (knownPadding)
        {
            Assert.Equal(new byte[] { 0xAB, 0xCD }, copied.Read(18, 2));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => copied.Read(18, 2));
        }
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.True((result.Effect.Pools[2].Slots[0].Flags & 1) != 0);
        Assert.Equal(1u, result.Effect.Pools[2].Slots[0].ReadDword(0x18));
        Assert.Equal((ushort)2, result.Effect.Pools[0].Slots[0].ReadWord(0xC));
        Assert.Equal(new NativeBattleShockwavePulsar260StageWrite(15, 0, 0), fixture.Operations.Last());
    }

    [Fact]
    public void Root_parity_and_retained_scalar_JSON_next_are_exact_without_replaying_entry()
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Frame restored = JsonSerializer.Deserialize<NativeBattleShockwavePulsar260Frame>(JsonSerializer.Serialize(fixture.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(fixture.Step(fixture.Frame)), JsonSerializer.Serialize(new Fixture().Step(restored)));
    }

    [Fact]
    public void Camera_interpolation_publishes_target_then_eye_GPF_GPL_without_touching_geometry_or_source_W()
    {
        Fixture fixture = new();
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xA5000000, Rgb = [0x11111111, 0x22222222, 0x33333333], NormalInput = new byte[8].ToImmutableArray(), NormalInputKnown = [true, true, true, true, true, true, false, false] };
        NativeBattleShockwavePulsar260Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(0x25086A0, 100).WriteWord(0x25086A4, 20000), Memory = fixture.Frame.Memory with { ScriptedColorRegisters = colors } };
        string beforeGeometry = JsonSerializer.Serialize(frame.Memory.GeometryRegisters);
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260Controller.AdvanceMainCamera(frame, 148, fixture.Inputs);
        NativeBattleCameraPose pose = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260CameraPose>()).Pose;
        Assert.Equal(2013u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new uint?[] { 0xA5FF0000, 0xA5FF0041, 0xA5FF00FF }, result.Memory.ScriptedColorRegisters.Rgb.ToArray());
        Assert.Equal(new int?[] { pose.Eye.X, pose.Eye.Y, pose.Eye.Z }, result.Memory.ScriptedColorRegisters.Ir.ToArray());
        Assert.Equal(new int?[] { pose.Eye.X, pose.Eye.Y, pose.Eye.Z }, result.Memory.ScriptedColorRegisters.Mac.ToArray());
        Assert.Equal(0x00380000u, result.Memory.ScriptedColorRegisters.Flags);
        Assert.Equal(0xA5000000u, result.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(beforeGeometry, JsonSerializer.Serialize(result.Memory.GeometryRegisters));
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal((short)321, pose.Roll);
        Assert.Equal((short)444, pose.FocalLength);
    }

    [Theory]
    [InlineData(20, 2463, -4674, -5697, -318, -2185, 313)]
    [InlineData(58, 418, 1741, -8000, -102, -8385, -2438)]
    [InlineData(59, -361, -13641, -8304, -365, -16445, -538)]
    [InlineData(79, -6357, -5670, -4421, -29, 2999, 291)]
    [InlineData(118, 27, -1073, -4566, -204, -13, 2255)]
    [InlineData(119, 27, -1073, -4566, -204, -13, 2255)]
    [InlineData(178, 8894, -1218, 2368, -205, -14, 2256)]
    [InlineData(213, 4455, -8171, 8301, 702, 99, -459)]
    public void Camera_reached_boundaries_preserve_literal_points_and_unwritten_roll(int age, int ex, int ey, int ez, int tx, int ty, int tz)
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Controller.AdvanceMainCamera(fixture.Frame, age, fixture.Inputs);
        NativeBattleCameraPose pose = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260CameraPose>()).Pose;
        Assert.Equal(new NativeBattleCameraPoint((short)ex, (short)ey, (short)ez), pose.Eye);
        Assert.Equal(new NativeBattleCameraPoint((short)tx, (short)ty, (short)tz), pose.Target);
        Assert.Equal((short)321, pose.Roll);
        Assert.Equal((short)444, pose.FocalLength);
        if (age is 79 or 118)
        {
            Assert.Equal(new NativeBattleShockwavePulsar260CameraFocal(200), fixture.Operations[0]);
        }
        else if (age == 213)
        {
            Assert.Equal(new NativeBattleShockwavePulsar260CameraFocal(288), fixture.Operations[0]);
        }
        else
        {
            Assert.DoesNotContain(fixture.Operations, operation => operation is NativeBattleShockwavePulsar260CameraFocal);
        }
    }

    [Fact]
    public void Camera_age0_is_only_roll_reset_and_nontransition_age_does_not_require_camera_owner()
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Controller.AdvanceMainCamera(fixture.Frame, 0, fixture.Inputs);
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleShockwavePulsar260CameraControl>(operation), operation => Assert.Equal(new NativeBattleShockwavePulsar260CameraFocal(288), operation), operation => Assert.Equal((short)0, Assert.IsType<NativeBattleShockwavePulsar260CameraPose>(operation).Pose.Roll));
        fixture.Operations.Clear();
        NativeBattleShockwavePulsar260Inputs unknown = fixture.Inputs with { ReadCamera = _ => throw new InvalidOperationException() };
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260Controller.AdvanceMainCamera(fixture.Frame, 19, unknown);
        Assert.Same(fixture.Frame, result);
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Camera_JSON_next_keeps_unknown_material_FIFO_and_signed_interpolation_owner()
    {
        Fixture fixture = new();
        NativeBattleShockwavePulsar260Frame first = NativeBattleShockwavePulsar260Controller.AdvanceMainCamera(fixture.Frame, 147, fixture.Inputs);
        NativeBattleShockwavePulsar260Frame restored = JsonSerializer.Deserialize<NativeBattleShockwavePulsar260Frame>(JsonSerializer.Serialize(first))!;
        NativeBattleShockwavePulsar260Frame Next(NativeBattleShockwavePulsar260Frame current) => NativeBattleShockwavePulsar260Controller.AdvanceMainCamera(current, 148, new Fixture().Inputs);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
        Assert.All(first.Memory.ScriptedColorRegisters!.Rgb, value => Assert.Null(value));
    }

    private const int TimOffset = 0x30000;
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private sealed class ProbeException : Exception
    {
    }

    private sealed class Fixture
    {
        public NativeBattleShockwavePulsar260Frame Frame { get; }
        public NativeBattleShockwavePulsar260Resources Resources { get; }
        public NativeBattleShockwavePulsar260Inputs Inputs { get; }
        public List<NativeBattleShockwavePulsar260Operation> Operations { get; } = [];
        public List<int> Anchors { get; } = [];
        public bool CaptureBeforeMain { get; set; }
        public NativeBattleShockwavePulsar260Frame? Captured { get; private set; }

        public Fixture(ushort formation = 368)
        {
            Resources = MakeResources();
            byte[] tim = new byte[32];
            BinaryPrimitives.WriteInt32LittleEndian(tim, 0x10);
            BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 12);
            BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(20), 12);
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 0x2E50).ToArray()).Write(TimOffset, tim);
            NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
            (roots, int slot) = roots.Append(RootCallback);
            roots = roots with { Slots = roots.Slots.SetItem(slot, roots.Slots[slot].WriteDword(0xC, 0)) };
            NativeBattleOriginalEffectState effect = new(260, 259, new(260, Entry, Initializer), new(0, 0, 0x18, 0, 0, 0), RootDescriptor) { Pools = [roots] };
            NativeBattleEffectArena euler = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)11)).Write(2, BitConverter.GetBytes((short)-22)).Write(4, BitConverter.GetBytes((short)33));
            NativeBattleShockwavePulsar260State state = NativeBattleShockwavePulsar260State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D972C0).WriteView(0x2508690, euler, 8);
            Frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, effect, new(123), state);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            byte[] header = new byte[20];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), 0x1D28344);
            header[16] = 2;
            packets = packets with { Headers = packets.Headers.SetItem(0, new(header.ToImmutableArray(), 0, 0, null)), ContactCount = 2, Contacts = packets.Contacts.SetItem(0, new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0))).SetItem(1, new(new(4, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0))) };
            Inputs = new(packets, formation, actor => new(actor, (ushort)(actor + 2)) { Transform = new(default, default, 0, 0, 4096) }, actor => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)(100 + actor))).Write(2, BitConverter.GetBytes((short)(200 + actor))).Write(4, BitConverter.GetBytes((short)(300 + actor))), _ => null, current =>
            {
                if (CaptureBeforeMain)
                {
                    Captured = current;
                    throw new ProbeException();
                }
                return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 123, Y = -456, Z = 789 }, 0x1234);
            }, _ => NativeBattleCameraState.FromColdProcess() with { Roll = 321, CurrentPose = new(new(1, 2, 3), new(4, 5, 6), 444, 321) }, (current, actor) =>
            {
                Anchors.Add(actor);
                return new(current, new((short)(actor * 10), (short)(actor * -20), (short)(actor * 30), 77));
            }, (_, _) => throw new InvalidOperationException(), (current, operation) =>
            {
                Operations.Add(operation);
                return current;
            });
        }

        public NativeBattleShockwavePulsar260Frame Step(NativeBattleShockwavePulsar260Frame frame) => NativeBattleShockwavePulsar260Controller.Advance(frame, Resources, Inputs);
        public NativeBattleShockwavePulsar260Frame SetRoot(NativeBattleShockwavePulsar260Frame frame, ushort age, byte parity, byte started) => NativeBattleShockwavePulsar260Controller.Write(frame, RootBank, NativeBattleShockwavePulsar260Controller.Read(frame, RootBank).WriteWord(0xC, age).WriteByte(0xE, parity).WriteByte(0xF, started));

        public NativeBattleShockwavePulsar260Frame StartedWithChild(NativeBattleShockwavePulsar260Frame frame)
        {
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + MainOffset, MainStride, 1);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ArenaAddress + ChildrenOffset, ChildStride, ChildCount);
            (children, int slot) = children.Append(ChildActCallback);
            children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(0xC, 10).WriteWord(0xE, 20).WriteWord(0x10, 30)) };
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, children] } };
            frame = SetRoot(frame, 2, 0, 1);
            return NativeBattleShockwavePulsar260Controller.InitializeAct(frame, ArenaAddress + ChildrenOffset, 0x14, 0xD8, 0x111D424, Resources);
        }

        private static NativeBattleShockwavePulsar260Resources MakeResources()
        {
            uint[] addresses = [0x111D424, 0x111DEBC, 0x111F09C, 0x112015C, 0x112A974];
            int[] lengths = [2712, 4576, 4288, 43032, 10268];
            ImmutableArray<NativeBattleGfAnimationSource>.Builder acts = ImmutableArray.CreateBuilder<NativeBattleGfAnimationSource>();
            for (int index = 0; index < addresses.Length; index++)
            {
                byte[] bytes = new byte[lengths[index]];
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 50);
                acts.Add(new(addresses[index], bytes.ToImmutableArray()));
            }
            return new(new("FF8_EN:111B3CC", new byte[0x1B74].ToImmutableArray()), acts.ToImmutable(), [new(0, 0x111CF4C, 0, 0, new byte[428].ToImmutableArray()), new(1, 0x111D0F8, 0, 0, new byte[532].ToImmutableArray())], new(0, 0x111D30C, new byte[280].ToImmutableArray()), Enumerable.Repeat((byte)'0', 16).ToImmutableArray(), Enumerable.Repeat((byte)'0', 15).ToImmutableArray(), [new(0x111CF40, 1), new(0x111CF44, 2), new(0x111CF48, 3)], [new(360, "ma8def_0.tim", new byte[33824].ToImmutableArray()), new(361, "ma8def_1.tim", new byte[33824].ToImmutableArray())]);
        }
    }
}
