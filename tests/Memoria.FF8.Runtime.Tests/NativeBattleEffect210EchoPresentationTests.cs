using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210EchoPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_authored_echo_face_identity_survives_prior_clipping_and_preserves_native_ledger(bool rejectFirst)
    {
        byte[] original = Faces();
        byte[] faces = new byte[52];
        original.AsSpan(0, 16).CopyTo(faces);
        BinaryPrimitives.WriteUInt16LittleEndian(faces, rejectFirst ? (ushort)4 : (ushort)0);
        original.CopyTo(faces, 16);
        List<int> plainReads = [];
        List<int> capturedReads = [];
        NativeBattleDuelEchoPacketsResult plain = Draw(null, plainReads);
        NativeBattlePilotVisualIdentity identity = new(210, 0x100, 0, 123, 101) { ResourceGeneration = 456 };
        NativeBattleDuelEchoPacketsResult captured = Draw(identity, capturedReads);
        Assert.Equal(plainReads, capturedReads);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        int[] authoredOffsets = rejectFirst ? [16, 32] : [0, 16, 32];
        Assert.Equal(authoredOffsets.Length, captured.Batch.Attempts.Length);
        for (int index = 0; index < authoredOffsets.Length; index++)
        {
            NativeBattleGeometryPrimitive primitive = captured.Batch.Attempts[index].Primitive!;
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal(unchecked(identity.Primitive * 397 + authoredOffsets[index]), geometry.Identity.Primitive);
            Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
            Assert.True(geometry.ScreenSpace);
            Assert.False(geometry.Continuous);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(1, geometry.ScreenScale);
            for (int corner = 0; corner < geometry.CornerCount; corner++)
            {
                uint screen = primitive.Words[2 + corner * 2];
                Assert.Equal(new System.Numerics.Vector3(unchecked((short)screen), unchecked((short)(screen >> 16)), 0), geometry.Corner(corner));
            }
        }

        NativeBattleDuelEchoPacketsResult Draw(NativeBattlePilotVisualIdentity? pilot, List<int> reads)
        {
            return NativeBattleDuelEchoPackets.Emit68AAE0(Context(2, 1), 12, faces.ToImmutableArray(), index =>
            {
                reads.Add(index);
                return index == 4 ? new(0xAABBCCDD, 0, 16) { KnownBytesMask = 0xC0 } : Vertex(index);
            }, 17, 0, 0x155, 0x100, pilotIdentity: pilot);
        }
    }

    [Fact]
    public void Captured_black_quad_preserves_native_contiguous_screen_words_and_empty_depth_payload()
    {
        NativeBattleGfGeometryBatch plain = NativeBattleEffect210CapturePackets.Black(NativeBattleEffectArena.Clear().MarkUnknown(0, 24));
        NativeBattleGfGeometryBatch captured = NativeBattleEffect210CapturePackets.Black(NativeBattleEffectArena.Clear().MarkUnknown(0, 24), pilotIdentity: new(210, 0x100, 0, 123, 0xB1AC));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
        Assert.Empty(primitive.VertexPayload4);
        Assert.Equal(new System.Numerics.Vector3(192, 0, 0), geometry.B);
        Assert.Equal(new System.Numerics.Vector3(0, 256, 0), geometry.C);
        Assert.Equal(new System.Numerics.Vector3(192, 256, 0), geometry.D);
        Assert.Equal(8, geometry.ScreenScale);
        Assert.False(geometry.Continuous);
        Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
    }

    private static NativeBattleModulePoolTask Context(ushort triangles, ushort quads) => new NativeBattleModulePoolTask(1, 0, null, 0, ImmutableArray.CreateRange(new byte[88])).WriteWord(20, triangles).WriteWord(22, quads).WriteDword(32, 0x123456);

    private static NativeBattleDuelEchoProjectedVertex Vertex(int index) => index switch
    {
        0 => new(0x00100010, 1600, 0),
        1 => new(0x00100020, 1600, 0),
        2 => new(0x00200010, 1600, 0),
        3 => new(0x00200020, 1600, 0),
        _ => throw new InvalidOperationException()
    };

    private static byte[] Faces()
    {
        byte[] faces = new byte[36];
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(6), 0x2468);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(12), 0xABCD1234);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(20), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(22), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(24), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(28), 0xABCD1234);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(32), 0x2468);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(34), 0x1357);
        return faces;
    }
}
