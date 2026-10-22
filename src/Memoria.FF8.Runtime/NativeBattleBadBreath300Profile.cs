namespace Memoria.FF8.Runtime;

public static class NativeBattleBadBreath300Profile
{
    public const uint Entry = 0x66EF00;
    public const uint Initializer = 0x66EF10;
    public const uint RootCallback = 0x66EFB0;
    public const uint MainCallback = 0x66F270;
    public const uint TargetCallback = 0x66F790;
    public const uint ParticleCallback = 0x66FB20;
    public const uint PackedViewGlobal = 0x2507758;
    public const uint CasterGlobal = 0x2507778;
    public const uint InitializerGlobal = 0x25077A0;
    public const uint WorkingGlobal = 0x25077A4;
    public const uint ParametersGlobal = 0x25077A8;
    public const uint RootBank = 0x25077B0;
    public const uint CursorGlobal = 0x25077C4;
    public const uint BlackGlobal = 0x25077C8;
    public const uint ParticleDescriptor = 0x25077D0;
    public const uint TargetDescriptor = 0x25077E0;
    public const uint MainDescriptor = 0x25077F0;
    public const uint RootDescriptor = 0x2507800;
    public const uint MeshAAddress = 0x103FA04;
    public const uint MeshBAddress = 0x1040E84;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int BindingOffset = 0x83C;
    public const int ScratchOffset = 0x85C;
    public const int ScratchLength = 1320;
    public const int MorphOffset = ScratchOffset;
    public const int MorphBytes = ScratchLength;
    public const int FirstPacketOffset = 0xD84;
    public const int SecondPacketOffset = 0x18D84;
    public const int PacketBankBytes = 0x18000;
}
