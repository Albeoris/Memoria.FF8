using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect69ResourcesReader
{
    public static NativeBattleEffect69Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> vibration = Bytes(VibrationAddress, VibrationLength);
        NativeBattleEffect69Resources result = new(new(ProgrammeAddress, Bytes(ProgrammeAddress, ProgrammeLength)), Bytes(SelectorsAddress, 9), new("FF8_EN:14D0858", Bytes(CameraAddress, CameraLength)), new(memory.SourceHash, [BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan().Slice(4))], vibration), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), new(0, SpriteAddress, 0, 0, Bytes(SpriteAddress, SpriteLength)));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect69Resources? nativeEffect69Resources;

    public NativeBattleEffect69Resources ReadNativeEffect69Resources()
    {
        if (nativeEffect69Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            NativeExecutableMemory memory = new(executable);
            uint namePointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + 37 * 4, 4));
            byte[] nameBytes = memory.Read(namePointer, 128);
            int length = nameBytes.AsSpan().IndexOf((byte)0);
            if (length < 1 || Encoding.ASCII.GetString(nameBytes.AsSpan(0, length)) != "A0STG037.X")
            {
                throw new InvalidDataException("Effect69 stage input disagrees with its original file directory.");
            }
            using IFlArchive battle = OpenArchive("battle");
            NativeBattleEffectFileAsset file = new(37, "A0STG037.X", Read(battle, "A0STG037.X").ToImmutableArray());
            nativeEffect69Resources = NativeBattleEffect69ResourcesReader.Read(executable) with
            {
                StageFile37 = file,
                DecodeReloadStage = bytes => BattleResourceReader.ReadStage(37, bytes).NativeSource ?? throw new InvalidDataException("Effect69 stage reload requires its decoded current stage source.")
            };
            nativeEffect69Resources.Validate();
        }
        return nativeEffect69Resources;
    }
}
