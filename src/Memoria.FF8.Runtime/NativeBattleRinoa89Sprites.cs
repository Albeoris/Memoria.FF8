using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89SpriteServices(Func<NativeBattleRinoa89Frame, NativeBattleVector, ushort, short, short, NativeBattleRinoa89Frame> DrawBillboard571C80, Func<NativeBattleRinoa89Frame, NativeBattleActionMatrix, NativeBattleVector, NativeBattleRinoa89Frame> DrawMesh572200);
public sealed record NativeBattleRinoa89SpriteBirth(NativeBattleRinoa89Frame Frame, NativeBattleEffectTaskPool Pool);

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89SpriteBirth SpawnSprites6FD1B0(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, NativeBattleVector point, int particles)
    {
        RequireSpritePool(pool);
        if (particles < 0)
        {
            throw new InvalidDataException("Native6FD1B0 requires its actual nonnegative particle count.");
        }
        if (pool.Slots.Any(task => (task.Flags & 1) == 0))
        {
            (pool, int slot) = pool.Append(0x6FD340);
            NativeBattleModulePoolTask task = SpritePosition(pool.Slots[slot], point).WriteWord(18, 4096).WriteBytes(31, [0]);
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        }
        for (int index = 0; index < particles; index++)
        {
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                break;
            }
            (pool, int slot) = pool.Append(0x6FD3C0);
            NativeBattleModulePoolTask task = SpritePosition(pool.Slots[slot], point).WriteWord(18, 4096).WriteBytes(31, [15]);
            task = task.WriteWord(20, unchecked((ushort)((BeamRandom(ref frame) & 0x7F) - 64)));
            task = task.WriteWord(22, unchecked((ushort)(128 - (BeamRandom(ref frame) & 0x7F))));
            task = task.WriteWord(24, unchecked((ushort)((BeamRandom(ref frame) & 0x7F) + 127)));
            task = task.WriteBytes(26, [unchecked((byte)BeamRandom(ref frame))]);
            task = task.WriteBytes(27, [unchecked((byte)BeamRandom(ref frame))]);
            task = task.WriteBytes(29, [unchecked((byte)((BeamRandom(ref frame) & 0x1F) - 16))]);
            task = task.WriteBytes(30, [unchecked((byte)((BeamRandom(ref frame) & 0x1F) - 16))]);
            task = task.WriteBytes(28, [unchecked((byte)-20)]);
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        }
        return new(frame, pool);
    }

    public static NativeBattleRinoa89TaskStep AdvanceSprite6FD340(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRinoa89SpriteServices services)
    {
        RequireSpritePool(pool);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        RequireSpriteTask(task, 0x6FD340);
        sbyte phase = unchecked((sbyte)task.ReadBytes(31, 1)[0]);
        ushort scale = task.ReadWord(18);
        short displacement = unchecked((short)-(unchecked((short)scale) >> 2));
        frame = services.DrawBillboard571C80(frame, SpritePoint(task), scale, displacement, phase);
        sbyte next = unchecked((sbyte)(phase + 1));
        task = task.WriteBytes(31, [unchecked((byte)next)]);
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, next < 12 ? 0 : 2);
    }

    public static NativeBattleRinoa89TaskStep AdvanceSprite6FD3C0(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRinoa89SpriteServices services)
    {
        RequireSpritePool(pool);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        RequireSpriteTask(task, 0x6FD3C0);
        byte angleX = task.ReadBytes(26, 1)[0];
        byte angleY = task.ReadBytes(27, 1)[0];
        NativeBattleVector point = SpritePoint(task);
        NativeBattleVector angles = new((short)(unchecked((sbyte)angleX) * 16), (short)(unchecked((sbyte)angleY) * 16), 0);
        NativeBattleActionMatrix pose = NativeBattleGfMatrices.Euler56CD50(angles) with { X = point.X, Y = point.Y, Z = point.Z };
        frame = services.DrawMesh572200(frame, pose, angles);
        byte remaining = unchecked((byte)(task.ReadBytes(31, 1)[0] - 1));
        task = task.WriteBytes(31, [remaining]);
        if (remaining == 0)
        {
            return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 2);
        }
        short velocityY = unchecked((short)task.ReadWord(22));
        short x = unchecked((short)(point.X + task.ReadWord(20)));
        short y = unchecked((short)(point.Y + velocityY));
        short z = unchecked((short)(point.Z + task.ReadWord(24)));
        short nextVelocityY = y < 0 ? unchecked((short)(velocityY + unchecked((sbyte)task.ReadBytes(28, 1)[0]))) : unchecked((short)-velocityY);
        task = SpritePosition(task, new(x, y, z)).WriteWord(22, unchecked((ushort)nextVelocityY));
        task = task.WriteBytes(26, [unchecked((byte)(angleX + task.ReadBytes(29, 1)[0]))]);
        task = task.WriteBytes(27, [unchecked((byte)(angleY + task.ReadBytes(30, 1)[0]))]);
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 0);
    }

    private static NativeBattleModulePoolTask SpritePosition(NativeBattleModulePoolTask task, NativeBattleVector point) => task.WriteWord(12, unchecked((ushort)point.X)).WriteWord(14, unchecked((ushort)point.Y)).WriteWord(16, unchecked((ushort)point.Z));
    private static NativeBattleVector SpritePoint(NativeBattleModulePoolTask task) => new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16)));
    private static void RequireSpritePool(NativeBattleEffectTaskPool pool)
    {
        if (pool.DescriptorAddress != Sprites || pool.BankAddress != ArenaAddress + 3136 || pool.Stride != 48 || pool.Slots.Length != 32)
        {
            throw new InvalidDataException("Native Wishing Star sprites require their actual pool.");
        }
    }
    private static void RequireSpriteTask(NativeBattleModulePoolTask task, uint callback)
    {
        if ((task.Flags & 1) == 0 || task.Callback != callback)
        {
            throw new InvalidDataException("Native Wishing Star sprite callback lacks its live task owner.");
        }
    }
}

