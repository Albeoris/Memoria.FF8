using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMoogleCameraTests
{
    [Fact]
    public void InitializationImmediatelyAdvancesWithNegativeTruncationAndRetainsUnspecifiedBytes()
    {
        NativeBattleMoogleCameraSource source = Source((0x2A, 4090), (0x2C, 2), (0x36, 100), (0x38, 99), (0x26, -10), (0x28, -11), (0xC, -1), (0x3A, 2048));
        byte[] retained = Enumerable.Repeat((byte)0xA5, 0xEC).ToArray();
        Set(retained, 0xE8, 0);
        NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(new(retained.ToImmutableArray(), true), source, Anchor(), 0);
        Assert.False(step.Complete);
        Assert.Equal(1, Word(step.State, 0xE2));
        Assert.Equal(2048, Word(step.State, 0xD0));
        Assert.Equal(4094, Word(step.State, 0xD8));
        Assert.Equal((short?)100, step.Pose.FocalLength);
        Assert.Equal(-10, Word(step.State, 0xDE));
        Assert.Equal((short)0, step.Pose.Target.X);
        Assert.Equal(0xA5, step.State.Data[0x10]);
        Assert.Equal(0xA5, step.State.Data[0xEA]);
        Assert.Equal(source.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.State.Data.AsSpan().Slice(0xAC)));
        Assert.False(step.State.EyePaddingKnown);
        Assert.All(NativeBattleMoogleCamera.Reset732AF0().Data, value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(100, 50, 120, 120)]
    [InlineData(-100, -50, -120, -120)]
    [InlineData(100, 0, 0, 100)]
    [InlineData(32760, 10, 120, -32766)]
    public void AccelerationClampObservesStoredSignedWordsAndNeverClampsNegativeProgress(short velocity, short acceleration, short clamp, short expected)
    {
        NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), Source((0x3A, velocity), (0x3C, acceleration), (0x3E, clamp)), Anchor(), 0);
        Assert.Equal(expected, Word(step.State, 0xD2));
        Assert.Equal(expected, Word(step.State, 0xD0));
        Assert.False(step.Complete);
    }

    [Fact]
    public void YawSelectorRetentionContinuationAndAnchorWordWrappingUseExactOwners()
    {
        byte[] bytes = NativeBattleMoogleCamera.Reset732AF0().Data.ToArray();
        Set(bytes, 0xDA, 1234);
        Set(bytes, 0xD8, 50);
        Set(bytes, 0xDC, 60);
        Set(bytes, 0xE0, 200);
        Set(bytes, 0xDE, 300);
        NativeBattleMoogleCameraStep held = NativeBattleMoogleCamera.Initialize732B10(new(bytes.ToImmutableArray(), true), Source((0x42, 2), (0x3A, 0)), Anchor(), 1);
        Assert.Equal(1234, Word(held.State, 0xDA));
        Assert.Equal(50, Word(held.State, 0xC2));
        Assert.Equal(60, Word(held.State, 0xCA));
        Assert.Equal(200, Word(held.State, 0xBA));
        Assert.Equal(300, Word(held.State, 0xBE));
        NativeBattleMoogleCameraStep increasing = NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), Source((0x2E, 4090), (0x30, 2), (0x3A, 2048)), Anchor(), 0);
        NativeBattleMoogleCameraStep decreasing = NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), Source((0x2E, 2), (0x30, 4090), (0x42, 1), (0x3A, 2048)), Anchor(), 0);
        Assert.Equal(4094, Word(increasing.State, 0xDA));
        Assert.Equal(4094, Word(decreasing.State, 0xDA));
        NativeBattleMoogleCameraStep wrapped = NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), Source((4, 2), (0xC, 2), (0x3A, 4096)), Anchor(short.MaxValue), 0);
        Assert.Equal(unchecked((short)(short.MaxValue + 2)), wrapped.Pose.Target.X);
    }

    [Fact]
    public void ExplicitBoundedCurveAndIndependentRestorePreserveNextCallback()
    {
        NativeBattleMoogleCameraSource source = Source();
        byte[] row = source.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(row, 0x9000);
        source = source with { Data = row.ToImmutableArray() };
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), source, Anchor(), 0));
        NativeBattleMoogleCameraCurve curve = new(0x9000, [1024, -1, 4096]);
        NativeBattleMoogleCameraStep first = NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), source, Anchor(), 0, curve);
        NativeBattleMoogleCameraState restored = JsonSerializer.Deserialize<NativeBattleMoogleCameraState>(JsonSerializer.Serialize(first.State))!;
        NativeBattleMoogleCameraStep second = NativeBattleMoogleCamera.Advance732E00(first.State, curve);
        Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(NativeBattleMoogleCamera.Advance732E00(restored, curve)));
        Assert.Equal(-1, Word(second.State, 0xD0));
        NativeBattleMoogleCameraStep end = NativeBattleMoogleCamera.Advance732E00(second.State, curve);
        Assert.True(end.Complete);
        Assert.Equal(0x9004u, BinaryPrimitives.ReadUInt32LittleEndian(end.State.Data.AsSpan().Slice(0x50)));
        Assert.True(NativeBattleMoogleCamera.Advance732E00(end.State, curve).Complete);
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogleCamera.Advance732E00(first.State, new(0x9004, [4096])));
    }

    [Fact]
    public void PublicationUsesSaturatedIrAndDoesNotReplacePublishedViewOrGuessEyePadding()
    {
        byte[] bytes = NativeBattleMoogleCamera.Reset732AF0().Data.ToArray();
        Set(bytes, 0x62, 77);
        NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(new(bytes.ToImmutableArray(), true), Source((0x2E, 2048), (0x30, 2048), (0x26, short.MinValue), (0x28, short.MinValue), (0x36, 288), (0x38, 288), (0x3A, 4096)), Anchor(), 0);
        Assert.True(step.Complete);
        Assert.Equal(short.MaxValue, step.Pose.Eye.Z);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize() with { Projection = projection, EyePaddingF6 = 91, TargetPaddingFE = 92 };
        NativeBattleCameraState published = NativeBattleMoogleCamera.Publish733150(step, camera);
        Assert.Same(projection, published.Projection);
        Assert.Same(view, published.Projection!.View);
        Assert.Equal(step.Pose, published.CurrentPose);
        Assert.Null(published.EyePaddingF6);
        Assert.Equal((short)77, published.TargetPaddingFE);
    }

    [Fact]
    public void UnsupportedAnchorAndContinuationSelectorsFailBeforeInventingAnotherCamera()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), Source((0x24, 1)), Anchor(), 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), Source(), Anchor(), 2));
    }

    private static NativeBattleMoogleCameraSource Source(params (int Offset, int Value)[] fields)
    {
        byte[] bytes = new byte[0x48];
        foreach ((int offset, int value) in fields)
        {
            Set(bytes, offset, value);
        }
        return new(0x8000, bytes.ToImmutableArray());
    }
    private static ImmutableArray<byte> Anchor(short x = 0)
    {
        byte[] bytes = new byte[8];
        Set(bytes, 0, x);
        return bytes.ToImmutableArray();
    }
    private static short Word(NativeBattleMoogleCameraState state, int offset) => BinaryPrimitives.ReadInt16LittleEndian(state.Data.AsSpan().Slice(offset));
    private static void Set(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), unchecked((ushort)value));
}
