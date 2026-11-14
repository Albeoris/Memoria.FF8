using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorContextTests
{
    [Fact]
    public void NativeCoreAndHeaderJsonKeepTheirOriginalShapeAndKnownness()
    {
        uint start = NativeBattleScriptedEffectMemory.Address;
        NativeBattleScriptedEffectMemory original = NativeBattleScriptedEffectMemory.FromColdProcess().Write(start + 0x44, [5]).MarkUnknown(start + 0x46, 2);
        string json = NativeBattleTestSnapshots.Json(original);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("Core", Assert.Single(document.RootElement.EnumerateObject()).Name);
        NativeBattleScriptedEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleScriptedEffectMemory>(json)!;
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        NativeBattleWaterHeader header = restored.ReadHeader();
        Assert.Null(header.PresentationActors);
        Assert.Equal((byte)5, Assert.Single(header.Read(0x44, 1)));
        Assert.Throws<NotSupportedException>(() => header.Read(0x46, 2));
        using JsonDocument headerDocument = JsonDocument.Parse(NativeBattleTestSnapshots.Json(header));
        Assert.Equal(new[] { "Bytes", "Known" }, headerDocument.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Same(restored, restored.WithHeader(header));
        NativeBattleWaterHeader written = restored.ReadHeader();
        Assert.Equal(header.Bytes.ToArray(), written.Bytes.ToArray());
        Assert.Equal(header.Known.ToArray(), written.Known.ToArray());
    }

    [Fact]
    public void CacheProjectsBoundRowsFromTheFullBankAndRetainsUnknownFacts()
    {
        NativeBattleCinematicActorContext context = Create();
        context.Validate();
        Assert.Equal(25, context.CachedActors.Length);
        Assert.Null(context.CachedActors[6]);
        Assert.Null(context.ActorOrdinals[6]);
        Assert.Throws<InvalidDataException>(() => context.ReadCachedActor(6));
        Assert.Throws<InvalidDataException>(() => context.ReadActorOrdinal(25));
        Assert.Throws<NotSupportedException>(() => context.ReadActorOrdinal(24));
        Assert.Throws<NotSupportedException>(() => context.ReadFirstActor());
        Assert.Throws<NotSupportedException>(() => context.ReadSavedActors());

        NativeBattleCinematicActorCache unknownTransform = context.ReadCachedActor(7);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(7), unknownTransform.ModelAddress);
        Assert.Equal((ushort)3, unknownTransform.Flags0);
        Assert.Null(unknownTransform.Position28);
        Assert.Null(unknownTransform.Vertical34);
        Assert.Null(unknownTransform.Rotation12);
        Assert.Equal((short)-54, unknownTransform.Rotation14);
        Assert.Null(unknownTransform.Rotation16);
        Assert.Null(unknownTransform.Scale148);
        Assert.Null(unknownTransform.Scale154);

        NativeBattleCinematicActorCache known = context.ReadCachedActor(24);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(24), known.ModelAddress);
        Assert.Equal(new NativeBattleVector(10, 20, 30), known.Position28);
        Assert.Equal((short)-100, known.Vertical34);
        Assert.Equal((short)123, known.Rotation12);
        Assert.Null(known.Rotation14);
        Assert.Equal((short)456, known.Rotation16);
        Assert.Equal(new NativeBattleVector(1000, 2000, 3000), known.Scale148);
        Assert.Equal((short)2048, known.Scale154);
    }

    [Fact]
    public void ContactCaptureDeduplicatesWhileAuthoredListsRetainRepeatsAndOlderOrdinals()
    {
        NativeBattleCinematicActorContext context = Create().WithSavedActors([]).WithContactCursor(49);
        NativeBattleCinematicActorContext first = context.CaptureCurrentContacts([24, 7, 24, 0, 7], 0);
        Assert.Equal(new byte[] { 24, 7, 0 }, first.CurrentActors);
        Assert.Equal((byte)24, first.ReadFirstActor());
        Assert.Equal((byte)0, first.ReadActorOrdinal(24));
        Assert.Equal((byte)1, first.ReadActorOrdinal(7));
        Assert.Equal((byte)2, first.ReadActorOrdinal(0));
        Assert.Empty(first.ReadSavedActors());
        Assert.Equal(49, first.ContactCursor);

        NativeBattleCinematicActorContext recaptured = first.CaptureCurrentContacts([7, 7], 24);
        Assert.Equal((byte)0, recaptured.ReadActorOrdinal(7));
        Assert.Equal((byte)2, recaptured.ReadActorOrdinal(0));
        NativeBattleCinematicActorContext repeated = recaptured.WithCurrentActors([24, 24, 7]).WithSavedActors([7, 24, 7]);
        Assert.Equal(new byte[] { 24, 24, 7 }, repeated.CurrentActors);
        Assert.Equal(new byte[] { 7, 24, 7 }, repeated.ReadSavedActors());
        Assert.Equal((byte)24, repeated.WithCurrentActors([]).ReadFirstActor());
        Assert.Equal((byte)0, repeated.CaptureCurrentContacts([], 0).ReadFirstActor());
        Assert.Equal(recaptured.ActorOrdinals, repeated.ActorOrdinals);
    }

    [Fact]
    public void HeaderPublicationAndNativeWriterFreezeRetainTheContextAcrossCoreJson()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(NativeBattleScriptedEffectMemory.Address + 0x44, [2]);
        NativeBattleCinematicActorContext context = Create().CaptureCurrentContacts([24, 7], 0).WithSavedActors([]).WithContactCursor(48);
        NativeBattleWaterHeader header = core.ReadHeader() with { PresentationActors = context };
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        models = models.SetItem(24, models[24] with { Rotation14 = -123 });
        NativeBattleWaterHeader cached = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(header, models);
        NativeBattleCinematicActorContext cachedContext = cached.PresentationActors!;
        Assert.Equal(context.ReadCachedActor(24) with { Rotation14 = -123 }, cachedContext.ReadCachedActor(24));
        Assert.Equal(context.CurrentActors, cachedContext.CurrentActors);
        Assert.Equal(context.FirstActor, cachedContext.FirstActor);
        Assert.Equal(context.SavedActors, cachedContext.SavedActors);
        Assert.Equal(context.ActorOrdinals, cachedContext.ActorOrdinals);
        Assert.Equal(context.ContactCursor, cachedContext.ContactCursor);
        NativeBattleScriptedEffectMemory published = core.WithHeader(cached);
        Assert.Null(core.PresentationActors);
        Assert.Same(cachedContext, published.ReadHeader().PresentationActors);

        NativeBattleScriptedEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleScriptedEffectMemory>(NativeBattleTestSnapshots.Json(published))!;
        restored.Validate();
        NativeBattleWaterHeader restoredHeader = restored.ReadHeader();
        Assert.Equal(cached.Bytes.ToArray(), restoredHeader.Bytes.ToArray());
        Assert.Equal(cached.Known.ToArray(), restoredHeader.Known.ToArray());
        NativeBattleCinematicActorContext restoredContext = Assert.IsType<NativeBattleCinematicActorContext>(restoredHeader.PresentationActors);
        Assert.Empty(restoredContext.ReadSavedActors());
        Assert.Equal((byte)24, restoredContext.ReadFirstActor());
        Assert.Equal((byte)1, restoredContext.ReadActorOrdinal(7));
        Assert.Equal(cachedContext.ReadCachedActor(24), restoredContext.ReadCachedActor(24));
        NativeBattleWaterHeader changed = restoredHeader with { PresentationActors = restoredContext.WithCurrentActors([7, 7]).WithContactCursor(49) };
        NativeBattleCinematicActorContext next = restored.WithHeader(changed).PresentationActors!;
        Assert.Equal(new byte[] { 7, 7 }, next.CurrentActors);
        Assert.Equal((byte)1, next.ReadActorOrdinal(7));
        Assert.Equal(49, next.ContactCursor);
    }

    [Fact]
    public void RefreshUpdatesOnlyCachedFacts()
    {
        NativeBattleCinematicActorContext before = Create().CaptureCurrentContacts([24, 0], 7).WithSavedActors([7]).WithContactCursor(55);
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        NativeBattleGraphicsModel changedModel = models[24] with { Transform = null, Rotation14 = 42 };
        NativeBattleCinematicActorContext after = before.RefreshCache(models.SetItem(24, changedModel));
        Assert.Equal((short)42, after.ReadCachedActor(24).Rotation14);
        Assert.Null(after.ReadCachedActor(24).Position28);
        Assert.NotNull(before.ReadCachedActor(24).Position28);
        Assert.Same(before.Roster, after.Roster);
        Assert.Equal(before.CurrentActors, after.CurrentActors);
        Assert.Equal(before.FirstActor, after.FirstActor);
        Assert.Equal(before.SavedActors, after.SavedActors);
        Assert.Equal(before.ActorOrdinals, after.ActorOrdinals);
        Assert.Equal(before.ContactCursor, after.ContactCursor);
    }

    [Fact]
    public void OwnershipAndRangeViolationsAreRejectedByBothViews()
    {
        NativeBattleCinematicActorContext context = Create();
        NativeBattleCinematicActorCache cached = context.ReadCachedActor(24);
        Assert.Throws<InvalidDataException>(() => (context with { CachedActors = context.CachedActors.SetItem(24, cached with { ModelAddress = NativeBattleGraphicsModelAddress.Encode(7) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { CachedActors = context.CachedActors.SetItem(6, cached) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { ActorOrdinals = context.ActorOrdinals.SetItem(6, 0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { ActorOrdinals = context.ActorOrdinals.SetItem(24, 25) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { CachedActors = [] }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { CurrentActors = default }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { SavedActors = default(ImmutableArray<byte>) }).Validate());
        Assert.Throws<InvalidDataException>(() => context.WithCurrentActors([6]));
        Assert.Throws<InvalidDataException>(() => context.WithSavedActors([255]));
        Assert.Throws<InvalidDataException>(() => context.CaptureCurrentContacts([], 6));
        Assert.Throws<InvalidDataException>(() => context.CaptureCurrentContacts([24], 6));
        NativeBattleCinematicActorContext selected = context.WithCurrentActors([24, 7]);
        Assert.Throws<InvalidDataException>(() => (selected with { FirstActor = 7 }).Validate());
        Assert.Throws<InvalidDataException>(() => (selected with { FirstActor = null }).Validate());
        Assert.Throws<InvalidDataException>(() => context.WithContactCursor(-1));
        Assert.Throws<InvalidDataException>(() => context.WithContactCursor(NativeBattleContactAddress.MaximumRowCount + 1));
        Assert.Equal(NativeBattleContactAddress.MaximumRowCount, context.WithContactCursor(NativeBattleContactAddress.MaximumRowCount).ContactCursor);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicActorContext.FromGraphics(context.Roster, Models(7)));
        Assert.Throws<InvalidDataException>(() => context.RefreshCache(Models().SetItem(24, new(7, 0))));
        NativeBattleCinematicActorContext unboundFirst = context with { FirstActor = 6 };
        Assert.Throws<InvalidDataException>(() => (NativeBattleScriptedEffectMemory.FromColdProcess() with { PresentationActors = unboundFirst }).Validate());
        Assert.Throws<InvalidDataException>(() => (NativeBattleWaterHeader.FromColdProcess() with { PresentationActors = unboundFirst }).Validate());
    }

    [Fact]
    public void RepeatedAuthoredListsAreBoundedByTheByteCount()
    {
        NativeBattleCinematicActorContext context = Create();
        ImmutableArray<byte> maximum = [.. Enumerable.Repeat((byte)24, byte.MaxValue)];
        NativeBattleCinematicActorContext filled = context.WithCurrentActors(maximum).WithSavedActors(maximum);
        filled.Validate();
        Assert.Equal(byte.MaxValue, filled.CurrentActors.Length);
        Assert.Throws<InvalidDataException>(() => context.WithCurrentActors(maximum.Add(24)));
        Assert.Throws<InvalidDataException>(() => context.WithSavedActors(maximum.Add(24)));
    }

    private static NativeBattleCinematicActorContext Create()
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[25];
        rows[0] = new("caster", true);
        rows[7] = new("ally", true);
        rows[24] = new("enemy", false);
        return NativeBattleCinematicActorContext.FromGraphics(new([.. rows]), Models());
    }

    private static ImmutableArray<NativeBattleGraphicsModel> Models(int count = 25)
    {
        return [.. Enumerable.Range(0, count).Select(index => index switch
        {
            7 => new NativeBattleGraphicsModel(index, 3) { Rotation14 = -54 },
            24 => new NativeBattleGraphicsModel(index, 3) { Rotation12 = 123, Rotation16 = 456, Transform = new(default, new(10, 20, 30), -100, 0, 0) { Scale148 = new(1000, 2000, 3000), Scale154 = 2048 } },
            _ => new NativeBattleGraphicsModel(index, 0)
        })];
    }
}
