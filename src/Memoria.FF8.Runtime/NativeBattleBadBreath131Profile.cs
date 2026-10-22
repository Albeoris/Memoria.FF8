namespace Memoria.FF8.Runtime;

public static class NativeBattleBadBreath131Profile
{
    public const ushort Effect = 131;
    public const uint Entry = 0x6B2BD0;
    public const uint Initializer = 0x6B2BE0;
    public const uint RootCallback = 0x6B2C80;
    public const uint MainCallback = 0x6B2EC0;
    public const uint TargetCallback = 0x6B3D70;
    public const uint ParticleCallback = 0x6B40B0;
    public const uint PackedViewGlobal = 0x2520FF0;
    public const uint CasterGlobal = 0x2521010;
    public const uint InitializerGlobal = 0x2521038;
    public const uint WorkingGlobal = 0x252103C;
    public const uint ParametersGlobal = 0x2521040;
    public const uint RootBank = 0x2521048;
    public const uint CursorGlobal = 0x252105C;
    public const uint BlackGlobal = 0x2521060;
    public const uint ParticleDescriptor = 0x2521068;
    public const uint TargetDescriptor = 0x2521078;
    public const uint MainDescriptor = 0x2521088;
    public const uint RootDescriptor = 0x2521098;
    public const uint MeshAAddress = 0x1219258;
    public const uint MeshBAddress = 0x121A6D8;
    public const uint TargetActAddress = 0x121BB58;
    public const uint ParticleSpriteAddress = 0x1219078;
    public const uint CameraAddress = 0x121C7D4;
    public const uint ActorColorAddress = 0x121C98C;
    public const uint SoundAddress = 0x121C978;
    public const uint GradientAddress = 0x121C97C;
    public const uint TrigTableAddress = 0x121C990;
    public const uint ArenaAddress = 0x20DFAB8;
    public const ushort RootStride = 20;
    public const ushort MainStride = 52;
    public const ushort TargetStride = 324;
    public const ushort TargetCount = 3;
    public const ushort ParticleStride = 24;
    public const ushort ParticleCount = 64;
    public const int MainOffset = 0x9CC;
    public const int TargetOffset = 0x600;
    public const int ScratchOffset = 0xA00;
    public const int ScratchLength = 1320;
    public const int MorphOffset = ScratchOffset;
    public const int MorphBytes = ScratchLength;
    public const int FirstPacketOffset = 0xF28;
    public const int SecondPacketOffset = 0x18F28;
    public const int PacketBankBytes = 0x18000;
}
