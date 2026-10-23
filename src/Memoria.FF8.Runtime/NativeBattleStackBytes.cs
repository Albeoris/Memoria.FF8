using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeBattleStackPointerKind
{
    Frame,
    Allocation
}

/// <summary>An actual nonnull native owner reference, without choosing a numeric process address.</summary>
public sealed record NativeBattleStackPointer([property: JsonRequired] NativeBattleStackPointerKind Kind, [property: JsonRequired] string Owner, [property: JsonRequired] int Offset)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || string.IsNullOrWhiteSpace(Owner) || (Offset & 3) != 0)
        {
            throw new InvalidDataException("A symbolic native pointer requires its actual aligned frame or allocation owner.");
        }
    }
}

public enum NativeBattleStackOperation
{
    LiteralStore,
    PointerStore,
    UnknownRange,
    RegisterStore
}

/// <summary>Order is observation order; an invalidated range is not claimed as an instruction write width.</summary>
public sealed record NativeBattleStackObservation([property: JsonRequired] long Order, [property: JsonRequired] NativeBattleStackOperation Operation, [property: JsonRequired] int Offset, [property: JsonRequired] int Length, [property: JsonRequired] uint Instruction, [property: JsonRequired] string Phase, [property: JsonRequired] ImmutableArray<byte> Literal, [property: JsonRequired] NativeBattleStackPointer? Pointer)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<bool>? RegisterKnownBytes { get; init; }
}

public sealed record NativeBattleStackPointerCell([property: JsonRequired] int Offset, [property: JsonRequired] NativeBattleStackPointer Pointer, [property: JsonRequired] long WriterOrder);

