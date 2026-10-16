using System.Numerics;
using System.Collections.Immutable;
using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original stage camera directory and the settled endpoint of its standard introduction script.</summary>
internal static class BattleStageCameraReader
{
    // Original50E510 dispatch and each stage initializer's camera argument to500F70.
    // These are original resource offsets, not signatures or presentation tuning.
    
    private static readonly int[] CameraOffsets =
    [
        1496, 1496, 1496, 1496, 1492, 1492, 1496, 1496, 1560, 1492, 1496, 1496, 1492, 1492, 1492, 1492,
        1576, 1496, 1496, 1604, 1564, 1492, 1492, 1492, 1492, 1496, 1492, 1496, 1496, 1492, 2356, 2356,
        1492, 1492, 1492, 1492, 1492, 3264, 1496, 1492, 1492, 1496, 1496, 1496, 2468, 2468, 2468, 1496,
        1560, 1496, 1492, 1536, 1536, 1492, 1568, 1492, 1568, 1496, 1496, 1496, 1496, 1492, 1492, 1492,
        1492, 1492, 1492, 1492, 1492, 1492, 1492, 1492, 1492, 1492, 1496, 1492, 1496, 1496, 1492, 1560,
        1496, 1496, 1492, 1492, 1496, 1492, 1492, 1492, 1492, 1492, 1492, 1492, 2108, 1496, 1492, 1496,
        1492, 1492, 1492, 1492, 1492, 1496, 1496, 1496, 1496, 1492, 1492, 1536, 1536, 1496, 1496, 1496,
        1496, 1496, 1496, 1496, 1496, 1496, 1496, 1496, 1496, 1492, 1492, 1492, 1492, 1492, 1492, 1492,
        1496, 1496, 1496, 1496, 1496, 1496, 1496, 1492, 1532, 4060, 1492, 1496, 1496, 1492, 6204, 1496,
        1492, 1492, 1496, 2572, 1492, 1492, 1492, 1492, 1496, 1496, 1496, 1496, 1496, 1592, 1492, 1496,
        1492, 1496, 1496
    ];

    // Native509810 command5 selects scene camera variable0; the remaining50DB40 program waits
    // for that introduction, yields, and then stops. No branch or camera motion is approximated here.
    private static ReadOnlySpan<byte> IntroductionScript => [0x05, 0x00, 0xC3, 0x10, 0xE5, 0x7F, 0xC1, 0x00, 0xCB, 0x7F, 0xEA, 0x06, 0xE6, 0x07, 0xE6, 0x02, 0x01, 0xE6, 0xF1, 0x03, 0x02, 0x00, 0x00, 0x00];

