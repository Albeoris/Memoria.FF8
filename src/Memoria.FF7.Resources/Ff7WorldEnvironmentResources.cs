using System.Numerics;
using System.Reflection.PortableExecutable;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldSkyZone(int Index, int X, int Z, int Enabled, int Radius, int Radius2, Vector3 UpperColor, Vector3 LowerColor);
public sealed record Ff7WorldSkyColors(Vector3 Upper, Vector3 Lower);

public sealed class Ff7WorldEnvironmentResources
{
    private Ff7WorldEnvironmentResources(PcTexture radar, PcTexture map, PcTexture clouds, PcTexture largeMap, PcTexture shadow, Vector3 upper, Vector3 lower)
    {
        Radar = radar;
        Map = map;
        Clouds = clouds;
        LargeMap = largeMap;
        Shadow = shadow;
        DefaultSkyColors = new(upper, lower);
    }

    public PcTexture Radar { get; }
    public PcTexture Map { get; }
    public PcTexture Clouds { get; }
    public PcTexture LargeMap { get; }
    public PcTexture Shadow { get; }
    public Ff7WorldSkyColors DefaultSkyColors { get; }
    public Vector4 ClearColor => new(0, 0, 0, 1);

    public static Ff7WorldEnvironmentResources Load(Ff7Installation installation, Ff7WorldResources resources, bool alternateMap = false)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.World != 0)
        {
            throw new NotSupportedException("FFVII surface environment requires the original overworld resource set.");
        }
        Ff7WorldSkyColors colors = Ff7PcExecutableData.LoadWorldSkyColors(installation);
        return new(PcTexture.Read(resources.ReadRequired("radar.tex")), PcTexture.Read(resources.ReadRequired("map.tex")), PcTexture.Read(resources.ReadRequired("wm_kumo.tex")), PcTexture.Read(resources.ReadRequired(alternateMap ? "midlmap2.tex" : "midlmap.tex")), PcTexture.Read(resources.ReadRequired("shadow.tex")), colors.Upper, colors.Lower);
    }

    public Ff7WorldSkyColors SkyColorsAt(Vector3 player, IEnumerable<Ff7WorldSkyZone> zones)
    {
        Ff7WorldSkyColors colors = DefaultSkyColors;
        foreach (Ff7WorldSkyZone zone in zones.OrderBy(value => value.Index))
        {
            if (zone.Enabled == 0)
            {
                continue;
            }
            int dx = Math.Abs((int)player.X - zone.X);
            int dz = Math.Abs((int)player.Z - zone.Z);
            if (dx >= 0x24000)
            {
                dx = 0x48000 - dx;
            }
            if (dz >= 0x1C000)
            {
                dz = 0x38000 - dz;
            }
            int ratio = Math.Max(0, dx + Math.Abs((int)player.Y) + dz - (zone.Radius2 << 10));
            int total = zone.Radius << 10;
            if (ratio < total)
            {
                Vector3 Blend(Vector3 source, Vector3 target)
                {
                    int Channel(float first, float second) => Math.Min(255, (ratio * (int)first + (total - ratio) * (int)second) / total);
                    return new(Channel(source.X, target.X), Channel(source.Y, target.Y), Channel(source.Z, target.Z));
                }
                colors = new(Blend(DefaultSkyColors.Upper, zone.UpperColor), Blend(DefaultSkyColors.Lower, zone.LowerColor));
            }
        }
        return colors;
    }
}

public sealed partial class Ff7PcExecutableData
{
    public static Ff7WorldSkyColors LoadWorldSkyColors(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] upper = ReadVirtual(stream, pe, 0x969B58, 3);
        byte[] lower = ReadVirtual(stream, pe, 0x969B5C, 3);
        return new(new(upper[0], upper[1], upper[2]), new(lower[0], lower[1], lower[2]));
    }
}
