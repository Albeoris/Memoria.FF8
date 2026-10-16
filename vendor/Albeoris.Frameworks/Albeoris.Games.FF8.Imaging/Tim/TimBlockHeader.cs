using System.Runtime.InteropServices;

namespace Albeoris.Games.FF8.Imaging.Tim;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct TimBlockHeader
{
    public const Int32 Size = sizeof(Int32) + sizeof(Int16) * 4;

    private readonly Int32 _blockSize;
    private readonly Int16 _x;
    private readonly Int16 _y;
    private readonly Int16 _width;
    private readonly Int16 _height;

    public TimBlockHeader(Int32 blockSize, Int16 x, Int16 y, Int16 width, Int16 height)
    {
        _blockSize = blockSize;
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    public Int32 BlockSize => _blockSize;
    public Int32 ContentSize => checked(_blockSize - Size);
    public Int16 X => _x;
    public Int16 Y => _y;
    public Int16 Width => _width;
    public Int16 Height => _height;
}