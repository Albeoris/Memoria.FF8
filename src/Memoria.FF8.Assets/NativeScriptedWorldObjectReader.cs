using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private readonly Dictionary<ushort, NativeScriptedWorldFootRecord> scriptedFootRecords = [];

    public NativeScriptedWorldFootRecord ReadScriptedFootRecord(ushort story)
    {
        NativeWorldFootAllocation allocation = ReadFootAllocation(story is >= 34 and < 205 ? (byte)1 : (byte)0, story);
        if (scriptedFootRecords.TryGetValue(story, out NativeScriptedWorldFootRecord? retained))
        {
            return retained;
        }
        ReadOnlySpan<byte> program = wmset.AsSpan(sections[9], sections[10] - sections[9]);
        ReadOnlySpan<byte> coordinate = wmset.AsSpan(sections[10], 16);
        var result = new NativeScriptedWorldFootRecord(1, NativeScriptedWorldFootRecord.AllocationRule, Convert.ToHexString(SHA256.HashData(program)), Convert.ToHexString(SHA256.HashData(coordinate)), allocation.ShapeId, allocation.MinimumStory, allocation.MaximumStory, allocation.Pose);
        result.Validate();
        scriptedFootRecords.Add(story, result);
        return result;
    }
}
