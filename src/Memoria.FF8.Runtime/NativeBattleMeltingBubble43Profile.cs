using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltingBubble43ChannelRegion(uint Address, int Length);

public static class NativeBattleMeltingBubble43Profile
{
    public const uint Entry = 0x839940;
    public const uint Initializer = 0x839920;
    public const uint InitializerGlobal = 0x2655778;
    public const uint RootCallback = 0x839A60;
    public const uint TargetCallback = 0x839C60;
    public const uint GenericCallback = 0x839D70;
    public const int DrawStep = 0xA000;
    public const uint ChannelBase = 0x265622C;
    public const int ChannelByteCount = 948;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x2655868, 0x26557A0, 100, 2), new(0x2656088, 0x2656098, 88, 4), new(0x2656078, 0x2655888, 676, 3)];
    public static ImmutableArray<NativeBattleMeltingBubble43ChannelRegion> ChannelRegions { get; } = [new(0x2656328, 40), new(0x2656354, 40), new(0x2656380, 40), new(0x265691C, 40), new(0x2656948, 40), new(0x2656974, 40), new(0x2656F14, 40), new(0x2656F40, 40), new(0x2656F6C, 40), new(0x2657404, 30), new(0x2657424, 30), new(0x2657444, 30), new(0x2657778, 30), new(0x2657798, 30), new(0x26577B8, 30), new(0x2657B08, 30), new(0x2657B28, 30), new(0x2657B48, 30), new(0x2657EC4, 30), new(0x2657EE4, 30), new(0x2657F04, 30), new(0x265829C, 48), new(0x26586A4, 30), new(0x26586C4, 30), new(0x26586E4, 30), new(0x2658BC8, 30), new(0x2658BE8, 30), new(0x2658C08, 30)];
}
