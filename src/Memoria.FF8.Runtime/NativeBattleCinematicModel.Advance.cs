using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicModel
{
    public static NativeBattleWaterExecutionFrame AdvanceAndCompose(NativeBattleWaterExecutionFrame frame, uint model, NativeBattleCinematicModelServices services)
    {
        bool paused = frame.ReadTemporaryByte(0x145) != 0;
        ushort flags = paused ? (ushort)0 : Word(unchecked(model + 2));
        if (paused || (flags & 1) != 0)
        {
            Restore();
        }
        else if (Word(model) != 0)
        {
            if ((flags & 2) != 0)
            {
                Restore();
            }
            else
            {
                bool complete;
                (frame, complete) = AdvanceRegular(frame, model, services);
                if (complete)
                {
                    byte animation = frame.Arena.Read(ArenaOffset(unchecked(model + 9), 1), 1)[0];
                    frame = Select509440(frame, model, animation, services);
                }
                ushort counter = unchecked((ushort)(Word(model) + 1));
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(model, 2), BitConverter.GetBytes(counter)) };
            }
        }
        uint skeleton = Dword(Dword(unchecked(model + 0x14)));
        int bones = frame.Arena.Read(ArenaOffset(skeleton, 1), 1)[0];
        byte[] rotation = frame.ReadTemporary(0xE0, 18);
        NativeBattleActionMatrix parent = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(2 * index))).ToImmutableArray(), unchecked((int)frame.ReadTemporaryDword(0xF4)), unchecked((int)frame.ReadTemporaryDword(0xF8)), unchecked((int)frame.ReadTemporaryDword(0xFC)), 4096);
        for (int index = 0; index < bones; index++)
        {
            int offset = ArenaOffset(unchecked(skeleton + 0x20u + 0x30u * (uint)index), 32);
            NativeBattleActionMatrix local = NativeBattleArenaPose.ReadMatrix(frame.Arena, offset);
            NativeBattleActionMatrix composed = NativeBattlePoseMatrices.Compose56C2F0(parent, local);
            frame = frame with { Arena = NativeBattleArenaPose.WriteMatrix(frame.Arena, offset, composed, true) };
        }
        if (Word(model) == 0)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(model, 2), BitConverter.GetBytes((ushort)1)) };
        }
        return frame;

        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void Restore()
        {
            uint current = Dword(Dword(unchecked(model + 0x14)));
            int offset = ArenaOffset(current, 1);
            int count = frame.Arena.Read(offset, 1)[0];
            NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(frame.Arena, offset, count);
            frame = frame with { Arena = NativeBattleArenaPose.RestoreBoneMatrices(frame.Arena, offset, pose) };
        }
    }

    private static (NativeBattleWaterExecutionFrame Frame, bool Complete) AdvanceRegular(NativeBattleWaterExecutionFrame frame, uint model, NativeBattleCinematicModelServices services)
    {
        uint sequence = unchecked(model + 0x20);
        byte current = ReadByte(unchecked(sequence + 6));
        if (current >= ReadByte(unchecked(sequence + 7)))
        {
            return (frame, true);
        }
        byte flags = ReadByte(unchecked(sequence + 1));
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException($"Native embedded model pose speed flags 0x{flags:X2} require their selected cursor contract.");
        }
        uint table = ReadDword(unchecked(model + 0x14));
        uint skeleton = ReadDword(table);
        uint animations = ReadDword(unchecked(table + 8));
        byte[] section = services.ReadAnimations(frame, animations) ?? throw new InvalidDataException("Native embedded model requires its current bounded animation section.");
        int offset = ArenaOffset(skeleton, 1);
        int count = frame.Arena.Read(offset, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(frame.Arena, offset, count);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(section, count, ReadByte(sequence), pose, ReadWord(unchecked(sequence + 2)), ReadWord(unchecked(sequence + 4)));
        byte[] control = frame.Arena.Read(ArenaOffset(sequence, 8), 8);
        control[1] = (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2));
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), next.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), next.BitOffset);
        control[6] = unchecked((byte)(current + 1));
        NativeBattleEffectArena arena = frame.Arena.Write(ArenaOffset(sequence, 8), control);
        arena = NativeBattleArenaPose.WritePose(arena, offset, next.Pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, offset, next.Pose);
        return (frame with { Arena = arena }, false);

        byte ReadByte(uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
        ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }
}
