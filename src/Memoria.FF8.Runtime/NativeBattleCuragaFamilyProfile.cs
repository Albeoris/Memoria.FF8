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
    private ushort RootEffect => Effect is 28 or 52 ? Effect : throw new NotSupportedException("This generic node module requires its dedicated root controller.");
    public NativeBattleCuragaRootMode RootMode => RootEffect == 28 ? NativeBattleCuragaRootMode.CuragaFade : NativeBattleCuragaRootMode.XPotion;
    public uint Entry => RootEffect == 28 ? 0x87DB10u : 0x817410u;
    public uint Initializer => RootEffect == 28 ? 0x87DAF0u : 0x8173F0u;
    public uint InitializerGlobal => RootEffect == 28 ? 0x26D75BCu : 0x2619494u;
    public uint AnimationAddress => RootEffect == 28 ? 0x160E0B0u : 0x15A9218u;
    public uint SoundAddress => RootEffect == 28 ? 0x160E0ACu : 0x15A9214u;
    public uint FlashAddress => RootEffect == 28 ? 0x160E0A0u : 0x15A9208u;
    public uint ExecutableAddress => Effect switch { 81 => 0x1744A1C, 82 => 0x1741028, 16 => 0x17F7F1C, 61 => 0x1795000, 15 => 0x17FC68C, 28 => 0x17D5F4C, _ => 0x17AB0E8 };
    public int ExecutableLength => Effect switch { 81 => 0x5138, 82 => 0x39F4, 16 => 0x4770, 61 => 0x2CC0, 15 => 0x5944, _ => NativeBattleCuragaExecutableState.Length };
    public int EmitterCapacity => Effect is 16 or 61 or 81 or 82 ? 20 : Effect == 15 ? 10 : 90;
    public int GeometryCapacity => Effect is 81 or 82 ? 60 : Effect == 16 ? 90 : Effect == 61 ? 100 : Effect == 15 ? 40 : 140;
    public int EmitterStorageLength => EmitterCapacity * 108;
    public int GeometryStorageLength => GeometryCapacity * 672;
    public int DrawBankStep => Effect is 16 or 61 or 81 or 82 ? 0x8000 : Effect == 15 ? 0x9000 : 0x6000;
    public bool UsesCommonPacketCursor => Effect is 16 or 61 or 81 or 82;
    public ImmutableArray<NativeBattleCureFamilyPool> Pools => RootEffect == 28
        ? [new(0x26D76B0, 0x26D75E8, 100, 2), new(0x26D8160, 0x26D8170, 88, 4), new(0x26D74F8, 0x26D7508, 60, 3), new(0x26D7EC0, 0x26D76D0, 676, 3), new(0x26D8150, 0x26D7ED0, 64, 10)]
        : [new(0x2619588, 0x26194C0, 100, 2), new(0x2619DA8, 0x2619DB8, 88, 4), new(0x26193D0, 0x26193E0, 60, 3), new(0x2619D98, 0x26195A8, 676, 3)];

    public static NativeBattleCuragaFamilyProfile ForEffect(ushort effect)
    {
        return effect is 28 or 52 ? new(effect) : throw new NotSupportedException("The Curaga family requires an original Curaga or XPotion module.");
    }

    public static NativeBattleCuragaFamilyProfile ForGenericNodes(ushort effect)
    {
        return effect is 15 or 16 or 28 or 52 or 61 or 81 or 82 ? new(effect) : throw new NotSupportedException("The generic node family requires an implemented original module.");
    }

    public uint Callback(uint canonical)
    {
        if (Effect is 15 or 16 or 61 or 81 or 82)
        {
            uint callback = Effect == 81 ? 0x7A5940u : Effect == 82 ? 0x79F590u : Effect == 15 ? 0x8B0DA0u : Effect == 16 ? 0x8AB890u : 0x7E6010u;
            return canonical == 0x87E1A0 ? callback : throw new NotSupportedException("This node module requires its dedicated nongeneric callbacks.");
        }
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
        NativeBattleCuragaExecutableState retained = (Effect switch { 81 => memory.ActorDirector81Executable, 82 => memory.ActorDirector82Executable, 16 => memory.ActorDirector16Executable, 61 => memory.ActorDirector61Executable, 15 => memory.DrawStock15Executable, 28 => memory.CuragaExecutable, _ => memory.Potion52Executable }) ?? throw new NotSupportedException("The Curaga family requires its own retained executable bank.");
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
        retained.Validate();
        return Effect == 81 ? memory with { ActorDirector81Executable = retained } : Effect == 82 ? memory with { ActorDirector82Executable = retained } : Effect == 16 ? memory with { ActorDirector16Executable = retained } : Effect == 61 ? memory with { ActorDirector61Executable = retained } : Effect == 15 ? memory with { DrawStock15Executable = retained } : Effect == 28 ? memory with { CuragaExecutable = retained } : memory with { Potion52Executable = retained };
    }
}
