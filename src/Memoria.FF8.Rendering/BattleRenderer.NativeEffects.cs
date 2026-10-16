using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private short nativeRoll;
    private readonly record struct EffectVertex(Vector3 Position, Vector2 Uv, Vector3 Color);
    private readonly record struct EffectScreen(Screen Screen, Vector3 Color);

    /// <summary>Native571BC0/571DC0 sprites and570650/572200 defensive meshes, drawn from scheduler receipts.</summary>
    public void DrawNativeModuleEffects(IReadOnlyList<NativeBattleModuleDraw> draws, NativeBattleEffectTextureState? textures, IReadOnlyList<NativeBattleDefensiveResource> defensiveResources, int? inheritedBlendMode = null)
    {
        foreach (NativeBattleModuleDraw draw in draws)
        {
            switch (draw.Kind)
            {
                case NativeBattleModuleDrawKind.Sound:
                    break;
                case NativeBattleModuleDrawKind.Flash:
                    DrawNativeFlash(draw.Intensity, inheritedBlendMode ?? throw new NotSupportedException("Native flash requires its inherited ordering-table draw mode."));
                    break;
                case NativeBattleModuleDrawKind.Sprite:
                    DrawNativeSprite(draw, textures ?? throw new NotSupportedException("Native sprite rendering requires the retained original texture memory."));
                    break;
                case NativeBattleModuleDrawKind.Defensive:
                    DrawNativeDefensive(draw, defensiveResources.SingleOrDefault(resource => resource.Variant == draw.Variant) ?? throw new NotSupportedException($"Native defensive variant{draw.Variant} lacks its571240 mesh."), inheritedBlendMode ?? throw new NotSupportedException("Native defensive rendering requires its inherited ordering-table draw mode."));
                    break;
                default:
                    throw new NotSupportedException($"Native module draw{draw.Kind} has no original renderer.");
            }
        }
    }

    private void DrawNativeFlash(byte intensity, int blendMode)
    {
        //571300 publishes a semi-transparent monochrome TILE at(0,0), size320x216.
        int bottom = Math.Min(Height, Height * 216 / 240);
        for (int y = 0; y < bottom; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                BlendEffect((y * Width + x) * 4, new(intensity), blendMode);
            }
        }
    }

    private void DrawNativeSprite(NativeBattleModuleDraw draw, NativeBattleEffectTextureState textures)
    {
        NativeBattleModuleResource resource = draw.Resource ?? throw new InvalidDataException("Native sprite receipt lacks its immutable resource.");
        NativeBattleModuleSpriteFrame frame = draw.SpriteFrame ?? throw new InvalidDataException("Native sprite receipt lacks its current frame.");
        if (frame.Quads.Length % 20 != 0 || (draw.SpriteFlags & 2) != 0 && draw.Direction is not (4096 or -4096) || !draw.SpriteWorldRotation.IsEmpty && draw.SpriteWorldRotation.Length != 9)
        {
            throw new InvalidDataException("Native sprite receipt has malformed original quads or direction.");
        }
        NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new InvalidDataException("Native sprite receipt lacks its original anchor.");
        Vector3 center = Vector3.Transform(NativeWorld(anchor.X, anchor.Y, anchor.Z), view);
        if (center != Vector3.Zero)
        {
            //571BC0 normalizes camera-space translation and applies the native signed depth offset.
            center += Vector3.Normalize(center) * (draw.DepthOffset / 100f);
        }
        if ((draw.SpriteFlags & ~0x2FF) != 0)
        {
            throw new NotSupportedException("Native sprite flags require an unsupported original571C80 setup.");
        }
        int angle = (draw.SpriteFlags & 1) != 0 ? draw.Rotation - nativeRoll : (draw.SpriteFlags & 0x200) == 0 ? -nativeRoll : 0;
        int cosine = NativeBattleCameraMath.Cosine(angle);
        int sine = NativeBattleCameraMath.Sine(angle);
        for (int offset = 0; offset < frame.Quads.Length; offset += 20)
        {
            byte[] quad = frame.Quads.AsSpan().Slice(offset, 20).ToArray();
            int width = quad[0];
            int height = quad[2];
            if (width == 0 || height == 0)
            {
                continue;
            }
            ushort flags = U16(quad, 14);
            ushort page = (ushort)(flags & 0x1FF);
            int selector = flags >> 12;
            if (selector > 3 || resource.Bytes.Length < 8)
            {
                throw new NotSupportedException("Native sprite palette selector exceeds its four original resource offsets.");
            }
            int paletteOffset = unchecked((sbyte)resource.Bytes[4 + selector]);
            if ((draw.PackedParameters & 0x80000000) != 0 && selector == 0)
            {
                paletteOffset = (int)((draw.PackedParameters >> 24) & 0x7F);
            }
            if ((draw.SpriteFlags & (0x10 << selector)) != 0 && !draw.SpritePaletteOffsets.IsEmpty)
            {
                if (draw.SpritePaletteOffsets.Length != 4)
                {
                    throw new InvalidDataException("Native sprite explicit palettes require four BYTE registers.");
                }
                paletteOffset = unchecked((sbyte)draw.SpritePaletteOffsets[selector]);
            }
            ushort clut = unchecked((ushort)(U16(quad, 6) + paletteOffset * 64));
            PixelImage texture = NativeBattleEffectTextureReader.DecodeRegion(textures, page, clut, quad[4], quad[5], width + 1, height + 1);
            int localCosine = (flags & 0xC00) != 0 ? NativeBattleCameraMath.Cosine(S16(quad, 12)) : 4096;
            int localSine = (flags & 0xC00) != 0 ? NativeBattleCameraMath.Sine(S16(quad, 12)) : 0;
            int scaleX = (flags & 0xC00) != 0 ? S16(quad, 16) : 4096;
            int scaleY = (flags & 0xC00) != 0 ? S16(quad, 18) : 4096;
            EffectVertex At(int x, int y, int u, int v)
            {
                int localX = unchecked((short)((x * (scaleX * localCosine >> 12) - y * (scaleY * localSine >> 12)) >> 12));
                int localY = unchecked((short)((x * (scaleX * localSine >> 12) + y * (scaleY * localCosine >> 12)) >> 12));
                localX += 8 * width + 16 * S16(quad, 8);
                localY += 8 * height + 16 * S16(quad, 10);
                int rotatedX = (localX * cosine - localY * sine) >> 12;
                int rotatedY = (localX * sine + localY * cosine) >> 12;
                Vector3 delta;
                if (draw.SpriteWorldRotation.IsEmpty)
                {
                    if ((draw.SpriteFlags & 2) != 0)
                    {
                        rotatedX = rotatedX * draw.Direction >> 12;
                    }
                    delta = new(rotatedX * draw.Scale / 409600f, -rotatedY * draw.Scale / 409600f, 0);
                }
                else
                {
                    int Row(int row) => (int)(((long)draw.SpriteWorldRotation[row * 3] * localX + (long)draw.SpriteWorldRotation[row * 3 + 1] * localY) >> 12);
                    Vector3 cameraVector = Vector3.TransformNormal(NativeWorld(Row(0), Row(1), Row(2)), view);
                    delta = new(cameraVector.X * cosine / 4096f + cameraVector.Y * sine / 4096f, -cameraVector.X * sine / 4096f + cameraVector.Y * cosine / 4096f, cameraVector.Z);
                }
                Vector3 color = new((quad[3] & 1) != 0 ? 128 : quad[1]);
                if ((draw.SpriteFlags & 4) != 0 && (quad[3] & 1) == 0)
                {
                    color = new(unchecked((byte)((draw.SpriteColor & 255) * quad[1] >> 7)), unchecked((byte)(((draw.SpriteColor >> 8) & 255) * quad[1] >> 7)), unchecked((byte)(((draw.SpriteColor >> 16) & 255) * quad[1] >> 7)));
                }
                return new(center + delta, new(u, v), color);
            }
            EffectVertex a = At(-8 * width, -8 * height, 0, 0);
            int inset = (draw.SpriteFlags & 8) != 0 ? 1 : 0;
            EffectVertex b = At(8 * width, -8 * height, Math.Min(width - inset, 255 - quad[4]), 0);
            EffectVertex c = At(-8 * width, 8 * height, 0, Math.Min(height - inset, 255 - quad[5]));
            EffectVertex d = At(8 * width, 8 * height, Math.Min(width - inset, 255 - quad[4]), Math.Min(height - inset, 255 - quad[5]));
            //4617F0/461A30 selects the polygon's own TPAGE after any extended E1000220 packet.
            int blend = (quad[3] & 2) == 0 ? -1 : (page >> 5) & 3;
            EffectTriangle(a, b, c, texture, blend, true);
            EffectTriangle(b, d, c, texture, blend, true);
        }
    }

    private void DrawNativeDefensive(NativeBattleModuleDraw draw, NativeBattleDefensiveResource resource, int blendMode)
    {
        if (draw.Phase >= 8)
        {
            throw new InvalidDataException("Native defensive phase exceeds its eight scheduler callbacks.");
        }
        byte[] bytes = resource.Bytes.ToArray();
        NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new InvalidDataException("Native defensive receipt lacks its original anchor.");
        short[] rotation = NativeBattlePoseMatrices.RotationWords(new(0, draw.Rotation, draw.Orientation)).Select(value => unchecked((short)(value * draw.Scale >> 12))).ToArray();
        int fade = draw.Phase < 2 ? (2 - draw.Phase) << 11 : draw.Phase >= 4 ? (draw.Phase - 4) << 10 : 0;
        int cursor = checked((int)U32(bytes, 0));
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)U32(bytes, cursor));
            cursor += 4;
            if (count != 0 && group is not (4 or 5))
            {
                throw new NotSupportedException("Native defensive resource contains an unsupported primitive group.");
            }
            int corners = group == 5 ? 4 : 3;
            for (int face = 0; face < count; face++)
            {
                int start = cursor;
                EffectVertex At(int corner)
                {
                    int vertex = 8 + U16(bytes, start + 4 + corner * 2) * 4;
                    int x = S16(bytes, vertex);
                    int y = S16(bytes, vertex + 2);
                    int z = S16(bytes, vertex + 4);
                    int Row(int row) => (int)(((long)rotation[row * 3] * x + (long)rotation[row * 3 + 1] * y + (long)rotation[row * 3 + 2] * z) >> 12);
                    Vector3 world = NativeWorld(anchor.X + Row(0), anchor.Y + Row(1), anchor.Z + Row(2));
                    int color = start + (corner == 0 ? 0 : 8 + corner * 4);
                    return new(Vector3.Transform(world, view), Vector2.Zero, new Vector3(bytes[color], bytes[color + 1], bytes[color + 2]) * ((4096 - fade) / 4096f));
                }
                EffectVertex a = At(0);
                EffectVertex b = At(1);
                EffectVertex c = At(2);
                EffectTriangle(a, b, c, null, blendMode, false);
                if (corners == 4)
                {
                    EffectTriangle(b, At(3), c, null, blendMode, false);
                }
                cursor += group == 5 ? 24 : 20;
            }
        }
    }

    private void EffectTriangle(EffectVertex a, EffectVertex b, EffectVertex c, PixelImage? texture, int blend, bool textured)
    {
        const float near = 0.25f;
        Span<EffectVertex> input = stackalloc EffectVertex[3] { a, b, c };
        Span<EffectVertex> clipped = stackalloc EffectVertex[5];
        int count = 0;
        for (int index = 0; index < 3; index++)
        {
            EffectVertex current = input[index];
            EffectVertex previous = input[(index + 2) % 3];
            bool inside = current.Position.Z <= -near;
            if (inside != (previous.Position.Z <= -near))
            {
                float amount = (-near - previous.Position.Z) / (current.Position.Z - previous.Position.Z);
                clipped[count++] = new(Vector3.Lerp(previous.Position, current.Position, amount), Vector2.Lerp(previous.Uv, current.Uv, amount), Vector3.Lerp(previous.Color, current.Color, amount));
            }
            if (inside)
            {
                clipped[count++] = current;
            }
        }
        EffectScreen ProjectEffect(EffectVertex vertex) => new(Project(new(vertex.Position, vertex.Uv)), vertex.Color);
        for (int index = 1; index + 1 < count; index++)
        {
            EffectRaster(ProjectEffect(clipped[0]), ProjectEffect(clipped[index]), ProjectEffect(clipped[index + 1]), texture, blend, textured);
        }
    }

    private void EffectRaster(EffectScreen first, EffectScreen second, EffectScreen third, PixelImage? texture, int blend, bool textured)
    {
        float area = Edge(first.Screen, second.Screen, third.Screen.X, third.Screen.Y);
        if (Math.Abs(area) < 0.001f)
        {
            return;
        }
        if (area < 0)
        {
            (second, third) = (third, second);
            area = -area;
        }
        Screen a = first.Screen;
        Screen b = second.Screen;
        Screen c = third.Screen;
        int left = Math.Clamp((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, Width - 1);
        int right = Math.Clamp((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 0, Width - 1);
        int top = Math.Clamp((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, Height - 1);
        int bottom = Math.Clamp((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), 0, Height - 1);
        bool Inside(float edge, Screen start, Screen end) => edge > 0 || edge == 0 && (end.Y < start.Y || end.Y == start.Y && end.X > start.X);
        DrawnTriangles++;
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float ea = Edge(b, c, x + 0.5f, y + 0.5f);
                float eb = Edge(c, a, x + 0.5f, y + 0.5f);
                float ec = Edge(a, b, x + 0.5f, y + 0.5f);
                if (!Inside(ea, b, c) || !Inside(eb, c, a) || !Inside(ec, a, b))
                {
                    continue;
                }
                float wa = ea / area;
                float wb = eb / area;
                float wc = ec / area;
                float inverse = a.InverseZ * wa + b.InverseZ * wb + c.InverseZ * wc;
                int index = y * Width + x;
                if (inverse < depth[index])
                {
                    continue;
                }
                Vector3 color = first.Color + (second.Color - first.Color) * wb + (third.Color - first.Color) * wc;
                int selectedBlend = blend;
                if (textured)
                {
                    Vector2 uv = (a.UvOverZ * wa + b.UvOverZ * wb + c.UvOverZ * wc) / inverse;
                    Rgba32 texel = texture![Math.Clamp((int)uv.X, 0, texture.Width - 1), Math.Clamp((int)uv.Y, 0, texture.Height - 1)];
                    if (texel.A == 0)
                    {
                        continue;
                    }
                    color *= new Vector3(texel.R, texel.G, texel.B) / 128f;
                    if (texel.A == 255)
                    {
                        selectedBlend = -1;
                    }
                }
                BlendEffect(index * 4, color, selectedBlend);
            }
        }
    }

    private void BlendEffect(int pixel, Vector3 color, int blend)
    {
        for (int component = 0; component < 3; component++)
        {
            int source = Math.Clamp((int)color[component], 0, 255);
            int destination = pixels[pixel + component];
            int result = blend switch
            {
                -1 => source,
                0 => destination / 2 + source / 2,
                1 => destination + source,
                2 => destination - source,
                3 => destination + source / 4,
                _ => throw new NotSupportedException("Unknown native PSX semi-transparency mode.")
            };
            pixels[pixel + component] = (byte)Math.Clamp(result, 0, 255);
        }
        pixels[pixel + 3] = 255;
    }

    private static Vector3 NativeWorld(int x, int y, int z) => new Vector3(x, -y, -z) / 100f;
    private static short S16(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
