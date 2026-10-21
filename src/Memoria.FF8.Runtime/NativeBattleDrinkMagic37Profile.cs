using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDrinkMagic37Profile
{
    public const ushort Effect = 37;
    public const uint Entry = 0x8569F0;
    public const uint Initializer = 0x8569D0;
    public const uint InitializerGlobal = 0x26A05C8;
    public const uint RootCallback = 0x856B40;
    public const uint TargetCallback = 0x856D70;
    public const uint GenericCallback = 0x856E90;
    public const uint ParticleCallback = 0x85BBD0;
    public const uint OrbCallback = 0x85C1F0;
    public const uint CameraAddress = 0x15D8EB4;
    public const int CameraLength = 0x440;
    public const uint ExecutableAddress = 0x17C8420;
    public const int ExecutableLength = 0x430C;
    public const uint OrbAddress = 0x15D62EC;
    public const int OrbLength = 0x2320;
    public const uint ShapeAddress = 0x15D860C;
    public const int ShapeLength = 0x8A8;
    public const int DrawStep = 0x9000;
    public const string TextureName = "mag036.tim";

    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x26A06B8, 0x26A05F0, 100, 2), new(0x26A8C08, 0x26A8C18, 88, 4), new(0x26A8968, 0x26A8178, 676, 3), new(0x26A06C8, 0x26A06D8, 304, 100), new(0x26A7DA0, 0x26A7DB0, 96, 10), new(0x26A8BF8, 0x26A8978, 64, 10)];
}
