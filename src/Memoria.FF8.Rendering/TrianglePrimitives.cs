using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>Common screen-space primitives submitted to the selected triangle backend.</summary>
public static class TrianglePrimitives
{
    public static void Quad(ITriangleRenderer renderer, Vector2 origin, Vector2 size, float depth, Vector4 color, TriangleMaterial material, Vector2 uvEnd, Vector2 uvStart = default, bool pointSampleRectangle = false)
    {
        Vector4 screenRectangle = default;
        Vector4 texelRectangle = default;
        if (pointSampleRectangle && material.Texture is { } texture)
        {
            Vector2 textureSize = new(texture.Width, texture.Height);
            screenRectangle = new(origin, size.X, size.Y);
            Vector2 texelOrigin = uvStart * textureSize;
            Vector2 texelSize = (uvEnd - uvStart) * textureSize;
            texelRectangle = new(texelOrigin, texelSize.X, texelSize.Y);
        }
        TriangleVertex a = new(origin, depth, 1, uvStart, color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        TriangleVertex b = new(origin + new Vector2(size.X, 0), depth, 1, new Vector2(uvEnd.X, uvStart.Y), color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        TriangleVertex c = new(origin + new Vector2(0, size.Y), depth, 1, new Vector2(uvStart.X, uvEnd.Y), color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        TriangleVertex d = new(origin + size, depth, 1, uvEnd, color) { TextureScreenRectangle = screenRectangle, TextureTexelRectangle = texelRectangle };
        renderer.Draw(a, b, c, material);
        renderer.Draw(b, d, c, material);
    }
}
