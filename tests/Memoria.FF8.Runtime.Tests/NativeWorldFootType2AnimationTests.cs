using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldFootType2AnimationTests
{
    [Theory]
    [InlineData(0, 0, true, -1, 16, 1, 0)]
    [InlineData(1, 304, true, 4, 320, 1, 0)]
    [InlineData(1, 16, false, 4, 320, 0, 0)]
    [InlineData(3, 224, false, -1, 240, 2, 0)]
    [InlineData(4, 208, false, -1, 224, 0, 0)]
    [InlineData(2, 32, true, -1, 480, 1, 0)]
    [InlineData(2, 32, false, 0, 480, 0, 0)]
    public void MovingStoppingForcedAndTerminalBranchesPreserveNativeOrdering(int animation, int counter, bool moving, int forced, int length, int expectedAnimation, int expectedCounter)
    {
        NativeWorldForestState forest = NativeWorldForestState.Create(7, -5);
        NativeWorldFootType2AnimationResult result = NativeWorldFootType2Animation.Step(animation, (ushort)counter, length, moving, forced, forest, new byte[256]);
        Assert.Equal((expectedAnimation, (ushort)expectedCounter, expectedCounter), (result.Animation, result.Counter, result.SharedCounter));
        Assert.Same(forest, result.Forest);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 0)]
    [InlineData(255, 0)]
    public void IdleZeroDrawsExactlyOneByteAndUsesLowByteMask(byte value, int expectedAnimation)
    {
        byte[] random = new byte[256];
        random[1] = value;
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0);
        NativeWorldFootType2AnimationResult result = NativeWorldFootType2Animation.Step(0, 0, 16, false, -1, forest, random);
        Assert.Equal(expectedAnimation, result.Animation);
        Assert.Equal((byte)1, result.Forest.RandomCursor);
        Assert.Equal((byte)0, forest.RandomCursor);
    }

    [Theory]
    [InlineData(64, 4)]
    [InlineData(65, 2)]
    [InlineData(255, 2)]
    public void IdleTwoCompletionUsesStrictUnsignedThresholdAndRetainedSharedRng(byte value, int expectedAnimation)
    {
        byte[] random = new byte[256];
        random[0] = unchecked((byte)(value + 12));
        NativeWorldForestState forest = NativeWorldForestState.Create(255, 0);
        NativeWorldFootType2AnimationResult result = NativeWorldFootType2Animation.Step(2, 464, 480, false, -1, forest, random);
        Assert.Equal((expectedAnimation, (ushort)0), (result.Animation, result.Counter));
        Assert.Equal(((byte)0, (byte)12), (result.Forest.RandomCursor, result.Forest.RandomOffset));
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(result.Forest))!;
        NativeWorldFootType2AnimationResult next = NativeWorldFootType2Animation.Step(result.Animation, result.Counter, 480, false, -1, result.Forest, random);
        NativeWorldFootType2AnimationResult cold = NativeWorldFootType2Animation.Step(result.Animation, result.Counter, 480, false, -1, restored, random);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold));
    }

    [Fact]
    public void ForcedSentinelUsesType2IdleEntryAndMissingProducerRefuses()
    {
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0);
        NativeWorldFootType2AnimationResult result = NativeWorldFootType2Animation.Step(0, 0, 16, false, 128, forest, new byte[256]);
        Assert.Equal(3, result.Animation);
        Assert.Same(forest, result.Forest);
        Assert.Throws<NotSupportedException>(() => NativeWorldFootType2Animation.Step(0, 0, 16, false, null, forest, new byte[256]));
        Assert.Throws<NotSupportedException>(() => NativeWorldFootType2Animation.Step(2, 0, 480, false, 128, forest, new byte[256]));
        Assert.Throws<InvalidDataException>(() => NativeWorldFootType2Animation.Step(5, 0, 16, false, -1, forest, new byte[256]));
    }
}
