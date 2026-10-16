using System.Runtime.InteropServices;

namespace Albeoris.Games.FF8.Imaging.Tdw;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct TdwHeader
{
    public const Int32 Size = sizeof(Int32) * 2;

    private readonly Int32 _tableOffset;
    private readonly Int32 _dataOffset;

    public TdwHeader(Int32 tableOffset, Int32 dataOffset)
    {
        _tableOffset = tableOffset;
        _dataOffset = dataOffset;
    }

    public Int32 TableOffset => _tableOffset;
    public Int32 DataOffset => _dataOffset;
}