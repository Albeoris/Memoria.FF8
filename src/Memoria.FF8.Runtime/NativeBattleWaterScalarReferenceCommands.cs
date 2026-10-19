using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterScalarReferenceServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, uint, byte> ReadParameterByte, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel> ReadActor, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterActorAnchorSource> ReadAnchor, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> UpdateSelected, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> CachePosition);

/// <summary>Selected scalar and reference operations over current slots and explicit engine services.</summary>
public static class NativeBattleWaterScalarReferenceCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterScalarReferenceServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = CoreDword(frame, 0x2797450);
        switch (instruction & 0x1FF)
        {
            case 0x55:
                uint manager = CoreDword(frame, 0x27973EC);
                uint parameters = CoreDword(frame, unchecked(manager + 0xC0));
                bool branch = (instruction >> 9) switch
                {
                    0 => frame.Core.Read(unchecked(manager + 0xD0), 1)[0] >= 1,
                    1 => frame.Core.Read(unchecked(manager + 0xD0), 1)[0] != frame.Core.Read(unchecked(manager + 0xD1), 1)[0],
                    2 => (services.ReadParameterByte(frame, unchecked(parameters + 1)) & 3) == 1,
                    3 => (services.ReadParameterByte(frame, unchecked(parameters + 1)) & 2) != 0,
                    _ => throw new NotSupportedException("Native selected conditional branch requires one of its four reached conditions.")
                };
                return WriteCoreDword(frame, 0x2797450, branch ? unchecked(pc + (uint)(int)(short)Operand(2)) : unchecked(pc + 4));
            case 0x61:
            case 0x62:
                return CopyMotion(frame, program, services.ResolveSlot, services.CachePosition, services.UpdateSelected);
            case 0x75:
                int actorIndex = ReadSlot(frame, 0x1B, 1)[0];
                if ((uint)actorIndex >= 7)
                {
                    throw new NotSupportedException("Native scalar height leaves its seven captured actor rows.");
                }
                uint header = CoreDword(frame, 0x279744C);
                uint actorPointer = CoreDword(frame, unchecked(header + 0x60 + (uint)(actorIndex * 4)));
                NativeBattleGraphicsModel actor = services.ReadActor(frame, actorPointer);
                int height = (actor.Transform ?? throw new NotSupportedException("Native scalar height requires its actual actor Scale38.")).Scale38;
                if ((instruction & 0x200) != 0)
                {
                    NativeBattleWaterActorAnchorSource source = services.ReadAnchor(frame, actorPointer);
                    NativeBattleWaterActorAnchorResult anchor = NativeBattleWaterActorAnchor.EvaluateB66B80(frame, source.Skeleton, source.LocalBones, 0, 0x1000, source.Basis);
                    frame = anchor.Frame;
                    NativeBattleEffectLocalBounds bounds = actor.GeometryBounds52 ?? throw new NotSupportedException("Native scalar anchor height requires its actual signed actor bounds.");
                    int lower = unchecked(anchor.Anchor.Y - bounds.Minimum.Y);
                    int upper = unchecked(bounds.Maximum.Y - anchor.Anchor.Y);
                    height = Math.Max(height, Math.Max(lower, upper));
                }
                int scalar = unchecked(unchecked((short)Operand(2) * height) << 8);
                for (int lane = 0; lane < 6; lane++)
                {
                    if (Selected(lane))
                    {
                        frame = WriteSlotDword(frame, 0x50 + lane * 4, unchecked((uint)scalar));
                    }
                }
                frame = services.UpdateSelected(frame);
                frame = services.CachePosition(frame);
                return Advance(4);
            case 0x76:
                uint randomSource = services.ResolveSlot(frame, unchecked((short)Operand(2)));
                for (int lane = 0; lane < 6; lane++)
                {
                    if (Selected(lane))
                    {
                        short magnitude = Word(randomSource, 0x8C + lane * 2 + (lane >= 3 ? 2 : 0));
                        NativeBattleWaterVectorRandom draw = NativeBattleWaterVectorCommands.RandomB65740(magnitude, frame.Random);
                        frame = frame with { Random = draw.Random };
                        uint value = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0x50 + lane * 4, 4));
                        frame = WriteSlotDword(frame, 0x50 + lane * 4, unchecked(value + (uint)(draw.Value << 16)));
                    }
                }
                frame = services.UpdateSelected(frame);
                frame = services.CachePosition(frame);
                return Advance(4);
            case 0x87:
                int factor = unchecked((short)Operand(2));
                int divisor = unchecked((short)Operand(4));
                for (int lane = 0; lane < 6; lane++)
                {
                    if (Selected(lane))
                    {
                        int value = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0x50 + lane * 4, 4))) >> 16;
                        int numerator = unchecked(unchecked((factor - 0x100) * value) << 8);
                        if (divisor == 0 || numerator == int.MinValue && divisor == -1)
                        {
                            throw new NotSupportedException("Native selected scalar division would fault on its actual signed operands.");
                        }
                        frame = WriteSlotDword(frame, 0x68 + lane * 4, unchecked((uint)(numerator / divisor)));
                    }
                }
                return Advance(6);
            case 0x4C:
                return ChangeUpdate(frame, program, services.UpdateSelected);
            case 0xD2:
                return NegateMotion(frame, program, services.UpdateSelected, services.CachePosition);
            case 0xD3:
                uint colorSource = services.ResolveSlot(frame, unchecked((short)Operand(2)));
                int colorFactor = Word(colorSource, 0x8C);
                int red = Math.Clamp(unchecked(Word(colorSource, 0x94) * colorFactor) / 0x100, 0, 255);
                int green = Math.Clamp(unchecked(Word(colorSource, 0x96) * colorFactor) / 0x100, 0, 255);
                int blue = Math.Clamp(unchecked(Word(colorSource, 0x98) * colorFactor) / 0x100, 0, 255);
                uint oldColor = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4));
                frame = WriteSlotDword(frame, 0xCC, (oldColor & 0xFF000000) | (uint)(red | (green << 8) | (blue << 16)));
                return Advance(4);
            default:
                throw new NotSupportedException("Native scalar reference commands admit only selected operations 55, 61, 62, 75, 76, 87 and D3.");
        }

        bool Selected(int lane) => (instruction & (0x8000 >> lane)) != 0;
        NativeBattleWaterExecutionFrame Advance(uint length) => WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + length));
        short Word(uint address, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(address + (uint)offset), 2), 2));

        ushort Operand(int delta)
        {
            uint relative = unchecked(pc + (uint)delta - program.Address);
            if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
            {
                throw new InvalidDataException("Native scalar reference operand leaves its current program owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        }
    }

    public static NativeBattleWaterExecutionFrame ChangeUpdate(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> update)
    {
        byte kind = NativeBattleScriptedCommandReads.Program(frame, program, 2, 1)[0];
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x18, 1), [kind]) };
        frame = update(frame);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame NegateMotion(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> update, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> cachePosition)
    {
        short index = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint address = unchecked(CoreDword(frame, 0x27973E8) + 0x50u + (uint)(4 * index));
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(unchecked(0u - value))) };
        frame = update(frame);
        frame = cachePosition(frame);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame CopyMotion(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> cachePosition, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> updateSelected)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) is not (0x61 or 0x62))
        {
            throw new InvalidDataException("Native motion copy requires its selected instruction.");
        }
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        uint source = resolveSlot(frame, selector);
        int offset = instruction == 0x61 ? 0x50 : 0x68;
        short mask = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        for (int lane = 0; lane < 6; lane++)
        {
            if ((mask & (1 << lane)) == 0)
            {
                continue;
            }
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(source + (uint)(offset + 4 * lane)), 4), 4));
            frame = WriteSlotDword(frame, offset + lane * 4, value);
        }
        frame = cachePosition(frame);
        frame = updateSelected(frame);
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }
}
