using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private readonly Dictionary<int, NativeWorldTrainStation> trainStations = [];

    public NativeWorldTrainStation ReadTrainStation(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 2);
        if (!trainStations.TryGetValue(index, out NativeWorldTrainStation? station))
        {
            //542DA0 binds20402DC to the pointer at base+0x30: zero-based section12.
            //548AF0 reads X/rawZ/signed height; bytes10..11 are retained without interpreting them as a spawn yaw.
            ReadOnlySpan<byte> section = wmset.AsSpan(sections[12], sections[13] - sections[12]);
            if (section.Length != 40)
            {
                throw new InvalidDataException("Native train station section must contain its three original records and trailing dword.");
            }
            station = new NativeWorldTrainStation(index, Convert.ToHexString(SHA256.HashData(section)), section.Slice(index * 12, 12).ToArray().ToImmutableArray());
            station.Validate();
            trainStations.Add(index, station);
        }
        return station;
    }
}
