namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDegenerator306ModelFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary136);
public sealed record NativeBattleDegenerator306ModelServices(Func<NativeBattleDegenerator306ModelFrame, NativeBattleDegenerator306ModelFrame> Shadow5088A0, Func<NativeBattleDegenerator306ModelFrame, NativeBattleActionMatrix, NativeBattleDegenerator306ModelFrame> Compose5095B0, Func<NativeBattleDegenerator306ModelFrame, NativeBattleDegenerator306ModelFrame> DrawAndDeliverCommon, Func<NativeBattleDegenerator306ModelFrame, NativeBattleDegenerator306ModelFrame> Restore508C90, Func<NativeBattleDegenerator306ModelFrame, NativeBattleDegenerator306ModelFrame>? DrawLinked);

public static partial class NativeBattleDegenerator306ModelDrawing
{
    public static NativeBattleDegenerator306ModelFrame DrawOuter(NativeBattleDegenerator306ModelFrame frame, NativeBattlePackedMatrix32 camera, uint scratchAddress, NativeBattleEffectArena primitiveFlags4, bool linkedPresent, NativeBattleDegenerator306ModelServices services)
    {
        if ((frame.Model.Flags0 & 0x20) == 0)
        {
            frame = services.Shadow5088A0(frame);
        }
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Degenerator target drawing requires its actual world matrix.");
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        NativeBattleEffectArena temporary = frame.Temporary136.Write(0x68, composed.Bytes.AsSpan()).MarkUnknown(0x7A, 2);
        frame = services.Compose5095B0(frame with { Temporary136 = temporary }, world);
        temporary = frame.Temporary136;
        temporary = Dword(temporary, 4, scratchAddress);
        temporary = Dword(temporary, 0x1C, frame.Model.Color40 ?? throw new NotSupportedException("Degenerator target drawing requires its actual full color DWORD."));
        temporary = Dword(temporary, 0x20, (frame.Model.SequenceState ?? throw new NotSupportedException("Degenerator target drawing requires its current mesh mask.")).MeshMask124);
        temporary = temporary.Write(0x14, new byte[4]);
        byte alpha = frame.Model.Alpha7 ?? throw new NotSupportedException("Degenerator target drawing requires its actual alpha BYTE.");
        temporary = temporary.Write(0x28, [alpha, alpha, alpha]);
        temporary = NativeBattleEffectNodeContextMemory.Copy(primitiveFlags4, 0, temporary, 0x10, 4);
        temporary = temporary.Write(0x18, BitConverter.GetBytes((ushort)320)).Write(0x1A, BitConverter.GetBytes((ushort)216));
        frame = services.DrawAndDeliverCommon(frame with { Temporary136 = temporary });
        frame = services.Restore508C90(frame);
        if (linkedPresent && (frame.Model.Flags0 & 0x200) == 0)
        {
            frame = frame with { Temporary136 = Dword(frame.Temporary136, 0x20, uint.MaxValue) };
            frame = (services.DrawLinked ?? throw new NotSupportedException("Degenerator linked drawing requires its actual linked source, composition and local-pose restoration service."))(frame);
        }
        return frame;
    }
}
