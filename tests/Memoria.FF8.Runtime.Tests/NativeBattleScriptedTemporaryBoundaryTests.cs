using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedTemporaryBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Whole_callback_publishes_temporary_only_on_success_while_nested_segment_history_is_explicit(bool fail)
    {
        NativeBattleWaterExecutionFrame source = NativeBattleIfritCallbackTests.Create();
        NativeBattleWaterTemporaryFrame before = NativeBattleStorageSnapshots.Capture(source.TemporaryFrame);
        NativeBattleWaterTemporaryFrame? accepted = null;
        NativeBattleScriptedCallbackServices services = NativeBattleIfritCallbackTests.Services() with
        {
            DrawSlots = (current, _) =>
            {
                using NativeBattleWaterFrameBatch batch = new();
                current = batch.Begin(current);
                current.WriteTemporaryWord(0xE0, 0x1111);
                current = batch.Publish(current);
                accepted = NativeBattleStorageSnapshots.Capture(current.TemporaryFrame);
                current = batch.Begin(current);
                current.WriteTemporaryWord(0xE0, 0x2222);
                if (fail)
                {
                    throw new InvalidOperationException("Later unpublished callback segment failed.");
                }
                return batch.Publish(current);
            }
        };
        if (fail)
        {
            Assert.Throws<InvalidOperationException>(() => NativeBattleScriptedCallback.Run(source, NativeBattleIfritCallbackTests.Input(1), services));
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source.TemporaryFrame)));
        }
        else
        {
            NativeBattleWaterCallbackResult result = NativeBattleScriptedCallback.Run(source, NativeBattleIfritCallbackTests.Input(1), services);
            Assert.Same(source.TemporaryFrame, result.Frame.TemporaryFrame);
            Assert.Equal((ushort)0x2222, source.ReadTemporaryWord(0xE0));
        }
        Assert.NotNull(accepted);
        Assert.Equal((ushort)0x1111, accepted.ReadUInt16(0xE0));
    }
}
