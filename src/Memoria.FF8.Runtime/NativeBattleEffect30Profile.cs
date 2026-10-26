using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect30Profile
{
    public const uint Entry = 0x8735F0;
    public const uint Initializer = 0x8735D0;
    public const uint InitializerGlobal = 0x26D40B8;
    public const uint CursorGlobal = 0x26D40BC;
    public const uint RootCallback = 0x873740;
    public const uint TargetCallback = 0x873A70;
    public const uint NodeCallback = 0x873B90;
    public const uint BeamCallback = 0x878930;
    public const uint ParticleCallback = 0x878F20;
    public const uint FadeInCallback = 0x873950;
    public const uint FadeOutCallback = 0x879350;
    public const uint SourceAddress = 0x17CC72C;
    public const int SourceLength = 0x60AE;
    public const uint CameraAddress = 0x160D3EC;
    public const int CameraLength = 0x14C;
    public const uint ArenaAddress = NativeBattleEffectNodeMemory.ArenaAddress;
    public const int DrawStep = 0x6000;
    public const int EmitterCount = 10;
    public const int GeometryCount = 50;
    public const int EmitterStride = 108;
    public const int GeometryStride = 672;
    public const int EmitterBytes = EmitterCount * EmitterStride;
    public const int GeometryBytes = GeometryCount * GeometryStride;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x26D41A8, 0x26D40E0, 100, 2), new(0x26D6898, 0x26D68A8, 88, 4), new(0x26D65F8, 0x26D5E08, 676, 3), new(0x26D41B8, 0x26D41C8, 144, 10), new(0x26D4770, 0x26D4780, 96, 60), new(0x26D6888, 0x26D6608, 64, 10)];
}
