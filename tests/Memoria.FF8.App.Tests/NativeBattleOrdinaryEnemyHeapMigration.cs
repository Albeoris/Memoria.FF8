using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleOrdinaryEnemyHeapMigration
{
    // Explicit resource rebind for the declared ordinary-Attack component; this does not reconstruct earlier callbacks.
    internal static string[] Apply(JsonNode root, BattleAssets assets)
    {
        JsonNode battleNode = root["Battle"] ?? throw new InvalidDataException("Migration requires the named active battle component.");
        BattleSnapshot battle = battleNode.Deserialize<BattleSnapshot>()!;
        NativeBattleGraphicsState graphics = battle.NativeGraphics ?? throw new InvalidDataException("Migration requires its physical model bank.");
        Assert.Equal(4, graphics.EntryLifecycle);
        Assert.Null(graphics.OriginalEffect);
        Assert.Null(graphics.NativeOutcome);
        Assert.Null(battle.NativeModelLoader?.Task);
        Assert.Equal(0, battle.NativeEffectMemory!.Active);
        Assert.Null(battle.NativeEffectMemory.Entry21DFEC0);
        NativeBattleModelCpuResources cpu = battle.NativeModelCpuResources ?? throw new InvalidDataException("Migration must retain the actual party CPU banks.");
        Assert.Null(cpu.EnemyCursor);
        NativeBattleFileStaging staging = battle.NativeFileStaging ?? throw new InvalidDataException("Migration must retain its current shared party/file memory.");
        NativeBattleModelTextureCache cache = battle.NativeModelTextureCache ?? throw new InvalidDataException("Migration requires the actual resource row material bindings.");
        NativeBattleGraphicsDefinition definition = battle.Definition.NativeGraphics!;
        var models = graphics.Models.ToBuilder();
        var bindings = definition.Bindings.ToBuilder();
        cpu = cpu with { EnemyCursor = 0, EnemyLinkedSlots = 0 };
        foreach (NativeBattleGraphicsModel model in graphics.Models.Where(model => (model.Flags0 & 2) != 0 && model.ModelId4 >= 16).OrderBy(model => model.Index))
        {
            Assert.NotEqual((byte?)143, model.ModelId4);
            Assert.Null(model.ActiveStagedResourceRow100);
            Assert.Null(model.ActiveResourceRow100);
            Assert.Null(model.ResourceMagicId);
            int row = model.TextureResourceIndex ?? throw new InvalidDataException("Migration requires the actual retained texture row pointer.");
            Assert.Equal((byte)3, cache.Rows[row].Kind);
            Assert.Equal(model.ModelId4, cache.Rows[row].ModelId);
            Assert.Equal(model.BodyTextureSlots!.PaletteRows.ToArray(), cache.Rows[row].Binding!.PaletteRows.ToArray());
            Assert.Equal(model.TextureAllocationMask, cache.Rows[row].TextureMask);
            NativeBattleModelDatSource source = assets.ReadNativeModelDatSource(model.ModelId4!.Value, false, 0);
            NativeBattleGraphicsBinding binding = definition.Bindings.Single(binding => binding.ActorId == graphics.ActorIds[model.Index]);
            Assert.Equal(binding.ModelId, model.ModelId4);
            // The named component had no nonempty motion table; correcting its section selector cannot change its bytecode PC.
            Assert.Equal(source.Section(3), source.Section(4));
            Assert.Equal(source.Bytes.Slice(source.Section(4), source.Section(5) - source.Section(4)).ToArray(), binding.Resource.SequenceSection.ToArray());
            NativeBattleModelTextureLoad upload = NativeBattleModelTextures.Prepare507400(source.TexturePack(0x507120), unchecked((ushort)~model.TextureAllocationMask!.Value));
            Assert.Equal(model.TextureAllocationMask, upload.AllocatedSlots);
            Assert.Equal(model.BodyTextureSlots.PaletteRows.ToArray(), upload.Binding.PaletteRows.ToArray());
            source = source.Relocate507400(0x507120, upload);
            int? existing = Enumerable.Range(0, cpu.Rows.Length).Where(index => index != row && cpu.Rows[index].PrivateLength4 is not null && cache.Rows[index].Kind == 3 && cache.Rows[index].ModelId == model.ModelId4).Select(index => (int?)index).FirstOrDefault();
            if (existing is null)
            {
                byte slot = 255;
                if (source.Section(9) != source.Section(10))
                {
                    for (byte index = 0; index < 3; index++)
                    {
                        if ((cpu.EnemyLinkedSlots!.Value & (1 << index)) == 0)
                        {
                            slot = index;
                            cpu = cpu with { EnemyLinkedSlots = unchecked((byte)(cpu.EnemyLinkedSlots.Value | (1 << index))) };
                            break;
                        }
                    }
                }
                cpu = cpu with { Rows = cpu.Rows.SetItem(row, cpu.Rows[row] with { LinkedPhysicalIndex36 = slot }) };
            }
            (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, source, row, existing, false);
            models[model.Index] = model with { ActiveResourceRow100 = row, OriginalResourceRow104 = row };
            int bindingIndex = Array.FindIndex(bindings.ToArray(), item => item.ActorId == binding.ActorId);
            bindings[bindingIndex] = binding with { Resource = binding.Resource with { ModelLoadSources = [assets.ReadNativeModelDatSource(model.ModelId4.Value, false, 0)] } };
        }
        cpu.Validate();
        staging.Validate();
        battleNode["NativeModelCpuResources"] = JsonSerializer.SerializeToNode(cpu);
        battleNode["NativeFileStaging"] = JsonSerializer.SerializeToNode(staging);
        battleNode["NativeGraphics"]!["Models"] = JsonSerializer.SerializeToNode(models.ToImmutable());
        battleNode["Definition"]!["NativeGraphics"]!["Bindings"] = JsonSerializer.SerializeToNode(bindings.ToImmutable());
        root["NativeBattleMemory"]!["ModelCpuResources"] = JsonSerializer.SerializeToNode(cpu);
        root["NativeBattleMemory"]!["FileStaging"] = JsonSerializer.SerializeToNode(staging);
        return ["Battle.NativeModelCpuResources", "Battle.NativeFileStaging", "Battle.NativeGraphics.Models", "Battle.Definition.NativeGraphics.Bindings", "NativeBattleMemory.ModelCpuResources", "NativeBattleMemory.FileStaging"];
    }
}
