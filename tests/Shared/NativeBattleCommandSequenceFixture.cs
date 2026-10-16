using System.Collections.Immutable;

namespace Memoria.FF8.Tests;

internal static class NativeBattleCommandSequenceFixture
{
    // English FF8_EN.exe, SHA256 064D466B5FE2BA901FD44ABF19F37C0FD6A2DB40AABD95C9E5959195B6589570,
    // VA B8150C: all 27h command sequence bytes read by 48D200. Verified against the installed executable in Assets tests.
    public static ImmutableArray<byte> Values { get; } = [0, 13, 11, 14, 15, 21, 30, 24, 11, 11, 30, 21, 13, 15, 21, 21, 21, 21, 21, 21, 21, 21, 21, 23, 11, 11, 11, 11, 25, 11, 11, 24, 11, 11, 11, 11, 11, 11, 14];
}
