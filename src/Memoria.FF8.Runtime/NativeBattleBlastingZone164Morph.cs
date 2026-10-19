namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep Advance5F4720(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (Word(task, 14) > 0)
        {
            if (!paused)
            {
                task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) - 1)));
            }
            return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 0);
        }
        NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), Word(task, 28), Word(task, 32), Word(task, 28));
        NativeBattleActionMatrix matrix = AtCamera(rotation, Billboard(task, Word(task, 28), 0, view));
        NativeBattleFileStaging staging = Forget(StoreMatrix(frame.Staging, 0x23C9DA8, matrix), 0x23C9DBA, 2);
        int count = ReadDword(staging, 0xD8D91C);
        if (count is < 0 or > 105)
        {
            throw new NotSupportedException("Native5F4720 requires its bounded original selected-vertex bank.");
        }
        short counter = Word(task, 12);
        int factor = (counter << 12) / 36;
        for (int index = 0; index < count; index++)
        {
            uint offset = (uint)index * 8;
            int sourceX = ReadWord(staging, 0xD8E410 + offset);
            int sourceY = ReadWord(staging, 0xD8E412 + offset);
            int sourceZ = ReadWord(staging, 0xD8E414 + offset);
            int deltaX = ReadWord(staging, 0xD8E760 + offset) - sourceX;
            int deltaY = ReadWord(staging, 0xD8E762 + offset) - sourceY;
            staging = WriteWord(staging, 0x23C9DC8 + offset, unchecked((ushort)(sourceX + (unchecked(factor * deltaX) >> 12))));
            staging = WriteWord(staging, 0x23C9DCA + offset, unchecked((ushort)(sourceY + (unchecked((factor - 4096) * deltaY) >> 12))));
            staging = WriteWord(staging, 0x23C9DCC + offset, unchecked((ushort)sourceZ));
        }
        frame = frame with { Staging = staging };
        frame = inputs.Execute(frame, new NativeBattleBlastingZone164MeshDraw(0xD8D918, matrix, new(0x20F3, 0, Word(task, 24), null, null, null, 17, 2), false, true) { SelectedVerticesAddress = 0x23C9DC8 });
        if (paused)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 34);
        task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + speed))).WriteWord(12, unchecked((ushort)(counter + 1))).WriteWord(34, unchecked((ushort)(speed - speed / 12)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, Word(task, 12) >= 36 - Word(task, 14) ? 2 : 0);
    }
}
