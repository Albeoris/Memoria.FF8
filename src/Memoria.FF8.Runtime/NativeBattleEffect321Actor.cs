using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect321Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect321Actor
{
    public static NativeBattleEffect321Step Advance(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 14);
        uint model = checked(0x1D972C0u + (uint)actor * 0x9C);
        uint saved = checked(0x221D420u + (uint)actor * 36);
        int age = Signed(task, 12);
        if (age == 0)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = frame with { State = frame.State.WriteView(saved, PutWord(NativeBattleEffectArena.Clear(), 0, Word(current, 0) & 0x1020), 2).WriteView(saved + 12, CopyBytes(current, 28, NativeBattleEffectArena.Clear(), 0, 8), 8).WriteView(saved + 4, CopyBytes(current, 12, NativeBattleEffectArena.Clear(), 0, 8), 8) };
        }
        if (age < 66)
        {
            NativeBattleEffectArena current = inputs.ReadModelArena9C(frame, model);
            frame = WriteModel(frame, model, 0, PutWord(NativeBattleEffectArena.Clear(), 0, Word(current, 0) | 0x1020), 2, inputs);
            frame = WriteModel(frame, model, 12, PutWord(NativeBattleEffectArena.Clear(), 0, unchecked(age * Signed(task, 24)) / 12), 2, inputs);
            frame = WriteModel(frame, model, 14, PutWord(NativeBattleEffectArena.Clear(), 0, unchecked(age << 13) / 12), 2, inputs);
            NativeBattleVector anchor = new(Signed(task, 16), Signed(task, 18), Signed(task, 20));
            NativeBattleVector original = Vector(frame.State.ReadView(saved + 12, 8));
            NativeBattleVector delta = new(unchecked((short)(anchor.X - original.X)), unchecked((short)(anchor.Y - original.Y)), unchecked((short)(anchor.Z - original.Z)));
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(Vector(inputs.ReadModelArena9C(frame, model), 12));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, delta);
            int vertical;
            if (age < 12)
            {
                vertical = unchecked(NativeBattleCameraMath.Sine(unchecked(age << 10) / 12) * Signed(task, 32)) >> 12;
            }
            else if (age < 54)
            {
                vertical = Signed(task, 32);
            }
            else
            {
                vertical = unchecked(NativeBattleCameraMath.Sine(1024 + (unchecked((age - 54) << 10) / 12)) * Signed(task, 32)) >> 12;
            }
            NativeBattleEffectArena position = PutWord(PutWord(PutWord(NativeBattleEffectArena.Clear(), 0, anchor.X - rotated.X), 2, anchor.Y - rotated.Y + vertical), 4, anchor.Z - rotated.Z);
            frame = WriteModel(frame, model, 28, position, 4, inputs);
            frame = WriteModel(frame, model, 32, CopyBytes(position, 4, NativeBattleEffectArena.Clear(), 0, 2), 2, inputs);
        }
        else
        {
            frame = Restore(frame, model, saved, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (Signed(task, 12) == 66)
        {
            (frame, uint child) = Append(frame, 0x221C600, 0x5988B0);
            if (child == 0)
            {
                throw new InvalidDataException("The selected actor completion requires its available child slot.");
            }
            uint mask = 4u << (Signed(task, 26) & 31);
            NativeBattleModulePoolTask output = Read(frame, child).WriteWord(12, 0).WriteWord(18, 0).WriteWord(16, task.ReadWord(16)).WriteWord(20, task.ReadWord(20)).WriteWord(14, unchecked((ushort)mask));
            frame = Write(frame, child, output);
        }
        short next = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        if (next < 68)
        {
            return new(frame, 0);
        }
        NativeBattleEffectArena final = inputs.ReadModelArena9C(frame, model);
        int flags = (unchecked((ushort)Word(final, 0)) & 0xEFDF) | unchecked((ushort)Word(frame.State.ReadView(saved, 2), 0));
        frame = WriteModel(frame, model, 0, PutWord(NativeBattleEffectArena.Clear(), 0, flags), 2, inputs);
        frame = inputs.Execute(frame, new NativeBattleEffect321Contact506690(Signed(Read(frame, address), 26)));
        return new(frame, 2);
    }

    private static NativeBattleEffect321Frame Restore(NativeBattleEffect321Frame frame, uint model, uint saved, NativeBattleEffect321Inputs inputs)
    {
        frame = WriteModel(frame, model, 28, frame.State.ReadView(saved + 12, 4), 4, inputs);
        frame = WriteModel(frame, model, 32, frame.State.ReadView(saved + 16, 4), 4, inputs);
        frame = WriteModel(frame, model, 12, frame.State.ReadView(saved + 4, 4), 4, inputs);
        return WriteModel(frame, model, 16, frame.State.ReadView(saved + 8, 4), 4, inputs);
    }

    private static NativeBattleEffect321Frame WriteModel(NativeBattleEffect321Frame frame, uint model, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect321Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect321WriteModelBytes(model, offset, data, length));
}
