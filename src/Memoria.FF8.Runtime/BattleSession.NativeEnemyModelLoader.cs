using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryPollNativeEnemyModel(ref NativeBattleGraphicsState graphics, NativeBattleModelLoader loader, out int remaining)
    {
        remaining = 0;
        if (loader.Task is not { Callback: 0x507120 or 0x507F80 } task)
        {
            return false;
        }
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native enemy load requires its eleven physical resource rows.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native enemy load requires its retained heap cursor and row aliases.");
        NativeBattleFileStaging staging = NativeFileStaging ?? NativeBattleFileStaging.Unknown();
        if (task.Phase == 0 && task.Callback == 0x507120)
        {
            int existing = Array.FindIndex(cache.Rows.ToArray(), row => row.Kind == 3 && row.ModelId == task.ModelId);
            if (existing >= 0)
            {
                NativeBattleModelTextureCacheLoad clone = cache.LoadEnemy(task.ModelId, new("unused-shared-pack", []));
                NativeBattleModelDatSource source = NativeModelResourceSources().First(item => item.ModelId == task.ModelId && !item.Linked);
                (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, source, clone.ResourceIndex, existing, false);
                NativeModelCpuResources = cpu;
                NativeFileStaging = staging;
                NativeModelTextureCache = clone.Cache;
                BindNativeEnemyModel(ref graphics, task, clone.ResourceIndex, clone.Binding);
                NativeModelLoader = new(null);
                return true;
            }
        }
        if (task.Callback != 0x507F80 || task.Phase != 1 || NativeEffectIoResult < 0)
        {
            return false;
        }
        int shared = -1;
        int destination = -1;
        for (int index = 0; index < cache.Rows.Length; index++)
        {
            if (cache.Rows[index] is { Kind: 3, ModelId: 142 })
            {
                shared = index;
            }
            if (destination < 0 && cache.Rows[index].Kind == 0)
            {
                destination = index;
            }
        }
        if (destination < 0)
        {
            throw new NotSupportedException("Native507F80 has no free resource row.");
        }
        int owner = shared >= 0 ? shared : cpu.LastModel142Row ?? throw new NotSupportedException("Native507F80 has no actually retained model142 row.");
        NativeBattleModelTextureBinding binding = cache.Rows[owner].Binding ?? throw new NotSupportedException("Native507F80 model142 material binding remains unknown.");
        NativeBattleModelFileSelection file = task.File ?? throw new InvalidDataException("Native507F80 has no completed source file.");
        NativeBattleModelDatSource dat = new(task.ModelId, false, 0, file.FileId, file.Name, staging.ReadDat(NativeBattleFileStaging.Address));
        (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, dat, destination, owner, true);
        NativeModelCpuResources = cpu;
        NativeFileStaging = staging;
        NativeModelTextureCache = cache with { Rows = cache.Rows.SetItem(destination, cache.Rows[destination] with { Kind = 3, ModelId = 143, TextureMask = 0, Binding = binding }) };
        BindNativeEnemyModel(ref graphics, task, destination, binding);
        NativeModelLoader = new(null);
        return true;
    }

    private void ReserveNativeEnemyResource(NativeBattleModelLoadTask task, NativeBattleModelTextureLoad upload)
    {
        NativeBattleModelTextureCache cache = NativeModelTextureCache!;
        NativeBattleModelTextureCacheLoad load = cache.LoadEnemy(task.ModelId, task.OriginalTexturePack!);
        if (load.Upload is null || load.Upload.AllocatedSlots != upload.AllocatedSlots || !load.Binding.PaletteRows.SequenceEqual(upload.Binding.PaletteRows))
        {
            throw new InvalidDataException("Native507120 material allocation changed while its singleton task was executing.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources!;
        NativeBattleModelDatSource source = new(task.ModelId, false, task.Selector, task.File!.FileId, task.File.Name, NativeFileStaging!.ReadDat(NativeBattleFileStaging.Address));
        byte slot = 255;
        if (source.Section(9) != source.Section(10))
        {
            byte occupied = cpu.EnemyLinkedSlots ?? throw new NotSupportedException("Native507120 requires its retained linked sound slot mask.");
            for (byte index = 0; index < 3; index++)
            {
                if ((occupied & (1 << index)) == 0)
                {
                    slot = index;
                    cpu = cpu with { EnemyLinkedSlots = unchecked((byte)(occupied | (1 << index))) };
                    break;
                }
            }
        }
        NativeModelCpuResources = cpu with { Rows = cpu.Rows.SetItem(load.ResourceIndex, cpu.Rows[load.ResourceIndex] with { LinkedPhysicalIndex36 = slot }) };
        NativeModelTextureCache = load.Cache;
        NativeModelLoader = NativeModelLoader! with { Task = task with { EnemyResourceIndex = load.ResourceIndex } };
    }

    private void CompleteNativeEnemyResource(ref NativeBattleGraphicsState graphics, NativeBattleModelResourceBind bind)
    {
        int row = bind.Task.EnemyResourceIndex ?? throw new InvalidDataException("Native507120 completion has no actually allocated resource row.");
        (NativeModelCpuResources, NativeFileStaging) = NativeBattleModelEnemyHeap.Load(NativeModelCpuResources!, NativeFileStaging!, bind.PreparedDat, row, null, false);
        BindNativeEnemyModel(ref graphics, bind.Task, row, bind.TextureBinding);
    }

    private void BindNativeEnemyModel(ref NativeBattleGraphicsState graphics, NativeBattleModelLoadTask task, int row, NativeBattleModelTextureBinding binding)
    {
        NativeBattleGraphicsModel model = graphics.Models[task.PhysicalIndex];
        graphics = graphics with { Models = graphics.Models.SetItem(task.PhysicalIndex, model with { ActiveResourceRow100 = row, OriginalResourceRow104 = row, ActiveStagedResourceRow100 = null, TextureResourceIndex = row, BodyTextureSlots = binding, TextureAllocationMask = NativeModelTextureCache!.Rows[row].TextureMask, ResourceMagicId = null }) };
    }
}
