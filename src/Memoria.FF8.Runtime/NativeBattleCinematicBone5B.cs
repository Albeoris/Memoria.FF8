using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicBonePose(NativeBattleWaterExecutionFrame Frame, ImmutableArray<byte> Skeleton, ImmutableArray<NativeBattleActionMatrix> Bones, NativeBattleActionMatrix? PhysicalBasis)
{
    public Func<int, int, byte[]>? ReadMetadata { get; init; }
    public int MetadataLength { get; init; }
}
public sealed record NativeBattleCinematicBone5BServices(Func<NativeBattleWaterExecutionFrame, ushort, uint> ResolveNode, Func<NativeBattleWaterExecutionFrame, uint, bool, NativeBattleCinematicBonePose> RestorePose, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> UpdateSlot, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PublishInput, NativeBattleWaterActorAnchorServices ActorAnchor);

public static class NativeBattleCinematicBone5B
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicBone5BServices services)
    {
        bool physical = (ControlByte(0x4B) & 4) != 0;
        uint source;
        uint reference = 0;
        if (physical)
        {
            byte actor = ReadSlot(frame, 0x1B, 1)[0];
            source = CoreDword(frame, unchecked(CoreDword(frame, 0x279744C) + 0x60u + 4u * actor));
        }
        else
        {
            reference = services.ResolveNode(frame, unchecked((ushort)Operand(2)));
            source = Dword(reference + 0xBC);
            if (source == 0)
            {
                return NativeBattleScriptedCommandReads.Advance(frame, (ControlByte(0x4B) & 8) != 0 ? 8u : 4u);
            }
        }
        int offset;
        short selector;
        int fraction;
        if ((ControlByte(0x4B) & 8) != 0)
        {
            ushort packed = unchecked((ushort)Operand(4));
            selector = (short)(packed & 255);
            offset = packed >> 8;
            fraction = Operand(6) << 4;
            frame = NativeBattleScriptedCommandReads.Advance(frame, 8);
        }
        else
        {
            offset = 0;
            selector = unchecked((short)SlotWord(frame, 0x8C));
            fraction = 4096;
            frame = NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        NativeBattleActionMatrix? basis = null;
        if (!physical)
        {
            (byte[] seed, bool[] known) = frame.Core.ReadSnapshot(0x2797968, 20);
            ImmutableArray<byte>.Builder bytes = frame.TemporaryFrame.Bytes.ToBuilder();
            ImmutableArray<bool>.Builder mask = frame.TemporaryFrame.Known.ToBuilder();
            for (int index = 0; index < 20; index++)
            {
                bytes[0x60 + index] = seed[index];
                mask[0x60 + index] = known[index];
            }
            frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Bytes = bytes.ToImmutable(), Known = mask.ToImmutable() } };
            NativeBattleActionMatrix seedMatrix = ReadCoreMatrix(0x2797968, false);
            int x = unchecked((short)SlotWord(frame, 0x94));
            int y = unchecked((short)SlotWord(frame, 0x96));
            int z = unchecked((short)SlotWord(frame, 0x98));
            frame = frame.WriteTemporaryDword(0x74, unchecked((uint)x)).WriteTemporaryDword(0x78, unchecked((uint)y)).WriteTemporaryDword(0x7C, unchecked((uint)z));
            int sx = SignedWord(reference + 0x8C) << 4;
            frame = frame.WriteTemporaryDword(0x80, unchecked((uint)sx));
            int sy = SignedWord(reference + 0x8E) << 4;
            frame = frame.WriteTemporaryDword(0x84, unchecked((uint)sy));
            int sz = SignedWord(reference + 0x90) << 4;
            frame = frame.WriteTemporaryDword(0x88, unchecked((uint)sz));
            NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(seedMatrix, sx, sy, sz);
            for (int index = 0; index < 9; index++)
            {
                frame = frame.WriteTemporaryWord(0x60 + index * 2, unchecked((ushort)scaled.Rotation[index]));
            }
            basis = scaled with { X = x, Y = y, Z = z };
        }
        NativeBattleCinematicBonePose pose = services.RestorePose(frame, physical ? source : reference, physical);
        frame = pose.Frame;
        NativeBattleActionMatrix selectedBasis = basis ?? pose.PhysicalBasis ?? throw new NotSupportedException("Native bone positioning requires its actual physical actor matrix.");
        NativeBattleWaterActorAnchorResult result = pose.ReadMetadata is { } readMetadata ? services.ActorAnchor.EvaluateB66B80(frame, pose.MetadataLength, readMetadata, pose.Bones, selector, fraction, selectedBasis) : services.ActorAnchor.EvaluateB66B80(frame, pose.Skeleton, pose.Bones, selector, fraction, selectedBasis);
        frame = result.Frame;
        frame = services.PublishInput(frame, frame.TemporaryFrame.Address + 0xC0);
        int mode = ControlWord(0x4A) >> 12;
        if (mode is not (1 or 2))
        {
            StoreSlotDword(0x5C, unchecked((uint)result.Anchor.X << 16));
            StoreSlotDword(0x60, unchecked((uint)result.Anchor.Y << 16));
            StoreSlotDword(0x64, unchecked((uint)result.Anchor.Z << 16));
            return services.UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]);
        }
        uint destination;
        if (mode == 2 && offset != 0)
        {
            destination = unchecked(CoreDword(frame, 0x27973E8) + (uint)offset);
        }
        else
        {
            (frame, destination) = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        }
        NativeBattleActionMatrix matrix = result.Matrix with { X = result.Anchor.X, Y = result.Anchor.Y, Z = result.Anchor.Z };
        StoreMatrix(destination, matrix, false);
        if (mode == 1)
        {
            matrix = NativeBattleGfMatrices.Compose56C2F0(ReadCoreMatrix(0x27979E8, true), matrix);
            StoreMatrix(destination, matrix, true);
        }
        return frame;

        short Operand(int offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, checked((uint)offset), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        byte ControlByte(int offset)
        {
            uint address = unchecked(CoreDword(frame, 0x27973B8) + (uint)offset);
            return address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address < 0x180 ? frame.ReadTemporary(checked((int)(address - frame.TemporaryFrame.Address)), 1)[0] : frame.Core.Read(address, 1)[0];
        }
        ushort ControlWord(int offset) => (ushort)(ControlByte(offset) | ControlByte(offset + 1) << 8);
        NativeBattleActionMatrix ReadCoreMatrix(uint address, bool translation)
        {
            byte[] rotation = frame.Core.Read(address, 18);
            return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), translation ? unchecked((int)CoreDword(frame, address + 20)) : 0, translation ? unchecked((int)CoreDword(frame, address + 24)) : 0, translation ? unchecked((int)CoreDword(frame, address + 28)) : 0, 4096);
        }
        void StoreSlotDword(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), BitConverter.GetBytes(value)) };
        void StoreMatrix(uint address, NativeBattleActionMatrix value, bool clearPadding)
        {
            byte[] raw = new byte[32];
            for (int index = 0; index < 9; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(index * 2), value.Rotation[index]);
            }
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(20), value.X);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(24), value.Y);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(28), value.Z);
            if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 32)
            {
                frame = frame with { Core = frame.Core.Write(address, raw) };
                if (!clearPadding)
                {
                    frame = frame with { Core = frame.Core.MarkUnknown(address + 18, 2) };
                }
            }
            else
            {
                int index = ArenaOffset(address, 32);
                NativeBattleEffectArena arena = frame.Arena.Write(index, raw);
                frame = frame with { Arena = clearPadding ? arena : arena.MarkUnknown(index + 18, 2) };
            }
        }
    }
}
