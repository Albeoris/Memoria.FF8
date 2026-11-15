using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedPresentationInitializationTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;

    [Theory]
    [InlineData(7, 24)]
    [InlineData(24, 7)]
    public void DeferredHostBindsSparseRosterModelsGeometryAndColdContext(byte source, byte target)
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(source, target, NativeBattleScriptedEffectMemory.FromColdProcess(), 256);
        BattleSession owner = BattleSession.CreateEffectPresentation(inputs);
        NativeBattleGraphicsState graphics = owner.NativeGraphics!;
        Assert.False(owner.EffectPresentationInvoked);
        Assert.Same(inputs.Packets.PresentationRoster, graphics.PresentationRoster);
        Assert.Equal(25, graphics.Models.Length);
        Assert.Equal(Enumerable.Range(0, 25), graphics.Models.Select(model => model.Index));
        Assert.Equal("caster", graphics.ActorIds[source]);
        Assert.Equal("target", graphics.ActorIds[target]);
        Assert.Null(graphics.ActorIds[6]);
        Assert.Equal(source, graphics.ActionContext!.ActorIndex);
        Assert.Equal(target, graphics.ActionContext.TargetIndex);
        Assert.Equal(new[] { (int)target }, graphics.ActionContext.TargetActors!.Value.EnumerateRows());
        Assert.Equal(256, graphics.ActionGraphics!.FirstHeaderTargetCount);
        Assert.Empty(owner.NativeEffectMemory!.ScriptedEffects!.PresentationActors!.ReadSavedActors());
        NativeBattleCinematicActorContext context = owner.NativeEffectMemory.ScriptedEffects.PresentationActors!;
        Assert.Null(context.FirstActor);
        Assert.Empty(context.CurrentActors);
        Assert.Equal((byte)0, context.ReadActorOrdinal(source));
        Assert.Equal((byte)0, context.ReadActorOrdinal(target));
        Assert.Equal(graphics.Models[target].Transform!.Position28, context.ReadCachedActor(target).Position28);
        Assert.Null(context.CachedActors[OriginalOwner(6)]);
        graphics.Validate();

        NativeBattleContactPacket[] replacements = Enumerable.Repeat(Packet(target, 91), 256).ToArray();
        owner.ReplaceHostedEffectContacts(replacements);
        Assert.Equal((ushort)91, owner.NativeActionPackets.ReadContact(new BattleContactOccurrence(255)).Primary.Amount6);
    }

    [Fact]
    public void HostedActivationBindingKeepsItsRosterResourceOwnerWithoutSequenceGlobals()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1);
        inputs = inputs with { NativeTargets = [inputs.NativeTargets[0] with { CanActivate = true }] };

        BattleSession owner = BattleSession.CreateEffectPresentation(inputs);

        Assert.Same(inputs.Packets.PresentationRoster, owner.NativeModelCpuResources!.Cpu.PresentationRoster);
        Assert.Equal("target", owner.NativeGraphics!.ActorIds[24]);
    }

    [Fact]
    public void RetainedSequenceGlobalsDoNotBypassNativeEnemyActivationResources()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(0, 3, NativeBattleScriptedEffectMemory.FromColdProcess(), 1, hosted: false);
        inputs = inputs with
        {
            Source = inputs.Source with { ModelId4 = 16 },
            SourceIsParty = false,
            NativeTargets = [inputs.NativeTargets[0] with { CanActivate = true }],
            SequenceGlobals = new(123, 456)
        };

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => BattleSession.CreateEffectPresentation(inputs));

        Assert.Contains("Native500EA0 requires its installed stage", error.Message);
    }

    [Fact]
    public void PhysicalPresentationForkPreservesSourceRowAndRejectsHostedOwnership()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess();
        NativeBattleEffectPresentationInputs physical = Inputs(1, 3, core, 1, hosted: false);
        using NativeBattleEffectPresentation original = new(physical);
        using NativeBattleEffectPresentation fork = original.Fork();

        Assert.Equal(1, fork.Source.Index);
        Assert.Equal(original.Source, fork.Source);
        NativeBattleGraphicsState before = original.Graphics;
        Assert.Throws<InvalidDataException>(() => original.PublishSource(original.Source with { Index = 0 }));
        Assert.Same(before, original.Graphics);

        using NativeBattleEffectPresentation activation = new(physical with { NativeTargets = [physical.NativeTargets[0] with { CanActivate = true }] });
        Assert.Throws<InvalidOperationException>(() => activation.Fork());

        using NativeBattleEffectPresentation hosted = new(Inputs(7, 24, core, 1));
        Assert.Throws<InvalidOperationException>(() => hosted.Fork());
        Assert.Throws<InvalidDataException>(() => hosted.PublishSource(hosted.Source with { Index = 0 }));
        Assert.Equal(7, hosted.Source.Index);
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
        BattleSession owner = BattleSession.CreateEffectPresentation(Inputs(7, 24, core, 1, bindOriginalRows: true));
        NativeBattleCinematicActorContext context = owner.NativeEffectMemory!.ScriptedEffects!.PresentationActors!;
        if (kind == "known")
        {
            Assert.Equal(new byte[] { 1, 2 }, context.ReadSavedActors());
        }
        else
        {
            Assert.Null(context.SavedActors);
        }
        Assert.Equal((byte)2, context.ReadActorOrdinal(OriginalOwner(1)));
        Assert.Throws<NotSupportedException>(() => context.ReadActorOrdinal(OriginalOwner(2)));
        Assert.Equal((byte)0, context.ReadActorOrdinal(24));
    }

    [Fact]
    public void MatchingRetainedContextKeepsListsOrdinalsAndCursorButRefreshesCache()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1);
        NativeBattleCinematicActorContext retained = NativeBattleCinematicActorContext.FromGraphics(inputs.Packets.PresentationRoster!, IndexedModels(25))
            .CaptureCurrentContacts([24, 7], 7).WithSavedActors([7, 24]).WithContactCursor(50);
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess() with { PresentationActors = retained };
        inputs = inputs with { Resources = inputs.Resources with { NativeRetainedMemory = RetainedMemory(core) } };
        BattleSession owner = BattleSession.CreateEffectPresentation(inputs);
        NativeBattleCinematicActorContext actual = owner.NativeEffectMemory!.ScriptedEffects!.PresentationActors!;
        Assert.Equal(new byte[] { 24, 7 }, actual.CurrentActors);
        Assert.Equal((byte)24, actual.FirstActor);
        Assert.Equal(new byte[] { 7, 24 }, actual.ReadSavedActors());
        Assert.Equal((byte)1, actual.ReadActorOrdinal(7));
        Assert.Equal(50, actual.ContactCursor);
        Assert.NotNull(actual.ReadCachedActor(7).Position28);
        Assert.Null(retained.ReadCachedActor(7).Position28);
    }

    [Fact]
    public void HostRejectsAnExistingContextWithDifferentActorIdentity()
    {
        NativeBattleEffectPresentationInputs original = Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1);
        BattleModelRoster originalRoster = original.Packets.PresentationRoster!;
        BattleModelRoster different = new(originalRoster.Rows.SetItem(24, new("different", false)));
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(different, IndexedModels(25));
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess() with { PresentationActors = context };
        Assert.Throws<InvalidDataException>(() => BattleSession.CreateEffectPresentation(Inputs(7, 24, core, 1)));
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
            "target-id" => inputs with { NativeTargets = [target with { ActorId = "other" }] },
            "target-side" => inputs with { NativeTargets = [target with { IsParty = true }] },
            "source-side" => inputs with { SourceIsParty = false },
            "duplicate" => inputs with { NativeTargets = [target, target] },
            "unbound" => inputs with { NativeTargets = [target with { Model = target.Model with { Index = 23 } }] },
            _ => inputs with { NativeTargets = [] }
        };
        Assert.Throws<InvalidDataException>(() => BattleSession.CreateEffectPresentation(inputs));
    }

    [Fact]
    public void NativePresentationStillOmitsRosterAndCinematicContextFromJson()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(0, 3, NativeBattleScriptedEffectMemory.FromColdProcess(), 1, hosted: false);
        BattleSession owner = BattleSession.CreateEffectPresentation(inputs);
        Assert.Equal(7, owner.NativeGraphics!.Models.Length);
        Assert.Equal("source", owner.NativeGraphics.ActorIds[0]);
        Assert.DoesNotContain("\"PresentationRoster\"", NativeBattleTestSnapshots.Json(owner.NativeGraphics));
        Assert.DoesNotContain("\"PresentationActors\"", NativeBattleTestSnapshots.Json(owner.NativeEffectMemory!.ScriptedEffects));
    }

    [Fact]
    public void CameraAliasReferencesReachHostedRowsWhileLiteralSevenAndEightRemainUnsupported()
    {
        BattleSession owner = BattleSession.CreateEffectPresentation(Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1));
        MethodInfo transform = typeof(BattleSession).GetMethod("TransformNativeCameraReference", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleCameraPoint Read(byte reference) => (NativeBattleCameraPoint)transform.Invoke(owner, [reference, new NativeBattleCameraPoint(0, 0, 0), 0u])!;
        Assert.Equal(new NativeBattleCameraPoint(700, 0, 107), Read(0x9F));
        Assert.Equal(new NativeBattleCameraPoint(2400, 0, 124), Read(0xAF));
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Read(0x7F)).InnerException);
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Read(0x8F)).InnerException);
    }

    [Fact]
    public void HostedForeignBindingRequiresCompleteLoadedModelFacts()
    {
        NativeBattleEffectPresentationInputs inputs = Inputs(7, 24, NativeBattleScriptedEffectMemory.FromColdProcess(), 1);
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [.. Enumerable.Repeat((sbyte)-1, 16)], [.. Enumerable.Repeat((sbyte)-1, 32)])
        {
            SummonColors = NativeBattleGteColorInterpolation.CodeConstants45F270 with { Normal = 0x808080 }
        };
        inputs = inputs with { Resources = inputs.Resources with { NativeGraphics = new(rules, [], []) } };
        NativeBattleForeignActor foreign = new(24, Model(24).Transform!, new(0, 0, 0, 0), (_, _) => default) { IsParty = false, StatusFlags = 0 };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattleSession.CreateEffectPresentation(inputs with { NativeTargets = [], ForeignActors = [foreign] }));
        Assert.Contains("Loaded native graphics row", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IfritPositioningUsesTheExplicitPartySideForHostedCaster(bool party)
    {
        var fixture = NativeBattleIfritEntryTests.Fixture();
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[24] = new("caster", party);
        BattleModelRoster roster = new([.. rows]);
        ImmutableArray<NativeBattleGraphicsModel> models = IndexedModels(25).SetItem(24, Model(24));
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(roster, models);
        NativeBattleEffectMemory memory = fixture.Memory with { ScriptedEffects = fixture.Memory.ScriptedEffects! with { PresentationActors = context } };
        NativeBattleWaterInitializationInputs inputs = fixture.Inputs with { Caster = 24, FirstHeaderFirstActor = 24, Contacts = [24], ContactCount = 1, FirstHeaderCount = 1, Graphics = models };
        NativeBattleOriginalEffectState effect = fixture.Effect with { Parameters = null, PresentationRoster = roster };
        if (!party)
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleCinematicEntry.InitializeIfritPresentation(memory, effect, fixture.Directories, inputs));
            return;
        }
        NativeBattleCinematicEntryStep result = NativeBattleCinematicEntry.InitializeIfritPresentation(memory, effect, fixture.Directories, inputs);
        Assert.Equal((byte)24, result.Memory.ScriptedEffects!.PresentationActors!.ReadFirstActor());
        Assert.Equal(0xB25DF0u, Assert.Single(Assert.Single(result.Effect.Pools).Slots).Callback);
    }

    private static NativeBattleEffectPresentationInputs Inputs(byte source, byte target, NativeBattleScriptedEffectMemory core, int count, bool bindOriginalRows = false, bool hosted = true)
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[source] = new BattlePartyActorModel(new("caster"));
        rows[target] = new BattleEnemyActorModel(new("target"));
        NativeBattleGraphicsResource resource = new([0, 0]);
        List<NativeBattleEffectModelBinding> targets = [new("target", Model(target), resource) { IsParty = false }];
        if (bindOriginalRows)
        {
            rows[1] = new BattlePartyActorModel(new("original-1"));
            rows[2] = new BattleEnemyActorModel(new("original-2"));
            targets.Add(new("original-1", Model(1), resource));
            targets.Add(new("original-2", Model(2), resource));
        }
        BattleModelRoster? roster = hosted ? new([.. rows]) : null;
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("target", Packet(target, 1)), count), roster);
        NativeBattleActionPacketsState packets;
        NativeBattleEffectParameters parameters;
        if (hosted)
        {
            (packets, BattleModelTargets actors) = bridge.PrepareHosted(8, 0, 1, 1, source);
            parameters = new(source, 0, actors.NativePhysicalBits, 0, 0, 0) { PresentationTargets = actors };
        }
        else
        {
            (packets, ushort mask) = bridge.Prepare(8, 0, 1, 1, source);
            parameters = new(source, 0, mask, new BattleActionHeaderReference(0), 0, 0);
        }
        BattleDefinition definition = new()
        {
            NativeEffectCatalog = [.. Enumerable.Range(1, 400).Select(effect => new NativeBattleEffectBinding((ushort)effect, effect == 1 ? 1u : 0u, 0))],
            NativeRetainedMemory = RetainedMemory(core)
        };
        return new(definition, Model(source), resource, packets, parameters, 1, NativeBattleEffectTextureState.FromColdProcess(), new(0), new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108), [])
        {
            SourceIsParty = true,
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

    private static ImmutableArray<NativeBattleGraphicsModel> IndexedModels(int count) => [.. Enumerable.Range(0, count).Select(row => new NativeBattleGraphicsModel(0))];

    private static NativeBattleContactPacket Packet(byte row, ushort amount) => new(new(row, 0, 0, 0, 0, amount, 0), new(255, 0, 0, 0, 0, 0, 0));
}
