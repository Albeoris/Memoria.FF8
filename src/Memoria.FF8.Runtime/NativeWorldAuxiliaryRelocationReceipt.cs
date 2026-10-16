using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWorldAuxiliaryRelocation([property: JsonRequired] int Channel, [property: JsonRequired] byte Selector, [property: JsonRequired] byte EntityType, [property: JsonRequired] NativeWorldPathPosition Position, [property: JsonRequired] short Yaw);

/// <summary>Executed544860→54D6A0/543770 channel consumption before the dominating548080/548300 publication.</summary>
public sealed record NativeWorldAuxiliaryRelocationReceipt([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] string ResourceHash, [property: JsonRequired] byte Before269, [property: JsonRequired] byte Before270, [property: JsonRequired] ImmutableArray<NativeWorldAuxiliaryRelocation> Relocations, [property: JsonRequired] WorldTransition Packet, [property: JsonRequired] NativeSavedWorldPose FinalLoadPose)
{
    public const string RelocationRule = "ff8-en-world-aux-relocation-544860-54d6a0-543770-548300-v1";

    public void Validate()
    {
        if (Version != 1 || Rule != RelocationRule || ResourceHash is not { Length: 64 } || !ResourceHash.All(Uri.IsHexDigit) || Before269 == 0 && Before270 == 0 || Relocations.IsDefaultOrEmpty || Relocations.Any(item => item is null) || Packet is null || FinalLoadPose is null)
        {
            throw new InvalidDataException("Native world auxiliary relocation lacks its executed selector, resource row or final loader publication.");
        }
        byte[] selectors = [Before269, Before270];
        int[] channels = Enumerable.Range(0, 2).Where(channel => selectors[channel] != 0).ToArray();
        if (!Relocations.Select(item => item.Channel).SequenceEqual(channels) || Relocations.Any(item => item.Selector != selectors[item.Channel] || item.Yaw is < sbyte.MinValue or > sbyte.MaxValue))
        {
            throw new InvalidDataException("Native auxiliary relocation changed its channel order, unsigned row selector or signed byte yaw.");
        }
    }

    public void RequireGardenPublication(WorldTransition packet, NativeSavedWorldPose loadPose)
    {
        Validate();
        if (Packet != packet || FinalLoadPose != loadPose || unchecked((byte)packet.Mode) is not (0 or 48) || Relocations.Any(item => item.EntityType != 48))
        {
            throw new NotSupportedException("Native auxiliary relocation requires its actual Garden48 target and controlled48 or parked-foot548080/548300 loader pose.");
        }
    }

    public NativeEventMemory ApplyTo(NativeEventMemory memory)
    {
        Validate();
        memory.Validate();
        if (memory.Read(269, 1).RequireExact() != Before269 || memory.Read(270, 1).RequireExact() != Before270)
        {
            throw new InvalidDataException("Native auxiliary relocation no longer matches the actual pending event269/270 selectors.");
        }
        NativeEventMemory result = memory;
        foreach (NativeWorldAuxiliaryRelocation relocation in Relocations)
        {
            result = result.Write(269 + relocation.Channel, 1, NativeScalarValue.Exact(0));
        }
        return result;
    }

    public void RequireRagnarokPublication(WorldTransition packet, NativeSavedWorldPose loadPose)
    {
        Validate();
        if (Packet != packet || FinalLoadPose != loadPose || unchecked((byte)packet.Mode) is not (0 or 50))
        {
            throw new NotSupportedException("Native auxiliary relocation requires its actual Ragnarok allocation packet and final548080/548300 loader pose.");
        }
    }

    public ImmutableArray<NativeRagnarokWorldObject> RelocateRagnarokObjects(ImmutableArray<NativeRagnarokWorldObject> objects)
    {
        Validate();
        var result = objects.ToBuilder();
        foreach (NativeWorldAuxiliaryRelocation relocation in Relocations)
        {
            int shape = relocation.EntityType switch
            {
                0 or 3 or 4 or 5 or 7 or 8 or 9 => 0,
                1 => 4,
                2 => 5,
                6 => 6,
                32 => 74,
                33 => 73,
                >= 34 and <= 40 => relocation.EntityType + 47,
                48 => 64,
                49 => 2,
                50 => 1,
                65 => 77,
                66 => 78,
                131 => 70,
                _ => -1
            };
            int[] targets = shape == 64 ? [64, 65] : [shape];
            foreach (int target in targets)
            {
                int index = -1;
                for (int candidate = 0; candidate < result.Count; candidate++)
                {
                    if (result[candidate].ShapeId == target)
                    {
                        index = candidate;
                        break;
                    }
                }
                if (index < 0)
                {
                    throw new NotSupportedException("Native54D6A0 relocation target has no actual allocated object; invalid-index native memory aliases are unsupported.");
                }
                result[index] = result[index] with { Position = relocation.Position, Yaw = relocation.Yaw };
            }
        }
        return result.ToImmutable();
    }
}
