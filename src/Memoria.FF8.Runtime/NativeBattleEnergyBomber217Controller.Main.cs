using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEnergyBomber217Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEnergyBomber217Controller
{
    public static (NativeBattleEnergyBomber217Frame Frame, int Result) AdvanceMain(NativeBattleEnergyBomber217Frame frame, uint address, NativeBattleEnergyBomber217Resources resources, NativeBattleEnergyBomber217Inputs inputs)
    {
        int age = unchecked((short)Read(frame, address).ReadWord(12));
        int depth = age < 12 ? unchecked(age * 2300) / 12 : age >= 74 ? unchecked((86 - age) * 2300) / 12 : 2300;
        frame = inputs.Execute(frame, new NativeBattleEnergyBomber217Stage(unchecked((short)depth), 0));
        if ((uint)age < 67)
        {
            NativeBattleEffectArena position;
            if (age < 47)
            {
                NativeBattleEnergyBomber217PointStep bone = inputs.ReadBone502170(frame, Actor(frame.State.ReadDword(CasterGlobal)), 39, 0);
                frame = bone.Frame;
                NativeBattleVector point = bone.Position with { Y = unchecked((short)(bone.Position.Y - 512)) };
                if (age == 46)
                {
                    NativeBattleVector previous = TaskVector(Read(frame, address), 0x10);
                    frame = Write(frame, address, PutVector(Read(frame, address), 0x18, new(unchecked((short)(point.X - previous.X)), unchecked((short)(point.Y - previous.Y)), unchecked((short)(point.Z - previous.Z)))));
                }
                position = Packed(point);
                frame = Write(frame, address, CopyToTask(Read(frame, address), 0x10, position, 8));
            }
            else if (age < 55)
            {
                NativeBattleVector point = TaskVector(Read(frame, address), 0x10);
                NativeBattleVector velocity = TaskVector(Read(frame, address), 0x18);
                frame = Write(frame, address, PutVector(Read(frame, address), 0x10, new(unchecked((short)(point.X + velocity.X)), unchecked((short)(point.Y + velocity.Y)), unchecked((short)(point.Z + velocity.Z)))));
                position = TaskView(Read(frame, address), 0x10, 8);
            }
            else
            {
                int start = age < 59 ? 55 : age < 63 ? 59 : 63;
                int factor = Ease(age - start);
                if (age == 55)
                {
                    frame = Random(frame, out ushort roll);
                    NativeBattleModulePoolTask task = Read(frame, address);
                    NativeBattleVector target = TaskVector(task, 0x28);
                    NativeBattleVector current = TaskVector(task, 0x10);
                    NativeBattleVector destination = new(unchecked((short)(target.X + (roll & 0xFFF) - 2048)), unchecked((short)(target.Y - 2000)), unchecked((short)(target.Z + (current.Z - target.Z) * 3 / 4)));
                    frame = Write(frame, address, PutVector(task, 0x20, destination));
                }
                else if (age == 59)
                {
                    NativeBattleModulePoolTask task = Read(frame, address);
                    task = CopyToTask(task, 0x10, TaskView(task, 0x20, 8), 8);
                    NativeBattleVector previous = TaskVector(task, 0x20);
                    NativeBattleVector target = TaskVector(task, 0x28);
                    NativeBattleVector destination = new(unchecked((short)-previous.X), target.Y, unchecked((short)(target.Z + (previous.Z - target.Z) * 3 / 4)));
                    frame = Write(frame, address, PutVector(task, 0x20, destination));
                }
                else if (age == 63)
                {
                    NativeBattleModulePoolTask task = Read(frame, address);
                    task = CopyToTask(task, 0x10, TaskView(task, 0x20, 8), 8);
                    frame = Write(frame, address, PutVector(task, 0x20, TaskVector(task, 0x28)));
                }
                NativeBattleModulePoolTask currentTask = Read(frame, address);
                NativeBattleEnergyBomber217PointStep blended = inputs.BlendPoints56CB90(frame, TaskVector(currentTask, 0x10), TaskVector(currentTask, 0x20), unchecked((short)(4096 - factor)), unchecked((short)factor));
                frame = blended.Frame;
                position = Packed(blended.Position);
            }
            NativeBattleEffectArena context = Context(frame, position);
            frame = inputs.AdvanceAct(frame, address, 0x44, 0x398, resources.MainAct, context).Frame;
            frame = DrawTrail(frame, address, inputs);
            if (age >= 55)
            {
                NativeBattleModulePoolTask task = Read(frame, address);
                task = CopyToTask(task, 0x38, TaskView(task, 0x30, 8), 8);
                task = CopyToTask(task, 0x30, position, 8).WriteWord(0x40, unchecked((ushort)Math.Min(2, unchecked((short)task.ReadWord(0x40)) + 1))).WriteWord(0x42, 0);
                frame = Write(frame, address, task);
            }
        }
        int impactAge = age - 63;
        if ((uint)impactAge < 23)
        {
            if (impactAge == 0)
            {
                NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0x2A8].ToImmutableArray()).Initialize7016B0(resources.ImpactAct, 0x2A8);
                frame = Write(frame, address, Read(frame, address).WriteBytes(0x3DC, workspace.Data.AsSpan()));
            }
            else if (impactAge < 5)
            {
                frame = BirthSprites(frame, address);
            }
            frame = inputs.AdvanceAct(frame, address, 0x3DC, 0x2A8, resources.ImpactAct, Context(frame, TaskView(Read(frame, address), 0x28, 8))).Frame;
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEnergyBomber217Sound(resources.Sound));
        }
        if (age == 78)
        {
            frame = inputs.Execute(frame, new NativeBattleEnergyBomber217Contact(FirstContact(frame, inputs)));
        }
        if (age >= 86)
        {
            if (age == 86)
            {
                frame = inputs.Execute(frame, new NativeBattleEnergyBomber217Stage(0, 0));
                frame = Write(frame, address, Read(frame, address).WriteWord(12, 87));
            }
            return (frame, 2);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, 0);
    }

    private static NativeBattleEnergyBomber217Frame DrawTrail(NativeBattleEnergyBomber217Frame frame, uint address, NativeBattleEnergyBomber217Inputs inputs)
    {
        int count = unchecked((short)Read(frame, address).ReadWord(0x40));
        if (count <= 0)
        {
            return frame;
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Energy Bomber requires its actual trail temporary memory."), 120);
        frame = frame with { Staging = allocation.Staging };
        for (int index = unchecked((short)Read(frame, address).ReadWord(0x42)); index < count; index++)
        {
            NativeBattleVector point = TaskVector(Read(frame, address), checked(0x30 + index * 8));
            frame = inputs.Execute(frame, new NativeBattleEnergyBomber217DrawTrail120(allocation.Address, point, unchecked(3072 + index * 1024 / 2)));
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 120) };
        return Write(frame, address, Read(frame, address).WriteWord(0x42, unchecked((ushort)(Read(frame, address).ReadWord(0x42) + 1))));
    }

    private static NativeBattleEnergyBomber217Frame BirthSprites(NativeBattleEnergyBomber217Frame frame, uint address)
    {
        for (int index = 0; index < 16; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
            if (!pool.Slots.Any(task => (task.Flags & 1) == 0))
            {
                break;
            }
            (pool, int slot) = pool.Append(SpriteCallback);
            frame = WithPool(frame, 2, pool);
            frame = Random(frame, out ushort yaw);
            frame = Random(frame, out ushort pitch);
            NativeBattleVector direction = NativeBattleRinoa89Controller.Direction6A6020(pitch, yaw);
            frame = Random(frame, out ushort roll);
            int speed = (roll & 0x1FF) + 512;
            NativeBattleVector velocity = new(unchecked((short)(unchecked(direction.X * speed) >> 12)), unchecked((short)(unchecked(direction.Y * speed) >> 12)), unchecked((short)(unchecked(direction.Z * speed) >> 12)));
            NativeBattleVector acceleration = new(unchecked((short)(-velocity.X >> 4)), unchecked((short)(-velocity.Y >> 4)), unchecked((short)(-velocity.Z >> 4)));
            NativeBattleModulePoolTask child = pool.Slots[slot];
            child = PutVector(child, 12, TaskVector(Read(frame, address), 0x28)).WriteWord(18, 0);
            child = PutVector(child, 20, velocity);
            child = PutVector(child, 26, acceleration);
            frame = Write(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), child);
        }
        return frame;
    }

    public static (NativeBattleEnergyBomber217Frame Frame, int Result) AdvanceSprite(NativeBattleEnergyBomber217Frame frame, uint address, NativeBattleEnergyBomber217Inputs inputs)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Energy Bomber requires its actual sprite temporary memory."), 180);
        frame = inputs.Execute(frame with { Staging = allocation.Staging }, new NativeBattleEnergyBomber217DrawSprite180(address, allocation.Address));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 180) };
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleVector velocity = TaskVector(task, 20);
        NativeBattleVector acceleration = TaskVector(task, 26);
        velocity = new(unchecked((short)(velocity.X + acceleration.X)), unchecked((short)(velocity.Y + acceleration.Y)), unchecked((short)(velocity.Z + acceleration.Z)));
        NativeBattleVector position = TaskVector(task, 12);
        task = PutVector(task, 20, velocity);
        task = PutVector(task, 12, new(unchecked((short)(position.X + velocity.X)), unchecked((short)(position.Y + velocity.Y)), unchecked((short)(position.Z + velocity.Z))));
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        frame = Write(frame, address, task.WriteWord(18, unchecked((ushort)phase)));
        return (frame, phase >= 12 ? 2 : 0);
    }

    private static NativeBattleEffectArena Context(NativeBattleEnergyBomber217Frame frame, NativeBattleEffectArena point) => NativeBattleEffectNodeContextMemory.Copy(point, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 0, 8).Write(8, BitConverter.GetBytes(-128)).Write(12, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));
    private static NativeBattleEffectArena Packed(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    private static NativeBattleVector TaskVector(NativeBattleModulePoolTask task, int offset) => new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));
    private static NativeBattleModulePoolTask PutVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    private static NativeBattleEnergyBomber217Frame Random(NativeBattleEnergyBomber217Frame frame, out ushort roll) => frame with { Random = frame.Random.Draw(out roll) };
    private static int Ease(int age)
    {
        int factor = unchecked((age * 1024 + 512) >> 2);
        for (int index = 0; index < 3; index++)
        {
            factor = NativeBattleCameraMath.Sine(factor) >> 2;
        }
        return NativeBattleCameraMath.Sine(factor);
    }
}
