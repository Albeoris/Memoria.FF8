using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEnemyTextureCacheTests(ITestOutputHelper output)
{
    [Fact]
    public void InstalledIfritTexturePackUsesFirstUnblockedSlotsAfterTheSixReservedPartySlots()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(61);
        NativeBattleEffectHeapAllocation pack = Assert.IsType<NativeBattleEffectHeapAllocation>(resource.BodyTexturePack);
        byte[] original = pack.Data.ToArray();
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(pack, 63);
        ImmutableArray<byte?> expectedRows = ImmutableArray.CreateRange(new byte?[16]);
        int nextSlot = 6;
        ushort expectedMask = 0;
        foreach (int offset in load.TimOffsets)
        {
            short clutX = BinaryPrimitives.ReadInt16LittleEndian(original.AsSpan(offset + 12));
            short clutY = BinaryPrimitives.ReadInt16LittleEndian(original.AsSpan(offset + 14));
            if (clutX >= 256 || clutY is < 224 or >= 240)
            {
                continue;
            }
            Assert.InRange(nextSlot, 6, 14);
            expectedRows = expectedRows.SetItem(clutY - 224, (byte)nextSlot);
            expectedMask |= (ushort)(1 << nextSlot);
            Assert.Equal(nextSlot + 224, BinaryPrimitives.ReadInt16LittleEndian(load.Source.Data.AsSpan().Slice(offset + 14)));
            nextSlot++;
        }
        Assert.True(nextSlot > 6);
        Assert.Equal(expectedMask, load.AllocatedSlots);
        Assert.Equal(expectedRows.ToArray(), load.Binding.PaletteRows.ToArray());
        Assert.Equal(original, pack.Data);
        NativeBattleEffectTextureState cold = NativeBattleEffectTextureState.FromColdProcess();
        Assert.DoesNotContain(cold.ReadVram(), value => value != 0);
        NativeBattleEffectTextureState uploaded = NativeBattleModelTextures.Enqueue507400(cold, load).Drain505D20().State;
        Assert.Contains(uploaded.ReadVram(), value => value != 0);
        output.WriteLine($"Ifrit: TIMs={load.TimOffsets.Length}; reserved=003F; allocated={load.AllocatedSlots:X4}; rows={string.Join(',', load.Binding.PaletteRows.Select(row => row?.ToString() ?? "?"))}");
    }

    [Fact]
    public void InstalledIfritClonesRetainTheirSharedRelocationUntilTheLastPhysicalRowIsReleasedAcrossJsonRestore()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectHeapAllocation pack = Assert.IsType<NativeBattleEffectHeapAllocation>(assets.ReadEnemyNativeGraphicsResource(61).BodyTexturePack);
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess().Reset508260();
        Assert.Equal(11, cache.Rows.Length);
        Assert.All(cache.Rows, row => Assert.Equal((byte)0, row.Kind));
        Assert.Equal((ushort)63, cache.BlockedSlots);
        NativeBattleModelTextureCacheLoad original = cache.LoadEnemy(77, pack);
        Assert.Equal(0, original.ResourceIndex);
        NativeBattleModelTextureLoad upload = Assert.IsType<NativeBattleModelTextureLoad>(original.Upload);
        NativeBattleModelTextureCacheLoad clone = original.Cache.LoadEnemy(77, new("unread-on-507740-cache-hit", []));
        Assert.Equal(1, clone.ResourceIndex);
        Assert.Null(clone.Upload);
        Assert.Equal(upload.Binding, clone.Binding);
        Assert.Equal(original.Cache.BlockedSlots, clone.Cache.BlockedSlots);
        Assert.Equal(original.Cache.Rows[0], clone.Cache.Rows[1]);

        NativeBattleModelTextureCache restored = JsonSerializer.Deserialize<NativeBattleModelTextureCache>(JsonSerializer.Serialize(clone.Cache))!;
        NativeBattleModelTextureCache firstReleased = clone.Cache.Release5080D0(original.ResourceIndex);
        NativeBattleModelTextureCache independentFirst = restored.Release5080D0(original.ResourceIndex);
        Assert.Equal(JsonSerializer.Serialize(firstReleased), JsonSerializer.Serialize(independentFirst));
        Assert.Equal((byte)0, firstReleased.Rows[0].Kind);
        Assert.Equal((byte)3, firstReleased.Rows[1].Kind);
        Assert.Equal(upload.Binding, firstReleased.Rows[0].Binding);
        Assert.Equal(clone.Cache.BlockedSlots, firstReleased.BlockedSlots);
        NativeBattleModelTextureCache lastReleased = firstReleased.Release5080D0(clone.ResourceIndex);
        Assert.Equal(JsonSerializer.Serialize(lastReleased), JsonSerializer.Serialize(independentFirst.Release5080D0(clone.ResourceIndex)));
        Assert.Equal((ushort)63, lastReleased.BlockedSlots);
        Assert.All(lastReleased.Rows, row => Assert.Equal((byte)0, row.Kind));
        NativeBattleModelTextureCacheLoad reloaded = lastReleased.LoadEnemy(77, pack);
        Assert.Equal(0, reloaded.ResourceIndex);
        Assert.NotNull(reloaded.Upload);
        Assert.Equal(upload.Binding.PaletteRows.ToArray(), reloaded.Binding.PaletteRows.ToArray());
        Assert.Equal(original.Cache.BlockedSlots, reloaded.Cache.BlockedSlots);
    }

    [Fact]
    public void CacheResetOnlyClearsKindsAndTheGlobalMaskWithoutInventingPreviouslyUnknownRowFields()
    {
        NativeBattleModelTextureCache unknown = NativeBattleModelTextureCache.Initialize508260(null);
        Assert.Equal((ushort)63, unknown.BlockedSlots);
        Assert.All(unknown.Rows, row =>
        {
            Assert.Equal((byte)0, row.Kind);
            Assert.Null(row.ModelId);
            Assert.Null(row.TextureMask);
            Assert.Null(row.Binding);
        });
        NativeBattleModelTextureBinding binding = new(ImmutableArray.CreateRange(Enumerable.Repeat<byte?>(null, 16)));
        NativeBattleModelTextureCacheRow retained = new(3, 77, 0xC0, binding);
        NativeBattleModelTextureCache cache = unknown with { Rows = unknown.Rows.SetItem(5, retained), BlockedSlots = 0xFF };
        NativeBattleModelTextureCache reset = cache.Reset508260();
        Assert.Equal(retained with { Kind = 0 }, reset.Rows[5]);
        Assert.Equal((ushort)63, reset.BlockedSlots);
        Assert.All(reset.Rows, row => Assert.Equal((byte)0, row.Kind));
    }

    [Fact]
    public void ElevenInstalledIfritAliasesFillThePhysicalBankWithoutEvictingOrReuploading()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectHeapAllocation pack = Assert.IsType<NativeBattleEffectHeapAllocation>(assets.ReadEnemyNativeGraphicsResource(61).BodyTexturePack);
        NativeBattleModelTextureCacheLoad first = NativeBattleModelTextureCache.FromColdProcess().Reset508260().LoadEnemy(77, pack);
        NativeBattleModelTextureCache cache = first.Cache;
        for (int index = 1; index < 11; index++)
        {
            NativeBattleModelTextureCacheLoad clone = cache.LoadEnemy(77, new("unread-on-507740-cache-hit", []));
            Assert.Equal(index, clone.ResourceIndex);
            Assert.Null(clone.Upload);
            Assert.Equal(first.Binding, clone.Binding);
            cache = clone.Cache;
        }
        Assert.Equal(first.Cache.BlockedSlots, cache.BlockedSlots);
        Assert.Throws<NotSupportedException>(() => cache.LoadEnemy(77, pack));
        Assert.All(cache.Rows, row => Assert.Equal((byte)3, row.Kind));
    }

    [Fact]
    public void FreshOriginalEncounter94PublishesItsEnemyTextureCacheAndRestoresTheSameNextGraphicsCallback()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, encounter.Id);
        BattleDefinition definition = new()
        {
            EncounterId = 94,
            StageId = encounter.StageId,
            Combatants = actors,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        NativeBattleModelTextureCache initial = Assert.IsType<NativeBattleModelTextureCache>(battle.NativeModelTextureCache);
        Assert.Equal((ushort)63, initial.BlockedSlots);
        Assert.All(initial.Rows, row => Assert.Equal((byte)0, row.Kind));
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(battle.NativeGraphics);
        NativeBattleGraphicsModel enemy = graphics.Models[3];
        int resourceIndex = Assert.IsType<int>(enemy.TextureResourceIndex);
        NativeBattleModelTextureCache cache = Assert.IsType<NativeBattleModelTextureCache>(battle.NativeModelTextureCache);
        NativeBattleModelTextureCacheRow row = cache.Rows[resourceIndex];
        Assert.Equal((byte)3, row.Kind);
        Assert.Equal((byte?)77, row.ModelId);
        Assert.Equal(row.TextureMask, enemy.TextureAllocationMask);
        Assert.Equal(row.Binding, enemy.BodyTextureSlots);
        Assert.NotEqual((ushort)63, cache.BlockedSlots);
        NativeBattleEffectTextureState textures = Assert.IsType<NativeBattleEffectTextureState>(battle.NativeEffectMemory!.Textures);
        Assert.Contains(textures.ReadVram(), value => value != 0);

        BattleSnapshot snapshot = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
        TimeSpan callbackDuration = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        battle.AdvanceNativeGraphicsPresentation(callbackDuration);
        restored.AdvanceNativeGraphicsPresentation(callbackDuration);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));

        Assert.Equal(0, enemy.Flags2!.Value & 4);
        foreach ((uint soundFlags, int loadCount, bool cleared) in new (uint, int, bool)[] { (0, 1, true), (1, 0, false), (0x80000000, 0, false), (uint.MaxValue, 1, false) })
        {
            NativeBattleGraphicsModel removing = NativeBattleGraphicsModels.StartRemoval(enemy, 5) with { Phase6 = 1 };
            BattleSession removal = BattleSession.Restore(snapshot with
            {
                NativeGraphics = graphics with { Models = graphics.Models.SetItem(3, removing), ModelLoadCount = loadCount },
                NativeEffectMemory = snapshot.NativeEffectMemory! with { SoundPublishedFlags = soundFlags }
            });
            removal.AdvanceNativeGraphicsPresentation(callbackDuration);
            Assert.Equal(cleared, removal.NativeGraphics!.Models[3].Flags0 == 0);
            Assert.Equal(cleared ? (byte)0 : (byte)3, removal.NativeModelTextureCache!.Rows[resourceIndex].Kind);
            Assert.Equal(cleared ? (ushort)63 : cache.BlockedSlots, removal.NativeModelTextureCache.BlockedSlots);
        }
    }
}
