using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15Rotate(NativeBattleActionMatrix Matrix, char Axis, short Angle) : NativeBattleDrawStock15ParticleOperation
{
    public ushort? Padding18 { get; init; }
}
public sealed record NativeBattleDrawStock15Transform(NativeBattleActionMatrix Matrix, NativeBattleVector Point) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15LookBasis(NativeBattleVector Direction, NativeBattleVector Up) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15Anchor(byte Actor, NativeBattleModuleAnchor Value) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15HeadSprite(NativeBattleModulePoolTask Task) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15SparkleSprite(NativeBattleModulePoolTask Task) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15Ribbon(NativeBattleModulePoolTask Task) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrawStock15ParticleStep(NativeBattleEffectHierarchy Hierarchy, NativeCrtRandomState Random, ImmutableArray<NativeBattleDrawStock15ParticleOperation> Operations, int Result);
public sealed record NativeBattleDrawStock15ParticleInputs(byte ParameterActor, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors, Func<byte, NativeBattleModuleAnchor> ReadBone241);

public static partial class NativeBattleDrawStock15Particles
{
    public static NativeBattleDrawStock15ParticleStep Advance(NativeBattleEffectHierarchy hierarchy, uint address, NativeCrtRandomState random, NativeBattleDrawStock15ParticleInputs inputs)
    {
        return AdvanceCore(hierarchy, address, random, inputs, false, null);
    }

    public static NativeBattleDrawStock15ParticleStep AdvanceDrinkMagic37Leaf(NativeBattleEffectHierarchy hierarchy, uint address, NativeCrtRandomState random, NativeBattleDrawStock15ParticleInputs inputs, ImmutableArray<byte> capturedAnchor8)
    {
        return AdvanceCore(hierarchy, address, random, inputs, true, (task, state) => NativeBattleDrinkMagic37Particles.InitializeTrajectory(task, capturedAnchor8, state));
    }

