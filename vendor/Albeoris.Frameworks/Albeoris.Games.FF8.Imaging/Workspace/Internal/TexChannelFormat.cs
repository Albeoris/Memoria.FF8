using Albeoris.Games.FF8.Imaging.Tex;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed record TexChannelFormat(
    Int32 Bits,
    Int32 Mask,
    Int32 Shift,
    Int32 ConstantBits,
    Int32 Maximum)
{
    public void ApplyRed(ref TexHeader header)
    {
        header.RedBits = Bits;
        header.RedMask = Mask;
        header.RedShift = Shift;
        header.RedBitsConstant = ConstantBits;
        header.RedMaximum = Maximum;
    }

    public void ApplyGreen(ref TexHeader header)
    {
        header.GreenBits = Bits;
        header.GreenMask = Mask;
        header.GreenShift = Shift;
        header.GreenBitsConstant = ConstantBits;
        header.GreenMaximum = Maximum;
    }

    public void ApplyBlue(ref TexHeader header)
    {
        header.BlueBits = Bits;
        header.BlueMask = Mask;
        header.BlueShift = Shift;
        header.BlueBitsConstant = ConstantBits;
        header.BlueMaximum = Maximum;
    }

    public void ApplyAlpha(ref TexHeader header)
    {
        header.AlphaBits = Bits;
        header.AlphaMask = Mask;
        header.AlphaShift = Shift;
        header.AlphaBitsConstant = ConstantBits;
        header.AlphaMaximum = Maximum;
    }
}
