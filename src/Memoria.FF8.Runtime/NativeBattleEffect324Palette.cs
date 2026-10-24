using static Memoria.FF8.Runtime.NativeBattleEffect324Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect324Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect324Palette
{
    public static NativeBattleEffect324Step Advance(NativeBattleEffect324Frame frame, uint address, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        uint model = Model324(Signed(task, 14));
        uint color;
        if (age < 4)
        {
            frame = Color324(frame, inputs.ReadColor(frame, 0xB8B7D8), resources.MaterialColors24[0], unchecked((uint)((4 - age) << 10)), inputs, out color);
        }
        else if (age >= 42)
        {
            frame = Color324(frame, 0, resources.MaterialColors24[Signed(task, 28)], unchecked((uint)((age - 42) << 9)), inputs, out color);
        }
        else
        {
            int index = (age - 4) % 24;
            task = task.WriteWord(28, unchecked((ushort)index));
            frame = Write(frame, address, task);
            color = resources.MaterialColors24[index];
        }
        frame = WriteModel324(frame, model, 40, Put324(NativeBattleEffectArena.Clear(), 0, color), 4, inputs);
        NativeBattleEffectArena actor = inputs.ReadModelArena9C(frame, model);
        ushort flags = unchecked((ushort)(Word324(actor, 0) | 0x800));
        frame = WriteModel324(frame, model, 0, PutWord324(NativeBattleEffectArena.Clear(), 0, flags), 2, inputs);
        if (Paused324(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        if (next < 50)
        {
            return new(frame, 0);
        }
        actor = inputs.ReadModelArena9C(frame, model);
        flags = unchecked((ushort)(Word324(actor, 0) & 0xF7FF));
        frame = WriteModel324(frame, model, 0, PutWord324(NativeBattleEffectArena.Clear(), 0, flags), 2, inputs);
        return new(frame, 2);
    }
}
