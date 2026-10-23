using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48EngineTests
{
    private const uint TaskAddress = 0x26489B8;
    private static readonly NativeBattleMagmaBreath48Resources UnusedResources = null!;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(7, 0)]
    public void Mouth_reads_actual_bone_twice_before_lifecycle_and_preserves_each_publication(int phase, int expectedResult)
    {
        NativeBattleMagmaBreath48Frame frame = Frame(phase);
        List<int> calls = [];
        NativeBattleMagmaBreath48Inputs inputs = Inputs((current, actor, selector, distance) =>
        {
            Assert.Equal(3, actor);
            Assert.Equal(0x15, selector);
            Assert.Equal(0, distance);
            int old = current.State.ReadWord(0x26495C0);
            calls.Add(old);
            current = current with { State = current.State.WriteWord(0x26495C0, checked((ushort)(old + 1))) };
            return new(current, PointView(new(checked((short)(10 + old)), checked((short)(20 + old)), 32760)).MarkUnknown(6, 2));
        });
        var result = AdvanceEngine825E90(frame, TaskAddress, UnusedResources, inputs);
        Assert.Equal(new[] { 0, 1 }, calls);
        Assert.Equal(expectedResult, result.Result);
        NativeBattleModulePoolTask task = Read(result.Frame, TaskAddress);
        Assert.Equal(10u << 16, task.ReadDword(0x228));
        Assert.Equal(20u << 16, task.ReadDword(0x22C));
        Assert.Equal(unchecked((uint)(-32520 << 16)), task.ReadDword(0x230));
        Assert.Equal(11u << 16, task.ReadDword(0x238));
        Assert.Equal(0u, task.ReadDword(0x23C));
        Assert.Equal(unchecked((uint)(-32520 << 16)), task.ReadDword(0x240));
        Assert.Equal(0xDEADBEEFu, task.ReadDword(0x234));
        Assert.Equal(0xABCDEF01u, task.ReadDword(0x244));
        Assert.Equal((ushort)(phase == 0 ? 1 : phase), task.ReadWord(0x54));
        Assert.Equal((ushort)16, result.Frame.State.ReadWord(0x26495D2));
        Assert.Equal((ushort)20, result.Frame.State.ReadWord(0x26495C2));
        Assert.Equal(TaskAddress + 0x34, result.Frame.State.ReadDword(0x26495E8));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Placement_two_requires_prior_stack_owner_before_any_bone_or_count_publication()
    {
        NativeBattleMagmaBreath48Frame frame = Frame(0);
        frame = Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(0x58, 2));
        Assert.Throws<NotSupportedException>(() => AdvanceEngine825E90(frame, TaskAddress, UnusedResources, Inputs((_, _, _, _) => throw new InvalidOperationException())));
    }

    [Fact]
    public void Completed_context_remains_resumable_with_identical_mouth_and_count_stores()
    {
        NativeBattleMagmaBreath48Frame frame = Frame(2);
        NativeBattleMagmaBreath48Frame restored = JsonSerializer.Deserialize<NativeBattleMagmaBreath48Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(AdvanceEngine825E90(frame, TaskAddress, UnusedResources, Inputs()).Frame), JsonSerializer.Serialize(AdvanceEngine825E90(restored, TaskAddress, UnusedResources, Inputs()).Frame));
    }

    internal static NativeBattleMagmaBreath48Frame Frame(int phase)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x26491A8, TaskAddress, 676, 3);
        (pool, _) = pool.Append(0x825410);
        NativeBattleOriginalEffectState effect = new(48, 47, new(48, 0x824CF0, 0x824CD0), null, 0x26480D0) { Pools = [pool] };
        NativeBattleMagmaBreath48State state = NativeBattleMagmaBreath48State.FromColdProcess().WriteWord(0x26495D2, 11).WriteWord(0x26495C2, 13);
        NativeBattleMagmaBreath48Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), state);
        return Write(frame, TaskAddress, Read(frame, TaskAddress).WriteWord(0x54, checked((ushort)phase)).WriteWord(0x52, 3).WriteWord(0x58, 0).WriteWord(0x48, 5).WriteWord(0x4A, 7).WriteDword(0x234, 0xDEADBEEF).WriteDword(0x244, 0xABCDEF01));
    }

    internal static NativeBattleMagmaBreath48Inputs Inputs(Func<NativeBattleMagmaBreath48Frame, int, int, int, NativeBattleMagmaBreath48PointStep>? bone = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, uint.MaxValue, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException("Unreached model"), bone ?? ((current, _, _, _) => new(current, PointView(new(1, 2, 3)))), _ => 0, (_, _) => throw new InvalidOperationException("Unreached packet"));
}
