using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175CaptureGeometryTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
    private static ImmutableArray<NativeBattleHoly175CaptureDirectoryEntry> Directory => [new(1, 0), new(2, 1), new(3, 2), new(4, 3)];

    [Fact]
    public void CaptureSamplesAllCurrentPosesWithoutAdvancingEvenInvisibleGroupThreeOrApplyingStageYaw()
    {
        NativeBattleStageState stage = Stage() with { Rotation = 0x1234, RotationSpeed = 57 };
        stage = stage with { Groups = stage.Groups.Select(group => group with { Flags = 0 }).ToImmutableArray() };
        NativeBattleHoly175CaptureStage capture = Draw(stage, []);
        Assert.Empty(capture.Packets);
        Assert.Equal(stage.Rotation, capture.Stage.Rotation);
        Assert.Equal(stage.RotationSpeed, capture.Stage.RotationSpeed);
        for (int group = 0; group < 4; group++)
        {
            Assert.Equal(stage.Groups[group].Counter, capture.Stage.Groups[group].Counter);
            Assert.Equal(stage.Groups[group].PoseFlags, capture.Stage.Groups[group].PoseFlags);
            Assert.Equal(stage.Groups[group].Animation, capture.Stage.Groups[group].Animation);
            Assert.Equal(View.Rotation.ToArray(), capture.Stage.Groups[group].Matrices[0].Rotation.ToArray());
            Assert.Equal(1000, capture.Stage.Groups[group].Matrices[0].Z);
            Assert.Equal(0, stage.Groups[group].Matrices[0].Z);
        }
    }

    [Fact]
    public void FourActualDirectoryPairsAndCurrentGroupColorsSelectOrderedVisibleGeometry()
    {
        NativeBattleStageState stage = Stage();
        stage = stage with { Groups = stage.Groups.SetItem(0, stage.Groups[0] with { Flags = 1, ColorFlags = 0x00112233 }).SetItem(2, stage.Groups[2] with { Flags = 1, ColorFlags = 0x00010203, ColorDepth = 4096 }) };
        NativeBattleHoly175CaptureStage capture = Draw(stage, [Mesh(0, false), Mesh(1, false), Mesh(2, true), Mesh(3, false)]);
        Assert.Equal(2, capture.Packets.Length);
        Assert.Equal(new[] { 1001, 253 }, capture.Packets.Select(packet => packet.Bucket).ToArray());
        Assert.Equal(0x24112233u, capture.Packets[0].Words[1]);
        Assert.Equal(0x24010203u, capture.Packets[1].Words[1]);
        Assert.Equal((ushort)1263, unchecked((ushort)capture.Packets[0].Words[2]));
        Assert.Equal((short)911, unchecked((short)(capture.Packets[0].Words[2] >> 16)));
        ImmutableArray<NativeBattleHoly175CaptureDirectoryEntry> changed = Directory.SetItem(0, new(25, 3));
        NativeBattleHoly175CaptureStage different = NativeBattleHoly175CaptureGeometry.Draw5D29E0(stage, [Mesh(0, false)], changed, View, new(null, 216, 1, 2), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(150, Assert.Single(different.Packets).Bucket);
        string json = JsonSerializer.Serialize(capture.Stage);
        NativeBattleStageState restored = JsonSerializer.Deserialize<NativeBattleStageState>(json)!;
        Assert.Equal(JsonSerializer.Serialize(Draw(capture.Stage, [Mesh(0, false), Mesh(2, true)])), JsonSerializer.Serialize(Draw(restored, [Mesh(0, false), Mesh(2, true)])));
    }

    [Fact]
    public void MissingInvisibleSourceAndMalformedDirectoryDoNotInventPoseOrDirectoryDefaults()
    {
        NativeBattleStageState stage = Stage();
        stage = stage with { Groups = stage.Groups.SetItem(3, stage.Groups[3] with { Source = null, Matrices = [] }) };
        Assert.Throws<NotSupportedException>(() => Draw(stage, []));
        Assert.Throws<InvalidDataException>(() => NativeBattleHoly175CaptureGeometry.Draw5D29E0(Stage(), [], [new(1, 0)], View, new(null, 216, 0, 0), NativeBattleGteColorInterpolation.CodeConstants45F270));
        Assert.Throws<InvalidDataException>(() => NativeBattleHoly175CaptureGeometry.Draw5D29E0(Stage(), [], Directory.SetItem(0, new(1, 32)), View, new(null, 216, 0, 0), NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleHoly175CaptureStage Draw(NativeBattleStageState stage, ImmutableArray<NativeBattleRawMesh> geometry) => NativeBattleHoly175CaptureGeometry.Draw5D29E0(stage, geometry, Directory, View, new(null, 216, -3, -5), NativeBattleGteColorInterpolation.CodeConstants45F270);

    private static NativeBattleStageState Stage()
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
        NativeBattleStageSource resource = new(16, [source, source, source, source], [], new("declared-stage", [])) { ModelColors = new(0, 0) };
        NativeBattleStageState initialized = NativeBattleStageState.FromColdProcess().Initialize500EA0(resource);
        return initialized with { Groups = initialized.Groups.Select(group => group with { Flags = 0 }).ToImmutableArray() };
    }

    private static NativeBattleRawMesh Mesh(int group, bool colored)
    {
        byte[] polygon = new byte[colored ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        if (colored)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(16), 0x24808080);
        }
        return new([new(new(-10, -10, 0), 0), new(new(10, -10, 0), 0), new(new(-10, 10, 0), 0)], [new(false, polygon.ToImmutableArray())]) { StageGroup = group };
    }
}
