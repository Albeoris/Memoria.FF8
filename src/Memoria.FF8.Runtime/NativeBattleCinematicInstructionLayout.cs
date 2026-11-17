using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicModelOperands(ImmutableArray<byte> Operands, bool Complete);

/// <summary>Encodes the widths and control successors of authored cinematic instructions.</summary>
internal readonly record struct NativeBattleCinematicInstructionLayout(ushort Word)
{
    public int Opcode => Word & 0x1FF;
    public int Mode => Word >> 12;
    private int Parameters => Word >> 9;
    public bool ReadsLiteralActor => Opcode == 0x64 && (Word & 0x9000) == 0x1000;
    public int ActorOrderCount => Opcode == 0x9A ? Mode : 0;

    public static NativeBattleCinematicInstructionLayout Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
        {
            throw new InvalidDataException("Native cinematic instruction exceeds its source WORD.");
        }
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes));
    }

    public bool TryReadSuccessors(ReadOnlySpan<byte> bytes, int effect, uint handler, out int next, out int? branch)
    {
        int relativeAt = 0;
        bool continues = true;
        int width = Opcode switch
        {
            0 or 4 or 7 or 8 or 9 or 0x20 or 0x22 or 0x23 or 0x24 or 0x2A or 0x2D or 0x2F or 0x30 or 0x31 or 0x34 or 0x37 or 0x38 or 0x47 or 0x48 or 0x4D or 0x57 or 0x5E or 0x6D or 0x71 or 0x72 or 0x9B or 0xB4 or 0xD0 or 0xEB or 0xF2 or 0x12E => 2,
            1 or 2 or 3 or 5 or 0x1B or 0x1D or 0x1E or 0x1F or 0x25 or 0x27 or 0x28 or 0x2B or 0x32 or 0x33 or 0x39 or 0x3B or 0x3C or 0x3F or 0x40 or 0x45 or 0x46 or 0x4A or 0x4C or 0x51 or 0x52 or 0x53 or 0x58 or 0x5D or 0x66 or 0x67 or 0x68 or 0x6A or 0x6E or 0x70 or 0x74 or 0x7D or 0x81 or 0x8D or 0x9C or 0xA0 or 0xA2 or 0xAA or 0xAD or 0xB0 or 0xB1 or 0xB2 or 0xBC or 0xC4 or 0xCD or 0xCE or 0xD2 or 0xD3 or 0x104 or 0x116 or 0x120 or 0x13D or 0x140 => 4,
            0x26 or 0x49 or 0x56 or 0x5A or 0x61 or 0x62 or 0x63 or 0x69 or 0x6F or 0x73 or 0x79 or 0x7A or 0x82 or 0x8B or 0x91 or 0x92 or 0xA1 or 0xA4 or 0xB5 or 0xE7 or 0xFB or 0x105 or 0x10D => 6,
            0x21 or 0x3E or 0x5F => 8,
            0x93 => 10,
            6 => (Word & 0x8000) != 0 ? 2 : 4,
            0x1A => 4 + 2 * (ReadSignedWord(bytes, 2) & 15),
            0x1C => 6,
            0x29 => (Word & 0xE000) == 0 || (Word & 0x8000) != 0 ? 4 : (Word & 0x4000) != 0 ? 8 : 10,
            0x2C when handler == 0xB25CD0 => 2,
            0x35 when effect == 223 || handler == 0xA996A0 => (Word & 0x8000) != 0 ? 6 : 10,
            0x41 when Mode is 0 or 8 => 4,
            0x41 when Mode is 1 => 6,
            0x42 when Parameters is 0 or 2 && handler is 0xB24BB0 or 0xAFEF60 or 0xB57280 or 0x9C8CC0 or 0x963E30 => 4,
            0x43 or 0x44 or 0x4E => 6 + 2 * ReadSignedWord(bytes, 4),
            0x4B => Mode == 1 ? 10 : Mode == 2 ? 8 : 6,
            0x55 when handler == 0xB2B380 => 4,
            0x59 => (Word & 0x1000) != 0 ? 6 : 4,
            0x5B => (Word & 0x0800) != 0 ? 8 : 4,
            0x5C when handler is 0xB64090 or 0xAFEBF0 or 0xAF3660 => (Word & 0x8000) != 0 ? 8 : 6,
            0x60 => (Word & 0x8000) != 0 ? 8 : 4,
            0x64 => ReadsLiteralActor ? 4 : 2,
            0x83 when handler == 0xB2DF10 => 2,
            0x87 when handler == 0xB2D2B0 => 6,
            0x8A => Mode switch { 1 or 2 => 4, 3 => 2, _ => 6 },
            0x94 when (effect == 230 && handler == 0xA77C10) || (effect == 238 && handler == 0xA26E20) => 10,
            0x9A => 2 + 2 * ActorOrderCount,
            0xAC => (Word & 0x200) == 0 ? 6 : 4 + 2 * BitOperations.PopCount((uint)(Word & 0xFC00)),
            0xAE when handler == 0xB2FBE0 => 4,
            0xAF when handler == 0xB52F20 => 0x50,
            0xB3 when handler == 0xAC7800 => 4,
            0xB7 when Mode == 1 && (effect == 206 || handler == 0xAE3350) => 6,
            0xB9 when handler == 0xB2CA80 => 4,
            0xBB when effect == 223 => 6,
            0xBD when effect == 223 => (Word & 0x8000) != 0 ? 6 : 10,
            0xBE when handler == 0xB3A9B0 => 4,
            0xC1 when handler == 0xB3AA00 => 2,
            0xD4 => (Word & 0x8000) == 0 ? 8 : 4,
            0xD5 when handler == 0xAFB630 && Mode is 0 or 1 => 4,
            0x106 when effect == 223 => 6,
            0x10C => Mode is 2 or 3 ? 6 : 4,
            0x112 when handler == 0xA32E10 => 10,
            0x113 when handler == 0xA32F50 => 8,
            >= 0xA and <= 0x17 => NativeBattleWaterVectorCommands.ReadInstructionWidth(Word),
            _ => 0
        };
        switch (Opcode)
        {
            case 0 or 4 or 0x1D:
                continues = false;
                break;
            case 1 or 2:
                continues = false;
                relativeAt = 2;
                break;
            case 3 or 0x1F or 0x32 or 0x46 or 0x55 or 0x5D or 0x9C or 0xAE or 0xBE:
                relativeAt = 2;
                break;
            case 0xD5 when Mode == 1:
                relativeAt = 2;
                break;
            case 0x42 when Parameters == 2:
                relativeAt = 2;
                break;
            case 5 when Mode is 2 or 3:
                width = 6;
                relativeAt = 4;
                break;
            case 0x26 or 0x49 or 0x79 or 0x8B or 0xB5:
                relativeAt = 4;
                break;
            case 0x10C when Mode is 2 or 3:
                relativeAt = 4;
                break;
            case 0x94:
                relativeAt = 8;
                break;
        }
        if (width == 0)
        {
            next = 0;
            branch = null;
            return false;
        }
        if (width < 2 || width > bytes.Length)
        {
            throw new InvalidDataException("Native cinematic instruction operands exceed their authored source.");
        }
        next = continues ? width : 0;
        branch = relativeAt == 0 ? null : ReadSignedWord(bytes, relativeAt);
        return true;
    }

    private static short ReadSignedWord(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset > bytes.Length - 2)
        {
            throw new InvalidDataException("Native cinematic control operand exceeds its source WORD.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
    }
}

