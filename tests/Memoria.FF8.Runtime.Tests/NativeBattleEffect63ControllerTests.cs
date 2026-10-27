using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect63Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect63ControllerTests
{
    private const uint Arena = 0x20DFAB8;

    [Fact]
    public void First_root_visit_selects_second_packet_bank_and_defers_main_birth()
    {
        Harness harness = new();
        NativeBattleEffect63Step step = AdvanceRoot6E6CB0(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Equal(0, Read(step.Frame, RootBank).ReadByte(14));
        Assert.Equal(1, Read(step.Frame, RootBank).ReadByte(15));
        Assert.Single(step.Frame.Effect.Pools);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Root_retirement_returns_current_common_bytes_and_exclusive_root_owner()
    {
        NativeBattleEffect63Frame frame = Frame(66);
        frame = WriteBytes(frame, Arena + MainOffset + 0x30, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1), 4);
        NativeBattleEffect63Frame result = Advance(frame, null!, new Harness().Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        Assert.Equal(66, BitConverter.ToUInt16(result.State.ReadBank(0).Read(12, 2)));
        (byte[] data, bool[] known) = result.Memory.Arena!.ReadSnapshot(MainOffset + 0x30, 4);
        Assert.Equal(new[] { true, true, false, true }, known);
        Assert.Equal(new byte[] { 1, 2, 4 }, data.Where((_, index) => known[index]).ToArray());
        NativeBattleEffect63Frame restored = JsonSerializer.Deserialize<NativeBattleEffect63Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(known, restored.Memory.Arena!.ReadSnapshot(MainOffset + 0x30, 4).Known);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(16, 0)]
    [InlineData(17, 575)]
    [InlineData(19, 1725)]
    [InlineData(20, 2300)]
    [InlineData(51, 2300)]
    [InlineData(52, 2300)]
    [InlineData(63, 191)]
    [InlineData(64, 0)]
    public void Stage_envelope_preserves_native_ramp_plateau_and_signed_truncation(int age, int strength)
    {
        Assert.Equal(strength, StageStrength(age));
    }

    [Fact]
    public void Terminal_main_still_queries_both_current_bones_and_restores_stage_twice()
    {
        Harness harness = new();
        NativeBattleEffect63Frame frame = AddMain(Frame(65), 64);
        NativeBattleEffect63Step result = AdvanceMain6E6E10(frame, Arena + MainOffset, Resources(), harness.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(64, Read(result.Frame, Arena + MainOffset).ReadWord(12));
        Assert.Equal(new[] { ModelAddress(4), ModelAddress(4) }, harness.PointModels);
        Assert.Equal(2u, result.Frame.State.ReadDword(InitializerGlobal));
        Assert.Equal(new NativeBattleEffect63Operation[] { new NativeBattleEffect63Stage5713E0(0, 0), new NativeBattleEffect63Stage5713E0(0, 0) }, harness.Operations);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Current_zero_xz_basis_keeps_native_division_failure()
    {
        Harness harness = new() { Point = new(100, 20, 300), Target = new(100, 0, 300) };
        NativeBattleEffect63Frame frame = AddMain(Frame(49), 48);
        Assert.Throws<DivideByZeroException>(() => AdvanceMain6E6E10(frame, Arena + MainOffset, Resources(), harness.Inputs()));
        Assert.Equal(2, harness.PointModels.Count);
    }

    [Fact]
    public void Full_optional_sprite_pool_consumes_no_random_draw()
    {
        NativeBattleEffect63Frame frame = Frame(38);
        for (int index = 0; index < SpriteCount; index++)
        {
            (frame, _) = Append(frame, SpriteDescriptor, SpriteCallback);
        }
        NativeBattleEffect63Frame result = BirthSprites(frame, PointView(new(10, 20, 30)));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(SpriteCount, result.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void One_free_sprite_uses_three_draws_before_failed_next_allocation()
    {
        NativeBattleEffect63Frame frame = Frame(38);
        for (int index = 0; index < SpriteCount - 1; index++)
        {
            (frame, _) = Append(frame, SpriteDescriptor, SpriteCallback);
        }
        NativeBattleEffectArena point = PointView(new(-123, 456, -789)).MarkUnknown(6, 2);
        NativeBattleEffect63Frame result = BirthSprites(frame, point);
        Assert.Equal(frame.Random.Draw(out _).Draw(out _).Draw(out _), result.Random);
        NativeBattleModulePoolTask task = Read(result, Arena + (SpriteCount - 1) * SpriteStride);
        Assert.Equal(new NativeBattleVector(-123, 456, -789), Vector(TaskView(task, 12, 8)));
        Assert.Equal(0, task.ReadByte(0x1B));
        Assert.Equal(unchecked((byte)(Signed(task, 0x12) / -12)), task.ReadByte(0x18));
        Assert.Equal(unchecked((byte)(Signed(task, 0x14) / -12)), task.ReadByte(0x19));
        Assert.Equal(unchecked((byte)(Signed(task, 0x16) / -12)), task.ReadByte(0x1A));
    }

    private static NativeBattleEffect63Frame AddMain(NativeBattleEffect63Frame frame, int age)
    {
        (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, main + 12, 0x37F);
        return Write(frame, main, Read(frame, main).WriteWord(12, unchecked((ushort)age)));
    }

    private static NativeBattleEffect63Frame Frame(int age, bool initialized = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[4]).WriteWord(12, unchecked((ushort)age)).WriteByte(14, initialized ? (byte)1 : (byte)0)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            pools = pools.Add(NativeBattleEffectTaskPool.Initialize(MainDescriptor, Arena + MainOffset, MainStride, 1)).Add(NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, Arena, SpriteStride, SpriteCount));
        }
        NativeBattleEffect63State state = NativeBattleEffect63State.FromColdProcess().DetachBank(0).WriteDword(WorkGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(4)).WriteDword(TargetGlobal, ModelAddress(0));
        NativeBattleOriginalEffectState effect = new(63, 62, new(63, Entry, Initializer), new(4, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools), effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect63Resources Resources()
    {
        ImmutableArray<byte> point = PointView(new(0, 0, 0)).Write(6, BitConverter.GetBytes((ushort)1)).Read(0, 8).ToImmutableArray();
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(FirstPointAddress, point).Add(SecondPointAddress, point));
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect63Operation> Operations { get; } = [];
        public List<uint> PointModels { get; } = [];
        public NativeBattleVector Point { get; set; } = new(100, 20, 300);
        public NativeBattleVector Target { get; set; } = new(0, 0, 0);

        public NativeBattleEffect63Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattleActionPacketsState.Fresh(), (_, _) => PutPoint(NativeBattleEffectArena.Clear(), 0x1C, Target), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, retained) => new(frame, PutPoint(retained, 0, default)), (frame, model, local, retained) =>
        {
            PointModels.Add(model);
            Assert.Equal(1, BitConverter.ToUInt16(local.Read(6, 2)));
            Assert.Equal(new[] { false, false }, retained.ReadSnapshot(6, 2).Known);
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, checked(frame.State.ReadDword(InitializerGlobal) + 1)) }, PutPoint(retained, 0, Point));
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
