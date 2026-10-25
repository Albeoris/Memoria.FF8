using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Profile
{
    public const ushort Effect = 31;
    public const uint Initializer = 0x86C9C0;
    public const uint Entry = 0x86C9E0;
    public const uint RootCallback = 0x86CB50;
    public const uint BaseAddress = 0x26CDCC8;
    public const uint CursorGlobal = 0x26CDD90;
    public const uint AuxiliaryCursorGlobal = 0x26CDDAC;
    public const uint ViewGlobal = 0x26CDDB0;
    public const uint RootDescriptor = 0x26CDE80;
    public const uint RootBank = 0x26CDDB8;
    public const uint EmitterIndexGlobal = 0x26D10A0;
    public const uint ParticleIndexGlobal = 0x26D2DB8;
    public const uint EmitterBankGlobal = 0x26D2DAC;
    public const uint ParticleBankGlobal = 0x26D33D4;
    public const uint ScratchCursorGlobal = 0x26D33D0;
    public const uint AllocationCursorGlobal = 0x26D33DC;
    public const uint ContextGlobal = 0x26D33E0;
    public const uint MorphScratch = 0x26D2DC0;
    public const int MorphScratchLength = 1544;
    public const int EmitterStride = 108;
    public const int EmitterCapacity = 10;
    public const int ParticleStride = 672;
    public const int ParticleCapacity = 50;
    public const int StorageLength = 0x571C;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 0x64, 2), new(0x26D2C30, 0x26D2C40, 0x58, 4), new(0x26CDCC8, 0x26CDCD8, 0x3C, 3), new(0x26D2990, 0x26D21A0, 0x2A4, 3), new(0x26D2188, 0x26D10A8, 0x1B0, 10), new(0x26CDE90, 0x26CDEA0, 0x80, 100), new(0x26D2C20, 0x26D29A0, 0x40, 10)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = Pools;
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(InitializerGlobal, 10), (AuxiliaryCursorGlobal, 8), (EmitterIndexGlobal, 2), (0x26D10A4, 4), (0x26D2198, 8), (0x26D2DA0, 16), (ParticleIndexGlobal, 2), (MorphScratch, MorphScratchLength), (0x26D33C8, 2), (ScratchCursorGlobal, 20)];
}
