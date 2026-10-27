using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100CameraTaskStep(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState Camera, NativeBattleChoco97Context Context, ImmutableArray<NativeBattleChoco97Operation> Operations, bool ReadyToComplete);

public static class NativeBattleChoco100CameraTask
{
    public const uint Callback = 0x7148C0;

    public static NativeBattleChoco100CameraTaskStep Advance7148C0(NativeBattleModulePoolTask task, NativeBattleMoogleCameraState? camera, NativeBattleChoco100CameraResources resources, NativeBattleChoco97Context context)
    {
        resources.Validate();
        context.Validate();
        if (task.Callback != Callback || task.PayloadLength != 0x70 - 12)
        {
            throw new InvalidDataException("Native Choco100 camera requires its complete original auxiliary task.");
        }
        ImmutableArray<NativeBattleChoco97Operation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleChoco97Operation>();
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                camera = NativeBattleMoogleCamera.Reset732AF0();
                Initialize(0, false);
                Next();
                break;
            case 1:
                if (Advance())
                {
                    Next();
                }
                break;
            case 2:
                if (AtLeast(2))
                {
                    Initialize(1, false);
                    Next();
                }
                break;
            case 3:
                _ = Advance();
                if (AtLeast(4))
                {
                    Initialize(2, false);
                    Next();
                }
                break;
            case 4:
                _ = Advance();
                if (AtLeast(5))
                {
                    Initialize(3, true);
                    Next();
                }
                break;
            case 5:
                if (AtLeast(6))
                {
                    Initialize(4, true);
                    Next();
                }
                break;
            case 6:
                _ = Advance();
                if (AtLeast(8))
                {
                    Initialize(5, true);
                    task = task.WriteWord(0x6C, 9);
                    Next();
                }
                break;
            case 7:
                _ = Advance();
                task = task.WriteWord(0x6C, unchecked((ushort)(task.ReadWord(0x6C) - 1)));
                if (unchecked((short)task.ReadWord(0x6C)) <= 0)
                {
                    Next();
                }
                break;
            case 8:
                _ = Advance();
                (ushort requested, bool accepted) = NativeBattleChoco97CameraTask.RequestMode730EC0(context.Word(0x40), context.Word(0x44), 9);
                if (accepted)
                {
                    context = context.WriteWord(0x44, requested);
                    Next();
                }
                break;
            case 9:
                _ = Advance();
                if (AtLeast(9))
                {
                    Initialize(6, true);
                    operations.Add(new NativeBattleChoco97Sound(0x1529800, 0, 128));
                    Next();
                }
                break;
            case 10:
                if (Advance())
                {
                    Initialize(7, true);
                    Next();
                }
                break;
            case 11:
                _ = Advance();
                if (AtLeast(10))
                {
                    Next();
                }
                break;
            case 12:
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                Next();
                break;
            case 13:
                break;
            default:
                throw new NotSupportedException("Native Choco100 camera phase leaves its fourteen original phases.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, RequiredCamera(), context, operations.ToImmutable(), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        NativeBattleMoogleCameraState RequiredCamera() => camera ?? throw new NotSupportedException("Native Choco100 camera lost its retained EC-byte owner.");
        bool AtLeast(short mode) => NativeBattleChoco97CameraTask.ModeAtLeast730E80(context.Word(0x42), mode);
        void Initialize(int row, bool target) => Publish(NativeBattleMoogleCamera.Initialize732B10(RequiredCamera(), resources.Sources[row], context.Data.AsSpan().Slice(target ? 8 : 0, 8).ToArray().ToImmutableArray(), 0));
        bool Advance()
        {
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(RequiredCamera());
            Publish(step);
            return step.Complete;
        }
        void Publish(NativeBattleMoogleCameraStep step)
        {
            camera = step.State;
            operations.Add(new NativeBattleChoco97CameraPublish(step));
        }
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }
}
