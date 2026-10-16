using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;

namespace Albeoris.Games.FF8.Imaging.MimMap;

public sealed class MimMapFile
{
    public MimMapFile(MimFile mim, MapFile map)
    {
        ArgumentNullException.ThrowIfNull(mim);
        ArgumentNullException.ThrowIfNull(map);
        Mim = mim;
        Map = map;
    }

    public MimFile Mim { get; }
    public MapFile Map { get; }

    public static MimMapFile Read(Byte[] mimContent, Byte[] mapContent)
    {
        ArgumentNullException.ThrowIfNull(mimContent);
        ArgumentNullException.ThrowIfNull(mapContent);
        return new MimMapFile(MimFile.Read(mimContent), MapFile.Read(mapContent));
    }
}
