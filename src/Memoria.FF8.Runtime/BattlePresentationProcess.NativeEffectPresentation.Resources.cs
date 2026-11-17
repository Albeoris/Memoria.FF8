namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState InitializeEffectPresentationSourceResources(NativeBattleGraphicsState graphics, NativeBattleGraphicsResource resource, bool preserveSelectedProgram = false)
    {
        if (NativeModelTextureCache?.PresentationActors is not null)
        {
            if (resource.ModelLoadSources is not { IsEmpty: false })
            {
                return graphics;
            }
            graphics = LoadHostedPresentationModel(graphics, RequirePresentationSourceOwner(), resource);
            DrainHostedPresentationModelResources();
            return graphics;
        }
        if (resource.ModelLoadSources is not { IsEmpty: false })
        {
            return graphics;
        }
        NativeModelCpuResources ??= NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = 0 };
        NativeModelTextureCache ??= NativeBattleModelTextureCache.Initialize508260(NativeBattleModelTextureCache.FromColdProcess());
        NativeEffectIoResult = 0;
        NativeBattleGraphicsModel source = graphics.ActorModels.Get(RequirePresentationSourceOwner());
        byte model = source.ModelId4 ?? throw new NotSupportedException("Native presentation resource loading requires its source model identity.");
        if (model >= 16)
        {
            throw new NotSupportedException("Native presentation source loading requires an original party body and linked resource catalog.");
        }
        NativeBattleGraphicsModel loading = source with { Flags0 = (ushort)(source.Flags0 & ~2), FileSelector128 = source.FileSelector128 ?? resource.LinkedFileSelector128, FileSelector129 = source.FileSelector129 ?? resource.BodyFileSelector129, ActiveResourceRow100 = null, OriginalResourceRow104 = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, TextureResourceIndex = null, LinkedTextureResourceIndex = null, ResourceMagicId = null, ResourceRenzokukenTargetBank = null, ActiveAnimationDirectory = null, ActiveAnimationBank = null };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(RequirePresentationSourceOwner(), loading) };
        Load(model);
        Load(checked((ushort)(0x1000 + model)));
        loading = graphics.ActorModels.Get(RequirePresentationSourceOwner());
        loading = loading with { Flags0 = source.Flags0, OriginalResourceRow104 = loading.ActiveResourceRow100 };
        if (preserveSelectedProgram && source.ResourceMagicId is not null)
        {
            loading = loading with { ResourceMagicId = source.ResourceMagicId, ResourceRenzokukenTargetBank = source.ResourceRenzokukenTargetBank, ActiveAnimationDirectory = source.ActiveAnimationDirectory, ActiveAnimationBank = source.ActiveAnimationBank };
        }
        return graphics with { ActorModels = graphics.ActorModels.Set(RequirePresentationSourceOwner(), loading) };

        void Load(ushort request)
        {
            QueueNativeModelResource507080(graphics, request, RequirePresentationSourceOwner());
            while (NativeModelLoader!.Task is not null)
            {
                PumpNativeFileIo482590();
                PollNativeModelResource507070(ref graphics);
            }
        }
    }

    private NativeBattleGraphicsState InitializeHostedPresentationModelResources(NativeBattleGraphicsState graphics, bool loadStage)
    {
        BattleModelRoster roster = nativeActionPackets.Roster;
        InitializeHostedPresentationModelOwners(roster);
        if (loadStage)
        {
            LoadNativeStage500EA0();
        }
        bool loaded = loadStage;
        foreach (BattleModelOwner index in graphics.ActorModels.Owners.OrderBy(index => graphics.ActorModels.Get(index).ModelId4 == 143))
        {
            if (ReadOriginalPresentationModelResource(index) is { ModelLoadSources.IsEmpty: false } resource)
            {
                graphics = LoadHostedPresentationModel(graphics, index, resource);
                loaded = true;
            }
        }
        if (loaded)
        {
            DrainHostedPresentationModelResources();
        }
        return graphics;
    }

    private NativeBattleGraphicsState LoadHostedPresentationModel(NativeBattleGraphicsState graphics, BattleModelOwner index, NativeBattleGraphicsResource resource)
    {
        NativeBattleGraphicsModel original = graphics.ActorModels.Get(index);
        byte model = original.ModelId4 ?? throw new NotSupportedException("Hosted model resource loading requires its actual model byte.");
        NativeBattleGraphicsModel loading = original with { Flags0 = (ushort)(original.Flags0 & ~2), FileSelector128 = original.FileSelector128 ?? resource.LinkedFileSelector128, FileSelector129 = original.FileSelector129 ?? resource.BodyFileSelector129, ActiveResourceRow100 = null, OriginalResourceRow104 = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, TextureResourceIndex = null, LinkedTextureResourceIndex = null, ResourceMagicId = null, ResourceRenzokukenTargetBank = null, ActiveAnimationDirectory = null, ActiveAnimationBank = null };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, loading) };
        Load(model);
        if (model < 16)
        {
            Load(checked((ushort)(0x1000 + model)));
        }
        loading = graphics.ActorModels.Get(index);
        return graphics with { ActorModels = graphics.ActorModels.Set(index, loading with { Flags0 = original.Flags0, OriginalResourceRow104 = loading.ActiveResourceRow100 }) };

        void Load(ushort request)
        {
            QueueNativeModelResource507080(graphics, request, index);
            while (NativeModelLoader!.Task is not null)
            {
                PumpNativeFileIo482590();
                PollNativeModelResource507070(ref graphics);
            }
        }
    }

    private void DrainHostedPresentationModelResources()
    {
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeEffectMemory = DrainNativeTextureUploads(memory, memory.Textures!);
    }
}
