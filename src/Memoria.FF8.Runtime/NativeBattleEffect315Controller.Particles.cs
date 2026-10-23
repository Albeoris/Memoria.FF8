using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect315Controller
{
    public static (NativeBattleEffect315Frame Frame, int Result) AdvanceDelayedParticles(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 0xE) > 0)
        {
            if (!Paused(frame, inputs))
            {
                frame = Write(frame, address, task.WriteWord(0xE, unchecked((ushort)(Signed(task, 0xE) - 1))));
            }
            return (frame, 0);
        }
        (frame, uint temporary) = Allocate(frame, 72);
        if (!Paused(frame, inputs) && Signed(task, 0xC) <= 20)
        {
            int selector = Signed(task, 0x18);
            if ((uint)selector >= 3)
            {
                throw new InvalidDataException("Effect315 delayed particle source selector leaves its three original rows.");
            }
            NativeBattleActionMatrix world = inputs.ReadModel(frame, ModelAddress(Caster(frame))).GeometryWorldMatrix ?? throw new NotSupportedException("Effect315 particles require the current caster world matrix.");
            NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(PointRowsAddress + (uint)(selector * 8), 8).AsSpan());
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(world, Vector315(source));
            source = source.Write(0, BitConverter.GetBytes(unchecked((short)(rotated.X + Signed(task, 0x10))))).Write(2, BitConverter.GetBytes(unchecked((short)(rotated.Y + Signed(task, 0x12))))).Write(4, BitConverter.GetBytes(unchecked((short)(rotated.Z + Signed(task, 0x14)))));
            NativeBattleEffectArena velocity = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(VectorRowsAddress + (uint)(selector * 16), 16).AsSpan());
            (int vx, int vy, int vz) = NativeBattleGfMatrices.RotateLong56C600(world, Int315(velocity, 0), Int315(velocity, 4), Int315(velocity, 8));
            velocity = velocity.Write(0, BitConverter.GetBytes(vx)).Write(4, BitConverter.GetBytes(vy)).Write(8, BitConverter.GetBytes(vz));
            NativeBattleEffectArena scratch = Context(frame, temporary, 72);
            scratch = NativeBattleEffectNodeContextMemory.Copy(source, 0, scratch, 0, 8);
            scratch = NativeBattleEffectNodeContextMemory.Copy(velocity, 0, scratch, 40, 16);
            frame = PublishContext(frame, temporary, scratch, 72);
            for (int birth = 0; birth < 2; birth++)
            {
                uint rowAddress = FreeParticle315(frame);
                if (rowAddress == 0)
                {
                    break;
                }
                NativeBattleEffectArena row = ReadBytes(frame, rowAddress, ParticleStride).Write(0, BitConverter.GetBytes(unchecked((uint)(int)Signed(task, 0x1A)))).Write(4, new byte[2]);
                frame = Random315(frame, out int scale);
                row = PutParticleWord315(row, 6, scale % 640 + 768);
                row = NativeBattleEffectNodeContextMemory.Copy(source, 0, row, 8, 8);
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = Random315(frame, out int jitter);
                    row = PutParticleWord315(row, 8 + axis * 2, Word315(row, 8 + axis * 2) + jitter % 16 - 8);
                }
                frame = Random315(frame, out int speed);
                int factor = speed % 450 + 140;
                row = PutParticleWord315(row, 16, unchecked(vx * factor) >> 12);
                row = PutParticleWord315(row, 18, unchecked(vy * factor) >> 12);
                row = PutParticleWord315(row, 20, unchecked(vz * factor) >> 12);
                frame = Random315(frame, out int lift);
                row = PutParticleWord315(row, 22, lift % 8 + 12);
                frame = WriteBytes(frame, rowAddress, row, ParticleStride);
            }
        }
        return DrawParticles315(frame, address, temporary, 72, 0, resources, inputs);
    }

    public static (NativeBattleEffect315Frame Frame, int Result) AdvanceVerticalParticles(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs, bool recipient)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 72);
        if (!Paused(frame, inputs) && Signed(task, 0xC) <= (recipient ? 95 : 40))
        {
            for (int birth = 0; birth < (recipient ? 3 : 2); birth++)
            {
                uint rowAddress = FreeParticle315(frame);
                if (rowAddress == 0)
                {
                    break;
                }
                NativeBattleEffectArena row = ReadBytes(frame, rowAddress, ParticleStride).Write(0, BitConverter.GetBytes(recipient ? 0x40u : 0x10u)).Write(4, new byte[2]);
                frame = Random315(frame, out int scale);
                row = PutParticleWord315(row, 6, recipient ? scale % 1280 + unchecked((ushort)(Signed(task, 0xC) + 48)) * 8 : scale % 1152 + 512);
                frame = Random315(frame, out int x);
                row = PutParticleWord315(row, 8, Signed(task, 0x10) + x % (recipient ? 1000 : 800) - (recipient ? 500 : 400));
                frame = Random315(frame, out int y);
                row = PutParticleWord315(row, 10, recipient ? 150 - y % 300 : -150 - y % 950);
                frame = Random315(frame, out int z);
                row = PutParticleWord315(row, 12, Signed(task, 0x14) + z % (recipient ? 1000 : 800) - (recipient ? 500 : 400));
                frame = Random315(frame, out int speed);
                row = PutParticleWord315(row, 18, recipient ? -5 - speed % 25 : -6 - speed % 18);
                frame = WriteBytes(frame, rowAddress, row, ParticleStride);
            }
        }
        return DrawParticles315(frame, address, temporary, 72, recipient ? 2 : 1, resources, inputs);
    }

    public static (NativeBattleEffect315Frame Frame, int Result) AdvanceOrbitParticles(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 80);
        if (!Paused(frame, inputs) && Signed(task, 0xC) <= 60)
        {
            for (int birth = 0; birth < 4; birth++)
            {
                uint rowAddress = FreeParticle315(frame);
                if (rowAddress == 0)
                {
                    break;
                }
                NativeBattleEffectArena row = ReadBytes(frame, rowAddress, ParticleStride).Write(0, BitConverter.GetBytes(0x20u)).Write(4, new byte[2]);
                frame = Random315(frame, out int scale);
                row = PutParticleWord315(row, 6, scale % 768 + 512);
                frame = Random315(frame, out int angle);
                row = PutParticleWord315(row, 8, angle % 4096);
                frame = Random315(frame, out int y);
                row = PutParticleWord315(row, 10, -50 - y % 150);
                frame = Random315(frame, out int radius);
                row = PutParticleWord315(row, 12, radius % 250 + 100);
                frame = Random315(frame, out int angularSpeed);
                row = PutParticleWord315(row, 16, angularSpeed % 65 + 15);
                frame = Random315(frame, out int rise);
                row = PutParticleWord315(row, 18, -5 - rise % 50);
                frame = Random315(frame, out int expansion);
                row = PutParticleWord315(row, 20, expansion % 20 + 5);
                frame = WriteBytes(frame, rowAddress, row, ParticleStride);
            }
        }
        return DrawParticles315(frame, address, temporary, 80, 3, resources, inputs);
    }

    private static (NativeBattleEffect315Frame Frame, int Result) DrawParticles315(NativeBattleEffect315Frame frame, uint taskAddress, uint temporary, int temporaryLength, int kind, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        (frame, uint spriteAddress) = Allocate(frame, 180);
        int matrixOffset = kind == 3 ? 16 : 8;
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleEffectArena scratch = NativeBattleEffectNodeContextMemory.Copy(PackedArena(view), 0, Context(frame, temporary, temporaryLength), matrixOffset, 32);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(SpriteAddresses[kind == 0 ? 0 : kind == 3 ? 2 : 1])).Write(0x24, BitConverter.GetBytes((ushort)(kind == 0 ? 10 : 0)));
        if (kind == 0)
        {
            sprite = sprite.Write(12, BitConverter.GetBytes(4096)).Write(16, BitConverter.GetBytes(2560));
        }
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        if (kind == 3)
        {
            scratch = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0x10, 8), 0, scratch, 8, 8);
        }
        frame = PublishContext(frame, temporary, scratch, temporaryLength);
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view), view) };
        uint mask = kind switch { 0 => unchecked((uint)(int)Signed(task, 0x1A)), 1 => 0x10, 2 => 0x40, _ => 0x20 };
        int survivors = 0;
        for (int index = 0; index < ParticleCount; index++)
        {
            uint rowAddress = checked(ParticleAddress + (uint)(index * ParticleStride));
            NativeBattleEffectArena row = ReadBytes(frame, rowAddress, ParticleStride);
            if ((Uint315(row, 0) & mask) == 0)
            {
                continue;
            }
            NativeBattleVector point;
            int pointOffset = 8;
            if (kind == 3)
            {
                int angle = Word315(row, 8);
                int radius = Word315(row, 12);
                point = new(unchecked((short)((unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12) + Word315(scratch, 8))), Word315(row, 10), unchecked((short)((unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12) + Word315(scratch, 12))));
                scratch = scratch.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
                pointOffset = 0;
            }
            else
            {
                point = Vector315(row, 8);
            }
            (byte[] padding, bool[] paddingKnown) = (kind == 3 ? scratch : row).ReadSnapshot(pointOffset + 6, 2);
            short? w = paddingKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 particles require current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 particles require current arithmetic registers.")), 0, point, w);
            frame = frame with { Memory = NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }) };
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(frame.Memory, unchecked((ushort)Word315(row, 6))) };
            sprite = Context(frame, spriteAddress, 180).Write(4, row.Read(4, 2));
            frame = PublishContext(frame, spriteAddress, sprite, 180);
            if (kind is 0 or 3)
            {
                ImmutableArray<int?> mac = frame.Memory.ScriptedColorRegisters!.Mac;
                int x = mac[0] ?? throw new NotSupportedException("Effect315 requires current MAC1.");
                int y = mac[1] ?? throw new NotSupportedException("Effect315 requires current MAC2.");
                int z = mac[2] ?? throw new NotSupportedException("Effect315 requires current MAC3.");
                (int nx, int ny, int nz) = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                int delta = -(Word315(row, 6) >> (kind == 0 ? 4 : 3));
                int normalOffset = kind == 0 ? 40 : 48;
                int translationOffset = kind == 0 ? 56 : 64;
                scratch = scratch.Write(normalOffset, BitConverter.GetBytes(nx)).Write(normalOffset + 4, BitConverter.GetBytes(ny)).Write(normalOffset + 8, BitConverter.GetBytes(nz));
                scratch = scratch.Write(translationOffset, BitConverter.GetBytes(unchecked(x + (unchecked(nx * delta) >> 12)))).Write(translationOffset + 4, BitConverter.GetBytes(unchecked(y + (unchecked(ny * delta) >> 12)))).Write(translationOffset + 8, BitConverter.GetBytes(unchecked(z + (unchecked(nz * delta) >> 12))));
                frame = frame with { Memory = NativeBattleEffect315Gte.LoadTranslation64DD70(frame.Memory, NativeBattleEffectNodeContextMemory.Copy(scratch, translationOffset, NativeBattleEffectArena.Clear(), 0, 12)) };
            }
            else
            {
                frame = frame with { Memory = NativeBattleEffect315Gte.MacToTranslation64DE40(frame.Memory) };
            }
            frame = PublishContext(frame, temporary, scratch, temporaryLength);
            frame = inputs.Execute(frame, new NativeBattleEffect315DrawSprite180(spriteAddress));
            if (Paused(frame, inputs))
            {
                continue;
            }
            row = ReadBytes(frame, rowAddress, ParticleStride);
            if (Word315(Context(frame, spriteAddress, 180), 0x28) < 0)
            {
                frame = WriteBytes(frame, rowAddress, row.Write(0, new byte[4]), ParticleStride);
                continue;
            }
            row = PutParticleWord315(row, 4, Word315(row, 4) + 1);
            if (kind == 0)
            {
                row = PutParticleWord315(row, 6, Word315(row, 6) + (Word315(row, 6) >> 4));
                row = PutParticleWord315(row, 8, Word315(row, 8) + Word315(row, 16));
                row = PutParticleWord315(row, 10, Word315(row, 10) + unchecked((short)(Word315(row, 18) + Word315(row, 22))));
                row = PutParticleWord315(row, 12, Word315(row, 12) + Word315(row, 20));
                for (int axis = 0; axis < 3; axis++)
                {
                    int velocity = Word315(row, 16 + axis * 2);
                    row = PutParticleWord315(row, 16 + axis * 2, velocity - (velocity >> 3));
                }
            }
            else if (kind == 3)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    row = PutParticleWord315(row, 8 + axis * 2, Word315(row, 8 + axis * 2) + Word315(row, 16 + axis * 2));
                }
            }
            else
            {
                row = PutParticleWord315(row, 10, Word315(row, 10) + Word315(row, 18));
            }
            frame = WriteBytes(frame, rowAddress, row, ParticleStride);
            survivors++;
        }
        frame = Release(Release(frame, 180), temporaryLength);
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        frame = Write(frame, taskAddress, Read(frame, taskAddress).WriteWord(0xC, unchecked((ushort)(Signed(Read(frame, taskAddress), 0xC) + 1))));
        return (frame, survivors == 0 ? 2 : 0);
    }

    private static uint FreeParticle315(NativeBattleEffect315Frame frame)
    {
        for (int index = 0; index < ParticleCount; index++)
        {
            uint address = checked(ParticleAddress + (uint)(index * ParticleStride));
            if (frame.State.ReadDword(address) == 0)
            {
                return address;
            }
        }
        return 0;
    }

    private static NativeBattleEffectArena PutParticleWord315(NativeBattleEffectArena row, int offset, int value) => row.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
}
