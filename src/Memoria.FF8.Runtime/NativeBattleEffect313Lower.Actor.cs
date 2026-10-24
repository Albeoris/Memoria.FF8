using static Memoria.FF8.Runtime.NativeBattleEffect313Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Lower
{
    public static NativeBattleEffect313Step AdvanceActor(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 14);
        if ((uint)actor >= 3)
        {
            throw new NotSupportedException("Effect313 actor callback requires its selected party row.");
        }
        uint model = checked(0x1D972C0u + (uint)actor * 0x9C);
        uint saved = checked(0x223CC38u + (uint)actor * 20);
        int age = Signed(task, 12);
        if (age == 0)
        {
            NativeBattleEffect313PointStep anchor = inputs.Anchor571400(frame, model, TaskView(task, 16, 8));
            frame = Write(anchor.Frame, address, CopyTask(Read(anchor.Frame, address), 16, anchor.Output8, 8));
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = frame with { State = frame.State.WriteView(saved, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked((ushort)Word313(current, 0)) & 0x1020), 2).WriteView(saved + 4, CopyBytes(current, 12, NativeBattleEffectArena.Clear(), 0, 8), 8).WriteView(saved + 12, CopyBytes(current, 28, NativeBattleEffectArena.Clear(), 0, 8), 8) };
            task = Read(frame, address);
        }
        if (age <= 24)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = WriteModel313(frame, model, 0, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked((ushort)Word313(current, 0)) | 0x1020), 2, inputs);
            frame = WriteModel313(frame, model, 12, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked(Signed(task, 24) * age) / 24), 2, inputs);
            frame = WriteModel313(frame, model, 14, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked(age << 13) / 24), 2, inputs);
            NativeBattleVector anchor = new(Signed(task, 16), Signed(task, 18), Signed(task, 20));
            NativeBattleVector original = Vector(frame.State.ReadView(saved + 12, 8));
            NativeBattleVector delta = new(unchecked((short)(anchor.X - original.X)), unchecked((short)(anchor.Y - original.Y)), unchecked((short)(anchor.Z - original.Z)));
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(Vector(inputs.ReadModelArena9C(frame, model), 12));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, delta);
            int bounce = unchecked(NativeBattleCameraMath.Sine(unchecked(age << 11) / 24) * Signed(task, 32)) >> 12;
            NativeBattleEffectArena position = PutWord313(PutWord313(PutWord313(NativeBattleEffectArena.Clear(), 0, anchor.X - rotated.X), 2, anchor.Y - rotated.Y + bounce), 4, anchor.Z - rotated.Z);
            frame = WriteModel313(frame, model, 28, position, 4, inputs);
            frame = WriteModel313(frame, model, 32, CopyBytes(position, 4, NativeBattleEffectArena.Clear(), 0, 2), 2, inputs);
        }
        else if (age <= 32)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = WriteModel313(frame, model, 0, PutWord313(NativeBattleEffectArena.Clear(), 0, unchecked((ushort)Word313(current, 0)) | 0x1020), 2, inputs);
            int bounce = unchecked(NativeBattleCameraMath.Sine(unchecked((age - 24) << 11) / 8) * (Signed(task, 32) >> 3)) >> 12;
            frame = WriteModel313(frame, model, 30, PutWord313(NativeBattleEffectArena.Clear(), 0, bounce), 2, inputs);
        }
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (Signed(task, 12) is 24 or 31)
        {
            frame = BirthActorRecords313(frame, task, Signed(task, 12) == 24);
            if (Signed(task, 12) == 24 && Signed(task, 26) >= 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect313SelectSequence505C00(model, 6));
            }
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        if (next < 32)
        {
            return new(frame, 0);
        }
        short contact = Signed(task, 26);
        if (contact >= 0 && inputs.ReadSequenceHeaderByte0(frame, model) == 6)
        {
            return new(frame, 0);
        }
        NativeBattleEffectArena final = inputs.ReadModelArena9C(frame, model);
        int flags = (unchecked((ushort)Word313(final, 0)) & 0xEFDF) | unchecked((ushort)Word313(frame.State.ReadView(saved, 2), 0));
        frame = WriteModel313(frame, model, 0, PutWord313(NativeBattleEffectArena.Clear(), 0, flags), 2, inputs);
        frame = WriteModel313(frame, model, 28, frame.State.ReadView(saved + 12, 4), 4, inputs);
        frame = WriteModel313(frame, model, 32, frame.State.ReadView(saved + 16, 4), 4, inputs);
        frame = WriteModel313(frame, model, 12, frame.State.ReadView(saved + 4, 4), 4, inputs);
        frame = WriteModel313(frame, model, 16, frame.State.ReadView(saved + 8, 4), 4, inputs);
        if (contact >= 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect313Contact506690(contact));
        }
        return new(frame, 2);
    }

    public static NativeBattleEffect313Frame BirthActorRecords313(NativeBattleEffect313Frame frame, NativeBattleModulePoolTask task, bool firstWave)
    {
        int count = firstWave ? 16 : 12;
        for (int attempt = 0; attempt < count; attempt++)
        {
            uint row = 0;
            for (int index = 0; index < 80; index++)
            {
                uint candidate = checked(0x223B698u + (uint)index * 24);
                if (frame.State.ReadDword(candidate) == 0)
                {
                    row = candidate;
                    break;
                }
            }
            if (row == 0)
            {
                break;
            }
            NativeBattleEffectArena record = frame.State.ReadView(row, 24);
            record = PutWord313(Put313(record, 0, 1), 4, 0);
            int scale = firstWave ? Random313(ref frame) % 1024 + 2560 : Random313(ref frame) % 768 + 2048;
            record = PutWord313(PutWord313(PutWord313(PutWord313(record, 6, scale), 8, Signed(task, 16)), 10, 0), 12, Signed(task, 20));
            int angle = Random313(ref frame) % 4096;
            int speed = firstWave ? Random313(ref frame) % 90 + 120 : Random313(ref frame) % 60 + 100;
            record = PutWord313(PutWord313(PutWord313(record, 16, unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12), 18, 0), 20, unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12);
            frame = frame with { State = frame.State.WriteView(row, record, 24) };
        }
        return frame;
    }

    private static NativeBattleEffect313Frame WriteModel313(NativeBattleEffect313Frame frame, uint model, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect313Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect313WriteModelBytes(model, offset, data, length));
}
