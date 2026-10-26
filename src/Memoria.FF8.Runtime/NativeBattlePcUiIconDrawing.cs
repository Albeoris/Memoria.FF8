using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The ordinary formatted-icon routes49D6F0 and49B300, separate from battle ordering tables.</summary>
public static class NativeBattlePcUiIconDrawing
{
    public static NativeBattlePcUiImmediateState Submit49BB30(NativeBattlePcUiImmediateState state, ReadOnlySpan<byte> packet, NativeBattlePcUiIconProfile opaque, NativeBattlePcUiIconProfile semi, NativeBattlePcUiRules rules)
    {
        if (packet.Length != 24 || BinaryPrimitives.ReadUInt32LittleEndian(packet) != 0x05000000 || (BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]) & 0xFF000000) != 0xE1000000 || packet[11] is not (0x64 or 0x66))
        {
            throw new InvalidDataException("Native49BB30 requires one complete formatted ordinary icon part.");
        }
        if (state.Suppressed ?? throw new NotSupportedException("Native PC UI requires its retained suppression BYTE."))
        {
            return state;
        }
        NativeBattlePcUiProfile profile = state.Profile ?? throw new NotSupportedException("Native PC UI requires its initialized device profile.");
        NativeBattlePcUiViewport viewport = state.Viewport ?? throw new NotSupportedException("Native PC UI requires its actual viewport owner.");
        bool grouped = state.Grouped ?? throw new NotSupportedException("Native PC UI requires its retained grouping BYTE.");
        float depth = state.Depth ?? throw new NotSupportedException("Native PC UI requires its retained depth FLOAT.");
        float offsetX = state.OffsetX ?? throw new NotSupportedException("Native PC UI requires its retained horizontal offset.");
        float offsetY = state.OffsetY ?? throw new NotSupportedException("Native PC UI requires its retained vertical offset.");
        float translationX = state.TranslationX ?? throw new NotSupportedException("Native PC UI requires its retained horizontal translation.");
        float translationY = state.TranslationY ?? throw new NotSupportedException("Native PC UI requires its retained vertical translation.");
        bool translucent = packet[11] == 0x66;
        NativeBattlePcUiIconProfile texture = translucent ? semi : opaque;
        if (texture.PageWidth <= 0 || texture.PageHeight <= 0 || texture.PageColumns is < 1 or > 2 || texture.PageRows is < 1 or > 2 || !float.IsFinite(texture.TextureScaleX) || !float.IsFinite(texture.TextureScaleY) || texture.TextureScaleX <= 0 || texture.TextureScaleY <= 0 || rules.PageExtent != 256)
        {
            throw new InvalidDataException("Native PC icon drawing requires its initialized texture-page profile.");
        }
        short x = BinaryPrimitives.ReadInt16LittleEndian(packet[12..]);
        short y = BinaryPrimitives.ReadInt16LittleEndian(packet[14..]);
        short width = BinaryPrimitives.ReadInt16LittleEndian(packet[20..]);
        short height = BinaryPrimitives.ReadInt16LittleEndian(packet[22..]);
        float screenWidth = (float)((double)width * profile.ScaleX);
        float screenHeight = (float)((double)height * profile.ScaleY);
        float startX = translucent
            ? (float)(((double)x + offsetX) * profile.ScaleX + profile.TranslationX + translationX)
            : Add((float)(((double)x + offsetX) * profile.ScaleX + profile.TranslationX), translationX);
        float startY = translucent
            ? (float)(((double)y + offsetY) * profile.ScaleY + profile.TranslationY + translationY)
            : Add((float)(((double)y + offsetY) * profile.ScaleY + profile.TranslationY), translationY);
        float u = translucent ? (float)(packet[16] * (double)texture.TextureScaleX) : (int)(packet[16] * (double)texture.TextureScaleX);
        float v = translucent ? (float)(packet[17] * (double)texture.TextureScaleY) : (int)(packet[17] * (double)texture.TextureScaleY);
        float textureWidth = !translucent && texture.ViewportScaledTextureExtent ? screenWidth : (float)(width * (double)texture.TextureScaleX);
        float textureHeight = !translucent && texture.ViewportScaledTextureExtent ? screenHeight : (float)(height * (double)texture.TextureScaleY);
        ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(packet[18..]);
        int palette = (clut >> 6) - texture.OriginY + ((clut & 63) * 16 == texture.OriginX ? 0 : 16);
        uint color = 0x7F000000u | (uint)(Math.Min(packet[8] * 2, 255) << 16) | (uint)(Math.Min(packet[9] * 2, 255) << 8) | (uint)Math.Min(packet[10] * 2, 255);
        state = state with { Depth = grouped ? depth : Add(depth, rules.DepthStep) };
        if (translucent)
        {
            float endX = (float)((double)width * profile.ScaleX + (((double)x + offsetX) * profile.ScaleX + profile.TranslationX + translationX) + translationX);
            float endY = (float)((double)height * profile.ScaleY + startY + translationY);
            NativeBattlePcUiQuad quad = Quad(0, startX, startY, endX, endY, u, v, Add(u, textureWidth), Add(v, textureHeight));
            return state with { CurrentSemi = state.CurrentSemi.Add(quad) };
        }
        ImmutableArray<NativeBattlePcUiQuad>.Builder quads = ImmutableArray.CreateBuilder<NativeBattlePcUiQuad>();
        foreach (Segment vertical in Split(startY, screenHeight, v, textureHeight, texture.PageRows))
        {
            foreach (Segment horizontal in Split(startX, screenWidth, u, textureWidth, texture.PageColumns))
            {
                quads.Add(Quad(vertical.Page * texture.PageColumns + horizontal.Page, horizontal.Start, vertical.Start, horizontal.End, vertical.End, horizontal.UvStart, vertical.UvStart, horizontal.UvEnd, vertical.UvEnd));
            }
        }
        return state with { Opaque = state.Opaque.AddRange(quads) };

        NativeBattlePcUiQuad Quad(int page, float left, float top, float right, float bottom, float firstU, float firstV, float lastU, float lastV)
        {
            float bias = rules.HalfPixel;
            return new(page, palette, color,
            [
                new(Add(left, -bias), Add(top, -bias), depth, firstU / texture.PageWidth, firstV / texture.PageHeight),
                new(Add(left, -bias), Add(bottom, -bias), depth, firstU / texture.PageWidth, lastV / texture.PageHeight),
                new(Add(right, -bias), Add(top, -bias), depth, lastU / texture.PageWidth, firstV / texture.PageHeight),
                new(Add(right, -bias), Add(bottom, -bias), depth, lastU / texture.PageWidth, lastV / texture.PageHeight)
            ], viewport, state.Epoch);
        }
    }

    private readonly record struct Segment(int Page, float Start, float End, float UvStart, float UvEnd);

    private static ImmutableArray<Segment> Split(float start, float extent, float uv, float uvExtent, int pages)
    {
        int page = uv >= 256 ? 1 : 0;
        if (page >= pages)
        {
            throw new NotSupportedException("Native icon drawing selected a texture page that its resource profile does not own.");
        }
        if (page == 1 || (double)uv + uvExtent <= 256)
        {
            return [new(page, start, Add(start, extent), Add(uv, -256 * page), Add(Add(uv, -256 * page), uvExtent))];
        }
        if (pages != 2)
        {
            throw new NotSupportedException("Native icon drawing crossed into an unallocated texture page.");
        }
        double overflow = (double)uvExtent - (255d - uv) - 1;
        float boundary = (float)(start + (extent - overflow));
        return [new(0, start, boundary, uv, 256), new(1, boundary, Add(start, extent), 0, (float)overflow)];
    }

    private static float Add(float left, float right) => NativeBattlePcUiImmediateState.Add(left, right);
}
