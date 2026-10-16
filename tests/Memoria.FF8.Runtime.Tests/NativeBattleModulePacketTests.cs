using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModulePacketTests
{
    [Fact]
    public void ModuleProjectionRoundsTheRotationBeforeAddingTranslationAndClampsNativeDepthAndEighthPixelScreen()
    {
        NativeBattleActionMatrix matrix = Identity with { Rotation = [2048, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 1 };
        NativeBattleModuleProjectedVertex vertex = NativeBattleModuleProjection.Project45FE10(matrix, new(1, 0, 0), Projection);
        Assert.Equal(1282u | 864u << 16, vertex.Screen);
        Assert.Equal((ushort)1024, vertex.Depth);
        Assert.Equal(0u, vertex.Flags);
        NativeBattleModuleProjectedVertex near = NativeBattleModuleProjection.Project45FE10(Identity with { X = 10000, Z = -1 }, new(0, 0, 0), Projection);
        Assert.Equal((ushort)0, near.Depth);
        Assert.Equal((ushort)8184, unchecked((ushort)near.Screen));
        Assert.Equal(0x60000u, near.Flags & 0x60000);
        Assert.NotEqual(0u, near.Flags & 0x4000);
        NativeBattleProjectedVertex packed = near;
        Assert.Equal((ushort)0, packed.Depth);
        NativeBattleModuleProjectedVertex far = NativeBattleModuleProjection.Project45FE10(Identity with { Z = 65536 }, new(0, 0, 0), Projection);
        Assert.Equal(ushort.MaxValue, far.Depth);
        Assert.Equal(0x40000u, far.Flags & 0x60000);
    }

    [Fact]
    public void Ft4UsesZsf4SumDepthAndPreservesBothUvsInTheOriginalPackedWord()
    {
        byte[] face = Face(0x2C804020);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x01420605);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x56781234);
        NativeBattleGeometryPrimitive primitive = NativeBattleModulePackets.EmitQuad(face, Corners, true, 0x2F0, 0x3867, 2048, 17, 2)!;
        Assert.Equal(81, primitive.Bucket);
        Assert.Equal(new uint[] { 0x9000000, 0x2C402010, Screen(100, 100), 0x38670201, Screen(200, 100), 0x01420605, Screen(100, 200), 0x56781234, Screen(200, 200), 0x5678 }, primitive.Words.ToArray());
        Assert.Equal(new[] { 1024, 1024, 1024, 1024 }, primitive.VertexPayload4.ToArray());
    }

    [Fact]
    public void G4UsesItsOwnSemiTransparencyBitsAndOriginalColorFifoCommandByteDuringFade()
    {
        byte[] face = Face(0x30804020);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x01020304);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x05112233);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x07445566);
        NativeBattleGeometryPrimitive primitive = NativeBattleModulePackets.EmitQuad(face, Corners, false, 0xF3, 0, 2048, 17, 2)!;
        Assert.Equal(0x32402010u, primitive.Words[1]);
        Assert.Equal(0x07010102u, primitive.Words[3]);
        Assert.Equal(0x07081119u, primitive.Words[5]);
        Assert.Equal(0x07222A33u, primitive.Words[7]);
        Assert.Equal(9, primitive.Words.Length);
        NativeBattleGeometryPrimitive forcedOpaque = NativeBattleModulePackets.EmitQuad(face, Corners, false, 0xA, 0, 0, 17, 2)!;
        Assert.Equal(0x30804020u, forcedOpaque.Words[1]);
    }

    [Fact]
    public void MeshCullingChecksOnlyTheFirstThreeNearFlagsAndKeepsNativeUnsignedScreenRejectionAndZeroArea()
    {
        byte[] face = Face(0x2C808080);
        NativeBattleProjectedVertex[] back = [Corners[0], Corners[2], Corners[1], Corners[3]];
        Assert.Null(NativeBattleModulePackets.EmitQuad(face, back, true, 0, 0, 0, 0, 2));
        Assert.NotNull(NativeBattleModulePackets.EmitQuad(face, back, true, 0x10, 0, 0, 0, 2));
        Assert.Null(NativeBattleModulePackets.EmitQuad(face, back, false, 0x10, 0, 0, 0, 2));
        Assert.NotNull(NativeBattleModulePackets.EmitQuad(face, back, false, 0x20, 0, 0, 0, 2));
        NativeBattleProjectedVertex[] nearFourth = Corners.ToArray();
        nearFourth[3] = nearFourth[3] with { DepthAndClip = 0x20000 | 64 };
        Assert.NotNull(NativeBattleModulePackets.EmitQuad(face, nearFourth, true, 0, 0, 0, 0, 2));
        nearFourth[0] = nearFourth[0] with { DepthAndClip = 0x20000 | 64 };
        Assert.Null(NativeBattleModulePackets.EmitQuad(face, nearFourth, true, 0, 0, 0, 0, 2));
        NativeBattleProjectedVertex[] outside = Corners.Select(vertex => vertex with { Screen = Screen(-8, 100) }).ToArray();
        Assert.Null(NativeBattleModulePackets.EmitQuad(face, outside, true, 0x10, 0, 0, 0, 2));
        NativeBattleProjectedVertex[] zeroArea = Corners.Select(vertex => vertex with { Screen = Screen(100, 100) }).ToArray();
        Assert.NotNull(NativeBattleModulePackets.EmitQuad(face, zeroArea, true, 0, 0, 0, 0, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SpritePacketPreservesSignedPaletteModulationUvClampAndExtendedAllocationInsertionOrder(int extension)
    {
        byte[] quad = new byte[20];
        quad[0] = 8;
        quad[1] = 128;
        quad[2] = 4;
        quad[3] = 0x2E;
        quad[4] = 250;
        quad[5] = 254;
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(6), 0x3800);
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(8), -4);
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(10), -2);
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(14), 0x1142);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Anchor = new(0, 0, 0, 0),
            Scale = 4096,
            SpriteFlags = (ushort)(extension == 2 ? 0x10C : 0xC),
            SpriteColor = 0x4080FF,
            Resource = new(0, 1, 0, 0, [0, 0, 0, 0, 0, 254, 0, 0]),
            SpriteFrame = new(0, extension == 1, quad.ToImmutableArray())
        };
        NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, Projection, 0, 17, 2);
        Assert.Equal(extension == 0 ? 40 : 52, batch.AllocationBytes);
        NativeBattleAllocatedPrimitive sprite = batch.Primitives[^1];
        Assert.Equal(0, sprite.ByteOffset);
        Assert.Equal((ushort)0, sprite.Flags20);
        Assert.Equal(81, sprite.Primitive.Bucket);
        Assert.Equal(0x2E4080FFu, sprite.Primitive.Words[1]);
        Assert.Equal(0x3780FEFAu, sprite.Primitive.Words[3]);
        Assert.Equal(0x0142FEFFu, sprite.Primitive.Words[5]);
        Assert.Equal(0xFFFAu, sprite.Primitive.Words[7]);
        Assert.Equal(Screen(1152, 800), sprite.Primitive.Words[2]);
        Assert.Equal(new[] { 1024, 1024, 1024, 1024 }, sprite.Primitive.VertexPayload4.ToArray());
        if (extension != 0)
        {
            Assert.Equal(2, batch.Primitives.Length);
            NativeBattleAllocatedPrimitive mode = batch.Primitives[0];
            Assert.Equal(40, mode.ByteOffset);
            Assert.Equal(new uint[] { 0x1000000, 0xE1000220 }, mode.Primitive.Words.ToArray());
            Assert.Equal(new int[4], mode.Primitive.VertexPayload4.ToArray());
            byte[] second = quad.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(second.AsSpan(10), 8);
            NativeBattleModuleDraw ground = draw with { SpriteWorldRotation = NativeBattlePoseMatrices.RotationWords(new(1024, 0, 0)), SpriteFrame = draw.SpriteFrame with { Quads = quad.Concat(second).ToImmutableArray() } };
            NativeBattleModulePacketBatch multiple = NativeBattleModulePackets.EmitSprite571DC0(ground, Projection, 0, 17, 2);
            Assert.Equal(92, multiple.AllocationBytes);
            Assert.Equal(new[] { 40, 0, 52 }, multiple.Primitives.Select(primitive => primitive.ByteOffset).ToArray());
            Assert.Equal(multiple.Primitives[1].Primitive.Bucket, multiple.Primitives[2].Primitive.Bucket);
            Assert.NotEqual(multiple.Primitives[1].Primitive.VertexPayload4.Sum(), multiple.Primitives[2].Primitive.VertexPayload4.Sum());
        }
        string json = JsonSerializer.Serialize(batch);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleModulePacketBatch>(json)));
    }

    private static byte[] Face(uint command)
    {
        byte[] face = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, command);
        return face;
    }

    private static uint Screen(int x, int y) => unchecked((ushort)x) | unchecked((uint)y << 16);
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096);
    private static NativeBattleCameraProjection Projection => new(Identity, 256, 160, 108);
    private static NativeBattleProjectedVertex[] Corners => [new(Screen(100, 100), 1024), new(Screen(200, 100), 1024), new(Screen(100, 200), 1024), new(Screen(200, 200), 1024)];
}
