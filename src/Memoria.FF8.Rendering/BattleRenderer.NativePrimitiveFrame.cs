using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private readonly record struct PrimitiveVertex(float X, float Y, float Z, float ReciprocalW, Vector2 Uv, Vector3 Color, float Alpha);
    private readonly record struct NativeTextureKey(ushort Page, ushort Clut, byte? AlternateShift, int X, int Y, int Width, int Height, NativeBattlePcTextureWindowPixels? Pixels, string? TextureSourceOwner);
    private Dictionary<NativeTextureKey, PixelImage> nativeTexturePages = [];
    private Dictionary<NativeTextureKey, PixelImage> previousNativeTexturePages = [];
    private NativeBattleEffectTextureState? nativeTextureOwner;
    private NativeBattlePcDirectTextureFormat? nativeDirectTextureFormat;
    private IReadOnlyList<NativeBattleTextureReplacement>? nativeModelReplacements;
    private INativeBattleEffectTextureSource? nativeEffectTextureSource;
    private long effectTextureGeneration;
    private readonly List<NativeRasterTriangle> nativeTriangles = [];
    private readonly record struct NativeRasterTriangle(PrimitiveVertex A, PrimitiveVertex B, PrimitiveVertex C, PixelImage? Texture, int Blend, byte Pass, float Area, int Left, int Right, int Top, int Bottom, bool Line = false);

    /// <summary>Render the executed original battle OT into a viewport representing its320x216 draw environment.</summary>
    public byte[] RenderNativePrimitiveFrame(NativeBattlePrimitiveFrame frame, NativeBattleEffectTextureState? textures, BattleCamera? overlayCamera = null, NativeBattlePrimitiveCapabilities? capabilities = null, NativeBattlePcTextureWindowCache? textureWindowCache = null, IReadOnlyList<NativeBattleTextureReplacement>? replacements = null, NativeBattlePcUiFrame? uiFrame = null, Func<int, int, bool, PixelImage>? uiTexture = null, INativeBattleEffectTextureSource? effectTextureSource = null)
        => RenderNativePrimitiveFrame(NativeBattlePreparedPrimitiveFrame.From(frame), textures, overlayCamera, capabilities, textureWindowCache, replacements, uiFrame, uiTexture, effectTextureSource);

    public byte[] RenderNativePrimitiveFrame(NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState? textures, BattleCamera? overlayCamera = null, NativeBattlePrimitiveCapabilities? capabilities = null, NativeBattlePcTextureWindowCache? textureWindowCache = null, IReadOnlyList<NativeBattleTextureReplacement>? replacements = null, NativeBattlePcUiFrame? uiFrame = null, Func<int, int, bool, PixelImage>? uiTexture = null, INativeBattleEffectTextureSource? effectTextureSource = null)
    {
        //The executed OT already owns projected XY/depth; a native WAIT can legitimately publish coincident eye and target.
        sceneProjectionReady = false;
        TriangleRenderer?.Begin(Width, Height, new Vector4(0, 0, 0, 1));
        if (TriangleRenderer is null)
        {
            Array.Clear(pixels);
            Array.Fill(depth, float.PositiveInfinity);
        }
        DrawnTriangles = 0;
        nativeTriangles.Clear();
        for (int index = 3; TriangleRenderer is null && index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }
        (nativeTexturePages, previousNativeTexturePages) = (previousNativeTexturePages, nativeTexturePages);
        nativeTexturePages.Clear();
        NativeBattleEffectTextureState? previousTextureOwner = nativeTextureOwner;
        bool sameReplacements = ReferenceEquals(nativeModelReplacements, replacements) || nativeModelReplacements is not null && replacements is not null && nativeModelReplacements.SequenceEqual(replacements);
        sameReplacements &= ReferenceEquals(nativeEffectTextureSource, effectTextureSource) && effectTextureGeneration == (effectTextureSource?.Generation ?? 0) && SameTextureOrigins(previousTextureOwner, textures);
        nativeEffectTextureSource = effectTextureSource;
        nativeModelReplacements = replacements?.ToArray();
        if (nativeDirectTextureFormat != capabilities?.DirectTextureFormat)
        {
            previousNativeTexturePages.Clear();
        }
        nativeTextureOwner = textures;
        nativeDirectTextureFormat = capabilities?.DirectTextureFormat;
        DrawNativePcUi(uiFrame, uiTexture, false);
        foreach (NativeBattleLogicalResolvedCommand resolved in frame.LastCommands)
        {
            if (resolved.Pass == 0 || resolved.Command.Opcode is >= 0xE1 and <= 0xE6)
            {
                continue;
            }
            NativeBattleLogicalCommand command = resolved.Command;
            if (command.Opcode == 0x80)
            {
                if (resolved.Pass != 1 || command.Words.Length != 4)
                {
                    throw new InvalidDataException("Native VRAM transfer presentation requires its executed first-pass command and four payload DWORDs.");
                }
                //The shared software handler has already copied the absolute VRAM words and invalidated their texture cache.
                continue;
            }
            if (command.Opcode == 0x02)
            {
                if (resolved.Pass != 1 || command.Words.Length != 3)
                {
                    throw new InvalidDataException("Native VRAM fill presentation requires its executed first-pass command and three payload DWORDs.");
                }
                //The absolute VRAM fill has already executed through NativeBattleSoftwarePrimitiveExecution and emits no viewport primitive.
                continue;
            }
            if (command.Opcode is 0x40 or 0x42 or 0x50 or 0x52)
            {
                DrawNativePrimitiveLine(frame, resolved);
                continue;
            }
            if ((command.Opcode & 0xFC) is 0x60 or 0x68)
            {
                DrawNativePrimitiveTile(frame, resolved);
                continue;
            }
            if (command.Opcode is 0x64 or 0x66)
            {
                DrawNativePrimitiveSprite(frame, resolved, textures, capabilities, textureWindowCache, replacements, previousTextureOwner, sameReplacements);
                continue;
            }
            if ((command.Opcode & 0xE0) != 0x20)
            {
                throw new NotSupportedException($"Native battle opcode{command.Opcode:X2} requires its original CPU rasterizer.");
            }
            bool textured = (command.Opcode & 4) != 0;
            bool gouraud = (command.Opcode & 16) != 0;
            bool quad = (command.Opcode & 8) != 0;
            bool semi = (command.Opcode & 2) != 0;
            if (resolved.Pass == 2 && !semi || resolved.Pass == 1 && semi)
            {
                continue;
            }
            int corners = quad ? 4 : 3;
            if (command.ProjectedCoordinates is { } projectedPoints && (projectedPoints.IsDefault || projectedPoints.Length != corners))
            {
                throw new InvalidDataException("Expanded polygon coordinates require one point per original corner.");
            }
            int stride = (gouraud ? 1 : 0) + 1 + (textured ? 1 : 0);
            int expectedWords = corners * stride + (gouraud ? 0 : 1);
            if (command.Words.Length != expectedWords)
            {
                throw new InvalidDataException("Native polygon command does not contain its original packed corner fields.");
            }
            bool perspective = !command.VertexPayload4.IsEmpty && command.VertexPayload4[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native polygon rendering requires the executed OT descriptor DWORD8; migrate the named snapshot explicitly."));
            bool fullViewport = quad && !textured && !gouraud && (command.Flags20 & 1) != 0 && command.VertexPayload4.All(value => value == 0) && NativeScreenQuadCoversProjection(command, resolved.DrawMode);
            NativeBattlePrimitiveDepthState state = frame.LastDepthState ?? throw new NotSupportedException("Native polygon rendering requires its retained460B60 depth owner.");
            NativeBattlePrimitiveColorState colorState = frame.LastColorState ?? throw new NotSupportedException("Native polygon rendering requires its retained PC diffuse-color dispatch owner.");
            ImmutableArray<NativeBattlePrimitiveDepthVertex> projected = state.Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native polygon lacks its original OT bucket."), perspective, command.Flags20, command.VertexPayload4);
            PixelImage? texture = null;
            if (textured)
            {
                texture = ResolveNativePrimitiveTexture(frame, resolved, textures, capabilities, textureWindowCache, replacements, colorState, previousTextureOwner, sameReplacements);
            }
            PrimitiveVertex At(int corner)
            {
                int coordinateWord = 1 + corner * stride;
                uint coordinate = command.Words[coordinateWord];
                uint color = command.Words[gouraud ? corner * stride : 0];
                int mode = resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native polygon diffuse color requires its executed ABR.");
                NativeBattlePrimitiveColor diffuse = textured ? colorState.Textured(color, semi, mode, (command.Opcode & 1) != 0) : colorState.Untextured(color, semi, mode);
                uint uv = textured ? command.Words[coordinateWord + 1] : 0;
                Vector2 coordinates = new((byte)uv, (byte)(uv >> 8));
                if (textured)
                {
                    NativeBattlePcTextureWindowSelection window = resolved.DrawMode.TextureSelection!;
                    coordinates *= new Vector2(window.ScaleU, window.ScaleV);
                }
                Vector2 position = command.ProjectedCoordinates is { } points
                    ? NativePrimitivePosition(points[corner].X, points[corner].Y, command.Flags20, resolved.DrawMode)
                    : NativePrimitivePosition(coordinate, command.Flags20, resolved.DrawMode);
                if (fullViewport)
                {
                    position = new((corner & 1) == 0 ? 0 : Width, corner < 2 ? 0 : Height);
                }
                return new(position.X, position.Y, projected[corner].Depth, projected[corner].ReciprocalW, coordinates, diffuse.Rgb, diffuse.Alpha);
            }
            int blend = semi && resolved.Pass == 2 ? resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native semi-transparent polygon requires its inherited ABR.") : -1;
            PrimitiveVertex a = At(0);
            PrimitiveVertex b = At(1);
            PrimitiveVertex c = At(2);
            QueueNativePrimitive(a, b, c, texture, blend, resolved.Pass);
            if (quad)
            {
                QueueNativePrimitive(b, At(3), c, texture, blend, resolved.Pass);
            }
        }
        if (TriangleRenderer is not null)
        {
            DrawNativePcRenderTail(frame.LastPcRenderTail, capabilities);
            DrawNativePcUi(uiFrame, uiTexture, true, true);
            TriangleRenderer.End();
            return pixels;
        }
        int stripes = Math.Min(8, Math.Min(Environment.ProcessorCount, Math.Max(1, Height / 90)));
        if (stripes == 1)
        {
            RasterStripe(0);
        }
        else
        {
            Parallel.For(0, stripes, new ParallelOptions { MaxDegreeOfParallelism = stripes }, RasterStripe);
        }
        DrawNativePcRenderTail(frame.LastPcRenderTail, capabilities);
        DrawNativePcUi(uiFrame, uiTexture, true, true);
        return pixels;

        void RasterStripe(int stripe)
        {
            int top = Height * stripe / stripes;
            int bottom = Height * (stripe + 1) / stripes - 1;
            // Every stripe executes the original submission order, with exclusive color/depth rows.
            foreach (NativeRasterTriangle triangle in nativeTriangles)
            {
                if (triangle.Line)
                {
                    RasterNativeLine(triangle, top, bottom);
                }
                else
                {
                    RasterNativePrimitive(triangle, top, bottom);
                }
            }
        }
    }

    private PixelImage ResolveNativePrimitiveTexture(NativeBattlePreparedPrimitiveFrame frame, NativeBattleLogicalResolvedCommand resolved, NativeBattleEffectTextureState? textures, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePcTextureWindowCache? textureWindowCache, IReadOnlyList<NativeBattleTextureReplacement>? replacements, NativeBattlePrimitiveColorState colorState, NativeBattleEffectTextureState? previousTextureOwner, bool sameReplacements)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        PixelImage? texture;
        ushort page = resolved.DrawMode.TexturePage ?? throw new NotSupportedException("Native textured polygon requires its executed texture page.");
        _ = resolved.DrawMode.TextureWindowBits ?? throw new NotSupportedException("Native465CE0 requires its retained45BC60 texture-window owner; explicitly migrate the named snapshot.");
        NativeBattlePcTextureWindowSelection window = resolved.DrawMode.TextureSelection ?? throw new NotSupportedException("Native465CE0 requires its executed PC window-cache selection; explicitly migrate the named snapshot.");
        window.Validate();
        NativeBattlePcTextureWindowPixels? retainedPixels;
        if (resolved.DrawMode.TexturePixelSnapshot is int snapshot)
        {
            ImmutableArray<NativeBattlePcTextureWindowPixels> snapshots = frame.LastTextureSnapshots ?? throw new NotSupportedException("Native texture draw requires its executed resource pixel snapshots.");
            if ((uint)snapshot >= snapshots.Length)
            {
                throw new InvalidDataException("Native texture draw refers outside its retained resource pixel snapshots.");
            }
            retainedPixels = snapshots[snapshot];
            retainedPixels.ValidateMapping(window.Row, new(window.X, window.Y, window.Width, window.Height));
        }
        else
        {
            retainedPixels = textureWindowCache is not null ? textureWindowCache.PixelWords(window) : window.UsesRetainedPixelWords == false ? null : throw new NotSupportedException("Native texture selection requires its retained cache pixel owner.");
        }
        ushort clut = (ushort)(command.Words[2] >> 16);
        byte? alternateShift = null;
        if (resolved.DrawMode.BlendMode == 2)
        {
            if (colorState.SubtractUsesNormalizedColor)
            {
                throw new NotSupportedException("Native PC ABR2 requires the selected alpha-palette device profile.");
            }
            alternateShift = colorState.AlternatePaletteShift ?? throw new NotSupportedException("Native PC ABR2 requires its retained45B540 alternate-palette shift.");
        }
        NativeTextureKey key = new(page, clut, alternateShift, window.X, window.Y, window.Width, window.Height, retainedPixels, command.TextureSourceOwner);
        if (!nativeTexturePages.TryGetValue(key, out texture))
        {
            bool reusable = sameReplacements && previousNativeTexturePages.TryGetValue(key, out texture) && (ReferenceEquals(previousTextureOwner, textures) || previousTextureOwner is not null && textures is not null && NativeBattleEffectTextureReader.HasSamePcRegionSource(previousTextureOwner, textures, page, clut, window.X, window.Y, window.Width, window.Height, retainedPixels));
            if (!reusable)
            {
                texture = NativeBattleEffectTextureReader.DecodePcRegion(textures ?? throw new NotSupportedException("Native polygon texture sampling requires its retained VRAM owner."), page, clut, window.X, window.Y, window.Width, window.Height, alternateShift, capabilities?.DirectTextureFormat, retainedPixels);
                if (replacements is not null && alternateShift is null)
                {
                    texture = NativeBattleEffectTextureReader.ApplyModelReplacements(texture, textures!, page, clut, window.X, window.Y, retainedPixels, replacements, command.TextureSourceOwner);
                }
                if (nativeEffectTextureSource is not null)
                {
                    texture = nativeEffectTextureSource.Apply(texture, textures!, page, clut, window.X, window.Y, window.Width, window.Height, retainedPixels, alternateShift, capabilities?.DirectTextureFormat);
                    effectTextureGeneration = nativeEffectTextureSource.Generation;
                }
            }
            nativeTexturePages.Add(key, texture!);
        }
        return texture!;
    }

    private static bool SameTextureOrigins(NativeBattleEffectTextureState? previous, NativeBattleEffectTextureState? current)
    {
        if (ReferenceEquals(previous, current))
        {
            return true;
        }
        bool samePixels = previous?.LiveTextureSources is { } before && current?.LiveTextureSources is { } after ? before.SequenceEqual(after) : previous?.LiveTextureSources is null && current?.LiveTextureSources is null;
        bool sameRegistrations = previous?.TextureRegistrations is { } previousRegistrations && current?.TextureRegistrations is { } currentRegistrations ? previousRegistrations.SequenceEqual(currentRegistrations) : previous?.TextureRegistrations is null && current?.TextureRegistrations is null;
        return samePixels && sameRegistrations;
    }

    private bool NativeScreenQuadCoversProjection(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode)
    {
        Vector2 origin = new(ProjectionBounds.X, ProjectionBounds.Y);
        Vector2 extent = new(ProjectionBounds.Width, ProjectionBounds.Height);
        Vector2 At(int word)
        {
            Vector2 position = NativePrimitivePosition(command.Words[word], command.Flags20, mode);
            // Stored viewport scales can put an integer screen boundary a fraction of a pixel away from its edge.
            return new(MathF.Round(position.X), MathF.Round(position.Y));
        }
        return At(1) == origin
            && At(2) == origin + new Vector2(extent.X, 0)
            && At(3) == origin + new Vector2(0, extent.Y)
            && At(4) == origin + extent;
    }

    private void DrawNativePrimitiveTile(NativeBattlePreparedPrimitiveFrame frame, NativeBattleLogicalResolvedCommand resolved)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        bool semi = (command.Opcode & 2) != 0;
        if (resolved.Pass == 1 && semi || resolved.Pass == 2 && !semi)
        {
            return;
        }
        bool point = (command.Opcode & 0xFC) == 0x68;
        if (command.Words.Length != (point ? 2 : 3))
        {
            throw new InvalidDataException("Native TILE requires its original RGB, XY and selected size fields.");
        }
        int blend = resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native TILE requires its executed ABR.");
        NativeBattlePrimitiveColor diffuse = (frame.LastColorState ?? throw new NotSupportedException("Native TILE requires its retained PC color owner.")).Untextured(command.Words[0], semi, blend);
        bool perspective = !command.VertexPayload4.IsEmpty && command.VertexPayload4[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native TILE requires its executed descriptor depth mode."));
        ImmutableArray<int> depths = command.VertexPayload4.IsEmpty ? [] : Enumerable.Repeat(command.VertexPayload4[0], 4).ToImmutableArray();
        ImmutableArray<NativeBattlePrimitiveDepthVertex> vertices = (frame.LastDepthState ?? throw new NotSupportedException("Native TILE requires its retained depth dispatch owner.")).Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native TILE lacks its original bucket."), perspective, command.Flags20, depths);
        int x = unchecked((short)command.Words[1]);
        int y = unchecked((short)(command.Words[1] >> 16));
        int width = point ? (command.Flags20 & 1) != 0 ? 1 : 8 : (ushort)command.Words[2];
        int height = point ? width : (ushort)(command.Words[2] >> 16);
        PrimitiveVertex At(int px, int py)
        {
            Vector2 position = NativePrimitivePosition(px, py, command.Flags20, resolved.DrawMode);
            return new(position.X, position.Y, vertices[0].Depth, vertices[0].ReciprocalW, Vector2.Zero, diffuse.Rgb, diffuse.Alpha);
        }
        PrimitiveVertex a = At(x, y);
        PrimitiveVertex b = At(x + width, y);
        PrimitiveVertex c = At(x, y + height);
        QueueNativePrimitive(a, b, c, null, semi ? blend : -1, resolved.Pass);
        QueueNativePrimitive(b, At(x + width, y + height), c, null, semi ? blend : -1, resolved.Pass);
    }

    private void QueueNativePrimitive(PrimitiveVertex a, PrimitiveVertex b, PrimitiveVertex c, PixelImage? texture, int blend, byte pass)
    {
        static float Edge(PrimitiveVertex first, PrimitiveVertex second, float x, float y) => (second.X - first.X) * (y - first.Y) - (second.Y - first.Y) * (x - first.X);
        float area = Edge(a, b, c.X, c.Y);
        if (!float.IsFinite(area) || area == 0)
        {
            return;
        }
        if (area < 0)
        {
            (b, c) = (c, b);
            area = -area;
        }
        int left = Math.Clamp((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, Width - 1);
        int right = Math.Clamp((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 0, Width - 1);
        int top = Math.Clamp((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, Height - 1);
        int bottom = Math.Clamp((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), 0, Height - 1);
        DrawnTriangles++;
        if (TriangleRenderer is { } renderer)
        {
            static TriangleVertex Convert(PrimitiveVertex vertex) => new(new Vector2(vertex.X, vertex.Y), vertex.Z, vertex.ReciprocalW, vertex.Uv, new Vector4(vertex.Color / 255f, vertex.Alpha / 255f));
            TriangleBlend equation = blend switch
            {
                -1 => TriangleBlend.Replace,
                0 or 2 => TriangleBlend.Alpha,
                1 => TriangleBlend.Add,
                3 => TriangleBlend.AlphaAdd,
                _ => throw new NotSupportedException("Unsupported native blend equation.")
            };
            float minimumAlpha = texture is null ? 0 : pass == 1 ? 112f / 255 : pass == 2 && blend == 0 ? 122f / 255 : 1f / 255;
            TriangleMaterial material = new(texture, equation, TriangleDepthComparison.LessEqual, pass == 1, minimumAlpha, TriangleSampler.PointWrap);
            renderer.Draw(Convert(a), Convert(b), Convert(c), material);
            return;
        }
        nativeTriangles.Add(new(a, b, c, texture, blend, pass, area, left, right, top, bottom));
    }

    private void RasterNativePrimitive(NativeRasterTriangle triangle, int stripeTop, int stripeBottom)
    {
        static float Edge(PrimitiveVertex first, PrimitiveVertex second, float x, float y) => (second.X - first.X) * (y - first.Y) - (second.Y - first.Y) * (x - first.X);
        static bool Inside(float value, PrimitiveVertex first, PrimitiveVertex second) => value > 0 || value == 0 && (second.Y < first.Y || second.Y == first.Y && second.X > first.X);
        PrimitiveVertex a = triangle.A;
        PrimitiveVertex b = triangle.B;
        PrimitiveVertex c = triangle.C;
        PixelImage? texture = triangle.Texture;
        int blend = triangle.Blend;
        byte pass = triangle.Pass;
        float area = triangle.Area;
        int left = triangle.Left;
        int right = triangle.Right;
        int top = Math.Max(triangle.Top, stripeTop);
        int bottom = Math.Min(triangle.Bottom, stripeBottom);
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                //The PC's -.5 transformed-vertex bias and integer pixel centers are equivalent to these half-pixel samples.
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
                float z = a.Z + (b.Z - a.Z) * wb + (c.Z - a.Z) * wc;
                int index = y * Width + x;
                if (!float.IsFinite(z) || z < 0 || z > 1 || z > depth[index])
                {
                    continue;
                }
                Vector3 color = a.Color + (b.Color - a.Color) * wb + (c.Color - a.Color) * wc;
                float alpha = (a.Alpha + (b.Alpha - a.Alpha) * wb + (c.Alpha - a.Alpha) * wc) / 255;
                if (texture is not null)
                {
                    float rhw = a.ReciprocalW * wa + b.ReciprocalW * wb + c.ReciprocalW * wc;
                    if (!float.IsFinite(rhw) || rhw <= 0)
                    {
                        continue;
                    }
                    Vector2 uv = (a.Uv * (a.ReciprocalW * wa) + b.Uv * (b.ReciprocalW * wb) + c.Uv * (c.ReciprocalW * wc)) / rhw;
                    int u = (int)MathF.Floor(uv.X * texture.Width) % texture.Width;
                    int v = (int)MathF.Floor(uv.Y * texture.Height) % texture.Height;
                    Rgba32 texel = texture[u < 0 ? u + texture.Width : u, v < 0 ? v + texture.Height : v];
                    if (texel.A == 0 || pass == 1 && texel.A < 112 || pass == 2 && blend == 0 && texel.A < 122)
                    {
                        continue;
                    }
                    color *= new Vector3(texel.R, texel.G, texel.B) / 255f;
                    //D3DTBLEND_MODULATE selects texture alpha when the texture has an alpha channel.
                    alpha = texel.A / 255f;
                }
                BlendNativePcPrimitive(index * 4, color, alpha, blend);
                if (pass == 1)
                {
                    depth[index] = z;
                }
            }
        }
    }

    private void BlendNativePcPrimitive(int pixel, Vector3 color, float alpha, int blend)
    {
        for (int component = 0; component < 3; component++)
        {
            float source = Math.Clamp(color[component], 0, 255);
            float destination = pixels[pixel + component];
            float result = blend switch
            {
                -1 => source,
                0 or 2 => source * alpha + destination * (1 - alpha),
                1 => source + destination,
                3 => source * alpha + destination,
                _ => throw new NotSupportedException("Native PC primitive has no supported device blend equation.")
            };
            pixels[pixel + component] = (byte)Math.Clamp((int)result, 0, 255);
        }
        pixels[pixel + 3] = 255;
    }
}
