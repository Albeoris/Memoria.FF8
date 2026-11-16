using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AttachedEncounterEnemyModelCloneTests
{
    [Fact]
    public void Formation89_shares_the_original_enemy_heap_tail_and_reclaims_it_after_both_skeletons()
    {
        FormationHeap physical = ReadHeap(attached: false);
        FormationHeap attached = ReadHeap(attached: true);

        Assert.Equal(48356, physical.Cursor);
        Assert.Equal(22792, physical.SecondSkeleton - physical.FirstSkeleton);
        Assert.Equal(physical, attached);
    }

    [Fact]
    public void Shared_texture_copy_drains_once_updates_each_cache_and_survives_source_retirement()
    {
        BattleEnemyActorModel sourceOwner = new(new("source"));
        BattleEnemyActorModel cloneOwner = new(new("clone"));
        BattleEnemyActorModel independentOwner = new(new("independent"));
        NativeBattleEffectHeapAllocation emptyPack = new("empty-texture-pack", [0, 0, 0, 0, 0, 0, 0, 0]);
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(new([sourceOwner, cloneOwner, independentOwner]));
        NativeBattleModelTextureCacheLoad sourceResource = cache.LoadEnemy(34, emptyPack, sourceOwner);
        NativeBattleModelTextureCacheLoad independentResource = sourceResource.Cache.LoadEnemy(34, emptyPack, independentOwner);
        NativeBattleModelTextureCacheLoad cloneResource = independentResource.Cache.CloneHostedEnemy(new(34, cloneOwner, null, 0, 0, 0x507120, 0), sourceResource.ResourceIndex);
        cache = cloneResource.Cache;
        int allocation = cache.Rows[sourceResource.ResourceIndex].EnemyTextureAllocationId!.Value;
        Assert.Equal(allocation, cache.Rows[cloneResource.ResourceIndex].EnemyTextureAllocationId);
        Assert.NotEqual(allocation, cache.Rows[independentResource.ResourceIndex].EnemyTextureAllocationId);
        Assert.Null(cloneResource.Upload);

        NativeBattleEffectTextureState leaf = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 2, 1, [0x11, 0, 0x44, 0]);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess()
            .WithPresentationTexture("source", new(leaf, TextureCache()))
            .WithPresentationTexture("clone", new(NativeBattleEffectTextureState.FromColdProcess(), TextureCache()))
            .WithPresentationTexture("independent", new(NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x77, 0]), TextureCache()))
            .ClonePresentationEnemyTexture("source", "clone", allocation);
        NativeBattlePresentationTextureBank before = state.RequirePresentationTexture("source");
        NativeBattlePresentationTextureBank clone = state.RequirePresentationTexture("clone");
        Assert.Same(before.Textures, clone.Textures);
        Assert.NotSame(before.Cache, clone.Cache);

        state = state.WithPresentationTexture("clone", clone with { Cache = TextureCache(), Textures = clone.Textures.EnqueueCopy505EB0(1, 0, 1, 1, 0, 0) });
        Assert.Equal(1, state.RequirePresentationTexture("source").Textures.Count);
        Assert.Equal(1, state.RequirePresentationTexture("clone").Textures.Count);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0) with { Textures = state };
        NativeBattleEffectTextureState drained = BattleSession.DrainNativeTextureUploads(memory, state, null).Textures!;
        NativeBattleEffectTextureState shared = drained.RequirePresentationTexture("source").Textures;
        Assert.Same(shared, drained.RequirePresentationTexture("clone").Textures);
        Assert.Equal(0, shared.Count);
        Assert.Equal((ushort)0x44, shared.ReadVramWord(0, 0));
        Assert.Equal((ushort)0x11, before.Textures.ReadVramWord(0, 0));
        Assert.Equal((ushort)0x77, drained.RequirePresentationTexture("independent").Textures.ReadVramWord(0, 0));
        foreach (string actor in new[] { "source", "clone" })
        {
            NativeBattlePcTextureWindowRegion region = Assert.Single(drained.RequirePresentationTexture(actor).Cache.Rows[0].Regions);
            Assert.False(region.CurrentPixels);
            Assert.Equal((ushort)0x11, region.RetainedPixels!.ReadWord(0, 0));
        }

        NativeBattleEffectTextureState capture = NativeBattleStorageSnapshots.Capture(drained);
        var decoded = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(capture));
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(decoded!);
        Assert.Same(restored.RequirePresentationTexture("source").Textures, restored.RequirePresentationTexture("clone").Textures);
        NativeBattleEffectTextureState survivor = restored.ReleasePresentationEnemyTexture("source");
        Assert.Equal(allocation, survivor.RequirePresentationTexture("clone").EnemyAllocationId);
        Assert.Null(survivor.RequirePresentationTexture("source").EnemyAllocationId);
        NativeBattlePresentationTextureBank stillActive = survivor.RequirePresentationTexture("clone");
        survivor = survivor.WithPresentationTexture("clone", stillActive with { Textures = stillActive.Textures.Upload45BD30(0, 0, 1, 1, [0x66, 0]) });
        Assert.Equal((ushort)0x44, survivor.RequirePresentationTexture("source").Textures.ReadVramWord(0, 0));
        Assert.Equal((ushort)0x66, survivor.RequirePresentationTexture("clone").Textures.ReadVramWord(0, 0));
        survivor = survivor.ReleasePresentationEnemyTexture("clone");
        Assert.All(survivor.PresentationTextures!.Values, bank => Assert.Null(bank.EnemyAllocationId));
        cache = cache.Release5080D0(sourceResource.ResourceIndex).Release5080D0(cloneResource.ResourceIndex);
        Assert.Equal(independentOwner, Assert.Single(cache.PresentationActors!).Value);
    }

    private static FormationHeap ReadHeap(bool attached)
    {
        const ushort formation = 89;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game) { UsePhysicalBattlePresentation = !attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, formation, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        for (int frame = 0; frame < 1000 && !battle.NativeEntryReady; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(battle.NativeEntryReady);
        ExpandedBattlePresentationProcess? process = battle.ExpandedPresentationProcess;
        Assert.Equal(attached, process is not null);
        NativeBattleGraphicsState graphics = process?.Graphics ?? battle.NativeGraphics!;
        NativeBattleModelCpuResources cpu = process?.ModelCpuResources ?? battle.NativeModelCpuResources!;
        NativeBattleModelTextureCache cache = process?.ModelTextureCache ?? battle.NativeModelTextureCache!;
        BattleCombatant[] actors = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.ModelId == 18).OrderBy(actor => actor.SceneSlot).ToArray();
        Assert.Equal(2, actors.Length);
        int[] rows = actors.Select(actor => graphics.ActorModels.Get(battle.ReadPresentationModelOwner(actor.Id)).TextureResourceIndex!.Value).ToArray();
        Assert.NotEqual(rows[0], rows[1]);
        Assert.Equal(cpu.Rows[rows[0]].Require(32), cpu.Rows[rows[1]].Require(32));
        if (process is not null)
        {
            Assert.Equal(cache.Rows[rows[0]].EnemyTextureAllocationId, cache.Rows[rows[1]].EnemyTextureAllocationId);
            Assert.NotNull(cache.Rows[rows[0]].EnemyTextureAllocationId);
            NativeBattleEffectTextureState textures = process.Memory.Textures!;
            Assert.Same(textures.RequirePresentationTexture(actors[0].Id).Textures, textures.RequirePresentationTexture(actors[1].Id).Textures);
        }
        NativeBattleModelCpuResourceRow first = cpu.Rows[rows[0]];
        NativeBattleModelCpuResourceRow second = cpu.Rows[rows[1]];
        NativeBattleFileStaging staging = battle.NativeFileStaging ?? NativeBattleFileStaging.Unknown();
        FormationHeap heap = new(cpu.EnemyCursor!.Value, first.Require(12).Offset, second.Require(12).Offset, first.Require(32).Offset,
            first.PrivateLength4!.Value, first.SharedLength8!.Value, 0, 0, 0);
        (cpu, staging) = NativeBattleModelEnemyHeap.Release(cpu, staging, cache, rows[0]);
        cache = cache.Release5080D0(rows[0]);
        Assert.Equal(heap.Cursor - heap.PrivateSize, cpu.EnemyCursor);
        int survivingSkeleton = cpu.Rows[rows[1]].Require(12).Offset;
        int survivingScript = cpu.Rows[rows[1]].Require(32).Offset;
        (cpu, _) = NativeBattleModelEnemyHeap.Release(cpu, staging, cache, rows[1]);
        Assert.Equal(heap.Cursor - 2 * heap.PrivateSize - heap.SharedSize, cpu.EnemyCursor);
        return heap with { SurvivingSkeleton = survivingSkeleton, SurvivingScript = survivingScript, FinalCursor = cpu.EnemyCursor!.Value };
    }

    private static NativeBattlePcTextureWindowCache TextureCache()
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = 1, ScaleV = 1 };
        return cache.Preprocess462F90(0, 0).Prepare464BD0().Cache.Flush465930();
    }

    private sealed record FormationHeap(int Cursor, int FirstSkeleton, int SecondSkeleton, int Script, int PrivateSize, int SharedSize, int SurvivingSkeleton, int SurvivingScript, int FinalCursor);
}
