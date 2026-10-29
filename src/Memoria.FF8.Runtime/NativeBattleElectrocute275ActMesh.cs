using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleElectrocute275ActMeshStep(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Temporary88, NativeBattleEffectArena Arena, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattleElectrocute275ActMesh
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleElectrocute275ActMeshStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context12, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena temporary88, NativeBattleEffectArena arena, uint morphAddress, int morphLength, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf3, short? zsf4, NativeBattleSummonColors constants, short? localVectorW = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleAnchorActPreparationStep prepared = NativeBattleAnchorActPreparation.Prepare(source, node, context12, camera, temporary88, arena, morphAddress, morphLength, registers, constants, NativeBattleAnchorActContextPolicy.Electrocute275, localVectorW);
        int geometry = prepared.GeometryOffset;
        uint shapeAddress = checked(source.Address + (uint)geometry);
        uint selectedAddress = prepared.SelectedVerticesAddress;
        NativeBattleActionMatrix matrix = prepared.Matrix;
        temporary88 = prepared.Temporary88;
        arena = prepared.Arena;
        registers = prepared.Registers;
        int end = checked(geometry + I32(source.Data, geometry));
        foreach (int stride in new[] { 12, 12, 20, 24, 20, 24, 28, 36 })
        {
            int faces = I32(source.Data, end);
            if (faces < 0)
            {
                throw new InvalidDataException("Electrocute ACT has a negative primitive count.");
            }
            end = checked(end + 4 + faces * stride);
        }
        ImmutableArray<byte> shape = Slice(source.Data, geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        NativeBattleCuraga88MeshStep drawn = NativeBattleCuraga88Mesh.EmitBatch(shape, shapeAddress, matrix, projection, temporary88, registers, farControls, dqa, dqb, zsf4, constants, zsf3: zsf3, selectedVerticesAddress: selectedAddress, readSelectedVertex: prepared.ReadVertex, pilotIdentity: pilotIdentity);
        return new(drawn.Batch, drawn.Context88, arena, drawn.Registers, drawn.FarControls);
    }
    private static int I32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Slice(bytes, offset, 4));
    private static ReadOnlySpan<byte> Slice(ImmutableArray<byte> bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bytes.Length - count)
        {
            throw new InvalidDataException("Electrocute ACT read leaves its original source.");
        }
        return bytes.AsSpan().Slice(offset, count);
    }
}
