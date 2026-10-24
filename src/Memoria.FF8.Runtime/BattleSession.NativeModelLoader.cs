using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleModelLoader? NativeModelLoader { get; private set; }
    public NativeBattleModelCpuResources? NativeModelCpuResources { get; private set; }
    public NativeBattleFileStaging? NativeFileStaging { get; private set; }

    private void QueueNativeModelResource507080(NativeBattleGraphicsState graphics, ushort request, byte physicalIndex, uint argument = 0)
    {
        NativeBattleGraphicsModel model = graphics.Models[physicalIndex];
        bool linked = request >= 0x1000;
        byte body = linked || request >= 16 ? (byte)0 : model.FileSelector129 ?? throw new NotSupportedException("Native5077B0 requires the observed model BYTE129 costume selector.");
        byte weapon = !linked || request == 0x1007 ? (byte)0 : model.FileSelector128 ?? throw new NotSupportedException("Native507BF0/507E20 requires the observed model BYTE128 kernel weapon selector.");
        NativeModelLoader = NativeBattleModelLoader.Queue507080(request, physicalIndex, argument, weapon, body);
    }

    private int PollNativeModelResource507070(ref NativeBattleGraphicsState graphics)
    {
        NativeBattleModelLoader loader = NativeModelLoader ?? throw new NotSupportedException("Native507070 requires its actually initialized singleton load list.");
        if (TryPollNativeEnemyModel(ref graphics, loader, out int enemyRemaining))
        {
            return enemyRemaining;
        }
        ImmutableArray<NativeBattleModelDatSource> sources = NativeModelResourceSources();
        if (NativeFileStaging is null && loader.Task is { Phase: > 0 })
        {
            throw new NotSupportedException("Native model callback requires its retained shared199B018 staging bytes; migrate an older active load explicitly.");
        }
        NativeBattleModelLoadStep step = loader.Poll507070(NativeEffectIoResult ?? throw new NotSupportedException("Native507070 requires its retained signed1D999C8 IO result."), sources, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), NativeModelTextureCache?.BlockedSlots ?? 63);
        if (step.FileRequest is not null)
        {
            //508480 writes the result gate before482610's synchronous read and delayed508470 callback.
            NativeEffectIoResult = -1;
        }
        NativeFileStaging = step.Staging;
        if (step.FileRequest is { } request)
        {
            QueueNativeFileIo482610(request.FileResult, request.CompletionCallback);
        }
        if (step.TextureUpload is { } upload)
        {
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native507400 requires its retained model CPU/palette owner.");
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native507400 requires its retained process texture owner.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native507400 requires its actual texture-upload queue and VRAM.");
            NativeModelCpuResources = cpu.SavePalettes507400(upload);
            NativeEffectMemory = memory with { Textures = NativeBattleModelTextures.Enqueue507400(textures, upload) };
        }
        if (step.Bind is { } bind)
        {
            ApplyNativeModelResourceBind(ref graphics, bind);
        }
        NativeModelLoader = step.State;
        if (step.TextureUpload is { } enemyUpload && step.State.Task is { Callback: 0x507120 } enemyTask)
        {
            ReserveNativeEnemyResource(enemyTask, enemyUpload);
        }
        return step.Remaining;
    }

    private void ApplyNativeModelResourceBind(ref NativeBattleGraphicsState graphics, NativeBattleModelResourceBind bind)
    {
        if (bind.Task.Callback == 0x507120)
        {
            CompleteNativeEnemyResource(ref graphics, bind);
            return;
        }
        NativeBattleModelLoadTask task = bind.Task;
        NativeBattleGraphicsModel model = graphics.Models[task.PhysicalIndex];
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native resource completion requires its retained CPU bank/row aliases.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native resource completion requires its eleven physical texture rows.");
        NativeBattleEffectHeapAllocation pack = bind.OriginalTexturePack ?? throw new InvalidDataException("Native resource completion has no pre-relocation TIM pack.");
        int bodyRow;
        int? linkedRow = null;
        if (task.Callback is 0x5077B0 or 0x5079B0)
        {
            NativeBattleModelTextureCacheLoad load = cache.LoadPlayer(task.ModelId, task.PhysicalIndex, pack);
            cache = load.Cache;
            bodyRow = load.ResourceIndex;
            model = model with { BodyTextureSlots = bind.TextureBinding, TextureResourceIndex = bodyRow, TextureAllocationMask = cache.Rows[bodyRow].TextureMask, ActiveResourceRow100 = bodyRow, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, ResourceMagicId = null, ResourceRenzokukenTargetBank = null };
        }
        else
        {
            bodyRow = model.TextureResourceIndex ?? throw new NotSupportedException("Native linked resource completion requires its actual primary resource-row pointer.");
            if (task.Callback == 0x507BF0)
            {
                NativeBattleModelTextureCacheLoad load = cache.LoadLinkedWeapon(task.ModelId, task.PhysicalIndex, pack);
                cache = load.Cache;
                linkedRow = load.ResourceIndex;
            }
            model = model with { LinkedTextureSlots = bind.TextureBinding, LinkedTextureResourceIndex = linkedRow };
        }
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(bind, cpu.Cpu, cpu.Rows, bodyRow, linkedRow);
        cpu = cpu with { Cpu = completed.Cpu, Rows = completed.Rows };
        if (completed.AnimationCopy is { } animation)
        {
            int header = task.PhysicalIndex * 32 + animation.HeaderPointerField;
            cpu = cpu with { AnimationFrames = cpu.AnimationFrames.Write(animation.DestinationOffset, animation.Bytes.AsSpan()), AnimationPointers = cpu.AnimationPointers.SetItem(header, animation.Bytes.IsEmpty ? null : animation.DestinationOffset) };
        }
        if (completed.ScratchCandidate is int candidate)
        {
            ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native507010 requires its actual508260 scratch maximum WORD.");
            if (maximum < candidate)
            {
                cpu = cpu with { ScratchMaximum = unchecked((ushort)candidate) };
            }
        }
        NativeModelCpuResources = cpu;
        NativeModelTextureCache = cache;
        graphics = graphics with { Models = graphics.Models.SetItem(task.PhysicalIndex, model) };
    }
}
