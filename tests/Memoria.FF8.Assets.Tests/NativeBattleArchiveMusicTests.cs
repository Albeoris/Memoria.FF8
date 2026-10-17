using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleArchiveMusicTests
{
    [Fact]
    public void Original_file766_selects_the_actual_second_nested_AKAO_resource_for501A20()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleArchiveFile file = assets.ReadNativeBattleFile(766);
        Assert.Equal("r0win.dat", file.Name);
        Assert.Equal(163916, file.Bytes.Length);
        Assert.Equal("385EAC9AEFE970A7536BB332BA7733DF437908B1AEBC99D50F52264FC0F7A584", Convert.ToHexString(SHA256.HashData(file.Bytes.AsSpan())));
        int first = file.Section(0);
        NativeBattleMusicLoadSource source = NativeBattleMusicLoadSource.Read501A20(file.Bytes.Slice(first, file.Section(1) - first));
        Assert.Equal(126144, source.SoundBank.Length);
        Assert.Equal(4280, source.Music.Length);
        Assert.Equal("FBBBCCD24F1D2CFBA7F39EB9807426B4E74C5835E564C541ADDDFA730BBBA923", Convert.ToHexString(SHA256.HashData(source.Music.AsSpan())));
        Assert.Equal(new NativeMusicBufferIdentity(2, NativeMusicBufferProducer.Battle501A70), source.ReadIdentity());
    }
}
