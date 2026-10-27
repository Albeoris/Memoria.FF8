using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect338Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect338Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect338ControllerTests
{
    [Theory]
    [InlineData(128, 253)]
    [InlineData(159, 252)]
    [InlineData(-159, 3)]
    [InlineData(-160, 4)]
    public void Impact_acceleration_retains_native_signed_high_product_and_two_arithmetic_shifts(short velocity, byte expected)
    {
        Assert.Equal(expected, Acceleration6ECAF3(velocity));
    }

    [Fact]
    public void Fragment_yaw_scale_uses_negative_angle_and_signed_WORD_scale()
    {
        NativeBattleActionMatrix matrix = FragmentScale6ED1E0(1024, 2048);
        Assert.Equal(new short[] { 0, 0, 2048, 0, 2048, 0, -2048, 0, 0 }, matrix.Rotation);
        Assert.Equal(new short[] { 0, 0, -2048, 0, -2048, 0, 2048, 0, 0 }, FragmentScale6ED1E0(1024, -2048).Rotation);
    }

    [Fact]
    public void Old72_draws_and_advances_before_stopping_retained_vibration_and_preserves_CRT_after_JSON()
    {
        Fixture fixture = new(72);
        NativeBattleEffect338Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect338Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Frame)))!);
        var result = AdvanceMain(restored, fixture.Main, fixture.Resources, fixture.Inputs);
        Assert.Equal(0, result.Result);
        Assert.Equal((ushort)73, Read(result.Frame, fixture.Main).ReadWord(12));
        Assert.Equal(1024, Assert.IsType<NativeBattleEffect338Stage5713E0>(fixture.Operations[0]).Intensity);
        Assert.Equal(17, Assert.IsType<NativeBattleEffect338StopVibration4A2940>(fixture.Operations[1]).Handle);
        Assert.Equal(restored.Random, result.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleActionMatrix light = result.Frame.Memory.CaptureEffectLight1D99AB0().ReadMatrix();
        Assert.Equal(100, light.X);
        Assert.Equal(200, light.Y);
        Assert.Equal(300, light.Z);
    }

    [Fact]
    public void Last_main_callback_enqueues_exact_terminal_file_at_first_packet_bank_after_age_increment()
    {
        Fixture fixture = new(75);
        var result = AdvanceMain(fixture.Frame, fixture.Main, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)76, Read(result.Frame, fixture.Main).ReadWord(12));
        Assert.Equal(256, Assert.IsType<NativeBattleEffect338Stage5713E0>(fixture.Operations[0]).Intensity);
        NativeBattleEffect338QueueFile5341D0 queued = Assert.IsType<NativeBattleEffect338QueueFile5341D0>(fixture.Operations[1]);
        Assert.Equal((short)362, queued.FileId);
        Assert.Equal(ArenaAddress + 0x5CD8, queued.DestinationAddress);
        Assert.Equal((short)0, queued.Type);
        Assert.Equal(0u, result.Frame.State.ReadDword(0x255623C));
    }

    private sealed class Fixture
    {
        public NativeBattleEffect338Frame Frame { get; }
        public NativeBattleEffect338Resources Resources { get; }
        public NativeBattleEffect338Inputs Inputs { get; }
        public uint Main { get; }
        public List<NativeBattleEffect338Operation> Operations { get; } = [];

        public Fixture(ushort age)
        {
            ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
            (NativeBattleEffectTaskPool mainPool, int slot) = pools[1].Append(MainCallback);
            Main = mainPool.BankAddress + (uint)(slot * mainPool.Stride);
            NativeBattleModulePoolTask main = mainPool.Slots[slot].WriteWord(12, age).WriteDword(16, 17).WriteDword(0x78, 100).WriteDword(0x7C, 200).WriteDword(0x80, 300);
            mainPool = mainPool.WriteSlot(slot, main);
            pools = pools.SetItem(1, mainPool);
            NativeBattleOriginalEffectState effect = new(338, 338, new(338, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools };
            NativeBattleEffect338State state = NativeBattleEffect338State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress).WriteDword(TargetGlobal, 0x1D972C0 + 3 * 156).WriteDword(0x25561D8, ArenaAddress) with { RootBankStorage = null };
            Frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Resources = new(null!, [], new("unused-camera", [1]), new(new string('0', 64), [0], [0, 0, 0, 0]), 0, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, [], new(362, "unused.tim", [1]));
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            }, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        }
    }
}
