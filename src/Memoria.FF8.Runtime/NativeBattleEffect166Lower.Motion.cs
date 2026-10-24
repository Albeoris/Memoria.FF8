using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Lower
{
    private static NativeBattleEffectArena Model(NativeBattleEffect166Frame frame, int actor, NativeBattleEffect166Inputs inputs) => (inputs.ReadModelArena9C ?? throw new NotSupportedException("Effect166 requires its current addressed model fields."))(frame, actor);

    private static NativeBattleEffect166Frame SaveModel(NativeBattleEffect166Frame frame, int actor, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleEffectArena model = Model(frame, actor, inputs);
        uint saved = checked(0x23C1360u + (uint)(actor * 28));
        NativeBattleEffectArena record = frame.State.ReadView(saved, 20);
        record = Word(record, 0, S(model, 0) & 0x1000);
        record = Copy(Copy(Copy(Copy(record, 4, model, 12, 4), 8, model, 16, 4), 12, model, 28, 4), 16, model, 32, 4);
        return frame with { State = frame.State.WriteView(saved, record, 20) };
    }

    private static NativeBattleEffect166Frame WriteModel(NativeBattleEffect166Frame frame, int actor, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect166Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect166WriteModelBytes(actor, offset, data, length));

    private static NativeBattleEffect166Frame RestoreModel(NativeBattleEffect166Frame frame, int actor, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleEffectArena model = Model(frame, actor, inputs);
        NativeBattleEffectArena record = frame.State.ReadView(checked(0x23C1360u + (uint)(actor * 28)), 20);
        frame = WriteModel(frame, actor, 0, Word(NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, (unchecked((ushort)S(model, 0)) & 0xEFFF) | unchecked((ushort)S(record, 0))), 2, inputs);
        foreach ((int target, int source) in new (int, int)[] { (12, 4), (16, 8), (28, 12), (32, 16) })
        {
            frame = WriteModel(frame, actor, target, Copy(NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, record, source, 4), 4, inputs);
        }
        return frame;
    }

    private static NativeBattleEffect166Step AdvanceLift(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 14);
        int age = Signed(task, 12);
        if (age == 0)
        {
            frame = SaveModel(frame, actor, inputs);
        }
        if (age < 8)
        {
            NativeBattleEffectArena model = Model(frame, actor, inputs);
            frame = WriteModel(frame, actor, 0, Word(NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, S(model, 0) | 0x1000), 2, inputs);
            short originalY = unchecked((short)frame.State.ReadWord(checked(0x23C136Eu + (uint)(actor * 28))));
            frame = WriteModel(frame, actor, 30, Word(NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, originalY - Signed(task, 32) * age / 8), 2, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        return new(next >= 9 ? RestoreModel(frame, actor, inputs) : frame, next >= 9 ? 2 : 0);
    }

    private static NativeBattleEffect166Step AdvanceTurn(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 14);
        int age = Signed(task, 12);
        if (age == 0)
        {
            NativeBattleEffect166PointStep point = (inputs.ReadAnchor571400 ?? throw new NotSupportedException("Effect166 requires its current actor anchor."))(frame, actor, TaskView(task, 16, 8));
            frame = point.Frame;
            task = CopyTask(Read(frame, address), 16, point.Output8, 8);
            frame = Write(frame, address, task);
            frame = SaveModel(frame, actor, inputs);
        }
        NativeBattleEffectArena model = Model(frame, actor, inputs);
        if (age <= 20)
        {
            frame = WriteModel(frame, actor, 0, Word(NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, S(model, 0) | 0x1000), 2, inputs);
            NativeBattleEffectArena angles = Copy(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, model, 12, 8);
            angles = Word(Word(angles, 0, Signed(task, 24) * age / 20), 2, age * 8192 / 20);
            frame = WriteModel(frame, actor, 12, Copy(NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, angles, 0, 4), 4, inputs);
            NativeBattleEffectArena saved = frame.State.ReadView(checked(0x23C1360u + (uint)(actor * 28)), 20);
            NativeBattleVector delta = new(unchecked((short)(Signed(task, 16) - S(saved, 12))), unchecked((short)(Signed(task, 18) - S(saved, 14))), unchecked((short)(Signed(task, 20) - S(saved, 16))));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CE30(Vector(angles)), delta);
            int lift = unchecked(Signed(task, 32) * NativeBattleCameraMath.Sine(age * 2048 / 20)) >> 12;
            NativeBattleEffectArena position = PutPoint(NativeBattleEffectArena.Clear().MarkUnknown(0, 6), 0, new(unchecked((short)(Signed(task, 16) - rotated.X)), unchecked((short)(Signed(task, 18) - rotated.Y + lift)), unchecked((short)(Signed(task, 20) - rotated.Z))));
            frame = WriteModel(frame, actor, 28, Copy(NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, position, 0, 4), 4, inputs);
            frame = WriteModel(frame, actor, 32, Copy(NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, position, 4, 2), 2, inputs);
        }
        else if (age <= 28)
        {
            frame = WriteModel(frame, actor, 0, Word(NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, S(model, 0) | 0x1000), 2, inputs);
            int y = unchecked((Signed(task, 32) >> 3) * NativeBattleCameraMath.Sine((age - 20) * 2048 / 8)) >> 12;
            frame = WriteModel(frame, actor, 30, Word(NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 0, y), 2, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int contact = Signed(task, 26);
        if (age == 20)
        {
            (frame, uint spawned) = Append(frame, 0x23C0540, 0x5E9250);
            NativeBattleEffectArena current = Model(frame, actor, inputs);
            NativeBattleModulePoolTask child = Read(frame, spawned).WriteWord(12, 0).WriteWord(16, unchecked((ushort)S(current, 28))).WriteWord(18, 0).WriteWord(20, unchecked((ushort)S(current, 32)));
            int scale = (3072 + Roll(ref frame) % 1024) / 4;
            frame = Write(frame, spawned, child.WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale));
            if (contact >= 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect166Sequence505C00(actor, 6));
            }
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next < 28 || contact >= 0 && inputs.ReadSequenceHeaderByte0(frame, actor) == 6)
        {
            return new(frame, 0);
        }
        frame = RestoreModel(frame, actor, inputs);
        if (contact >= 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect166Contact506690(contact));
        }
        return new(frame, 2);
    }
}
