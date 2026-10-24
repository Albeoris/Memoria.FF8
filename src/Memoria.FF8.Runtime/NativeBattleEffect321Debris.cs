using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect321Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect321Debris
{
    public static NativeBattleEffect321Step Advance(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        (frame, uint workAddress) = Allocate(frame, 104);
        NativeBattleModulePoolTask task = Read(frame, address);
        if (!Paused(frame, inputs) && Signed(task, 12) <= 5)
        {
            NativeBattleEffectArena sourcePoint = TaskView(task, 16, 8);
            frame = PublishContext(frame, workAddress, CopyBytes(sourcePoint, 0, Context(frame, workAddress, 104), 0, 8), 104);
            frame = Birth(frame, sourcePoint);
        }
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena work = CopyBytes(PackedArena(inputs.ReadView(frame)), 0, Context(frame, workAddress, 104), 8, 32);
        frame = PublishContext(frame, workAddress, work, 104);
        frame = PublishContext(frame, temporary, Put(Put(Context(frame, temporary, 88), 0, 0xCD1C24), 28, 0x30), 88);
        int survivors = 0;
        for (int index = 0; index < NativeBattleEffect321Records.Count; index++)
        {
            uint current = NativeBattleEffect321Records.First + (uint)(index * 24);
            NativeBattleEffectArena row = frame.State.ReadView(current, 24);
            if ((row.Read(0, 1)[0] & 2) == 0)
            {
                continue;
            }
            int age = Word(row, 4);
            int scale = Word(row, 6);
            if (age >= 8)
            {
                scale = unchecked(scale - (age - 8) * (scale / 8));
            }
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(Word(row, 16), 0, Word(row, 20))) with { X = Word(row, 8), Y = Word(row, 10), Z = Word(row, 12) };
            local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((short)scale), unchecked((short)scale), unchecked((short)scale));
            work = Context(frame, workAddress, 104);
            work = PutWord(PutWord(PutWord(work, 0, Word(row, 16)), 2, 0), 4, Word(row, 20));
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(local, null);
            work = CopyBytes(PackedArena(packed), 0, work, 0x28, 32);
            for (int axis = 0; axis < 3; axis++)
            {
                work = Put(work, 0x48 + axis * 4, unchecked((uint)scale));
            }
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            NativeBattlePackedMatrix32 output = NativeBattlePackedMatrix32.FromMatrix(composed, null);
            work = CopyBytes(PackedArena(output), 0, work, 0x28, 18);
            work = CopyBytes(PackedArena(output), 20, work, 0x3C, 12);
            frame = PublishContext(frame, workAddress, work, 104);
            frame = LoadMatrix(frame, output);
            frame = DrawStatic(frame, temporary, resources, inputs);
            if (Paused(frame, inputs))
            {
                continue;
            }
            row = frame.State.ReadView(current, 24);
            if (Word(row, 4) >= 16)
            {
                row = Put(row, 0, 0);
            }
            else
            {
                row = PutWord(row, 4, Word(row, 4) + 1);
                row = PutWord(row, 10, Word(row, 10) + Word(row, 18));
                row = PutWord(row, 18, Word(row, 18) - (Word(row, 18) >> 3));
                row = PutWord(row, 16, Word(row, 16) + Word(row, 14));
                row = PutWord(row, 20, Word(row, 20) + Word(row, 22));
                survivors++;
            }
            frame = frame with { State = frame.State.WriteView(current, row, 24) };
        }
        frame = Release(Release(frame, 88), 104);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)))), survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect321Frame Birth(NativeBattleEffect321Frame frame, NativeBattleEffectArena point)
    {
        for (int birth = 0; birth < 5; birth++)
        {
            uint address = NativeBattleEffect321Records.FindFree(frame);
            if (address == 0)
            {
                return frame;
            }
            NativeBattleEffectArena row = PutWord(Put(frame.State.ReadView(address, 24), 0, 2), 4, 0);
            row = PutWord(row, 6, Random(ref frame) % 1280 + 768);
            row = CopyBytes(point, 0, row, 8, 8);
            row = PutWord(row, 8, Word(row, 8) + Random(ref frame) % 3800 - 1900);
            row = PutWord(row, 10, Word(row, 10) - Random(ref frame) % 250);
            row = PutWord(row, 12, Word(row, 12) + Random(ref frame) % 3800 - 1900);
            int angle = Random(ref frame) & 4095;
            row = PutWord(row, 16, angle);
            row = PutWord(row, 18, -110 - Random(ref frame) % 240);
            int second = angle >> 1;
            row = PutWord(row, 20, second);
            int spin = Random(ref frame) % 70 + 30;
            row = PutWord(row, 14, (angle & 1) != 0 ? -spin : spin);
            spin = Random(ref frame) % 40 + 15;
            row = PutWord(row, 22, (second & 1) != 0 ? -spin : spin);
            frame = frame with { State = frame.State.WriteView(address, row, 24) };
        }
        return frame;
    }
}
