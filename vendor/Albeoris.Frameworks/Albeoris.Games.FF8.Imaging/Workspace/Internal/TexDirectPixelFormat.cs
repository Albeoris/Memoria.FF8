using Albeoris.Games.FF8.Imaging.Tex;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class TexDirectPixelFormat
{
    public Int32 BytesPerPixel { get; init; }
    public required TexChannelFormat Red { get; init; }
    public required TexChannelFormat Green { get; init; }
    public required TexChannelFormat Blue { get; init; }
    public required TexChannelFormat Alpha { get; init; }

    public static TexDirectPixelFormat From(TexHeader header) => new()
    {
        BytesPerPixel = header.BytesPerPixel,
        Red = new TexChannelFormat(header.RedBits, header.RedMask, header.RedShift, header.RedBitsConstant, header.RedMaximum),
        Green = new TexChannelFormat(header.GreenBits, header.GreenMask, header.GreenShift, header.GreenBitsConstant, header.GreenMaximum),
        Blue = new TexChannelFormat(header.BlueBits, header.BlueMask, header.BlueShift, header.BlueBitsConstant, header.BlueMaximum),
        Alpha = new TexChannelFormat(header.AlphaBits, header.AlphaMask, header.AlphaShift, header.AlphaBitsConstant, header.AlphaMaximum),
    };

    public void Apply(ref TexHeader header)
    {
        header.BitsPerPixel = checked(BytesPerPixel * TexWorkspaceCodec.BitsPerByte);
        header.BytesPerPixel = BytesPerPixel;
        Red.ApplyRed(ref header);
        Green.ApplyGreen(ref header);
        Blue.ApplyBlue(ref header);
        Alpha.ApplyAlpha(ref header);
    }
}
