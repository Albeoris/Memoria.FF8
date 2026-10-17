using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal void AdvanceMotion(int node, byte[] source)
    {
        int counter = unchecked((short)memory.Word(node + 0x1CA));
        memory.Copy(node + 0x194, node + 0xCC, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            ushort angle = unchecked((ushort)(memory.Word(node + 0xCC + axis * 2) + Curve(0xD8 + axis * 4)));
            memory.Word(node + 0xCC + axis * 2, (ushort)(angle & 0xFFF));
        }
        memory.Copy(node + 0xDC, node + 0x11C, 16);
        int count = unchecked((sbyte)memory.Byte(node + 0x1D8));
        switch (unchecked((sbyte)source[0x27]))
        {
            case 0:
                for (int instance = 0; instance < count; instance++)
                {
                    int scalar = node + 0x174 + instance * 4;
                    Add(scalar, memory.Dword(node + 0x184 + instance * 4));
                    short friction = unchecked((short)memory.Word(node + 0x1DA));
                    if (friction != 0)
                    {
                        memory.Dword(scalar, unchecked((uint)Damp880C00(friction, unchecked((int)memory.Dword(scalar)))));
                    }
                    (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(ReadMatrix(node + 0xC + instance * 32), 0, unchecked(-(int)memory.Dword(scalar)), 0);
                    int position = node + 0x12C + instance * 16;
                    Add(position, unchecked((uint)x));
                    Add(position + 4, unchecked((uint)y));
                    Add(position + 8, unchecked((uint)z));
                    int velocity = node + 0x220 + instance * 16;
                    int displacement = node + 0x1E0 + instance * 16;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Add(velocity + axis * 4, memory.Dword(node + 0x260 + instance * 16 + axis * 4));
                        Add(displacement + axis * 4, memory.Dword(velocity + axis * 4));
                    }
                    Add(displacement + 4, memory.Dword(node + 0x1DC));
                    for (int axis = 0; axis < 3; axis++)
                    {
                        if (friction != 0)
                        {
                            memory.Dword(displacement + axis * 4, unchecked((uint)Damp880C00(friction, unchecked((int)memory.Dword(displacement + axis * 4)))));
                        }
                        Add(position + axis * 4, memory.Dword(displacement + axis * 4));
                    }
                }
                break;
            case 1:
                for (int instance = 0; instance < count; instance++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        memory.Dword(node + 0x12C + instance * 16 + axis * 4, unchecked((uint)(Curve(0x13C + axis * 4) << 16)));
                    }
                    //8806C1 copies the parent matrix, then8DD770 overwrites every component used by56C600 with identity.
                }
                break;
        }
        NativeBattleActionMatrix? positionRotation = null;
        if (source[0x26] == 1)
        {
            int order = unchecked((sbyte)source[0x1D]);
            if (order is < 0 or > 3)
            {
                throw new NotSupportedException("Native8805A0 rotation selector requires the observed uninitialized temporary matrix for its out-of-range branch.");
            }
            NativeBattleActionMatrix matrix;
            if (order < 2)
            {
                uint parent = memory.Dword(node + 0x1BC);
                matrix = parent == 0 ? NativeBattleCuragaMatrices.Identity8DD770() : ReadMatrix(memory.Offset(parent) + 0x2C);
            }
            else
            {
                NativeBattleActionMatrix camera = inputs.CopiedCamera ?? throw new NotSupportedException("Native8805A0 camera rotation requires87DC50's actual1D97778 copy.");
                camera.Validate();
                matrix = camera with { Rotation = Enumerable.Range(0, 9).Select(index => camera.Rotation[index % 3 * 3 + index / 3]).ToImmutableArray() };
            }
            short ax = unchecked((short)memory.Word(node + 0xCC));
            short ay = unchecked((short)memory.Word(node + 0xCE));
            short az = unchecked((short)memory.Word(node + 0xD0));
            if ((order & 1) == 0)
            {
                matrix = Rotate(matrix, az, ax, ay, false);
            }
            else
            {
                matrix = Rotate(matrix, ay, ax, az, true);
            }
            positionRotation = matrix;
        }
        for (int instance = 0; instance < count; instance++)
        {
            int input = node + 0x12C + instance * 16;
            int output = node + 0xDC + instance * 16;
            switch (unchecked((sbyte)source[0x22]))
            {
                case 0:
                    if (positionRotation is null)
                    {
                        memory.Copy(input, output, 16);
                    }
                    else
                    {
                        (int x, int y, int z) = Point(input);
                        memory.Dword(output, unchecked((uint)x));
                        memory.Dword(output + 4, unchecked((uint)y));
                        memory.Dword(output + 8, unchecked((uint)z));
                        memory.MarkUnknown(output + 12, 4); //56C600 writes XYZ only, but880B83 copies all four temporary DWORDs.
                    }
                    break;
                case 1:
                    uint pointer = memory.Dword(node + 0x1BC);
                    if (pointer != 0)
                    {
                        (int x, int y, int z) = Point(input);
                        memory.Copy(memory.Offset(pointer) + 0x4C, output, 16);
                        Add(output, unchecked((uint)x));
                        Add(output + 4, unchecked((uint)y));
                        Add(output + 8, unchecked((uint)z));
                    }
                    break;
            }
        }
        if (source[0x1B] == 1)
        {
            for (int instance = 0; instance < count; instance++)
            {
                memory.Dword(node + 0xE0 + instance * 16, 0);
            }
        }
        short Curve(int field)
        {
            uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(field));
            return BinaryPrimitives.ReadInt16LittleEndian(bank.Read(unchecked(channel + (uint)(counter * 2)), 2));
        }
        void Add(int offset, uint value) => memory.Dword(offset, unchecked(memory.Dword(offset) + value));
        (int X, int Y, int Z) Point(int offset)
        {
            int x = unchecked((int)memory.Dword(offset));
            int y = unchecked((int)memory.Dword(offset + 4));
            int z = unchecked((int)memory.Dword(offset + 8));
            return positionRotation is null ? (x, y, z) : NativeBattleGfMatrices.RotateLong56C600(positionRotation, x, y, z);
        }
    }

    /// <summary>880C00/880C50 divide twice with signed truncation and keep the intervening DWORD product overflow.</summary>
    internal static int Damp880C00(int ratio, int value)
    {
        int product = unchecked(value / 256 * Math.Clamp(ratio, 0, 65536));
        return unchecked(value - product / 256);
    }

    private NativeBattleActionMatrix Rotate(NativeBattleActionMatrix matrix, short first, short x, short last, bool yFirst)
    {
        if (first != 0)
        {
            matrix = yFirst ? NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, first) : NativeBattleCuragaMatrices.RotateZ8DD960(matrix, first);
        }
        if (x != 0)
        {
            matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, x);
        }
        if (last != 0)
        {
            matrix = yFirst ? NativeBattleCuragaMatrices.RotateZ8DD960(matrix, last) : NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, last);
        }
        return matrix;
    }
}
