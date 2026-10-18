using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105CaptureGeometryTests
{
    [Fact]
    public void ExactMultipleWidthStillIncrementsTheNativeQuotientBeforeTranslation()
    {
        NativeBattleThundaga105CaptureTransform capture = Transform(128, 128, 64, 64, 512);
        Assert.Equal(-64, capture.Matrix.X);
        Assert.Equal(64, capture.Matrix.Y);
        Assert.Equal(512, capture.Matrix.Z);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 0, -4096, 0, 0, 0 }, capture.Matrix.Rotation.ToArray());
        Assert.Equal<short?>(64, capture.Projection.OffsetX);
        Assert.Equal<short?>(64, capture.Projection.OffsetY);
        Assert.Equal((short)-3, capture.PriorOffsetX);
        Assert.Equal((short)-5, capture.PriorOffsetY);
        Assert.Equal<ushort?>(512, capture.Projection.FocalLength);
    }

    [Fact]
    public void QuotientRemainderAndThreeDivisionStagesAreNotReplacedByCeilingOrCombinedRatios()
    {
        NativeBattleThundaga105CaptureTransform capture = Transform(16384, 257, 129, -257, 0x8001);
        Assert.Equal(-65, capture.Matrix.X);
        Assert.Equal(-43, capture.Matrix.Y);
        Assert.Equal(-32767, capture.Matrix.Z);
        Assert.Equal((short)32, capture.Matrix.Rotation[0]);
        Assert.Equal(unchecked((short)-(0x1000000 / ((257 << 12) / 128))), capture.Matrix.Rotation[5]);
        string json = JsonSerializer.Serialize(capture);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleThundaga105CaptureTransform>(json)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(127)]
    public void NativeZeroDenominatorsFailInsteadOfInventingMinimumCaptureScale(int width)
    {
        Assert.Throws<DivideByZeroException>(() => Transform(width, 128, 0, 0, 512));
    }

    [Fact]
    public void CaptureSelectsGroupOneAndDoesNotSampleVisibleGroupZero()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.SetItem(0, stage.Groups[0] with { Flags = 1 }) };
        NativeBattleThundaga105CaptureStage capture = NativeBattleThundaga105CaptureGeometry.DrawGroup6D5620(stage, default, Transform(128, 128, 0, 0, 512), null!);
        Assert.Same(stage, capture.Stage);
        Assert.Empty(capture.Packets);
    }

    private static NativeBattleThundaga105CaptureTransform Transform(int width, int depth, short anchorX, short anchorZ, ushort focal)
    {
        byte[] anchor = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(anchor, anchorX);
        BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(4), anchorZ);
        return NativeBattleThundaga105CaptureGeometry.Transform6D5620(anchor.ToImmutableArray(), new(new(0, 0, 0), new(checked((short)width), 0, checked((short)depth))), new(null, focal, -3, -5));
    }
}
