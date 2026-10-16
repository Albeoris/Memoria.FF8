using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeVibrationQueueTests
{
    private static string GameDirectory => TestResources.GameDirectory;
    private static NativeVibrationBank Bank() => NativeVibrationBankReader.ReadExecutable(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));

    private static NativeVibrationProcessState Callbacks(NativeVibrationProcessState state, int count)
    {
        for (int callback = 0; callback < count; callback++)
        {
            state = state.Advance(4);
        }
        return state;
    }

    [Fact]
    public void CallbackCadenceRejectsUnsupportedTicksAndRemaindersBeforeAdvancing()
    {
        NativeVibrationProcessState state = NativeVibrationProcessState.FromColdProcess().Enqueue(Bank(), 6, 1).State;
        string original = JsonSerializer.Serialize(state);
        foreach (int ticks in new[] { -1, int.MinValue, 5, int.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(ticks));
            Assert.Equal(original, JsonSerializer.Serialize(state));
        }
        foreach (int remainder in new[] { -1, 4, int.MaxValue })
        {
            NativeVibrationProcessState malformed = state with { QuarterClockRemainder = remainder };
            Assert.Throws<InvalidDataException>(() => malformed.Validate());
            Assert.Throws<InvalidDataException>(() => malformed.Advance(1));
        }
        foreach (int ticks in new[] { 0, 1, 2, 3, 4 })
        {
            NativeVibrationProcessState advanced = state.Advance(ticks);
            Assert.Equal(ticks % 4, advanced.QuarterClockRemainder);
            Assert.Equal((short)(ticks == 4 ? 0 : -1), advanced.Slots[0].A.Counter);
            Assert.Equal((byte)(ticks == 4 ? 128 : 0), advanced.Ports[0].RequestedA);
            Assert.Equal(original, JsonSerializer.Serialize(state));
        }
        Assert.Equal(JsonSerializer.Serialize(state.Advance(4)), JsonSerializer.Serialize(state.Advance(1).Advance(1).Advance(2)));
        Assert.Equal(JsonSerializer.Serialize(state.Advance(4)), JsonSerializer.Serialize(state.Advance(3).Advance(1)));
    }

    [Fact]
    public void OriginalEmbeddedPatternsAndColdAllocationHaveExactNativeWords()
    {
        NativeVibrationBank bank = Bank();
        Assert.Equal(64, bank.Offsets.Length);
        Assert.Equal(new int[] { 256, 270, 308, 332, 350, 370, 394 }, bank.Offsets.Take(7));
        Assert.All(bank.Offsets.Skip(7), value => Assert.Equal(0, value));
        Assert.Equal("1A01EBCDDE88CA7FE6A1511AD6F77C540B7ADDA57313CBD2A1A4A7E318E1CAC1", Convert.ToHexString(SHA256.HashData(bank.Bytes.AsSpan(0, 256))));
        Assert.Equal(new byte[] { 0x33, 2, 0x33, 0x1B, 0, 0xFF }, bank.Bytes.Skip(260).Take(6));
        NativeVibrationProcessState state = NativeVibrationProcessState.FromColdProcess();
        Assert.Equal(-1, state.CarHandleC76788);
        Assert.Equal(0, state.QuarterClockRemainder);
        Assert.Equal(0, state.LastSetReturn);
        Assert.All(state.Slots, slot =>
        {
            Assert.Equal((ushort)1, slot.Generation);
            Assert.Equal((byte)0, slot.Priority);
            Assert.Empty(slot.A.Bytes);
            Assert.Equal((short)0, slot.A.Counter);
        });
        using WorldAssets world = new(GameDirectory);
        world.VibrationBank.Validate();
        Assert.True(world.VibrationBank.Offsets.Length > 1 && world.VibrationBank.Offsets[1] != 0);
    }

    [Fact]
    public void NativePatternZeroHasQuarterClockInterpolationSentinelAndPersistentGeneration()
    {
        NativeVibrationProcessState cold = NativeVibrationProcessState.FromColdProcess();
        NativeVibrationEnqueue issued = cold.Enqueue(Bank(), 0, 1);
        Assert.Equal(32, issued.Handle);
        Assert.Equal((short)-1, issued.State.Slots[0].A.Counter);
        Assert.False(cold.IsPlaying(32));
        Assert.True(issued.State.IsPlaying(32));
        NativeVibrationProcessState half = issued.State.Advance(2);
        Assert.Equal((short)-1, half.Slots[0].A.Counter);
        Assert.Equal(2, half.QuarterClockRemainder);
        NativeVibrationProcessState sampled = half.Advance(2);
        Assert.Equal((byte)102, sampled.Ports[0].RequestedA);
        Assert.Equal((byte)102, sampled.MarkControllerPending(0).Ports[0].RequestedA);
        Assert.Throws<ArgumentOutOfRangeException>(() => sampled.MarkControllerPending(2));
        Assert.Equal((short)0, sampled.Slots[0].A.Counter);
        sampled = Callbacks(sampled, 4);
        Assert.Equal(2, sampled.Slots[0].A.Cursor);
        Assert.Equal((byte)98, sampled.Ports[0].RequestedA);
        sampled = Callbacks(sampled, 32);
        Assert.Equal((byte)0, sampled.Slots[0].Priority);
        Assert.Equal((byte)1, sampled.Slots[0].A.Active);
        Assert.Equal((byte)0, sampled.Ports[0].RequestedA);
        Assert.Equal((ushort)2, sampled.Slots[0].Generation);
        Assert.False(sampled.IsPlaying(32));
        NativeVibrationProcessState entered = issued.State.Advance(2).EndFieldCallback();
        Assert.Equal(2, entered.QuarterClockRemainder);
        Assert.Equal((short)-1, entered.Slots[0].A.Counter);
        Assert.Equal((ushort)2, entered.Slots[0].Generation);
        Assert.Equal((byte)0, entered.Slots[0].Priority);
        Assert.All(entered.Ports, port => Assert.True(port.Pending && port.RequestedA == 0 && port.RequestedB == 0));
    }

    [Fact]
    public void ReachedPatternSixProducesOriginalTwoMotorSamplesAndEndsWithoutClearingChannels()
    {
        NativeVibrationProcessState state = NativeVibrationProcessState.FromColdProcess().Enqueue(Bank(), 6, 1).State;
        Assert.Equal((ushort)6, state.Slots[0].A.Length);
        Assert.Equal((ushort)8, state.Slots[0].B.Length);
        state = state.Advance(4);
        Assert.Equal((byte)128, state.Ports[0].RequestedA);
        Assert.Equal((byte)128, state.Ports[0].RequestedB);
        state = Callbacks(state, 3);
        Assert.Equal((byte)128, state.Ports[0].RequestedA);
        Assert.Equal((byte)0, state.Ports[0].RequestedB);
        Assert.Equal(4, state.Slots[0].B.Cursor);
        state = state.Advance(4);
        Assert.Equal((byte)128, state.Ports[0].RequestedA);
        Assert.Equal((byte)0, state.Ports[0].RequestedB);
        state = Callbacks(state, 40);
        Assert.Equal((byte)0, state.Slots[0].Priority);
        Assert.Equal((byte)1, state.Slots[0].A.Active);
        Assert.Equal((byte)1, state.Slots[0].B.Active);
    }

    [Fact]
    public void QueueSelectionPreservesSignedPriorityFullDwordAndNativeIndexComparison()
    {
        NativeVibrationBank bank = Bank();
        NativeVibrationProcessState state = NativeVibrationProcessState.FromColdProcess();
        foreach (int priority in new[] { 5, 1, 3, 2 })
        {
            state = state.Enqueue(bank, 6, priority).State;
        }
        //EDI becomes0 after slot0, so it does not select lower-priority slot1 afterward.
        NativeVibrationEnqueue replaced = state.Enqueue(bank, 6, 5);
        Assert.Equal(48, replaced.Handle);
        Assert.Equal((ushort)3, replaced.State.Slots[0].Generation);
        Assert.Equal((ushort)2, replaced.State.Slots[1].Generation);
        Assert.Equal(0, state.Enqueue(bank, int.MinValue, -256).Handle);
        NativeVibrationProcessState signed = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Priority = 255 }) };
        Assert.Equal(48, signed.Enqueue(bank, 0, -1).Handle);
        NativeVibrationEnqueue wrappedPriority = NativeVibrationProcessState.FromColdProcess().Enqueue(bank, 6, 257);
        Assert.Equal((byte)1, wrappedPriority.State.Slots[0].Priority);
        Assert.Equal(-1, NativeVibrationProcessState.FromColdProcess().Enqueue(bank, 63, 1).Handle);
        Assert.Throws<NotSupportedException>(() => NativeVibrationProcessState.FromColdProcess().Enqueue(bank, 64, 1));
    }

    [Fact]
    public void GenerationWordWrapAndEmptyChannelPreservePhysicalRetainedWords()
    {
        NativeVibrationBank bank = Bank();
        NativeVibrationProcessState state = NativeVibrationProcessState.FromColdProcess().Enqueue(bank, 0, 1).State;
        NativeVibrationSlot old = state.Slots[0];
        state = state.StopAll() with { Slots = state.Slots.SetItem(0, old with { Priority = 0, Generation = ushort.MaxValue }) };
        NativeVibrationEnqueue wrapped = state.Enqueue(bank, 0, 1);
        Assert.Equal(0, wrapped.Handle);
        Assert.Equal((ushort)0, wrapped.State.Slots[0].Generation);
        state = state with { Slots = state.Slots.SetItem(0, old with { Priority = 0, Generation = 1023 }) };
        Assert.Equal(16, state.Enqueue(bank, 0, 1).Handle);
        //Declared4A29A0 zero-channel branch, not an original resource or research save.
        NativeVibrationBank empty = new(new string('A', 64), ImmutableArray.Create(4), ImmutableArray.Create<byte>(4, 0, 0, 0, 0, 0, 0, 0));
        NativeVibrationChannel retained = state.Slots[0].A;
        NativeVibrationEnqueue cleared = state.Enqueue(empty, 0, 1);
        Assert.Equal(retained with { Active = 0 }, cleared.State.Slots[0].A);
    }

    [Fact]
    public void ActualCarBankUsesPreviousGraphicsScalarAndRestoresIndependentNextCallback()
    {
        using WorldAssets world = new(GameDirectory);
        NativeVibrationProcessState state = NativeVibrationProcessState.FromColdProcess();
        Assert.Equal(state, state.SampleCarStart(33, world.VibrationBank));
        NativeVibrationProcessState moving = state with { GraphicsSpeed20409F0 = 7 };
        NativeVibrationProcessState queued = moving.SampleCarStart(33, world.VibrationBank);
        Assert.Equal(32, queued.CarHandleC76788);
        Assert.Equal((byte)255, queued.Slots[0].Priority);
        Assert.Equal(7, queued.PreviousCarSpeed2045C84);
        Assert.Equal(queued, queued.SampleCarStart(33, world.VibrationBank));
        Assert.Equal(moving, moving.SampleCarStart(0, world.VibrationBank));
        string serialized = JsonSerializer.Serialize(queued.Advance(3));
        NativeVibrationProcessState restored = JsonSerializer.Deserialize<NativeVibrationProcessState>(serialized)!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(queued.Advance(4)), JsonSerializer.Serialize(restored.Advance(1)));
        JsonObject missing = JsonNode.Parse(serialized)!.AsObject();
        missing.Remove(nameof(state.Slots));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeVibrationProcessState>(missing.ToJsonString()));
    }
}
