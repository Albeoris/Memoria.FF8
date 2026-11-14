namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState InitializeEffectPresentationSourceResources(NativeBattleGraphicsState graphics, NativeBattleGraphicsResource resource, bool preserveSelectedProgram = false)
    {
        if (graphics.PresentationRoster is not null)
        {
            if (resource.ModelLoadSources is not { IsEmpty: false })
            {
                return graphics;
            }
            graphics = LoadHostedPresentationModel(graphics, presentationSourceIndex, resource);
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
        NativeBattleGraphicsModel source = graphics.Models[presentationSourceIndex];
        byte model = source.ModelId4 ?? throw new NotSupportedException("Native presentation resource loading requires its source model identity.");
        if (model >= 16)
        {
            throw new NotSupportedException("Native presentation source loading requires an original party body and linked resource catalog.");
        }
        NativeBattleGraphicsModel loading = source with { Flags0 = (ushort)(source.Flags0 & ~2), FileSelector128 = source.FileSelector128 ?? resource.LinkedFileSelector128, FileSelector129 = source.FileSelector129 ?? resource.BodyFileSelector129, ActiveResourceRow100 = null, OriginalResourceRow104 = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, TextureResourceIndex = null, LinkedTextureResourceIndex = null, ResourceMagicId = null, ResourceRenzokukenTargetBank = null, ActiveAnimationDirectory = null, ActiveAnimationBank = null };
        graphics = graphics with { Models = graphics.Models.SetItem(presentationSourceIndex, loading) };
        Load(model);
        Load(checked((ushort)(0x1000 + model)));
        loading = graphics.Models[presentationSourceIndex];
        loading = loading with { Flags0 = source.Flags0, OriginalResourceRow104 = loading.ActiveResourceRow100 };
        if (preserveSelectedProgram && source.ResourceMagicId is not null)
        {
            loading = loading with { ResourceMagicId = source.ResourceMagicId, ResourceRenzokukenTargetBank = source.ResourceRenzokukenTargetBank, ActiveAnimationDirectory = source.ActiveAnimationDirectory, ActiveAnimationBank = source.ActiveAnimationBank };
        }
        return graphics with { Models = graphics.Models.SetItem(presentationSourceIndex, loading) };

        void Load(ushort request)
        {
            QueueNativeModelResource507080(graphics, request, checked((byte)presentationSourceIndex));
            while (NativeModelLoader!.Task is not null)
            {
                PumpNativeFileIo482590();
                PollNativeModelResource507070(ref graphics);
            }
        }
    }

    private NativeBattleGraphicsState InitializeHostedPresentationModelResources(NativeBattleGraphicsState graphics, bool loadStage)
    {
        NativeBattleGraphicsRoster roster = graphics.PresentationRoster ?? throw new InvalidDataException("Hosted model loading requires its explicit presentation roster.");
        InitializeHostedPresentationModelOwners(roster);
        if (loadStage)
        {
            LoadNativeStage500EA0();
        }
        bool loaded = loadStage;
        foreach (int index in Enumerable.Range(0, roster.ActorCount).Where(index => roster.Rows[index] is not null).OrderBy(index => graphics.Models[index].ModelId4 == 143))
        {
            if (ReadHostedPresentationModelResource(index) is { ModelLoadSources.IsEmpty: false } resource)
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

    private NativeBattleGraphicsState LoadHostedPresentationModel(NativeBattleGraphicsState graphics, int index, NativeBattleGraphicsResource resource)
    {
        NativeBattleGraphicsModel original = graphics.Models[index];
        byte model = original.ModelId4 ?? throw new NotSupportedException("Hosted model resource loading requires its actual model byte.");
        NativeBattleGraphicsModel loading = original with { Flags0 = (ushort)(original.Flags0 & ~2), FileSelector128 = original.FileSelector128 ?? resource.LinkedFileSelector128, FileSelector129 = original.FileSelector129 ?? resource.BodyFileSelector129, ActiveResourceRow100 = null, OriginalResourceRow104 = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, TextureResourceIndex = null, LinkedTextureResourceIndex = null, ResourceMagicId = null, ResourceRenzokukenTargetBank = null, ActiveAnimationDirectory = null, ActiveAnimationBank = null };
        graphics = graphics with { Models = graphics.Models.SetItem(index, loading) };
        Load(model);
        if (model < 16)
        {
            Load(checked((ushort)(0x1000 + model)));
        }
        loading = graphics.Models[index];
        return graphics with { Models = graphics.Models.SetItem(index, loading with { Flags0 = original.Flags0, OriginalResourceRow104 = loading.ActiveResourceRow100 }) };

        void Load(ushort request)
        {
            QueueNativeModelResource507080(graphics, request, checked((byte)index));
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
