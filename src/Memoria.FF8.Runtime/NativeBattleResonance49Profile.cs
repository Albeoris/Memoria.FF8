using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleResonance49Profile
{
    public const ushort Effect = 49;
    public const uint Entry = 0x821540;
    public const uint Initializer = 0x821520;
    public const uint RootCallback = 0x821690;
    public const uint RootDescriptor = 0x26466C0;
    public const uint RootBank = 0x26465E8;
    public const int RootBankLength = 200;
    public const uint BaseAddress = 0x26464D8;
    public const uint InitializerGlobal = 0x26464D8;
    public const uint PrimaryCursorGlobal = 0x26464DC;
    public const uint SecondaryCursorGlobal = 0x26465DC;
    public const uint SharedViewAddress = 0x2793E58;
    public const uint EffectObjectPointer = 0x15ADAF0;
    public const uint EffectObjectAddress = 0x26478A0;
    public const int EffectObjectLength = 84;
    public const uint ScreenObjectPointer = 0x15ADC18;
    public const uint ScreenObjectAddress = 0x26466B0;
    public const int ScreenObjectLength = 16;
    public const uint GridAddress = 0x2647900;
    public const int GridLength = 1056;
    public const uint ScratchAddress = 0x2647D20;
    public const int ScratchLength = 960;
    public const int PacketBankBytes = 0xC000;
    public const uint ProgrammeAAddress = 0x17B329C;
    public const int ProgrammeALength = 0x32D0;
    public const int ProgrammeAWorkspaceLength = 724;
    public const uint ProgrammeBAddress = 0x17B656C;
    public const int ProgrammeBLength = 0x3238;
    public const int ProgrammeBWorkspaceLength = 712;
    public const uint CameraAddress = 0x15ADAF8;
    public const int CameraLength = 288;
    public const uint SoundAddress = 0x15ADAF4;
    public const uint TextureNameAddress = 0x15ADC1C;
    public const int TextureLength = 167968;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 100, 2), new(0x26475F0, 0x2647600, 88, 4), new(0x26465B8, 0x26464E0, 72, 3), new(0x26472C0, 0x2646BF0, 872, 2), new(0x26466D0, 0x26467E8, 128, 8), new(0x26475E0, 0x2647360, 64, 10)];
}
