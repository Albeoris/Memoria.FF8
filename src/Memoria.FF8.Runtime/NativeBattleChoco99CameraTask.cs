using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99CameraTaskStep(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState Camera, ImmutableArray<NativeBattleMoogleCameraStep> Publications, bool ReadyToComplete);

public static class NativeBattleChoco99CameraTask
{
    public const uint Callback = 0x71DC40;

    public static NativeBattleChoco99CameraTaskStep Advance71DC40(NativeBattleModulePoolTask task, NativeBattleMoogleCameraState? camera, NativeBattleChoco99CameraResources resources, short cameraMode42, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8)
    {
        resources.Validate();
        if (task.Callback != Callback || task.PayloadLength != 0xB0 - 12)
        {
            throw new InvalidDataException("Native Choco99 camera requires its complete B0-byte auxiliary task.");
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
                _ = Advance();
                if (cameraMode42 >= 5)
                {
                    // The fourth native temporary WORD is unconsumed; zero represents only that padding.
                    Initialize(3, new byte[8].ToImmutableArray());
                    Next();
                }
                break;
            case 5:
                _ = Advance();
                if (cameraMode42 >= 8)
                {
                    Initialize(4, new byte[8].ToImmutableArray());
                    Next();
                }
                break;
            case 6:
                Set(0x86, Word(0x86) + 16);
                if (Word(0x86) >= 128)
                {
                    Set(0x86, 128);
                }
                Set(0x5E, Word(0x5E) + Word(0x86));
                Set(0x8C, Word(0x8C) + 1);
                if (Word(0x8C) >= 32)
                {
                    Set(0x8C, 32);
                }
                Set(0xD8, (Word(0xD8) + Word(0x8C)) & 4095);
                if (Word(0xD8) >= 0 && Word(0xD8) < 2048)
                {
                    Set(0xD8, 0);
                }
                Publish(NativeBattleMoogleCamera.RecomputePublication(RequiredCamera()));
                if (cameraMode42 >= 10)
                {
                    Initialize(5, targetRaw8);
                    Next();
                }
                break;
            case 7:
                if (Advance())
                {
                    Initialize(6, targetRaw8);
                    Set(0x8E, 6);
                    Next();
                }
                break;
            case 8:
                _ = Advance();
                if (Word(0xE2) >= 66)
                {
                    Set(0x8E, Word(0x8E) - 1);
                    if (Word(0x8E) < 0)
                    {
                        Set(0x8E, 0);
                    }
                }
                Set(0xDA, (Word(0xDA) + Word(0x8E)) & 4095);
                Publish(NativeBattleMoogleCamera.RecomputePublication(RequiredCamera()));
                if (cameraMode42 >= 11)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                break;
            case 9:
                break;
            default:
                throw new NotSupportedException("Native Choco99 camera phase leaves its ten original phases.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, RequiredCamera(), publications.ToImmutable(), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        NativeBattleMoogleCameraState RequiredCamera() => camera ?? throw new NotSupportedException("Native Choco99 camera lost its retained EC-byte state.");
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(RequiredCamera().Data.AsSpan().Slice(offset));
        void Set(int offset, int value)
        {
            byte[] bytes = RequiredCamera().Data.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), unchecked((ushort)value));
            camera = RequiredCamera() with { Data = bytes.ToImmutableArray() };
        }
        void Initialize(int row, ImmutableArray<byte> anchor) => Publish(NativeBattleMoogleCamera.Initialize732B10(RequiredCamera(), resources.Sources[row], anchor, 0));
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
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }
}
