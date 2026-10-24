using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect344Profile
{
    public const ushort Effect = 344;
    public const uint Entry = 0x62A650;
    public const uint Initializer = 0x575580;
    public const uint RootCallback = 0x62A7A0;
    public const uint ChildCallback = 0x62A6D0;
    public const uint MainCallback = ChildCallback;
    public const uint BaseAddress = 0x24DFD70;
    public const uint TargetGlobal = BaseAddress;
    public const uint CompletionGlobal = 0x24DFD98;
    public const uint ParametersGlobal = 0x24E0BC0;
    public const uint CasterGlobal = 0x24E0BC4;
    public const uint CursorGlobal = 0x24F0BC8;
    public const uint RootDescriptor = 0x24DFD88;
    public const uint RootBank = 0x24DFD78;
    public const uint ChildDescriptor = 0x24DFDA0;
    public const uint ChildBank = 0x24DFDB0;
    public const uint MusicAddress = 0xDEE388;
    public const int MusicLength = 140112;
    public const uint CameraAddress = 0xE106D8;
    public const int CameraLength = 520;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 1), new(ChildDescriptor, ChildBank, 36, 100)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(TargetGlobal, 4), (CompletionGlobal, 1), (ParametersGlobal, 8), (CursorGlobal, 4)];
}
