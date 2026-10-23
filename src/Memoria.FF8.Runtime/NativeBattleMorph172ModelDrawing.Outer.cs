namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMorph172ModelFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary220);
public sealed record NativeBattleMorph172ModelServices(Func<NativeBattleMorph172ModelFrame, NativeBattleMorph172ModelFrame> Shadow5088A0, Func<NativeBattleMorph172ModelFrame, NativeBattleActionMatrix, NativeBattleMorph172ModelFrame> Compose5095B0, Func<NativeBattleMorph172ModelFrame, NativeBattleMorph172ModelFrame> DrawAndDeliverCommon, Func<NativeBattleMorph172ModelFrame, NativeBattleMorph172ModelFrame> Restore508C90, Func<NativeBattleMorph172ModelFrame, NativeBattleMorph172ModelFrame>? DrawLinked);

public static partial class NativeBattleMorph172ModelDrawing
{
    public static NativeBattleMorph172ModelFrame DrawOuter(NativeBattleMorph172ModelFrame frame, NativeBattlePackedMatrix32 camera, uint scratchAddress, NativeBattleEffectArena primitiveFlags4, bool linkedPresent, NativeBattleMorph172ModelServices services)
    {
        NativeBattleEffectArena temporary = frame.Temporary220;
        if ((frame.Model.Flags0 & 0x20) == 0)
        {
            frame = services.Shadow5088A0(frame);
        }
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Morph body requires its actual current world matrix.");
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        temporary = frame.Temporary220.Write(0xBC, composed.Bytes.AsSpan()).MarkUnknown(0xCE, 2);
        frame = services.Compose5095B0(frame with { Temporary220 = temporary }, world);
        temporary = frame.Temporary220;
        temporary = Dword(temporary, 4, scratchAddress);
        temporary = Dword(temporary, 0x1C, frame.Model.Color40 ?? throw new NotSupportedException("Morph body requires its actual full model color DWORD."));
        temporary = Dword(temporary, 0x20, (frame.Model.SequenceState ?? throw new NotSupportedException("Morph body requires its current mesh mask.")).MeshMask124);
        temporary = temporary.Write(0x14, new byte[4]);
        byte alpha = frame.Model.Alpha7 ?? throw new NotSupportedException("Morph body requires its actual alpha BYTE.");
        temporary = temporary.Write(0x28, [alpha, alpha, alpha]);
        temporary = NativeBattleEffectNodeContextMemory.Copy(primitiveFlags4, 0, temporary, 0x10, 4);
        temporary = temporary.Write(0x18, BitConverter.GetBytes((ushort)320)).Write(0x1A, BitConverter.GetBytes((ushort)216));
        frame = services.DrawAndDeliverCommon(frame with { Temporary220 = temporary });
        frame = services.Restore508C90(frame);
        if (linkedPresent && (frame.Model.Flags0 & 0x200) == 0)
        {
            frame = frame with { Temporary220 = Dword(frame.Temporary220, 0x20, uint.MaxValue) };
            frame = (services.DrawLinked ?? throw new NotSupportedException("Morph linked drawing requires its current camera-composed matrices, ordinary model renderer and local-pose restoration service."))(frame);
        }
        return frame;
    }
}
