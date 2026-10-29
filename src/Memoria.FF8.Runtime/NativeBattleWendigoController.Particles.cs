using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWendigoController
{
    private sealed partial class Execution
    {
        private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
        private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
        private static NativeBattleActionMatrix TaskMatrix(NativeBattleModulePoolTask task, int offset) => new(Enumerable.Range(0, 9).Select(index => Signed(task, offset + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(offset + 20)), unchecked((int)task.ReadDword(offset + 24)), unchecked((int)task.ReadDword(offset + 28)), 4096);
        private static NativeBattleModulePoolTask TaskMatrix(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix value)
        {
            for (int index = 0; index < 9; index++)
            {
                task = task.WriteWord(offset + index * 2, unchecked((ushort)value.Rotation[index]));
            }
            return task.WriteDword(offset + 20, unchecked((uint)value.X)).WriteDword(offset + 24, unchecked((uint)value.Y)).WriteDword(offset + 28, unchecked((uint)value.Z));
        }
        private void DustSpawner(uint address)
        {
            NativeBattleModulePoolTask parent = Read(address);
            if (Stage(parent) == 0)
            {
                uint child = Append(3, shoot ? 0x845B30u : 0x84CA70u, TaskSize, address);
                NativeBattleModulePoolTask task = Read(child).WriteWord(shoot ? 0x1F8 : 0xD8, 8);
                int positions = shoot ? 0x78 : 0x58;
                int velocities = shoot ? 0xF8 : 0x98;
                for (int index = 0; index < 8; index++)
                {
                    task = task.WriteDword(positions + index * 8, parent.ReadDword(0x1C)).WriteDword(positions + index * 8 + 4, parent.ReadDword(0x20));
                    int angle = index * 512 + (parent.ReadWord(0x24) == 0 ? 0 : 256);
                    int distance = Roll() & 511;
                    task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) + NativeBattleCameraMath.Sine(angle) * distance / 4096))).WriteWord(0x20, unchecked((ushort)(Signed(task, 0x20) + NativeBattleCameraMath.Cosine(angle) * distance / 4096)));
                    int speed = (Roll() & 2047) + 1024;
                    task = Vector(task, velocities + index * 8, new(unchecked((short)(NativeBattleCameraMath.Sine(angle) * speed / 4096)), unchecked((short)(-128 - (Roll() & 1023))), unchecked((short)(NativeBattleCameraMath.Cosine(angle) * speed / 4096))));
                }
                Store(child, task);
                if (Signed(parent, 0x24) >= 1)
                {
                    Store(address, Next(Read(address)));
                }
            }
            else if (Stage(parent) == 1)
            {
                Store(address, Next(parent.WriteByte(0x26, (byte)(parent.ReadByte(0x26) | 1))));
            }
        }
        private void Dust(uint address)
        {
            if (Stage(Read(address)) == 0)
            {
                Store(address, Next(Read(address).WriteDword(0x4C, resources.Dust.Address).WriteWord(0x52, 15)));
            }
            else if (Stage(Read(address)) == 1)
            {
                MoveParticles(address, false);
            }
            DrawParticles(address);
        }
        private void BurstSpawner(uint address)
        {
            NativeBattleModulePoolTask parent = Read(address);
            if (Stage(parent) == 0)
            {
                uint child = Append(3, 0x844E30, TaskSize, address);
                int count = parent.ReadWord(0x24) == 0 ? 10 : 5;
                bool large = parent.ReadWord(0x1FA) != 0;
                NativeBattleModuleResource resource = (large ? resources.LargeBurst : resources.SmallBurst) ?? throw new NotSupportedException("Native Shoot requires its selected burst flipbook.");
                NativeBattleModulePoolTask task = Read(child).WriteWord(0x1F8, (ushort)count).WriteDword(0x4C, resource.Address).WriteWord(0x52, large ? (ushort)13 : (ushort)7);
                for (int index = 0; index < count; index++)
                {
                    int speed = 4096 + (Roll() & 4095);
                    NativeBattleActionMatrix turn = Identity();
                    if ((Roll() & 4095) != 0)
                    {
                        turn = NativeBattleCuragaMatrices.RotateY8DD8A0(turn, (short)(Roll() & 4095));
                    }
                    turn = NativeBattleCuragaMatrices.RotateX8DD7E0(turn, 0x300);
                    NativeBattleVector velocity = NativeBattleGfMatrices.Transform460840(turn with { X = 0, Y = 0, Z = 0 }, new(0, (short)speed, 0));
                    velocity = NativeBattleGfMatrices.Transform460840(TaskMatrix(parent, 0x58) with { X = 0, Y = 0, Z = 0 }, velocity);
                    task = task.WriteDword(0x78 + index * 8, parent.ReadDword(0x1C)).WriteDword(0x7C + index * 8, parent.ReadDword(0x20));
                    task = Vector(task, 0xF8 + index * 8, velocity).WriteWord(0x17A + index * 8, (ushort)(176 + (Roll() & 63)));
                }
                Store(child, task);
                if (Signed(parent, 0x24) >= 1)
                {
                    Store(address, Next(Read(address)));
                }
            }
            else if (Stage(parent) == 1)
            {
                Store(address, Next(parent.WriteByte(0x26, (byte)(parent.ReadByte(0x26) | 1))));
            }
        }
        private void Burst(uint address)
        {
            if (Stage(Read(address)) == 0)
            {
                MoveParticles(address, true);
            }
            DrawParticles(address);
        }
        private void MoveParticles(uint address, bool burst)
        {
            NativeBattleModulePoolTask task = Read(address);
            int positions = shoot ? 0x78 : 0x58;
            int velocities = shoot ? 0xF8 : 0x98;
            for (int index = 0; index < Signed(task, shoot ? 0x1F8 : 0xD8); index++)
            {
                if (burst)
                {
                    task = task.WriteWord(velocities + index * 8 + 2, unchecked((ushort)(Signed(task, velocities + index * 8 + 2) + Signed(task, 0x17A + index * 8))));
                }
                for (int axis = 0; axis < 6; axis += 2)
                {
                    int velocity = Signed(task, velocities + index * 8 + axis);
                    short changed = unchecked((short)(velocity - velocity * (burst ? 400 : 768) / 4096));
                    task = task.WriteWord(velocities + index * 8 + axis, unchecked((ushort)changed));
                    task = task.WriteWord(positions + index * 8 + axis, unchecked((ushort)(Signed(task, positions + index * 8 + axis) + changed / 16)));
                }
            }
            int frame = unchecked((short)(task.ReadWord(0x50) + 1));
            if (frame > Signed(task, 0x52))
            {
                frame = Signed(task, 0x52);
                task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
            }
            Store(address, task.WriteWord(0x50, unchecked((ushort)frame)));
        }
        private void DrawParticles(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            int positions = shoot ? 0x78 : 0x58;
            for (int index = 0; index < Signed(task, shoot ? 0x1F8 : 0xD8); index++)
            {
                task = task.WriteDword(0x1C, task.ReadDword(positions + index * 8)).WriteDword(0x20, task.ReadDword(positions + index * 8 + 4));
                Store(address, task);
                if ((task.ReadByte(0x26) & 4) != 0)
                {
                    continue;
                }
                NativeBattleModuleResource? resource = task.ReadDword(0x4C) == resources.Dust.Address ? resources.Dust : task.ReadDword(0x4C) == resources.SmallBurst?.Address ? resources.SmallBurst : task.ReadDword(0x4C) == resources.LargeBurst?.Address ? resources.LargeBurst : null;
                if (resource is null)
                {
                    throw new NotSupportedException("Native Wendigo requires its exact initialized flipbook.");
                }
                NativeBattleVector p = Vector(task, 0x1C);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(Signed(task, 0x50)), Phase = task.ReadWord(0x50), Direction = 4096, Anchor = new(p.X, p.Y, p.Z, 0), Scale = 4096, DepthOffset = Signed(task, 0x54), SpriteFlags = 0, PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(current.Effect.Binding.Effect, task, index) with { ResourceGeneration = resource.Address } };
                Emit(new NativeBattleWendigoSprite(draw, false));
            }
        }
    }
}
