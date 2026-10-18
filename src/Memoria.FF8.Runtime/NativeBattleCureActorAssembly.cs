using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>8D8280 replaces the shared actor context's model pointer and target selector.</summary>
public sealed record NativeBattleCureActorContext(byte Target);

public sealed record NativeBattleCureActorGeometry(NativeBattleRawMesh Mesh, NativeBattleModelTextureBinding Binding);

public static class NativeBattleCureActorAssembly
{
    public static NativeBattleCureActorPacketBatch Emit8D7110(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, NativeBattleActionMatrix copiedCamera, NativeBattleCameraProjection projection, NativeBattleVector center, short verticalOffset, short? contextF4)
    {
        return Emit(model, resource, copiedCamera, projection, center, verticalOffset, contextF4, NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 128, 128), 0x808080);
    }

    public static NativeBattleCureActorPacketBatch Emit88BDA0(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, NativeBattleActionMatrix copiedCamera, NativeBattleCameraProjection projection, NativeBattleVector center, short verticalOffset, short? contextF4)
    {
        return Emit(model, resource, copiedCamera, projection, center, verticalOffset, contextF4, NativeBattleCureActorPackets.TextureProjection8D7050(640, 448, 320, 244, 128, 64), 0x404040);
    }

    private static NativeBattleCureActorPacketBatch Emit(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, NativeBattleActionMatrix copiedCamera, NativeBattleCameraProjection projection, NativeBattleVector center, short verticalOffset, short? contextF4, NativeBattleCureTextureProjection texture, uint overlayColor)
    {
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native8D7110 requires the actual body skeleton and animation.");
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native8D7110 requires the current decoded body pose.");
        NativeBattleDecodedPose pose = source.Sample(sequence.Pose ?? throw new NotSupportedException("Native508C90 requires its selected body pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native508C90 requires its current body frame."), sequence.PoseFlags109);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
        NativeBattleCureActorMatrices matrices = NativeBattlePoseMatrices.CureActorMatrices8D7110(model.GeometryWorldMatrix ?? throw new NotSupportedException("Native8D7200 requires the previously published model matrix."), copiedCamera, new(0, verticalOffset, 0), 1, center, 8192);
        uint color = model.Color40 ?? throw new NotSupportedException("Native8D7430 requires the actual model color DWORD40.");
        int processBytes = 0;
        int effectBytes = 0;
        ImmutableArray<NativeBattleCureAllocatedPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleCureAllocatedPrimitive>();
        foreach (NativeBattleCureActorGeometry geometry in SelectGeometry8D7110(model, resource, sequence.MeshMask124))
        {
            Emit(geometry.Mesh, geometry.Binding);
        }
        return new(result.ToImmutable(), processBytes, effectBytes);

        void Emit(NativeBattleRawMesh mesh, NativeBattleModelTextureBinding? binding)
        {
            NativeBattleCureActorPacketBatch batch = NativeBattleCureActorPackets.Emit8D7430([mesh], bones, matrices.MainCamera, matrices.OverlayCamera, projection, 1, color, overlayColor, texture, contextF4 is short known ? unchecked((ushort)known) : null, binding ?? throw new NotSupportedException("Native8D7430 requires the executed507400 material relocation."), 17);
            foreach (NativeBattleCureAllocatedPrimitive allocation in batch.Primitives)
            {
                int offset = allocation.Bank == NativeBattleCurePacketBank.Process ? processBytes : effectBytes;
                result.Add(allocation with { ByteOffset = checked(allocation.ByteOffset + offset) });
            }
            processBytes = checked(processBytes + batch.ProcessAllocationBytes);
            effectBytes = checked(effectBytes + batch.EffectAllocationBytes);
        }
    }

    public static ImmutableArray<NativeBattleCureActorGeometry> SelectGeometry8D7110(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, uint mask)
    {
        ImmutableArray<NativeBattleRawMesh> body = resource.Geometry ?? throw new NotSupportedException("Native actor drawing requires its original body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native actor drawing requires observed linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native actor drawing requires its actual linked resource shape."));
        if (shared && (body.Length < 2 || linked.Length != 1))
        {
            throw new NotSupportedException("Native507E20 requires body directory slot1 and its single weapon object.");
        }
        ImmutableArray<NativeBattleCureActorGeometry>.Builder selected = ImmutableArray.CreateBuilder<NativeBattleCureActorGeometry>();
        for (int index = 0; index < body.Length; index++)
        {
            if ((mask & (1u << (index & 31))) != 0)
            {
                bool weapon = shared && index == 1;
                Add(weapon ? linked[0] : body[index], weapon ? model.LinkedTextureSlots : model.BodyTextureSlots);
            }
        }
        if (!shared)
        {
            for (int index = 0; index < linked.Length; index++)
            {
                if ((mask & (1u << (index & 31))) != 0)
                {
                    Add(linked[index], model.LinkedTextureSlots);
                }
            }
        }
        return selected.ToImmutable();

        void Add(NativeBattleRawMesh mesh, NativeBattleModelTextureBinding? binding)
        {
            selected.Add(new(mesh, binding ?? throw new NotSupportedException("Native actor drawing requires its executed material relocation.")));
        }
    }

}
