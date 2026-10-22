namespace Memoria.FF8.Runtime;

public static class NativeBattleOilBlast137Profile
{
    public const ushort Effect = 137;
    public const uint Entry = 0x6AB850;
    public const uint Initializer = 0x6AB860;
    public const uint RootCallback = 0x6AB900;
    public const uint MainCallback = 0x6ABA50;
    public const uint ParticleCallback = 0x6AC140;
    public const uint SecondaryCallback = 0x6AC1F0;
    public const uint ActCallback = 0x6ACF40;
    public const uint BaseAddress = 0x2520CC0;
    public const int GlobalsLength = 0x68;
    public const uint CasterGlobal = 0x2520CC0;
    public const uint InitializerGlobal = 0x2520CC4;
    public const uint WorkingGlobal = 0x2520CC8;
    public const uint ParametersGlobal = 0x2520CCC;
    public const uint RootBank = 0x2520CD0;
    public const uint CursorGlobal = 0x2520CE4;
    public const uint ParticleDescriptor = 0x2520CE8;
    public const uint SecondaryDescriptor = 0x2520CF8;
    public const uint MainDescriptor = 0x2520D08;
    public const uint RootDescriptor = 0x2520D18;
    public const ushort RootStride = 20;
    public const ushort ParticleStride = 36;
    public const ushort ParticleCount = 60;
    public const ushort SecondaryStride = 44;
    public const ushort SecondaryCount = 32;
    public const ushort MainStride = 0x5A0;
    public const int SecondaryOffset = 0x870;
    public const int MainOffset = 0xDF0;
    public const int MorphOffset = 0x1390;
    public const int MorphBytes = 712;
    public const int FirstPacketOffset = 0x1658;
    public const int SecondPacketOffset = 0x11658;
    public const int PacketStep = 0x10000;
    public const uint OpeningActAddress = 0x11EAE10;
    public const uint MainActAddress = 0x11EB4D8;
    public const uint ImpactActAddress = 0x11ECDC0;
    public const uint SpriteAddress = 0x11E91A0;
    public const uint CustomMeshAddress = 0x11E92F8;
    public const uint StaticMeshAddress = 0x11EACC0;
    public const uint CameraAddress = 0x11F0108;
    public const int CameraLength = 0x254;
    public const uint SoundAddress = 0x11F035C;
    public const uint MatrixAddress = 0x11F0360;
    public const uint TextureNameAddress = 0x11F2384;
}
