using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect316ResourcesReader
{
    public static NativeBattleEffect316Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleEffect316Resources result = new(mainTim, Bytes(EmbeddedTimAddress, 66080), new("effect316-camera", Bytes(CameraAddress, 256)), new("effect316-vibration", Bytes(VibrationAddress, 270)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), new(0, ProjectedQuadsAddress, 0, 0, Bytes(ProjectedQuadsAddress, NativeBattleEffect316Resources.ProjectedQuadsLength)));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect316Resources? nativeEffect316Resources;

    public NativeBattleEffect316Resources ReadNativeEffect316Resources()
    {
        if (nativeEffect316Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect316Resources = NativeBattleEffect316ResourcesReader.Read(executable, new("mag315.tim", Read(magic, "mag315.tim").ToImmutableArray()));
        }
        return nativeEffect316Resources;
    }
}