    public static NativeBattleCameraBank ReadNativeBank(int stageId, byte[] bytes, int geometryOffset)
    {
        if ((uint)stageId >= CameraOffsets.Length)
        {
            throw new NotSupportedException($"Stage {stageId} has no original camera initializer contract.");
        }
        int offset = CameraOffsets[stageId];
        if (offset < 0 || offset > bytes.Length - 6 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset)) != 2)
        {
            throw new InvalidDataException("Native stage camera directory header is invalid.");
        }
        int directory = offset + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 4));
        if (directory < offset + 6 || directory >= geometryOffset || geometryOffset > bytes.Length)
        {
            throw new InvalidDataException("Native stage camera track bank exceeds its original resource.");
        }
        return new(bytes.AsSpan(directory, geometryOffset - directory).ToArray().ToImmutableArray());
    }

    public static IReadOnlyDictionary<int, BattleStageCamera> Read(int stageId, byte[] bytes, int geometryOffset)
    {
        if ((uint)stageId >= CameraOffsets.Length)
        {
            throw new NotSupportedException($"Stage {stageId} has no original camera initializer contract.");
        }
        using BinaryReader reader = new(new MemoryStream(bytes, false));
        int offset = CameraOffsets[stageId];
        reader.BaseStream.Position = offset;
        if (reader.ReadUInt16() != 2)
        {
            throw new InvalidDataException($"Stage {stageId} camera directory header is invalid.");
        }
        int script = offset + reader.ReadUInt16();
        int directory = offset + reader.ReadUInt16();
        if (script < offset + 6 || directory != script + IntroductionScript.Length || directory >= geometryOffset || !bytes.AsSpan(script, IntroductionScript.Length).SequenceEqual(IntroductionScript))
        {
            throw new NotSupportedException($"Stage {stageId} requires an unsupported camera introduction program.");
        }
        reader.BaseStream.Position = directory;
        int groups = reader.ReadUInt16();
        if (groups is < 1 or > 16)
        {
            throw new InvalidDataException($"Stage {stageId} camera group count is invalid.");
        }
        int[] groupOffsets = Enumerable.Range(0, groups).Select(_ => directory + reader.ReadUInt16()).ToArray();
        Dictionary<int, BattleStageCamera> cameras = [];
        for (int group = 0; group < groups; group++)
        {
            int groupOffset = groupOffsets[group];
            if (groupOffset < directory + 2 + groups * 2 || groupOffset > geometryOffset - 16)
            {
                throw new InvalidDataException($"Stage {stageId} camera group exceeds its directory.");
            }
            reader.BaseStream.Position = groupOffset;
            int[] tracks = Enumerable.Range(0, 8).Select(_ => groupOffset + 2 * reader.ReadInt16()).ToArray();
            for (int track = 0; track < tracks.Length; track++)
            {
                if (tracks[track] < groupOffset + 16 || tracks[track] > geometryOffset - 2)
                {
                    throw new InvalidDataException($"Stage {stageId} camera track exceeds its directory.");
                }
                reader.BaseStream.Position = tracks[track];
                BattleStageCamera? camera = ReadSettledEndpoint(reader, geometryOffset);
                if (camera is not null)
                {
                    cameras.Add(group * 16 + track, camera);
                }
            }
        }
        return cameras;
    }

    private static BattleStageCamera? ReadSettledEndpoint(BinaryReader reader, int end)
    {
        Vector3? eye = null;
        Vector3? target = null;
        int focal = 512;
        int roll = 0;
        while (reader.BaseStream.Position <= end - 2)
        {
            ushort flags = reader.ReadUInt16();
            if (flags == ushort.MaxValue)
            {
                return eye is Vector3 position && target is Vector3 subject && position != subject && focal > 0 && roll == 0 ? new BattleStageCamera(position, subject, focal) : null;
            }
            int lensMode = flags >> 6 & 3;
            int rollMode = flags >> 8 & 3;
            int parameterWords = Math.Max(0, lensMode - 1) + Math.Max(0, rollMode - 1);
            if (reader.BaseStream.Position > end - parameterWords * 2)
            {
                throw new InvalidDataException("Battle camera lens/roll parameters exceed their resource.");
            }
            // Native503C70: mode0 retains the current value,1 selects its default,2 reads one WORD,
            // and3 reads both interpolation endpoints. Only the final endpoint belongs to this view.
            focal = ReadEndValue(reader, lensMode, focal, 512);
            roll = ReadEndValue(reader, rollMode, roll, 0);
            int knots = 0;
            while (reader.BaseStream.Position <= end - 2 && reader.ReadInt16() >= 0)
            {
                if (++knots > 32 || reader.BaseStream.Position > end - 16)
                {
                    throw new InvalidDataException("Battle camera path exceeds its native knot bounds.");
                }
                byte eyeReference = (byte)reader.ReadUInt16();
                Vector3 nextEye = new(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                byte targetReference = (byte)reader.ReadUInt16();
                Vector3 nextTarget = new(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                // Native503AE0/50D790: F1/FB are absolute, F3/F4 hold the current eye/target.
                // Actor-relative final endpoints need the native camera actor/bone resolver.
                eye = eyeReference is 0xF1 or 0xFB ? nextEye : eyeReference == 0xF3 ? eye : null;
                target = targetReference is 0xF1 or 0xFB ? nextTarget : targetReference == 0xF4 ? target : null;
            }
        }
        throw new InvalidDataException("Battle camera track has no bounded terminator.");
    }

    private static int ReadEndValue(BinaryReader reader, int mode, int current, int defaultValue)
    {
        if (mode == 3)
        {
            reader.ReadInt16();
        }
        return mode switch
        {
            0 => current,
            1 => defaultValue,
            _ => reader.ReadInt16()
        };
    }
}
