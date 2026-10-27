using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTonberry90ActorDraw(NativeBattleTonberry90Frame Frame, int Remaining);
public sealed record NativeBattleTonberry90CompanionStep(NativeBattleTonberry90Frame Frame, bool Complete, NativeBattleStageState Stage);
public sealed record NativeBattleTonberry90ActorServices(Func<NativeBattleTonberry90Frame, uint, NativeBattleGfAnimationSource> ReadSource, Func<NativeBattleTonberry90Frame, uint, NativeBattleGfAnimationSource, uint, NativeBattleTonberry90Frame> Bounds8DE160And8DE1F0, Func<NativeBattleTonberry90Frame, uint, NativeBattleTonberry90ActorDraw> Draw764130);

public static partial class NativeBattleTonberry90Actors
{
    public static NativeBattleTonberry90TaskStep Advance763EB0(NativeBattleTonberry90Frame frame, uint address, NativeBattleTonberry90ActorServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x763EB0)
        {
            throw new InvalidDataException("Native Tonberry phase actor requires its actual callback task.");
        }
        uint director = Director(frame);
        uint depth = State(frame).ReadWord(unchecked(director + 0x46)) switch
        {
            30 => 0x1547468,
            90 => 0x154747C,
            100 => 0x1547490,
            105 => 0x15474A4,
            113 => 0x15474B8,
            _ => 0
        };
        if (depth != 0)
        {
            frame = frame with { Memory = frame.Memory with { Tonberry90 = State(frame).WriteDword(0x25A4C04, depth) } };
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= 6)
                {
                    uint sourceAddress = task.ReadDword(0x74);
                    NativeBattleGfAnimationSource source = services.ReadSource(frame, sourceAddress);
                    if (source.Address != sourceAddress)
                    {
                        throw new InvalidDataException("Native Tonberry actor source differs from its current task pointer.");
                    }
                    NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x94 - 12), task.PayloadLength - (0x94 - 12));
                    workspace = workspace.Initialize7016B0(source, unchecked((int)task.ReadDword(0x78)));
                    task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 0x94, workspace);
                    director = Director(frame);
                    task = task.WriteDword(0x1C, State(frame).ReadDword(director)).WriteDword(0x20, State(frame).ReadDword(unchecked(director + 4))).WriteWord(0x1E, 0);
                    NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), unchecked((short)task.ReadWord(0x62)));
                    NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, 0x180));
                    task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + offset.X))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + offset.Y))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + offset.Z))).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
                    frame = Write(frame, address, task);
                    frame = frame with { Memory = frame.Memory with { Tonberry90 = State(frame).WriteDword(0x25A4C04, 0x1547454) } };
                    if (source.Data.IsDefault || source.Data.Length < 0x28)
                    {
                        throw new InvalidDataException("Native Tonberry actor bounds require their current geometry selector DWORD.");
                    }
                    int relative = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(0x24));
                    uint geometry = unchecked(sourceAddress + (uint)(relative / 4 * 4));
                    frame = services.Bounds8DE160And8DE1F0(frame, address, source, geometry);
                    frame = Next(frame, address);
                }
                break;
            case 1:
                var requested = NativeBattleTonberry90Programme.RequestPhase763940(frame, 2);
                frame = requested.Frame;
                if (requested.Accepted)
                {
                    frame = Next(frame, address);
                }
                break;
            case 2:
                if (Spawned(frame, 2))
                {
                    frame = services.Draw764130(frame, address).Frame;
                    frame = Next(frame, address);
                }
                break;
            case 3:
                if (Spawned(frame, 3))
                {
                    frame = Complete(frame, address);
                }
                else
                {
                    NativeBattleTonberry90ActorDraw drawn = services.Draw764130(frame, address);
                    frame = drawn.Frame;
                    if (drawn.Remaining == 0)
                    {
                        frame = Complete(frame, address);
                    }
                }
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry signed phase actor state {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleTonberry90CompanionStep Advance7681B0(NativeBattleTonberry90Frame frame, uint address, NativeBattleStageState stage)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x7681B0)
        {
            throw new InvalidDataException("Native Tonberry color companion requires its actual callback task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = Write(frame, address, task.WriteWord(0x76, 0));
                stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000 }).ToImmutableArray() };
                frame = Next(frame, address);
                break;
            case 1:
                ushort factor = unchecked((ushort)(task.ReadWord(0x76) + 0x100));
                if (unchecked((short)factor) >= 0x400)
                {
                    factor = 0x400;
                    frame = Complete(frame, address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x76, factor));
                stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)factor) }).ToImmutableArray() };
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry signed color companion state {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        NativeBattleTonberry90TaskStep finished = Finish(frame, address);
        return new(finished.Frame, finished.Complete, stage);
    }

    public static NativeBattleTonberry90CompanionStep Advance7682A0(NativeBattleTonberry90Frame frame, uint address, NativeBattleStageState stage)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x7682A0)
        {
            throw new InvalidDataException("Native Tonberry inverse color companion requires its selected current callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= 30)
                {
                    frame = Next(Write(frame, address, task.WriteWord(0x76, 1024)), address);
                }
                break;
            case 1:
                ushort value = unchecked((ushort)(task.ReadWord(0x76) - 256));
                if (unchecked((short)value) <= 0)
                {
                    value = 0;
                    frame = Complete(frame, address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x76, value));
                stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)value) }).ToImmutableArray() };
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry inverse companion signed phase {unchecked((sbyte)task.ReadByte(0x29))} has no selected state.");
        }
        NativeBattleTonberry90TaskStep finished = Finish(frame, address);
        return new(finished.Frame, finished.Complete, stage);
    }

    private static NativeBattleTonberry90TaskStep Finish(NativeBattleTonberry90Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }
    private static NativeBattleTonberry90Frame Complete(NativeBattleTonberry90Frame frame, uint address) => Next(Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1))), address);
    private static NativeBattleTonberry90Frame Next(NativeBattleTonberry90Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static bool Spawned(NativeBattleTonberry90Frame frame, short phase) => unchecked((short)State(frame).ReadWord(unchecked(Director(frame) + 0x40))) >= phase;
    private static NativeBattleTonberry90State State(NativeBattleTonberry90Frame frame) => frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry actor requires its retained module globals.");
    private static uint Director(NativeBattleTonberry90Frame frame) => State(frame).DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry actor requires its current director pointer.");
    private static NativeBattleModulePoolTask Read(NativeBattleTonberry90Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleTonberry90Frame Write(NativeBattleTonberry90Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
