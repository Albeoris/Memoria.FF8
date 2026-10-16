using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageTests
{
    [Fact]
    public void OriginalStageAnimationLoopsAndFreezePreservesInPlaceCameraCompositions()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess().Reset506C50().Initialize500EA0(Source());
        Assert.All(stage.Groups, group => Assert.Equal((byte)1, group.Counter));
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        stage = stage.Step500FD0(camera);
        Assert.Equal(16, stage.Rotation);
        Assert.All(stage.Groups, group => Assert.Equal((byte)2, group.Counter));
        Assert.Equal(10, stage.Groups[0].Matrices[0].X);
        stage = stage.Step500FD0(camera);
        Assert.Equal((byte)3, stage.Groups[0].Counter);
        Assert.Equal(10, stage.Groups[0].Matrices[0].X);
        stage = JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(stage))!;
        stage.Validate();
        stage = stage.Step500FD0(camera);
        Assert.Equal((byte)1, stage.Groups[0].Counter);
        Assert.Equal(10, stage.Groups[0].Matrices[0].X);
        stage = (stage with { Flags = 5 }).Step500FD0(camera);
        Assert.Equal(48, stage.Rotation);
        Assert.Equal((byte)1, stage.Groups[0].Counter);
        Assert.Equal(20, stage.Groups[0].Matrices[0].X);
        Assert.Equal(40, stage.Groups[0].Matrices[0].Y);
        Assert.Equal(60, stage.Groups[0].Matrices[0].Z);
    }

    [Fact]
    public void StageResetPreservesHighFlagsAndColorsAndSignedLowWordControlsRotatingGroup()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.Select(group => group with { Flags = 0xFF, ColorDepth = 999, ColorFlags = 0x12345678, PoseFlags = 0xFF }).ToImmutableArray() };
        stage = stage.Reset506C50().Initialize500EA0(Source());
        Assert.All(stage.Groups, group =>
        {
            Assert.Equal((byte)0xFF, group.Flags);
            Assert.Equal((short)0, group.ColorDepth);
            Assert.Equal(0x12345678u, group.ColorFlags);
            Assert.Equal((byte)4, group.PoseFlags);
        });
        stage = stage with { Rotation = 0x8000, Flags = 1 };
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        stage = stage.Step500FD0(camera);
        Assert.Equal(0x8000, stage.Rotation);
        Assert.Equal(camera.Rotation.ToArray(), stage.Groups[0].Matrices[0].Rotation.ToArray());
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(0, -4096, 0)).ToArray(), stage.Groups[3].Matrices[0].Rotation.ToArray());
    }

    private static NativeBattleStageSource Source()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animations = new byte[17];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 3;
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
        return new(16, [source, source, source, source], [], new("stage:TIM", []));
    }
}
