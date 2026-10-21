using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRaldo19Profile
{
    public const ushort Effect = 19;
    public const uint Entry = 0x8A32A0;
    public const uint Initializer = 0x8A3280;
    public const uint InitializerGlobal = 0x2726900;
    public const uint RootCallback = 0x8A33F0;
    public const uint TargetCallback = 0x8A3620;
    public const uint DirectorCallback = 0x8A3760;
    public const uint GenericCallback = 0x8A3A30;
    public const uint BallCallback = 0x8A93E0;
    public const uint CameraAddress = 0x162A204;
    public const int CameraLength = 0x26C;
    public const uint ExecutableAddress = 0x17F37BC;
    public const int ExecutableLength = 0x4760;
    public const uint SharedAddress = 0x2730418;
    public const uint BallAddress = 0x2726910;
    public const uint BodyScratchAddress = 0x2726AC0;
    public const int BodyScratchLength = 0x4E30;
    public const uint DescriptorAddress = 0x27303E8;
    public const uint VertexScratchAddress = 0x272E0A0;
    public const int VertexScratchLength = 0x1F40;
    public const int DrawStep = 0x4000;
    public const string TextureName = "mag018.tim";

    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x272B9C8, 0x272B900, 100, 2), new(0x2730270, 0x2730280, 88, 4), new(0x2726AB0, 0x27269D8, 72, 3), new(0x272E090, 0x272DDE8, 676, 1), new(0x272B9D8, 0x272B9E8, 184, 50), new(0x2730260, 0x272FFE0, 64, 10)];
}
