using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco98CameraTaskStep(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState Camera, ImmutableArray<NativeBattleMoogleCameraStep> Publications, bool ReadyToComplete);

public static class NativeBattleChoco98CameraTask
{
    public const uint Callback = 0x7276F0;

    public static NativeBattleChoco98CameraTaskStep Advance7276F0(NativeBattleModulePoolTask task, NativeBattleMoogleCameraState? camera, NativeBattleChoco98CameraResources resources, short cameraMode42, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8)
    {
        resources.Validate();
        if (task.Callback != Callback || task.PayloadLength != 0x70 - 12)
        {
            throw new InvalidDataException("Native Choco camera requires its complete original auxiliary task.");
        }
        ImmutableArray<NativeBattleMoogleCameraStep>.Builder publications = ImmutableArray.CreateBuilder<NativeBattleMoogleCameraStep>();
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                camera = NativeBattleMoogleCamera.Reset732AF0();
                Initialize(0, actorRaw8);
                Next();
                break;
            case 1:
                if (Advance())
                {
                    Next();
                }
                break;
            case 2:
                if (cameraMode42 >= 2)
                {
                    Initialize(1, actorRaw8);
                    Next();
                }
                break;
            case 3:
                _ = Advance();
                if (cameraMode42 >= 4)
                {
                    Initialize(2, actorRaw8);
                    Next();
                }
                break;
            case 4:
                if (Advance())
                {
                    Initialize(3, targetRaw8);
                    Next();
                }
                break;
            case 5:
                if (Advance())
                {
                    Initialize(4, targetRaw8);
                    Next();
                }
                break;
            case 6:
                _ = Advance();
                if (cameraMode42 >= 6)
                {
                    Next();
                }
                break;
            case 7:
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                Next();
                break;
            case 8:
                break;
            default:
                throw new NotSupportedException("Native Choco camera phase leaves its nine original callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, RequiredCamera(), publications.ToImmutable(), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        NativeBattleMoogleCameraState RequiredCamera() => camera ?? throw new NotSupportedException("Native Choco camera lost its retained process state.");
        void Initialize(int row, ImmutableArray<byte> anchor) => Publish(NativeBattleMoogleCamera.Initialize732B10(RequiredCamera(), resources.Sources[row], anchor, 0, resources.Curve));
        bool Advance()
        {
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(RequiredCamera(), resources.Curve);
            Publish(step);
            return step.Complete;
        }
        void Publish(NativeBattleMoogleCameraStep step)
        {
            camera = step.State;
            publications.Add(step);
        }
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }
}
