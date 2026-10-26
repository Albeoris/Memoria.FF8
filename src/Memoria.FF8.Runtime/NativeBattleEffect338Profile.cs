using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect338Profile
{
    public const uint Entry = 0x6EBD50;
    public const uint Initializer = 0x575590;
    public const uint ArenaAddress = 0x20DFAB8;
    public const uint RootDescriptor = 0x2556210;
    public const uint RootBank = 0x2556220;
    public const uint RootCallback = 0x6EBE20;
    public const uint MainCallback = 0x6EC0F0;
    public const uint ArenaGlobal = 0x2556250;
    public const uint ParametersGlobal = 0x25561CC;
    public const uint CasterGlobal = 0x25561D0;
    public const uint TargetGlobal = 0x25561D4;
    public const uint CursorGlobal = 0x2556234;
    public const uint EndGlobal = 0x2556238;
    public const int IoOffset = 0x5C90;
    public const int ScratchOffset = 0x1C90;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2556200, ArenaAddress + 0x1BC0, 0xD0, 1), new(0x25561F0, ArenaAddress + 0xF00, 0x198, 8), new(0x25561E0, ArenaAddress + 0x600, 0x24, 64)];
}
