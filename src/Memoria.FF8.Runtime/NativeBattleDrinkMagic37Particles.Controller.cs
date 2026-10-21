using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrinkMagic37Anchor(byte Actor, short Distance, NativeBattleVector Value) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrinkMagic37ParticleInputs(byte ParameterActor, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors, Func<byte, short, NativeBattleVector> ReadAnchor12, Func<byte, short> ReadYaw, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> Position8DC700);
public sealed record NativeBattleDrinkMagic37ParticleStep(NativeBattleEffectHierarchy Hierarchy, NativeCrtRandomState Random, ImmutableArray<byte> CapturedAnchor8, ImmutableArray<NativeBattleDrawStock15ParticleOperation> Operations, int Result);

public static partial class NativeBattleDrinkMagic37Particles
{
    public static NativeBattleDrinkMagic37ParticleStep Advance(NativeBattleEffectHierarchy hierarchy, uint address, NativeCrtRandomState random, ImmutableArray<byte> capturedAnchor8, NativeBattleDrinkMagic37ParticleInputs inputs)
    {
        NativeBattleEffectTaskPool pool = hierarchy.Pools.Single(pool => pool.DescriptorAddress == 0x26A06C8);
        if (pool.BankAddress != 0x26A06D8 || pool.Stride != 304 || pool.Slots.Length != 100 || address < pool.BankAddress || address - pool.BankAddress >= 304 * 100 || (address - pool.BankAddress) % 304 != 0 || capturedAnchor8.IsDefault || capturedAnchor8.Length != 8)
        {
            throw new InvalidDataException("Drink Magic particles require their retained anchor and original 304-byte bank.");
        }
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback is 0x85E930 or 0x85F060)
        {
            NativeBattleDrawStock15ParticleStep leaf = NativeBattleDrawStock15Particles.AdvanceDrinkMagic37Leaf(hierarchy, address, random, new(inputs.ParameterActor, inputs.SelectActors, _ => throw new InvalidOperationException("Drink Magic trajectories use their captured anchor.")), capturedAnchor8);
            return new(leaf.Hierarchy, leaf.Random, capturedAnchor8, leaf.Operations, leaf.Result);
        }
        ImmutableArray<NativeBattleDrawStock15ParticleOperation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleDrawStock15ParticleOperation>();
        if (task.Callback == 0x85BBD0)
        {
            Scheduler();
            short age = Word(task, 0x24);
            if (age > 13 && age < 63)
            {
                uint child = Append(3, 0x85BD50, 304);
                Jitter(child);
            }
        }
        else if (task.Callback == 0x85BD50)
        {
            Related();
        }
        else
        {
            throw new NotSupportedException("Drink Magic particle callback has no original handler.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        return new(complete ? hierarchy.Complete8DC530(address) : hierarchy, random, capturedAnchor8, operations.ToImmutable(), complete ? 2 : 0);

        int Rand()
        {
            random = random.Draw(out ushort value);
            return value;
        }

        void Next()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }

        uint Append(int poolIndex, uint callback, int size)
        {
            hierarchy = hierarchy.Write(address, task);
            NativeBattleEffectHierarchyAppend result = hierarchy.Append8DC540(poolIndex, callback, size, address, inputs.SelectActors);
            hierarchy = result.Hierarchy;
            task = hierarchy.Read(address);
            if (result.Address == 0)
            {
                throw new NotSupportedException("Drink Magic task allocation returned null before the original field stores.");
            }
            return result.Address;
        }

        void Jitter(uint child)
        {
            NativeBattleDrawStock15Jitter jitter = NativeBattleDrawStock15Particles.Offset8B5BB0(hierarchy.Read(child), 128, random);
            random = jitter.Random;
            operations.AddRange(jitter.Operations);
            hierarchy = hierarchy.Write(child, jitter.Task);
        }

        void Trajectory(ushort selector, bool last)
        {
            uint child = Append(3, 0x85E930, 304);
            Jitter(child);
            NativeBattleModulePoolTask created = hierarchy.Read(child).WriteWord(0x120, selector);
            if (last)
            {
                created = created.WriteWord(0x122, 1);
            }
            hierarchy = hierarchy.Write(child, created);
        }

        void Scheduler()
        {
            int phase = unchecked((sbyte)task.ReadByte(0x29));
            short age = Word(task, 0x24);
            switch (phase)
            {
                case 0:
                    NativeBattleVector anchor = inputs.ReadAnchor12(inputs.ParameterActor, 0);
                    operations.Add(new NativeBattleDrinkMagic37Anchor(inputs.ParameterActor, 0, anchor));
                    NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
                    short yaw = inputs.ReadYaw(inputs.ParameterActor);
                    operations.Add(new NativeBattleDrawStock15Rotate(identity, 'Y', yaw));
                    NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(identity, yaw);
                    NativeBattleVector local = new(0, -256, 0);
                    operations.Add(new NativeBattleDrawStock15Transform(rotation, local));
                    NativeBattleVector moved = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
                    byte[] bytes = capturedAnchor8.ToArray();
                    BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)(anchor.X + moved.X)));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), unchecked((short)(anchor.Y + moved.Y)));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), unchecked((short)(anchor.Z + moved.Z)));
                    capturedAnchor8 = bytes.ToImmutableArray();
                    task = inputs.Position8DC700(task);
                    _ = Append(4, NativeBattleDrinkMagic37Profile.OrbCallback, 96);
                    Next();
                    break;
                case 1:
                case 2:
                case 3:
                    if (age == 38 + 2 * phase)
                    {
                        Trajectory((ushort)(phase - 1), false);
                        Next();
                    }
                    break;
                case 4:
                    if (age == 46)
                    {
                        Trajectory(3, false);
                        Trajectory(4, true);
                        task = task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 1));
                        Next();
                    }
                    break;
                case 5:
                    break;
                default:
                    throw new InvalidDataException("Drink Magic scheduler phase leaves its six original handlers.");
            }
        }

        void Related()
        {
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    task = task.WriteDword(0x4C, 0x15D9624).WriteWord(0x52, 10).WriteWord(0x54, 0).WriteWord(0x126, (ushort)(Rand() & 0xFFF)).WriteDword(0x88, task.ReadDword(0x1C)).WriteDword(0x8C, task.ReadDword(0x20));
                    task = task.WriteDword(0x80, BinaryPrimitives.ReadUInt32LittleEndian(capturedAnchor8.AsSpan())).WriteDword(0x84, BinaryPrimitives.ReadUInt32LittleEndian(capturedAnchor8.AsSpan().Slice(4)));
                    NativeBattleVector direction = new(unchecked((short)(Word(task, 0x80) - Word(task, 0x88))), unchecked((short)(Word(task, 0x82) - Word(task, 0x8A))), unchecked((short)(Word(task, 0x84) - Word(task, 0x8C))));
                    NativeBattleActionMatrix basis = NativeBattleDrawStock15Particles.LookBasis8DDA50(direction);
                    operations.Add(new NativeBattleDrawStock15LookBasis(direction, new(0, 4096, 0)));
                    short angle = (short)((Rand() & 0x7FF) + 0x400);
                    operations.Add(new NativeBattleDrawStock15Rotate(basis, 'Z', angle) { Padding18 = NativeBattleDrawStock15Particles.ReadPadding(task, 0x72) });
                    basis = NativeBattleCuragaMatrices.RotateZ8DD960(basis, angle);
                    for (int lane = 0; lane < 9; lane++)
                    {
                        task = task.WriteWord(0x60 + lane * 2, unchecked((ushort)basis.Rotation[lane]));
                    }
                    task = task.WriteWord(0x12C, (ushort)((Rand() & 0x1FF) + 0xA00));
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x12E, unchecked((ushort)(task.ReadWord(0x12E) + 0x30)));
                    task = task.WriteWord(0x12C, unchecked((ushort)(task.ReadWord(0x12C) + task.ReadWord(0x12E))));
                    if (Word(task, 0x12C) >= 4096)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                        Next();
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Drink Magic related particle phase leaves its three original handlers.");
            }
            short counter = Word(task, 0x12C);
            task = task.WriteDword(0x1C, task.ReadDword(0x88)).WriteDword(0x20, task.ReadDword(0x8C));
            for (int axis = 0; axis < 3; axis++)
            {
                int delta = Word(task, 0x80 + 2 * axis) - Word(task, 0x88 + 2 * axis);
                task = task.WriteWord(0x1C + 2 * axis, unchecked((ushort)(Word(task, 0x1C + 2 * axis) + unchecked(delta * counter) / 4096)));
            }
            NativeBattleActionMatrix rotation = NativeBattleDrawStock15Particles.ReadRotation(task, 0x60);
            NativeBattleVector local = new(0, unchecked((short)(NativeBattleCameraMath.Sine(unchecked((short)(unchecked(counter << 11) / 4096))) / 3)), 0);
            operations.Add(new NativeBattleDrawStock15Transform(rotation, local));
            NativeBattleVector moved = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
            task = task.WriteWord(0x1C, unchecked((ushort)(Word(task, 0x1C) + moved.X))).WriteWord(0x1E, unchecked((ushort)(Word(task, 0x1E) + moved.Y))).WriteWord(0x20, unchecked((ushort)(Word(task, 0x20) + moved.Z)));
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                operations.Add(new NativeBattleDrawStock15HeadSprite(task));
            }
            task = NativeBattleDrawStock15Particles.AdvanceTrailCounters(task);
            task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
            if (Word(task, 0x50) > Word(task, 0x52))
            {
                task = task.WriteWord(0x50, 0);
            }
        }
    }
}
