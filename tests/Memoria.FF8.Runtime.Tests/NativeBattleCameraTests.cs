using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraTests
{
    [Fact]
    public void OriginalTrackDurationAdvancesBySixteenAndClearsOnlyItsChannelOnTheNextPoll()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(Bank(2), 3) with { Mask = 0x8008 };
        state = state.Tick(0);
        Assert.Equal((ushort)16, state.Tasks[0]!.Elapsed);
        Assert.Equal((ushort)32, state.Tasks[0]!.Duration);
        state = state.Tick(0);
        Assert.Equal((ushort)0x8008, state.Mask);
        state = state.Tick(0);
        Assert.Equal((ushort)0x8000, state.Mask);
        Assert.All(state.Tasks, task => Assert.Null(task));
        Assert.Empty(state.Order);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x100u)]
    public void GraphicsPauseLoadsTheSegmentButFreezesElapsedAndRestoresExactly(uint flags)
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(Bank(2), 0).Tick(flags);
        Assert.Equal((ushort)0, state.Tasks[0]!.Elapsed);
        Assert.Equal((ushort)32, state.Tasks[0]!.Duration);
        string json = JsonSerializer.Serialize(state);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(json)!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(state.Tick(0)), JsonSerializer.Serialize(restored.Tick(0)));
    }

    [Fact]
    public void NativeMaskIsWrittenByCallbacksRatherThanInferredFromOtherTasks()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize().Start(Bank(1), 2).Start(Bank(2), 2);
        state = state.Tick(0).Tick(0);
        Assert.Equal((ushort)0, state.Mask);
        Assert.Null(state.Tasks[0]);
        Assert.NotNull(state.Tasks[1]);
        state = state.Start(Bank(1), 7);
        Assert.Equal(new[] { 1, 0 }, state.Order);
        Assert.Same(state, state.Start(Bank(1), 0));
    }

    [Fact]
    public void MissingGroupsDoNothingAndMalformedDirectoriesFail()
    {
        NativeBattleCameraState state = NativeBattleCameraState.Initialize();
        Assert.Same(state, state.Start(Bank(2), 0x10));
        Assert.Throws<InvalidDataException>(() => state.Start(new NativeBattleCameraBank([1, 0]), 0));
        state = state.Start(Bank(2), 0);
        Assert.Throws<InvalidDataException>(() => (state with { Order = [0, 0] }).Validate());
    }

    private static NativeBattleCameraBank Bank(ushort frames)
    {
        ushort[] words = [1, 4, 8, 8, 8, 8, 8, 8, 8, 8, 0, frames, 0xF1, 1, 2, 3, 0xF1, 4, 5, 6, 0xFFFF, 0xFFFF];
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return new(bytes.ToImmutableArray());
    }
}
