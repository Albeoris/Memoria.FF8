using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

internal static class Ff7KernelBattleAi
{
    public const int IdentityCount = 12;
    private const int Directory = 0x61C;

    public static ReadOnlyMemory<byte> ReadScript(ReadOnlyMemory<byte> kernel, int identity, byte callback)
    {
        if (kernel.Length != 3988 || identity is < 0 or >= IdentityCount || callback >= 16)
        {
            throw new InvalidDataException("FFVII party AI requires its original battle kernel and logical callback owner.");
        }
        ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(Directory + identity * 2)..]);
        if (offset == ushort.MaxValue)
        {
            return default;
        }
        int bank = Directory + offset;
        int end = kernel.Length;
        for (int id = 0; id < IdentityCount; id++)
        {
            int candidate = Directory + BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(Directory + id * 2)..]);
            if (candidate > bank && candidate < end)
            {
                end = candidate;
            }
        }
        if (bank < Directory + IdentityCount * 2 || bank + 32 > end)
        {
            throw new InvalidDataException("FFVII party AI bank directory is invalid.");
        }
        ushort script = BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(bank + callback * 2)..]);
        if (script == ushort.MaxValue)
        {
            return default;
        }
        int start = bank + script;
        for (int index = 0; index < 16; index++)
        {
            ushort other = BinaryPrimitives.ReadUInt16LittleEndian(kernel.Span[(bank + index * 2)..]);
            if (other != ushort.MaxValue && bank + other > start && bank + other < end)
            {
                end = bank + other;
            }
        }
        if (script < 32 || start >= end)
        {
            throw new InvalidDataException("FFVII party AI callback exceeds its bank.");
        }
        return kernel[start..end];
    }

    public static byte[] InitialAliases()
    {
        return Enumerable.Repeat(byte.MaxValue, IdentityCount).ToArray();
    }

    public static int ResolveIdentity(int identity, IReadOnlyList<byte> aliases)
    {
        if (identity is < 0 or >= IdentityCount || aliases.Count != IdentityCount)
        {
            throw new InvalidDataException("FFVII party AI alias lookup exceeds its native owner.");
        }
        return aliases[identity] == byte.MaxValue ? identity : aliases[identity];
    }

    public static byte[] RestoreDeclaredAliases(ReadOnlyMemory<byte> kernel, IEnumerable<int> initializedIdentities)
    {
        byte[] aliases = InitialAliases();
        foreach (int identity in initializedIdentities.Distinct())
        {
            ReadOnlySpan<byte> script = ReadScript(kernel, identity, 0).Span;
            if (!InitializerContainsAlias(script))
            {
                continue;
            }
            Stack<uint> constants = new();
            byte? declared = null;
            bool resolved = false;
            int pc = 0;
            while (pc < script.Length)
            {
                byte opcode = script[pc++];
                if (opcode is >= 0x60 and <= 0x62)
                {
                    int width = opcode - 0x5F;
                    if (script.Length - pc < width)
                    {
                        break;
                    }
                    uint value = 0;
                    for (int index = 0; index < width; index++)
                    {
                        value |= (uint)script[pc++] << (index * 8);
                    }
                    constants.Push(value);
                }
                else if (opcode == 0x75 && constants.TryPop(out uint linkedValue))
                {
                    declared = unchecked((byte)linkedValue);
                }
                else if (opcode == 0x73)
                {
                    if (declared is byte alias)
                    {
                        aliases[identity] = alias;
                        resolved = true;
                    }
                    break;
                }
                else
                {
                    break;
                }
            }
            if (!resolved)
            {
                throw new InvalidDataException("FFVII party AI requires its retained alias state for a non-declarative initializer.");
            }
        }
        return aliases;
    }

    private static bool InitializerContainsAlias(ReadOnlySpan<byte> script)
    {
        bool alias = false;
        int pc = 0;
        while (pc < script.Length)
        {
            byte opcode = script[pc++];
            if (opcode == 0x73)
            {
                return alias;
            }
            int width = opcode switch
            {
                <= 3 or >= 0x10 and <= 0x13 or >= 0x70 and <= 0x72 => 2,
                >= 0x60 and <= 0x62 => opcode - 0x5F,
                0x75 or 0x80 or 0x90 or 0x91 => 0,
                _ => throw new InvalidDataException("FFVII party AI initializer cannot restore absent alias metadata from an unsupported declaration.")
            };
            alias |= opcode == 0x75;
            if (script.Length - pc < width)
            {
                throw new InvalidDataException("FFVII party AI initializer declaration is truncated.");
            }
            pc += width;
        }
        if (script.IsEmpty)
        {
            return false;
        }
        throw new InvalidDataException("FFVII party AI initializer declaration has no return.");
    }
}
