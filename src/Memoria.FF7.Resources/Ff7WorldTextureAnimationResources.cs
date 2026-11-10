using System.Buffers.Binary;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldTextureCycle(int TextureIndex, int FrameCount, int Divisor);

public sealed class Ff7WorldTextureAnimationResources
{
    private readonly IReadOnlyDictionary<int, Ff7WorldTextureCycle> cycles;

    public Ff7WorldTextureAnimationResources(Ff7Installation installation, Ff7WorldResources resources, IReadOnlyList<Ff7WorldTextureDescriptor> descriptors)
    {
        byte[] source = resources.ReadRequired("wm.ta");
        if (source.Length < 4)
        {
            throw new InvalidDataException("FFVII world texture animation table has no header.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source);
        int[] animatedIds = Ff7PcExecutableData.LoadWorldTextureAnimationIds(installation);
        if (count < 0 || count != animatedIds.Length || source.Length < 4 + count * 8)
        {
            throw new InvalidDataException("FFVII world texture animation table does not match its selected texture profile.");
        }
        Dictionary<int, Ff7WorldTextureCycle> result = new();
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> row = source.AsSpan(4 + index * 8, 8);
            int frames = row[6];
            int divisor = row[7];
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(row);
            int size = BinaryPrimitives.ReadUInt16LittleEndian(row[4..]);
            if (divisor == 0 || frames != descriptors[animatedIds[index]].AnimationFrames!.Count || offset > source.Length || (long)offset + (long)size * frames > source.Length)
            {
                throw new InvalidDataException("FFVII world texture animation cycle is invalid.");
            }
            result.Add(animatedIds[index], new(animatedIds[index], frames, divisor));
        }
        cycles = result;
    }

    public IReadOnlyCollection<Ff7WorldTextureCycle> Cycles => cycles.Values.ToArray();

    public int Frame(int textureIndex, uint nativeFrameCounter)
    {
        return cycles.TryGetValue(textureIndex, out Ff7WorldTextureCycle? cycle) ? (int)(nativeFrameCounter / cycle.Divisor % cycle.FrameCount) : 0;
    }
}
