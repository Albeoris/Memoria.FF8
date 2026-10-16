using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private readonly record struct PrimitiveVertex(float X, float Y, float Z, float ReciprocalW, Vector2 Uv, Vector3 Color, float Alpha);

    /// <summary>Render the executed original battle OT into a viewport representing its320x216 draw environment.</summary>
    public byte[] RenderNativePrimitiveFrame(NativeBattlePrimitiveFrame frame, NativeBattleEffectTextureState? textures, BattleCamera? overlayCamera = null)
    {
        if (overlayCamera is BattleCamera camera)
        {
            Begin(camera);
        }
        else
        {
            sceneProjectionReady = false;
        }
        Array.Clear(pixels);
        Array.Fill(depth, float.PositiveInfinity);
        DrawnTriangles = 0;
        for (int index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }
        Dictionary<(ushort Page, ushort Clut, byte? AlternateShift), PixelImage> pages = [];
        foreach (NativeBattleLogicalResolvedCommand resolved in frame.LastCommands)
        {
            if (resolved.Pass == 0 || resolved.Command.Opcode == 0xE1)
            {
                continue;
            }
            NativeBattleLogicalCommand command = resolved.Command;
            if ((command.Opcode & 0xFC) == 0x60)
            {
                DrawNativePrimitiveTile(frame, resolved);
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
            int stride = (gouraud ? 1 : 0) + 1 + (textured ? 1 : 0);
            int expectedWords = corners * stride + (gouraud ? 0 : 1);
            if (command.Words.Length != expectedWords)
            {
                throw new InvalidDataException("Native polygon command does not contain its original packed corner fields.");
            }
            bool perspective = !command.VertexPayload4.IsEmpty && command.VertexPayload4[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native polygon rendering requires the executed OT descriptor DWORD8; migrate the named snapshot explicitly."));
            NativeBattlePrimitiveDepthState state = frame.LastDepthState ?? throw new NotSupportedException("Native polygon rendering requires its retained460B60 depth owner.");
            NativeBattlePrimitiveColorState colorState = frame.LastColorState ?? throw new NotSupportedException("Native polygon rendering requires its retained PC diffuse-color dispatch owner.");
            ImmutableArray<NativeBattlePrimitiveDepthVertex> projected = state.Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native polygon lacks its original OT bucket."), perspective, command.Flags20, command.VertexPayload4);
            PixelImage? texture = null;
            if (textured)
            {
                ushort page = resolved.DrawMode.TexturePage ?? throw new NotSupportedException("Native textured polygon requires its executed texture page.");
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
                if (!pages.TryGetValue((page, clut, alternateShift), out texture))
                {
                    texture = NativeBattleEffectTextureReader.DecodePcRegion(textures ?? throw new NotSupportedException("Native polygon texture sampling requires its retained VRAM owner."), page, clut, 0, 0, 256, 256, alternateShift);
                    pages.Add((page, clut, alternateShift), texture);
                }
            }
            PrimitiveVertex At(int corner)
            {
                int coordinateWord = 1 + corner * stride;
                uint coordinate = command.Words[coordinateWord];
                uint color = command.Words[gouraud ? corner * stride : 0];
                int mode = resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native polygon diffuse color requires its executed ABR.");
                NativeBattlePrimitiveColor diffuse = textured ? colorState.Textured(color, semi, mode, (command.Opcode & 1) != 0) : colorState.Untextured(color, semi, mode);
                uint uv = textured ? command.Words[coordinateWord + 1] : 0;
                float unit = (command.Flags20 & 1) != 0 ? 1 : 0.125f;
                return new(unchecked((short)coordinate) * unit * Width / 320, unchecked((short)(coordinate >> 16)) * unit * Height / 216, projected[corner].Depth, projected[corner].ReciprocalW, new((byte)uv, (byte)(uv >> 8)), diffuse.Rgb, diffuse.Alpha);
            }
            int blend = semi && resolved.Pass == 2 ? resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native semi-transparent polygon requires its inherited ABR.") : -1;
            PrimitiveVertex a = At(0);
            PrimitiveVertex b = At(1);
            PrimitiveVertex c = At(2);
            RasterNativePrimitive(a, b, c, texture, blend, resolved.Pass);
            if (quad)
            {
                RasterNativePrimitive(b, At(3), c, texture, blend, resolved.Pass);
            }
        }
        return pixels;
    }

    private void DrawNativePrimitiveTile(NativeBattlePrimitiveFrame frame, NativeBattleLogicalResolvedCommand resolved)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        bool semi = (command.Opcode & 2) != 0;
        if (resolved.Pass == 1 && semi || resolved.Pass == 2 && !semi)
        {
            return;
        }
        if (command.Words.Length != 3)
        {
            throw new InvalidDataException("Native variable TILE requires its original RGB, XY and WH words.");
        }
        int blend = resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native TILE requires its executed ABR.");
        NativeBattlePrimitiveColor diffuse = (frame.LastColorState ?? throw new NotSupportedException("Native TILE requires its retained PC color owner.")).Untextured(command.Words[0], semi, blend);
        ImmutableArray<NativeBattlePrimitiveDepthVertex> vertices = (frame.LastDepthState ?? throw new NotSupportedException("Native TILE requires its retained depth dispatch owner.")).Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native TILE lacks its original bucket."), false, command.Flags20, []);
        int x = unchecked((short)command.Words[1]);
        int y = unchecked((short)(command.Words[1] >> 16));
        int width = (ushort)command.Words[2];
        int height = (ushort)(command.Words[2] >> 16);
        float unit = (command.Flags20 & 1) != 0 ? 1 : 0.125f;
        PrimitiveVertex At(int px, int py) => new(px * unit * Width / 320, py * unit * Height / 216, vertices[0].Depth, 1, Vector2.Zero, diffuse.Rgb, diffuse.Alpha);
        PrimitiveVertex a = At(x, y);
        PrimitiveVertex b = At(x + width, y);
        PrimitiveVertex c = At(x, y + height);
        RasterNativePrimitive(a, b, c, null, semi ? blend : -1, resolved.Pass);
        RasterNativePrimitive(b, At(x + width, y + height), c, null, semi ? blend : -1, resolved.Pass);
    }

    private void RasterNativePrimitive(PrimitiveVertex a, PrimitiveVertex b, PrimitiveVertex c, PixelImage? texture, int blend, byte pass)
    {
        static float Edge(PrimitiveVertex first, PrimitiveVertex second, float x, float y) => (second.X - first.X) * (y - first.Y) - (second.Y - first.Y) * (x - first.X);
        static bool Inside(float value, PrimitiveVertex first, PrimitiveVertex second) => value > 0 || value == 0 && (second.Y < first.Y || second.Y == first.Y && second.X > first.X);
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
                    Rgba32 texel = texture[Math.Clamp((int)MathF.Floor(uv.X), 0, 255), Math.Clamp((int)MathF.Floor(uv.Y), 0, 255)];
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
