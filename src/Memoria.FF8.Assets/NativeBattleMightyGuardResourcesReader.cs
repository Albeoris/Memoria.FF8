using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMightyGuardResourcesReader
{
    public static NativeBattleMightyGuardResources Read(byte[] executable, ushort effect)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleMightyGuardProfile profile = NativeBattleMightyGuardProfile.ForEffect(effect);
        byte[] name = memory.Read(profile.FilenameAddress, 32);
        int terminator = Array.IndexOf(name, (byte)0);
        if (terminator < 0 || System.Text.Encoding.ASCII.GetString(name, 0, terminator) != profile.TextureName)
        {
            throw new InvalidDataException("Mighty Guard requires its original shared texture filename.");
        }
        NativeBattleMightyGuardResources result = new(effect, new($"FF8_EN:{profile.CameraAddress:X}", memory.Read(profile.CameraAddress, profile.CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(profile.SoundAddress, 4)), new(memory.Read(profile.SourceAddress, profile.SourceLength).ToImmutableArray()) { ResourceAddress = profile.SourceAddress }, profile.CasterDatAddress is { } casterDat ? memory.Read(casterDat, profile.CasterDatLength).ToImmutableArray() : null);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleMightyGuardResources> nativeMightyGuardResources = [];

    public NativeBattleMightyGuardResources ReadNativeMightyGuardResources(ushort effect)
    {
        if (!nativeMightyGuardResources.TryGetValue(effect, out NativeBattleMightyGuardResources? result))
        {
            result = NativeBattleMightyGuardResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), effect);
            nativeMightyGuardResources.Add(effect, result);
        }
        return result;
    }
}
