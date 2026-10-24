using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect344TextBankTests
{
    [Fact]
    public void Presentation_text_bank_is_independent_of_AI_and_retains_exact_values_after_JSON()
    {
        NativeBattleGraphicsResource current = new([1, 0, 4, 0, 0]) { AnimationFrames = [], TextBank = ImmutableArray.Create("First", "Second") };
        NativeBattleGraphicsResource restored = JsonSerializer.Deserialize<NativeBattleGraphicsResource>(JsonSerializer.Serialize(current))!;
        Assert.Equal("Second", restored.ReadText91(1));
        NativeBattleGraphicsResource replacement = restored with { TextBank = ImmutableArray.Create("Replacement") };
        Assert.Equal("Replacement", replacement.ReadText91(0));
        Assert.Equal("First", restored.ReadText91(0));
    }

    [Fact]
    public void Missing_and_out_of_range_text_bank_entries_fail_at_the_actual_consumer()
    {
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0]);
        Assert.Throws<NotSupportedException>(() => resource.ReadText91(0));
        resource = resource with { TextBank = ImmutableArray.Create("Only") };
        Assert.Throws<InvalidDataException>(() => resource.ReadText91(-1));
        Assert.Throws<InvalidDataException>(() => resource.ReadText91(1));
    }
}
