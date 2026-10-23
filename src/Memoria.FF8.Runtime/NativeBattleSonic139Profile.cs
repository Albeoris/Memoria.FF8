namespace Memoria.FF8.Runtime;

public static class NativeBattleSonic139Profile
{
    public const ushort Effect = 139;
    public const uint Entry = 0x6A8830;
    public const uint Initializer = 0x6A8840;
    public const uint RootCallback = 0x6A88F0;
    public const uint MainCallback = 0x6A8AE0;
    public const uint TargetCallback = 0x6A98E0;
    public const uint BaseAddress = 0x2517B50;
    public const uint CasterGlobal = 0x2517B50;
    public const uint InitializerGlobal = 0x2517B54;
    public const uint WorkingGlobal = 0x2517B58;
    public const uint CopiedMeshAddress = 0x2517B5C;
    public const int EntryCopyLength = 0x9080;
    public const uint ParametersGlobal = 0x2520BDC;
    public const uint RootBank = 0x2520BE0;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2520BF4;
    public const uint LowerDescriptor = 0x2520BF8;
    public const uint MainDescriptor = 0x2520C08;
    public const uint RootDescriptor = 0x2520C18;
    public const uint MatrixAddress = 0x2520C28;
    public const uint SourceMeshAddress = 0x11D4E90;
    public const uint Frame0Address = 0x11D72B0;
    public const uint MutableFramesAddress = 0x11D7900;
    public const int FrameLength = 0x650;
    public const int MutableFramesLength = 3 * FrameLength;
    public const uint MainActAddress = 0x11DAB70;
    public const uint TargetActAddress = 0x11D8BF0;
    public const uint CameraAddress = 0x11DD21C;
    public const uint SoundAddress = 0x11DD338;
    public const uint AnchorAddress = 0x11DD340;
    public const uint SqrtLookupAddress = 0x11DD2C8;
    public const uint SineAddress = 0x11DD4C8;
    public const uint ColorsAddress = 0x11DF4C8;
    public const uint TextureNameAddress = 0x11DF4D4;
    public const int MainOffset = 0xA60;
    public const int MorphOffset = 0xF3C;
    public const int ScreenOffset = 0x1584;
    public const int FirstPacketOffset = 0x1EF0;
    public const int SecondPacketOffset = 0x11EF0;
    public const int PacketBankBytes = 0x10000;
    public const int VertexCount = 201;
}
