using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218Resources(NativeBattleEffectHeapAllocation InitialTexture, ImmutableArray<NativeBattleEffectFileAsset> Files, NativeBattleEffectOwnedModelResource StreamedModel, NativeBattleEffectOwnedModelResource EmbeddedModel, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableArray<uint> FragmentMeshes, ImmutableArray<byte> RayOrigins, ImmutableArray<byte> AlignmentVector, ImmutableArray<byte> UpVector, ImmutableArray<byte> FragmentOrigins, ImmutableArray<byte> CaptureClearQuad, NativeBattleEffectHeapAllocation Camera, NativeVibrationBank Vibration, uint InitialStream, ImmutableArray<uint> Sounds)
{
    public void Validate()
    {
        if (InitialTexture.Data.IsDefault || InitialTexture.Data.Length != 166432 || Files.IsDefault || Files.Length != 5 || Meshes.Count != 35 || Sprites.Count != 3 || FragmentMeshes.IsDefault || FragmentMeshes.Length != 24 || RayOrigins.IsDefault || RayOrigins.Length != 64 || AlignmentVector.IsDefault || AlignmentVector.Length != 16 || UpVector.IsDefault || UpVector.Length != 16 || FragmentOrigins.IsDefault || FragmentOrigins.Length != 192 || Camera.Data.IsDefault || Camera.Data.Length != 354 || Sounds.IsDefault || Sounds.Length != 3)
        {
            throw new InvalidDataException("Effect218 requires its complete selected original resource families.");
        }
        int[] lengths = [104480, 68356, 16960, 98880, 4];
        for (int index = 0; index < Files.Length; index++)
        {
            if (Files[index].FileId != 705 + index || Files[index].Data.IsDefault || Files[index].Data.Length != lengths[index])
            {
                throw new InvalidDataException("Effect218 streamed resource has lost its selected file or original extent.");
            }
        }
        RequireModel(StreamedModel, 0x20DFAB8, 0x10B04, 0xD18, 0xAE9C, 69);
        RequireModel(EmbeddedModel, 0xF1DD54, 0xBCC, 0x88, 0x73C, 2);
        if (CaptureClearQuad.IsDefault || CaptureClearQuad.Length != 24)
        {
            throw new InvalidDataException("Effect218 capture requires its complete original clear quad.");
        }
        Vibration.Validate();
        foreach (uint address in FragmentMeshes)
        {
            _ = Mesh(address);
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? mesh) ? mesh : throw new NotSupportedException("Effect218 selected an unsupported original mesh address.");

    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? sprite) ? sprite : throw new NotSupportedException("Effect218 selected an unsupported original sprite address.");

    private static void RequireModel(NativeBattleEffectOwnedModelResource resource, uint address, int length, int geometry, int animations, int bones)
    {
        if (resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != length || resource.Section(0) != 0x18 || resource.Section(1) != geometry || resource.Section(2) != animations || resource.Section(3) != length || resource.Section(4) != length || resource.PoseSource.BoneCount != bones)
        {
            throw new InvalidDataException("Effect218 cinematic model has lost its original section owners.");
        }
        resource.PoseSource.Validate();
    }
}
