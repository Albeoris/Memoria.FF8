using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleBreath157Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath157Children
{
    public static (NativeBattleBreath157Frame Frame, int Result) Advance6044D0(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs) => AdvanceParticles(frame, address, 0, resources, inputs);
    public static (NativeBattleBreath157Frame Frame, int Result) Advance604C80(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs) => AdvanceParticles(frame, address, 1, resources, inputs);
    public static (NativeBattleBreath157Frame Frame, int Result) Advance6052A0(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs) => AdvanceParticles(frame, address, 2, resources, inputs);

    public static (NativeBattleBreath157Frame Frame, int Result) Advance604B30(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = NativeBattleBreath157Geometry.LoadPose(frame, 0x23EB5C0);
        frame = NativeBattleBreath157Geometry.TransformBillboard(frame, frame.State.ReadView(MovingPoint(task), 8), Signed(task, 0x1C), true);
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xDB0AC0u)).Write(4, BitConverter.GetBytes((ushort)(task.ReadWord(12) & 3))).Write(0x24, new byte[2]), 180);
        frame = inputs.Execute(frame, new NativeBattleBreath157DrawSprite180(temporary, 0xDB0AC0));
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        uint z = MovingPoint(task) + 4;
        frame = PutWord(frame, z, Word(frame, z) - Signed(task, 0x14));
        task = Add(task, 12, 1);
        return (Write(frame, address, task), Signed(task, 12) >= 5 ? 2 : 0);
    }

    private static (NativeBattleBreath157Frame Frame, int Result) AdvanceParticles(NativeBattleBreath157Frame frame, uint address, int kind, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        uint source = kind switch { 0 => 0xDB0B34, 1 => 0xDB0CE0, _ => 0xDB0914 };
        uint tag = 1u << kind;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int survivors = 0;
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, new byte[2]), 180);
        if (kind < 2)
        {
            frame = NativeBattleBreath157Geometry.LoadPose(frame, kind == 0 ? 0x23ECDB0u : 0x23EB5C0u);
        }
        for (int index = 0; index < 100; index++)
        {
            uint record = 0x23EB628u + (uint)(index * 24);
            if ((frame.State.ReadDword(record) & tag) == 0)
            {
                continue;
            }
            short size = Word(frame, record + 6);
            frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(frame.State.ReadWord(record + 4))), 180);
            if (kind == 0)
            {
                int angle = Word(frame, record + 8);
                int radius = Word(frame, record + 14);
                NativeBattleVector point = new(unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12)), unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)), Word(frame, record + 12));
                NativeBattleEffectArena packedPoint = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
                frame = NativeBattleBreath157Geometry.TransformBillboard(frame, packedPoint, size, true);
            }
            else if (kind == 1)
            {
                frame = NativeBattleBreath157Geometry.TransformBillboard(frame, frame.State.ReadView(record + 8, 8), size, false);
            }
            else
            {
                NativeBattleEffectArena point = frame.State.ReadView(record + 8, 8);
                (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
                short? w = known[0] && known[1] ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
                frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Point(frame, record + 8), w, size, unchecked((short)-(size >> 4))) };
            }
            frame = inputs.Execute(frame, new NativeBattleBreath157DrawSprite180(temporary, source));
            if (paused)
            {
                continue;
            }
            frame = PutWord(frame, record + 4, Word(frame, record + 4) + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(RequireStaging(frame).Read(temporary + 0x28, 2)) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(record, 0) };
                continue;
            }
            if (kind == 0)
            {
                frame = PutWord(frame, record + 8, Word(frame, record + 8) + Word(frame, record + 10));
            }
            survivors++;
        }
        frame = Release(frame, 180);
        if (paused)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 12);
        if (age >= 0 && age <= (kind == 0 ? 30 : 4))
        {
            for (int birth = 0; birth < (kind == 0 ? 3 : 6); birth++)
            {
                uint record = FindFree(frame);
                if (record == 0)
                {
                    break;
                }
                frame = frame with { State = frame.State.WriteDword(record, tag).WriteWord(record + 4, 0) };
                frame = DrawRandom(frame, out ushort random);
                frame = PutWord(frame, record + 6, kind switch { 0 => (random & 1023) + 768, 1 => random % 896 + 512, _ => random % 2816 + 1536 });
                if (kind == 0)
                {
                    frame = DrawRandom(frame, out ushort angle);
                    frame = PutWord(frame, record + 8, angle & 4095);
                    frame = DrawRandom(frame, out ushort spin);
                    frame = PutWord(frame, record + 10, spin % 50 + 10);
                    frame = DrawRandom(frame, out ushort z);
                    frame = PutWord(frame, record + 12, -10 - z % 600);
                    frame = DrawRandom(frame, out ushort radius);
                    frame = PutWord(frame, record + 14, radius % 1300 + 300);
                }
                else
                {
                    uint point = kind == 1 ? MovingPoint(task) : 0x23ECDD0;
                    frame = frame with { State = frame.State.WriteView(record + 8, frame.State.ReadView(point, 8), 8) };
                    int spread = kind == 1 ? age * 30 + 350 : 1200;
                    frame = DrawRandom(frame, out ushort x);
                    frame = PutWord(frame, record + 8, Word(frame, record + 8) + x % spread - (spread >> 1));
                    frame = DrawRandom(frame, out ushort y);
                    frame = PutWord(frame, record + 10, Word(frame, record + 10) + y % spread - (spread >> 1));
                    frame = DrawRandom(frame, out ushort z);
                    frame = PutWord(frame, record + 12, Word(frame, record + 12) + (kind == 1 ? z % Signed(task, 0x14) : z % 1200 - 600));
                }
            }
        }
        task = Add(Read(frame, address), 12, 1);
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static uint FindFree(NativeBattleBreath157Frame frame)
    {
        for (int index = 0; index < 100; index++)
        {
            uint address = 0x23EB628u + (uint)(index * 24);
            if (frame.State.ReadDword(address) == 0)
            {
                return address;
            }
        }
        return 0;
    }
}
