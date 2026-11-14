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

        Assert.Equal(new[] { "begin:0", "get", "set", "end:0", "begin:1", "get", "set", "end:1", "begin:2", "end:2" }, context.Calls);
        Assert.Equal(2, executed.Commands.Length);
        Assert.Equal(0, executed.Commands[1].DrawMode.TextureSelection!.Row);
        Assert.Equal(1f, executed.Cache!.ScaleU);
        Assert.Empty(executed.Cache.Rows[0].Regions);
        Assert.Equal(2f, context.ActorCache.ScaleU);
        Assert.Single(context.ActorCache.Rows[0].Regions);
        Assert.Equal(0u, context.ActorCache.Rows[0].UsedMask);
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

        public void ExecuteSoftware(NativeBattleLogicalCommand command)
        {
            throw new InvalidOperationException("This fixture has no software handlers.");
        }

        public void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass)
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

        public void ValidateModelTextureOwner(NativeBattleLogicalCommand command)
        {
            Assert.Equal(Owner, command.ModelTextureOwner);
        }

        public NativeBattlePcTextureWindowCache? GetTextureCache(NativeBattleLogicalCommand command)
        {
            Assert.Equal(Owner, command.ModelTextureOwner);
            Calls.Add("get");
            return ActorCache;
        }

        public void SetTextureCache(NativeBattleLogicalCommand command, NativeBattlePcTextureWindowCache? cache)
        {
            Assert.Equal(Owner, command.ModelTextureOwner);
            Calls.Add("set");
            ActorCache = cache!;
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
