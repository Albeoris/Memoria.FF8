using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97CameraTaskStep(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState Camera, ImmutableArray<NativeBattleMoogleCameraStep> Publications, bool ReadyToComplete);

public static class NativeBattleChoco97CameraTask
{
    public const uint Callback = 0x72FBC0;

    public static NativeBattleChoco97CameraTaskStep Advance72FBC0(NativeBattleModulePoolTask task, NativeBattleMoogleCameraState? camera, NativeBattleChoco97CameraResources resources, short cameraMode, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8)
    {
        resources.Validate();
        if (task.Callback != Callback || task.Payload.IsDefault || task.PayloadLength != 0x70 - 12)
        {
            throw new InvalidDataException("Native Choco camera requires its complete original auxiliary task.");
        }
        ImmutableArray<NativeBattleMoogleCameraStep>.Builder publications = ImmutableArray.CreateBuilder<NativeBattleMoogleCameraStep>();
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                camera = NativeBattleMoogleCamera.Reset732AF0();
                Initialize(0, actorRaw8);
                NextPhase();
                break;
            case 1:
                if (Advance())
                {
                    NextPhase();
                }
                break;
            case 2:
                if (cameraMode >= 2)
                {
                    Initialize(1, actorRaw8);
                    NextPhase();
                }
                break;
            case 3:
                _ = Advance();
                if (cameraMode >= 4)
                {
                    Initialize(2, actorRaw8);
                    NextPhase();
                }
                break;
            case 4:
                if (Advance())
                {
                    Initialize(3, targetRaw8);
                    NextPhase();
                }
                break;
            case 5:
                _ = Advance();
                if (cameraMode >= 6)
                {
                    NextPhase();
                }
                break;
            case 6:
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                NextPhase();
                break;
            case 7:
                break;
            default:
                throw new NotSupportedException("Native Choco camera phase leaves its original eight callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, RequiredCamera(), publications.ToImmutable(), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        NativeBattleMoogleCameraState RequiredCamera() => camera ?? throw new NotSupportedException("Native Choco camera lost its retained process owner.");

        void Initialize(int row, ImmutableArray<byte> anchor)
        {
            Publish(NativeBattleMoogleCamera.Initialize732B10(RequiredCamera(), resources.Sources[row], anchor, 0));
        }

        bool Advance()
        {
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(RequiredCamera());
            Publish(step);
            return step.Complete;
        }

        void Publish(NativeBattleMoogleCameraStep step)
        {
            camera = step.State;
            publications.Add(step);
        }

        void NextPhase()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }
    }

    public static bool ModeAtLeast730E80(ushort current, short mode) => unchecked((short)current) >= mode;

    public static (ushort Requested, bool Accepted) RequestMode730EC0(ushort current, ushort requested, short mode)
    {
        bool accepted = requested == current && unchecked((short)current) == (int)mode - 1;
        return (accepted ? unchecked((ushort)mode) : requested, accepted);
    }
}