    private static NativeBattleDrawStock15ParticleStep AdvanceCore(NativeBattleEffectHierarchy hierarchy, uint address, NativeCrtRandomState random, NativeBattleDrawStock15ParticleInputs inputs, bool drinkMagic, Func<NativeBattleModulePoolTask, NativeCrtRandomState, NativeBattleDrawStock15Jitter>? initializeTrajectory)
    {
        uint descriptor = drinkMagic ? 0x26A06C8u : 0x2732CF8u;
        uint bank = drinkMagic ? 0x26A06D8u : 0x2732D08u;
        int stride = drinkMagic ? 304 : 300;
        uint trajectoryCallback = drinkMagic ? 0x85E930u : 0x8B5C70u;
        uint sparkleCallback = drinkMagic ? 0x85F060u : 0x8B6440u;
        NativeBattleEffectTaskPool pool = hierarchy.Pools.Single(pool => pool.DescriptorAddress == descriptor);
        if (pool.BankAddress != bank || pool.Stride != stride || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Draw Stock particles require their original physical task bank.");
        }
        int poolIndex = hierarchy.Pools.IndexOf(pool);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (address < pool.BankAddress || address - pool.BankAddress >= stride * 100 || (address - pool.BankAddress) % stride != 0)
        {
            throw new InvalidDataException("Draw Stock particle callback leaves its actual bank.");
        }
        ImmutableArray<NativeBattleDrawStock15ParticleOperation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleDrawStock15ParticleOperation>();
        if (!drinkMagic && task.Callback == 0x8B5AE0)
        {
            Scheduler();
        }
        else if (task.Callback == trajectoryCallback)
        {
            Trajectory();
        }
        else if (task.Callback == sparkleCallback)
        {
            Sparkle();
        }
        else
        {
            throw new NotSupportedException("Native particle leaf callback has no implementation for its original layout.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(hierarchy, random, operations.ToImmutable(), complete ? 2 : 0);

        int Rand()
        {
            random = random.Draw(out ushort value);
            return value;
        }

        void NextPhase()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }

        uint Append(uint callback)
        {
            hierarchy = hierarchy.Write(address, task);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(poolIndex, callback, checked((ushort)stride), address, inputs.SelectActors);
            hierarchy = appended.Hierarchy;
            task = hierarchy.Read(address);
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Draw Stock particle allocation returned null before its original field writes.");
            }
            return appended.Address;
        }

        void CreateTrajectory(ushort selector, bool last)
        {
            uint child = Append(trajectoryCallback);
            NativeBattleModulePoolTask created = hierarchy.Read(child);
            NativeBattleDrawStock15Jitter jitter = Offset8B5BB0(created, 128, random);
            random = jitter.Random;
            operations.AddRange(jitter.Operations);
            created = jitter.Task.WriteWord(0x120, selector);
            if (last)
            {
                created = created.WriteWord(0x122, 1);
            }
            hierarchy = hierarchy.Write(child, created);
        }

        void Scheduler()
        {
            short counter = Word(task, 0x24);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    NativeBattleModulePoolTask first = hierarchy.Read(task.ReadDword(0x14));
                    task = task.WriteDword(0x1C, first.ReadDword(0x38)).WriteDword(0x20, first.ReadDword(0x3C));
                    if (counter >= 0)
                    {
                        CreateTrajectory(0, false);
                        NextPhase();
                    }
                    break;
                case 1:
                    if (counter == 2)
                    {
                        CreateTrajectory(1, false);
                        NextPhase();
                    }
                    break;
                case 2:
                    if (counter == 4)
                    {
                        CreateTrajectory(2, false);
                        NextPhase();
                    }
                    break;
                case 3:
                    if (counter == 6)
                    {
                        CreateTrajectory(3, false);
                        CreateTrajectory(4, true);
                        task = task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 1));
                        NextPhase();
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Draw Stock scheduler signed phase leaves its five original handlers.");
            }
        }

        void Trajectory()
        {
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    if (initializeTrajectory is not null)
                    {
                        NativeBattleDrawStock15Jitter initialized = initializeTrajectory(task, random);
                        task = initialized.Task;
                        random = initialized.Random;
                        operations.AddRange(initialized.Operations);
                    }
                    else
                    {
                        task = task.WriteDword(0x4C, 0x162B520).WriteWord(0x52, 5).WriteWord(0x54, 0).WriteWord(0x126, (ushort)(Rand() & 0xFFF)).WriteDword(0x88, task.ReadDword(0x1C)).WriteDword(0x8C, task.ReadDword(0x20));
                        NativeBattleModuleAnchor endpoint = inputs.ReadBone241(inputs.ParameterActor);
                        operations.Add(new NativeBattleDrawStock15Anchor(inputs.ParameterActor, endpoint));
                        task = task.WriteWord(0x80, unchecked((ushort)endpoint.X)).WriteWord(0x82, unchecked((ushort)endpoint.Y)).WriteWord(0x84, unchecked((ushort)endpoint.Z));
                        NativeBattleVector direction = new(unchecked((short)(Word(task, 0x80) - Word(task, 0x88))), unchecked((short)(Word(task, 0x82) - Word(task, 0x8A))), unchecked((short)(Word(task, 0x84) - Word(task, 0x8C))));
                        NativeBattleActionMatrix basis = LookBasis8DDA50(direction);
                        operations.Add(new NativeBattleDrawStock15LookBasis(direction, new(0, 4096, 0)));
                        short angle = (short)((Rand() & 0x3FF) + ((task.ReadByte(0x120) & 1) != 0 ? 0x300 : 0x900));
                        operations.Add(new NativeBattleDrawStock15Rotate(basis, 'Z', angle) { Padding18 = ReadPadding(task, 0x72) });
                        basis = NativeBattleCuragaMatrices.RotateZ8DD960(basis, angle);
                        task = WriteRotation(task, 0x60, basis);
                    }
                    NextPhase();
                    break;
                case 1:
                    if (Word(task, 0x124) < 10)
                    {
                        _ = Append(sparkleCallback);
                    }
                    else
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteWord(0x124, 10);
                        NextPhase();
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Draw Stock trajectory signed phase leaves its three original handlers.");
            }
            NativeBattleDrawStock15Position moved = Position8B5E00(task);
            task = moved.Task;
            operations.Add(new NativeBattleDrawStock15Transform(moved.Matrix, moved.Point));
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                operations.Add(new NativeBattleDrawStock15HeadSprite(task));
            }
            task = task.WriteWord(0x126, (ushort)((task.ReadWord(0x126) + 0x180) & 0xFFF));
            operations.Add(new NativeBattleDrawStock15Ribbon(task));
            task = AdvanceTrailCounters(task);
            task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
            if (Word(task, 0x50) > Word(task, 0x52))
            {
                task = task.WriteWord(0x50, 0);
            }
        }

        void Sparkle()
        {
            NativeBattleModulePoolTask parent = hierarchy.Read(task.ReadDword(0x18));
            task = task.WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20));
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    task = task.WriteDword(0x4C, drinkMagic ? 0x15D9504u : 0x162B730u).WriteWord(0x52, 5).WriteWord(0x54, 0).WriteWord(0x42, (ushort)(Rand() & 0xFFF)).WriteWord(0x44, (ushort)(Rand() & 0xFFF));
                    NextPhase();
                    break;
                case 1:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                    if (Word(task, 0x50) > Word(task, 0x52))
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteWord(0x50, task.ReadWord(0x52));
                        NextPhase();
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new InvalidDataException("Draw Stock sparkle signed phase leaves its three original handlers.");
            }
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                operations.Add(new NativeBattleDrawStock15SparkleSprite(task));
            }
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
