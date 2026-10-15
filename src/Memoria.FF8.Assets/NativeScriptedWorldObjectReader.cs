using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private readonly Dictionary<(ushort Story, byte EventFlags265), NativeScriptedWorldFootRecord> scriptedFootRecords = [];

    public NativeScriptedWorldFootRecord ReadScriptedFootRecord(ushort story, byte eventFlags265)
    {
        if (scriptedFootRecords.TryGetValue((story, eventFlags265), out NativeScriptedWorldFootRecord? retained))
        {
            return retained;
        }
        NativeTrainWorldAllocation allocation = ReadTrainWorldAllocation(story, eventFlags265);
        if (allocation.Objects.Length != 1 || allocation.ControlledObjectIndex != 0 || allocation.Objects[0].ShapeId is not (0 or 4) || allocation.Objects[0].CoordinateIndex != 0)
        {
            throw new NotSupportedException("Native scripted world requires the selected single coordinate0 foot allocation; additional dense records need their actual object contract.");
        }
        NativeTrainWorldObject foot = allocation.Objects[0];
        var result = new NativeScriptedWorldFootRecord(1, NativeScriptedWorldFootRecord.AllocationRule, allocation.ProgramHash, Convert.ToHexString(SHA256.HashData(foot.CoordinateBytes.AsSpan())), foot.ShapeId, allocation.MinimumStory, allocation.MaximumStory, foot.Pose);
        result.Validate();
        scriptedFootRecords.Add((story, eventFlags265), result);
        return result;
    }
}
