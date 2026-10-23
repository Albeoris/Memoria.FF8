using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleStare45Profile
{
    public const ushort Effect = 45;
    public const uint Entry = 0x836590;
    public const uint Initializer = 0x836570;
    public const uint RootCallback = 0x8366E0;
    public const uint RootDescriptor = 0x2654588;
    public const uint RootBank = 0x26544C0;
    public const int RootBankLength = 200;
    public const uint BaseAddress = 0x26543B0;
    public const uint InitializerGlobal = 0x26543B0;
    public const uint PrimaryCursorGlobal = 0x26543B4;
    public const uint SecondaryCursorGlobal = 0x26544B4;
    public const uint SharedViewAddress = 0x2793E58;
    public const uint EffectObjectPointer = 0x15C357C;
    public const uint EffectObjectAddress = 0x26553C8;
    public const int EffectObjectLength = 84;
    public const uint ScratchAddress = 0x2655428;
    public const int ScratchLength = 800;
    public const uint BasisAddress = 0x2655758;
    public const int BasisLength = 32;
    public const int PacketBankBytes = 0xC000;
    public const uint ProgrammeAAddress = 0x17C4DA0;
    public const int ProgrammeALength = 0x1C74;
    public const int ProgrammeAWorkspaceLength = 452;
    public const uint ProgrammeBAddress = 0x17C6A14;
    public const int ProgrammeBLength = 0x1A0C;
    public const int ProgrammeBWorkspaceLength = 572;
    public const uint CameraAddress = 0x15C3584;
    public const int CameraLength = 236;
    public const uint SoundAddress = 0x15C3580;
    public const uint TextureNameAddress = 0x15C3670;
    public const int TextureLength = 167968;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 100, 2), new(0x2655250, 0x2655260, 88, 4), new(0x2654490, 0x26543B8, 72, 3), new(0x2654FA0, 0x2654730, 720, 3), new(0x2654598, 0x26545A8, 48, 8), new(0x2655240, 0x2654FC0, 64, 10)];
}
