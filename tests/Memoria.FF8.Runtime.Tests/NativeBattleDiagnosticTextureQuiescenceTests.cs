using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiagnosticTextureQuiescenceTests
{
    private static NativeBattleGraphicsState Fixture(int count = 1)
    {
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear();
        for (int row = 0; row < count; row++)
        {
            auxiliary = auxiliary.AppendTextureAnimation(row % 7, 11, new([255], 0));
        }
        return new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3) { ResourceFlags44 = 10, Sequence = 11 }).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0) { Auxiliary = auxiliary };
    }

    private static BattleDiagnosticTextureQuiescence Capture(NativeBattleGraphicsState graphics)
    {
        return (BattleDiagnosticTextureQuiescence)typeof(BattleSession).GetMethod("CaptureDiagnosticTextureQuiescence", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [graphics])!;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(16)]
    public void Complete_terminal_chain_matches_actual_native_no_write_callbacks_without_releasing_tasks(int count)
    {
        NativeBattleGraphicsState graphics = Fixture(count);
        string before = NativeBattleTestSnapshots.Json(graphics);
        BattleDiagnosticTextureQuiescence observation = Capture(graphics);
        Assert.True(observation.IsTerminalQuiescent);
        Assert.True(observation.Complete);
        Assert.Equal(16, observation.Capacity);
        Assert.Equal(count, observation.PhysicalActiveCount);
        Assert.Equal(count, observation.Tasks.Length);
        Assert.Equal(graphics.Auxiliary.Head, observation.Head);
        Assert.Equal(graphics.Auxiliary.Tail, observation.Tail);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(graphics));
        int callbacks = 0;
        NativeBattleAuxiliaryStep advanced = graphics.Auxiliary.Advance(new()
        {
            ReadModel = index => graphics.Models[index],
            ApplyTexture = (_, _, _) => throw new InvalidOperationException("Terminal native branch must not copy a texture."),
            RestoreTextureBlink = _ => throw new InvalidOperationException("Terminal native branch must not restore or release blink ownership.")
        }, _ => callbacks++);
        Assert.Equal(count, callbacks);
        Assert.Equal(NativeBattleTestSnapshots.Json(graphics.Auxiliary), NativeBattleTestSnapshots.Json(advanced.State));
        foreach (BattleDiagnosticTextureTask row in observation.Tasks)
        {
            Assert.Same(graphics.Auxiliary.Slots[row.Slot], advanced.State.Slots[row.Slot]);
            Assert.Equal(0xF0u, row.UnknownByteMask);
            Assert.Equal((byte)255, row.Deadline);
            Assert.Equal((byte)11, row.CapturedSequence);
            Assert.Equal(row.CapturedSequence, row.ModelSequence);
            Assert.Equal(32, row.PayloadLength);
        }
    }

    [Theory]
    [InlineData("pointer-unknown")]
    [InlineData("sequence-unknown")]
    [InlineData("pointer-invalid")]
    [InlineData("short-payload")]
    [InlineData("program-missing")]
    [InlineData("program-outside")]
    [InlineData("program-empty")]
    [InlineData("deadline-not-terminal")]
    [InlineData("foreign-kind")]
    [InlineData("unloaded-model")]
    [InlineData("resource-unknown")]
    [InlineData("resource-release")]
    [InlineData("sequence-changed")]
    [InlineData("sequence-unavailable")]
    [InlineData("model-unavailable")]
    [InlineData("model-index-contradiction")]
    [InlineData("cycle")]
    [InlineData("orphan-active")]
    [InlineData("free-linked")]
    [InlineData("wrong-tail")]
    [InlineData("missing-head")]
    [InlineData("wrong-capacity")]
    public void Unknown_foreign_nonterminal_or_incomplete_native_owners_cannot_be_classified_quiescent(string fault)
    {
        NativeBattleGraphicsState graphics = Fixture();
        NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[0];
        task = fault switch
        {
            "pointer-unknown" => task with { UnknownByteMask = task.UnknownByteMask | 1 },
            "sequence-unknown" => task with { UnknownByteMask = task.UnknownByteMask | 0x200 },
            "pointer-invalid" => task with { Payload = task.Payload.SetItem(0, 0).SetItem(1, 0).SetItem(2, 0).SetItem(3, 0) },
            "short-payload" => task with { Payload = task.Payload.Take(4).ToImmutableArray() },
            "program-missing" => task with { Program = null },
            "program-outside" => task with { Program = task.Program! with { Offset = 1 } },
            "program-empty" => task with { Program = new([], 0) },
            "deadline-not-terminal" => task with { Program = new([1], 0) },
            "foreign-kind" => task with { Kind = NativeBattleAuxiliaryKind.CameraBlend509930 },
            "free-linked" => task with { Flags = 0 },
            "cycle" => task with { Next = 0 },
            _ => task
        };
        graphics = graphics with { Auxiliary = graphics.Auxiliary with { Slots = graphics.Auxiliary.Slots.SetItem(0, task) } };
        NativeBattleGraphicsModel model = graphics.Models[0];
        model = fault switch
        {
            "unloaded-model" => model with { Flags0 = 0 },
            "resource-unknown" => model with { ResourceFlags44 = null },
            "resource-release" => model with { ResourceFlags44 = 0x40 },
            "sequence-changed" => model with { Sequence = 12 },
            "sequence-unavailable" => model with { Sequence = null },
            _ => model
        };
        graphics = graphics with { Models = graphics.Models.SetItem(0, model) };
        graphics = fault switch
        {
            "model-unavailable" => graphics with { Models = [] },
            "model-index-contradiction" => graphics with { Models = graphics.Models.SetItem(0, new NativeBattleGraphicsModel(1, 3) { ResourceFlags44 = 10, Sequence = 11 }) },
            "orphan-active" => graphics with { Auxiliary = graphics.Auxiliary with { Slots = graphics.Auxiliary.Slots.SetItem(1, task) } },
            "wrong-tail" => graphics with { Auxiliary = graphics.Auxiliary with { Tail = 1 } },
            "missing-head" => graphics with { Auxiliary = graphics.Auxiliary with { Head = null } },
            "wrong-capacity" => graphics with { Auxiliary = graphics.Auxiliary with { Slots = graphics.Auxiliary.Slots.RemoveAt(15) } },
            _ => graphics
        };
        string before = NativeBattleTestSnapshots.Json(graphics);
        BattleDiagnosticTextureQuiescence observation = Capture(graphics);
        Assert.False(observation.IsTerminalQuiescent);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(graphics));
        if (fault is "pointer-unknown" or "sequence-unknown" or "foreign-kind")
        {
            BattleDiagnosticTextureTask row = Assert.Single(observation.Tasks);
            Assert.Null(row.ModelPointer);
            Assert.Null(row.CapturedSequence);
        }
        if (fault is "cycle" or "orphan-active" or "free-linked" or "wrong-tail" or "missing-head" or "wrong-capacity")
        {
            Assert.False(observation.Complete);
        }
    }

    [Fact]
    public void Empty_native_bank_is_not_a_nonempty_terminal_owner_certificate()
    {
        BattleDiagnosticTextureQuiescence observation = Capture(Fixture() with { Auxiliary = NativeBattleAuxiliaryState.Clear() });
        Assert.True(observation.Complete);
        Assert.Empty(observation.Tasks);
        Assert.False(observation.IsTerminalQuiescent);
    }

    [Theory]
    [InlineData("dropped-row")]
    [InlineData("reordered")]
    [InlineData("duplicate-row")]
    [InlineData("changed-next")]
    [InlineData("physical-count")]
    [InlineData("claimed-complete")]
    public void Copied_certificate_cannot_lose_actual_chain_coverage(string fault)
    {
        BattleDiagnosticTextureQuiescence original = Capture(Fixture(3));
        Assert.True(original.IsTerminalQuiescent);
        BattleDiagnosticTextureQuiescence invalid = fault switch
        {
            "dropped-row" => original with { Tasks = original.Tasks.RemoveAt(1) },
            "reordered" => original with { Tasks = original.Tasks.SetItem(0, original.Tasks[1]).SetItem(1, original.Tasks[0]) },
            "duplicate-row" => original with { Tasks = original.Tasks.SetItem(1, original.Tasks[0]) },
            "changed-next" => original with { Tasks = original.Tasks.SetItem(0, original.Tasks[0] with { Next = 2 }) },
            "physical-count" => original with { PhysicalActiveCount = 2 },
            _ => original with { Complete = false }
        };
        Assert.False(invalid.IsTerminalQuiescent);
        Assert.True(original.IsTerminalQuiescent);
    }
}