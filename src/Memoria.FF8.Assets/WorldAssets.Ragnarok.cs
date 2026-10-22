using System.Buffers.Binary;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeRagnarokCameraDefaults(string ExecutableHash, short OffsetX, short OffsetY, short OffsetZ)
{
    public void Validate()
    {
        if (ExecutableHash is not (NativeFieldRuntimeProfile.FF8EnExecutableHash or NativeFieldRuntimeProfile.RemasteredEnImageHash) || OffsetX != 0 || OffsetZ >= 0)
        {
            throw new InvalidDataException("Ragnarok camera requires authenticated558C20 entity50 offsets.");
        }
    }
}

public sealed partial class WorldAssets
{
    private FieldModel? ragnarokModel;

    public FieldModel GetRagnarokModel()
    {
        if (ragnarokModel is null)
        {
            //546E10 maps entity50 to original animated chara.one shape1.
            FieldModel model = WorldAssetReader.ReadWorldCharacter(Read("chara.one"), 1);
            ragnarokModel = model with { Textures = textureScope.RegisterList(new ResourceKey("world/chara.one"), model.Textures, "classic-model", "model/1") };
        }
        return modelOverrides?.Apply("world/1", ragnarokModel) ?? ragnarokModel;
    }

    public NativeRagnarokCameraDefaults ReadRagnarokCameraDefaults()
    {
        IGameNativeMemorySource memory = NativeWorldMemory;
        ReadOnlySpan<byte> words = memory.Read(0xC76D38, 6);
        var result = new NativeRagnarokCameraDefaults(memory.SourceHash, BinaryPrimitives.ReadInt16LittleEndian(words), BinaryPrimitives.ReadInt16LittleEndian(words[2..]), BinaryPrimitives.ReadInt16LittleEndian(words[4..]));
        result.Validate();
        return result;
    }
}
