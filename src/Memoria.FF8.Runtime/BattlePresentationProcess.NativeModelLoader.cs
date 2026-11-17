using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    public NativeBattleModelLoader? NativeModelLoader { get; private set; }
    public NativeBattleModelCpuResources? NativeModelCpuResources { get; private set; }
    private NativeBattleFileStaging? nativeFileStaging;
    public NativeBattleFileStaging? NativeFileStaging
    {
        get => nativeFileStaging;
        private set
        {
            if (NativeBattleTaskOperation.IsActive && !NativeBattleTaskCallerOperation.IsActive)
            {
                NativeBattleTaskOperation.AcceptPublishedStaging(value);
            }
            nativeFileStaging = value;
        }
    }

    private void QueueNativeModelResource507080(NativeBattleGraphicsState graphics, ushort request, BattleModelOwner owner, uint argument = 0)
    {
        NativeBattleModelSlot? originalSlot = graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out byte operand) ? NativeBattleModelSlot.Decode(operand) : null;
        bool actorMaterial = NativeModelTextureCache?.PresentationActors is not null;
        if (actorMaterial)
        {
            _ = RequireHostedModelLoadOwner(graphics, owner);
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        bool linked = request >= 0x1000;
        byte body = linked || request >= 16 ? (byte)0 : model.FileSelector129 ?? throw new NotSupportedException("Native5077B0 requires the observed model BYTE129 costume selector.");
        byte weapon = !linked || request == 0x1007 ? (byte)0 : model.FileSelector128 ?? throw new NotSupportedException("Native507BF0/507E20 requires the observed model BYTE128 kernel weapon selector.");
        NativeBattleModelLoader queued = NativeBattleModelLoader.Queue507080(request, owner, originalSlot, argument, weapon, body);
        if (actorMaterial && queued.Task is { } hostedTask)
        {
            if (hostedTask.Callback is 0x507BF0 or 0x507E20)
            {
                int alias = model.TextureResourceIndex ?? throw new InvalidDataException("Hosted linked loading requires its bound body alias.");
                RequireHostedModelLoadAlias(NativeModelCpuResources!, NativeModelTextureCache!, alias, owner, 1, hostedTask.ModelId);
            }
            if (hostedTask.Callback != 0x507E20)
            {
                _ = NativeModelTextureCache!.SelectHostedLoadAlias(hostedTask, owner);
            }
        }
        if (!linked && request < 16)
        {
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Complete party load planning requires its current CPU resource owner.");
            byte linkedSelector = request == 7 ? (byte)0 : model.FileSelector128 ?? throw new NotSupportedException("Complete party load planning requires its selected BYTE128 weapon resource.");
            BattlePartyModelOwner party = owner as BattlePartyModelOwner ?? throw new InvalidDataException("Party loading requires its party model owner.");
            cpu = cpu.BindPartyOwner(party, originalSlot);
            NativeBattlePartyLoadPlan plan = NativeBattlePartyLoadPlan.Create(checked((byte)request), party, originalSlot, body, linkedSelector, cpu.Cpu.EffectiveLayout, NativeModelResourceSources());
            if (plan.RequiresResourceStorage)
            {
                NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, party, graphics);
            }
            plan = plan with { PreferResourceStorage = NativeBattleModelResourceCopies.CanUseResourceStorage(cpu, party, graphics) };
            queued = queued with { PartyPlan = plan };
            NativeModelCpuResources = cpu;
        }
        else if (linked && queued.Task is { } task && NativeModelLoader is { Task: null, PartyPlan: { } plan } && plan.ModelId == task.ModelId && plan.Owner == owner)
        {
            _ = plan.Part(task);
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Linked party planning requires its current body owner.");
            int row = model.TextureResourceIndex ?? throw new NotSupportedException("Linked party planning requires its completed body resource row.");
            NativeBattleModelCpuReference tail = cpu.Rows[row].Require(8);
            if (tail.Bank != cpu.Cpu.PartyBankIndex(plan.Owner) || tail.Offset != plan.Body.CopiedLength || tail.EffectiveLayout != plan.Layout)
            {
                throw new InvalidDataException("The linked request no longer belongs to its planned body publication.");
            }
            queued = queued with { PartyPlan = plan };
        }
        NativeModelLoader = queued;
    }

    private int PollNativeModelResource507070(ref NativeBattleGraphicsState graphics)
    {
        NativeBattleModelLoader loader = NativeModelLoader ?? throw new NotSupportedException("Native507070 requires its actually initialized singleton load list.");
        if (loader.Task is { } pending)
        {
            NativeBattleModelSlot? currentSlot = graphics.NativeBindings.TryEncodeExactActor(pending.Owner, graphics.ActorModels, out byte operand) ? NativeBattleModelSlot.Decode(operand) : null;
            if (!graphics.ActorModels.Contains(pending.Owner) || currentSlot != pending.OriginalSlot)
            {
                throw new InvalidDataException("Model loading requires the same bound owner and original alias throughout its task.");
            }
        }
        (string ActorId, NativeBattlePresentationTextureBank Bank)? material = null;
        if (NativeModelTextureCache?.PresentationActors is not null && loader.Task is { } task)
        {
            material = RequireHostedModelLoadOwner(graphics, task.Owner);
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(task.Owner);
            if (task.Callback is 0x507BF0 or 0x507E20)
            {
                int bodyAlias = actor.TextureResourceIndex ?? throw new InvalidDataException("Hosted linked loading requires its bound body alias.");
                RequireHostedModelLoadAlias(NativeModelCpuResources!, NativeModelTextureCache!, bodyAlias, task.Owner, 1, task.ModelId);
            }
            if (task.Callback == 0x507120 && task.EnemyResourceIndex is int enemyAlias)
            {
                RequireHostedModelLoadAlias(NativeModelCpuResources!, NativeModelTextureCache!, enemyAlias, task.Owner, 3, task.ModelId);
            }
        }
        if (TryPollNativeEnemyModel(ref graphics, loader, out int enemyRemaining))
        {
            return enemyRemaining;
        }
        if (loader.PartyPlan is { UseResourceStorage: true } plan && loader.Task is not null)
        {
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Planned party loading requires its retained CPU owner.");
            if (cpu.Cpu.EffectiveLayout != plan.Layout)
            {
                throw new InvalidDataException("The planned party load no longer belongs to its selected CPU layout.");
            }
            NativeBattleModelResourceCopies.RequirePlannedTransition(cpu, plan.Owner, graphics);
        }
        ImmutableArray<NativeBattleModelDatSource> sources = NativeModelResourceSources();
        if (NativeFileStaging is null && loader.Task is { Phase: > 0 })
        {
            throw new NotSupportedException("Native model callback requires its retained shared199B018 staging bytes; migrate an older active load explicitly.");
        }
        NativeBattleModelLoadStep step = loader.Poll507070(NativeEffectIoResult ?? throw new NotSupportedException("Native507070 requires its retained signed1D999C8 IO result."), sources, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), material is not null ? HostedEnemyBlockedTextureSlots : NativeModelTextureCache?.BlockedSlots ?? 63, ownedEnemySource: NativeModelCpuResources?.EnemyResources is not null);
        NativeBattleModelCpuResources? hostedPalettes = null;
        NativeBattleEffectTextureState? hostedTextures = null;
        if (material is { } owner && step.TextureUpload is { } hostedUpload)
        {
            NativeBattleModelCpuResources cpu = NativeModelCpuResources!;
            NativeBattleEffectTextureState textures = NativeEffectMemory!.Textures!;
            hostedPalettes = cpu.SavePalettes507400(hostedUpload, loader.Task!.Owner);
            hostedTextures = PublishNativeModelTextures(textures, owner, NativeBattleModelTextures.Enqueue507400(owner.Bank.Textures, hostedUpload));
        }
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
            NativeModelCpuResources = hostedPalettes ?? cpu.SavePalettes507400(upload);
            NativeEffectMemory = memory with { Textures = hostedTextures ?? NativeBattleModelTextures.Enqueue507400(textures, upload) };
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
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(task.Owner);
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native resource completion requires its retained CPU bank/row aliases.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native resource completion requires its eleven physical texture rows.");
        NativeBattleEffectHeapAllocation pack = bind.OriginalTexturePack ?? throw new InvalidDataException("Native resource completion has no pre-relocation TIM pack.");
        int bodyRow;
        int? linkedRow = null;
        if (task.Callback is 0x5077B0 or 0x5079B0)
        {
            NativeBattleModelTextureCacheLoad load = cache.LoadPlayer(task.ModelId, task.OriginalSlot?.Offset ?? 0, pack, cache.PresentationActors is null ? null : (BattlePartyModelOwner)task.Owner);
            cache = load.Cache;
            bodyRow = load.ResourceIndex;
            RequireHostedModelLoadAlias(cpu, cache, bodyRow, task.Owner, 1, task.ModelId);
            RequireHostedModelLoadBinding(cache, bind, load.Binding);
            model = model with { BodyTextureSlots = bind.TextureBinding, TextureResourceIndex = bodyRow, TextureAllocationMask = cache.Rows[bodyRow].TextureMask, ActiveResourceRow100 = bodyRow, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, ResourceMagicId = null, ResourceRenzokukenTargetBank = null };
        }
        else
        {
            bodyRow = model.TextureResourceIndex ?? throw new NotSupportedException("Native linked resource completion requires its actual primary resource-row pointer.");
            RequireHostedModelLoadAlias(cpu, cache, bodyRow, task.Owner, 1, task.ModelId);
            if (task.Callback == 0x507BF0)
            {
                NativeBattleModelTextureCacheLoad load = cache.LoadLinkedWeapon(task.ModelId, task.OriginalSlot?.Offset ?? 0, pack, cache.PresentationActors is null ? null : (BattlePartyModelOwner)task.Owner);
                cache = load.Cache;
                linkedRow = load.ResourceIndex;
                RequireHostedModelLoadAlias(cpu, cache, load.ResourceIndex, task.Owner, 2, task.ModelId);
                RequireHostedModelLoadBinding(cache, bind, load.Binding);
            }
            model = model with { LinkedTextureSlots = bind.TextureBinding, LinkedTextureResourceIndex = linkedRow };
        }
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(bind, cpu.Cpu, cpu.Rows, bodyRow, linkedRow);
        NativeBattleModelResourceCopies.RequireInactiveTransition(cpu, completed, graphics);
        cpu = cpu with { Cpu = completed.Cpu, Rows = completed.Rows };
        if (completed.AnimationCopy is { } animation)
        {
            cpu = cpu.BindAnimation((BattlePartyModelOwner)task.Owner, task.OriginalSlot, animation);
        }
        if (completed.ScratchCandidate is int candidate)
        {
            ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native507010 requires its actual508260 scratch maximum WORD.");
            ushort updated = NativeBattleModelResourceCopies.UpdateScratchMaximum(maximum, candidate);
            if (maximum != updated)
            {
                cpu = cpu with { ScratchMaximum = updated };
            }
        }
        NativeModelCpuResources = cpu;
        NativeModelTextureCache = cache;
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(task.Owner, model) };
    }

    private static void RequireHostedModelLoadBinding(NativeBattleModelTextureCache cache, NativeBattleModelResourceBind bind, NativeBattleModelTextureBinding binding)
    {
        if (cache.PresentationRoster is not null && !binding.PaletteRows.SequenceEqual(bind.TextureBinding.PaletteRows))
        {
            throw new InvalidDataException("Hosted resource completion disagrees with the uploaded material binding.");
        }
    }
}
