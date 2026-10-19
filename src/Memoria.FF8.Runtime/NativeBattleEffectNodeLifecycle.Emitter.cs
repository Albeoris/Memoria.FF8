using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal void InitializeEmitter(int node, ReadOnlySpan<byte> source)
    {
        memory.Byte(node + 0x68, source[0x11]);
        uint parentPointer = memory.Dword(node + 0x5C);
        sbyte orientation = unchecked((sbyte)source[0x1C]);
        int target = unchecked((sbyte)memory.Byte(node + 0x6B));
        switch (orientation)
        {
            case 0:
                WriteMatrix(node + 0x2C, NativeBattleCuragaMatrices.Identity8DD770());
                break;
            case 1:
            case 2:
                int dx = unchecked((int)(memory.ContextDword(0x1C4) - memory.ContextDword(0x54 + target * 16)));
                int dz = unchecked((int)(memory.ContextDword(0x1CC) - memory.ContextDword(0x5C + target * 16)));
                if (orientation == 2)
                {
                    dx = unchecked(-dx);
                    dz = unchecked(-dz);
                }
                short angle = unchecked((short)NativeWorldScriptedPathMath.NativeAngle(dx, dz));
                WriteMatrix(node + 0x2C, NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), angle));
                break;
            case 3:
                Copy(memory.Offset(parentPointer) + 0x8C, node + 0x2C, 32);
                break;
            case 4:
                short rotation = inputs.ReadModel(unchecked((short)memory.ContextWord(0x1E))).Rotation14 ?? throw new NotSupportedException("Native882990 orientation4 requires the actual caster model WORD14.");
                WriteMatrix(node + 0x2C, NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), rotation));
                break;
        }
        switch (unchecked((sbyte)memory.Byte(node + 0x68)))
        {
            case 0:
                SetAnchoredPosition(node, source, true);
                break;
            case 1:
                SetAnchoredPosition(node, source, false);
                break;
            case 2:
            case 3:
                FollowParent(node, source);
                break;
            case 4:
                NativeBattleEffectAnimatedAnchor.Advance(memory, node, source.ToArray(), bank.Read);
                break;
        }
        memory.Word(node + 0x64, unchecked((ushort)(short)(sbyte)source[0x12]));
    }

    private void SetAnchoredPosition(int node, ReadOnlySpan<byte> source, bool target)
    {
        int selector = unchecked((sbyte)source[0x25]);
        if (selector is >= 0 and <= 3)
        {
            int offset = target ? 0x94 + unchecked((sbyte)memory.Byte(node + 0x6B)) * 16 + selector * 0x40 : 0x1D4 + selector * 16;
            for (int component = 0; component < 4; component++)
            {
                memory.Dword(node + 0x4C + component * 4, memory.ContextDword(offset + component * 4));
            }
        }
        AddRotatedSourceOffset(node, source, target);
    }

    internal void FollowParent(int node, ReadOnlySpan<byte> source)
    {
        uint pointer = memory.Dword(node + 0x5C);
        if (pointer == 0)
        {
            return;
        }
        int parent = memory.Offset(pointer);
        if (source[0x35] == 1)
        {
            for (int component = 0; component < 3; component++)
            {
                int sum = unchecked((int)(memory.Dword(parent + 0x11C + component * 4) + memory.Dword(parent + 0xDC + component * 4)));
                memory.Dword(node + 0x4C + component * 4, unchecked((uint)(sum / 2)));
            }
        }
        else
        {
            Copy(parent + 0xDC, node + 0x4C, 16);
        }
        if (source[0x1C] == 3)
        {
            Copy(parent + 0x8C, node + 0x2C, 32);
            AddRotatedSourceOffset(node, source);
        }
    }

    private void AddRotatedSourceOffset(int node, ReadOnlySpan<byte> source, bool scaleTarget = false)
    {
        short x = unchecked((short)(BinaryPrimitives.ReadInt32LittleEndian(source) / 65536));
        short y = unchecked((short)(BinaryPrimitives.ReadInt32LittleEndian(source[4..]) / 65536));
        short z = unchecked((short)(BinaryPrimitives.ReadInt32LittleEndian(source[8..]) / 65536));
        NativeBattleActionMatrix placement = ReadMatrix(node + 0x2C);
        if (scaleTarget && inputs.ActorScale is { } actorScale)
        {
            placement = NativeBattlePoseMatrices.Scale56BEF0(placement, actorScale.X, actorScale.Y, actorScale.Z);
        }
        NativeBattleVector delta = NativeBattleCuragaMatrices.RotateVector45DF80(placement, new(x, y, z));
        memory.Dword(node + 0x4C, unchecked(memory.Dword(node + 0x4C) + (uint)(delta.X << 16)));
        memory.Dword(node + 0x50, unchecked(memory.Dword(node + 0x50) + (uint)(delta.Y << 16)));
        memory.Dword(node + 0x54, unchecked(memory.Dword(node + 0x54) + (uint)(delta.Z << 16)));
    }

    internal byte[] ReadSource(sbyte sourceIndex)
    {
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(unchecked(memory.ContextDword(0x224) + (uint)(sourceIndex * 4)), 4));
        return bank.Read(pointer, 0x158);
    }

    private NativeBattleActionMatrix ReadMatrix(int offset)
    {
        ImmutableArray<short>.Builder rotation = ImmutableArray.CreateBuilder<short>(9);
        for (int index = 0; index < 9; index++)
        {
            rotation.Add(unchecked((short)memory.Word(offset + index * 2)));
        }
        return new(rotation.ToImmutable(), unchecked((int)memory.Dword(offset + 20)), unchecked((int)memory.Dword(offset + 24)), unchecked((int)memory.Dword(offset + 28)), 4096);
    }

    private void WriteMatrix(int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            memory.Word(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        memory.Dword(offset + 20, unchecked((uint)matrix.X));
        memory.Dword(offset + 24, unchecked((uint)matrix.Y));
        memory.Dword(offset + 28, unchecked((uint)matrix.Z));
    }

    private void Copy(int source, int destination, int count)
    {
        memory.Copy(source, destination, count);
    }
}
