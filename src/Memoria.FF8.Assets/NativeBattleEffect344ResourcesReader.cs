using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect344ResourcesReader
{
    public static NativeBattleEffect344Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleEffect344Resources resources = new(new("effect344-music", memory.Read(MusicAddress, MusicLength).ToImmutableArray()), new("effect344-camera", memory.Read(CameraAddress, CameraLength).ToImmutableArray()));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect344Resources? nativeEffect344Resources;

    public NativeBattleEffect344Resources ReadNativeEffect344Resources() => nativeEffect344Resources ??= NativeBattleEffect344ResourcesReader.Read(ReadNativeExecutable());
}
