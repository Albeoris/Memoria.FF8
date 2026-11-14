using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedTextureCacheTests
{
    [Fact]
    public void FullRosterUsesThirtySevenOwnedAliasesIncludingOnePartyForm()
    {
        NativeBattleGraphicsRoster roster = FullRoster();
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.Initialize508260(null).WithPresentationRoster(roster);
        Assert.Equal(37, cache.Rows.Length);
        NativeBattleEffectHeapAllocation body = TimPack(2);
        NativeBattleEffectHeapAllocation linked = TimPack(1);
        foreach (int actor in roster.PartyRows)
        {
            NativeBattleModelTextureCacheLoad primary = cache.LoadPlayer(0, actor, body);
            Assert.Equal(actor, primary.Cache.PresentationActors![primary.ResourceIndex]);
            NativeBattleModelTextureCacheLoad weapon = primary.Cache.LoadLinkedWeapon(0, actor, linked);
            Assert.Equal(actor, weapon.Cache.PresentationActors![weapon.ResourceIndex]);
            cache = weapon.Cache;
        }
        foreach (int actor in roster.EnemyRows)
        {
            NativeBattleModelTextureCacheLoad enemy = cache.LoadEnemy(77, body, actor);
            Assert.NotNull(enemy.Upload);
            Assert.Equal(actor, enemy.Cache.PresentationActors![enemy.ResourceIndex]);
            cache = enemy.Cache;
        }
        NativeBattleModelTextureCacheLoad form = cache.LoadEnemy(77, body, 7);
        Assert.Equal(36, form.ResourceIndex);
        Assert.NotNull(form.Upload);
        cache = form.Cache;
        cache.Validate();
        ImmutableDictionary<int, int> owners = cache.PresentationActors!;
        Assert.Equal(37, owners.Count);
        Assert.Equal(7, owners[36]);
        Assert.Equal((ushort)63, cache.BlockedSlots);
        Assert.Throws<NotSupportedException>(() => cache.LoadEnemy(78, body, 7));
        Assert.Equal(form.ResourceIndex, cache.LoadEnemy(77, body, 7).ResourceIndex);
    }

    [Theory]
    [InlineData(0, 0, 12)]
    [InlineData(1, 2, 13)]
    [InlineData(2, 4, 14)]
    [InlineData(7, 0, 12)]
    [InlineData(24, 0, 12)]
    public void PartyReloadsReuseActorAliasWithItsOriginalOrPrivateMasks(int actor, int slot, int weaponSlot)
    {
        NativeBattleModelTextureCache initial = NativeBattleModelTextureCache.Initialize508260(null).WithPresentationRoster(FullRoster());
        NativeBattleModelTextureCacheLoad body = initial.LoadPlayer(1, actor, TimPack(2));
        NativeBattleModelTextureCacheLoad linked = body.Cache.LoadLinkedWeapon(1, actor, TimPack(1));
        Assert.Equal((ushort)(3 << slot), body.Upload!.AllocatedSlots);
        Assert.Equal((byte?)slot, body.Binding.PaletteRows[0]);
        Assert.Equal((byte?)(slot + 1), body.Binding.PaletteRows[1]);
        Assert.Equal((ushort)(1 << weaponSlot), linked.Upload!.AllocatedSlots);
        Assert.Equal((byte?)weaponSlot, linked.Binding.PaletteRows[0]);
        Assert.Equal(body.ResourceIndex, linked.Cache.LoadPlayer(1, actor, TimPack(2)).ResourceIndex);
        Assert.Equal(linked.ResourceIndex, linked.Cache.LoadLinkedWeapon(1, actor, TimPack(1)).ResourceIndex);
        Assert.Same(linked.Cache, linked.Cache.Release5080D0(body.ResourceIndex));
        Assert.Same(linked.Cache, linked.Cache.Release5080D0(linked.ResourceIndex));
    }

    [Fact]
    public void SameModelOnTwoActorsPublishesSeparateUploadsAndReleasesOnlyItsOwnRow()
    {
        NativeBattleModelTextureCache start = NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(FullRoster());
        NativeBattleModelTextureCacheLoad first = start.LoadEnemy(77, TimPack(1, 31), 11);
        NativeBattleModelTextureCacheLoad second = first.Cache.LoadEnemy(77, TimPack(1, 0x3E0), 12);
        Assert.NotEqual(first.ResourceIndex, second.ResourceIndex);
        Assert.Equal((byte?)6, first.Binding.PaletteRows[0]);
        Assert.Equal((byte?)6, second.Binding.PaletteRows[0]);
        NativeBattleModelTextureLoad firstUpload = Assert.IsType<NativeBattleModelTextureLoad>(first.Upload);
        NativeBattleModelTextureLoad secondUpload = Assert.IsType<NativeBattleModelTextureLoad>(second.Upload);
        Assert.Equal((ushort)64, firstUpload.AllocatedSlots);
        Assert.Equal((ushort)64, secondUpload.AllocatedSlots);
        Assert.Equal((ushort)31, PaletteColor(firstUpload));
        Assert.Equal((ushort)0x3E0, PaletteColor(secondUpload));
        Assert.Equal(start.BlockedSlots, second.Cache.BlockedSlots);
        NativeBattleModelTextureCache cache = second.Cache with { EnemyTextureMotion = second.Cache.EnemyTextureMotion.Add(77, [21]) };
        NativeBattleModelTextureCache released = cache.Release5080D0(first.ResourceIndex);
        ImmutableDictionary<int, int> owners = released.PresentationActors!;
        Assert.DoesNotContain(first.ResourceIndex, owners.Keys);
        Assert.Equal(12, owners[second.ResourceIndex]);
        Assert.Equal(cache.Rows[second.ResourceIndex], released.Rows[second.ResourceIndex]);
        Assert.Equal(cache.Rows[first.ResourceIndex] with { Kind = 0 }, released.Rows[first.ResourceIndex]);
        Assert.True(released.EnemyTextureMotion.ContainsKey(77));
        NativeBattleModelTextureCache last = released.Release5080D0(second.ResourceIndex);
        Assert.Empty(last.PresentationActors!);
        Assert.False(last.EnemyTextureMotion.ContainsKey(77));
        Assert.Equal(start.BlockedSlots, last.BlockedSlots);
    }

    [Fact]
    public void HostedResetRetainsOverprovisionedStorageAndJsonOmitsNativeOwners()
    {
        NativeBattleModelTextureCache native = NativeBattleModelTextureCache.Initialize508260(null);
        string nativeJson = NativeBattleTestSnapshots.Json(native);
        using JsonDocument document = JsonDocument.Parse(nativeJson);
        Assert.False(document.RootElement.TryGetProperty(nameof(native.PresentationRoster), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(native.PresentationActors), out _));
        Assert.Equal(11, NativeBattleTestSnapshots.RestoreJson<NativeBattleModelTextureCache>(nativeJson).Rows.Length);

        NativeBattleModelTextureCache cache = native.WithPresentationRoster(FullRoster()).LoadEnemy(77, TimPack(1), 11).Cache;
        cache = cache with { Rows = cache.Rows.Add(new(0, 42, null, null)) };
        string json = NativeBattleTestSnapshots.Json(cache);
        NativeBattleModelTextureCache restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelTextureCache>(json);
        restored.Validate();
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        NativeBattleModelTextureCache reset = restored.Reset508260().WithPresentationRoster(FullRoster());
        Assert.Equal(38, reset.Rows.Length);
        Assert.Empty(reset.PresentationActors!);
        Assert.Equal((byte?)42, reset.Rows[37].ModelId);
        Assert.Equal(restored.Rows[0] with { Kind = 0 }, reset.Rows[0]);
        Assert.All(reset.Rows, row => Assert.Equal((byte)0, row.Kind));
    }

    [Fact]
    public void AttachingRosterToOccupiedNativeRowsRequiresFactualOwners()
    {
        NativeBattleGraphicsRoster roster = FullRoster();
        NativeBattleModelTextureCache native = NativeBattleModelTextureCache.Initialize508260(null).LoadPlayer(2, 0, TimPack(2)).Cache;
        ImmutableDictionary<int, int> owners = ImmutableDictionary<int, int>.Empty.Add(0, 7);
        Assert.Throws<InvalidDataException>(() => native.WithPresentationRoster(roster));
        Assert.Throws<InvalidDataException>(() => native.WithPresentationRoster(roster, owners.SetItem(0, 11)));
        Assert.Throws<InvalidDataException>(() => native.WithPresentationRoster(roster, owners.Remove(0).Add(1, 7)));
        Assert.Throws<InvalidDataException>(() => native.WithPresentationRoster(roster, owners.Remove(0).Add(51, 7)));
        NativeBattleModelTextureCache hosted = native.WithPresentationRoster(roster, owners);
        Assert.Equal(native.Rows, hosted.Rows.Take(11));
        Assert.Equal(7, hosted.PresentationActors![0]);
        Assert.Throws<InvalidDataException>(() => hosted.WithPresentationRoster(roster, owners.SetItem(0, 0)));
        Assert.Throws<InvalidDataException>(() => hosted.WithPresentationRoster(new(roster.Rows.SetItem(7, new("replacement", true)))));
    }

    [Fact]
    public void LoadsRejectWrongSidesHolesAndAbsentHostedOwners()
    {
        NativeBattleGraphicsRoster roster = FullRoster();
        roster = new(roster.Rows.SetItem(16, null));
        NativeBattleEffectHeapAllocation pack = TimPack(1);
        NativeBattleModelTextureCache native = NativeBattleModelTextureCache.Initialize508260(null);
        NativeBattleModelTextureCache cache = native.WithPresentationRoster(roster);
        Assert.Throws<InvalidDataException>(() => cache.LoadPlayer(1, 11, pack));
        Assert.Throws<InvalidDataException>(() => cache.LoadLinkedWeapon(1, 16, pack));
        Assert.Throws<InvalidDataException>(() => cache.LoadEnemy(77, pack));
        Assert.Throws<InvalidDataException>(() => cache.LoadEnemy(77, pack, 16));
        Assert.Throws<InvalidDataException>(() => cache.LoadEnemy(77, pack, 25));
        Assert.Throws<InvalidDataException>(() => native.LoadEnemy(77, pack, 11));
        Assert.Throws<InvalidDataException>((cache with { PresentationActors = null }).Validate);
    }

    private static NativeBattleGraphicsRoster FullRoster()
    {
        return new(Enumerable.Range(0, 25).Select(row => (NativeBattleGraphicsActor?)new NativeBattleGraphicsActor($"actor-{row}", row < 10 || row == 24)).ToImmutableArray());
    }

    private static ushort PaletteColor(NativeBattleModelTextureLoad upload)
    {
        NativeBattleEffectTextureDrain drain = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), upload).Drain505D20();
        return drain.State.ReadVramWord(1, 230);
    }

    private static NativeBattleEffectHeapAllocation TimPack(int count, ushort color = 31)
    {
        int directoryBytes = 4 + count * 4;
        byte[] bytes = new byte[directoryBytes + count * 66];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        for (int index = 0; index < count; index++)
        {
            int offset = directoryBytes + index * 66;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + 4 * index), offset);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), 16);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 8), 44);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 14), (short)(224 + index));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 16), 16);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 18), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 22), color);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 52), 14);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 60), 1);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 62), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 64), 0x1111);
        }
        return new("model", bytes.ToImmutableArray());
    }
}
