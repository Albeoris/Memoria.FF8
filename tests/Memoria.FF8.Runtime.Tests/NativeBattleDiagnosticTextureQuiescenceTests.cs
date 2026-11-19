using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiagnosticTextureQuiescenceTests
{
    private static NativeBattleGraphicsState Fixture(int count = 1)
    {
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear();
        for (int row = 0; row < count; row++)
        {
            auxiliary = auxiliary.AppendTextureAnimation(OriginalOwner(row % 7), 11, new([255], 0), OriginalAliases);
        }
        return OriginalGraphics(Enumerable.Range(0, 7).Select(slot => (slot, OriginalOwner(slot), new NativeBattleGraphicsModel(3) { ResourceFlags44 = 10, Sequence = 11 }))) with { Auxiliary = auxiliary };
    }


    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(16)]
    public void Complete_terminal_chain_matches_actual_native_no_write_callbacks_without_releasing_tasks(int count)
    {
        NativeBattleGraphicsState graphics = Fixture(count);
        string before = NativeBattleTestSnapshots.Json(graphics);
        BattleDiagnosticTextureQuiescence observation = BattlePresentationProcess.CaptureDiagnosticTextureQuiescence(graphics);
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
            ReadModel = graphics.ActorModels.Get,
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
    [InlineData("pointer-and-owner-unknown")]
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
    [InlineData("model-owner-contradiction")]
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
            "pointer-and-owner-unknown" => task with { UnknownByteMask = task.UnknownByteMask | 1, ModelOwner = null },
            "sequence-unknown" => task with { UnknownByteMask = task.UnknownByteMask | 0x200 },
            "model-owner-contradiction" => task with { ModelOwner = OriginalOwner(1) },
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
        NativeBattleGraphicsModel model = ReadOriginalModelCell(graphics, 0);
        model = fault switch
        {
            "unloaded-model" => model with { Flags0 = 0 },
            "resource-unknown" => model with { ResourceFlags44 = null },
            "resource-release" => model with { ResourceFlags44 = 0x40 },
            "sequence-changed" => model with { Sequence = 12 },
            "sequence-unavailable" => model with { Sequence = null },
            _ => model
        };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(OriginalOwner(0), model) };
        graphics = fault switch
        {
            "model-unavailable" => graphics with { ActorModels = graphics.ActorModels.Remove(OriginalOwner(0)) },
            "orphan-active" => graphics with { Auxiliary = graphics.Auxiliary with { Slots = graphics.Auxiliary.Slots.SetItem(1, task) } },
            "wrong-tail" => graphics with { Auxiliary = graphics.Auxiliary with { Tail = 1 } },
            "missing-head" => graphics with { Auxiliary = graphics.Auxiliary with { Head = null } },
            "wrong-capacity" => graphics with { Auxiliary = graphics.Auxiliary with { Slots = graphics.Auxiliary.Slots.RemoveAt(15) } },
            _ => graphics
        };
        string before = NativeBattleTestSnapshots.Json(graphics);
        BattleDiagnosticTextureQuiescence observation = BattlePresentationProcess.CaptureDiagnosticTextureQuiescence(graphics);
        Assert.False(observation.IsTerminalQuiescent);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(graphics));
        if (fault is "pointer-and-owner-unknown" or "foreign-kind")
        {
            Assert.Null(Assert.Single(observation.Tasks).ModelPointer);
        }
        if (fault is "sequence-unknown" or "foreign-kind")
        {
            Assert.Null(Assert.Single(observation.Tasks).CapturedSequence);
        }
        if (fault is "cycle" or "orphan-active" or "free-linked" or "wrong-tail" or "missing-head" or "wrong-capacity")
        {
            Assert.False(observation.Complete);
        }
    }

    [Fact]
    public void Current_component_observer_preserves_the_proved_resident_program_and_fingerprint()
    {
        const ushort effect = 101;
        BattleSession logical = new(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleGraphicsState graphics = Fixture() with { OriginalEffect = new(effect, effect - 1, new(effect, 1, 1), null, 0), PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(logical, graphics);
        Assert.Same(graphics, process.Graphics);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeEffectMemory), memory);
        BattleDiagnosticInvocationOwners owners = new(0, 0, 0, 0, false, true, 0, false, 0, 0, false, 0, false, false, false, false)
        {
            ObservedOriginalEffect = true,
            SelectedReturnedDescriptor = 0,
            OriginalPools = []
        };
        BattleComponentTextureQuiescenceObservation before = Assert.IsType<BattleComponentTextureQuiescenceObservation>(process.ObserveComponentTerminalTextures(effect, owners, ["effect-or-auxiliary-task"]));
        Assert.True(before.IsEligible);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(memory, process.Memory);
        Assert.Null(process.ObserveComponentTerminalTextures(effect, owners with { ActionOrCommand = true }, ["effect-or-auxiliary-task"]));

        NativeBattleAuxiliaryStep next = graphics.Auxiliary.Advance(new()
        {
            ReadModel = graphics.ActorModels.Get,
            ApplyTexture = (_, _, _) => throw new InvalidOperationException("Resident texture animation may not change texture bytes at its fixed point."),
            RestoreTextureBlink = _ => throw new InvalidOperationException("Resident texture animation still owns its blink state.")
        });
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics with { Auxiliary = next.State, Callback = graphics.Callback + 1 });
        BattleComponentTextureQuiescenceObservation after = Assert.IsType<BattleComponentTextureQuiescenceObservation>(process.ObserveComponentTerminalTextures(effect, owners, ["effect-or-auxiliary-task"]));
        Assert.Equal(before.ResidentFingerprint, after.ResidentFingerprint);
        Assert.Equal(before.GraphicsCallback + 1, after.GraphicsCallback);
        Assert.Equal(before.Textures.Tasks[0].ModelActorId, after.Textures.Tasks[0].ModelActorId);
    }

    [Fact]
    public void Live_typed_model_owner_preserves_quiescence_with_an_unknown_original_pointer()
    {
        NativeBattleGraphicsState graphics = Fixture();
        NativeBattleAuxiliaryTask original = graphics.Auxiliary.Slots[0];
        NativeBattleAuxiliaryTask typed = original with { UnknownByteMask = original.UnknownByteMask | 0xFu };
        graphics = graphics with { Auxiliary = graphics.Auxiliary with { Slots = graphics.Auxiliary.Slots.SetItem(0, typed) } };
        BattleDiagnosticTextureQuiescence observation = BattlePresentationProcess.CaptureDiagnosticTextureQuiescence(graphics);
        Assert.True(observation.IsTerminalQuiescent);
        BattleDiagnosticTextureTask task = Assert.Single(observation.Tasks);
        Assert.Null(task.ModelPointer);
        Assert.Null(task.ModelIndex);
        Assert.False(string.IsNullOrWhiteSpace(task.ModelActorId));
        Assert.Equal((byte)11, task.CapturedSequence);
    }

    [Fact]
    public void Empty_native_bank_is_not_a_nonempty_terminal_owner_certificate()
    {
        BattleDiagnosticTextureQuiescence observation = BattlePresentationProcess.CaptureDiagnosticTextureQuiescence(Fixture() with { Auxiliary = NativeBattleAuxiliaryState.Clear() });
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
        BattleDiagnosticTextureQuiescence original = BattlePresentationProcess.CaptureDiagnosticTextureQuiescence(Fixture(3));
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
