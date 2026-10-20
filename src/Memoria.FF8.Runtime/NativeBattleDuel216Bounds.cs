using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216BoundsStep(NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, short Maximum, short Minimum);

public static class NativeBattleDuel216Bounds
{
    public static NativeBattleDuel216BoundsStep Read695F60(NativeBattleFileStaging staging, NativeBattleGraphicsModel model, NativeBattlePoseSource source, ImmutableArray<NativeBattleRawMesh> geometry, Func<NativeBattleActionMatrix, NativeBattleRawVertex, short> projectIr2)
    {
        ArgumentNullException.ThrowIfNull(projectIr2);
        if (geometry.IsDefault)
        {
            throw new NotSupportedException("Native Duel216 bounds require the actual selected geometry directory.");
        }
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel216 bounds require the current world matrix.");
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel216 bounds require the actual packed position WORDs.");
        world = world with { X = transform.Position28.X, Y = transform.Position28.Y, Z = transform.Position28.Z };
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model with { GeometryWorldMatrix = world }, world);
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(staging, 24);
        uint address = temporary.Address;
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Duel216 bounds require the actual object mask.");
        int minimum = 65536;
        int maximum = -65536;
        staging = temporary.Staging.Write(address, BitConverter.GetBytes(minimum)).Write(address + 4, BitConverter.GetBytes(maximum)).Write(address + 8, new byte[4]).Write(address + 12, BitConverter.GetBytes((ushort)4096)).Write(address + 20, BitConverter.GetBytes(mask));
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        for (int meshIndex = 0; meshIndex < geometry.Length; meshIndex++)
        {
            if ((mask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            geometry[meshIndex].Validate();
            foreach (NativeBattleRawVertex vertex in geometry[meshIndex].Vertices)
            {
                if (vertex.Bone >= matrices.Length)
                {
                    throw new InvalidDataException("Native Duel216 bounds vertex has no current matrix.");
                }
                short y = projectIr2(matrices[vertex.Bone], vertex);
                staging = staging.Write(address + 16, BitConverter.GetBytes(y));
                if (minimum > y)
                {
                    minimum = y;
                    staging = staging.Write(address, BitConverter.GetBytes(minimum));
                }
                if (maximum < y)
                {
                    maximum = y;
                    staging = staging.Write(address + 4, BitConverter.GetBytes(maximum));
                }
            }
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 24);
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        return new(staging, model, unchecked((short)maximum), unchecked((short)minimum));
    }
}
