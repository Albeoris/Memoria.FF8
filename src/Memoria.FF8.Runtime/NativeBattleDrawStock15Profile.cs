using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDrawStock15Profile
{
    public const ushort Effect = 15;
    public const uint Entry = 0x8B0910;
    public const uint Initializer = 0x8B08F0;
    public const uint InitializerGlobal = 0x2732BF8;
    public const uint CameraAddress = 0x162AD54;
    public const uint FilenameAddress = 0x162B850;
    public const uint ExecutableAddress = 0x17FC68C;
    public const int ExecutableLength = 0x5944;
    public const int DrawStep = 0x9000;
    public const string TextureName = "mag014.tim";
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x2732CE8, 0x2732C20, 100, 2), new(0x273ACD8, 0x273ACE8, 88, 4), new(0x273AA38, 0x273A248, 676, 3), new(0x2732CF8, 0x2732D08, 300, 100), new(0x273ACC8, 0x273AA48, 64, 10)];
}
