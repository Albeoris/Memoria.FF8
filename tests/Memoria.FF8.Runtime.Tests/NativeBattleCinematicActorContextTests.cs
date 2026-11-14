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

    private static readonly BattlePartyActorModel Caster = new(new("caster"));
    private static readonly BattlePartyActorModel Ally = new(new("ally"));
    private static readonly BattleEnemyActorModel Enemy = new(new("enemy"));
    private static readonly BattleEnemyActorModel Unbound = new(new("unbound"));

    [Fact]
    public void RetainedHeaderJsonOmitsTypedOwnersAndPreservesRawBytesAndKnownness()
    {
        uint start = NativeBattleScriptedEffectMemory.Address;
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(start + 0x44, [5]).MarkUnknown(start + 0x46, 2);
        BattleActorModels models = Models().Add(Unbound, new(0));
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models).CaptureCurrentContacts([Unbound, Ally, Enemy, Caster], Caster);
        NativeBattleWaterHeader header = core.ReadHeader() with { PresentationActors = context };
        NativeBattleScriptedEffectMemory published = core.WithHeader(header);

        NativeBattleScriptedEffectMemory copied = NativeBattleTestSnapshots.Independent(published);
        NativeBattleCinematicActorContext copiedContext = Assert.IsType<NativeBattleCinematicActorContext>(copied.PresentationActors);
        Assert.Equal<BattleModelOwner>([Unbound, Ally, Enemy, Caster], copiedContext.CurrentActors);
        Assert.Equal(3, copiedContext.ReadActorOrdinal(Caster));
        Assert.Same(context, published.ReadHeader().PresentationActors);

        string coreJson = NativeBattleTestSnapshots.Json(published);
        string headerJson = NativeBattleTestSnapshots.Json(header);
        using JsonDocument coreDocument = JsonDocument.Parse(coreJson);
        using JsonDocument headerDocument = JsonDocument.Parse(headerJson);
        Assert.False(coreDocument.RootElement.TryGetProperty("PresentationActors", out _));
        Assert.False(headerDocument.RootElement.TryGetProperty("PresentationActors", out _));
        NativeBattleScriptedEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleScriptedEffectMemory>(WithLegacyContext(coreJson));
        NativeBattleWaterHeader restoredHeader = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterHeader>(WithLegacyContext(headerJson));
        Assert.Null(restored.PresentationActors);
        Assert.Null(restoredHeader.PresentationActors);
        foreach (NativeBattleWaterHeader result in new[] { restored.ReadHeader(), restoredHeader })
        {
            Assert.Equal(header.Bytes.ToArray(), result.Bytes.ToArray());
            Assert.Equal(header.Known.ToArray(), result.Known.ToArray());
            Assert.Equal((byte)5, Assert.Single(result.Read(0x44, 1)));
            Assert.Throws<NotSupportedException>(() => result.Read(0x46, 2));
        }

        static string WithLegacyContext(string json) => json.Insert(1, "\"PresentationActors\":{\"ContactCursor\":4},");
    }

    [Fact]
    public void DetachedTaskJsonOmitsInvocationParametersAndRetainsItsPool()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24CA828, 0x24CA818, 16, 1);
        (pool, int slot) = pool.Append(0x62A610);
        pool.Slots[slot].WriteWord(12, 0x1234);
        NativeBattleEffectParameters parameters = new(Caster, 1, BattleModelTargets.From([Enemy]), new(0), 2, 0);
        NativeBattleOriginalEffectState task = new(2, 1, new(2, 0x6298A0, 0x629880), parameters, pool.DescriptorAddress) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().AssignReturnedTask(task).RetainReturnedTaskOnModuleLoad(task);
        Assert.Equal(parameters, NativeBattleStorageSnapshots.Capture(memory).DetachedReturnedTask!.Parameters);

        string json = NativeBattleTestSnapshots.Json(memory);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("DetachedReturnedTask").TryGetProperty("Parameters", out _));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(json);
        restored.ValidateReturnedTaskOwner(null);
        NativeBattleOriginalEffectState retained = Assert.IsType<NativeBattleOriginalEffectState>(restored.DetachedReturnedTask);
        Assert.Null(retained.Parameters);
        Assert.Equal(task.Binding, retained.Binding);
        Assert.Equal(task.ReturnedDescriptor, retained.ReturnedDescriptor);
        NativeBattleEffectTaskPool retainedPool = Assert.Single(retained.Pools);
        Assert.Equal(pool.BankAddress, retainedPool.BankAddress);
        Assert.Equal(0x62A610u, retainedPool.Slots[slot].Callback);
        Assert.Equal((ushort)0x1234, retainedPool.Slots[slot].ReadWord(12));
    }

    [Fact]
    public void CacheFollowsExplicitModelOrderAndRetainsUnknownFacts()
    {
        NativeBattleCinematicActorContext context = Create();
        context.Validate();
        Assert.Equal<BattleModelOwner>([Caster, Enemy, Ally], context.Owners);
        Assert.Equal(3, context.CachedActors.Count);
        Assert.Null(context.ActorOrdinals[Ally]);
        Assert.Throws<InvalidDataException>(() => context.ReadCachedActor(Unbound));
        Assert.Throws<InvalidDataException>(() => context.ReadActorOrdinal(Unbound));
        Assert.Throws<NotSupportedException>(() => context.ReadActorOrdinal(Enemy));
        Assert.Throws<NotSupportedException>(() => context.ReadFirstActor());
        Assert.Throws<NotSupportedException>(() => context.ReadSavedActors());

        NativeBattleCinematicActorCache unknownTransform = context.ReadCachedActor(Ally);
        Assert.Equal((ushort)3, unknownTransform.Flags0);
        Assert.Null(unknownTransform.Position28);
        Assert.Null(unknownTransform.Vertical34);
        Assert.Null(unknownTransform.Rotation12);
        Assert.Equal((short)-54, unknownTransform.Rotation14);
        Assert.Null(unknownTransform.Rotation16);
        Assert.Null(unknownTransform.Scale148);
        Assert.Null(unknownTransform.Scale154);

        NativeBattleCinematicActorCache known = context.ReadCachedActor(Enemy);
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
        NativeBattleCinematicActorContext context = Create().WithSavedActors([]).WithContactsProcessed(49);
        NativeBattleCinematicActorContext first = context.CaptureCurrentContacts([Enemy, Ally, Enemy, Caster, Ally], Caster);
        Assert.Equal<BattleModelOwner>([Enemy, Ally, Caster], first.CurrentActors);
        Assert.Equal(Enemy, first.ReadFirstActor());
        Assert.Equal(0, first.ReadActorOrdinal(Enemy));
        Assert.Equal(1, first.ReadActorOrdinal(Ally));
        Assert.Equal(2, first.ReadActorOrdinal(Caster));
        Assert.Empty(first.ReadSavedActors());
        Assert.Equal(49, first.ContactsProcessed);

        NativeBattleCinematicActorContext recaptured = first.CaptureCurrentContacts([Ally, Ally], Enemy);
        Assert.Equal(0, recaptured.ReadActorOrdinal(Ally));
        Assert.Equal(2, recaptured.ReadActorOrdinal(Caster));
        NativeBattleCinematicActorContext repeated = recaptured.WithCurrentActors([Enemy, Enemy, Ally]).WithSavedActors([Ally, Enemy, Ally]);
        Assert.Equal<BattleModelOwner>([Enemy, Enemy, Ally], repeated.CurrentActors);
        Assert.Equal<BattleModelOwner>([Ally, Enemy, Ally], repeated.ReadSavedActors());
        Assert.Equal(Enemy, repeated.WithCurrentActors([]).ReadFirstActor());
        Assert.Equal(Caster, repeated.CaptureCurrentContacts([], Caster).ReadFirstActor());
        Assert.Same(recaptured.ActorOrdinals, repeated.ActorOrdinals);
        repeated.Validate();
    }

    [Fact]
    public void HeaderPublicationRetainsContextAndNativeBytesInMemory()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(NativeBattleScriptedEffectMemory.Address + 0x44, [2]);
        NativeBattleCinematicActorContext context = Create().CaptureCurrentContacts([Enemy, Ally], Caster).WithSavedActors([]).WithContactsProcessed(48);
        BattleActorModels models = Models();
        NativeBattleCinematicActorContext cached = context.RefreshCache(models.Set(Enemy, models.Get(Enemy) with { Rotation14 = -123 }));
        NativeBattleWaterHeader header = core.ReadHeader() with { PresentationActors = cached };
        NativeBattleScriptedEffectMemory published = core.WithHeader(header);
        Assert.Null(core.PresentationActors);
        published.Validate();
        NativeBattleWaterHeader readHeader = published.ReadHeader();
        Assert.Equal(header.Bytes.ToArray(), readHeader.Bytes.ToArray());
        Assert.Equal(header.Known.ToArray(), readHeader.Known.ToArray());
        Assert.Same(cached, readHeader.PresentationActors);
        Assert.Equal(context.ReadCachedActor(Enemy) with { Rotation14 = -123 }, cached.ReadCachedActor(Enemy));
        Assert.Empty(cached.ReadSavedActors());
        Assert.Equal(Enemy, cached.ReadFirstActor());
        Assert.Equal(1, cached.ReadActorOrdinal(Ally));

        NativeBattleWaterHeader changed = readHeader with { PresentationActors = cached.WithCurrentActors([Ally, Ally]).WithContactsProcessed(49) };
        NativeBattleCinematicActorContext next = published.WithHeader(changed).PresentationActors!;
        Assert.Equal<BattleModelOwner>([Ally, Ally], next.CurrentActors);
        Assert.Equal(1, next.ReadActorOrdinal(Ally));
        Assert.Equal(49, next.ContactsProcessed);
        Assert.Equal(48, published.PresentationActors!.ContactsProcessed);
    }

    [Fact]
    public void RefreshFlagsAndRefreshCachePreserveSelectionAndUpdateOnlyTheirOwnedFacts()
    {
        NativeBattleCinematicActorContext before = Create().CaptureCurrentContacts([Enemy, Caster], Ally).WithSavedActors([Ally]).WithContactsProcessed(55);
        BattleActorModels models = Models();
        NativeBattleGraphicsModel changedModel = models.Get(Enemy) with { Flags0 = 5, Transform = null, Rotation14 = 42 };
        BattleActorModels updated = models.Set(Enemy, changedModel);
        NativeBattleCinematicActorContext flagsOnly = before.RefreshFlags(updated);
        Assert.Equal(before.ReadCachedActor(Enemy) with { Flags0 = 5 }, flagsOnly.ReadCachedActor(Enemy));
        Assert.Same(before.ReadCachedActor(Caster), flagsOnly.ReadCachedActor(Caster));

        NativeBattleCinematicActorContext after = before.RefreshCache(updated);
        Assert.Equal((ushort)5, after.ReadCachedActor(Enemy).Flags0);
        Assert.Equal((short)42, after.ReadCachedActor(Enemy).Rotation14);
        Assert.Null(after.ReadCachedActor(Enemy).Position28);
        Assert.NotNull(before.ReadCachedActor(Enemy).Position28);
        foreach (NativeBattleCinematicActorContext refreshed in new[] { flagsOnly, after })
        {
            Assert.Equal(before.Owners, refreshed.Owners);
            Assert.Equal(before.CurrentActors, refreshed.CurrentActors);
            Assert.Equal(before.FirstActor, refreshed.FirstActor);
            Assert.Equal(before.SavedActors, refreshed.SavedActors);
            Assert.Same(before.ActorOrdinals, refreshed.ActorOrdinals);
            Assert.Equal(before.ContactsProcessed, refreshed.ContactsProcessed);
        }
    }

    [Fact]
    public void CacheRefreshRejectsReplacedAllocationGenerationsAndReorderedOwners()
    {
        NativeBattleCinematicActorContext context = Create();
        BattleActorModels models = Models();
        BattleEnemyActorModel replacement = new(new("enemy-next-allocation"));
        BattleActorModels rebound = models.ReplaceOwner(Enemy, replacement, models.Get(Enemy));
        Assert.Throws<InvalidDataException>(() => context.RefreshFlags(rebound));
        Assert.Throws<InvalidDataException>(() => context.RefreshCache(rebound));
        BattleActorModels reordered = new(models.Entries.Reverse());
        Assert.Throws<InvalidDataException>(() => context.RefreshFlags(reordered));
        Assert.Throws<InvalidDataException>(() => context.RefreshCache(reordered));
    }

    [Fact]
    public void OwnershipAndRangeViolationsAreRejectedByBothViews()
    {
        NativeBattleCinematicActorContext context = Create();
        NativeBattleCinematicActorCache cached = context.ReadCachedActor(Enemy);
        Assert.Throws<InvalidDataException>(() => (context with { CachedActors = context.CachedActors.Remove(Enemy).Add(Unbound, cached) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { CachedActors = context.CachedActors.Add(Unbound, cached) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { ActorOrdinals = context.ActorOrdinals.Remove(Enemy).Add(Unbound, 0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { ActorOrdinals = context.ActorOrdinals.SetItem(Enemy, 3) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { ActorOrdinals = context.ActorOrdinals.SetItem(Enemy, -1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { CachedActors = context.CachedActors.Clear() }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { CurrentActors = default }).Validate());
        Assert.Throws<InvalidDataException>(() => (context with { SavedActors = default(ImmutableArray<BattleModelOwner>) }).Validate());
        Assert.Throws<InvalidDataException>(() => context.WithCurrentActors([Unbound]));
        Assert.Throws<InvalidDataException>(() => context.WithSavedActors([Unbound]));
        Assert.Throws<InvalidDataException>(() => context.CaptureCurrentContacts([], Unbound));
        Assert.Throws<InvalidDataException>(() => context.CaptureCurrentContacts([Enemy], Unbound));
        Assert.Throws<InvalidDataException>(() => context.CaptureCurrentContacts([Unbound], Enemy));
        NativeBattleCinematicActorContext selected = context.WithCurrentActors([Enemy, Ally]);
        Assert.Throws<InvalidDataException>(() => (selected with { FirstActor = Ally }).Validate());
        Assert.Throws<InvalidDataException>(() => (selected with { FirstActor = null }).Validate());
        Assert.Throws<InvalidDataException>(() => context.WithContactsProcessed(-1));
        Assert.Throws<InvalidDataException>(() => (context with { ContactsProcessed = -1 }).Validate());
        NativeBattleCinematicActorContext progressed = context.WithContactsProcessed(int.MaxValue);
        progressed.Validate();
        Assert.Equal(int.MaxValue, progressed.ContactsProcessed);
        NativeBattleCinematicActorContext unboundFirst = context with { FirstActor = Unbound };
        Assert.Throws<InvalidDataException>(() => (NativeBattleScriptedEffectMemory.FromColdProcess() with { PresentationActors = unboundFirst }).Validate());
        Assert.Throws<InvalidDataException>(() => (NativeBattleWaterHeader.FromColdProcess() with { PresentationActors = unboundFirst }).Validate());
    }

    [Fact]
    public void ActorOrdinalsAndRepeatedListsCanExceedTheAuthoredByteCount()
    {
        ImmutableArray<BattleModelOwner> owners = [.. Enumerable.Range(0, 300).Select(index => new BattleEnemyActorModel(new($"enemy-{index}")))];
        var models = new BattleActorModels(owners.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new(0))));
        NativeBattleCinematicActorContext captured = NativeBattleCinematicActorContext.FromGraphics(models).CaptureCurrentContacts(owners, owners[0]);
        Assert.Equal(299, captured.ReadActorOrdinal(owners[^1]));

        ImmutableArray<BattleModelOwner> repeats = [.. Enumerable.Repeat(owners[^1], 302)];
        NativeBattleCinematicActorContext filled = captured.WithCurrentActors(repeats).WithSavedActors(repeats);
        filled.Validate();
        Assert.Equal(302, filled.CurrentActors.Length);
        Assert.Equal(302, filled.ReadSavedActors().Length);
        Assert.Equal(299, filled.ReadActorOrdinal(owners[^1]));
    }

    private static NativeBattleCinematicActorContext Create() => NativeBattleCinematicActorContext.FromGraphics(Models());

    private static BattleActorModels Models()
    {
        return new([
            new(Caster, new(0)),
            new(Enemy, new(3) { Rotation12 = 123, Rotation16 = 456, Transform = new(default, new(10, 20, 30), -100, 0, 0) { Scale148 = new(1000, 2000, 3000), Scale154 = 2048 } }),
            new(Ally, new(3) { Rotation14 = -54 })
        ]);
    }
}
