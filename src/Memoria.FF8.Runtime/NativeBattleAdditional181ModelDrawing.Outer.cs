namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAdditional181ModelFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary128);
public sealed record NativeBattleAdditional181ModelServices(Func<NativeBattleAdditional181ModelFrame, NativeBattleAdditional181ModelFrame> Shadow5088A0, Func<NativeBattleAdditional181ModelFrame, NativeBattleActionMatrix, NativeBattleAdditional181ModelFrame> Compose5095B0, Func<NativeBattleAdditional181ModelFrame, NativeBattleAdditional181ModelFrame> DrawAndDeliverCommon, Func<NativeBattleAdditional181ModelFrame, NativeBattleAdditional181ModelFrame> Restore508C90, Func<NativeBattleAdditional181ModelFrame, NativeBattleAdditional181ModelFrame>? DrawLinked);

public static partial class NativeBattleAdditional181ModelDrawing
{
    public static NativeBattleAdditional181ModelFrame DrawOuter(NativeBattleAdditional181ModelFrame frame, NativeBattlePackedMatrix32 camera, uint scratchAddress, NativeBattleEffectArena primitiveFlags4, bool linkedPresent, NativeBattleAdditional181ModelServices services)
    {
        if ((frame.Model.Flags0 & 0x20) == 0)
        {
            frame = services.Shadow5088A0(frame);
        }
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Additional body target drawing requires its actual world matrix.");
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        NativeBattleEffectArena temporary = frame.Temporary128.Write(0x60, composed.Bytes.AsSpan()).MarkUnknown(0x72, 2);
        frame = services.Compose5095B0(frame with { Temporary128 = temporary }, world);
        temporary = frame.Temporary128;
        temporary = Dword(temporary, 4, scratchAddress);
        temporary = Dword(temporary, 0x1C, frame.Model.Color40 ?? throw new NotSupportedException("Additional body target drawing requires its actual full color DWORD."));
        temporary = Dword(temporary, 0x20, (frame.Model.SequenceState ?? throw new NotSupportedException("Additional body target drawing requires its current mesh mask.")).MeshMask124);
        temporary = temporary.Write(0x14, new byte[4]);
        byte alpha = frame.Model.Alpha7 ?? throw new NotSupportedException("Additional body target drawing requires its actual alpha BYTE.");
        temporary = temporary.Write(0x28, [alpha, alpha, alpha]);
        temporary = NativeBattleEffectNodeContextMemory.Copy(primitiveFlags4, 0, temporary, 0x10, 4);
        temporary = temporary.Write(0x18, BitConverter.GetBytes((ushort)320)).Write(0x1A, BitConverter.GetBytes((ushort)216));
        frame = services.DrawAndDeliverCommon(frame with { Temporary128 = temporary });
        frame = services.Restore508C90(frame);
        if (linkedPresent && (frame.Model.Flags0 & 0x200) == 0)
        {
            frame = frame with { Temporary128 = Dword(frame.Temporary128, 0x20, uint.MaxValue) };
            frame = (services.DrawLinked ?? throw new NotSupportedException("Additional body linked drawing requires its actual linked source, composition and local-pose restoration service."))(frame);
        }
        return frame;
    }
}
