namespace Memoria.FF8.Runtime;

public static class NativeBattleShockwavePulsar260Profile
{
    public const uint Entry = 0x690690;
    public const uint Initializer = 0x6906A0;
    public const uint InitializerGlobal = 0x2508610;
    public const uint PrimaryCursorGlobal = 0x250861C;
    public const uint SecondaryCursorGlobal = 0x2508620;
    public const uint ParametersGlobal = 0x2508624;
    public const uint CasterGlobal = 0x2508628;
    public const uint ChildDescriptor = 0x2508630;
    public const uint MainDescriptor = 0x2508640;
    public const uint RootDescriptor = 0x2508650;
    public const uint RootBank = 0x2508660;
    public const uint WorkingGlobal = 0x25086B8;
    public const uint CopiedViewAddress = 0x25086C0;
    public const uint SavedFlagsAddress = 0x25086E0;
    public const uint RootCallback = 0x690730;
    public const uint MainCallback = 0x690C20;
    public const uint TargetCallback = 0x6932C0;
    public const uint LateTargetCallback = 0x6934B0;
    public const uint ChildActCallback = 0x693720;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int MainOffset = 0x48;
    public const int MainStride = 0x1298;
    public const int PuffsOffset = 0x12E0;
    public const int PuffsCount = 128;
    public const int SecondaryParticlesOffset = 0x1AE0;
    public const int SecondaryParticlesCount = 64;
    public const int MorphOffset = 0x1EE0;
    public const int MorphBytes = 640;
    public const int ChildrenOffset = 0x2160;
    public const int ChildStride = 0x114;
    public const int ChildCount = 12;
    public const int StreamOffset = 0x2E50;
    public const int FirstPacketOffset = 0xB650;
    public const int SecondPacketOffset = 0x1B650;
    public const int PacketBankBytes = 0x10000;
}
