using System.Text;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ExternalVoiceResolverTests
{
    [Theory]
    [InlineData("voice/room/12a.ogg", "voice/room/12a.ogg")]
    [InlineData("VOICE\\ROOM\\w2_12a.OGG", "voice/room/w2_12a.ogg")]
    [InlineData("voice/room/12_3.ogg", "voice/room/12_3.ogg")]
    [InlineData("voice/room/12 - Copy.ogg", null)]
    [InlineData("voice/_battle/name/line.ogg", null)]
    [InlineData("voice/room/12.wav", null)]
    [InlineData("voice/room/../12.ogg", null)]
    public void AddressHasOneCanonicalSupportedNamespace(string path, string? expected)
    {
        Assert.Equal(expected, ExternalVoiceResolver.Address(path)?.Path);
    }

    [Fact]
    public void LookupUsesWindowThenPageThenFirstPageFallbackWithoutReadingAudio()
    {
        ExternalVoiceResolver resolver = Create("", "room/12", "room/w2_12", "room/12a", "room/w2_12a", "room/12b");
        Assert.Equal("voice/room/w2_12a.ogg", resolver.Resolve("ROOM", 2, 12, 0)!.Key.Path);
        Assert.Equal("voice/room/12a.ogg", resolver.Resolve("room", 3, 12, 0)!.Key.Path);
        Assert.Equal("voice/room/12b.ogg", resolver.Resolve("room", 2, 12, 1)!.Key.Path);
        Assert.Null(resolver.Resolve("room", 2, 12, 2));
        Assert.Equal(2.75, resolver.Resolve("room", 2, 12, 0)!.Gain);
        Assert.Equal(5, resolver.Files.Count());
        Assert.Equal("voice/room/w2_12.ogg", Create("", "room/w2_12", "room/12").Resolve("room", 2, 12, 0)!.Key.Path);
    }

    [Fact]
    public void MessagePageWindowAndChoiceUseNativeByteWidthsAndChoiceIsNotRebased()
    {
        ExternalVoiceResolver resolver = Create("", "room/w2_12a", "room/12z", "room/12_5");
        Assert.Equal("voice/room/w2_12a.ogg", resolver.Resolve("room", 258, 268, 256)!.Key.Path);
        Assert.Equal("voice/room/12z.ogg", resolver.Resolve("room", 2, 12, 255)!.Key.Path);
        Assert.Equal("voice/room/12_5.ogg", resolver.Resolve("room", 2, 268, 0, 261)!.Key.Path);
    }

    [Fact]
    public void SequentialRedirectsWrapAndMissingTargetsFallBackWithConfiguredGain()
    {
        ExternalVoiceResolver resolver = Create("[room-12]\nvolume=40\nsequential=['room/alternate', 'room/missing']", "room/12", "room/alternate");
        Assert.Equal("voice/room/alternate.ogg", resolver.Resolve("room", 0, 12, 0)!.Key.Path);
        ExternalVoiceSelection fallback = resolver.Resolve("room", 0, 12, 0)!;
        Assert.Equal("voice/room/12.ogg", fallback.Key.Path);
        Assert.Equal(0.4, fallback.Gain);
        Assert.Equal("voice/room/alternate.ogg", resolver.Resolve("room", 0, 12, 0)!.Key.Path);
    }

    [Fact]
    public void GameMomentReplacesParentRuleRatherThanInheritingVolume()
    {
        ExternalVoiceResolver resolver = Create("[room-12]\nvolume=20\n[room-12.gm-7]\nshuffle=['room/alternate']", "room/12", "room/alternate");
        Assert.Equal(0.2, resolver.Resolve("room", 0, 12, 0)!.Gain);
        ExternalVoiceSelection moment = resolver.Resolve("room", 0, 12, 0, gameMoment: 7)!;
        Assert.Equal("voice/room/alternate.ogg", moment.Key.Path);
        Assert.Equal(2.75, moment.Gain);
    }

    [Fact]
    public void PriorityAndEligibilityKeepConfigurationWithItsWinningOwner()
    {
        ModPackage first = Package("first");
        ModPackage second = Package("second");
        ModFile[] files = [Audio(first.Id, "room/12"), Config(second.Id, "[room-12]\nvolume=10"), Audio(second.Id, "room/12")];
        ModCatalogSnapshot catalog = new(1, [first, second], files);
        ExternalVoiceResolver resolver = new(catalog);
        Assert.Equal(first.Id, resolver.Resolve("room", 0, 12, 0)!.PackageId);
        Assert.Equal(2.75, resolver.Resolve("room", 0, 12, 0)!.Gain);
        Assert.Single(resolver.Files);
        ExternalVoiceResolver filtered = new(catalog, file => file.PackageId != first.Id);
        Assert.Equal(second.Id, filtered.Resolve("room", 0, 12, 0)!.PackageId);
        Assert.Equal(0.1, filtered.Resolve("room", 0, 12, 0)!.Gain);
    }

    [Theory]
    [InlineData("[room-12]\nunknown=true")]
    [InlineData("[room-12]\nvolume=101")]
    [InlineData("[room-12]\nshuffle=[]")]
    [InlineData("[room-12]\nsequential=[1]")]
    [InlineData("[room-12]\nshuffle=['room/1']\nsequential=['room/2']")]
    [InlineData("[room-12]\nshuffle=['../escape']")]
    public void UnsupportedConfigurationHasOwningDiagnostics(string text)
    {
        ModPackage package = Package("owner");
        ModCatalogSnapshot catalog = new(1, [package], [Config(package.Id, text)]);
        ModDiagnostic diagnostic = Assert.Single(ExternalVoiceResolver.Validate(catalog));
        Assert.Equal("external-voice-config-invalid", diagnostic.Code);
        Assert.Equal(package.Id, diagnostic.PackageId);
        Assert.Equal("voice/config.toml", diagnostic.RelativePath);
    }

    [Fact]
    public void UnsupportedNamespacesAreDiagnosedButEditorSidecarsAndCopiesAreIgnored()
    {
        ModPackage package = Package("owner");
        ModCatalogSnapshot catalog = new(1, [package], [Audio(package.Id, "_world/1"), Audio(package.Id, "room/12 - Copy"), new(package.Id, ModFormat.Junction, "voice/room/12.ogg.sfk", 1, () => throw new InvalidOperationException())]);
        Assert.Equal("external-voice-namespace-unsupported", Assert.Single(ExternalVoiceResolver.Validate(catalog)).Code);
        Assert.Empty(new ExternalVoiceResolver(catalog).Files);
    }

    [Fact]
    public void DisabledOrIneligibleContributionsNeverReadConfigurationOrAudio()
    {
        ModPackage package = Package("owner") with { Status = ModEffectiveStatus.Disabled };
        ModFile file = new(package.Id, ModFormat.Junction, "voice/config.toml", 1, () => throw new InvalidOperationException());
        ModCatalogSnapshot catalog = new(1, [package], [file]);
        Assert.Empty(ExternalVoiceResolver.Validate(catalog));
        Assert.Empty(new ExternalVoiceResolver(catalog).Files);
        ModCatalogSnapshot active = new(1, [Package("owner")], [file]);
        Assert.Empty(ExternalVoiceResolver.Validate(active, _ => false));
        Assert.Empty(new ExternalVoiceResolver(active, _ => false).Files);
    }

    [Fact]
    public void FinalLookupCanRedirectWithoutPhysicalBaseAndNeverBorrowsAnotherOwner()
    {
        ExternalVoiceResolver alias = Create("[room-12]\nsequential=['room/alternate']", "room/alternate");
        Assert.Equal("voice/room/alternate.ogg", alias.Resolve("room", 0, 12, 0)!.Key.Path);
        ModPackage first = Package("first");
        ModPackage second = Package("second");
        ModCatalogSnapshot catalog = new(1, [first, second], [Config(first.Id, "[room-12]\nshuffle=['room/alternate']"), Audio(first.Id, "room/12"), Audio(second.Id, "room/alternate")]);
        ExternalVoiceSelection selection = new ExternalVoiceResolver(catalog).Resolve("room", 0, 12, 0)!;
        Assert.Equal("voice/room/12.ogg", selection.Key.Path);
        Assert.Equal(first.Id, selection.PackageId);
    }

    private static ModPackage Package(string id) => new(id, "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, ".");
    private static ModFile Audio(string owner, string name) => new(owner, ModFormat.Junction, $"voice/{name}.ogg", 1, () => throw new InvalidOperationException("The resolver must not read audio bytes."));
    private static ModFile Config(string owner, string text) => new(owner, ModFormat.Junction, "voice/config.toml", 1, () => Encoding.UTF8.GetBytes(text));
    private static ExternalVoiceResolver Create(string config, params string[] names)
    {
        ModPackage package = Package("owner");
        return new(new ModCatalogSnapshot(1, [package], new[] { Config(package.Id, config) }.Concat(names.Select(name => Audio(package.Id, name))).ToArray()));
    }
}
