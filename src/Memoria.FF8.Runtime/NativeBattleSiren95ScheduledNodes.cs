using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95NodeScheduleServices(NativeBattleActionPacketsState Packets, Func<NativeBattleSiren95Frame, uint, int, byte[]> ReadSchedule);

public static class NativeBattleSiren95ScheduledNodes
{
    public static NativeBattleSiren95TaskStep Advance744F90(NativeBattleSiren95Frame frame, uint address, NativeBattleSiren95NodeScheduleServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x744F90)
        {
            throw new InvalidDataException("Native Siren scheduled emitter requires its actual callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                uint director = State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren scheduled emitter requires its director pointer.");
                task = task.WriteDword(0x1C, State(frame).ReadDword(director + 8)).WriteDword(0x20, State(frame).ReadDword(director + 12));
                Next();
                break;
            case 1:
                task = task.WriteWord(0xDC, 0);
                Next();
                break;
            case 2:
                director = State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren scheduled emitter requires its director pointer.");
                if (unchecked((short)State(frame).ReadWord(director + 0x40)) >= 6)
                {
                    Complete();
                    break;
                }
                byte[] row = Row();
                if (Word(row, 0) == short.MaxValue)
                {
                    Complete();
                    break;
                }
                while (Word(row, 0) <= unchecked((short)task.ReadWord(0x24)))
                {
                    frame = frame with { Random = frame.Random.Draw(out ushort jitter) };
                    int angle = (Word(row, 2) + (jitter & 63) - 32) & 4095;
                    uint source = Word(row, 8) == 0 ? 0x16A40ECu : State(frame).ReadDword(unchecked(State(frame).ReadDword(0x258EAA0) + 0x108));
                    frame = Write(frame, address, task);
                    var appended = NativeBattleSiren95Actors.AppendTask(frame, 0x258BA90, 0x745180, 0x2A4, address, services.Packets);
                    frame = appended.Frame;
                    NativeBattleModulePoolTask child = Read(frame, appended.Address).WriteDword(0x30, source).WriteWord(0x298, 0).WriteWord(0x29C, 3).WriteWord(0x29E, 10);
                    int radius = Word(row, 6);
                    child = child.WriteWord(0x288, unchecked((ushort)-(unchecked(NativeBattleCameraMath.Sine(angle) * radius) / 4096))).WriteWord(0x28A, unchecked((ushort)-384)).WriteWord(0x28C, unchecked((ushort)(3264 - unchecked(NativeBattleCameraMath.Cosine(angle) * radius) / 4096)));
                    frame = frame with { Random = frame.Random.Draw(out ushort height) };
                    child = child.WriteWord(0x28A, unchecked((ushort)(child.ReadWord(0x28A) + Word(row, 4) - (height & 63))));
                    frame = Write(frame, appended.Address, child);
                    task = Read(frame, address).WriteWord(0xDC, unchecked((ushort)(Read(frame, address).ReadWord(0xDC) + 1)));
                    row = Row();
                }
                break;
            case 3:
                break;
            default:
                throw new NotSupportedException($"Native Siren scheduled emitter signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Write(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        byte[] Row()
        {
            uint pointer = unchecked(0x15339D8u + (uint)(unchecked((short)task.ReadWord(0xDC)) * 10));
            byte[] bytes = services.ReadSchedule(frame, pointer, 10);
            return bytes.Length == 10 ? bytes : throw new InvalidDataException("Native Siren schedule requires its full current ten-byte row.");
        }
        void Complete()
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            Next();
        }
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }

    private static short Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Write(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren scheduled emitter requires its raw module owner.");
}
