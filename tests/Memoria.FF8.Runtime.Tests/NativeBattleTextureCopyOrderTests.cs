using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureCopyOrderTests
{
    [Fact]
    public void ImportedCurrentFrameStillMovesToTheEndOfAnOlderCopyOrder()
    {
        NativeBattleTextureRegistration observed = Assert.Single(Copy(Copy(Upload(), 64, 256), 72, 256)!.Value);
        NativeBattleTextureRegistration imported = JsonSerializer.Deserialize<NativeBattleTextureRegistration>(JsonSerializer.Serialize(observed with { CurrentFrame = 0 }))!;
        imported.Validate();
        string before = JsonSerializer.Serialize(imported);

        NativeBattleTextureRegistration next = Assert.Single(Copy([imported], 64, 256)!.Value);

        Assert.Equal(0, next.CurrentFrame);
        Assert.Equal(new[] { 1, 0 }, next.CopyOrder!.Value);
        Assert.Equal(imported.CopyFrames, next.CopyFrames);
        Assert.Equal(new[] { 0, 1 }, imported.CopyOrder!.Value);
        Assert.Equal(before, JsonSerializer.Serialize(imported));
        next.Validate();
    }

    [Fact]
    public void UnknownPaletteClearsImportedOrderEvenWhenHistoryWasAlreadyUnknown()
    {
        NativeBattleTextureRegistration observed = Assert.Single(Copy(Upload(), 64, 256)!.Value);
        NativeBattleTextureRegistration imported = observed with { CopyHistoryKnown = false, PaletteOwnedSpans = null };
        imported.Validate();
        string before = JsonSerializer.Serialize(imported);

        NativeBattleTextureRegistration next = Assert.Single(Copy([imported], 72, 256)!.Value);

        Assert.False(next.CopyHistoryKnown);
        Assert.Null(next.CopyOrder);
        Assert.Equal(imported.CurrentFrame, next.CurrentFrame);
        Assert.Equal(imported.CopyFrames, next.CopyFrames);
        Assert.Equal(new[] { 0 }, imported.CopyOrder!.Value);
        Assert.Equal(before, JsonSerializer.Serialize(imported));
        next.Validate();
    }

    [Fact]
    public void RepeatedCopiesMoveStableFrameIndicesToTheirLastObservationOrder()
    {
        var owners = Upload();
        Assert.Empty(Assert.Single(owners!.Value).CopyOrder!.Value);
        owners = Copy(owners, 64, 256);
        owners = Copy(owners, 72, 256);
        owners = Copy(owners, 80, 256);
        owners = Copy(owners, 64, 256);
        owners = Copy(owners, 72, 256);
        NativeBattleTextureRegistration owner = Assert.Single(owners!.Value);

        Assert.Equal(new NativeBattleEffectTextureRectangle[] { new(64, 256, 4, 2), new(72, 256, 4, 2), new(80, 256, 4, 2) }, owner.CopyFrames);
        Assert.Equal(new[] { 2, 0, 1 }, owner.CopyOrder!.Value);
        Assert.Equal(1, owner.CurrentFrame);
        Assert.Equal(1, owner.ExactFrame);
        Assert.Equal(new[] { 72, 64, 80 }, owner.CopyOrder.Value.Reverse().Select(index => (int)owner.CopyFrames[index].X));
        owner.Validate();
    }

    [Fact]
    public void SourceAndDestinationKeepIndependentRecencyOfTheSameInputRectangle()
    {
        var owners = Upload();
        NativeBattleTextureOrigin target = new("target.tim", 0, NativeBattleTextureRole.Pixels, 4, 32, 16);
        owners = NativeBattleTextureCopyHistory.Upload(owners, 128, 256, 32, 16, target);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(128, 256, 4, 2, 136, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 4, 2, 128, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(72, 256, 4, 2, 128, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 4, 2, 128, 256));

        NativeBattleTextureRegistration source = NativeBattleTextureCopyHistory.Find(owners, 64, 256)!;
        NativeBattleTextureRegistration destination = NativeBattleTextureCopyHistory.Find(owners, 128, 256)!;
        Assert.Equal(new[] { 1, 0 }, source.CopyOrder!.Value);
        Assert.Equal(new[] { 0, 2, 1 }, destination.CopyOrder!.Value);
        Assert.Equal(source.CopyFrames[0], destination.CopyFrames[1]);
        Assert.Equal(0, source.CurrentFrame);
        Assert.Equal(1, destination.CurrentFrame);
    }

    [Fact]
    public void PaletteObservationsDoNotLoseTheLastMatchingPixelCopy()
    {
        var owners = Upload();
        owners = Copy(owners, 64, 256);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(0, 32, 16, 1, 0, 32));
        owners = Copy(owners, 64, 256);
        NativeBattleTextureRegistration owner = Assert.Single(owners!.Value);

        Assert.Equal(new[] { 1, 0 }, owner.CopyOrder!.Value);
        Assert.Equal(new(0, 32, 16, 1), owner.CopyFrames[1]);
        Assert.Equal(0, owner.CurrentFrame);
    }

    [Fact]
    public void KnownOrderSurvivesSerializationAndContinuedObservation()
    {
        var owners = Copy(Copy(Upload(), 64, 256), 72, 256);
        var restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattleTextureRegistration>>(JsonSerializer.Serialize(owners!.Value));
        var next = Copy(restored, 64, 256);
        NativeBattleTextureRegistration owner = Assert.Single(next!.Value);

        Assert.Equal(new[] { 1, 0 }, owner.CopyOrder!.Value);
        Assert.Equal(2, owner.CopyFrames.Length);
        Assert.Equal(0, owner.CurrentFrame);
    }

    [Fact]
    public void LegacyUnknownOrderIsOmittedAndStaysUnknownUntilReupload()
    {
        NativeBattleTextureRegistration current = Assert.Single(Copy(Upload(), 64, 256)!.Value);
        NativeBattleTextureRegistration legacy = current with { CopyOrder = null };
        string json = JsonSerializer.Serialize(legacy);
        Assert.DoesNotContain("CopyOrder", json);
        NativeBattleTextureRegistration restored = JsonSerializer.Deserialize<NativeBattleTextureRegistration>(json)!;
        var next = Copy([restored], 72, 256);

        Assert.Null(Assert.Single(next!.Value).CopyOrder);
        Assert.Equal(1, Assert.Single(next.Value).CurrentFrame);
        var reuploaded = NativeBattleTextureCopyHistory.Upload(next, current.X, current.Y, current.WidthWords, current.Height, current.Origin);
        Assert.Empty(Assert.Single(reuploaded!.Value).CopyOrder!.Value);
        Assert.Empty(Assert.Single(reuploaded.Value).CopyFrames);
        Assert.Equal(-1, Assert.Single(reuploaded.Value).CurrentFrame);
    }

    [Fact]
    public void AnUnobservedCopyDueToLegacyPaletteOwnershipInvalidatesKnownRecency()
    {
        NativeBattleTextureRegistration known = Assert.Single(Copy(Upload(), 64, 256)!.Value);
        NativeBattleTextureRegistration legacy = new(new("legacy.tim", 0, NativeBattleTextureRole.Pixels, 4, 4, 2), 128, 256, 4, 2, []);
        var next = Copy([known, legacy], 72, 256);

        Assert.All(next!.Value, owner => Assert.Null(owner.CopyOrder));
        Assert.All(next.Value, owner => Assert.False(owner.CopyHistoryKnown));
        Assert.Equal(known.CopyFrames, next.Value[0].CopyFrames);
        Assert.Equal(known.CurrentFrame, next.Value[0].CurrentFrame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void KnownOrderMustContainEveryStableFrameIndexExactlyOnce(int invalid)
    {
        NativeBattleTextureRegistration owner = Assert.Single(Copy(Copy(Upload(), 64, 256), 72, 256)!.Value);
        ImmutableArray<int> order = invalid switch
        {
            0 => default,
            1 => [0],
            2 => [0, 0],
            3 => [-1, 0],
            _ => [0, 2]
        };

        Assert.Throws<InvalidDataException>(() => (owner with { CopyOrder = order }).Validate());
    }

    private static ImmutableArray<NativeBattleTextureRegistration>? Upload()
    {
        NativeBattleTextureOrigin origin = new("source.tim", 0, NativeBattleTextureRole.Pixels, 4, 32, 16) { PaletteRectangle = new(0, 32, 16, 1) };
        return NativeBattleTextureCopyHistory.Upload(null, 64, 256, 32, 16, origin);
    }

    private static ImmutableArray<NativeBattleTextureRegistration>? Copy(ImmutableArray<NativeBattleTextureRegistration>? owners, short x, short y) => NativeBattleTextureCopyHistory.Copy(owners, new(x, y, 4, 2, 88, 264));
}
