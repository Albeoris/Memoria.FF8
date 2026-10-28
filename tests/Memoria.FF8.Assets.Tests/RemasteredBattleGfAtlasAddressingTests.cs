using System.Collections.Immutable;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class RemasteredBattleGfAtlasAddressingTests
{
    [Fact]
    public void NativeTableNameAndComponentOwnTheWholePhysicalPage()
    {
        ImmutableArray<RemasteredBattleImageRecord> table = [new("mag777", 12, 1, 2, 832, 256, 64, 128, 2)];
        ImmutableArray<NativeBattleTextureRegistration> registrations = [Registration("mag012_b.03", 47, 832, 256, 64, 128)];
        var address = RemasteredBattleGfAtlasAddressing.Create(table, registrations, 0x9D, 0, 128, 256, 128);
        Assert.NotNull(address);
        Assert.Equal("mag777", address.Name);
        Assert.Equal(2, address.FirstImage);
        Assert.Equal(2, address.ImageCount);
        Assert.Equal((0, 128, 256, 128), (address.X, address.Y, address.Width, address.Height));
    }

    [Fact]
    public void UnrecognizedEarlierPageOwnerPreventsInventingAtlasAuthority()
    {
        ImmutableArray<RemasteredBattleImageRecord> table = [new("mag777", 12, 0, 2, 832, 256, 64, 128, 2)];
        ImmutableArray<NativeBattleTextureRegistration> registrations = [Registration("unrecognized.tim", 0, 832, 384, 64, 128), Registration("mag012_b.03", 0, 832, 256, 64, 128)];
        Assert.Null(RemasteredBattleGfAtlasAddressing.Create(table, registrations, 0x9D, 0, 0, 256, 256));
    }

    [Fact]
    public void OriginalBattleArchiveImageProvenanceUsesTheSameNativeRectangleSelector()
    {
        ImmutableArray<RemasteredBattleImageRecord> table = [new("mag777", 12, 0, 2, 832, 256, 64, 128, 2)];
        NativeBattleTextureRegistration registration = Registration("mag012_b.03", 0, 832, 256, 64, 128);
        registration = registration with { Origin = registration.Origin with { Domain = NativeBattleTextureDomain.Battle, Kind = NativeBattleTextureSourceKind.Image } };
        var address = RemasteredBattleGfAtlasAddressing.Create(table, [registration], 0x9D, 0, 0, 256, 256);
        Assert.NotNull(address);
        Assert.Equal("mag777", address.Name);
    }

    [Fact]
    public void WrongRectangleAndUnprovedSourceDepthStayUnbound()
    {
        ImmutableArray<RemasteredBattleImageRecord> table = [new("mag777", 12, 0, 2, 832, 256, 64, 128, 2)];
        Assert.Null(RemasteredBattleGfAtlasAddressing.Create(table, [Registration("mag012_b.03", 0, 832, 256, 64, 256)], 0x9D, 0, 0, 256, 256));
        Assert.Null(RemasteredBattleGfAtlasAddressing.Create(table, [Registration("mag012_b.03", 0, 832, 256, 64, 128)], 0x1D, 0, 0, 256, 256));
    }

    [Theory]
    [InlineData(186, 28, 1)]
    [InlineData(190, 29, 0)]
    [InlineData(326, 26, 3)]
    [InlineData(217, 27, 2)]
    public void NativeTimFamiliesSelectComponentsFromThePhysicalPage(int identity, int physicalPage, int component)
    {
        ImmutableArray<RemasteredBattleImageRecord> table = [new("mag777", identity, -1, 2, 0, 0, 0, 0, 0)];
        int x = (physicalPage & 15) * 64;
        NativeBattleTextureRegistration registration = Registration($"mag{identity:000}.tim", 17, x, 256, 64, 256);
        registration = registration with { Origin = registration.Origin with { Kind = NativeBattleTextureSourceKind.Tim } };
        var address = RemasteredBattleGfAtlasAddressing.Create(table, [registration], (ushort)(physicalPage | 0x80), 0, 0, 256, 256);
        Assert.NotNull(address);
        Assert.Equal(component * 2, address.FirstImage);
    }

    private static NativeBattleTextureRegistration Registration(string name, int member, int x, int y, int widthWords, int height) => new(new(name, member, NativeBattleTextureRole.Pixels, 8, widthWords, height), x, y, widthWords, height, []);

    [Theory]
    [InlineData(832, 0)]
    [InlineData(768, 1)]
    [InlineData(704, 2)]
    public void NativeRawFamilyPublishesTheSameComponentsInBothUploadPhases(int x, int component)
    {
        ImmutableArray<RemasteredBattleImageRecord> table = [new("native-table-name", 205, -1, 2, 0, 0, 0, 0, 0)];
        NativeBattleTextureRegistration registration = Registration("mag205_b.04", 19, x, 256, 64, 128);
        registration = registration with { Origin = registration.Origin with { Domain = NativeBattleTextureDomain.Battle, Kind = NativeBattleTextureSourceKind.Image } };
        var address = RemasteredBattleGfAtlasAddressing.Create(table, [registration], (ushort)(0x90 | (x / 64)), 0, 0, 256, 256);
        Assert.NotNull(address);
        Assert.Equal("native-table-name", address.Name);
        Assert.Equal(component * 2, address.FirstImage);
    }

    [Fact]
    public void NativeRawFamilyRejectsAnUnrecognizedOriginalUploadRectangle()
    {
        ImmutableArray<RemasteredBattleImageRecord> table = [new("native-table-name", 205, -1, 2, 0, 0, 0, 0, 0)];
        Assert.Null(RemasteredBattleGfAtlasAddressing.Create(table, [Registration("mag205_b.04", 0, 832, 256, 64, 256)], 0x9D, 0, 0, 256, 256));
    }
}
