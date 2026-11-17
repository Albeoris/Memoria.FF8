using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedPresentationInitializationTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;

    private static readonly BattlePartyModelOwner Caster = new BattlePartyActorModel(new("caster"));
    private static readonly BattleEnemyModelOwner Target = new BattleEnemyActorModel(new("target"));
    private static readonly BattleModelOwner Other = new BattleEnemyActorModel(new("other"));
    private static readonly BattleModelOwner SavedParty1 = OriginalOwner(1);
    private static readonly BattleModelOwner SavedParty2 = OriginalOwner(2);

    [Theory]
    [InlineData(7, 24)]
    [InlineData(24, 7)]
    public void DeferredHostBindsSparseRosterModelsGeometryAndColdContext(byte source, byte target)
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(source, target, NativeBattleScriptedEffectMemory.FromColdProcess(), 256);
        BattlePresentationProcess owner = BattlePresentationProcess.CreateEffectPresentation(inputs);
        NativeBattleGraphicsState graphics = owner.Graphics;
        Assert.False(owner.EffectPresentationInvoked);
        Assert.Equal(new BattleModelOwner[] { Caster, Target }, graphics.ActorModels.Owners);
        Assert.False(graphics.NativeBindings.TryEncodeExactActor(Caster, graphics.ActorModels, out _));
        Assert.False(graphics.NativeBindings.TryEncodeExactActor(Target, graphics.ActorModels, out _));
        Assert.Equal(Caster, graphics.ActionContext!.ResolveSource(graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal(Target, graphics.ActionContext.ResolveTarget(graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal(BattleModelTargets.From([Target]), graphics.ActionContext.TargetActors!.Resolve(graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal(256, graphics.ActionGraphics!.FirstHeaderTargetCount);
        NativeBattleCinematicActorContext context = owner.Memory.ScriptedEffects!.PresentationActors!;
        Assert.Empty(context.ReadSavedActors());
        Assert.Null(context.FirstActor);
        Assert.Empty(context.CurrentActors);
        Assert.Throws<NotSupportedException>(() => context.ReadActorOrdinal(Caster));
        Assert.Throws<NotSupportedException>(() => context.ReadActorOrdinal(Target));
        Assert.Equal(graphics.ActorModels.Get(Target).Transform!.Position28, context.ReadCachedActor(Target).Position28);
        Assert.False(context.CachedActors.ContainsKey(OriginalOwner(6)));
        graphics.Validate();

        NativeBattleContactPacket[] replacements = Enumerable.Repeat(Packet(Target, 91), 256).ToArray();
        owner.ReplaceHostedEffectContacts(replacements);
        Assert.Equal((ushort)91, owner.NativeActionPackets.ReadContact(new BattleContactOccurrence(255)).Primary.Amount6);
    }

    [Fact]
    public void HostedActivationBindingKeepsItsRosterResourceOwnerWithoutSequenceGlobals()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1);
        inputs = inputs with { NativeTargets = [inputs.NativeTargets[0] with { CanActivate = true }] };

        BattlePresentationProcess owner = BattlePresentationProcess.CreateEffectPresentation(inputs);

        Assert.Equal(Combat(inputs).Packets.Roster.Owners, owner.ModelCpuResources.Cpu.PresentationRoster!.Owners);
        Assert.True(owner.Graphics.ActorModels.Contains(Target));
    }

    [Fact]
    public void RetainedSequenceGlobalsDoNotBypassNativeEnemyActivationResources()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(4, 3, NativeBattleScriptedEffectMemory.FromColdProcess(), 1, hosted: false, enemySource: true);
        inputs = inputs with
        {
            Source = inputs.Source with { ModelId4 = 16 },
            NativeTargets = [inputs.NativeTargets[0] with { CanActivate = true }],
            SequenceGlobals = new(123, 456)
        };

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => BattlePresentationProcess.CreateEffectPresentation(inputs));

        Assert.Contains("Native500EA0 requires its installed stage", error.Message);
    }

    [Fact]
    public void PhysicalPresentationForkPreservesSourceBindingAndRejectsHostedOwnership()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess();
        NativeBattleEffectPresentationInputs physical = Inputs(1, 3, core, 1, hosted: false);
        using NativeBattleEffectPresentation original = new(physical);
        using NativeBattleEffectPresentation fork = original.Fork();

        Assert.Equal(Caster, fork.Graphics.NativeBindings.DecodeLiteral(1, fork.Graphics.ActorModels));
        Assert.Equal(original.Source, fork.Source);
        NativeBattleGraphicsState before = original.Graphics;
        Assert.Throws<InvalidDataException>(() => original.PublishSource(Caster, original.Source));
        Assert.Same(before, original.Graphics);

        using NativeBattleEffectPresentation activation = new(physical with { NativeTargets = [physical.NativeTargets[0] with { CanActivate = true }] });
        Assert.Throws<InvalidOperationException>(() => activation.Fork());

        using NativeBattleEffectPresentation hosted = new(Inputs(7, 24, core, 1));
        Assert.Throws<InvalidOperationException>(() => hosted.Fork());
        Assert.Throws<InvalidDataException>(() => hosted.PublishSource(Caster, hosted.Source));
        Assert.Equal(hosted.Source, hosted.Graphics.ActorModels.Get(Caster));
    }

    [Theory]
    [InlineData("known")]
    [InlineData("count-unknown")]
    [InlineData("byte-unknown")]
    [InlineData("unbound")]
    [InlineData("too-many")]
    public void HostSeedsOnlyOwnedSavedListFactsAndOrdinals(string kind)
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.Unknown()
            .Write(Header + 0xCC + 1, [2]).Write(Header + 0xCC + 2, [25])
            .Write(Header + 0x174, [1, 2]).Write(Header + 0x17A, [2]);
        core = kind switch
        {
            "count-unknown" => core.MarkUnknown(Header + 0x17A, 1),
            "byte-unknown" => core.MarkUnknown(Header + 0x175, 1),
            "unbound" => core.Write(Header + 0x175, [6]),
            "too-many" => core.Write(Header + 0x17A, [7]),
            _ => core
        };
        BattlePresentationProcess owner = BattlePresentationProcess.CreateEffectPresentation(Inputs(7, 24, core, 1, bindOriginalRows: true));
        NativeBattleCinematicActorContext context = owner.Memory.ScriptedEffects!.PresentationActors!;
        if (kind == "known")
        {
            Assert.Equal(new[] { SavedParty1, SavedParty2 }, context.ReadSavedActors());
        }
        else
        {
            Assert.Null(context.SavedActors);
        }
        Assert.Equal(2, context.ReadActorOrdinal(SavedParty1));
        Assert.Throws<NotSupportedException>(() => context.ReadActorOrdinal(OriginalOwner(2)));
        Assert.Throws<NotSupportedException>(() => context.ReadActorOrdinal(Target));
    }

    [Fact]
    public void MatchingRetainedContextKeepsListsOrdinalsAndCursorButRefreshesCache()
    {
        NativeBattleCinematicActorContext retained = NativeBattleCinematicActorContext.FromGraphics(new BattleActorModels([new(Caster, new(0)), new(Target, new(0))]))
            .CaptureCurrentContacts([Target, Caster], Caster).WithSavedActors([Caster, Target]).WithContactsProcessed(50);
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess() with { PresentationActors = retained };
        NativeBattleEffectPresentationInputs inputs = Inputs(7, 24, core, 1);
        BattlePresentationProcess owner = BattlePresentationProcess.CreateEffectPresentation(inputs);
        NativeBattleCinematicActorContext actual = owner.Memory.ScriptedEffects!.PresentationActors!;
        Assert.Equal(new BattleModelOwner[] { Target, Caster }, actual.CurrentActors);
        Assert.Equal(Target, actual.FirstActor);
        Assert.Equal(new BattleModelOwner[] { Caster, Target }, actual.ReadSavedActors());
        Assert.Equal(1, actual.ReadActorOrdinal(Caster));
        Assert.Equal(50, actual.ContactsProcessed);
        Assert.NotNull(actual.ReadCachedActor(Caster).Position28);
        Assert.Null(retained.ReadCachedActor(Caster).Position28);
    }

    [Fact]
    public void HostRejectsAnExistingContextWithDifferentActorIdentity()
    {
        BattleActorModels different = new([new(Caster, new(0)), new(Other, new(0))]);
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(different);
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess() with { PresentationActors = context };
        Assert.Throws<InvalidDataException>(() => BattlePresentationProcess.CreateEffectPresentation(Inputs(7, 24, core, 1)));
    }

    [Theory]
    [InlineData("target-id")]
    [InlineData("target-side")]
    [InlineData("source-side")]
    [InlineData("duplicate")]
    [InlineData("unbound")]
    [InlineData("missing")]
    public void HostedConstructionRejectsContradictoryBindings(string kind)
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1);
        NativeBattleEffectModelBinding target = inputs.NativeTargets[0];
        inputs = kind switch
        {
            "target-id" => inputs with { NativeTargets = [target with { Owner = Other }] },
            "target-side" => inputs with { NativeTargets = [target with { Owner = new BattlePartyActorModel(new("target")) }] },
            "source-side" => inputs with { Invocation = Combat(inputs) with { Parameters = Combat(inputs).Parameters with { Source = new BattleEnemyActorModel(new("caster")) } } },
            "duplicate" => inputs with { NativeTargets = [target, target] },
            "unbound" => inputs with { NativeTargets = [target with { Owner = new BattleEnemyActorModel(new("unbound")) }] },
            _ => inputs with { NativeTargets = [] }
        };
        Assert.Throws<InvalidDataException>(() => BattlePresentationProcess.CreateEffectPresentation(inputs));
    }

    [Fact]
    public void NativePresentationStillOmitsRosterAndCinematicContextFromJson()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(0, 3, NativeBattleScriptedEffectMemory.FromColdProcess(), 1, hosted: false);
        BattlePresentationProcess owner = BattlePresentationProcess.CreateEffectPresentation(inputs);
        Assert.Equal(7, owner.Graphics.NativeBindings.CaptureOriginalCells(owner.Graphics.ActorModels).Length);
        Assert.Equal(Caster, owner.Graphics.NativeBindings.DecodeLiteral(0, owner.Graphics.ActorModels));
        Assert.DoesNotContain("\"PresentationRoster\"", NativeBattleTestSnapshots.Json(owner.Graphics));
        Assert.DoesNotContain("\"PresentationActors\"", NativeBattleTestSnapshots.Json(owner.Memory.ScriptedEffects));
    }

    [Fact]
    public void CameraAliasReferencesReachHostedRowsWhileLiteralSevenAndEightRemainUnsupported()
    {
        BattlePresentationProcess owner = BattlePresentationProcess.CreateEffectPresentation(Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1));
        MethodInfo transform = typeof(BattlePresentationProcess).GetMethod("TransformNativeCameraReference", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleCameraPoint Read(byte reference) => (NativeBattleCameraPoint)transform.Invoke(owner, [reference, new NativeBattleCameraPoint(0, 0, 0), 0u])!;
        Assert.Equal(new NativeBattleCameraPoint(700, 0, 107), Read(0x9F));
        Assert.Equal(new NativeBattleCameraPoint(2400, 0, 124), Read(0xAF));
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Read(0x7F)).InnerException);
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Read(0x8F)).InnerException);
    }

    [Fact]
    public void HostedForeignBindingRequiresCompleteLoadedModelFacts()
    {
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [.. Enumerable.Repeat((sbyte)-1, 16)], [.. Enumerable.Repeat((sbyte)-1, 32)])
        {
            SummonColors = NativeBattleGteColorInterpolation.CodeConstants45F270 with { Normal = 0x808080 }
        };
        NativeBattleEffectPresentationInputs inputs = Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1, graphics: new(rules, [], []));
        NativeBattleForeignActor foreign = new(Target, Model(24).Transform!, new(0, 0, 0, 0), (_, _) => default) { StatusFlags = 0 };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattlePresentationProcess.CreateEffectPresentation(inputs with { NativeTargets = [], ForeignActors = [foreign] }));
        Assert.Contains("Loaded native graphics model", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IfritPositioningUsesTheExplicitPartySideForHostedCaster(bool party)
    {
        var fixture = NativeBattleIfritEntryTests.Fixture();
        BattleModelOwner caster = party ? Caster : Other;
        BattleActorModels models = new([new(caster, Model(24))]);
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models);
        NativeBattleEffectMemory memory = fixture.Memory with { ScriptedEffects = fixture.Memory.ScriptedEffects! with { PresentationActors = context } };
        NativeBattleWaterResetCaptureInputs selection = new(caster, null, null, caster, [caster], models, NativeBattleModelBoundary.FromRetainedMemory(null), BattleContactTaskStorageContract.ExpandableArena);
        NativeBattleWaterInitializationInputs inputs = fixture.Inputs with { Selection = selection, FirstHeaderCount = 1 };
        NativeBattleOriginalEffectState effect = fixture.Effect with { Parameters = null };
        if (!party)
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleCinematicEntry.InitializeIfritPresentation(memory, effect, fixture.Directories, inputs));
            return;
        }
        NativeBattleCinematicEntryStep result = NativeBattleCinematicEntry.InitializeIfritPresentation(memory, effect, fixture.Directories, inputs);
        Assert.Equal(caster, result.Memory.ScriptedEffects!.PresentationActors!.ReadFirstActor());
        Assert.Equal(0xB25DF0u, Assert.Single(Assert.Single(result.Effect.Pools).Slots).Callback);
    }

    private static NativeBattleEffectInvocation.Combat Combat(NativeBattleEffectPresentationInputs inputs) => (NativeBattleEffectInvocation.Combat)inputs.Invocation;

    internal static NativeBattleEffectPresentationInputs Inputs(byte source, byte target, NativeBattleScriptedEffectMemory core, int count, bool bindOriginalRows = false, bool hosted = true, bool enemySource = false, NativeBattleGraphicsDefinition? graphics = null)
    {
        BattleModelOwner sourceOwner = enemySource ? new BattleEnemyActorModel(new("caster")) : Caster;
        NativeBattleGraphicsResource resource = new([0, 0]);
        List<NativeBattleEffectModelBinding> targets = [new(Target, Model(target), resource)];
        List<(NativeBattleModelSlot, BattleModelOwner)> bindings = [];
        if (bindOriginalRows)
        {
            bindings.Add((NativeBattleModelSlot.Decode(1), SavedParty1));
            bindings.Add((NativeBattleModelSlot.Decode(2), SavedParty2));
            targets.Add(new(SavedParty1, Model(1), resource));
            targets.Add(new(SavedParty2, Model(2), resource));
        }
        if (!hosted)
        {
            bindings.Add((NativeBattleModelSlot.Decode(source), sourceOwner));
            bindings.Add((NativeBattleModelSlot.Decode(target), Target));
        }
        BattleModelRoster roster = new(targets.Select(binding => binding.Owner).Prepend(sourceOwner));
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), new(roster, bindings));
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(sourceOwner, 8, 0, -1, 1, 1, null));
        packets = packets.AppendContacts(Enumerable.Repeat(Packet(Target, 1), count));
        (packets, _) = packets.Publish(count);
        NativeBattleEffectParameters parameters = new(sourceOwner, 0, BattleModelTargets.From([Target]), header, 0, 0);
        BattleDefinition definition = new()
        {
            NativeEffectCatalog = [.. Enumerable.Range(1, 400).Select(effect => new NativeBattleEffectBinding((ushort)effect, effect == 1 ? 1u : 0u, 0))],
            NativeRetainedMemory = RetainedMemory(core)
        };
        return new(new(definition, graphics), Model(source), resource, new NativeBattleEffectInvocation.Combat(packets, parameters), 1, NativeBattleEffectTextureState.FromColdProcess(), new(0), new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108), [])
        {
            NativeTargets = [.. targets],
            DeferInvocation = true
        };
    }

    private static NativeBattleRetainedMemory RetainedMemory(NativeBattleScriptedEffectMemory core)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { ScriptedEffects = core, PrimitiveProcess = memory.PrimitiveProcess!.Initialize500400() };
        return NativeBattleRetainedMemory.FromColdProcess() with { EffectMemory = memory };
    }

    private static NativeBattleGraphicsModel Model(int row)
    {
        NativeBattleVector position = new((short)(100 * row), 0, (short)(100 + row));
        return new(0)
        {
            ModelId4 = 0,
            Flags2 = 0,
            Rotation12 = 0,
            Rotation14 = 0,
            Rotation16 = 0,
            Transform = new(position, position, 0, 0, 4096),
            GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = position.X, Y = position.Y, Z = position.Z }
        };
    }

    private static NativeBattleContactPacket Packet(BattleModelOwner owner, ushort amount) => new(new(owner, 0, 0, 0, 0, amount, 0), null);
}
