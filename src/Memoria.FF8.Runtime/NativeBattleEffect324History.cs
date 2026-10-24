using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect324Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect324Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect324History
{
    public static NativeBattleEffect324Frame Capture597750(NativeBattleEffect324Frame frame, uint model, uint destination, NativeBattleEffect324Inputs inputs)
    {
        ImmutableArray<NativeBattlePackedMatrix32> bones = inputs.ReadCurrentBoneMatrices32(frame, model);
        for (int index = 0; index < bones.Length; index++)
        {
            frame = frame with { State = frame.State.WriteView(checked(destination + (uint)(index * 32)), PackedArena(bones[index]), 32) };
        }
        return frame;
    }

    public static NativeBattleEffect324Frame Restore597790(NativeBattleEffect324Frame frame, uint model, uint source, NativeBattleEffect324Inputs inputs)
    {
        int count = inputs.ReadCurrentBoneMatrices32(frame, model).Length;
        ImmutableArray<NativeBattlePackedMatrix32>.Builder bones = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(count);
        for (int index = 0; index < count; index++)
        {
            (byte[] bytes, bool[] known) = frame.State.ReadView(checked(source + (uint)(index * 32)), 32).ReadSnapshot(0, 32);
            bones.Add(new(bytes.ToImmutableArray(), known.ToImmutableArray()));
        }
        return inputs.Execute(frame, new NativeBattleEffect324WriteCurrentBoneMatrices32(model, bones.MoveToImmutable()));
    }

    public static NativeBattleEffect324Step Advance(NativeBattleEffect324Frame frame, uint address, NativeBattleEffect324Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint model = Caster324(frame);
        if (age == 0)
        {
            frame = Capture597750(frame, model, 0x22010A8, inputs);
            NativeBattleEffectArena initial = inputs.ReadModelArena9C(frame, model);
            frame = WriteModel324(frame, model, 124, Put324(NativeBattleEffectArena.Clear(), 0, Dword324(initial, 124) & ~2u), 4, inputs);
            initial = inputs.ReadModelArena9C(frame, model);
            frame = frame with { State = frame.State.WriteView(0x2201080, NativeBattleEffectNodeContextMemory.Copy(initial, 64, NativeBattleEffectArena.Clear(), 0, 32), 32) };
        }
        frame = Capture597750(frame, model, 0x2202428, inputs);
        NativeBattleEffectArena actor = inputs.ReadModelArena9C(frame, model);
        NativeBattleEffectArena savedColor = NativeBattleEffectNodeContextMemory.Copy(actor, 40, NativeBattleEffectArena.Clear(), 0, 4);
        NativeBattleEffectArena savedMask = NativeBattleEffectNodeContextMemory.Copy(actor, 124, NativeBattleEffectArena.Clear(), 0, 4);
        NativeBattleEffectArena savedWorld = NativeBattleEffectNodeContextMemory.Copy(actor, 64, NativeBattleEffectArena.Clear(), 0, 32);
        if (age > 6)
        {
            frame = WriteModel324(frame, model, 64, frame.State.ReadView(0x2201080, 32), 32, inputs);
        }
        frame = WriteModel324(frame, model, 124, Put324(NativeBattleEffectArena.Clear(), 0, 2), 4, inputs);
        frame = Restore597790(frame, model, 0x22010A8, inputs);
        if (age >= 16)
        {
            actor = inputs.ReadModelArena9C(frame, model);
            frame = Color324(frame, 0, Dword324(actor, 40), unchecked((uint)((age - 16) << 8)), inputs, out uint color);
            frame = WriteModel324(frame, model, 40, Put324(NativeBattleEffectArena.Clear(), 0, (color & 0xFFFFFF) | 0x02000000), 4, inputs);
            frame = WriteModel324(frame, model, 7, NativeBattleEffectArena.Clear().Write(0, [0]), 1, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect324DrawModel5977D0(model));
        frame = Restore597790(frame, model, 0x2202428, inputs);
        frame = WriteModel324(frame, model, 124, savedMask, 4, inputs);
        frame = WriteModel324(frame, model, 40, savedColor, 4, inputs);
        if (age > 6)
        {
            frame = WriteModel324(frame, model, 64, savedWorld, 32, inputs);
        }
        if (Paused324(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 32 ? 2 : 0);
    }
}
