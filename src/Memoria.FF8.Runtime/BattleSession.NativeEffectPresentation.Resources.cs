namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState InitializeEffectPresentationSourceResources(NativeBattleGraphicsState graphics, NativeBattleGraphicsResource resource)
    {
        if (resource.ModelLoadSources is not { IsEmpty: false })
        {
            return graphics;
        }
        NativeModelCpuResources ??= NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = 0 };
        NativeModelTextureCache ??= NativeBattleModelTextureCache.Initialize508260(NativeBattleModelTextureCache.FromColdProcess());
        NativeEffectIoResult = 0;
        NativeBattleGraphicsModel source = graphics.Models[0];
        byte model = source.ModelId4 ?? throw new NotSupportedException("Native presentation resource loading requires its source model identity.");
        if (model >= 16)
        {
            throw new NotSupportedException("Native presentation source loading requires an original party body and linked resource catalog.");
        }
        NativeBattleGraphicsModel loading = source with { Flags0 = (ushort)(source.Flags0 & ~2), FileSelector128 = source.FileSelector128 ?? resource.LinkedFileSelector128, FileSelector129 = source.FileSelector129 ?? resource.BodyFileSelector129, ActiveResourceRow100 = null, OriginalResourceRow104 = null, ActiveStagedResourceRow100 = null, LinkedStagedResourceRow = null, TextureResourceIndex = null, LinkedTextureResourceIndex = null, ResourceMagicId = null, ResourceRenzokukenTargetBank = null, ActiveAnimationDirectory = null, ActiveAnimationBank = null };
        graphics = graphics with { Models = graphics.Models.SetItem(0, loading) };
        Load(model);
        Load(checked((ushort)(0x1000 + model)));
        loading = graphics.Models[0];
        return graphics with { Models = graphics.Models.SetItem(0, loading with { Flags0 = source.Flags0, OriginalResourceRow104 = loading.ActiveResourceRow100 }) };

        void Load(ushort request)
        {
            QueueNativeModelResource507080(graphics, request, 0);
            while (NativeModelLoader!.Task is not null)
            {
                PumpNativeFileIo482590();
                PollNativeModelResource507070(ref graphics);
            }
        }
    }
}
