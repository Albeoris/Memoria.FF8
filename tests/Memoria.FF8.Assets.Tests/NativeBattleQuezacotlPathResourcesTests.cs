using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlPathResourcesTests
{
    [Fact]
    public void OriginalFortyPathsRetainTheirExactExtentHashOffsetsAndTerminalWord()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlPathResources resources = game.ReadNativeQuezacotlResources().Paths!;
        resources.Validate();
        Assert.Equal(0x12AFE58u, resources.Address);
        Assert.Equal(1944, resources.Data.Length);
        Assert.Equal("81EB42396AA8EB9A76D9F606ABFFBB1A8483F02380C4EA1B223A480DD3B5DFB4", Convert.ToHexString(SHA256.HashData(resources.Data.AsSpan())));
        Assert.Equal((short)40, resources.ReadWord(resources.Address));
        Assert.Equal((short)41, resources.ReadWord(resources.Address + 2));
        Assert.Equal((short)949, resources.ReadWord(resources.Address + 80));
        Assert.Equal(short.MaxValue, resources.ReadWord(resources.Address + 1942));
        Assert.Throws<NotSupportedException>(() => resources.ReadWord(resources.Address - 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadWord(resources.Address + 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadWord(resources.Address + 1944));
        Assert.Equal(0, game.ReadColdProcessNativeBattleMemory().EffectMemory!.QuezacotlTargetY1774);
    }
}
