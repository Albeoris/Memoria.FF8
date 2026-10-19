using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215BoundsStep(NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, short Maximum, short Minimum);

public static class NativeBattleDuel215Bounds
{
    public static NativeBattleDuel215BoundsStep Read69D360(NativeBattleFileStaging staging, NativeBattleGraphicsModel model, NativeBattlePoseSource source, ImmutableArray<NativeBattleRawMesh> geometry)
    {
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel215 bounds require the current model world matrix.");
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel215 bounds require the actual raw model position WORDs.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 36);
        uint address = allocation.Address;
        staging = Dword(Dword(Dword(allocation.Staging, address + 8, unchecked((uint)world.X)), address + 12, unchecked((uint)world.Y)), address + 16, unchecked((uint)world.Z));
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, world with { X = transform.Position28.X, Y = transform.Position28.Y, Z = transform.Position28.Z });
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Duel215 bounds require the actual selected object mask.");
        int minimum = 65536;
        int maximum = -65536;
        staging = Dword(Dword(Dword(Dword(staging, address, unchecked((uint)minimum)), address + 4, unchecked((uint)maximum)), address + 32, mask), address + 20, 0);
        staging = staging.Write(address + 24, BitConverter.GetBytes((ushort)4096));
        for (int index = 0; index < geometry.Length; index++)
        {
            if ((mask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            foreach (NativeBattleRawVertex vertex in geometry[index].Vertices)
            {
                NativeBattleVector position = NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position);
                staging = staging.Write(address + 28, BitConverter.GetBytes(position.Y));
                if (position.Y < minimum)
                {
                    minimum = position.Y;
                    staging = Dword(staging, address, unchecked((uint)minimum));
                }
                if (position.Y > maximum)
                {
                    maximum = position.Y;
                    staging = Dword(staging, address + 4, unchecked((uint)maximum));
                }
            }
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 36);
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        return new(staging, model, unchecked((short)maximum), unchecked((short)minimum));
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
}
