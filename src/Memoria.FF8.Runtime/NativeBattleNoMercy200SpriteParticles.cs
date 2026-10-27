namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNoMercy200SpriteDraw(NativeBattleModuleDraw Draw) : NativeBattleNoMercy200Operation
{
    public uint? TemporaryAddress { get; init; }
}

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep AdvanceTargetParticle(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs, NativeBattleModuleResource sharedSprite5)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        if (callback is not (0x639A60 or 0x639DE0 or 0x639EF0))
        {
            throw new InvalidDataException("Native No Mercy target particle requires its original callback.");
        }
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (Word(task, 14) > 0)
        {
            if (!paused)
            {
                task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) - 1)));
            }
            return new(frame, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback)), 0);
        }
        int scale = Word(task, callback == 0x639DE0 ? 32 : 28);
        NativeBattleVector point = Point(task);
        ushort phase = callback == 0x639DE0 ? unchecked((ushort)(9 - task.ReadWord(12))) : task.ReadWord(12);
        NativeBattleModuleResource resource = callback == 0x639DE0 ? sharedSprite5 : Sprite(callback == 0x639A60 ? 0xE3382Cu : 0xE33B84u);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(phase), Phase = phase, Anchor = new(point.X, point.Y, point.Z, Word(task, 22)), Scale = scale, DepthOffset = -(scale >> (callback == 0x639A60 ? 4 : 3)), SpriteFlags = callback == 0x639DE0 ? (ushort)16 : (ushort)0, SpritePaletteOffsets = callback == 0x639DE0 ? [4, 0, 0, 0] : [] };
        frame = inputs.Execute(frame, new NativeBattleNoMercy200SpriteDraw(draw));
        if (callback == 0x639A60)
        {
            NativeBattleModuleResource ground = Sprite(0xE339D8);
            int height = Math.Max((int)point.Y, -2000);
            byte tint = unchecked((byte)(160 * ((height << 12) / 2000) / 4096 - 96));
            NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(1024, 0, 0)), scale, scale, 4096);
            draw = draw with { Resource = ground, SpriteFrame = ground.ProjectedFrame571C80(phase), Anchor = new(point.X, Word(task, 22), point.Z, 0), DepthOffset = 0, SpriteFlags = 12, SpriteColor = (uint)(tint | tint << 8 | tint << 16), SpriteWorldRotation = rotation.Rotation };
            frame = inputs.Execute(frame, new NativeBattleNoMercy200SpriteDraw(draw));
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        if (callback == 0x639A60 && task.ReadWord(12) == 0)
        {
            for (int index = 0; index < 4; index++)
            {
                (pool, int childSlot) = pool.Append(0x639EF0);
                NativeBattleModulePoolTask child = CopyPoint(pool.Slots[childSlot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(index + Rand() % 6))), task);
                child = child.WriteWord(16, unchecked((ushort)(Word(child, 16) + Rand() % 300 - 150)));
                child = child.WriteWord(18, unchecked((ushort)(Word(child, 18) - Rand() % 100)));
                child = child.WriteWord(20, unchecked((ushort)(Word(child, 20) + Rand() % 300 - 150)));
                child = child.WriteWord(22, unchecked((ushort)(-20 - Rand() % 30))).WriteWord(28, unchecked((ushort)(Rand() % 1280 + 2560)));
                pool = pool.WriteSlot(childSlot, child);
            }
            for (int index = 0; index < 10; index++)
            {
                (pool, int childSlot) = pool.Append(0x639DE0);
                NativeBattleModulePoolTask child = pool.Slots[childSlot].WriteWord(12, unchecked((ushort)(Rand() % 4 + 5)));
                child = CopyPoint(child.WriteWord(14, unchecked((ushort)(index + Rand() % 4))), task);
                child = child.WriteWord(16, unchecked((ushort)(Word(child, 16) + Rand() % 400 - 200)));
                child = child.WriteWord(18, unchecked((ushort)(Word(child, 18) - Rand() % 200)));
                child = child.WriteWord(20, unchecked((ushort)(Word(child, 20) + Rand() % 400 - 200)));
                child = child.WriteWord(24, unchecked((ushort)(Rand() % 80 - 40)));
                child = child.WriteWord(26, unchecked((ushort)(-20 - Rand() % 50)));
                child = child.WriteWord(28, unchecked((ushort)(Rand() % 80 - 40)));
                child = child.WriteWord(32, unchecked((ushort)(Rand() % 1536 + 2304)));
                pool = pool.WriteSlot(childSlot, child);
            }
        }
        bool complete;
        if (callback == 0x639DE0)
        {
            short x = Word(task, 24);
            short y = Word(task, 26);
            short z = Word(task, 28);
            task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) - 64)));
            task = task.WriteWord(16, unchecked((ushort)(Word(task, 16) + x))).WriteWord(24, unchecked((ushort)(x - (x >> 4))));
            task = task.WriteWord(18, unchecked((ushort)(Word(task, 18) + y))).WriteWord(26, unchecked((ushort)(y + (y >> 4))));
            task = task.WriteWord(20, unchecked((ushort)(Word(task, 20) + z))).WriteWord(28, unchecked((ushort)(z - (z >> 4))));
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) - 1)));
            complete = Word(task, 12) < 0;
        }
        else
        {
            if (callback == 0x639EF0)
            {
                short speed = Word(task, 22);
                task = task.WriteWord(18, unchecked((ushort)(Word(task, 18) + speed))).WriteWord(22, unchecked((ushort)(speed + (speed >> 5))));
                task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) - (Word(task, 28) >> 6))));
            }
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            complete = Word(task, 12) >= 16;
        }
        return new(frame, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback)), complete ? 2 : 0);

        NativeBattleModuleResource Sprite(uint address)
        {
            NativeBattleSearchSpriteResource source = frame.Memory.NoMercy200?.Resources?.Sprites.Single(sprite => sprite.Address == address) ?? throw new NotSupportedException("Native No Mercy particle requires its original entered sprite.");
            return new(0, source.Address, 0, 0, source.Bytes);
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static NativeBattleModulePoolTask CopyPoint(NativeBattleModulePoolTask target, NativeBattleModulePoolTask source) => TaskDword(TaskDword(target, 16, TaskDword(source, 16)), 20, TaskDword(source, 20));
}
