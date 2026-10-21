using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleCuragaRootMode
{
    CuragaFade,
    XPotion
}

public sealed class NativeBattleCuragaFamilyProfile
{
    private NativeBattleCuragaFamilyProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public NativeBattleCuragaRootMode RootMode => Effect == 28 ? NativeBattleCuragaRootMode.CuragaFade : NativeBattleCuragaRootMode.XPotion;
    public uint Entry => Effect == 28 ? 0x87DB10u : 0x817410u;
    public uint Initializer => Effect == 28 ? 0x87DAF0u : 0x8173F0u;
    public uint InitializerGlobal => Effect == 28 ? 0x26D75BCu : 0x2619494u;
    public uint AnimationAddress => Effect == 28 ? 0x160E0B0u : 0x15A9218u;
    public uint SoundAddress => Effect == 28 ? 0x160E0ACu : 0x15A9214u;
    public uint FlashAddress => Effect == 28 ? 0x160E0A0u : 0x15A9208u;
    public uint ExecutableAddress => Effect == 28 ? 0x17D5F4Cu : 0x17AB0E8u;
    public ImmutableArray<NativeBattleCureFamilyPool> Pools => Effect == 28
        ? [new(0x26D76B0, 0x26D75E8, 100, 2), new(0x26D8160, 0x26D8170, 88, 4), new(0x26D74F8, 0x26D7508, 60, 3), new(0x26D7EC0, 0x26D76D0, 676, 3), new(0x26D8150, 0x26D7ED0, 64, 10)]
        : [new(0x2619588, 0x26194C0, 100, 2), new(0x2619DA8, 0x2619DB8, 88, 4), new(0x26193D0, 0x26193E0, 60, 3), new(0x2619D98, 0x26195A8, 676, 3)];

    public static NativeBattleCuragaFamilyProfile ForEffect(ushort effect)
    {
        return effect is 28 or 52 ? new(effect) : throw new NotSupportedException("The Curaga family requires an original Curaga or XPotion module.");
    }

    public uint Callback(uint canonical)
    {
        uint potion = canonical switch
        {
            0x87DC50 => 0x817540,
            0x87DF70 => 0x817750,
            0x87E020 => 0x817800,
            0x87E1A0 => 0x817980,
            0x87DE50 or 0x882FC0 when Effect == 28 => canonical,
            _ => throw new NotSupportedException("The Curaga family callback has no original module identity.")
        };
        return Effect == 28 ? canonical : potion;
    }

    public uint CanonicalSimpleCallback(uint callback)
    {
        foreach (uint canonical in new uint[] { 0x87E020, 0x87DE50, 0x882FC0 })
        {
            if (canonical != 0x87E020 && RootMode == NativeBattleCuragaRootMode.XPotion)
            {
                continue;
            }
            if (Callback(canonical) == callback)
            {
                return canonical;
            }
        }
        throw new NotSupportedException("The Curaga family simple task belongs to another module.");
    }

    public NativeBattleCuragaExecutableState RequireExecutable(NativeBattleEffectMemory memory)
    {
        NativeBattleCuragaExecutableState retained = (Effect == 28 ? memory.CuragaExecutable : memory.Potion52Executable) ?? throw new NotSupportedException("The Curaga family requires its own retained executable bank.");
        retained.Validate();
        if (retained.ResourceAddress != ExecutableAddress)
        {
            throw new InvalidDataException("The Curaga family retained bank belongs to another module.");
        }
        return retained;
    }

    public NativeBattleEffectMemory WithExecutable(NativeBattleEffectMemory memory, NativeBattleCuragaExecutableState retained)
    {
        if (retained.ResourceAddress != ExecutableAddress)
        {
            throw new InvalidDataException("The Curaga family cannot publish another module's executable bank.");
        }
        return Effect == 28 ? memory with { CuragaExecutable = retained } : memory with { Potion52Executable = retained };
    }
}
