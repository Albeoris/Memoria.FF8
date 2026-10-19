using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSiren95LayerProgrammes
{
    public static NativeBattleSiren95TaskStep Advance(NativeBattleSiren95Frame frame, uint address, NativeBattleGfAnimationSource source, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95ProgramAdvance> draw, Func<NativeBattleActionMatrix>? readCameraView = null)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        if (callback is not (0x73EF60 or 0x73F110 or 0x73F6A0 or 0x73F250 or 0x73F370 or 0x73F500) || task.ReadDword(0x74) != source.Address)
        {
            throw new InvalidDataException("Native Siren layer requires its current callback and source pointer.");
        }
        bool facingLayer = callback is 0x73F250 or 0x73F370 or 0x73F500;
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (phase == 0)
        {
            Initialize();
            uint scale = callback == 0x73F110 ? 0x3000u : facingLayer && callback != 0x73F500 ? 0x600u : 4096;
            task = task.WriteDword(0x50, scale).WriteDword(0x54, scale).WriteDword(0x58, scale).WriteWord(0x1C, 0).WriteWord(0x1E, callback is 0x73F110 or 0x73F500 ? (ushort)0xF800 : facingLayer ? (ushort)0xFC00 : (ushort)0).WriteWord(0x20, callback == 0x73F110 || facingLayer ? (ushort)0xCC0 : (ushort)0);
            if (callback == 0x73F110 || facingLayer)
            {
                task = task.WriteWord(0x88, unchecked((ushort)-128));
            }
            if (callback == 0x73EF60)
            {
                Draw();
            }
            Next();
        }
        else if (callback == 0x73EF60)
        {
            switch (phase)
            {
                case 1:
                    if (Draw() == 0)
                    {
                        Complete();
                    }
                    else if (Spawned() >= 5)
                    {
                        Next();
                    }
                    break;
                case 2:
                    int fade = unchecked((int)task.ReadDword(0x7C) + 256);
                    task = task.WriteDword(0x7C, unchecked((uint)(fade >= 2048 ? 2048 : fade)));
                    if (Draw() == 0)
                    {
                        Complete();
                    }
                    else if (Spawned() >= 8)
                    {
                        Next();
                    }
                    break;
                case 3:
                    if (Draw() == 0)
                    {
                        Complete();
                    }
                    else
                    {
                        fade = unchecked((int)task.ReadDword(0x7C) + 256);
                        task = task.WriteDword(0x7C, unchecked((uint)(fade >= 4096 ? 4096 : fade)));
                        if (fade >= 4096)
                        {
                            Complete();
                        }
                    }
                    break;
                case 4:
                    break;
                default:
                    Unsupported();
                    break;
            }
        }
        else if (callback == 0x73F110)
        {
            switch (phase)
            {
                case 1:
                    if (unchecked((short)task.ReadWord(0x24)) >= 24)
                    {
                        Next();
                    }
                    break;
                case 2:
                    if (Spawned() >= 4)
                    {
                        task = task.WriteWord(0x1E, 0xFC00).WriteDword(0x50, 0x600).WriteDword(0x54, 0x600).WriteDword(0x58, 0x600);
                        Next();
                    }
                    Draw();
                    break;
                case 3:
                    if (Draw() == 0)
                    {
                        Complete();
                    }
                    break;
                case 4:
                    break;
                default:
                    Unsupported();
                    break;
            }
        }
        else if (callback == 0x73F6A0)
        {
            switch (phase)
            {
                case 1:
                    if (unchecked((short)task.ReadWord(0x24)) >= 30)
                    {
                        Next();
                    }
                    break;
                case 2:
                    if (Draw() == 0)
                    {
                        Next();
                    }
                    break;
                case 3:
                    if (unchecked((short)task.ReadWord(0x24)) >= 137)
                    {
                        Initialize();
                        Next();
                    }
                    break;
                case 4:
                    if (Draw() == 0 || Spawned() >= 6)
                    {
                        Complete();
                    }
                    break;
                case 5:
                    break;
                default:
                    Unsupported();
                    break;
            }
        }
        else
        {
            switch (phase)
            {
                case 1:
                    int delay = callback == 0x73F250 ? 50 : callback == 0x73F370 ? 80 : 130;
                    if (unchecked((short)task.ReadWord(0x24)) >= delay)
                    {
                        Next();
                    }
                    break;
                case 2:
                    FaceCamera();
                    if (callback == 0x73F250)
                    {
                        if (Spawned() >= 5 || Draw() == 0)
                        {
                            Complete();
                        }
                    }
                    else
                    {
                        if (Spawned() >= (callback == 0x73F370 ? 5 : 6))
                        {
                            if (callback == 0x73F370)
                            {
                                task = task.WriteWord(0x1C, 0).WriteWord(0x1E, 0xF800).WriteWord(0x20, 0xCC0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
                            }
                            else
                            {
                                NativeBattleSiren95State shared = frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren facing layer requires its director owner.");
                                uint director = shared.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren facing layer requires its director pointer.");
                                task = task.WriteDword(0x1C, shared.ReadDword(director + 8)).WriteDword(0x20, shared.ReadDword(director + 12)).WriteWord(0x1E, 0xFA00).WriteDword(0x50, 8192).WriteDword(0x54, 8192).WriteDword(0x58, 8192);
                            }
                            Next();
                        }
                        Draw();
                    }
                    break;
                case 3 when callback != 0x73F250:
                    FaceCamera();
                    if (Spawned() >= (callback == 0x73F370 ? 6 : 9) || Draw() == 0)
                    {
                        Complete();
                    }
                    break;
                case 3:
                case 4 when callback != 0x73F250:
                    break;
                default:
                    Unsupported();
                    break;
            }
        }
        if (callback == 0x73EF60)
        {
            task = task.WriteWord(0x8C, (ushort)(unchecked((byte)(task.ReadByte(0x8C) - 1)) & 127));
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Write(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        int Draw()
        {
            NativeBattleSiren95ProgramAdvance result = draw(Write(frame, address, task), address);
            frame = result.Frame;
            task = Read(frame, address);
            return result.Remaining;
        }
        void Initialize()
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(0x94 - 12).ToArray().ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, unchecked((int)task.ReadDword(0x78)));
            byte[] payload = task.Payload.ToArray();
            workspace.Data.AsSpan().CopyTo(payload.AsSpan(0x94 - 12));
            task = task with { Payload = payload.ToImmutableArray() };
        }
        void Complete()
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            Next();
        }
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        short Spawned()
        {
            NativeBattleSiren95State shared = frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren layer requires its raw module owner.");
            return unchecked((short)shared.ReadWord((shared.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren layer requires its director pointer.")) + 0x40));
        }
        void FaceCamera()
        {
            NativeBattleActionMatrix matrix = readCameraView?.Invoke() ?? throw new NotSupportedException("Native Siren facing layer requires its current camera rotation.");
            matrix.Validate();
            int yaw = (NativeWorldScriptedPathMath.NativeAngle(matrix.Rotation[8], matrix.Rotation[2]) + 1024 - 2048) & 4095;
            task = task.WriteWord(0x62, unchecked((ushort)yaw));
        }
        void Unsupported() => throw new NotSupportedException($"Native Siren layer callback 0x{callback:X8}, signed phase {phase} is unsupported.");
    }

    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Write(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
