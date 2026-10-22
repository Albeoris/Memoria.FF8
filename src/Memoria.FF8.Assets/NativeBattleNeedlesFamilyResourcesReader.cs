using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleNeedlesFamilyResourcesReader
{
    public static NativeBattleNeedlesFamilyResources Read(byte[] executable, int effect)
    {
        NativeBattleNeedlesFamilyProfile p = NativeBattleNeedlesFamilyProfile.ForEffect(effect);
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(p.TextureNameAddress, 11)) != "mag176.tim\0")
        {
            throw new InvalidDataException("Needles requires its original initializer texture name.");
        }
        ImmutableArray<int> depths = Enumerable.Range(0, 7).Select(index => BinaryPrimitives.ReadInt32LittleEndian(memory.Read(p.SparkDepthOffsetsAddress + (uint)(index * 4), 4))).ToImmutableArray();
        NativeBattleNeedlesFamilyResources resources = new((ushort)effect, new($"FF8_EN:{p.CameraAddress:X}", memory.Read(p.CameraAddress, p.CameraLength).ToImmutableArray()), new(0, p.NeedleVerticesAddress, 0, 0, memory.Read(p.NeedleVerticesAddress, 32).ToImmutableArray()), new(0, p.NeedleFacesAddress, 0, 0, memory.Read(p.NeedleFacesAddress, 80).ToImmutableArray()), new(0, p.SparkSpriteAddress, 0, 0, memory.Read(p.SparkSpriteAddress, 40).ToImmutableArray()), depths, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(p.SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<int, NativeBattleNeedlesFamilyResources> nativeNeedlesFamilyResources = new();
    public NativeBattleNeedlesFamilyResources ReadNativeNeedlesFamilyResources(int effect)
    {
        _ = NativeBattleNeedlesFamilyProfile.ForEffect(effect);
        if (!nativeNeedlesFamilyResources.TryGetValue(effect, out NativeBattleNeedlesFamilyResources? value))
        {
            value = NativeBattleNeedlesFamilyResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), effect);
            nativeNeedlesFamilyResources.Add(effect, value);
        }
        return value;
    }
}
