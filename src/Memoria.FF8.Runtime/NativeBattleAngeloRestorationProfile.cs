namespace Memoria.FF8.Runtime;

/// <summary>Explicit physical owners for the two parameterized restoration modules.</summary>
public sealed class NativeBattleAngeloRestorationProfile
{
    public static NativeBattleAngeloRestorationProfile Recover93 { get; } = new(93, 0x74E390, 0x74E370, 0x16B62B8, 0x21C8, 20, 70);
    public static NativeBattleAngeloRestorationProfile Reverse94 { get; } = new(94, 0x7475F0, 0x7475D0, 0x16AAD80, 0x2C00, 30, 90);
    private NativeBattleAngeloRestorationProfile(ushort effect, uint entry, uint initializer, uint sourceAddress, int sourceLength, int emitterCapacity, int geometryCapacity)
    {
        Effect = effect;
        Entry = entry;
        Initializer = initializer;
        SourceAddress = sourceAddress;
        SourceLength = sourceLength;
        EmitterCapacity = emitterCapacity;
        GeometryCapacity = geometryCapacity;
    }
    public ushort Effect { get; }
    public uint Entry { get; }
    public uint Initializer { get; }
    public uint SourceAddress { get; }
    public int SourceLength { get; }
    public int EmitterCapacity { get; }
    public int GeometryCapacity { get; }
    public int EmitterBytes => checked(EmitterCapacity * 0x6C);
    public int GeometryBytes => checked(GeometryCapacity * 0x2A0);
    public uint MainDescriptor => Global(0x2592728);
    public uint MainBank => Global(0x2592660);
    public uint ModelDescriptor => Global(0x2594D50);
    public uint ModelBank => Global(0x2594C10);

    public static NativeBattleAngeloRestorationProfile ForBinding(NativeBattleEffectBinding binding)
    {
        NativeBattleAngeloRestorationProfile profile = binding.Effect switch
        {
            93 => Recover93,
            94 => Reverse94,
            _ => throw new NotSupportedException("The restoration module profile is unsupported.")
        };
        if (binding.Entry != profile.Entry || binding.Initializer != profile.Initializer)
        {
            throw new InvalidDataException("The restoration module contradicts its original entry/initializer identity.");
        }
        return profile;
    }

    public uint Code(uint recoverAddress)
    {
        if (recoverAddress is not (0x74E520 or 0x74E780 or 0x74E8B0 or 0x7544E0 or 0x74EB70 or 0x754E60 or 0x754950))
        {
            throw new InvalidDataException("The restoration profile cannot relocate an unspecified private callback.");
        }
        return Effect == 93 ? recoverAddress : checked(recoverAddress - 0x6DA0);
    }

    public uint Global(uint recoverAddress)
    {
        if (recoverAddress is not (0x2592548 or 0x2592558 or 0x2592630 or 0x2592660 or 0x2592728 or 0x2592738 or 0x2592748 or 0x2593510 or 0x2593520 or 0x25937E0 or 0x2593AD8 or 0x2593AF0 or 0x2593D98 or 0x2593DA8 or 0x2594028 or 0x2594038 or 0x2594048 or 0x2594C10 or 0x2594D50))
        {
            throw new InvalidDataException("The restoration profile cannot relocate an unspecified private owner.");
        }
        return Effect == 93 ? recoverAddress : checked(recoverAddress - 0x2850);
    }

    public NativeBattleRecover93State? Read(NativeBattleEffectMemory memory) => Effect == 93 ? memory.Recover93 : memory.Reverse94;
    public NativeBattleEffectMemory Write(NativeBattleEffectMemory memory, NativeBattleRecover93State state) => Effect == 93 ? memory with { Recover93 = state } : memory with { Reverse94 = state };
}
