using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlFivePairTests
{
    [Fact]
    public void OriginalFivePairsControlExtentAndExplicit209SoundAreAuthenticated()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_GAME_DIR to the installed game directory.");
        using GameAssets game = new(directory!);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleQuezacotlRibbonBank bank = resources.Ribbon!.SecondaryBank!;
        bank.Validate();
        Assert.Equal(0x12AF800u, bank.PairAddress);
        Assert.Equal(0x12AF828u, bank.ControlAddress);
        Assert.Equal(40, bank.Pairs.Length);
        Assert.Equal(65 * 24, bank.ControlTriangles.Length);
        Assert.Equal("12504F461AD51D72B5A91E9A528D02987619F5CBCB70DE5EB9E658AEA64F0EA3", Convert.ToHexString(SHA256.HashData(bank.Pairs.AsSpan())));
        Assert.Equal("B91D2D717A0DE5D45EED3A08BF89A7F8CC52F69EF7C96FBDD028D670E26FE54F", Convert.ToHexString(SHA256.HashData(bank.ControlTriangles.AsSpan())));
        Assert.Equal(240072u, resources.Frame209Sound);
        Assert.Equal(0x00505050u, resources.Frame229FlashColor);
    }
}
