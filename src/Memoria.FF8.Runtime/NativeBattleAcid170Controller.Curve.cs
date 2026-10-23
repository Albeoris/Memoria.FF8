using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid170Controller
{
    public static (NativeBattleAcid170Frame Frame, int Result) AdvanceCurve(NativeBattleAcid170Frame frame, uint address, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5DCAF0)
        {
            throw new NotSupportedException("Acid curve callback is unsupported.");
        }
        RequireCurveIndex(task);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        short age = Signed(task, 12);
        const uint curve = 0x23B0FD8;
        if (age == 0)
        {
            int caster = checked((int)frame.State.ReadDword(0x23B10E8));
            NativeBattleAcid170PointStep start = inputs.ReadBone502170(frame, caster, 41, 1536);
            frame = start.Frame with { State = start.Frame.State.WriteView(curve, start.Point8, 6) };
            NativeBattleAcid170PointStep target = inputs.ReadAnchor571400(frame, checked((int)frame.State.ReadDword(0x23AF820)));
            frame = target.Frame with { State = target.Frame.State.WriteView(curve + 8, target.Point8, 8) };
            frame = DrawRandom(frame, out ushort x);
            frame = PutWord170(frame, curve + 8, Word170(frame, curve + 8) + x % 700 - 350);
            frame = PutWord170(frame, curve + 10, 0);
            frame = DrawRandom(frame, out ushort z);
            frame = PutWord170(frame, curve + 12, Word170(frame, curve + 12) + z % 700 - 350);
            frame = frame with { State = frame.State.WriteView(curve + 16, frame.State.ReadView(curve, 8), 8).WriteView(curve + 40, frame.State.ReadView(curve + 8, 8), 8) };
            int dx = (Word170(frame, curve + 8) - Word170(frame, curve)) / 3;
            int dz = (Word170(frame, curve + 12) - Word170(frame, curve + 4)) / 3;
            frame = PutWord170(frame, curve + 24, Word170(frame, curve) + dx);
            frame = DrawRandom(frame, out ushort y1);
            frame = PutWord170(frame, curve + 26, Word170(frame, curve + 2) - y1 % 100 - 50);
            frame = PutWord170(frame, curve + 28, Word170(frame, curve + 4) + dz);
            frame = PutWord170(frame, curve + 32, Word170(frame, curve) + 2 * dx);
            frame = DrawRandom(frame, out ushort y2);
            frame = PutWord170(frame, curve + 34, Word170(frame, curve + 26) - y2 % 100 - 50);
            frame = PutWord170(frame, curve + 36, Word170(frame, curve + 4) + 2 * dz);
            (frame, uint temporary) = Allocate(frame, 400);
            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(4, frame.State.ReadView(curve + 16, 32), 0, Context(frame, temporary, 400));
            frame = PublishContext(frame, temporary, work, 400);
            NativeBattleEffectArena samples = frame.State.ReadView(curve + 48, 224);
            for (int sample = 0; sample < 28; sample++)
            {
                samples = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(4, work, sample * 4096 / 27, samples, sample * 8);
            }
            frame = frame with { State = frame.State.WriteView(curve + 48, samples, 224) };
            frame = Release(frame, 400);
        }
        if (age == 1)
        {
            (frame, uint child) = Append170(frame, 0x5DCF20);
            frame = DrawRandom(frame, out ushort size);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, task.ReadWord(14)).WriteWord(24, 0).WriteWord(26, 5).WriteWord(28, (ushort)(size % 80 + 768)));
        }
        if (age == 13)
        {
            (frame, uint child) = Append170(frame, 0x5DD030);
            NativeBattleModulePoolTask born = CopyTask(Read(frame, child), 16, frame.State.ReadView(curve + 8, 8), 8).WriteWord(12, 0).WriteWord(14, task.ReadWord(14));
            frame = DrawRandom(frame, out ushort angle);
            born = born.WriteWord(24, (ushort)(angle & 4095));
            frame = DrawRandom(frame, out ushort spin);
            born = born.WriteWord(26, (ushort)(spin % 6 + 16));
            frame = DrawRandom(frame, out ushort size);
            ushort scale = (ushort)(((size & 1023) + 2816) / 3);
            frame = Write(frame, child, born.WriteWord(28, scale).WriteWord(30, scale));
            (frame, child) = Append170(frame, 0x5DDE90);
            frame = Write(frame, child, CopyTask(Read(frame, child), 16, frame.State.ReadView(curve + 8, 8), 8).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(1 << (byte)task.ReadWord(14)))));
        }
        if (age == 12)
        {
            for (int birth = 0; birth < 4; birth++)
            {
                (frame, uint child) = Append170(frame, 0x5DE0B0);
                NativeBattleModulePoolTask born = Read(frame, child).WriteWord(12, 0);
                frame = DrawRandom(frame, out ushort delay);
                born = born.WriteWord(14, (ushort)(delay % 6 + birth));
                frame = DrawRandom(frame, out ushort x);
                born = born.WriteWord(16, unchecked((ushort)(Word170(frame, curve + 8) + x % 1600 - 800))).WriteWord(18, frame.State.ReadWord(curve + 10));
                frame = DrawRandom(frame, out ushort z);
                born = born.WriteWord(20, unchecked((ushort)(Word170(frame, curve + 12) + z % 1600 - 800)));
                frame = DrawRandom(frame, out ushort angle);
                born = born.WriteWord(24, (ushort)(angle & 4095));
                frame = DrawRandom(frame, out ushort size);
                ushort scale = (ushort)((size % 1536 + 2560) / 10);
                frame = Write(frame, child, born.WriteWord(28, scale).WriteWord(30, scale));
            }
            (frame, uint body) = Append170(frame, 0x5DE200);
            NativeBattleAcid170PointStep point = inputs.ReadAnchor571400(frame, checked((int)frame.State.ReadDword(0x23AF820)));
            frame = point.Frame;
            NativeBattleModulePoolTask bornBody = CopyTask(Read(frame, body), 16, point.Point8, 8).WriteWord(12, 0).WriteWord(14, task.ReadWord(14));
            ushort y = bornBody.ReadWord(18);
            bornBody = bornBody.WriteWord(18, bornBody.ReadWord(22)).WriteWord(22, y);
            ushort saved = (ushort)(inputs.ReadModel(frame, checked((int)frame.State.ReadDword(0x23AF820))).Flags0 & 4);
            frame = Write(frame, body, bornBody.WriteWord(26, saved));
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 40 ? 2 : 0);
    }

    private static void RequireCurveIndex(NativeBattleModulePoolTask task)
    {
        if (Signed(task, 14) != 0)
        {
            throw new NotSupportedException("Acid requires its produced single physical curve and selected vertex bank.");
        }
    }
    private static (NativeBattleAcid170Frame Frame, uint Address) Append170(NativeBattleAcid170Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, callback);
        return address != 0 ? (frame, address) : throw new InvalidDataException("Acid exhausted its actual child task bank.");
    }
    private static short Word170(NativeBattleAcid170Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleAcid170Frame PutWord170(NativeBattleAcid170Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
}
