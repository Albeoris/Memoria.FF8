namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid170ModelFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary172);
public sealed record NativeBattleAcid170ModelServices(Func<NativeBattleAcid170ModelFrame, NativeBattleAcid170ModelFrame> Shadow5088A0, Func<NativeBattleAcid170ModelFrame, NativeBattleActionMatrix, NativeBattleAcid170ModelFrame> Compose5095B0, Func<NativeBattleAcid170ModelFrame, NativeBattleAcid170ModelFrame> DrawAndDeliverCommon, Func<NativeBattleAcid170ModelFrame, NativeBattleAcid170ModelFrame> Restore508C90, Func<NativeBattleAcid170ModelFrame, NativeBattleAcid170ModelFrame>? DrawLinked);

public static partial class NativeBattleAcid170ModelDrawing
{
    public static NativeBattleAcid170ModelFrame DrawOuter(NativeBattleAcid170ModelFrame frame, NativeBattlePackedMatrix32 camera, uint scratchAddress, NativeBattleEffectArena primitiveFlags4, bool linkedPresent, NativeBattleAcid170ModelServices services)
    {
        if ((frame.Model.Flags0 & 0x20) == 0)
        {
            frame = services.Shadow5088A0(frame);
        }
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Acid body target drawing requires its actual world matrix.");
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        NativeBattleEffectArena temporary = frame.Temporary172.Write(0x8C, composed.Bytes.AsSpan()).MarkUnknown(0x9E, 2);
        frame = services.Compose5095B0(frame with { Temporary172 = temporary }, world);
        temporary = frame.Temporary172;
        temporary = Dword(temporary, 4, scratchAddress);
        temporary = Dword(temporary, 0x1C, frame.Model.Color40 ?? throw new NotSupportedException("Acid body target drawing requires its actual full color DWORD."));
        temporary = Dword(temporary, 0x20, (frame.Model.SequenceState ?? throw new NotSupportedException("Acid body target drawing requires its current mesh mask.")).MeshMask124);
        temporary = temporary.Write(0x14, new byte[4]);
        byte alpha = frame.Model.Alpha7 ?? throw new NotSupportedException("Acid body target drawing requires its actual alpha BYTE.");
        temporary = temporary.Write(0x28, [alpha, alpha, alpha]);
        temporary = NativeBattleEffectNodeContextMemory.Copy(primitiveFlags4, 0, temporary, 0x10, 4);
        temporary = temporary.Write(0x18, BitConverter.GetBytes((ushort)320)).Write(0x1A, BitConverter.GetBytes((ushort)216));
        frame = services.DrawAndDeliverCommon(frame with { Temporary172 = temporary.Write(0x8A, BitConverter.GetBytes((ushort)1)) });
        frame = services.Restore508C90(frame);
        if (linkedPresent && (frame.Model.Flags0 & 0x200) == 0)
        {
            frame = frame with { Temporary172 = Dword(frame.Temporary172, 0x20, uint.MaxValue).Write(0x8A, new byte[2]) };
            frame = (services.DrawLinked ?? throw new NotSupportedException("Acid body linked drawing requires its actual linked source, composition and local-pose restoration service."))(frame);
        }
        return frame;
    }
}
