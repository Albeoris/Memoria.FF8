using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216BoundsTests
{
    [Fact]
    public void BoundsPublishPackedWorldPositionAndOnlyTheOriginalTemporaryWrites()
    {
        (NativeBattlePoseSource source, NativeBattleGraphicsModel model) = Fixture(1);
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(address + 14, [0xA5, 0x5A]);
        NativeBattleRawMesh selected = new([new(new(0, -300, 0), 0), new(new(0, 700, 0), 0)], []);
        NativeBattleRawMesh skipped = new([new(new(0, 123, 0), 0)], []);
        List<short> vertices = [];
        NativeBattleDuel216BoundsStep step = NativeBattleDuel216Bounds.Read695F60(staging, model, source, [selected, skipped], (matrix, vertex) =>
        {
            vertices.Add(vertex.Position.Y);
            Assert.Equal(10, matrix.X);
            Assert.Equal(20, matrix.Y);
            Assert.Equal(30, matrix.Z);
            return vertex.Position.Y;
        });
        Assert.Equal(new short[] { -300, 700 }, vertices);
        Assert.Equal((short)700, step.Maximum);
        Assert.Equal((short)-300, step.Minimum);
        Assert.Equal((short)700, BitConverter.ToInt16(step.Staging.Read(address + 16, 2)));
        Assert.Equal(new byte[] { 0xA5, 0x5A }, step.Staging.Read(address + 14, 2));
        Assert.Throws<NotSupportedException>(() => step.Staging.Read(address + 18, 2));
        Assert.Equal(address, BitConverter.ToUInt32(step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(model.GeometryWorldMatrix! with { X = 10, Y = 20, Z = 30 }, step.Model.GeometryWorldMatrix);
        Assert.Equal(JsonSerializer.Serialize(model.CurrentBodyBoneMatrices), JsonSerializer.Serialize(step.Model.CurrentBodyBoneMatrices));
        NativeBattleDuel216BoundsStep restored = JsonSerializer.Deserialize<NativeBattleDuel216BoundsStep>(JsonSerializer.Serialize(step))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void EmptySelectionKeepsNativeSentinelWordsAndUnwrittenProjectionBytesUnknown()
    {
        (NativeBattlePoseSource source, NativeBattleGraphicsModel model) = Fixture(0);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleDuel216BoundsStep step = NativeBattleDuel216Bounds.Read695F60(staging, model, source, [new([new(new(0, 0, 0), 0)], [])], (_, _) => throw new InvalidOperationException());
        Assert.Equal((short)0, step.Maximum);
        Assert.Equal((short)0, step.Minimum);
        Assert.Equal(65536, BitConverter.ToInt32(step.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
        Assert.Equal(-65536, BitConverter.ToInt32(step.Staging.Read(NativeBattleTemporaryMemory.Address + 4, 4)));
        Assert.Throws<NotSupportedException>(() => step.Staging.Read(NativeBattleTemporaryMemory.Address + 16, 2));
    }

    private static (NativeBattlePoseSource, NativeBattleGraphicsModel) Fixture(uint mask)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleGraphicsModel model = new(0, 3) { Transform = new(new(0, 0, 0), new(10, 20, 30), 0, 0, 4096), GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1000, 2000, 3000, 4096), SequenceState = new() { MeshMask124 = mask, DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024))], false) } };
        return (source, NativeBattleBodyBoneMatrices.Publish508C90(model, source));
    }
}
