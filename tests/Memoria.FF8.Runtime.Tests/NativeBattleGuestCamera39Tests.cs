using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuestCamera39Tests
{
    [Fact]
    public void CaptureRetainsPhysicalMatrixPaddingAndOverwritesTranslationWithSignedCurrentNodeWords()
    {
        byte[] matrix = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x5E39);
        frame = frame with { Core = frame.Core.Write(0x2797968, matrix).MarkUnknown(0x279797A, 2).MarkUnknown(0x279797C, 12) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 0x8000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 0x7FFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 0xFFFF);
        NativeBattleSharedCustomMatrix captured = NativeBattleGuestCamera39.CaptureMode5(frame);
        captured.Validate();
        Assert.Equal(matrix.AsSpan(0, 18).ToArray(), captured.Bytes.AsSpan(0, 18).ToArray());
        Assert.False(captured.Known[18]);
        Assert.False(captured.Known[19]);
        Assert.All(captured.Known.Skip(20), value => Assert.True(value));
        Assert.Equal(-32768, BinaryPrimitives.ReadInt32LittleEndian(captured.Bytes.AsSpan().Slice(20)));
        Assert.Equal(32767, BinaryPrimitives.ReadInt32LittleEndian(captured.Bytes.AsSpan().Slice(24)));
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(captured.Bytes.AsSpan().Slice(28)));
        NativeBattleActionMatrix read = captured.ReadMatrix50D790();
        Assert.Equal(-32768, read.X);
        Assert.Equal(32767, read.Y);
        Assert.Equal(-1, read.Z);
        Assert.Equal((short)4096, read.Scale);
        string json = JsonSerializer.Serialize(captured);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleSharedCustomMatrix>(json)));
    }

    [Fact]
    public void UnknownRotationIsCopiedWithoutInventingKnowledgeAndFailsOnlyWhenTheConsumerReadsIt()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x5039);
        frame = frame with { Core = frame.Core.MarkUnknown(0x2797968, 32) };
        NativeBattleSharedCustomMatrix captured = NativeBattleGuestCamera39.CaptureMode5(frame);
        Assert.All(captured.Known.Take(20), value => Assert.False(value));
        Assert.All(captured.Known.Skip(20), value => Assert.True(value));
        Assert.Throws<NotSupportedException>(() => captured.ReadMatrix50D790());
        Assert.Throws<NotSupportedException>(() => NativeBattleGuestCamera39.CaptureMode5(frame.WriteTemporaryWord(0x14A, 0x6039)));
    }
}
