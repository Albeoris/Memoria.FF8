using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrimitiveAdmissionTests
{
    [Theory]
    [InlineData(0, "graphics definition")]
    [InlineData(1, "primitive dispatch tables")]
    [InlineData(2, "software primitive dispatch table")]
    [InlineData(3, "hardware execution capability")]
    [InlineData(4, "hardware execution capability")]
    [InlineData(5, "256-entry")]
    [InlineData(6, "256-entry")]
    public void Scene_rejects_incomplete_primitive_resources_before_publishing_owners(int missing, string required)
    {
        var (definition, graphics) = Scene();
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        string original = NativeBattleTestSnapshots.Json(battle.Capture());

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => battle.AttachPresentation(new(definition, Break(graphics, missing))));

        Assert.Contains(required, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(battle.Presentation);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(battle.Capture()));
    }

    [Theory]
    [InlineData(0, "graphics definition")]
    [InlineData(1, "primitive dispatch tables")]
    [InlineData(2, "software primitive dispatch table")]
    [InlineData(3, "hardware execution capability")]
    [InlineData(4, "hardware execution capability")]
    [InlineData(5, "256-entry")]
    [InlineData(6, "256-entry")]
    public void Deferred_and_immediate_effect_construction_share_the_same_resource_preflight(int missing, string required)
    {
        NativeBattleEffectPresentationInputs inputs = Effect();
        NativeBattleGraphicsDefinition graphics = inputs.Resources.NativeGraphics!;
        BattlePresentationResources incomplete = new(new(), Break(graphics, missing));
        object before = inputs.Textures.VramPublicationIdentity;

        foreach (bool defer in new[] { true, false })
        {
            NativeBattleEffectPresentationInputs rejected = inputs with { Resources = incomplete, DeferInvocation = defer };
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattlePresentationProcess.CreateEffectPresentation(rejected));
            Assert.Contains(required, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Same(before, inputs.Textures.VramPublicationIdentity);
        }
    }

    [Fact]
    public void Scene_attach_private_fork_and_restore_require_the_same_primitive_owners()
    {
        var (definition, graphics) = Scene();
        BattlePresentationResources resources = new(definition, graphics);
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.AttachPresentation(resources);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.NotNull(process.Graphics.PrimitiveFrame);
        Assert.NotNull(process.Memory.PrimitiveProcess);

        BattleSession fork = battle.ForkForContinuation();
        Assert.NotSame(process, fork.Presentation);
        Assert.NotNull(fork.Presentation!.Graphics.PrimitiveFrame);

        BattleSnapshot snapshot = battle.CaptureForContinuation();
        BattleSession restored = BattleSession.Restore(snapshot);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => restored.AttachRestoredPresentation(new(definition, graphics with { PrimitiveTables = null }), snapshot));
        Assert.Contains("primitive dispatch tables", error.Message);
        Assert.Null(restored.Presentation);
        restored.AttachRestoredPresentation(resources, snapshot);
        Assert.NotNull(restored.Presentation!.Graphics.PrimitiveFrame);
        Assert.Equal(process.Graphics.Callback, restored.Presentation.Graphics.Callback);
    }

    [Fact]
    public void Valid_deferred_effect_and_fork_preserve_initialized_frame_without_dispatch()
    {
        using NativeBattleEffectPresentation effect = new(Effect());
        using NativeBattleEffectPresentation fork = effect.Fork();

        Assert.False(effect.Invoked);
        Assert.False(fork.Invoked);
        Assert.NotNull(effect.Memory.PrimitiveProcess);
        Assert.NotNull(fork.Memory.PrimitiveProcess);
    }

    [Fact]
    public void Empty_modules_and_hidden_models_skip_submission_after_valid_admission()
    {
        NativeBattleEffectPresentationInputs inputs = Effect();
        BattlePresentationProcess process = BattlePresentationProcess.CreateEffectPresentation(inputs);
        NativeBattleGraphicsState graphics = process.Graphics;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo modules = typeof(BattlePresentationProcess).GetMethod("SubmitNativeModulePackets", flags)!;
        MethodInfo models = typeof(BattlePresentationProcess).GetMethod("SubmitNativeModelPackets502D40", flags)!;
        BattleModelOwner source = ((NativeBattleEffectInvocation.Combat)inputs.Invocation).Parameters.Source;

        Assert.Same(graphics, modules.Invoke(process, [graphics]));
        Assert.Same(graphics, models.Invoke(process, [graphics, source, new NativeBattleGraphicsModel(0), inputs.SourceResource]));
        Assert.Empty(graphics.PrimitiveFrame!.Pending.Proxies);
    }

    private static NativeBattleEffectPresentationInputs Effect()
    {
        return NativeBattleHostedPresentationInitializationTests.Inputs(0, 3, NativeBattleScriptedEffectMemory.FromColdProcess(), 1, hosted: false);
    }

    private static (BattleDefinition Definition, NativeBattleGraphicsDefinition Graphics) Scene()
    {
        BattleDefinition definition = new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, Information246 = 0 }
            ],
            StageId = 0,
            NativeStageSource = new(0, [], [], new("stage", [])) { PhysicalSource = new(1, 0, []) },
            NativeStageCameraBank = new([]),
            NativeEntryWaveFile = new(0, "entry", []),
            NativeMusicProcess = new(new(1, null, null, null, 0, null), 0, null),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeGlobalRandom = new(123),
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess()
        };
        NativeBattleGraphicsResource model = new([0, 0])
        {
            AnimationFrames = [1],
            ModelLoadSources = [new(0, false, 0, 1, "body", [])]
        };
        NativeBattleGraphicsDefinition graphics = NativeBattleHostedTestFixture.CreateGraphicsDefinition(
            new("party", 0, model) { InitialPosition = new(0, 0, 0) },
            new("enemy", 16, model) { InitialPosition = new(0, 0, 0) });
        return (definition, NativeBattleHostedTestFixture.PrimitiveExecutionGraphics(graphics));
    }

    private static NativeBattleGraphicsDefinition? Break(NativeBattleGraphicsDefinition graphics, int missing)
    {
        return missing switch
        {
            0 => null,
            1 => graphics with { PrimitiveTables = null },
            2 => graphics with { SoftwarePrimitiveTable = null },
            3 => graphics with { PrimitiveCapabilities = null },
            4 => graphics with { PrimitiveCapabilities = new(true) },
            5 => graphics with { PrimitiveTables = graphics.PrimitiveTables! with { FirstHandlers = [] } },
            6 => graphics with { SoftwarePrimitiveTable = new([]) },
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
    }
}
