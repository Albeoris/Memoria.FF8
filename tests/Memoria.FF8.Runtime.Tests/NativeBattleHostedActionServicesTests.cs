using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionServicesTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo ModelsBusy = typeof(BattleSession).GetMethod("NativeActionModelsBusy", PrivateInstance)!;
    private static readonly MethodInfo HeaderGeometry = typeof(BattleSession).GetMethod("PrepareNativeHeaderGeometry", PrivateInstance)!;
    private static readonly MethodInfo TypedGeometry = typeof(BattleSession).GetMethod("PrepareNativeActionGeometry", PrivateInstance, null,
        [typeof(NativeBattleGraphicsState), typeof(int), typeof(NativeBattleGraphicsActorSet), typeof(NativeBattleGraphicsActorSet), typeof(ushort)], null)!;

    [Fact]
    public void HostedBusyDistinguishesSelectedTargetsFromAllLoadedBoundModels()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        // A hole may contain unknown fields because this service must never query its readiness.
        graphics = graphics with { Models = graphics.Models.SetItem(1, new(1, 2)).SetItem(14, Model(14, busy: true)) };
        NativeBattleActionGraphicsState context = Context(7, Rows(15));
        Assert.False(Busy(session, graphics, context));
        Assert.True(Busy(session, graphics, context with { UseActionCamera = true }));
        Assert.True(Busy(session, graphics, Context(7, Rows(24)) with { CoverMask = 1 }));
        Assert.True(Busy(session, graphics with { Models = graphics.Models.SetItem(24, Model(24, busy: true)) }, Context(7, Rows(24))));
    }

    [Fact]
    public void BoundSourceKeepsTheFirstReadAndMalformedBindingsFailBeforeModelReads()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        graphics = graphics with { Models = graphics.Models.SetItem(7, Model(7, busy: true)).SetItem(15, new(15, 2)).SetItem(24, new(24, 2)) };
        NativeBattleActionGraphicsState context = Context(7, Rows(15, 24));
        Assert.True(Busy(session, graphics, context));
        AssertInvalid(() => Busy(session, graphics, Context(7, Rows(1))));
        AssertInvalid(() => Busy(session, graphics, context with { ActorIndex = 1 }));
        AssertInvalid(() => Busy(session, graphics, context with { CoverMask = 2 }));
        AssertInvalid(() => Busy(session, graphics with { Models = graphics.Models[..24] }, context));
        AssertInvalid(() => Busy(session, graphics with { Models = graphics.Models.SetItem(24, Model(23)) }, context));
        AssertInvalid(() => Busy(session, graphics with { ActorIds = graphics.ActorIds.SetItem(24, "old-actor") }, context));
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Busy(session, graphics with { Models = graphics.Models.SetItem(7, Model(7)) }, context)).InnerException);
    }

    [Fact]
    public void NativeAndHostedSevenRowBusyChecksAgree()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState hosted = Graphics(7);
        NativeBattleGraphicsState native = hosted with { PresentationRoster = null };
        NativeBattleActionGraphicsState context = Context(0, Rows(3));
        foreach (bool camera in new[] { false, true })
        {
            NativeBattleActionGraphicsState selected = context with { UseActionCamera = camera };
            Assert.Equal(Busy(session, native, selected with { PresentationTargets = null }), Busy(session, hosted, selected));
        }
        Assert.False(Busy(session, native, context with { PresentationTargets = null }));
        Assert.True(Busy(session, native, context with { PresentationTargets = null, UseActionCamera = true }));
    }

    [Fact]
    public void OrdinarySevenRowHeaderGeometryMatchesNativeAtTheSessionBoundary()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState hosted = Graphics(7);
        NativeBattleGraphicsState native = hosted with { PresentationRoster = null, ActorIds = hosted.ActorIds.SetItem(0, "ally").SetItem(3, "enemy") };
        NativeBattleActionGraphicsState context = Context(0, Rows(3)) with { FirstTarget = 3 };
        InstallHeader(session, null, 0, 1);
        NativeBattleGraphicsState expected = PrepareHeader(session, native, context with { PresentationTargets = null });
        InstallHeader(session, hosted.PresentationRoster, 0, 1);
        NativeBattleGraphicsState actual = PrepareHeader(session, hosted, context);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.ActionContext), NativeBattleTestSnapshots.Json(actual.ActionContext! with { PresentationTargets = null, PresentationSourceTargets = null }));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Models), NativeBattleTestSnapshots.Json(actual.Models));
    }

    [Fact]
    public void HostedHeaderGeometryUsesExtendedRowsAndPublishesItsAliases()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleSession session = Session();
        InstallHeader(session, graphics.PresentationRoster!, 7, 1);
        NativeBattleGraphicsActorSet targets = Rows(15, 24);
        NativeBattleGraphicsState prepared = PrepareHeader(session, graphics, Context(7, targets));
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        NativeBattleCameraAliases aliases = memory.CameraAliases!;
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals!;

        Assert.Equal((byte)7, prepared.ActionContext!.ActorIndex);
        Assert.Equal(targets, prepared.ActionContext.PresentationSourceTargets);
        Assert.Equal(targets, prepared.ActionContext.TargetActors);
        Assert.Equal((byte)24, prepared.ActionContext.TargetIndex);
        Assert.Equal(5, prepared.ActionContext.Matrices!.Value.Length);
        Assert.Equal((byte)7, aliases.Resolve50D790(0x90));
        Assert.Equal((byte)24, aliases.Resolve50D790(0xA0));
        Assert.Equal(prepared.ActionContext.ContactDistance, globals.ContactDistance);
        Assert.Equal(prepared.ActionContext.TargetYaw, globals.TargetYaw);

        List<int> reads = [];
        InstallForeignBones(session, graphics, reads, 0);
        NativeBattleGraphicsState cover = PrepareHeader(session, graphics, Context(7, targets) with { CoverMask = 1 });
        Assert.Equal(Rows(0), cover.ActionContext!.PresentationSourceTargets);
        Assert.Equal(Rows(0), cover.ActionContext.TargetActors);
        Assert.Equal(new[] { 0, 0 }, reads);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(28)]
    public void HostedOrdinaryEmptyTargetFallbackRetainsAnEmptySourceMask(byte command)
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleSession session = Session();
        InstallHeader(session, graphics.PresentationRoster!, 14, command);
        NativeBattleGraphicsState prepared = PrepareHeader(session, graphics, Context(14, default) with { FirstTarget = 24 });
        Assert.Equal(default(NativeBattleGraphicsActorSet), prepared.ActionContext!.PresentationSourceTargets);
        Assert.Equal(Rows(24), prepared.ActionContext.TargetActors);
        Assert.False(prepared.ActionContext.IsSelfTarget);

        prepared = PrepareHeader(session, graphics, Context(14, default));
        Assert.Equal(Rows(14), prepared.ActionContext!.TargetActors);
        Assert.False(prepared.ActionContext.IsSelfTarget);
        AssertInvalid(() => PrepareHeader(session, graphics, Context(14, default) with { FirstTarget = 23 }));
    }

    [Fact]
    public void Command240PrefersLoadedPartyModelFourAndOtherwiseUsesLastLoadedPartyRow()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        InstallHeader(session, graphics.PresentationRoster!, 15, 0xF0);
        List<int> reads = [];
        InstallForeignBones(session, graphics, reads, 7, 14, 15);
        graphics = graphics with { Models = graphics.Models.SetItem(7, Model(7, loaded: true) with { ModelId4 = 4 }).SetItem(14, Model(14, loaded: true)).SetItem(15, Model(15) with { ModelId4 = 4, Flags0 = 2 }) };

        NativeBattleGraphicsState preferred = PrepareHeader(session, graphics, Context(15, Rows(24)));
        Assert.Equal((byte)7, preferred.ActionContext!.ActorIndex);
        Assert.Equal(new[] { 7, 7 }, reads);
        reads.Clear();
        graphics = graphics with { Models = graphics.Models.SetItem(7, graphics.Models[7] with { ModelId4 = 1 }) };
        NativeBattleGraphicsState fallback = PrepareHeader(session, graphics, Context(15, Rows(24)));
        Assert.Equal((byte)14, fallback.ActionContext!.ActorIndex);
        Assert.Equal(new[] { 14, 14 }, reads);
    }

    [Fact]
    public void TypedGeometryRejectsUnboundSelectionsBeforeReadingBonesOrPublishingMemory()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        graphics = graphics with { Models = graphics.Models.SetItem(7, Model(7, loaded: true)) };
        List<int> reads = [];
        InstallForeignBones(session, graphics, reads, 7);
        NativeBattleEffectMemory before = session.NativeEffectMemory!;
        AssertInvalid(() => PrepareTyped(session, graphics, 7, Rows(1), Rows(24)));
        AssertInvalid(() => PrepareTyped(session, graphics, 7, Rows(7), Rows(23)));
        AssertInvalid(() => PrepareTyped(session, graphics, 23, Rows(7), Rows(24)));
        AssertInvalid(() => PrepareTyped(session, graphics with { Models = graphics.Models[..24] }, 7, Rows(7), Rows(24)));
        AssertInvalid(() => PrepareTyped(session, graphics, 7, Rows(7), Rows(24), 1));
        Assert.Empty(reads);
        Assert.Same(before, session.NativeEffectMemory);
    }

    [Fact]
    public void TypedGeometryPreservesOpaqueSourceFlagsWithoutCreatingASelfTarget()
    {
        BattleSession session = Session();
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleGraphicsActorSet actor = Rows(14);
        NativeBattleActionGeometry self = PrepareTyped(session, graphics, 14, actor, actor).ActionContext!;
        NativeBattleActionGeometry retained = PrepareTyped(session, graphics, 14, actor, actor, 0x4000).ActionContext!;

        Assert.True(self.IsSelfTarget);
        Assert.False(retained.IsSelfTarget);
        Assert.Equal((ushort)0x4000, retained.SourceTargetMask);
        Assert.Equal((ushort)0, retained.PhysicalTargetMask);
        Assert.Equal(actor, retained.PresentationSourceTargets);
        retained.Validate();
        Assert.Throws<InvalidDataException>((retained with { SourceTargetMask = 0x4001 }).Validate);
    }

    private static BattleSession Session()
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, NativeBattleEffectMemory.FromColdProcess());
        return session;
    }

    private static NativeBattleGraphicsState Graphics(int count = 25)
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[count];
        foreach (int row in count == 7 ? new[] { 0, 3, 6 } : new[] { 0, 7, 14, 15, 24 })
        {
            rows[row] = new($"presentation-{row}", count == 7 ? row < 3 : row is 7 or 14);
        }
        NativeBattleGraphicsRoster roster = new([.. rows]);
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, count).Select(row => Model(row, busy: row == 0 && count != 7 || row == 6 && count == 7)).ToImmutableArray();
        return new(models, rows.Select(row => row?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
    }

    private static NativeBattleGraphicsModel Model(int row, bool busy = false, bool loaded = false)
    {
        NativeBattleVector position = new(checked((short)(row * 53)), 0, checked((short)(-row * 71)));
        return new(row, busy || loaded ? (ushort)2 : (ushort)0)
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

    private static NativeBattleGraphicsActorSet Rows(params int[] rows)
    {
        NativeBattleGraphicsActorSet set = default;
        foreach (int row in rows)
        {
            set = set.Add(row);
        }
        return set;
    }

    private static NativeBattleActionGraphicsState Context(byte source, NativeBattleGraphicsActorSet targets)
    {
        return new(0, source, 0, targets.NativePhysicalBits, 0, null, null, 0, null, 0x50BB00) { PresentationTargets = targets, UseActionCamera = false };
    }

    private static void InstallHeader(BattleSession session, NativeBattleGraphicsRoster? roster, byte source, byte command)
    {
        NativeBattleActionPacketsState storage = roster is null ? NativeBattleActionPacketsState.Fresh() : NativeBattleActionPacketsState.FromPresentationStorage(roster);
        (NativeBattleActionPacketsState packets, _) = storage.Prepare(new(source, command, 0, -1, 0, 0, null));
        typeof(BattleSession).GetField("nativeActionPackets", PrivateInstance)!.SetValue(session, packets);
    }

    private static void InstallForeignBones(BattleSession session, NativeBattleGraphicsState graphics, List<int> reads, params int[] rows)
    {
        Dictionary<int, NativeBattleForeignActor> actors = [];
        foreach (int row in rows)
        {
            NativeBattleModelTransform transform = graphics.Models[row].Transform!;
            actors.Add(row, new(row, transform, new(0, 0, 0, 0), (bone, _) =>
            {
                reads.Add(row);
                return transform.Position28 with { Y = bone };
            }));
        }
        typeof(BattleSession).GetField("presentationForeignActors", PrivateInstance)!.SetValue(session, actors);
    }

    private static bool Busy(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        return (bool)ModelsBusy.Invoke(session, [graphics, context])!;
    }

    private static NativeBattleGraphicsState PrepareHeader(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        return (NativeBattleGraphicsState)HeaderGeometry.Invoke(session, [graphics, context])!;
    }

    private static NativeBattleGraphicsState PrepareTyped(BattleSession session, NativeBattleGraphicsState graphics, int actor, NativeBattleGraphicsActorSet source, NativeBattleGraphicsActorSet targets, ushort sourceMaskFlags = 0)
    {
        return (NativeBattleGraphicsState)TypedGeometry.Invoke(session, [graphics, actor, source, targets, sourceMaskFlags])!;
    }

    private static void AssertInvalid(Action act)
    {
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(act).InnerException);
    }
}
