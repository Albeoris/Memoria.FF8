namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePoisonGas171ModelFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary176);
public sealed record NativeBattlePoisonGas171ModelServices(Func<NativeBattlePoisonGas171ModelFrame, NativeBattlePoisonGas171ModelFrame> Shadow5088A0, Func<NativeBattlePoisonGas171ModelFrame, NativeBattleActionMatrix, NativeBattlePoisonGas171ModelFrame> Compose5095B0, Func<NativeBattlePoisonGas171ModelFrame, NativeBattlePoisonGas171ModelFrame> DrawAndDeliverCommon, Func<NativeBattlePoisonGas171ModelFrame, NativeBattlePoisonGas171ModelFrame> Restore508C90, Func<NativeBattlePoisonGas171ModelFrame, NativeBattlePoisonGas171ModelFrame>? DrawLinked);

public static partial class NativeBattlePoisonGas171ModelDrawing
{
    public static NativeBattlePoisonGas171ModelFrame DrawOuter(NativeBattlePoisonGas171ModelFrame frame, NativeBattlePackedMatrix32 camera, uint scratchAddress, NativeBattleEffectArena primitiveFlags4, bool linkedPresent, NativeBattlePoisonGas171ModelServices services)
    {
        NativeBattleEffectArena temporary = frame.Temporary176;
        uint window = NativeBattleEffectTextureWindows.Encode(new(S16(temporary, 0x34), S16(temporary, 0x36), S16(temporary, 0x38), S16(temporary, 0x3A)));
        temporary = Dword(Dword(temporary, 0x88, window), 0x8C, 0xE2000000);
        temporary = Dword(temporary, 0x84, unchecked((uint)(unchecked((int)U32(temporary, 0x30)) % S16(temporary, 0x3A))));
        frame = frame with { Temporary176 = temporary };
        if ((frame.Model.Flags0 & 0x20) == 0)
        {
            frame = services.Shadow5088A0(frame);
        }
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Poison Gas body requires its actual current world matrix.");
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        temporary = frame.Temporary176.Write(0x90, composed.Bytes.AsSpan()).MarkUnknown(0xA2, 2);
        frame = services.Compose5095B0(frame with { Temporary176 = temporary }, world);
        temporary = frame.Temporary176;
        temporary = Dword(temporary, 4, scratchAddress);
        temporary = Dword(temporary, 0x1C, frame.Model.Color40 ?? throw new NotSupportedException("Poison Gas body requires its actual full model color DWORD."));
        temporary = Dword(temporary, 0x20, (frame.Model.SequenceState ?? throw new NotSupportedException("Poison Gas body requires its current mesh mask.")).MeshMask124);
        temporary = temporary.Write(0x14, new byte[4]);
        byte alpha = frame.Model.Alpha7 ?? throw new NotSupportedException("Poison Gas body requires its actual alpha BYTE.");
        temporary = temporary.Write(0x28, [alpha, alpha, alpha]);
        temporary = NativeBattleEffectNodeContextMemory.Copy(primitiveFlags4, 0, temporary, 0x10, 4);
        temporary = temporary.Write(0x18, BitConverter.GetBytes((ushort)320)).Write(0x1A, BitConverter.GetBytes((ushort)216));
        frame = services.DrawAndDeliverCommon(frame with { Temporary176 = temporary });
        frame = services.Restore508C90(frame);
        if (linkedPresent && (frame.Model.Flags0 & 0x200) == 0)
        {
            frame = frame with { Temporary176 = Dword(frame.Temporary176, 0x20, uint.MaxValue) };
            frame = (services.DrawLinked ?? throw new NotSupportedException("Poison Gas linked drawing requires its current camera-composed matrices, ordinary model renderer and local-pose restoration service."))(frame);
        }
        return frame;
    }
}
