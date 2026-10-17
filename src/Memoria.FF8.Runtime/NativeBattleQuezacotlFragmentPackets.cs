using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>6C6660 draws the current12AC8B8 mesh before changing its44-byte task's motion or age.</summary>
public static class NativeBattleQuezacotlFragmentPackets
{
    public const uint MeshAddress = 0x12AC8B8;

    public static NativeBattleGfGeometryBatch Emit6C6660(NativeBattleEffectMemory memory, ImmutableArray<byte> task44, NativeBattleCameraProjection projection, Action<ushort>? observeDepth = null)
    {
        NativeBattleQuezacotlExecutableMemory executable = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native6C6660 requires its retained current executable mesh bytes.");
        executable.Validate();
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6C6660 requires its previously published1D97778 camera matrix.");
        NativeBattleActionMatrix matrix = Compose6C6660(task44, view);
        int offset = checked((int)(MeshAddress - NativeBattleQuezacotlExecutableMemory.Address));
        return NativeBattleModulePackets.EmitFlatTriangles572200(executable.Data, offset, matrix, projection, 17, 2, observeDepth);
    }

    public static NativeBattleActionMatrix Compose6C6660(ImmutableArray<byte> task44, NativeBattleActionMatrix view)
    {
        if (task44.IsDefault || task44.Length != 0x2C)
        {
            throw new InvalidDataException("Native6C6660 requires its actual44-byte physical task.");
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(Word(0x20), Word(0x22), Word(0x24)));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(view, local);
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, Word(0x10), Word(0x12), Word(0x14));
        return rotation with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };

        short Word(int offset)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(task44.AsSpan().Slice(offset, 2));
        }
    }
}
