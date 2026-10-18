using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>5C4F80's current control-directory selection and source-frame-to-fixed-tile505EB0 operands.</summary>
public static class NativeBattleShivaTextureCopy
{
    public static NativeBattleEffectVramCopy? Resolve5C4F80(NativeBattleCactuarRawModelState model, int selector, int frame, Func<uint, int, byte[]> readCurrentMemory)
    {
        ArgumentNullException.ThrowIfNull(readCurrentMemory);
        ArgumentOutOfRangeException.ThrowIfNegative(selector);
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        if (model.Model.IsDefault || model.Model.Length != 0x90 || model.Control.IsDefault || model.Control.Length != 0x34 || BinaryPrimitives.ReadUInt32LittleEndian(model.Model.AsSpan().Slice(0x84)) != NativeBattleShivaModelTask.Binding.ControlAddress)
        {
            throw new InvalidDataException("Native5C4F80 requires its actual current Shiva model/control binding.");
        }
        uint directory = BinaryPrimitives.ReadUInt32LittleEndian(model.Control.AsSpan().Slice(0x30));
        if (directory == 0)
        {
            return null;
        }
        byte rowOffset = ReadByte(checked(directory + (uint)selector));
        if (rowOffset == 0)
        {
            return null;
        }
        uint row = checked(directory + rowOffset);
        int rank = ReadByte(row);
        ushort mask = BinaryPrimitives.ReadUInt16LittleEndian(model.Control.AsSpan().Slice(2));
        int page = -1;
        for (int bit = 0; bit < 12; bit++)
        {
            if ((mask & (1 << bit)) == 0)
            {
                continue;
            }
            if (rank == 0)
            {
                page = bit;
                break;
            }
            rank--;
        }
        if (page < 0)
        {
            throw new NotSupportedException("Native5C4F80 row rank has no reached physical page among the current control's twelve bits.");
        }
        int pageX = (13 - page / 2) * 64;
        int pageY = (page & 1) * 128 + 256;
        short sourceX = checked((short)(pageX + ReadByte(checked(row + (uint)(frame * 2 + 5)))));
        short sourceY = checked((short)(pageY + ReadByte(checked(row + (uint)(frame * 2 + 6)))));
        short width = ReadByte(checked(row + 3));
        short height = ReadByte(checked(row + 4));
        short destinationY = checked((short)(pageY + ReadByte(checked(row + 2))));
        short destinationX = checked((short)(pageX + ReadByte(checked(row + 1))));
        return new(sourceX, sourceY, width, height, destinationX, destinationY);

        byte ReadByte(uint address)
        {
            byte[] bytes = readCurrentMemory(address, 1);
            if (bytes is not { Length: 1 })
            {
                throw new InvalidDataException("Native5C4F80 metadata reader did not return its exact current byte.");
            }
            return bytes[0];
        }
    }
}
