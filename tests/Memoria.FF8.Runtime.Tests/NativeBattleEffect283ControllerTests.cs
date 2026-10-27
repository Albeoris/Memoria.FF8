using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect283Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect283ControllerTests
{
    [Fact]
    public void Entry_defers_contact_resolution_and_preserves_warm_COMMON()
    {
        Harness harness = new();
        NativeBattleEffect283Frame before = Inactive();
        NativeBattleEffect283Frame frame = Initialize(before, Resources(), harness.Inputs());
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(Model(3), frame.State.ReadDword(CasterGlobal));
        Assert.Equal(new byte[] { 0xA5 }, frame.Memory.Arena!.Read(0, 1));
        Assert.Equal(0, harness.ContactReads);
        Assert.Empty(harness.Operations);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
    }

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    [InlineData(0x80000000u, FirstPacketOffset, 0u)]
    public void Root_selects_packet_bank_from_entire_old_parity_DWORD(uint parity, int offset, uint next)
    {
        Harness harness = new();
        NativeBattleEffect283Frame frame = Initialize(Inactive(), Resources(), harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, parity));
        frame = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(ArenaAddress + (uint)offset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(0, harness.ContactReads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Main_birth_uses_current_first_contact_and_signed_target_capture(int target)
    {
        Harness harness = new() { Target = target };
        NativeBattleEffect283Frame frame = Initialize(Inactive(), Resources(), harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        frame = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        NativeBattleModulePoolTask main = Read(frame, ArenaAddress);
        Assert.Equal(Model(target), main.ReadDword(0x28));
        Assert.Equal(0xA1B2C3D4u, main.ReadDword(0x14));
        Assert.Equal(unchecked((uint)-123), main.ReadDword(0x10));
        Assert.Equal((ushort)100, main.ReadWord(0x18));
        Assert.Equal((ushort)777, main.ReadWord(0x1A));
        Assert.Equal((ushort)300, main.ReadWord(0x1C));
        Assert.Equal((ushort)0, main.ReadWord(0x1E));
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.Equal((ushort)2, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(new NativeBattleEffect283ModelFlags(Model(target), 0, 4), harness.Operations[0]);
        Assert.Equal(new byte[] { 0x5A }, frame.Memory.Arena!.Read(ScratchOffset, 1));
        Assert.Equal(1, harness.ContactReads);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(7, 1925, true)]
    [InlineData(8, 2200, false)]
    [InlineData(51, 2200, false)]
    [InlineData(52, 2200, true)]
    [InlineData(59, 275, true)]
    public void Stage_uses_task_pointer_alpha_and_zeroes_flags_in_both_ramps(int age, int value, bool zero)
    {
        Harness harness = new();
        NativeBattleEffect283Frame frame = Active((ushort)age);
        NativeBattleEffect283Frame actual = AdvanceMain(frame, ArenaAddress, Resources(), harness.Inputs()).Frame;
        NativeBattleEffect283Stage5713E0 stage = Assert.Single(harness.Operations.OfType<NativeBattleEffect283Stage5713E0>());
        Assert.Equal(value, stage.Value);
        if (zero)
        {
            Assert.Equal(0u, stage.Flags);
        }
        else
        {
            Assert.Equal(ArenaAddress & 0xFF000000, stage.Flags & 0xFF000000);
        }
        Assert.NotEqual(frame.Memory.ScriptedColorRegisters, actual.Memory.ScriptedColorRegisters);
        Assert.Equal((ushort)(age + 1), Read(actual, ArenaAddress).ReadWord(12));
    }

    [Theory]
    [InlineData(24, 20)]
    [InlineData(43, 1)]
    public void Fade_projects_full_anchor_and_threads_actual_body_selector(int age, int bank)
    {
        Harness harness = new();
        NativeBattleEffect283Frame frame = Active((ushort)age);
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(0xE, 0xFFFE));
        frame = AdvanceMain(frame, ArenaAddress, Resources(), harness.Inputs()).Frame;
        Assert.Equal(new[] { (Model(6), (short)-2, bank) }, harness.BodyCalls);
        Assert.Equal((ushort)91, Read(frame, ArenaAddress).ReadWord(0xE));
        NativeBattleEffect283ModelColor44 color = Assert.Single(harness.Operations.OfType<NativeBattleEffect283ModelColor44>());
        Assert.Equal(0x32000000u, color.Value & 0xFF000000);
        Assert.Equal(0xA1B2C3D4u, Read(frame, ArenaAddress).ReadDword(0x14));
    }

    [Fact]
    public void Restore_at44_is_full_DWORD_and_retirement_retains_exact_root_age()
    {
        Harness harness = new();
        NativeBattleEffect283Frame frame = AdvanceMain(Active(44), ArenaAddress, Resources(), harness.Inputs()).Frame;
        Assert.Equal(new NativeBattleEffect283ModelFlags(Model(6), 4, 0), harness.Operations.OfType<NativeBattleEffect283ModelFlags>().Single());
        Assert.Equal(new NativeBattleEffect283ModelColor44(Model(6), 0xA1B2C3D4), harness.Operations.OfType<NativeBattleEffect283ModelColor44>().Single());
        Assert.Empty(harness.BodyCalls);
        frame = Active(59);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 60).WriteByte(15, 1));
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal((ushort)60, BitConverter.ToUInt16(frame.State.RootBankStorage!.Read(12, 2)));
        Assert.Equal(new NativeBattleEffect283Stage5713E0(0, 0), harness.Operations[^1]);
        NativeBattleEffect283Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect283Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Advance(restored, Resources(), harness.Inputs()))));
    }

    [Fact]
    public void Main_runs_exact_programme_counts_with_packed_depth_window_and_saved_scale()
    {
        Harness harness = new();
        NativeBattleEffect283Frame frame = Active(0);
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(0x18, 101).WriteWord(0x1A, 777).WriteWord(0x1C, 303).WriteDword(0x10, unchecked((uint)-123)));
        int result = 0;
        for (int age = 0; age < 60; age++)
        {
            (frame, result) = AdvanceMain(frame, ArenaAddress, Resources(), harness.Inputs());
            Assert.Equal(age == 59 ? 2 : 0, result);
        }
        Assert.Equal(60 * 17, harness.Programmes.Count(item => item.Source == MainProgrammeAddress));
        Assert.Equal(36 * 4, harness.Programmes.Count(item => item.Source == SecondaryProgrammeAddress));
        NativeBattleEffectArena[] main = harness.Programmes.Where(item => item.Source == MainProgrammeAddress).Select(item => item.Caller).ToArray();
        Assert.Equal((short)101, BitConverter.ToInt16(main[0].Read(0, 2)));
        Assert.Equal((short)777, BitConverter.ToInt16(main[0].Read(2, 2)));
        Assert.Equal((short)303, BitConverter.ToInt16(main[0].Read(4, 2)));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(main[0].Read(6, 2)));
        Assert.Equal((ushort)(15 << 3), BitConverter.ToUInt16(main[15 * 17].Read(26, 2)));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(main[16 * 17].Read(26, 2)));
        foreach ((uint source, NativeBattleEffectArena caller) in harness.Programmes)
        {
            Assert.Equal(-123, BitConverter.ToInt32(caller.Read(8, 4)));
            Assert.Equal(-123, BitConverter.ToInt32(caller.Read(12, 4)));
            Assert.Equal(-123, BitConverter.ToInt32(caller.Read(16, 4)));
            Assert.Equal(1, BitConverter.ToInt32(caller.Read(20, 4)));
            Assert.Equal((short)-512, BitConverter.ToInt16(caller.Read(24, 2)));
            Assert.Equal(ArenaAddress + ScratchOffset, BitConverter.ToUInt32(caller.Read(28, 4)));
            if (source == SecondaryProgrammeAddress)
            {
                Assert.Equal((ushort)0, BitConverter.ToUInt16(caller.Read(26, 2)));
                Assert.Equal((short)300, BitConverter.ToInt16(caller.Read(4, 2)));
            }
        }
        Assert.Equal(20, harness.BodyCalls.Count);
        Assert.Single(harness.Operations.OfType<NativeBattleEffect283Sound501330>());
    }

    private static NativeBattleEffect283Resources Resources() => NativeBattleEffect283StateTests.Resources();

    private static NativeBattleEffect283Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0, [0xA5]).Write(ScratchOffset, [0x5A]), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleOriginalEffectState effect = new(283, 282, new(283, Entry, Initializer), new(3, 0, 0x7F, 0, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, NativeBattleEffect283State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect283Frame Active(ushort age)
    {
        NativeBattleEffect283Frame frame = Inactive();
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        (NativeBattleEffectTaskPool root, _) = pools[0].Append(RootCallback);
        (NativeBattleEffectTaskPool main, _) = pools[1].Append(MainCallback);
        frame = frame with { Effect = frame.Effect with { Pools = pools.SetItem(0, root).SetItem(1, main), ReturnedDescriptor = RootDescriptor }, State = frame.State.WriteDword(ArenaGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset) with { RootBankStorage = null } };
        frame = ClearDwords701200(frame, ArenaAddress + 12, 0x18F);
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(12, age).WriteDword(0x10, 4096).WriteDword(0x14, 0xA1B2C3D4).WriteDword(0x28, Model(6)).WriteWord(0x24, 300));
        foreach ((NativeBattleGfAnimationSource source, int offset, int length) in new[] { (Resources().MainProgramme, MainProgrammeWorkspaceOffset, MainProgrammeWorkspaceLength), (Resources().SecondaryProgramme, SecondaryProgrammeWorkspaceOffset, SecondaryProgrammeWorkspaceLength) })
        {
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[length].ToImmutableArray()).Initialize7016B0(source, length);
            frame = WriteBytes(frame, ArenaAddress + (uint)offset, NativeBattleEffectArena.Clear().Write(0, NativeBattleStorageSnapshots.Capture(workspace).Data.AsSpan()), length);
        }
        return frame;
    }

    private static uint Model(int index) => 0x1D972C0u + (uint)(156 * index);

    private sealed class Harness
    {
        public int Target { get; init; } = 6;
        public int ContactReads { get; private set; }
        public List<NativeBattleEffect283Operation> Operations { get; } = [];
        public List<(uint, short, int)> BodyCalls { get; } = [];
        public List<(uint Source, NativeBattleEffectArena Caller)> Programmes { get; } = [];

        public NativeBattleEffect283Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, address) => new((int)((address - Model(0)) / 156), 2) { Transform = new(new(0, 0, 0), new(100, 200, 300), 999, 777, -123), Color44 = 0xA1B2C3D4 }, currentFrame =>
        {
            ContactReads++;
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(3, 8, 12, -1, 261, 283, null));
            (packets, _) = packets.AppendContact(new(new((byte)Target, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.AppendContact(new(new(4, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(2);
            return packets;
        }, (frame, _) => new(frame, PointView(new(100, 200, 300), 777)), (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        }, (frame, model, x, y, selector, bank, fade) =>
        {
            BodyCalls.Add((model, selector, bank));
            return new(frame, 91);
        }, (frame, source, node, caller) =>
        {
            Programmes.Add((source.Address, caller));
            return frame;
        });
    }
}
