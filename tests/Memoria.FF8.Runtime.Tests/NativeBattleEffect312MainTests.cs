using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312MainTests
{
    [Fact]
    public void Paused_main_updates_current_bone_anchor_before_returning_without_changing_GTE_age_or_random()
    {
        NativeBattleEffect312Frame frame = Frame(8);
        NativeBattleEffectMemory before = frame.Memory;
        var inputs = Inputs(0x201);
        NativeBattleEffect312Step result = NativeBattleEffect312Controller.AdvanceMain59CC60(frame, ChildBank, Resources(), inputs);
        Assert.Equal(0, result.Result);
        Assert.Equal(110, unchecked((short)result.Frame.State.ReadWord(AnchorAddress)));
        Assert.Equal(220, unchecked((short)result.Frame.State.ReadWord(AnchorAddress + 2)));
        Assert.Equal(330, unchecked((short)result.Frame.State.ReadWord(AnchorAddress + 4)));
        Assert.Equal(110u, result.Frame.State.ReadDword(MatrixAddress + 20));
        Assert.Equal(8, result.Frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Single(result.Frame.Effect.Pools[0].Slots.Where(task => task.Callback != 0));
        Assert.Equal(JsonSerializer.Serialize(before.GeometryRegisters), JsonSerializer.Serialize(result.Frame.Memory.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(before.ScriptedColorRegisters), JsonSerializer.Serialize(result.Frame.Memory.ScriptedColorRegisters));
        Assert.Equal(before.PrimitiveProcess, result.Frame.Memory.PrimitiveProcess);
    }

    [Fact]
    public void Age_eight_births_consume_three_draws_in_order_and_second_spin_uses_first_angle()
    {
        NativeBattleEffect312Frame frame = Frame(8);
        NativeCrtRandomState random = frame.Random.Draw(out ushort firstAngle).Draw(out ushort velocity).Draw(out ushort secondAngle);
        NativeBattleEffect312Step result = NativeBattleEffect312Controller.AdvanceMain59CC60(frame, ChildBank, Resources(), Inputs(0));
        Assert.Equal(random, result.Frame.Random);
        NativeBattleModulePoolTask[] tasks = result.Frame.Effect.Pools[0].Slots.Where(task => task.Callback != 0).ToArray();
        Assert.Equal(new uint[] { MainCallback, 0x59D7B0, 0x59D7B0, 0x59D930, 0x59DA50, 0x59DA50, 0x59DA50, 0x59DA50, 0x59DA50, 0x59DA50 }, tasks.Select(task => task.Callback));
        Assert.Equal(firstAngle % 4096, tasks[1].ReadWord(0x18));
        Assert.Equal(velocity % 40 + 20, tasks[1].ReadWord(0x1A));
        Assert.Equal(secondAngle % 4096, tasks[2].ReadWord(0x18));
        Assert.Equal(unchecked((ushort)-(firstAngle % 4096)), tasks[2].ReadWord(0x1A));
        Assert.Equal(Enumerable.Range(0, 6), tasks.Skip(4).Select(task => (int)task.ReadWord(14)));
    }

    [Fact]
    public void Mandatory_age_eight_allocation_fails_before_an_unallocated_birth_can_be_skipped()
    {
        NativeBattleEffect312Frame frame = Frame(8, capacity: 1);
        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => NativeBattleEffect312Controller.AdvanceMain59CC60(frame, ChildBank, Resources(), Inputs(0)));
        Assert.Contains("selected child allocation", failure.Message);
    }

    private static NativeBattleEffect312Frame Frame(ushort age, ushort capacity = 100)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, capacity);
        (pool, int slot) = pool.Append(MainCallback);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, age)) };
        NativeBattleEffect312State state = NativeBattleEffect312State.FromColdProcess().WriteDword(CasterGlobal, 3).WriteDword(ParametersGlobal, 0x1000);
        NativeBattleOriginalEffectState effect = new(312, 311, new(312, Entry, Initializer), null, null) { Pools = [pool] };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), state);
    }

    private static NativeBattleEffect312Resources Resources()
    {
        byte[] axis = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(axis.AsSpan(8), -4096);
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(CenterOffsetAddress, new byte[6].ToImmutableArray()).Add(AlignmentAddress, axis.ToImmutableArray()));
    }

    private static NativeBattleEffect312Inputs Inputs(uint flags)
    {
        NativeBattlePackedMatrix32 Matrix(int x, int y, int z) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, y, z, 4096), null);
        return new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => flags, _ => throw new InvalidOperationException("Main must use actual physical header/contact fields."), (_, _, _) => throw new InvalidOperationException("These ages do not read parameters."), (_, _) => throw new InvalidOperationException("These ages do not enumerate party rows."), (_, model) =>
        {
            Assert.Equal(0x1D972C0u + 3 * 0x9Cu, model);
            return Matrix(100, 200, 300);
        }, (_, model, bone) =>
        {
            Assert.Equal(0x1D972C0u + 3 * 0x9Cu, model);
            Assert.Equal(10, bone);
            return Matrix(10, 20, 30);
        }, (_, _, _) => throw new InvalidOperationException("These ages do not initialize the center."), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => Matrix(0, 0, 0), _ => new(null, 100, 160, 108), _ => 0, (_, _) => throw new InvalidOperationException("Paused main and age8 must not execute stage/sound/draw operations."));
    }
}
