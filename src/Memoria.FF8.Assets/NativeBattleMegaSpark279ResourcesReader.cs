using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleMegaSpark279ResourcesReader
{
    public static NativeBattleMegaSpark279Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag278.tim\0")
        {
            throw new InvalidDataException("Mega Spark requires its selected initializer texture identity.");
        }
        byte[] normalization = memory.Read(NormalizationAddress, NormalizationLength);
        if (!normalization.AsSpan().SequenceEqual(memory.Read(BodyNormalizationAddress, NormalizationLength)))
        {
            throw new InvalidDataException("Mega Spark requires matching effect and body normalization contents.");
        }
        NativeBattleMegaSpark279Resources result = new(Source(ProgrammeAAddress, ProgrammeALength), Source(ProgrammeBAddress, ProgrammeBLength), Source(ProgrammeCAddress, ProgrammeCLength), Source(ProgrammeDAddress, ProgrammeDLength), new(0, SpriteAddress, 0, 0, Bytes(SpriteAddress, SpriteLength)), new("FF8_EN:10B1A2C", Bytes(CameraAddress, CameraLength)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), Bytes(SourcePointsAddress, 24), Bytes(ExtraPointAddress, 8), Bytes(AttachmentTablesAddress, 576), Bytes(DistanceAddress, DistanceLength), normalization.ToImmutableArray(), Bytes(SineAddress, SineLength));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, Bytes(address, length));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMegaSpark279Resources? nativeMegaSpark279Resources;
    public NativeBattleMegaSpark279Resources ReadNativeMegaSpark279Resources() => nativeMegaSpark279Resources ??= NativeBattleMegaSpark279ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
