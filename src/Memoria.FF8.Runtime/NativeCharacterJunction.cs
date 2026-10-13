using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Original saved record GF WORD+88, command/ability bytes+80..87 and magic junction bytes+92..111.</summary>
public sealed record NativeCharacterJunction([property: JsonRequired] ushort GuardianForces, [property: JsonRequired] uint Commands, [property: JsonRequired] uint Abilities, [property: JsonRequired] byte[] MagicJunctions, [property: JsonRequired] ushort UnjunctionedMaximumHitPoints)
{
    [JsonIgnore]
    public IEnumerable<int> GuardianForceAssignments => Enumerable.Range(0, 16).Where(id => (GuardianForces & (1 << id)) != 0);

    public void Validate()
    {
        if (MagicJunctions is not { Length: 20 } || UnjunctionedMaximumHitPoints is 0 or > 9999)
        {
            throw new InvalidDataException("Native junction fields lack their original twenty magic bytes or proved unjunctioned maximum HP.");
        }
    }

    public NativeCharacterJunction Reset()
    {
        Validate();
        return this with { GuardianForces = 0, Commands = 0, Abilities = 0, MagicJunctions = new byte[20] };
    }

    public bool Equals(NativeCharacterJunction? other)
    {
        return other is not null && GuardianForces == other.GuardianForces && Commands == other.Commands && Abilities == other.Abilities && UnjunctionedMaximumHitPoints == other.UnjunctionedMaximumHitPoints && MagicJunctions.AsSpan().SequenceEqual(other.MagicJunctions);
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(GuardianForces);
        hash.Add(Commands);
        hash.Add(Abilities);
        hash.Add(UnjunctionedMaximumHitPoints);
        foreach (byte value in MagicJunctions)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }
}
