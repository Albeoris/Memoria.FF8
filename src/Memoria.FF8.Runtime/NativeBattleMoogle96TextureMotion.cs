using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMoogle96TextureProgram(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleMoogle96TextureTable(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleMoogle96TextureStep(NativeBattleModulePoolTask Task, ImmutableArray<NativeBattleTextureCopy> Copies);

/// <summary>739890's retained WORD-pair program and739960's BYTE-directory atlas caller over the existing505EB0 queue.</summary>
public static class NativeBattleMoogle96TextureMotion
{
    public static NativeBattleMoogle96TextureStep Advance739890(NativeBattleModulePoolTask task, NativeBattleMoogle96TextureProgram? program, NativeBattleMoogle96TextureTable? table)
    {
        if (task.Payload.IsDefault || task.PayloadLength < 0x134 - 12)
        {
            throw new InvalidDataException("Native Moogle texture motion requires its complete retained model task fields.");
        }
        uint pointer = task.ReadDword(0x110);
        if (pointer == 0)
        {
            return new(task, []);
        }
        if (program is null || program.Address != pointer || program.Data.IsDefaultOrEmpty || (program.Data.Length & 3) != 0)
        {
            throw new NotSupportedException("Native Moogle texture program requires its exact bounded original owner.");
        }
        if (task.ReadWord(0x132) != 0)
        {
            task = task.WriteWord(0x132, unchecked((ushort)(task.ReadWord(0x132) - 1)));
            if (unchecked((short)task.ReadWord(0x132)) > 0)
            {
                return new(task, []);
            }
            task = task.WriteWord(0x12C, unchecked((ushort)(task.ReadWord(0x12C) + 1)));
        }
        ReadPair();
        ushort variant = task.ReadWord(0x130);
        if (variant == 0xFF)
        {
            return new(task.WriteDword(0x110, 0), []);
        }
        if (variant == 0xFE)
        {
            task = task.WriteWord(0x12C, 0);
            ReadPair();
            variant = task.ReadWord(0x130);
        }
        if (task.ReadDword(0xB4) == 0)
        {
            throw new NotSupportedException("Native Moogle texture motion requires its initialized model control owner.");
        }
        uint tablePointer = task.ReadDword(0x10C);
        if (tablePointer == 0)
        {
            return new(task, []);
        }
        if (table is null || table.Address != tablePointer)
        {
            throw new NotSupportedException("Native Moogle texture table requires its actual model section owner.");
        }
        NativeBattleTextureCopy? copy = Plan739960(table, task.ReadWord(0xDE), task.ReadWord(0x12E), variant);
        return new(task, copy is null ? [] : [copy]);

        void ReadPair()
        {
            int offset = task.ReadWord(0x12C) * 4;
            if (offset > program.Data.Length - 4)
            {
                throw new InvalidDataException("Native Moogle texture program index leaves its supplied WORD-pair owner.");
            }
            task = task.WriteWord(0x130, BinaryPrimitives.ReadUInt16LittleEndian(program.Data.AsSpan().Slice(offset))).WriteWord(0x132, BinaryPrimitives.ReadUInt16LittleEndian(program.Data.AsSpan().Slice(offset + 2)));
        }
    }

    public static NativeBattleTextureCopy? Plan739960(NativeBattleMoogle96TextureTable table, ushort allocationMask, ushort selector, ushort variant)
    {
        if (table.Address == 0 || table.Data.IsDefault || selector >= table.Data.Length)
        {
            throw new InvalidDataException("Native Moogle texture selector leaves its supplied BYTE directory owner.");
        }
        int offset = table.Data[selector];
        if (offset == 0)
        {
            return null;
        }
        long variantOffset = offset + 5L + variant * 2L;
        if (offset > table.Data.Length - 5 || variantOffset > table.Data.Length - 2)
        {
            throw new InvalidDataException("Native Moogle texture variant leaves its five-byte descriptor owner.");
        }
        int ordinal = table.Data[offset];
        int selected = -1;
        for (int slot = 0; slot < 12; slot++)
        {
            if ((allocationMask & (1 << slot)) != 0)
            {
                if (ordinal == 0)
                {
                    selected = slot;
                    break;
                }
                ordinal--;
            }
        }
        if (selected < 0)
        {
            throw new NotSupportedException("Native Moogle texture ordinal has no actual allocated slot among its twelve bits.");
        }
        int x = (13 - selected / 2) * 64;
        int y = 256 + (selected & 1) * 128;
        return new(unchecked((short)(x + table.Data[(int)variantOffset])), unchecked((short)(y + table.Data[(int)variantOffset + 1])), table.Data[offset + 3], table.Data[offset + 4], unchecked((short)(x + table.Data[offset + 1])), unchecked((short)(y + table.Data[offset + 2])));
    }
}
