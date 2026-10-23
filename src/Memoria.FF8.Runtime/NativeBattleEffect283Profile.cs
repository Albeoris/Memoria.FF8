using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect283Profile
{
    public const ushort Effect = 283;
    public const uint Entry = 0x67C400;
    public const uint Initializer = 0x67C410;
    public const uint RootCallback = 0x67C4B0;
    public const uint MainCallback = 0x67C610;
    public const uint BaseAddress = 0x2507E20;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x2507E24;
    public const uint CasterGlobal = 0x2507E28;
    public const uint MainDescriptor = 0x2507E30;
    public const uint RootDescriptor = 0x2507E40;
    public const uint RootBank = 0x2507E50;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2507E64;
    public const uint ArenaGlobal = 0x2507E68;
    public const uint WorkingGlobal = ArenaGlobal;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int MainOffset = 0;
    public const int MainLength = 0x648;
    public const int ScratchOffset = 0x648;
    public const int ScratchLength = 512;
    public const int FirstPacketOffset = 0x650;
    public const int SecondPacketOffset = 0x10650;
    public const int PacketBankBytes = 0x10000;
    public const uint MainProgrammeAddress = 0x1095178;
    public const int MainProgrammeLength = 14496;
    public const int MainProgrammeWorkspaceOffset = 0x2C;
    public const int MainProgrammeWorkspaceLength = 0x4BC;
    public const uint SecondaryProgrammeAddress = 0x1098A18;
    public const int SecondaryProgrammeLength = 3184;
    public const int SecondaryProgrammeWorkspaceOffset = 0x4E8;
    public const int SecondaryProgrammeWorkspaceLength = 0x160;
    public const uint CameraAddress = 0x1094FC0;
    public const int CameraLength = 440;
    public const uint SoundAddress = 0x1099688;
    public const uint PaletteAddress = 0x109968C;
    public const uint WarpTablesAddress = 0x10997AC;
    public const int WarpTablesLength = 21377;
    public const int DistanceLength = 16513;
    public const uint TextureNameAddress = 0x109EAAC;
    public const int TextureLength = 100384;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(MainDescriptor, ArenaAddress, MainLength, 1)];
}