/// <summary>One physical stack interval relative to a retained execution-frame identity. Unknown bytes remain unknown.</summary>
public sealed record NativeBattleStackBytes([property: JsonRequired] string FrameOwner, [property: JsonRequired] int FirstOffset, [property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known, [property: JsonRequired] ImmutableArray<NativeBattleStackPointerCell> Pointers, [property: JsonRequired] ImmutableArray<NativeBattleStackObservation> Observations)
{
    public static NativeBattleStackBytes Unknown(string frameOwner, int firstOffset, int length)
    {
        if (string.IsNullOrWhiteSpace(frameOwner) || length <= 0)
        {
            throw new InvalidDataException("A physical stack interval requires its selected frame owner and positive extent.");
        }
        _ = checked(firstOffset + length);
        return new(frameOwner, firstOffset, ImmutableArray.CreateRange(new byte[length]), ImmutableArray.CreateRange(new bool[length]), [], []);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FrameOwner) || Bytes.IsDefaultOrEmpty || Known.IsDefault || Bytes.Length != Known.Length || Pointers.IsDefault || Observations.IsDefault)
        {
            throw new InvalidDataException("Native physical stack bytes require retained ownership and known-byte state.");
        }
        _ = checked(FirstOffset + Bytes.Length);
        long previous = 0;
        foreach (NativeBattleStackObservation observation in Observations)
        {
            if (observation is null || observation.Order != previous + 1 || observation.Instruction == 0 || string.IsNullOrWhiteSpace(observation.Phase) || !Enum.IsDefined(observation.Operation) || observation.Literal.IsDefault)
            {
                throw new InvalidDataException("Native stack observations require ordered instruction/phase provenance.");
            }
            _ = Index(observation.Offset, observation.Length);
            if (observation.Operation != NativeBattleStackOperation.RegisterStore && observation.RegisterKnownBytes is not null)
            {
                throw new InvalidDataException("Only a register store can carry partial register-byte knowledge.");
            }
            if (observation.Operation == NativeBattleStackOperation.LiteralStore)
            {
                if (observation.Literal.Length != observation.Length || observation.Length is not (1 or 2 or 4 or 8) || observation.Pointer is not null)
                {
                    throw new InvalidDataException("Native literal stores require their actual byte/WORD/DWORD/QWORD width.");
                }
            }
            else if (observation.Operation == NativeBattleStackOperation.RegisterStore)
            {
                if (observation.Length != 4 || observation.Literal.Length != 4 || observation.Pointer is not null || observation.RegisterKnownBytes is not ImmutableArray<bool> registerKnown || registerKnown.IsDefault || registerKnown.Length != 4)
                {
                    throw new InvalidDataException("A register store requires its complete DWORD width and exact known-byte mask.");
                }
                for (int item = 0; item < 4; item++)
                {
                    if (!registerKnown[item] && observation.Literal[item] != 0)
                    {
                        throw new InvalidDataException("Unknown register bytes cannot carry a fabricated numeric value.");
                    }
                }
            }
            else if (observation.Operation == NativeBattleStackOperation.PointerStore)
            {
                if (observation.Length != 4 || !observation.Literal.IsEmpty || observation.Pointer is null)
                {
                    throw new InvalidDataException("Native pointer stores require one complete DWORD owner reference.");
                }
                observation.Pointer.Validate();
            }
            else if (!observation.Literal.IsEmpty || observation.Pointer is not null)
            {
                throw new InvalidDataException("Unknown native ranges cannot carry a fabricated value.");
            }
            previous = observation.Order;
        }
        ValidateReplay();
        HashSet<int> occupied = [];
        foreach (NativeBattleStackPointerCell cell in Pointers)
        {
            if (cell is null)
            {
                throw new InvalidDataException("Native stack pointer cells cannot be null.");
            }
            ArgumentNullException.ThrowIfNull(cell.Pointer);
            cell.Pointer.Validate();
            int index = Index(cell.Offset, 4);
            if (cell.WriterOrder <= 0 || cell.WriterOrder > Observations.Length)
            {
                throw new InvalidDataException("A native stack pointer requires its recorded writer.");
            }
            NativeBattleStackObservation writer = Observations[(int)cell.WriterOrder - 1];
            if (writer.Operation != NativeBattleStackOperation.PointerStore || writer.Offset != cell.Offset || writer.Pointer != cell.Pointer)
            {
                throw new InvalidDataException("A native stack pointer does not match its exact recorded store.");
            }
            for (int item = 0; item < 4; item++)
            {
                if (Known[index + item] || !occupied.Add(index + item))
                {
                    throw new InvalidDataException("Native symbolic pointers cannot overlap literal or other pointer bytes.");
                }
            }
        }
    }

    private void ValidateReplay()
    {
        byte[] bytes = new byte[Bytes.Length];
        bool[] known = new bool[Bytes.Length];
        ImmutableArray<NativeBattleStackPointerCell> pointers = [];
        foreach (NativeBattleStackObservation observation in Observations)
        {
            int index = Index(observation.Offset, observation.Length);
            for (int item = 0; item < observation.Length; item++)
            {
                bytes[index + item] = observation.Operation is NativeBattleStackOperation.LiteralStore or NativeBattleStackOperation.RegisterStore ? observation.Literal[item] : (byte)0;
                known[index + item] = observation.Operation == NativeBattleStackOperation.LiteralStore || (observation.Operation == NativeBattleStackOperation.RegisterStore && observation.RegisterKnownBytes!.Value[item]);
            }
            pointers = pointers.Where(cell => (long)cell.Offset + 4 <= observation.Offset || (long)observation.Offset + observation.Length <= cell.Offset).ToImmutableArray();
            if (observation.Operation == NativeBattleStackOperation.PointerStore)
            {
                pointers = pointers.Add(new(observation.Offset, observation.Pointer!, observation.Order));
            }
        }
        if (!Bytes.SequenceEqual(bytes) || !Known.SequenceEqual(known) || !Pointers.SequenceEqual(pointers))
        {
            throw new InvalidDataException("Native stack snapshot bytes/pointers do not match their ordered physical store history.");
        }
    }
    public NativeBattleStackBytes StoreUInt64(int offset, ulong value, uint instruction, string phase)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return StoreLiteral(offset, bytes, instruction, phase);
    }

    public NativeBattleStackBytes StoreUInt32(int offset, uint value, uint instruction, string phase)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return StoreLiteral(offset, bytes, instruction, phase);
    }

    public NativeBattleStackBytes StoreRegister32(int offset, uint value, byte knownByteMask, uint instruction, string phase)
    {
        if (knownByteMask > 15)
        {
            throw new InvalidDataException("A DWORD register known-byte mask contains only four byte selectors.");
        }
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        for (int item = 0; item < 4; item++)
        {
            known[item] = (knownByteMask & (1 << item)) != 0;
            if (!known[item])
            {
                bytes[item] = 0;
            }
        }
        return Apply(NativeBattleStackOperation.RegisterStore, offset, 4, instruction, phase, bytes.ToImmutableArray(), null, known.ToImmutableArray());
    }

    public NativeBattleStackBytes StoreLiteral(int offset, ReadOnlySpan<byte> value, uint instruction, string phase)
    {
        if (value.Length is not (1 or 2 or 4 or 8))
        {
            throw new InvalidDataException("Native stack literal stores require their actual byte/WORD/DWORD/QWORD width.");
        }
        return Apply(NativeBattleStackOperation.LiteralStore, offset, value.Length, instruction, phase, ImmutableArray.CreateRange(value.ToArray()), null);
    }

    public NativeBattleStackBytes StorePointer(int offset, NativeBattleStackPointer pointer, uint instruction, string phase)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        pointer.Validate();
        return Apply(NativeBattleStackOperation.PointerStore, offset, 4, instruction, phase, [], pointer);
    }

    public NativeBattleStackBytes Invalidate(int offset, int length, uint nativeCall, string phase)
    {
        return Apply(NativeBattleStackOperation.UnknownRange, offset, length, nativeCall, phase, [], null);
    }

    public ushort ReadUInt16(int offset)
    {
        Validate();
        int index = Index(offset, 2);
        if (!Known[index] || !Known[index + 1])
        {
            throw new NotSupportedException("Native stack WORD bytes are not authenticated numeric values.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(index, 2));
    }

    public uint ReadUInt32(int offset)
    {
        Validate();
        int index = Index(offset, 4);
        if (Enumerable.Range(index, 4).Any(item => !Known[item]))
        {
            throw new NotSupportedException("Native stack DWORD bytes are not authenticated numeric values.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(index, 4));
    }

    public bool EqualsUInt32(int offset, uint expected)
    {
        Validate();
        int index = Index(offset, 4);
        NativeBattleStackPointerCell? pointer = Pointers.SingleOrDefault(cell => cell.Offset == offset);
        if (pointer is not null && (expected == 0 || (expected & 3) != 0))
        {
            return false;
        }
        if (Enumerable.Range(index, 4).All(item => Known[item]))
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(index, 4)) == expected;
        }
        throw new NotSupportedException("Native stack comparison requires literal bytes or a proved pointer distinction.");
    }

    public bool IsNonzero(int offset) => !EqualsUInt32(offset, 0);

    private NativeBattleStackBytes Apply(NativeBattleStackOperation operation, int offset, int length, uint instruction, string phase, ImmutableArray<byte> literal, NativeBattleStackPointer? pointer, ImmutableArray<bool>? registerKnown = null)
    {
        Validate();
        int index = Index(offset, length);
        if (instruction == 0 || string.IsNullOrWhiteSpace(phase))
        {
            throw new InvalidDataException("Native stack updates require their actual instruction/call and callback phase.");
        }
        long order = checked((long)Observations.Length + 1);
        byte[] bytes = Bytes.ToArray();
        bool[] known = Known.ToArray();
        for (int item = 0; item < length; item++)
        {
            bytes[index + item] = operation is NativeBattleStackOperation.LiteralStore or NativeBattleStackOperation.RegisterStore ? literal[item] : (byte)0;
            known[index + item] = operation == NativeBattleStackOperation.LiteralStore || (operation == NativeBattleStackOperation.RegisterStore && registerKnown!.Value[item]);
        }
        ImmutableArray<NativeBattleStackPointerCell> cells = Pointers.Where(cell => (long)cell.Offset + 4 <= offset || (long)offset + length <= cell.Offset).ToImmutableArray();
        if (pointer is not null)
        {
            cells = cells.Add(new(offset, pointer, order));
        }
        NativeBattleStackObservation observation = new(order, operation, offset, length, instruction, phase, literal, pointer) { RegisterKnownBytes = registerKnown };
        return this with { Bytes = bytes.ToImmutableArray(), Known = known.ToImmutableArray(), Pointers = cells, Observations = Observations.Add(observation) };
    }

    private int Index(int offset, int length)
    {
        long index = (long)offset - FirstOffset;
        if (length <= 0 || index < 0 || index + length > Bytes.Length)
        {
            throw new InvalidDataException("Native stack operation lies outside its selected physical interval.");
        }
        return (int)index;
    }
}
