using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    // The root waits through both 87-frame gestures and the result camera before it releases the attack poses.
    private const int BrothersEntranceEnd = 0x126;
    private const byte MinotaurBody = 78;
    private const byte SacredBody = 79;
    private const byte MinotaurRitualShape = 0x0F;
    private const byte SacredRitualShape = 0x10;
    private static readonly ImmutableArray<int> BrothersHandBones = [30, 31, 36, 37];

    private static bool IsBrothersRitualWait(NativeBattleWaterExecutionFrame frame, uint pc)
    {
        uint primary = CoreDword(frame, 0x2798A68);
        return pc - primary is 0x10E or 0x110 or 0x112 or 0x114 or 0x116 or 0x120 or
            0x16BE or 0x16C2 or 0x16C4 or 0x16CA or 0x16D0 or
            0x1782 or 0x1786 or 0x178A or 0x1790 or 0x1796;
    }

    private static NativeBattleWaterExecutionFrame MirrorBrothersHands(NativeBattleWaterExecutionFrame frame, NativeBattleEffectInitializerAsset primary, NativeBattleCinematicResources resources)
    {
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        ushort role = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(model + 8, 2), 2));
        if (role is not (0x000F or 0x0110))
        {
            return frame;
        }
        byte counter = frame.Arena.Read(ArenaOffset(model + 0x26, 1), 1)[0];
        byte animation = role == 0x000F ? (byte)1 : (byte)0;
        ReadOnlySpan<byte> otherShape = role == 0x000F ? ReadBrothersShape(primary.Data.AsSpan(), SacredRitualShape) : ReadMinotaurShape(resources).Span;
        ReadOnlySpan<byte> otherAnimations = otherShape[ReadShapeOffset(otherShape, 12)..];
        int clip = ReadShapeOffset(otherAnimations, 4 + 4 * animation);
        if (otherAnimations[clip] != 87 || counter is 0 or > 87)
        {
            throw new InvalidDataException("The original Brothers gesture clips must share their frame count.");
        }
        uint skeletonAddress = ReadArenaDword(frame, model + 0x30);
        int skeleton = ArenaOffset(skeletonAddress, 16 + 39 * 48);
        ReadOnlySpan<byte> otherSkeleton = otherShape[ReadShapeOffset(otherShape, 4)..];
        if (frame.Arena.Read(skeleton, 1)[0] != 39 || otherSkeleton[0] != 39)
        {
            throw new InvalidDataException("The original Brothers gesture clips require their 39-bone rigs.");
        }
        NativeBattleDecodedPose ownPose = NativeBattleArenaPose.ReadCurrentPose(frame.Arena, skeleton, 39);
        NativeBattleDecodedPose otherPose = NativeBattlePoseDecoder.Sample(otherAnimations, 39, animation, counter, false);
        var bones = ownPose.Bones.ToBuilder();
        foreach (int bone in BrothersHandBones)
        {
            int offset = 16 + bone * 48;
            if (!frame.Arena.Read(skeleton + offset, 2).AsSpan().SequenceEqual(otherSkeleton.Slice(offset, 2)))
            {
                throw new InvalidDataException("The original Brothers hand joints must share their bone parent indices.");
            }
            bones[bone] = bones[bone] with { Rotation = otherPose.Bones[bone].Rotation };
        }
        // Repose the draw matrices while retaining today's decoded pose and its next delta-frame state.
        NativeBattleEffectArena arena = NativeBattleArenaPose.RestoreBoneMatrices(frame.Arena, skeleton, ownPose with { Bones = bones.MoveToImmutable() });
        byte[] rotation = frame.ReadTemporary(0xE0, 18);
        NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(2 * index))).ToImmutableArray(), unchecked((int)frame.ReadTemporaryDword(0xF4)), unchecked((int)frame.ReadTemporaryDword(0xF8)), unchecked((int)frame.ReadTemporaryDword(0xFC)), 4096);
        for (int bone = 0; bone < 39; bone++)
        {
            int offset = skeleton + 32 + bone * 48;
            NativeBattleActionMatrix local = NativeBattleArenaPose.ReadMatrix(arena, offset);
            arena = NativeBattleArenaPose.WriteMatrix(arena, offset, NativeBattlePoseMatrices.Compose56C2F0(basis, local), true);
        }
        return frame with { Arena = arena };
    }

    private static ReadOnlyMemory<byte> ReadMinotaurFinisherGeometry(NativeBattleCinematicResources resources)
    {
        ReadOnlyMemory<byte> shape = ReadMinotaurShape(resources);
        int skeleton = ReadShapeOffset(shape.Span, 4);
        int geometry = ReadShapeOffset(shape.Span, 8);
        int animation = ReadShapeOffset(shape.Span, 12);
        if (shape.Span[skeleton] != 39 || BinaryPrimitives.ReadInt16LittleEndian(shape.Span[(skeleton + 2)..]) != 228 || BinaryPrimitives.ReadInt32LittleEndian(shape.Span[geometry..]) != 1)
        {
            throw new InvalidDataException("Brotherly Love requires the selected participant's original embedded model.");
        }
        return shape.Slice(geometry, animation - geometry);
    }

    private static NativeBattleWaterExecutionFrame ScaleMinotaurFinisherBody(NativeBattleWaterExecutionFrame frame)
    {
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint skeleton = ReadArenaDword(frame, model + 0x30);
        if (frame.Arena.Read(ArenaOffset(skeleton, 1), 1)[0] != 39 || frame.Arena.ReadUInt16(ArenaOffset(skeleton + 2, 2)) != 456)
        {
            throw new InvalidDataException("Brotherly Love requires the original paired rig and 2:1 body scale.");
        }
        for (int offset = 0xE0; offset < 0xF2; offset += 2)
        {
            short value = unchecked((short)frame.ReadTemporaryWord(offset));
            frame = frame.WriteTemporaryWord(offset, unchecked((ushort)(value / 2)));
        }
        return frame;
    }

    private static ReadOnlyMemory<byte> ReadMinotaurShape(NativeBattleCinematicResources resources)
    {
        int sourceFile = NativeBattleCinematicProfile.ForEffect(205).SourceBase + 3;
        ReadOnlyMemory<byte> source = resources.Files.Single(file => file.FileId == sourceFile).Bytes.AsMemory();
        int offset = ReadBrothersShapeOffset(source.Span, MinotaurRitualShape);
        return source.Slice(offset, ReadShapeOffset(source.Span, offset));
    }

    private static ReadOnlySpan<byte> ReadBrothersShape(ReadOnlySpan<byte> resource, byte shape)
    {
        int offset = ReadBrothersShapeOffset(resource, shape);
        return resource.Slice(offset, ReadShapeOffset(resource, offset));
    }

    private static int ReadBrothersShapeOffset(ReadOnlySpan<byte> resource, byte shape)
    {
        int directory = ReadShapeOffset(resource, 12);
        return checked(directory + ReadShapeOffset(resource, directory + 4 + 4 * shape));
    }

    private static int ReadShapeOffset(ReadOnlySpan<byte> resource, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(resource[offset..]);
    }

    private static uint ReadArenaDword(NativeBattleWaterExecutionFrame frame, uint address)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }
}
