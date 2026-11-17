using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionServicesTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo ModelsBusy = typeof(BattlePresentationProcess).GetMethod("NativeActionModelsBusy", PrivateInstance)!;
    private static readonly MethodInfo HeaderGeometry = typeof(BattlePresentationProcess).GetMethod("PrepareNativeHeaderGeometry", PrivateInstance)!;
    private static readonly MethodInfo TypedGeometry = typeof(BattlePresentationProcess).GetMethod("PrepareNativeActionGeometry", PrivateInstance, null,
        [typeof(NativeBattleGraphicsState), typeof(BattleModelOwner), typeof(BattleModelTargets), typeof(BattleModelTargets), typeof(bool)], null)!;

    private static readonly BattlePartyActorModel OriginalAlly = new(new("ally"));
    private static readonly BattleEnemyActorModel OriginalEnemy = new(new("enemy"));
    private static readonly BattleEnemyActorModel OtherOriginalEnemy = new(new("other-enemy"));
    private static readonly BattlePartyActorModel EarlyAlly = new(new("early-ally"));
    private static readonly BattlePartyActorModel LateAlly = new(new("late-ally"));
    private static readonly BattleEnemyActorModel EarlyEnemy = new(new("early-enemy"));
    private static readonly BattleEnemyActorModel LateEnemy = new(new("late-enemy"));
    private static readonly BattleEnemyActorModel Missing = new(new("missing"));

    [Fact]
    public void HostedBusyDistinguishesSelectedTargetsFromAllLoadedBoundModels()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(LateAlly, Model(14, busy: true)) };
        Assert.False(graphics.NativeBindings.CaptureAliases(graphics.ActorModels).TryEncodeExact(LateAlly, out _));
        NativeBattleActionGraphicsState context = Context(EarlyAlly, Targets(EarlyEnemy));
        Assert.False(Busy(session, graphics, context));
        Assert.True(Busy(session, graphics, context with { UseActionCamera = true }));
        Assert.True(Busy(session, graphics, Context(EarlyAlly, Targets(LateEnemy)) with { CoverActors = BattleRetainedModelSelection.From([new BattleRetainedModelReference.CanonicalOwner(OriginalAlly)]) }));
        Assert.True(Busy(session, graphics with { ActorModels = graphics.ActorModels.Set(LateEnemy, Model(24, busy: true)) }, Context(EarlyAlly, Targets(LateEnemy))));
    }

    [Fact]
    public void BoundSourceKeepsTheFirstReadAndMalformedBindingsFailBeforeModelReads()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(EarlyAlly, Model(7, busy: true)).Set(EarlyEnemy, new(2)).Set(LateEnemy, new(2)) };
        NativeBattleActionGraphicsState context = Context(EarlyAlly, Targets(EarlyEnemy, LateEnemy));
        Assert.True(Busy(session, graphics, context));
        AssertInvalid(() => Busy(session, graphics, Context(EarlyAlly, Targets(Missing))));
        AssertInvalid(() => Busy(session, graphics, context with { Actor = new BattleRetainedModelReference.CanonicalOwner(Missing) }));
        AssertInvalid(() => Busy(session, graphics, context with { CoverActors = BattleRetainedModelSelection.From([new BattleRetainedModelReference.CanonicalOwner(Missing)]) }));
        AssertInvalid(() => Busy(session, graphics with { ActorModels = graphics.ActorModels.Remove(LateEnemy) }, context));
        NativeBattleGraphicsState rebound = graphics with { ActorModels = graphics.ActorModels.ReplaceOwner(LateEnemy, Missing, graphics.ActorModels.Get(LateEnemy)) };
        AssertInvalid(() => Busy(session, rebound, context));
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Busy(session, graphics with { ActorModels = graphics.ActorModels.Set(EarlyAlly, Model(7)) }, context)).InnerException);
    }

    [Fact]
    public void NativeAndHostedSevenRowBusyChecksAgree()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState hosted = Graphics(originalOnly: true);
        NativeBattleGraphicsState native = hosted with { Effects = OriginalEffects() };
        NativeBattleActionGraphicsState context = Context(OriginalAlly, Targets(OriginalEnemy));
        foreach (bool camera in new[] { false, true })
        {
            NativeBattleActionGraphicsState selected = context with { UseActionCamera = camera };
            Assert.Equal(Busy(session, native, selected), Busy(session, hosted, selected));
        }
        Assert.False(Busy(session, native, context));
        Assert.True(Busy(session, native, context with { UseActionCamera = true }));
    }

    [Fact]
    public void OrdinarySevenRowHeaderGeometryMatchesNativeAtTheSessionBoundary()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState hosted = Graphics(originalOnly: true);
        NativeBattleGraphicsState native = hosted with { Effects = OriginalEffects() };
        NativeBattleActionGraphicsState context = Context(OriginalAlly, Targets(OriginalEnemy)) with { FirstTargetBinding = new BattleRetainedModelReference.CanonicalOwner(OriginalEnemy) };
        InstallHeader(session, native, OriginalAlly, 1);
        NativeBattleGraphicsState expected = PrepareHeader(session, native, context);
        InstallHeader(session, hosted, OriginalAlly, 1);
        NativeBattleGraphicsState actual = PrepareHeader(session, hosted, context);
        Assert.Equal(expected.ActionContext!.ResolveSource(expected.ActorModels, expected.NativeBindings), actual.ActionContext!.ResolveSource(actual.ActorModels, actual.NativeBindings));
        Assert.Equal(expected.ActionContext.SourceTargets.Resolve(expected.ActorModels, expected.NativeBindings), actual.ActionContext.SourceTargets.Resolve(actual.ActorModels, actual.NativeBindings));
        Assert.Equal(expected.ActionContext.ResolveTarget(expected.ActorModels, expected.NativeBindings), actual.ActionContext.ResolveTarget(actual.ActorModels, actual.NativeBindings));
        Assert.Equal(expected.ActionContext.TargetActors!.Resolve(expected.ActorModels, expected.NativeBindings), actual.ActionContext.TargetActors!.Resolve(actual.ActorModels, actual.NativeBindings));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.ActionContext), NativeBattleTestSnapshots.Json(actual.ActionContext));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.ActorModels.Entries), NativeBattleTestSnapshots.Json(actual.ActorModels.Entries));
    }

    [Fact]
    public void HostedHeaderGeometryUsesExtendedRowsAndPublishesItsAliases()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleSession session = Session();
        InstallHeader(session, graphics, EarlyAlly, 1);
        BattleModelTargets targets = Targets(EarlyEnemy, LateEnemy);
        NativeBattleGraphicsState prepared = PrepareHeader(session, graphics, Context(EarlyAlly, targets));
        NativeBattleEffectMemory memory = session.Presentation!.Memory;
        NativeBattleCameraAliases aliases = memory.CameraAliases!;
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals!;

        Assert.Equal(EarlyAlly, prepared.ActionContext!.ResolveSource(prepared.ActorModels, prepared.NativeBindings));
        Assert.Equal(targets, prepared.ActionContext.SourceTargets.Resolve(prepared.ActorModels, prepared.NativeBindings));
        Assert.Equal(targets, prepared.ActionContext.TargetActors!.Resolve(prepared.ActorModels, prepared.NativeBindings));
        Assert.Equal(LateEnemy, prepared.ActionContext.ResolveTarget(prepared.ActorModels, prepared.NativeBindings));
        Assert.Equal(5, prepared.ActionContext.Matrices!.Value.Length);
        Assert.Equal(EarlyAlly, aliases.Resolve50D790(0x90, prepared.ActorModels, prepared.NativeBindings));
        Assert.Equal(LateEnemy, aliases.Resolve50D790(0xA0, prepared.ActorModels, prepared.NativeBindings));
        Assert.Equal(prepared.ActionContext.ContactDistance, globals.ContactDistance);
        Assert.Equal(prepared.ActionContext.TargetYaw, globals.TargetYaw);

        List<BattleModelOwner> reads = [];
        InstallForeignBones(session, graphics, reads, OriginalAlly);
        NativeBattleGraphicsState cover = PrepareHeader(session, graphics, Context(EarlyAlly, targets) with { CoverActors = BattleRetainedModelSelection.From([new BattleRetainedModelReference.CanonicalOwner(OriginalAlly)]) });
        Assert.Equal(Targets(OriginalAlly), cover.ActionContext!.SourceTargets.Resolve(cover.ActorModels, cover.NativeBindings));
        Assert.Equal(Targets(OriginalAlly), cover.ActionContext.TargetActors!.Resolve(cover.ActorModels, cover.NativeBindings));
        Assert.Equal(new[] { OriginalAlly, OriginalAlly }, reads);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(28)]
    public void HostedOrdinaryEmptyTargetFallbackRetainsAnEmptySourceMask(byte command)
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleSession session = Session();
        InstallHeader(session, graphics, LateAlly, command);
        NativeBattleGraphicsState prepared = PrepareHeader(session, graphics, Context(LateAlly, BattleModelTargets.Empty) with { FirstTargetBinding = new BattleRetainedModelReference.CanonicalOwner(LateEnemy) });
        Assert.Equal(BattleModelTargets.Empty, prepared.ActionContext!.SourceTargets.Resolve(prepared.ActorModels, prepared.NativeBindings));
        Assert.Equal(Targets(LateEnemy), prepared.ActionContext.TargetActors!.Resolve(prepared.ActorModels, prepared.NativeBindings));
        Assert.False(prepared.ActionContext.IsSelfTarget(prepared.ActorModels, prepared.NativeBindings));

        prepared = PrepareHeader(session, graphics, Context(LateAlly, BattleModelTargets.Empty));
        Assert.Equal(Targets(LateAlly), prepared.ActionContext!.TargetActors!.Resolve(prepared.ActorModels, prepared.NativeBindings));
        Assert.False(prepared.ActionContext.IsSelfTarget(prepared.ActorModels, prepared.NativeBindings));
        AssertInvalid(() => PrepareHeader(session, graphics, Context(LateAlly, BattleModelTargets.Empty) with { FirstTargetBinding = new BattleRetainedModelReference.CanonicalOwner(Missing) }));
    }

    [Fact]
    public void Command240PrefersLoadedPartyModelFourAndOtherwiseUsesLastLoadedPartyRow()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        InstallHeader(session, graphics, EarlyEnemy, 0xF0);
        List<BattleModelOwner> reads = [];
        InstallForeignBones(session, graphics, reads, EarlyAlly, LateAlly, EarlyEnemy);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(EarlyAlly, Model(7, loaded: true) with { ModelId4 = 4 }).Set(LateAlly, Model(14, loaded: true)).Set(EarlyEnemy, Model(15) with { ModelId4 = 4, Flags0 = 2 }) };

        NativeBattleGraphicsState preferred = PrepareHeader(session, graphics, Context(EarlyEnemy, Targets(LateEnemy)));
        Assert.Equal(EarlyAlly, preferred.ActionContext!.ResolveSource(preferred.ActorModels, preferred.NativeBindings));
        Assert.Equal(new[] { EarlyAlly, EarlyAlly }, reads);
        reads.Clear();
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(EarlyAlly, graphics.ActorModels.Get(EarlyAlly) with { ModelId4 = 1 }) };
        NativeBattleGraphicsState fallback = PrepareHeader(session, graphics, Context(EarlyEnemy, Targets(LateEnemy)));
        Assert.Equal(LateAlly, fallback.ActionContext!.ResolveSource(fallback.ActorModels, fallback.NativeBindings));
        Assert.Equal(new[] { LateAlly, LateAlly }, reads);
    }

    [Fact]
    public void TypedGeometryRejectsUnboundSelectionsBeforeReadingBonesOrPublishingMemory()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(EarlyAlly, Model(7, loaded: true)) };
        List<BattleModelOwner> reads = [];
        InstallForeignBones(session, graphics, reads, EarlyAlly);
        NativeBattleEffectMemory before = session.Presentation!.Memory;
        AssertInvalid(() => PrepareTyped(session, graphics, EarlyAlly, Targets(Missing), Targets(LateEnemy)));
        AssertInvalid(() => PrepareTyped(session, graphics, EarlyAlly, Targets(EarlyAlly), Targets(Missing)));
        AssertInvalid(() => PrepareTyped(session, graphics, Missing, Targets(EarlyAlly), Targets(LateEnemy)));
        AssertInvalid(() => PrepareTyped(session, graphics with { ActorModels = graphics.ActorModels.Remove(LateEnemy) }, EarlyAlly, Targets(EarlyAlly), Targets(LateEnemy)));
                Assert.Empty(reads);
        Assert.Same(before, session.Presentation?.Memory);
    }

    [Fact]
    public void TypedGeometryPreservesOpaqueSourceFlagsWithoutCreatingASelfTarget()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        BattleModelTargets actor = Targets(LateAlly);
        NativeBattleActionGeometry self = PrepareTyped(session, graphics, LateAlly, actor, actor).ActionContext!;
        NativeBattleActionGeometry retained = PrepareTyped(session, graphics, LateAlly, actor, actor, true).ActionContext!;

        Assert.True(self.IsSelfTarget(graphics.ActorModels, graphics.NativeBindings));
        Assert.False(retained.IsSelfTarget(graphics.ActorModels, graphics.NativeBindings));
        Assert.True(retained.SourceHasNonActorSelectors);
        Assert.Equal(actor, retained.SourceTargets.Resolve(graphics.ActorModels, graphics.NativeBindings));
        retained.Validate();
    }

    private static BattleSession Session()
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        NativeBattleHostedTestFixture.BindProcess(session);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, NativeBattleEffectMemory.FromColdProcess());
        return session;
    }

    private static NativeBattleGraphicsState Graphics(bool originalOnly = false)
    {
        BattleModelRoster roster = originalOnly
            ? new([OriginalAlly, OriginalEnemy, OtherOriginalEnemy])
            : new([OriginalAlly, EarlyAlly, LateAlly, EarlyEnemy, LateEnemy]);
        NativeBattleModelAliases aliases = originalOnly
            ? new(roster, [(NativeBattleModelSlot.Decode(0), OriginalAlly), (NativeBattleModelSlot.Decode(3), OriginalEnemy), (NativeBattleModelSlot.Decode(6), OtherOriginalEnemy)])
            : new(roster, [(NativeBattleModelSlot.Decode(0), OriginalAlly)]);
        Dictionary<BattleModelOwner, NativeBattleGraphicsModel> models = new()
        {
            [OriginalAlly] = Model(0, busy: !originalOnly),
            [OriginalEnemy] = Model(3),
            [OtherOriginalEnemy] = Model(6, busy: true),
            [EarlyAlly] = Model(7),
            [LateAlly] = Model(14),
            [EarlyEnemy] = Model(15),
            [LateEnemy] = Model(24)
        };
        return NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => models[owner]);
    }

    private static NativeBattleGraphicsModel Model(int row, bool busy = false, bool loaded = false)
    {
        NativeBattleVector position = new(checked((short)(row * 53)), 0, checked((short)(-row * 71)));
        return new(busy || loaded ? (ushort)2 : (ushort)0)
        {
            Flags2 = 0,
            Flags8 = 0,
            Kind5 = 0,
            ResourceFlags44 = busy ? (ushort)0 : (ushort)0x140,
            Rotation12 = 0,
            Rotation14 = 0,
            Rotation16 = 0,
            Transform = new(position, position, 0, 0, 4096)
        };
    }

    private static BattleModelTargets Targets(params BattleModelOwner[] owners) => BattleModelTargets.From(owners);

    private static NativeBattleActionGraphicsState Context(BattleModelOwner source, BattleModelTargets targets)
    {
        return new(new(0), new BattleRetainedModelReference.CanonicalOwner(source), new(0), BattleRetainedModelSelection.From(targets.Enumerate().Select(owner => new BattleRetainedModelReference.CanonicalOwner(owner))), BattleRetainedModelSelection.Empty, null, null, 0, null, 0x50BB00) { UseActionCamera = false };
    }

    private static void InstallHeader(BattleSession session, NativeBattleGraphicsState graphics, BattleModelOwner source, byte command)
    {
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        NativeBattleActionPacketsState storage = NativeBattleActionPacketsState.Create(graphics.ActorModels.Roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (NativeBattleActionPacketsState packets, _) = storage.Prepare(new(source, command, 0, -1, 0, 0, null));
        typeof(BattlePresentationProcess).GetField("nativeActionPackets", PrivateInstance)!.SetValue(session.Presentation!, packets);
    }

    private static void InstallForeignBones(BattleSession session, NativeBattleGraphicsState graphics, List<BattleModelOwner> reads, params BattleModelOwner[] owners)
    {
        Dictionary<BattleModelOwner, NativeBattleForeignActor> actors = [];
        foreach (BattleModelOwner owner in owners)
        {
            NativeBattleModelTransform transform = graphics.ActorModels.Get(owner).Transform!;
            actors.Add(owner, new(owner, transform, new(0, 0, 0, 0), (bone, _) =>
            {
                reads.Add(owner);
                return transform.Position28 with { Y = bone };
            }));
        }
        typeof(BattlePresentationProcess).GetField("presentationForeignActors", PrivateInstance)!.SetValue(session.Presentation!, actors);
    }

    private static bool Busy(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        return (bool)ModelsBusy.Invoke(session.Presentation!, [graphics, context])!;
    }

    private static NativeBattleGraphicsState PrepareHeader(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        return (NativeBattleGraphicsState)HeaderGeometry.Invoke(session.Presentation!, [graphics, context])!;
    }

    private static NativeBattleGraphicsState PrepareTyped(BattleSession session, NativeBattleGraphicsState graphics, BattleModelOwner actor, BattleModelTargets source, BattleModelTargets targets, bool sourceHasNonActorSelectors = false)
    {
        return (NativeBattleGraphicsState)TypedGeometry.Invoke(session.Presentation!, [graphics, actor, source, targets, sourceHasNonActorSelectors])!;
    }

    private static void AssertInvalid(Action act)
    {
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(act).InnerException);
    }
}
