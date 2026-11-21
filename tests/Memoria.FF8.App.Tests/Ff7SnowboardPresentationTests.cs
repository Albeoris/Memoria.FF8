using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7SnowboardPresentationTests
{
    [Fact]
    public void NativeGouraudTrianglePreservesIndependentVertexColors()
    {
        Capture renderer = Render(128, 128, 128);
        Assert.Equal(1, renderer.Triangles);
        Assert.Equal(new Vector4(1, 0, 0, 1), renderer.Colors[0]);
        Assert.Equal(new Vector4(0, 0, 1, 1), renderer.Colors[1]);
        Assert.Equal(new Vector4(0, 1, 0, 1), renderer.Colors[2]);
    }

    [Fact]
    public void NearPlaneClipsCrossingTriangleWithoutDiscardingVisiblePart()
    {
        Assert.Equal(2, Render(32, 128, 128).Triangles);
        Assert.Equal(0, Render(32, 32, 32).Triangles);
    }

    private static Capture Render(short firstZ, short secondZ, short thirdZ)
    {
        byte[] bytes = new byte[88];
        UInt(0, 0x41);
        UInt(8, 1);
        UInt(12, 28);
        UInt(16, 3);
        UInt(28, 52);
        UInt(32, 1);
        Vertex(40, -32, -32, firstZ);
        Vertex(48, -32, 32, secondZ);
        Vertex(56, 32, -32, thirdZ);
        bytes[64] = 6;
        bytes[65] = 5;
        bytes[67] = 0x30;
        UInt(68, 0x000000FF);
        UInt(72, 0x0000FF00);
        UInt(76, 0x00FF0000);
        UInt(80, 0x00010000);
        UInt(84, 2);
        Ff7SnowboardAssets assets = new(PcSnowboardModel.Read(bytes), new Dictionary<string, PcTexture>());
        Ff7SnowboardPresentation presentation = new(assets, Array.Empty<Ff7SnowboardTextureBinding>());
        Ff7SnowboardFrame frame = new(0, Ff7SnowboardPhase.Playing, Vector3.Zero, Matrix4x4.Identity, Ff7SnowboardCamera.CreateProjection(640, 480), [new(0, Matrix4x4.Identity)], 0, 0, 0, 0);
        Capture renderer = new();
        presentation.Render(renderer, frame, 640, 480);
        return renderer;

        void UInt(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }

        void Vertex(int offset, short x, short y, short z)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), z);
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        internal int Triangles { get; private set; }
        internal List<Vector4> Colors { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles++;
            Colors.AddRange([a.Color, b.Color, c.Color]);
            Assert.InRange(a.Depth, 0, 1);
            Assert.InRange(b.Depth, 0, 1);
            Assert.InRange(c.Depth, 0, 1);
        }

        public void End()
        {
        }
    }
}
