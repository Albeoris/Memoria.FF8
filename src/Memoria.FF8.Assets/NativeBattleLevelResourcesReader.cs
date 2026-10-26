using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleLevelResourcesReader
{
    public static NativeBattleLevelResources Read(byte[] executable, int effect)
    {
        NativeBattleLevelProfile profile = NativeBattleLevelProfile.ForEffect(effect);
        NativeExecutableMemory memory = new(executable);
        return new(profile.Effect, new(profile.ProgrammeAddress, NativeBattleEffectStaticResourcesReader.Read(memory, profile.ProgrammeAddress, profile.ProgrammeBytes)), new($"FF8_EN:{profile.CameraAddress:X}", NativeBattleEffectStaticResourcesReader.Read(memory, profile.CameraAddress, 2252)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(profile.SoundAddress, 4)));
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<int, NativeBattleLevelResources> nativeLevelResources = [];

    public NativeBattleLevelResources ReadNativeLevelResources(int effect)
    {
        if (!nativeLevelResources.TryGetValue(effect, out NativeBattleLevelResources? result))
        {
            result = NativeBattleLevelResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), effect);
            nativeLevelResources.Add(effect, result);
        }
        return result;
    }
}
