using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99EnvironmentFrame(NativeBattleModulePoolTask Task, NativeBattleChoco97Context Context, NativeBattleStageState Stage, ImmutableArray<NativeBattleGraphicsModel> Models, uint FreezePrimary, uint FreezeSecondary);
public sealed record NativeBattleChoco99EnvironmentStep(NativeBattleChoco99EnvironmentFrame Frame, bool ReadyToComplete);
public sealed record NativeBattleChoco99EnvironmentSound(uint Descriptor, uint Flags, byte Pan);

public static class NativeBattleChoco99Environment
{
    public static NativeBattleChoco99EnvironmentStep Advance(NativeBattleChoco99EnvironmentFrame frame, Func<NativeBattleChoco99EnvironmentFrame, NativeBattleChoco99EnvironmentSound, NativeBattleChoco99EnvironmentFrame> playSound)
    {
        frame.Context.Validate();
        frame.Stage.Validate();
        if (frame.Models.IsDefault || frame.Models.Length != 7 || frame.Task.Payload.IsDefault || frame.Task.Payload.Length != 0xB0 - 12)
        {
            throw new InvalidDataException("Native Choco environment requires its original task, stage and actor rows.");
        }
        int phase = unchecked((sbyte)frame.Task.ReadByte(0x29));
        switch (frame.Task.Callback)
        {
            case 0x71F310:
                switch (phase)
                {
                    case 0:
                        if (Request(5))
                        {
                            Next();
                        }
                        break;
                    case 1:
                        if (AtLeast(5))
                        {
                            HideEnvironment();
                            Sound(0x1529FAC);
                            Store(frame.Task.WriteWord(0xAC, 36));
                            Next();
                        }
                        break;
                    case 2:
                        if (Countdown() <= 0)
                        {
                            Next();
                        }
                        break;
                    case 3:
                        if (Request(7))
                        {
                            Freeze(0);
                            Complete();
                        }
                        else
                        {
                            Freeze(1);
                        }
                        break;
                    case 4:
                        break;
                    default:
                        throw new NotSupportedException("Native Choco environment hide phase is unsupported.");
                }
                break;
            case 0x721010:
            case 0x721100:
                switch (phase)
                {
                    case 0:
                        Store(frame.Task.WriteWord(0x1C, frame.Task.Callback == 0x721010 ? (ushort)0 : (ushort)3072));
                        if (frame.Task.Callback == 0x721010)
                        {
                            SetDepth(0, true);
                        }
                        Next();
                        break;
                    case 1:
                        bool rise = frame.Task.Callback == 0x721010;
                        short depth = unchecked((short)(frame.Task.ReadWord(0x1C) + (rise ? 128 : -128)));
                        if (rise ? depth >= 1536 : depth <= 0)
                        {
                            depth = rise ? (short)1536 : (short)0;
                            Complete();
                        }
                        Store(frame.Task.WriteWord(0x1C, unchecked((ushort)depth)));
                        SetDepth(depth, false);
                        break;
                    case 2:
                        break;
                    default:
                        throw new NotSupportedException("Native Choco environment fade phase is unsupported.");
                }
                break;
            case 0x721460:
                switch (phase)
                {
                    case 0:
                        if (Request(10))
                        {
                            Next();
                        }
                        break;
                    case 1:
                        if (AtLeast(10))
                        {
                            RestoreEnvironment();
                            Sound(0x1529FB0);
                            Complete();
                        }
                        break;
                    case 2:
                        break;
                    default:
                        throw new NotSupportedException("Native Choco environment restore phase is unsupported.");
                }
                break;
            case 0x7215A0:
                switch (phase)
                {
                    case 0:
                        if (Request(8))
                        {
                            Next();
                        }
                        break;
                    case 1:
                        if (AtLeast(8))
                        {
                            Store(frame.Task.WriteWord(0xAC, 34));
                            Next();
                        }
                        break;
                    case 2:
                        if (Countdown() <= 0)
                        {
                            Next();
                        }
                        break;
                    case 3:
                        if (Request(9))
                        {
                            Complete();
                        }
                        break;
                    case 4:
                        break;
                    default:
                        throw new NotSupportedException("Native Choco environment wait phase is unsupported.");
                }
                break;
            default:
                throw new NotSupportedException("Native Choco environment callback is unsupported.");
        }
        Store(frame.Task.WriteWord(0x24, unchecked((ushort)(frame.Task.ReadWord(0x24) + 1))));
        return new(frame, (frame.Task.ReadByte(0x26) & 1) != 0 && frame.Task.ReadByte(0x28) == 0);

        void Store(NativeBattleModulePoolTask task)
        {
            frame = frame with { Task = task };
        }

        void Next()
        {
            Store(frame.Task.WriteByte(0x29, unchecked((byte)(frame.Task.ReadByte(0x29) + 1))));
        }

        void Complete()
        {
            Store(frame.Task.WriteByte(0x26, (byte)(frame.Task.ReadByte(0x26) | 1)));
            Next();
        }

        short Countdown()
        {
            ushort value = unchecked((ushort)(frame.Task.ReadWord(0xAC) - 1));
            Store(frame.Task.WriteWord(0xAC, value));
            return unchecked((short)value);
        }

        bool AtLeast(short mode) => NativeBattleChoco97CameraTask.ModeAtLeast730E80(frame.Context.Word(0x40), mode);

        bool Request(short mode)
        {
            (ushort requested, bool accepted) = NativeBattleChoco97CameraTask.RequestMode730EC0(frame.Context.Word(0x40), frame.Context.Word(0x44), mode);
            if (accepted)
            {
                frame = frame with { Context = frame.Context.WriteWord(0x44, requested) };
            }
            return accepted;
        }

        void Freeze(uint value)
        {
            (uint primary, uint secondary) = NativeBattleChoco99Director.SetFreeze71E840(frame.FreezePrimary, frame.FreezeSecondary, value);
            frame = frame with { FreezePrimary = primary, FreezeSecondary = secondary };
        }

        void Sound(uint descriptor)
        {
            frame = playSound(frame, new(descriptor, 0, 128));
        }

        void SetDepth(short depth, bool clearRgb)
        {
            frame = frame with { Stage = frame.Stage with { Groups = frame.Stage.Groups.Select(group => group with { ColorDepth = depth, ColorFlags = clearRgb ? group.ColorFlags & 0xFF000000 : group.ColorFlags }).ToImmutableArray() } };
        }

        void HideEnvironment()
        {
            frame = frame with { Stage = frame.Stage with { Groups = frame.Stage.Groups.Select(group => group with { Flags = (byte)(group.Flags & 0xFD) }).ToImmutableArray() } };
            byte[] context = frame.Context.Data.ToArray();
            ImmutableArray<NativeBattleGraphicsModel>.Builder models = frame.Models.ToBuilder();
            for (int index = 0; index < 4; index++)
            {
                ushort flags = models[index + 3].Flags0;
                if ((flags & 2) != 0)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(0x2C + index * 4), flags);
                    models[index + 3] = models[index + 3] with { Flags0 = (ushort)(flags | 4) };
                }
            }
            frame = frame with { Context = frame.Context with { Data = context.ToImmutableArray() }, Models = models.ToImmutable() };
        }

        void RestoreEnvironment()
        {
            frame = frame with { Stage = frame.Stage with { Groups = frame.Stage.Groups.Select(group => group with { Flags = (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2)) }).ToImmutableArray() } };
            ImmutableArray<NativeBattleGraphicsModel>.Builder models = frame.Models.ToBuilder();
            for (int index = 0; index < 4; index++)
            {
                ushort flags = models[index + 3].Flags0;
                if ((flags & 2) != 0)
                {
                    uint saved = BinaryPrimitives.ReadUInt32LittleEndian(frame.Context.Data.AsSpan().Slice(0x2C + index * 4));
                    models[index + 3] = models[index + 3] with { Flags0 = (ushort)(((uint)flags & ~4u) | (saved & 4u)) };
                }
            }
            frame = frame with { Models = models.ToImmutable() };
        }
    }
}
