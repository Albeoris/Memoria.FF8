using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEnemyMicroMissiles284Controller
{
    private static (NativeBattleEnemyMicroMissiles284Frame Frame, int Result) AdvanceRoot(NativeBattleEnemyMicroMissiles284Frame frame, uint address, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(frame.State.ReadDword(ArenaGlobal) + (alternate ? 0x47A8u : 0x147A8u)));
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeSecondaryBanks(frame);
            frame = SpawnMissiles284(frame, resources, inputs);
        }
        int missiles = 1;
        int impacts = 1;
        int particles = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, missiles) = AdvancePool(frame, 3, resources, inputs);
            if (missiles == 0 && Read(frame, address).ReadByte(15) == 1)
            {
                NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Enemy Micro Missiles requires its current first physical contact.");
                frame = inputs.Execute(frame, new NativeBattleEnemyMicroMissiles284Contact506690(first));
                frame = Write(frame, address, Read(frame, address).WriteByte(15, 2));
            }
            (frame, impacts) = AdvancePool(frame, 2, resources, inputs);
            (frame, particles) = AdvancePool(frame, 1, resources, inputs);
        }
        if (Signed(Read(frame, address), 12) == 2)
        {
            frame = inputs.Execute(frame, new NativeBattleEnemyMicroMissiles284Sound501330(resources.Sound, 0, 128));
        }
        root = Read(frame, address);
        if (root.ReadByte(15) != 0 && missiles == 0 && impacts == 0 && particles == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static NativeBattleEnemyMicroMissiles284Frame SpawnMissiles284(NativeBattleEnemyMicroMissiles284Frame frame, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Enemy Micro Missiles requires its actual first physical recipient.");
        uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
        NativeBattleGraphicsModel caster = inputs.ReadModel(frame, frame.State.ReadDword(CasterGlobal));
        short yaw = caster.Rotation14 ?? throw new NotSupportedException("Enemy Micro Missiles requires its current caster yaw.");
        NativeBattleModelTransform transform = caster.Transform ?? throw new NotSupportedException("Enemy Micro Missiles requires its current caster fields.");
        NativeBattleEffectArena source = SourcePoint67BB70(yaw, -500, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        short x = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(source.Read(0, 2)) + transform.Position28.X));
        short y = unchecked((short)(transform.Angle36 - 1000));
        short z = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(source.Read(4, 2)) + transform.Position28.Z));
        source = source.Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
        NativeBattleEffectArena endpoint = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int index = 0; index < 6; index++)
        {
            (frame, uint missile) = Append(frame, 0x2507DE0, 0x67BBB0);
            if (missile == 0)
            {
                throw new InvalidDataException("Enemy Micro Missiles requires its freshly reset six-slot missile bank.");
            }
            ushort step = checked((ushort)(4096 / (20 + index * 4)));
            frame = WriteBytes(frame, missile + 36, source, 8);
            frame = WriteBytes(frame, missile + 44, source, 8);
            frame = Write(frame, missile, Read(frame, missile).WriteWord(42, step).WriteWord(50, step));
            while (true)
            {
                frame = DrawRandom(frame, out ushort random);
                int bone = unchecked(random * inputs.ReadBoneCount(frame, target)) >> 15;
                NativeBattleEnemyMicroMissiles284PointStep selected = ReadAnchor67E550(frame, target, bone, endpoint, inputs);
                frame = selected.Frame;
                if (selected.Result == 0)
                {
                    endpoint = selected.Point8;
                    break;
                }
            }
            ushort variant = 0;
            if (index % 3 == 1)
            {
                frame = DrawRandom(frame, out ushort randomX);
                short endpointX = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(endpoint.Read(0, 2)) + (randomX & 2047) - 1024));
                frame = DrawRandom(frame, out ushort randomZ);
                short endpointZ = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(endpoint.Read(4, 2)) + (randomZ & 2047) - 1024));
                short endpointY = (inputs.ReadModel(frame, target).Transform ?? throw new NotSupportedException("Enemy Micro Missiles requires its current target WORD24.")).Angle36;
                endpoint = endpoint.Write(0, BitConverter.GetBytes(endpointX)).Write(2, BitConverter.GetBytes(endpointY)).Write(4, BitConverter.GetBytes(endpointZ));
                variant = 1;
            }
            short targetX = BinaryPrimitives.ReadInt16LittleEndian(endpoint.Read(0, 2));
            short targetY = BinaryPrimitives.ReadInt16LittleEndian(endpoint.Read(2, 2));
            short targetZ = BinaryPrimitives.ReadInt16LittleEndian(endpoint.Read(4, 2));
            var basis = NativeBattleFunguarLaser277.Basis50CBA0(x - targetX, y - targetY, targetZ - z);
            frame = Write(frame, missile, Read(frame, missile).WriteWord(32, unchecked((ushort)basis.Length)).WriteWord(34, variant));
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(TaskView(Read(frame, missile), 52, 2020), 0, 2020);
            workspace = workspace.Initialize7016B0(resources.Missile, 2020);
            frame = WriteBytes(frame, missile + 52, workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), 2020);
            NativeBattleActionMatrix roll = Roll7012C0(index * 256 - 640);
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(basis.Matrix, roll);
            NativeBattleModulePoolTask task = Read(frame, missile).WriteWord(30, 0);
            for (int lane = 0; lane < 9; lane++)
            {
                task = task.WriteWord(12 + lane * 2, unchecked((ushort)rotation.Rotation[lane]));
            }
            frame = Write(frame, missile, task);
        }
        return frame;
    }

    public static NativeBattleEffectArena SourcePoint67BB70(int yaw, int radius, NativeBattleEffectArena retainedPoint8)
    {
        int x = unchecked(NativeBattleCameraMath.Sine(yaw) * radius) >> 12;
        int z = unchecked(-unchecked(NativeBattleCameraMath.Cosine(yaw) * radius)) >> 12;
        return retainedPoint8.Write(0, BitConverter.GetBytes(unchecked((short)x))).Write(4, BitConverter.GetBytes(unchecked((short)z)));
    }

    private static NativeBattleActionMatrix Roll7012C0(int angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        return new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
    }

    public static NativeBattleActionHeaderSlot Header(NativeBattleEnemyMicroMissiles284Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(ParametersGlobal) != 0x1D99A78)
        {
            throw new NotSupportedException("Enemy Micro Missiles requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Enemy Micro Missiles parameter header leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