public static class NativeBattleCinematicOperandReader
{
    public static NativeBattleCinematicModelOperands Read(ImmutableArray<byte> program, NativeBattleCinematicProfile profile, Func<int, uint> readInstructionHandler)
    {
        if (program.Length < 8)
        {
            throw new InvalidDataException("Native cinematic requires its authored root program pointer.");
        }
        int entry = BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan()[4..]);
        HashSet<int> visited = [];
        HashSet<byte> operands = [];
        Stack<int> pending = new();
        pending.Push(entry);
        bool complete = true;
        while (pending.TryPop(out int offset))
        {
            if (offset < 0 || offset > program.Length - 2 || (offset & 1) != 0)
            {
                complete = false;
                continue;
            }
            if (!visited.Add(offset))
            {
                continue;
            }
            ReadOnlySpan<byte> bytes = program.AsSpan()[offset..];
            NativeBattleCinematicInstructionLayout layout = NativeBattleCinematicInstructionLayout.Read(bytes);
            if (!TryReadHandler(layout.Opcode, out uint handler) || !layout.TryReadSuccessors(bytes, profile.Effect, handler, out int next, out int? branch))
            {
                complete = false;
                continue;
            }
            if (layout.ReadsLiteralActor)
            {
                operands.Add(bytes[2]);
            }
            for (int actor = 0; actor < layout.ActorOrderCount; actor++)
            {
                operands.Add(bytes[2 + 2 * actor]);
            }
            if (next != 0)
            {
                pending.Push(checked(offset + next));
            }
            if (branch is int relative)
            {
                pending.Push(checked(offset + relative));
            }
        }
        return new(operands.Order().ToImmutableArray(), complete);

        bool TryReadHandler(int opcode, out uint handler)
        {
            try
            {
                handler = NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "vm_table", opcode, readInstructionHandler(opcode));
                return !NativeBattleCinematicDispatch.IsNoOp(profile.Effect, "vm_table", opcode, handler);
            }
            catch (NotSupportedException)
            {
                handler = 0;
                return false;
            }
        }
    }
}
