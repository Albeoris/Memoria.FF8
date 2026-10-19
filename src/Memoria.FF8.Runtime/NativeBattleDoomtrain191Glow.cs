namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191Glow
{
    public static NativeBattleDoomtrain191Frame Spawn(NativeBattleDoomtrain191Frame frame, uint constructor)
    {
        (ushort Duration, ushort Y, ushort FirstScale, ushort SecondScale) profile = constructor switch
        {
            0x6439C0 => (33, 0xFB82, 0xF00, 0xF00),
            0x643C10 => (99, 0xFB82, 0x1C00, 0x1C00),
            0x643C50 => (18, 0xFB82, 0xF00, 0xF00),
            0x643C90 => (52, 0xFB6E, 0x1300, 0x1000),
            0x643CD0 => (10, 0xFB6E, 0x1300, 0x1000),
            _ => throw new NotSupportedException($"Native Doomtrain glow constructor {constructor:X8} requires its original profile.")
        };
        NativeBattleEffectTaskPool pool = Pool(frame);
        (pool, int slot) = pool.Append(0x643A00);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0).WriteWord(0x10, 0).WriteWord(0xE, profile.Duration).WriteWord(0x12, profile.Y).WriteWord(0x14, 0xDA1C).WriteWord(0x1C, profile.FirstScale).WriteWord(0x1E, profile.SecondScale);
        return PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task) });
    }

    public static NativeBattleDoomtrain191LeafStep Advance643A00(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191MeshServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x643A00)
        {
            throw new InvalidDataException("Native Doomtrain glow requires its actual task.");
        }
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        int angle = memory.OrbitAngle ?? throw new NotSupportedException("Native Doomtrain glow requires its actual shared orbit angle.");
        int step = memory.OrbitStep ?? throw new NotSupportedException("Native Doomtrain glow requires its actual shared orbit step.");
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191!;
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = unchecked((short)task.ReadWord(0x10)), Y = unchecked((short)task.ReadWord(0x12)), Z = unchecked((short)task.ReadWord(0x14)) };
        NativeBattleActionMatrix anchored = NativeBattleGfMatrices.Compose56C2F0(memory.Anchor.ReadMatrix(), local);
        int scale = unchecked((short)task.ReadWord(0x1C));
        int fade = step % 13 == 1 ? 256 : unchecked((NativeBattleCameraMath.Sine(angle) + 4096) / 2 * 2304) >> 12;
        fade = Math.Max(fade, 0);
        NativeBattleActionMatrix first = NativeBattleGfMatrices.Compose56C2F0(state.BaseView.ReadMatrix(), NativeBattlePoseMatrices.Scale56BEF0(anchored, scale, scale, scale));
        frame = services.Draw(frame, new(0xE3A744, first, 0xF3, 3, fade));
        scale = unchecked((short)task.ReadWord(0x1E));
        NativeBattleActionMatrix second = NativeBattleGfMatrices.Compose56C2F0(state.BaseView.ReadMatrix(), NativeBattlePoseMatrices.Scale56BEF0(anchored, scale, scale, scale));
        frame = services.Draw(frame, new(0xE3AD34, second, 0xF3, 3, 0));
        if ((services.ReadFlags() & 1) != 0)
        {
            return new(frame, 0);
        }
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        memory = NativeBattleDoomtrain191Model.Require(frame);
        frame = NativeBattleDoomtrain191Model.Put(frame with { Random = random }, memory with { OrbitAngle = unchecked(angle + step), OrbitStep = unchecked(step + value % 64) });
        ushort next = unchecked((ushort)(task.ReadWord(0xC) + 1));
        pool = Pool(frame);
        frame = PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(0xC, next)) });
        return new(frame, unchecked((short)next) >= unchecked((short)task.ReadWord(0xE)) ? 2 : 0);
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
