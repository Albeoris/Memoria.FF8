using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class NativeBattleDrainFamilyProfile
{
    private NativeBattleDrainFamilyProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public uint Entry => Effect == 39 ? 0x8513D0u : 0x803200u;
    public uint Initializer => Effect == 39 ? 0x8513B0u : 0x8031E0u;
    public uint InitializerGlobal => Effect == 39 ? 0x269DA1Cu : 0x260E3FCu;
    public uint RootCallback => Effect == 39 ? 0x851510u : 0x803340u;
    public uint TargetCallback => Effect == 39 ? 0x851830u : 0x803660u;
    public uint FlashCallback => Effect == 39 ? 0x851900u : 0x803730u;
    public uint GenericCallback => Effect == 39 ? 0x851A80u : 0x8038B0u;
    public uint StageInCallback => Effect == 39 ? 0x851710u : 0x803540u;
    public uint StageOutCallback => Effect == 39 ? 0x8568A0u : 0x8086D0u;
    public uint BankAddress => Effect == 39 ? 0x15D193Cu : 0x159EF34u;
    public int BankLength => 0x4258;
    public uint DirectoryAddress => Effect == 39 ? 0x15D5A90u : 0x15A3088u;
    public uint ChannelsAddress => Effect == 39 ? 0x269E764u : 0x260F144u;
    public int ChannelsLength => 0x1E60;
    public uint FirstFlashAddress => Effect == 39 ? 0x15D190Cu : 0x159EF04u;
    public uint SecondFlashAddress => Effect == 39 ? 0x15D1918u : 0x159EF10u;
    public ImmutableArray<NativeBattleCureFamilyPool> Pools => Effect == 39
        ? [new(0x269DB10, 0x269DA48, 100, 2), new(0x269E5C0, 0x269E5D0, 88, 4), new(0x269D958, 0x269D968, 60, 3), new(0x269E320, 0x269DB30, 676, 3), new(0x269E5B0, 0x269E330, 64, 10)]
        : [new(0x260E4F0, 0x260E428, 100, 2), new(0x260EFA0, 0x260EFB0, 88, 4), new(0x260E338, 0x260E348, 60, 3), new(0x260ED00, 0x260E510, 676, 3), new(0x260EF90, 0x260ED10, 64, 10)];

    public static NativeBattleDrainFamilyProfile ForEffect(ushort effect) => effect switch
    {
        39 => new(39),
        56 => new(56),
        _ => throw new NotSupportedException("The siphon family requires its original supported module.")
    };

    public NativeBattleDrain39BankState RequireBank(NativeBattleEffectMemory memory)
    {
        NativeBattleDrain39BankState bank = (Effect == 39 ? memory.Drain39Bank : memory.Absorb56Bank) ?? throw new NotSupportedException("The siphon family requires its actual retained addressed bank.");
        bank.Validate();
        return bank;
    }

    public NativeBattleEffectMemory WithBank(NativeBattleEffectMemory memory, NativeBattleDrain39BankState bank)
    {
        bank.Validate();
        return Effect == 39 ? memory with { Drain39Bank = bank } : memory with { Absorb56Bank = bank };
    }
}
