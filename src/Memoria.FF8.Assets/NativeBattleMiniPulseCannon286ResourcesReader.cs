using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleMiniPulseCannon286ResourcesReader
{
    public static NativeBattleMiniPulseCannon286Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag285.tim\0")
        {
            throw new InvalidDataException("Mini Pulse Cannon requires its original initializer texture identity.");
        }
        NativeBattleMiniPulseCannon286Resources resources = new(new(ProgrammeAAddress, memory.Read(ProgrammeAAddress, ProgrammeALength).ToImmutableArray()), new(ProgrammeBAddress, memory.Read(ProgrammeBAddress, ProgrammeBLength).ToImmutableArray()), new(ProgrammeCAddress, memory.Read(ProgrammeCAddress, ProgrammeCLength).ToImmutableArray()), new(ProgrammeDAddress, memory.Read(ProgrammeDAddress, ProgrammeDLength).ToImmutableArray()), new(0, SpriteAddress, 0, 0, memory.Read(SpriteAddress, SpriteLength).ToImmutableArray()), new("FF8_EN:10847B0", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), memory.Read(SourcePointAddress, 8).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMiniPulseCannon286Resources? nativeMiniPulseCannon286Resources;
    public NativeBattleMiniPulseCannon286Resources ReadNativeMiniPulseCannon286Resources() => nativeMiniPulseCannon286Resources ??= NativeBattleMiniPulseCannon286ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
