using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private NativeBoatWorldFootRecord? scriptedBoatFootRecord;

    public NativeBoatWorldFootRecord ReadScriptedBoatFootRecord(ushort story)
    {
        NativeWorldFootAllocation allocation = ReadFootAllocation(1, story);
        if (scriptedBoatFootRecord is not null)
        {
            return scriptedBoatFootRecord;
        }
        ReadOnlySpan<byte> program = wmset.AsSpan(sections[9], sections[10] - sections[9]);
        ReadOnlySpan<byte> coordinate = wmset.AsSpan(sections[10], 16);
        var result = new NativeBoatWorldFootRecord(1, NativeBoatWorldFootRecord.AllocationRule, Convert.ToHexString(SHA256.HashData(program)), Convert.ToHexString(SHA256.HashData(coordinate)), allocation.MinimumStory, allocation.MaximumStory, allocation.Pose);
        result.Validate();
        scriptedBoatFootRecord = result;
        return result;
    }
}
