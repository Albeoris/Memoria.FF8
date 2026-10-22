using Memoria.FF8.Runtime;
using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private readonly Dictionary<(int Track, int Camera), NativeWorldScriptedPathDefinition> scriptedPaths = [];
    private readonly Dictionary<byte, WorldStaticModel> scriptedStaticModels = [];

    /// <summary>Reads passive original mode1/2 resources; this does not accept or activate a world packet.</summary>
    public NativeWorldScriptedPathDefinition ReadScriptedPath(int trackIndex, int? selectedCameraList = null)
    {
        int cameraList = selectedCameraList ?? trackIndex;
        if (!scriptedPaths.TryGetValue((trackIndex, cameraList), out NativeWorldScriptedPathDefinition? definition))
        {
            byte[] rail = Read("rail.obj");
            definition = NativeWorldScriptedPathReader.Read(rail, wmset.AsSpan(sections[17], sections[18] - sections[17]), NativeWorldMemory, trackIndex, cameraList);
            scriptedPaths.Add((trackIndex, cameraList), definition);
        }
        return definition;
    }

    public WorldStaticModel ReadScriptedStaticModel(byte shape)
    {
        if (shape < 64)
        {
            throw new NotSupportedException("Scripted world static shapes use the original shape-minus64 table.");
        }
        if (!scriptedStaticModels.TryGetValue(shape, out WorldStaticModel? model))
        {
            int index = shape - 64;
            int modelRelative = SectionPointer(15, index);
            int textureRelative = SectionPointer(41, index);
            int modelStart = sections[15] + modelRelative;
            int textureStart = sections[41] + textureRelative;
            var texture = WorldAssetReader.ReadTim(wmset, textureStart);
            if (texture.End > sections[42])
            {
                throw new InvalidDataException("Scripted static texture leaves its original WMSET section.");
            }
            model = WorldStaticModelReader.Read(shape, wmset.AsSpan(modelStart, sections[16] - modelStart), texture.Image);
            model = model with { Textures = textureScope.RegisterList(new Memoria.FF8.Resources.ResourceKey("world/wmsetus.obj"), model.Textures, "classic-model", $"static/{shape}/palette") };
            scriptedStaticModels.Add(shape, model);
        }
        return modelOverrides?.Apply($"world/static/{shape}", model) ?? model;
    }

    private int SectionPointer(int section, int index)
    {
        int count = WorldAssetReader.InnerPointers(wmset, sections[section]).Length;
        if (index >= count)
        {
            throw new InvalidDataException("Scripted static shape has no original resource table entry.");
        }
        int relative = BinaryPrimitives.ReadInt32LittleEndian(wmset.AsSpan(sections[section] + index * 4));
        if (relative <= 0 || relative > sections[section + 1] - sections[section] - 8)
        {
            throw new InvalidDataException("Scripted static resource pointer leaves its original WMSET section.");
        }
        return relative;
    }
}
