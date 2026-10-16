using System.Runtime.InteropServices;

namespace Albeoris.Games.FF8.Imaging.Tim;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct TimHeader
{
    public const Int32 ExpectedMagic = 0x10;

    private readonly Int32 _magic;
    private readonly Int32 _value;

    public TimHeader(TimPixelMode pixelMode, TimFlags flags)
    {
        _magic = ExpectedMagic;
        _value = (Int32)pixelMode | (Int32)flags;
    }

    public Int32 Magic => _magic;

    public TimPixelMode PixelMode
    {
        get
        {
            TimPixelMode result = (TimPixelMode)(_value & (Int32)TimHeaderMask.PixelMode);
            return Enum.IsDefined(result)
                ? result
                : throw new InvalidDataException($"Unsupported TIM pixel mode {(Int32)result}.");
        }
    }

    public TimFlags Flags => (TimFlags)(_value & ~(Int32)TimHeaderMask.PixelMode);
    public Boolean HasClut => (Flags & TimFlags.HasClut) == TimFlags.HasClut;
}