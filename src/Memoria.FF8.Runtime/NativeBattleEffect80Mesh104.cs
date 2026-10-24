using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect80Mesh104Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context104, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect80Mesh104
{
    public static NativeBattleEffect80Mesh104Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, Func<int, (NativeBattleVector Vertex, short? W)>? readSelectedVertex = null)
    {
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect80 requires current geometry registers.");
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect80 requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect80 requires current primitive controls.");
        byte[] packed = new byte[32];
        bool[] known = new bool[32];
        for (int index = 0; index < 20; index++)
        {
            if (geometry.Rotation20[index] is byte value)
            {
                packed[index] = value;
                known[index] = true;
            }
        }
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(20 + axis * 4), geometry.Translation[axis] ?? throw new NotSupportedException("Effect80 requires current translation registers."));
            Array.Fill(known, true, 20 + axis * 4, 4);
        }
        NativeBattlePackedMatrix32 matrix = new(packed.ToImmutableArray(), known.ToImmutableArray());
        short? padding = known[18] && known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(18)) : null;
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x14, 4));
        uint? selected = (flags & 0x2000) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) : null;
        NativeBattleActorWaveMeshStep drawn = NativeBattleActorWaveMesh.EmitBatch(mesh.Bytes, mesh.Address, matrix.ReadMatrix(), projection, context, new(geometry, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect80 requires current color registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Effect80 requires current far-color controls."), controls.Dqa, controls.Dqb, controls.Zsf4, constants, padding, controls.Zsf3, selectedVerticesAddress: selected, readSelectedVertex: readSelectedVertex);
        return new(memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } }, drawn.Context104, drawn.Batch);
    }
}
