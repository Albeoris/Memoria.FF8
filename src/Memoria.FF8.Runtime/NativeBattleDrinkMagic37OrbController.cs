using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrinkMagic37OrbDraw(NativeBattleModulePoolTask Task) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrinkMagic37OrbBounds(NativeBattleModulePoolTask Task) : NativeBattleDrawStock15ParticleOperation;
public sealed record NativeBattleDrinkMagic37OrbInputs(NativeBattleActionPacketsState Packets, NativeBattleEffectParameters Parameters, Func<byte, short, NativeBattleVector> ReadAnchor12, Func<byte, short> ReadYaw, Func<byte, NativeBattleVector> ReadPosition1C);
public sealed record NativeBattleDrinkMagic37OrbStep(NativeBattleEffectHierarchy Hierarchy, ImmutableArray<NativeBattleDrawStock15ParticleOperation> Operations, int Result);

public static class NativeBattleDrinkMagic37OrbController
{
    public static NativeBattleDrinkMagic37OrbStep Advance(NativeBattleEffectHierarchy hierarchy, uint address, NativeBattleDrinkMagic37OrbInputs inputs)
    {
        NativeBattleEffectTaskPool pool = hierarchy.Pools.Single(pool => pool.DescriptorAddress == 0x26A7DA0);
        if (pool.BankAddress != 0x26A7DB0 || pool.Stride != 96 || pool.Slots.Length != 10 || address < pool.BankAddress || address - pool.BankAddress >= 960 || (address - pool.BankAddress) % 96 != 0)
        {
            throw new InvalidDataException("Drink Magic orb requires its original ten-slot task bank.");
        }
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != NativeBattleDrinkMagic37Profile.OrbCallback)
        {
            throw new InvalidDataException("Drink Magic orb requires its original callback.");
        }
        ImmutableArray<NativeBattleDrawStock15ParticleOperation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleDrawStock15ParticleOperation>();
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
                Next();
                break;
            case 1:
                if (Word(task, 0x24) >= 15)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)).WriteDword(0x4C, NativeBattleDrinkMagic37Profile.OrbAddress).WriteDword(0x30, 0x1400).WriteDword(0x34, 0x1400).WriteDword(0x38, unchecked((uint)MaximumDistance(task, inputs))).WriteWord(0x46, unchecked((ushort)inputs.ReadYaw(task.ReadByte(0x2C)))).WriteWord(0x44, 0xF00);
                    Position();
                    task = task.WriteWord(0x50, 4096);
                    Next();
                }
                break;
            case 2:
                Position();
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - 256)));
                if (Word(task, 0x50) <= 0)
                {
                    task = task.WriteWord(0x50, 0).WriteWord(0x5C, 20);
                    Next();
                }
                break;
            case 3:
                Position();
                task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) - 1)));
                if (Word(task, 0x5C) <= 0)
                {
                    Next();
                }
                break;
            case 4:
                Position();
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 256)));
                if (Word(task, 0x50) >= 4096)
                {
                    task = task.WriteWord(0x50, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                break;
            case 5:
                break;
            default:
                throw new InvalidDataException("Drink Magic orb signed phase leaves its six original handlers.");
        }
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            task = task.WriteWord(0x54, (ushort)((task.ReadWord(0x54) + 0xA0) & 0xFFF)).WriteWord(0x58, (ushort)((task.ReadWord(0x58) - 0xA0) & 0xFFF)).WriteWord(0x5E, (ushort)((unchecked((byte)task.ReadWord(0x5E)) + 8) & 0x7F));
            operations.Add(new NativeBattleDrinkMagic37OrbDraw(task));
            operations.Add(new NativeBattleDrinkMagic37OrbBounds(task));
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        return new(complete ? hierarchy.Complete8DC530(address) : hierarchy, operations.ToImmutable(), complete ? 2 : 0);

        void Next()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }

        void Position()
        {
            byte actor = task.ReadByte(0x2C);
            NativeBattleVector anchor = inputs.ReadAnchor12(actor, 0x400);
            operations.Add(new NativeBattleDrinkMagic37Anchor(actor, 0x400, anchor));
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            short yaw = inputs.ReadYaw(actor);
            operations.Add(new NativeBattleDrawStock15Rotate(identity, 'Y', yaw));
            NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(identity, yaw);
            NativeBattleVector local = new(0, -256, 0);
            operations.Add(new NativeBattleDrawStock15Transform(rotation, local));
            NativeBattleVector moved = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
            task = task.WriteWord(0x1C, unchecked((ushort)(anchor.X + moved.X))).WriteWord(0x1E, unchecked((ushort)(anchor.Y + moved.Y))).WriteWord(0x20, unchecked((ushort)(anchor.Z + moved.Z)));
        }
    }

    public static int MaximumDistance(NativeBattleModulePoolTask task, NativeBattleDrinkMagic37OrbInputs inputs)
    {
        int headerIndex = checked(inputs.Parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Drink Magic orb distance requires its actual selected contact pointer.");
        NativeBattleVector caster = inputs.ReadPosition1C(task.ReadByte(0x2C));
        int maximum = 0;
        for (int index = 0; index < header.ReadByte(0x10); index++)
        {
            NativeBattleVector target = inputs.ReadPosition1C(inputs.Packets.ReadContact(first + index).Primary.ActorIndex);
            int x = unchecked((short)target.X) - unchecked((short)caster.X);
            int z = unchecked((short)target.Z) - unchecked((short)caster.Z);
            maximum = Math.Max(maximum, NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(x * x + z * z)));
        }
        return maximum;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset)
    {
        return unchecked((short)task.ReadWord(offset));
    }
}
