namespace Memoria.FF8.Runtime;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5FB7A0(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 2048, 0)), Word(task, 28), Word(task, 28), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        uint flags = counter >= 32 ? 0xF0u : 0x30u;
        uint? selected = null;
        if (counter >= 16)
        {
            int phase = counter - 16;
            for (int vertex = 0; vertex < 93; vertex++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    uint offset = checked((uint)(vertex * 8 + axis * 2));
                    int value = unchecked(ReadWord(frame.Staging, 0xDA2350 + offset) + phase * ReadWord(frame.Staging, 0x23D0258 + offset));
                    frame = frame with { Staging = WriteWord(frame.Staging, 0x23D07A0 + offset, unchecked((ushort)value)) };
                }
            }
            flags |= 0x2000;
            selected = 0x23D07A0;
        }
        NativeBattleModuleMeshContext context = new(flags, counter >= 32 ? 0u : null, counter >= 32 ? (counter - 32) << 9 : null, null, null, null, 17, 2);
        NativeBattleFatedCircle162MeshStep drawn = NativeBattleFatedCircle162Packets.Emit5FB980(frame.Staging, 0xDA2348, selected, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, context, 10 * counter, colors);
        frame = inputs.Execute(frame with { Staging = drawn.Staging }, new NativeBattleFatedCircle162Geometry(drawn.Batch));
        return DampedScale(frame, pool, slot, inputs.GraphicsFlags, task, counter, 40);
    }
}
