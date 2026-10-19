using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedCameraServices(Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> StartAnimation5099A0, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> Restore50A730)
{
    public Func<NativeBattleWaterExecutionFrame, NativeBattleCameraState>? ReadCamera { get; init; }
    public Func<NativeBattleWaterExecutionFrame, NativeBattleCameraState, NativeBattleWaterExecutionFrame>? PublishCamera { get; init; }
    public Func<NativeBattleWaterExecutionFrame, short, uint>? ResolveBoneB65370 { get; init; }
    public Func<NativeBattleWaterExecutionFrame, uint>? ReadGraphicsFlags { get; init; }
    public Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame>? PublishGraphicsFlags { get; init; }
}

/// <summary>Ifrit camera modes over the current bone, canonical camera and graphics flag owners.</summary>
public static class NativeBattleScriptedCameraCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedCameraServices services, NativeBattleWaterScriptProgram? program = null)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x39)
        {
            throw new NotSupportedException("Native cinematic camera command requires its selected instruction.");
        }
        switch (instruction >> 12)
        {
            case 1:
                uint file = CoreDword(frame, 0x2798A68);
                uint relative = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(file + 0x20), 4), 4));
                frame = services.StartAnimation5099A0(frame, unchecked(file + relative));
                break;
            case 4:
                NativeBattleCameraState camera = ReadCamera();
                NativeBattleCameraPose current = camera.CurrentPose ?? throw new NotSupportedException("Native cinematic camera capture requires its actual current eye and target.");
                short focal = camera.FocalLength ?? throw new NotSupportedException("Native cinematic camera capture requires its actual scalar scratch focal.");
                NativeBattleCameraState captured = camera.Capture503300() with { CapturedPose = current with { FocalLength = focal, Roll = camera.Roll } };
                frame = PublishCamera(captured);
                break;
            case 5:
            case 6:
            case 7:
                uint flags = (services.ReadGraphicsFlags ?? throw new NotSupportedException("Native cinematic graphics flag command requires its current DWORD owner."))(frame);
                flags = instruction >> 12 == 7 ? flags & 0xFFFFFEFFu : flags | 0x100u;
                frame = (services.PublishGraphicsFlags ?? throw new NotSupportedException("Native cinematic graphics flag command requires its canonical publisher."))(frame, flags);
                break;
            case 8:
                frame = services.Restore50A730(frame);
                break;
            case 2:
            case 3:
                throw new NotSupportedException("Native cinematic vibration modes require their current vibration process service.");
            default:
                uint sequence = CoreDword(frame, 0x27973C0);
                if ((frame.Core.Read(unchecked(sequence + 9), 1)[0] & 0x20) != 0)
                {
                    break;
                }
                byte[] position = ReadSlot(frame, 0x94, 6);
                NativeBattleCameraPoint eye = Point(position);
                short scratchFocal = unchecked((short)SlotWord(frame, 0x8C));
                short roll = unchecked((short)SlotWord(frame, 0x8E));
                NativeBattleWaterScriptProgram currentProgram = program ?? throw new NotSupportedException("Native cinematic bone camera requires its actual current operand program.");
                short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, currentProgram, 2, 2));
                uint target = (services.ResolveBoneB65370 ?? throw new NotSupportedException("Native cinematic bone camera requires its current physical selector service."))(frame, selector);
                NativeBattleCameraPoint targetPoint = Point(frame.Arena.Read(ArenaOffset(unchecked(target + 0x94), 6), 6));
                frame = PublishCamera(ReadCamera().WriteEffectGlobals(new(eye, targetPoint, scratchFocal, roll)));
                break;
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 4);

        NativeBattleCameraState ReadCamera() => (services.ReadCamera ?? throw new NotSupportedException("Native cinematic camera command requires its canonical camera owner."))(frame);
        NativeBattleWaterExecutionFrame PublishCamera(NativeBattleCameraState camera) => (services.PublishCamera ?? throw new NotSupportedException("Native cinematic camera command requires its canonical camera publisher."))(frame, camera);
    }

    private static NativeBattleCameraPoint Point(ReadOnlySpan<byte> raw) => new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(4)));
}
