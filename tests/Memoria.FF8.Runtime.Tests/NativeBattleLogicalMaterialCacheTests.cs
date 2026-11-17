using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalMaterialCacheTests
{
    private static readonly NativeBattleModelTextureOwner Owner = new("ally");

    [Fact]
    public void Context_default_cache_hooks_match_the_legacy_dispatch()
    {
        NativeBattleLogicalOrdering ordering = Ordering(null);
        NativeBattlePrimitiveTables tables = Tables();
        NativeBattlePrimitiveDrawMode mode = new(0) { TextureWindowBits = 0 };
        DefaultContext context = new();

        var legacy = NativeBattleLogicalPasses.Execute(ordering, tables, mode, 1, context.Cache);
        var observed = NativeBattleLogicalPasses.Execute(ordering, tables, mode, context);

        Assert.Equal(NativeBattleTestSnapshots.Json(legacy.Commands), NativeBattleTestSnapshots.Json(observed.Commands));
        Assert.Equal(NativeBattleTestSnapshots.Json(legacy.Cache), NativeBattleTestSnapshots.Json(observed.Cache));
        Assert.Equal(new byte[] { 0, 1 }, context.NormalPasses);
    }

    [Fact]
    public void Command_cache_hooks_preprocess_resolve_and_flush_the_selected_owner()
    {
        MaterialContext context = new();
        var executed = NativeBattleLogicalPasses.Execute(Ordering(Owner), Tables(), new(0) { TextureWindowBits = 0 }, context);

        Assert.Equal(new[] { "begin:0", "get", "set", "retain:0", "end:0", "begin:1", "get", "set", "retain:1", "end:1", "begin:2", "end:2" }, context.Calls);
        Assert.Equal(2, executed.Commands.Length);
        Assert.Equal(0, executed.Commands[1].DrawMode.TextureSelection!.Row);
        Assert.Equal(1f, executed.Cache!.ScaleU);
        Assert.Empty(executed.Cache.Rows[0].Regions);
        Assert.Equal(2f, context.ActorCache.ScaleU);
        Assert.Single(context.ActorCache.Rows[0].Regions);
        Assert.Equal(0u, context.ActorCache.Rows[0].UsedMask);
    }

    [Fact]
    public void Executor_keeps_interleaved_caches_and_global_pixel_publications_with_their_owners()
    {
        NativeBattlePcTextureWindowCache prepared = ResetCache(1).Preprocess462F90(0, 0).Prepare464BD0().Cache;
        NativeBattleEffectTextureState leaf = NativeBattleEffectTextureState.FromColdProcess().PublishVramSpan(0, [0x34, 0x12]);
        NativeBattleEffectTextureState initial = NativeBattleEffectTextureState.FromColdProcess()
            .WithPresentationTexture("ally", new(leaf, prepared))
            .WithPresentationTexture("clone", new(NativeBattleEffectTextureState.FromColdProcess(), ResetCache(1)))
            .ClonePresentationEnemyTexture("ally", "clone", 7);
        NativeBattleSoftwarePrimitiveExecution execution = CreateExecution(initial, prepared);
        NativeBattleLogicalCommand first = Assert.Single(Ordering(Owner).Traverse(Tables().WordLengths));
        NativeBattleLogicalCommand second = first with { ModelTextureOwner = new("clone") };
        NativeBattleLogicalCommand global = first with { ModelTextureOwner = null };
        NativeBattlePrimitiveDrawMode mode = new(0) { TextureWindowBits = 0 };
        NativeBattlePcTextureWindowSelection fullPage = prepared.Resolve465CE0(0, 0, 0).Selection;

        execution.BeginOrdering();
        execution.ValidateModelTextureOwner(first);
        Assert.Same(prepared, execution.GetTextureCache(first));
        NativeBattleLogicalResolvedCommand oldGlobal = execution.RetainTexturePixels(global, mode, 1, fullPage);
        NativeBattleLogicalResolvedCommand oldAlly = execution.RetainTexturePixels(first, mode, 1, fullPage);

        NativeBattleLogicalCommand fill = new(new("fill", 0, 0), 1, 0x02, [0x020000FF, 0, 0x00010001], 1, [], null);
        execution.ExecuteSoftware(fill);
        execution.FlushSoftwareImage();
        Assert.Same(initial.PresentationTextures, execution.Textures.PresentationTextures);
        Assert.Same(prepared, execution.GetTextureCache(first));
        NativeBattleLogicalResolvedCommand newGlobal = execution.RetainTexturePixels(global, mode, 1, fullPage);

        NativeBattlePcTextureWindowResolve changed = prepared.Resolve465CE0(0, 0, 1);
        execution.SetTextureCache(first, changed.Cache);
        NativeBattlePcTextureWindowCache otherCache = execution.GetTextureCache(second)!;
        Assert.Empty(otherCache.Rows[0].Regions);
        Assert.Equal(changed.Cache.ScaleU, otherCache.ScaleU);
        Assert.Same(changed.Cache, execution.GetTextureCache(first));
        Assert.Same(prepared, initial.RequirePresentationTexture("ally").Cache);
        NativeBattleLogicalResolvedCommand newAlly = execution.RetainTexturePixels(first, mode, 1, changed.Selection);

        NativeBattlePresentationTextureBank publishedAlly = execution.Textures.RequirePresentationTexture("ally");
        NativeBattlePresentationTextureBank publishedClone = execution.Textures.RequirePresentationTexture("clone");
        Assert.Equal(7, publishedAlly.EnemyAllocationId);
        Assert.Equal(7, publishedClone.EnemyAllocationId);
        Assert.Same(leaf, publishedAlly.Textures);
        Assert.Same(leaf, publishedClone.Textures);
        Assert.Same(initial.RequirePresentationTexture("clone"), publishedClone);
        Assert.Equal(0, ReadPixel(oldGlobal));
        Assert.Equal(31, ReadPixel(newGlobal));
        Assert.Equal(0x1234, ReadPixel(oldAlly));
        Assert.Equal(0x1234, ReadPixel(newAlly));

        execution.SetTextureCache(second, otherCache.Preprocess462F90(0, 0));
        execution.BeginPass(1);
        Assert.Single(execution.GetTextureCache(second)!.Rows[0].Regions);
        execution.EndPass(1);
        Assert.Equal(0u, execution.GetTextureCache(second)!.Rows[0].UsedMask);
        Assert.Same(leaf, execution.Textures.RequirePresentationTexture("clone").Textures);
        Assert.Empty(initial.RequirePresentationTexture("clone").Cache.Rows[0].Regions);
        Assert.Equal(0x1234, ReadPixel(oldAlly));

        ushort ReadPixel(NativeBattleLogicalResolvedCommand resolved)
        {
            return execution.TextureSnapshots[resolved.DrawMode.TexturePixelSnapshot!.Value].ReadWord(0, 0);
        }
    }

    [Fact]
    public void Executor_keeps_global_uv_controls_when_an_interleaved_model_uses_full_page_fallback()
    {
        NativeBattlePcTextureWindowCache full = ResetCache(1).Preprocess462F90(0, 0).Prepare464BD0().Cache;
        for (uint bits = 1; bits <= 7; bits++)
        {
            full = full.Resolve465CE0(0, 0, bits).Cache;
        }
        NativeBattlePcTextureWindowCache other = ResetCache(2);
        NativeBattleEffectTextureState initial = NativeBattleEffectTextureState.FromColdProcess()
            .WithPresentationTexture("ally", new(NativeBattleEffectTextureState.FromColdProcess(), full))
            .WithPresentationTexture("other", new(NativeBattleEffectTextureState.FromColdProcess(), other));
        NativeBattleSoftwarePrimitiveExecution execution = CreateExecution(initial, full);
        NativeBattleLogicalCommand first = Assert.Single(Ordering(Owner).Traverse(Tables().WordLengths));
        NativeBattleLogicalCommand second = first with { ModelTextureOwner = new("other") };
        execution.SetTextureCache(second, other);
        NativeBattlePcTextureWindowResolve fallback = execution.GetTextureCache(first)!.Resolve465CE0(0, 0, 8);
        execution.SetTextureCache(first, fallback.Cache);

        Assert.True(fallback.Selection.Fallback);
        Assert.Equal(0, fallback.Selection.Slot);
        Assert.Equal(2f, fallback.Selection.ScaleU);
        Assert.Equal(2f, fallback.Selection.ScaleV);
        Assert.Equal(2f, execution.Cache!.ScaleU);
        Assert.Same(other, execution.GetTextureCache(second));
        Assert.Same(full, initial.RequirePresentationTexture("ally").Cache);
    }

    [Fact]
    public void Executor_rejects_invalid_initial_texture_ownership_and_caches()
    {
        NativeBattlePcTextureWindowCache cache = ResetCache(1);
        NativeBattleEffectTextureState initial = NativeBattleEffectTextureState.FromColdProcess()
            .WithPresentationTexture("ally", new(NativeBattleEffectTextureState.FromColdProcess(), cache));
        var banks = initial.PresentationTextures!;

        Assert.Throws<InvalidDataException>(() => CreateExecution(initial with { PresentationTextures = banks.WithComparers(StringComparer.OrdinalIgnoreCase) }, cache));
        Assert.Throws<InvalidDataException>(() => CreateExecution(initial with { PresentationTextures = banks.Add("duplicate", banks["ally"]) }, cache));
        Assert.Throws<InvalidDataException>(() => CreateExecution(initial with { PresentationTextures = banks.SetItem("ally", banks["ally"] with { Cache = cache with { Rows = [] } }) }, cache));
        Assert.Throws<InvalidDataException>(() => CreateExecution(initial, cache with { Rows = [] }));
    }

    [Fact]
    public void Executor_accepts_detached_texture_reads_and_rejects_cache_mutation()
    {
        NativeBattlePcTextureWindowCache cache = ResetCache(1);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess()
            .WithPresentationTexture("ally", new(NativeBattleEffectTextureState.FromColdProcess(), cache));
        NativeBattleEffectTextureState detached = NativeBattleStorageSnapshots.Capture(textures);
        NativeBattleSoftwarePrimitiveExecution execution = CreateExecution(detached, cache);
        NativeBattleLogicalCommand command = Assert.Single(Ordering(Owner).Traverse(Tables().WordLengths));

        execution.ValidateModelTextureOwner(command);
        Assert.Same(detached.RequirePresentationTexture("ally").Cache, execution.GetTextureCache(command));
        Assert.Throws<InvalidOperationException>(() => execution.SetTextureCache(command, cache.Preprocess462F90(0, 0)));
        Assert.Same(detached, execution.Textures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Standalone_selector_rejects_model_materials_without_a_bank(bool invalidated)
    {
        NativeBattleModelTextureOwner owner = invalidated ? NativeBattleModelTextureOwner.InvalidatedOwner : Owner;
        NativeBattleLogicalCommand command = Assert.Single(Ordering(owner).Traverse(Tables().WordLengths));
        NativeBattleLogicalResolvedCommand resolved = new(command, new(0) { TextureWindowBits = 0 }, 1);

        Assert.Throws<NotSupportedException>(() => NativeBattleLogicalPasses.SelectTextures([resolved], ResetCache(1)));
    }

    private static NativeBattleLogicalOrdering Ordering(NativeBattleModelTextureOwner? owner) =>
        NativeBattleLogicalOrdering.Clear().Prepend(17, new("model", 0, 0), [0x07000000, 0x24808080, 0, 0, 80, 0, 80u << 16, 0], 0, [], modelTextureOwner: owner);

    private static NativeBattlePcTextureWindowCache ResetCache(float scale) =>
        NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = scale, ScaleV = scale };

    private static NativeBattleSoftwarePrimitiveExecution CreateExecution(NativeBattleEffectTextureState textures, NativeBattlePcTextureWindowCache cache)
    {
        uint[] handlers = new uint[256];
        handlers[0x02] = 0x466EF0;
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(new(0, 0, 319, 215, 0, 0, 0, 0, 0, null));
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer() with { Environment = environment };
        return new(state, textures, new(handlers.ToImmutableArray()), true, cache);
    }

    private static NativeBattlePrimitiveTables Tables()
    {
        byte[] lengths = new byte[256];
        lengths[0x24] = 7;
        uint[] handlers = new uint[256];
        handlers[0x24] = 1;
        ImmutableArray<uint> supported = handlers.ToImmutableArray();
        return new(lengths.ToImmutableArray(), new byte[256].ToImmutableArray(), supported, supported, new uint[256].ToImmutableArray());
    }

    private class DefaultContext : INativeBattleLogicalExecutionContext
    {
        public uint LiveMode => 1;
        public uint LiveDebug => 0;
        public bool HardwareEnabled => true;
        public NativeSoftwarePrimitiveTable SoftwareTable { get; } = new(new uint[256].ToImmutableArray());
        public NativeBattlePcTextureWindowCache? Cache { get; set; } = ResetCache(1);
        public List<byte> NormalPasses { get; } = [];

        public void ExecuteSoftware(NativeBattleLogicalCommandReference command)
        {
            throw new InvalidOperationException("This fixture has no software handlers.");
        }

        public void ExecuteNormal(NativeBattleLogicalCommandReference command, NativeBattlePrimitiveDrawMode mode, byte pass)
        {
            NormalPasses.Add(pass);
        }

        public NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode) => mode;

        public void EndBatch()
        {
        }
    }

    private sealed class MaterialContext : DefaultContext, INativeBattleLogicalExecutionContext
    {
        public NativeBattlePcTextureWindowCache ActorCache { get; private set; } = ResetCache(2);
        public List<string> Calls { get; } = [];

        public void ValidateModelTextureOwner(NativeBattleLogicalCommandReference command)
        {
            Assert.Equal(Owner, command.ModelTextureOwner);
        }

        public NativeBattlePcTextureWindowCache? GetTextureCache(NativeBattleLogicalCommandReference command)
        {
            Assert.Equal(Owner, command.ModelTextureOwner);
            Calls.Add("get");
            return ActorCache;
        }

        public void SetTextureCache(NativeBattleLogicalCommandReference command, NativeBattlePcTextureWindowCache? cache)
        {
            Assert.Equal(Owner, command.ModelTextureOwner);
            Calls.Add("set");
            ActorCache = cache!;
        }

        public NativeBattleLogicalResolvedCommand RetainTexturePixels(NativeBattleLogicalResolvedCommand command)
        {
            Calls.Add($"retain:{command.Pass}");
            Assert.Equal(Owner, command.Command.ModelTextureOwner);
            if (command.Pass == 0)
            {
                Assert.Null(command.DrawMode.TextureSelection);
            }
            else
            {
                NativeBattlePcTextureWindowSelection selection = Assert.IsType<NativeBattlePcTextureWindowSelection>(command.DrawMode.TextureSelection);
                Assert.NotEmpty(ActorCache.Rows[selection.Row].Regions);
            }
            return command;
        }

        public void BeginPass(byte pass)
        {
            Calls.Add($"begin:{pass}");
            if (pass == 1)
            {
                ActorCache = ActorCache.Prepare464BD0().Cache;
            }
        }

        public void EndPass(byte pass)
        {
            Calls.Add($"end:{pass}");
            if (pass == 1)
            {
                Assert.Equal(0u, Cache!.Rows[0].UsedMask);
                ActorCache = ActorCache.Flush465930();
            }
        }
    }
}
