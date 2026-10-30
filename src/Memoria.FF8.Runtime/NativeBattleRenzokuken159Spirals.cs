using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken159Controller
{
    public static NativeBattleRenzokuken159ChildStep Advance601EC0(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleRenzokuken159State state = State(frame);
        byte[] particles = state.Spirals.ToArray();
        if (counter < state.Duration - 8)
        {
            for (int emitted = 0; emitted < 3; emitted++)
            {
                int index = 0;
                //601EC0 admits only the first40 of602C90's60 physical spiral slots.
                while (index < 40 && Read(particles, index * 16) >= 0)
                {
                    index++;
                }
                if (index == 40)
                {
                    break;
                }
                int offset = index * 16;
                Write(particles, offset, 0);
                NativeBattleRenzokukenVisualLifetimes.Allocated(frame.Effect, NativeBattleRenzokukenVisualLifetimes.Bank(frame.Effect.Binding.Effect, 2), index);
                Write(particles, offset + 2, Rand() % 768 + 1024);
                int bones = (inputs.ReadBoneCount ?? throw new NotSupportedException("Native601EC0 requires the original body pose bone-count BYTE."))(state.Actor);
                int bone = bones == 0 ? 0 : Rand() % bones;
                NativeBattleVector position = (inputs.ReadBone ?? throw new NotSupportedException("Native601EC0 requires actual502170 current body-bone positions."))(state.Actor, bone, 0);
                Write(particles, offset + 4, position.X);
                Write(particles, offset + 6, position.Y);
                Write(particles, offset + 8, position.Z);
                Write(particles, offset + 10, Rand() % 10 + 2);
                Write(particles, offset + 12, unchecked((short)task.ReadWord(14)) % 24);
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))).WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1)));
        frame = WriteState(frame, State(frame) with { Spirals = particles.ToImmutableArray() });
        return new(frame, pool.WriteSlot(slot, task), unchecked((short)task.ReadWord(12)) >= state.Duration - 1 ? 2 : 0);

        int Rand()
        {
            NativeCrtRandomState next = frame.Random.Draw(out ushort value);
            frame = frame with { Random = next };
            return value;
        }
    }
}
