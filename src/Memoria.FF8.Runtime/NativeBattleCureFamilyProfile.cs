using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCureFamilyPool(uint Descriptor, uint Bank, ushort Stride, ushort Capacity);

/// <summary>Original Cure and Potion task, resource and retained-context identities.</summary>
public sealed class NativeBattleCureFamilyProfile
{
    private NativeBattleCureFamilyProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public uint Entry => Effect == 1 ? 0x8D6A00u : 0x81FA10u;
    public uint Initializer => Effect == 1 ? 0x8D69E0u : 0x81F9F0u;
    public uint InitializerGlobal => Effect == 1 ? 0x277AEC0u : 0x2634AB8u;
    public uint AnimationAddress => Effect == 1 ? 0x16417C8u : 0x15ACD7Cu;
    public uint SoundAddress => Effect == 1 ? 0x16417C4u : 0x15ACD78u;
    public uint ContextF4Address => Effect == 1 ? 0x278C8DCu : 0x26464D4u;
    public string TextureName => "mag000-049.tim";
    public ImmutableArray<NativeBattleCureFamilyPool> Pools => Effect == 1
        ? [new(0x277AFC0, 0x277AEF8, 100, 2), new(0x278A228, 0x278C628, 88, 4), new(0x277BF70, 0x277BF80, 108, 300), new(0x278C788, 0x278A238, 92, 100)]
        : [new(0x2634BB8, 0x2634AF0, 100, 2), new(0x2643E20, 0x2646220, 88, 4), new(0x2635B68, 0x2635B78, 108, 300), new(0x2646380, 0x2643E30, 92, 100)];

    public static NativeBattleCureFamilyProfile ForEffect(ushort effect)
    {
        return effect is 1 or 50 ? new(effect) : throw new NotSupportedException("The Cure family requires an original Cure or Potion module.");
    }

    public uint Callback(uint cureCallback)
    {
        uint potion = cureCallback switch
        {
            0x8D6B30 => 0x81FB40,
            0x8D6CC0 => 0x81FCD0,
            0x8D6D80 => 0x81FD90,
            0x8D7F60 => 0x820F70,
            0x8D80B0 => 0x8210C0,
            0x8DCC20 => 0x8DCC20,
            _ => throw new NotSupportedException("The Cure family callback has no original task identity.")
        };
        return Effect == 1 ? cureCallback : potion;
    }

    public uint CanonicalParticleCallback(uint callback)
    {
        foreach (uint canonical in new uint[] { 0x8D6D80, 0x8D7F60, 0x8D80B0, 0x8DCC20 })
        {
            if (Callback(canonical) == callback)
            {
                return canonical;
            }
        }
        throw new NotSupportedException("The Cure family particle callback belongs to another module.");
    }

    public uint Sprite(uint cureAddress)
    {
        uint potion = cureAddress switch
        {
            0x1641F78 => 0x15AD52C,
            0x1641FCC => 0x15AD580,
            0x164205C => 0x15AD610,
            0x1642238 => 0x15AD7EC,
            0x1642504 => 0x15ADAB8,
            _ => throw new NotSupportedException("The Cure family sprite has no original resource identity.")
        };
        return Effect == 1 ? cureAddress : potion;
    }
}
