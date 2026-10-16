using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStageSourceReader
{
    // Original50E510 initializer assignments to WORD1D96D9C, in physical stage order0..162.
    
    private static readonly short[] RotationSpeeds =
    [
        4, 4, 4, 4, 0, 0, 4, 4, 0, 0, 4, 4, 0, 0, 0, 0,
        0, 4, 4, 0, 0, 0, 0, 0, 0, 4, 0, 4, 4, 0, 16, 16,
        0, 0, 0, 0, 0, 8, 2, 0, 0, 8, -4, -4, 0, 0, 0, 4,
        0, 8, 0, 4, 4, 0, 4, 0, 4, -4, 8, -4, -4, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 4, 4, 0, 0,
        4, 4, 0, 0, 32, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0, 8,
        0, 0, 0, 0, 0, 4, 4, 4, 4, 0, 0, 4, 4, 8, -4, 4,
        4, 4, 4, 8, 8, 4, -4, -4, 8, 0, 0, 0, 0, 0, 0, 0,
        4, 4, 4, 4, 4, 4, -4, 0, 0, 0, 0, 8, 8, 0, 8, -4,
        0, 0, 8, 0, 0, 0, 0, 0, 4, 4, 4, 4, 4, 0, 0, 8,
        0, 8, 8
    ];

    public static NativeBattleStageSource Read(int stageId, byte[] bytes, int header)
    {
        if ((uint)stageId >= RotationSpeeds.Length || header < 0 || header > bytes.Length - 32 || Word32(header) != 6)
        {
            throw new InvalidDataException("Native500EA0 requires its original stage initializer and six-section root.");
        }
        int[] sections = Enumerable.Range(0, 7).Select(index => checked(header + Word32(header + 4 + index * 4))).ToArray();
        if (sections[6] != bytes.Length || sections.Any(offset => offset < header + 32 || offset > bytes.Length) || !sections.SequenceEqual(sections.Order()))
        {
            throw new InvalidDataException("Native500EA0 stage sections exceed their original resource.");
        }
        ImmutableArray<NativeBattlePoseSource?>.Builder groups = ImmutableArray.CreateBuilder<NativeBattlePoseSource?>(4);
        for (int group = 0; group < 4; group++)
        {
            int start = sections[group];
            int end = sections[group + 1];
            if (start == end)
            {
                groups.Add(null);
                continue;
            }
            if (start > end - 16 || Word32(start) != 3)
            {
                throw new InvalidDataException("Native500EA0 stage group lacks its three-section directory.");
            }
            int skeleton = checked(start + Word32(start + 4));
            int geometry = checked(start + Word32(start + 8));
            int animations = checked(start + Word32(start + 12));
            if (skeleton < start + 16 || skeleton > geometry || geometry > animations || animations >= end)
            {
                throw new InvalidDataException("Native stage skeleton or animation leaves its original group.");
            }
            NativeBattlePoseSource source = new(bytes.AsSpan(skeleton, geometry - skeleton).ToArray().ToImmutableArray(), bytes.AsSpan(animations, end - animations).ToArray().ToImmutableArray());
            source.Validate();
            groups.Add(source);
        }
        return new(RotationSpeeds[stageId], groups.MoveToImmutable(), bytes.AsSpan(sections[4], sections[5] - sections[4]).ToArray().ToImmutableArray(), new($"a0stg{stageId:000}.x:TIM", bytes.AsSpan(sections[5], sections[6] - sections[5]).ToArray().ToImmutableArray())) { Callback = NativeBattleStageCallbackReader.Read(stageId), ModelColors = NativeBattleStageColorsReader.Read(stageId).Colors };

        int Word32(int offset)
        {
            if (offset < 0 || offset > bytes.Length - 4)
            {
                throw new InvalidDataException("Native stage DWORD leaves its original resource.");
            }
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
        }
    }
}
