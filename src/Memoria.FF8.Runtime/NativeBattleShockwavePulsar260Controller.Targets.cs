using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShockwavePulsar260Controller
{
    public static NativeBattleShockwavePulsar260TaskStep AdvanceTarget(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            TargetCallback => AdvanceRisingTarget(frame, address, resources, inputs),
            LateTargetCallback => AdvanceFallingTarget(frame, address, resources, inputs),
            ChildActCallback => AdvanceTargetChild(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Shockwave Pulsar target selected an unsupported callback.")
        };
    }

    private static NativeBattleShockwavePulsar260TaskStep AdvanceRisingTarget(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 0x10);
        int factor = unchecked(age << 12) / 29;
        uint recordAddress = task.ReadDword(0xC);
        NativeBattleEffectArena record = ReadView(frame, recordAddress, 0x24);
        int actor = Actor(TargetDword(record, 0));
        NativeBattleEffectArena position = inputs.ReadPosition8(actor);
        int height = (TargetWord(record, 0x20) + TargetWord(record, 0x22)) / 2;
        NativeBattleEffectArena context = TargetContext(frame, TargetWord(position, 0), unchecked((short)(TargetWord(position, 2) + height)), TargetWord(position, 4), -1024);
        frame = AdvanceAct(frame, address, 0x14, 0xBC, 0x111F09C, context, resources, inputs).Frame;
        if (age == 5)
        {
            for (int angle = 4096; angle != 0; angle -= 256)
            {
                (NativeBattleShockwavePulsar260Frame allocated, uint puffAddress) = AllocatePuff(frame);
                frame = allocated;
                if (puffAddress == 0)
                {
                    break;
                }
                frame = DrawRandom(frame, out ushort radiusDraw);
                int radius = (radiusDraw & 15) + 16;
                frame = DrawRandom(frame, out ushort kindDraw);
                byte[] bytes = new byte[16];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x111CF4C);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), TargetWord(position, 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), TargetWord(position, 2));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), TargetWord(position, 4));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), (ushort)((kindDraw & 0x800) + 6000));
                bytes[12] = unchecked((byte)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 11));
                bytes[13] = 0;
                bytes[14] = unchecked((byte)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 11));
                bytes[15] = 16;
                frame = WriteView(frame, puffAddress, NativeBattleEffectArena.Clear().Write(0, bytes), 16);
            }
        }
        int remaining = unchecked(4096 - factor);
        int rotationDelta = remaining / 8;
        if ((actor & 1) == 0)
        {
            rotationDelta = -rotationDelta;
        }
        record = record.Write(0xC, BitConverter.GetBytes(unchecked((short)(TargetWord(record, 0xC) + rotationDelta))));
        record = record.Write(0xE, BitConverter.GetBytes(unchecked((short)(TargetWord(record, 0xE) + rotationDelta))));
        frame = WriteView(frame, recordAddress + 0xC, NativeBattleEffectNodeContextMemory.Copy(record, 0xC, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, 4), 4);
        int square = unchecked(remaining * remaining) >> 12;
        int y = unchecked(square << 12) / 4096 - height;
        frame = TargetPositionY(frame, actor, unchecked((short)y), inputs);
        task = Read(frame, address).WriteWord(0x10, unchecked((ushort)(age + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 0x10) >= 30)
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorFlags((byte)actor, 8, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    private static NativeBattleShockwavePulsar260TaskStep AdvanceFallingTarget(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 0x10);
        int duration = Signed(task, 0x12);
        uint recordAddress = task.ReadDword(0xC);
        NativeBattleEffectArena record = ReadView(frame, recordAddress, 0x24);
        int actor = Actor(TargetDword(record, 0));
        int targetScale = unchecked((int)TargetDword(record, 0x18));
        int scale = age < 13 ? unchecked(4096 - unchecked((4096 - targetScale) * age) / 13) : targetScale;
        frame = WriteDword(frame, recordAddress + 0x1C, unchecked((uint)scale));
        NativeBattleEffectArena position = inputs.ReadPosition8(actor);
        int sum = TargetWord(record, 0x20) + TargetWord(record, 0x22);
        NativeBattleEffectArena context = TargetContext(frame, TargetWord(position, 0), unchecked((short)(TargetWord(position, 2) + sum / 2)), TargetWord(position, 4), -1024);
        frame = AdvanceAct(frame, address, 0x14, 0x100, 0x111DEBC, context, resources, inputs).Frame;
        int elapsed = age - 13;
        if (unchecked((uint)elapsed) < unchecked((uint)duration))
        {
            int fraction = unchecked((int)(unchecked((uint)(elapsed << 12)) / unchecked((uint)duration)));
            if (elapsed == 0)
            {
                (NativeBattleShockwavePulsar260Frame appended, uint childAddress) = Append(frame, 2, ChildActCallback);
                frame = appended;
                NativeBattleModulePoolTask child = Read(frame, childAddress).WriteWord(0xC, unchecked((ushort)TargetWord(position, 0))).WriteWord(0xE, 0).WriteWord(0x10, unchecked((ushort)TargetWord(position, 4)));
                frame = Write(frame, childAddress, child);
                frame = InitializeAct(frame, childAddress, 0x14, 0xD8, 0x111D424, resources);
            }
            int rotationDelta = fraction / 8;
            if ((actor & 1) == 0)
            {
                rotationDelta = -rotationDelta;
            }
            frame = WriteWord(frame, recordAddress + 0xC, unchecked((ushort)(TargetWord(record, 0xC) + rotationDelta)));
            frame = WriteWord(frame, recordAddress + 0xE, unchecked((ushort)(TargetWord(record, 0xE) + rotationDelta)));
            int square = unchecked(fraction * fraction) >> 12;
            int bodyTerm = unchecked(sum * square) / 8192;
            int descent = unchecked(-square << 14) / 2 / 4096;
            frame = TargetPositionY(frame, actor, unchecked((short)(TargetWord(record, 6) + descent - bodyTerm)), inputs);
        }
        else if (unchecked((uint)elapsed) < 200u)
        {
            short y = TargetWord(position, 2);
            if (elapsed == duration)
            {
                y = unchecked((short)(-12288 - sum / 2));
            }
            frame = TargetPositionY(frame, actor, unchecked((short)(y - 256)), inputs);
        }
        NativeBattleEffectArena bodyOffset = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)0)).Write(2, BitConverter.GetBytes(unchecked((short)(sum >> 1)))).Write(4, BitConverter.GetBytes((short)0));
        NativeBattleEffectArena rotation = ReadView(frame, recordAddress + 0xC, 8);
        short currentScale = unchecked((short)ReadWord(frame, recordAddress + 0x1C));
        if (TargetDword(record, 0x14) != 0)
        {
            foreach (int linked in TargetRing(actor, inputs))
            {
                if ((inputs.ReadModel(linked).Flags0 & 2) != 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260DrawBody((byte)linked, rotation, bodyOffset, currentScale));
                }
            }
            foreach (int linked in TargetRing(actor, inputs))
            {
                if ((inputs.ReadModel(linked).Flags0 & 2) != 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorFlags((byte)linked, 0, 4));
                }
            }
        }
        else
        {
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260DrawBody((byte)actor, rotation, bodyOffset, currentScale));
            frame = inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorFlags((byte)actor, 0, 4));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x10, unchecked((ushort)(age + 1))));
        return new(frame, 0);
    }

    private static NativeBattleShockwavePulsar260TaskStep AdvanceTargetChild(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena context = TargetContext(frame, Signed(task, 0xC), Signed(task, 0xE), Signed(task, 0x10), -128);
        NativeBattleShockwavePulsar260TaskStep advanced = AdvanceAct(frame, address, 0x14, 0xD8, 0x111D424, context, resources, inputs);
        return new(advanced.Frame, advanced.Result == 0 ? 2 : 0);
    }

    private static NativeBattleEffectArena TargetContext(NativeBattleShockwavePulsar260Frame frame, short x, short y, short z, short depth)
    {
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z)).Write(8, BitConverter.GetBytes(0x112D190u)).Write(0xC, BitConverter.GetBytes(depth)).Write(0x10, BitConverter.GetBytes(checked(frame.State.ReadDword(WorkingGlobal) + MorphOffset)));
    }

    private static NativeBattleShockwavePulsar260Frame TargetPositionY(NativeBattleShockwavePulsar260Frame frame, int actor, short y, NativeBattleShockwavePulsar260Inputs inputs) => inputs.Execute(frame, new NativeBattleShockwavePulsar260ActorWrite((byte)actor, 0x1E, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(y)), 2));
    private static short TargetWord(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
    private static uint TargetDword(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));

    private static IEnumerable<int> TargetRing(int first, NativeBattleShockwavePulsar260Inputs inputs)
    {
        HashSet<int> visited = [];
        int current = first;
        while (true)
        {
            if (current is < 0 or > 6 || !visited.Add(current))
            {
                throw new NotSupportedException("Shockwave Pulsar requires its actual terminated or circular linked-model ring.");
            }
            yield return current;
            int? next = inputs.ReadLinkedActor(current);
            if (next is null || next == first)
            {
                yield break;
            }
            current = next.Value;
        }
    }
}
