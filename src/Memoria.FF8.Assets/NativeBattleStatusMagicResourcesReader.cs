using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStatusMagicResourcesReader
{
    public static NativeBattleStatusMagicResources Read(byte[] executable, ushort effect)
    {
        return Read(new NativeExecutableMemory(executable), effect);
    }

    public static NativeBattleStatusMagicResources Read(IGameNativeMemorySource memory, ushort effect)
    {
        if (effect is not (120 or 122))
        {
            throw new NotSupportedException("Native status resources require original120 or122.");
        }
        uint camera = effect == 120 ? 0x127DA24u : 0x1271C68u;
        uint sound = effect == 120 ? 0x127DB4Cu : 0x1272690u;
        ImmutableArray<NativeBattleGfAnimationSource> layouts = effect == 120
            ? [ReadLayout(0x1277640, 0x2628, 0x140), ReadLayout(0x1279C68, 0x3DBC, 0x4F4)]
            : [ReadLayout(0x126E4C0, 0x37A8, 0x498)];
        NativeBattleEffectHeapAllocation animation = new($"FF8_EN:{camera:X}", memory.Read(camera, checked((int)(sound - camera))).ToImmutableArray());
        return new(effect, animation, layouts, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(sound, 4)));

        NativeBattleGfAnimationSource ReadLayout(uint address, int length, int workspaceBytes)
        {
            NativeBattleGfAnimationSource source = new(address, memory.Read(address, length).ToImmutableArray());
            int geometryCount = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan());
            int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
            if (geometryCount is < 1 or > 64 || table < 8 + geometryCount * 4 || table > source.Data.Length - 8)
            {
                throw new InvalidDataException("Native status resource requires its bounded inline models and animation table.");
            }
            for (int index = 0; index < geometryCount; index++)
            {
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + index * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(geometry + 4)));
                int shapes = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(geometry + 8)));
                int directory = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry));
                if (count is < 0 or > 4096 || shapes is < 1 or > 256 || directory != checked(12 + count * shapes * 8))
                {
                    throw new InvalidDataException("Native status model requires its complete original vertex shapes.");
                }
                int cursor = checked(geometry + directory);
                foreach (int stride in new int[] { 12, 12, 20, 24, 20, 24, 28, 36 })
                {
                    int faces = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(cursor));
                    if (faces is < 0 or > 4096)
                    {
                        throw new InvalidDataException("Native status model primitive count leaves the bounded source.");
                    }
                    cursor = checked(cursor + 4 + faces * stride);
                }
                if (cursor > table)
                {
                    throw new InvalidDataException("Native status geometry overlaps its animation directory.");
                }
            }
            _ = new NativeBattleGfAnimationWorkspace(ImmutableArray.CreateRange(new byte[workspaceBytes])).Initialize7016B0(source, workspaceBytes);
            return source;
        }
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleStatusMagicResources> nativeStatusMagicResources = [];

    public NativeBattleStatusMagicResources ReadNativeStatusMagicResources(ushort effect)
    {
        if (!nativeStatusMagicResources.TryGetValue(effect, out NativeBattleStatusMagicResources? resources))
        {
            resources = NativeBattleStatusMagicResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), effect);
            nativeStatusMagicResources.Add(effect, resources);
        }
        return resources;
    }
}
