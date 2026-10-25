using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticLimitPreparationTests
{
    [Fact]
    public void Learned_prerequisites_preserve_original_initialization_and_training_owners()
    {
        NativeLimitProgress source = CreateProgress();
        string before = JsonSerializer.Serialize(source);

        DiagnosticLimitPreparationResult result = GameWorld.PrepareDiagnosticLimits(source, new() { LearnBlueMagic = 8, LearnDuel = 16, LearnAngelo = 32, LearnSpecialMagic = 4, Options978 = 1 });

        Assert.Equal((ushort)9, result.Progress.BlueMagicFlags);
        Assert.Equal((ushort)18, result.Progress.DuelFlags);
        Assert.Equal((byte)36, result.Progress.AngeloFlags);
        Assert.Equal((byte)5, result.Progress.SpecialMagicFlags);
        Assert.Equal((ushort)1, result.Progress.Options978);
        Assert.Equal(source.InitOutSha256, result.Progress.InitOutSha256);
        Assert.Equal(source.TrainingCounters, result.Progress.TrainingCounters);
        Assert.Equal(source.TrainingSelection, result.Progress.TrainingSelection);
        Assert.Single(result.Changes);
        Assert.Equal("limit-progress", result.Changes[0].Owner);
        Assert.Equal(before, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void Preparation_never_fabricates_unknown_saved_owners()
    {
        NativeLimitProgress source = CreateProgress() with { Options978 = null, SpecialMagicFlags = null };

        Assert.Throws<NotSupportedException>(() => GameWorld.PrepareDiagnosticLimits(source, new() { Options978 = 1 }));
        Assert.Throws<NotSupportedException>(() => GameWorld.PrepareDiagnosticLimits(source, new() { LearnSpecialMagic = 1 }));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticLimits(source, new() { LearnSpecialMagic = 64 }));
        DiagnosticLimitPreparationResult unchanged = GameWorld.PrepareDiagnosticLimits(source, new());
        Assert.Null(unchanged.Progress.Options978);
        Assert.Null(unchanged.Progress.SpecialMagicFlags);
        Assert.Empty(unchanged.Changes);
    }

    private static NativeLimitProgress CreateProgress()
    {
        return new(new string('A', 64), 1, 2, 4, 1, Enumerable.Range(0, 8).Select(value => (byte)value).ToImmutableArray()) { SpecialMagicFlags = 1, Options978 = 0 };
    }
}
