using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>50D300's retained actor BYTE and last selected target BYTE, consumed independently by50D790.</summary>
public sealed record NativeBattleCameraAliases([property: JsonRequired] byte? Actor1D99B95, [property: JsonRequired] byte? Target1D99AAA)
{
    public static NativeBattleCameraAliases FromColdProcess() => new(0, 0);

    public static NativeBattleCameraAliases Unknown() => new(null, null);

    public void Validate()
    {
        if (Actor1D99B95 >= 7 || Target1D99AAA >= 7)
        {
            throw new InvalidDataException("Native camera aliases require their actual physical model BYTEs.");
        }
    }

    public NativeBattleCameraAliases Observe50D300(byte actor, ushort targetMask)
    {
        if (actor >= 7 || (targetMask & ~127) != 0)
        {
            throw new InvalidDataException("Native50D300 requires a physical actor and seven-bit target mask.");
        }
        return this with { Actor1D99B95 = actor, Target1D99AAA = targetMask == 0 ? Target1D99AAA : checked((byte)BitOperations.Log2(targetMask)) };
    }

    public byte Resolve50D790(byte reference)
    {
        Validate();
        return (reference >> 4) switch
        {
            9 => Actor1D99B95 ?? throw new NotSupportedException("Native50D790 requires its retained actor BYTE; migrate the needed older snapshot explicitly."),
            10 => Target1D99AAA ?? throw new NotSupportedException("Native50D790 requires its retained target BYTE; migrate the needed older snapshot explicitly."),
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
    }
}
