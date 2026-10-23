using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect298BodyFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary152);
public sealed record NativeBattleEffect298BodyServices(Func<NativeBattleEffect298BodyFrame, NativeBattleEffect298BodyFrame> Shadow5088A0, Func<NativeBattleEffect298BodyFrame, NativeBattleEffect298BodyFrame> ApplyPose662C00, Func<NativeBattleEffect298BodyFrame, NativeBattleEffect298BodyFrame> DrawAndDeliverCommon, Func<NativeBattleEffect298BodyFrame, NativeBattleEffect298BodyFrame> Restore508C90);

public static partial class NativeBattleEffect298BodyDrawing
{
    public static NativeBattleEffect298BodyFrame DrawOuter(NativeBattleEffect298BodyFrame frame, NativeBattlePackedMatrix32 camera, uint offsetAddress, NativeBattleEffectArena offsets, uint scratchAddress, NativeBattleEffect298BodyServices services)
    {
        frame = services.Shadow5088A0(frame);
        frame = services.ApplyPose662C00(frame);
        NativeBattleBodyBoneMatrices bones = frame.Model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Offset-body drawing requires its just-published current pose.");
        bones.Validate();
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Offset-body drawing requires its actual model world matrix.");
        ImmutableArray<NativeBattleActionMatrix>.Builder composed = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(bones.Matrices.Length);
        for (int bone = 0; bone < bones.Matrices.Length; bone++)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, bones.Matrices[bone]);
            matrix = matrix with { X = unchecked(matrix.X + Word(offsets, bone * 16)), Y = unchecked(matrix.Y + Word(offsets, bone * 16 + 2)), Z = unchecked(matrix.Z + Word(offsets, bone * 16 + 4)) };
            composed.Add(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), matrix));
        }
        frame = frame with { Model = frame.Model with { CurrentBodyBoneMatrices = bones with { Matrices = composed.MoveToImmutable() } } };
        NativeBattleEffectArena temporary = frame.Temporary152.Write(0x4C, BitConverter.GetBytes(offsetAddress)).Write(4, BitConverter.GetBytes(scratchAddress));
        temporary = temporary.Write(0xC, new byte[4]).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216));
        temporary = temporary.Write(0x14, BitConverter.GetBytes(frame.Model.Color40 ?? throw new NotSupportedException("Offset-body drawing requires its actual model color DWORD.")));
        temporary = temporary.Write(0x18, BitConverter.GetBytes((frame.Model.SequenceState ?? throw new NotSupportedException("Offset-body drawing requires its current mesh mask.")).MeshMask124));
        frame = services.DrawAndDeliverCommon(frame with { Temporary152 = temporary });
        return services.Restore508C90(frame);
    }

    private static short Word(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
}
