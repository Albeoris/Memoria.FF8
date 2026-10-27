namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep AdvanceWorldMesh(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        uint callback = task.Callback;
        NativeBattleVector angles = callback switch
        {
            0x5EBFC0 => new(512, task.ReadWord(14) != 0 ? (short)768 : (short)-768, Word(task, 24)),
            0x5ED300 or 0x5EE0F0 => new(0, Word(task, 24), 0),
            0x5EFFF0 => new(0, 0, 0),
            _ => throw new NotSupportedException("Native Lion Heart world mesh callback is unsupported.")
        };
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(angles);
        if (callback == 0x5EBFC0)
        {
            world = NativeBattleGfMatrices.Multiply56C090(ReadMatrix(frame.Staging, 0x23C58E0), world);
        }
        world = world with { X = Word(task, 16), Y = callback is 0x5ED300 or 0x5EFFF0 ? 0 : Word(task, 18), Z = Word(task, 20) };
        short scale = Word(task, 28);
        world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, callback == 0x5ED300 ? Word(task, 32) : scale, scale);
        int fadeStart = callback == 0x5EBFC0 ? 4 : callback == 0x5ED300 ? 2 : callback == 0x5EE0F0 ? 12 : 6;
        int fade = callback is 0x5EBFC0 or 0x5EE0F0 ? 682 * (counter - fadeStart) : callback == 0x5ED300 ? (counter - fadeStart) << 9 : 409 * (counter - fadeStart);
        uint mesh = callback == 0x5EBFC0 ? 0xD8392Cu : callback == 0x5ED300 ? 0xD84BE4u : callback == 0x5EE0F0 ? 0xD7D9A0u : 0xD83244u;
        frame = inputs.Execute(frame, new NativeBattleLionHeart165MeshDraw(mesh, NativeBattlePoseMatrices.Compose56C2F0(view, world), new(counter >= fadeStart ? 0xF3u : 0x33u, 0, counter >= fadeStart ? fade : null, null, null, null, 17, 2), callback == 0x5ED300));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (callback == 0x5EBFC0)
        {
            task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
        }
        int scaleOffset = callback == 0x5ED300 ? 32 : 28;
        int speedOffset = callback == 0x5ED300 ? 34 : 30;
        short speed = Word(task, speedOffset);
        int divisor = callback == 0x5EBFC0 ? 7 : callback == 0x5EFFF0 ? 6 : 4;
        task = task.WriteWord(scaleOffset, unchecked((ushort)(Word(task, scaleOffset) + speed))).WriteWord(speedOffset, unchecked((ushort)(speed - speed / divisor)));
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= (callback == 0x5EE0F0 ? 18 : callback == 0x5EFFF0 ? 16 : 10) ? 2 : 0);
    }
}
