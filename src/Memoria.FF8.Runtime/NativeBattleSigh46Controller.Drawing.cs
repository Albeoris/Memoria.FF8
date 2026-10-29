using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSigh46DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal = 0x264EC64, ushort Flags = 0) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46VramCopy505EB0(NativeBattleEffectVramCopy Copy) : NativeBattleSigh46Operation;

public static partial class NativeBattleSigh46Controller
{
    public static (NativeBattleSigh46Frame Frame, int Result) AdvanceCustom8359C0(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                NativeBattleGraphicsModel model = inputs.ReadModel(frame, task.ReadByte(0x2C));
                NativeBattleVector position = (model.Transform ?? throw new NotSupportedException("Sigh requires its current actor position.")).Position28;
                short yaw = model.Rotation14 ?? throw new NotSupportedException("Sigh requires its actual actor rotation WORD14.");
                NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, -768));
                task = task.WriteWord(0x1C, unchecked((ushort)(position.X + offset.X))).WriteWord(0x1E, unchecked((ushort)(position.Y + offset.Y))).WriteWord(0x20, unchecked((ushort)(position.Z + offset.Z))).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x74, 0x15C0D58).WriteDword(0x78, 0x15C1858).WriteWord(0x7C, unchecked((ushort)(yaw - 0x155))).WriteWord(0x7E, unchecked((ushort)(yaw + 0x155))).WriteWord(0x80, unchecked((ushort)yaw));
                task = StoreMatrix46(task, 0x54, rotation);
                frame = Write(frame, address, task);
                frame = inputs.Execute(frame, new NativeBattleSigh46VramCopy505EB0(new(512, 256, 64, 256, 704, 256)));
                frame = inputs.Execute(frame, new NativeBattleSigh46VramCopy505EB0(new(672, 256, 64, 256, 768, 256)));
                frame = Next(Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 4))), address);
                break;
            case 1:
                if (Signed(task, 0x24) >= 4)
                {
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)));
                    (frame, uint childAddress) = Append(frame, 0x264EFA8, 0x835E90, 140, address, inputs.ReadPackets(frame));
                    NativeBattleModulePoolTask child = Read(frame, childAddress).WriteWord(0x1C, task.ReadWord(0x1C)).WriteWord(0x1E, 0).WriteWord(0x20, task.ReadWord(0x20));
                    child = CopyTask(child, 0x58, TaskView(task, 0x54, 32), 32);
                    frame = Next(Write(frame, childAddress, child), address);
                }
                break;
            case 2:
                if (Signed(task, 0x24) > 22)
                {
                    frame = Next(Write(frame, address, task.WriteWord(0x88, 0)), address);
                }
                break;
            case 3:
                int index = unchecked((short)(task.ReadWord(0x88) + 1));
                if ((uint)index >= (uint)resources.Fade.Length)
                {
                    throw new InvalidDataException("Sigh fade leaves its original signed WORD table.");
                }
                short cue = resources.Fade[index];
                frame = Write(frame, address, task.WriteWord(0x88, unchecked((ushort)index)).WriteWord(0x50, unchecked((ushort)Math.Min(cue, (short)4096))));
                if (cue >= 4096)
                {
                    frame = Done(frame, address);
                }
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException("Sigh custom phase leaves its five-entry native table.");
        }
        task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            ushort firstPhase = unchecked((byte)(task.ReadByte(0x84) + 8));
            frame = Write(frame, address, task.WriteWord(0x84, firstPhase));
            frame = ScrollVram46(frame, 704, 64, 512, firstPhase, inputs);
            task = Read(frame, address);
            ushort secondPhase = unchecked((byte)(task.ReadByte(0x86) + 6));
            frame = Write(frame, address, task.WriteWord(0x86, secondPhase));
            frame = ScrollVram46(frame, 768, 32, 672, secondPhase, inputs);
            frame = CopyMaterial46(frame, resources, Read(frame, address).ReadDword(0x74), 0x15BCD10, Signed(Read(frame, address), 0x82));
            frame = CopyMaterial46(frame, resources, Read(frame, address).ReadDword(0x78), 0x15C0D14, Signed(Read(frame, address), 0x82));
            for (int angle = 0; angle < 3; angle++)
            {
                task = Read(frame, address);
                frame = Write(frame, address, task.WriteWord(0x46, task.ReadWord(0x7C + angle * 2)).WriteDword(0x4C, task.ReadDword(0x74)));
                frame = DrawCustomMesh46(frame, address, inputs, angle * 2);
                frame = Write(frame, address, Read(frame, address).WriteDword(0x4C, Read(frame, address).ReadDword(0x78)));
                frame = DrawCustomMesh46(frame, address, inputs, angle * 2 + 1);
            }
            task = Read(frame, address);
            short selector = unchecked((short)(task.ReadWord(0x82) + 1));
            frame = Write(frame, address, task.WriteWord(0x82, unchecked((ushort)Math.Min(selector, (short)16))));
        }
        return Finish(frame, address);
    }

    public static (NativeBattleSigh46Frame Frame, int Result) AdvanceParticles835E90(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = Next(frame, address);
                break;
            case 1:
                if (Signed(task, 0x24) >= 8)
                {
                    frame = Next(Write(frame, address, task.WriteWord(0x88, 0)), address);
                }
                break;
            case 2:
                int index = Signed(task, 0x88);
                if ((uint)index >= (uint)resources.Delays.Length)
                {
                    throw new InvalidDataException("Sigh particle count leaves its original WORD table.");
                }
                int count = resources.Delays[index];
                for (int instance = 0; instance < count; instance++)
                {
                    frame = SpawnParticle46(frame, address, inputs);
                }
                index = unchecked((short)(Read(frame, address).ReadWord(0x88) + 1));
                if ((uint)index >= (uint)resources.Delays.Length)
                {
                    throw new InvalidDataException("Sigh particle termination reads beyond its original WORD table.");
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x88, unchecked((ushort)index)));
                if (resources.Delays[index] == 0)
                {
                    frame = Done(frame, address);
                }
                break;
            case 3:
                break;
            default:
                throw new NotSupportedException("Sigh particle producer leaves its four-entry native table.");
        }
        return Finish(frame, address);
    }

    public static (NativeBattleSigh46Frame Frame, int Result) AdvanceSprite836090(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        _ = resources;
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = DrawRandom(frame, out ushort selector);
                uint sprite = (selector % 3) switch { 0 => 0x15B82A4u, 1 => 0x15B8438u, _ => 0x15B8BA4u };
                frame = Next(Write(frame, address, task.WriteDword(0x4C, sprite).WriteWord(0x52, 14)), address);
                break;
            case 1:
                short vx = Signed(task, 0x78);
                short vy = unchecked((short)(task.ReadWord(0x7A) + task.ReadWord(0x82)));
                short vz = Signed(task, 0x7C);
                vx = unchecked((short)(vx - vx / 16));
                vz = unchecked((short)(vz - vz / 16));
                task = task.WriteWord(0x78, unchecked((ushort)vx)).WriteWord(0x7A, unchecked((ushort)vy)).WriteWord(0x7C, unchecked((ushort)vz)).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + vx / 16))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + vy / 16))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + vz / 16)));
                short phase = unchecked((short)(task.ReadWord(0x50) + 1));
                short maximum = Signed(task, 0x52);
                if (phase > maximum)
                {
                    frame = Done(Write(frame, address, task.WriteWord(0x50, unchecked((ushort)maximum)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4))), address);
                }
                else
                {
                    frame = Write(frame, address, task.WriteWord(0x50, unchecked((ushort)phase)));
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Sigh smoke leaves its three-entry native table.");
        }
        task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            (frame, uint temporary) = Allocate(frame, 180);
            (byte[] wBytes, bool[] wKnown) = TaskView(task, 0x22, 2).ReadSnapshot(0, 2);
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 0x1C), Signed(task, 0x1E), Signed(task, 0x20)), wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null, 4096, Signed(task, 0x54)) };
            NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(task.ReadDword(0x4C))).Write(4, BitConverter.GetBytes(task.ReadWord(0x50))).Write(0x24, new byte[2]);
            frame = PublishContext(frame, temporary, context, 180);
            frame = inputs.Execute(frame, new NativeBattleSigh46DrawSprite180(temporary, task.ReadDword(0x4C)) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(46, task) with { ResourceGeneration = task.ReadDword(0x4C) } });
            frame = Release(frame, 180);
        }
        return Finish(frame, address);
    }

    private static NativeBattleSigh46Frame SpawnParticle46(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Inputs inputs)
    {
        NativeBattleModulePoolTask parent = Read(frame, address);
        NativeBattlePackedMatrix32 packed = PackedTaskMatrix46(parent, 0x58);
        frame = DrawRandom(frame, out ushort angle);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(packed.ReadMatrix(), (short)((angle % 1024 - 512) & 0xFFF));
        (frame, uint childAddress) = Append(frame, 0x264EFA8, 0x836090, 140, address, inputs.ReadPackets(frame));
        NativeBattleVector displacement = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, 0, -3072));
        NativeBattleModulePoolTask child = CopyTask(Read(frame, childAddress), 0x1C, TaskView(parent, 0x1C, 8), 8);
        child = child.WriteWord(0x1C, unchecked((ushort)(child.ReadWord(0x1C) + displacement.X))).WriteWord(0x1E, unchecked((ushort)(child.ReadWord(0x1E) + displacement.Y))).WriteWord(0x20, unchecked((ushort)(child.ReadWord(0x20) + displacement.Z)));
        frame = DrawRandom(frame, out ushort speedDraw);
        int speed = (speedDraw & 0x7FF) + 1024;
        child = child.WriteWord(0x78, unchecked((ushort)(displacement.X * speed / 4096))).WriteWord(0x7C, unchecked((ushort)(displacement.Z * speed / 4096)));
        frame = DrawRandom(frame, out ushort vertical);
        child = child.WriteWord(0x7A, unchecked((ushort)-(vertical & 0x3FF)));
        frame = DrawRandom(frame, out ushort gravity);
        child = child.WriteWord(0x82, unchecked((ushort)(-16 - (gravity & 15))));
        return Write(frame, childAddress, child);
    }

    private static NativeBattleSigh46Frame DrawCustomMesh46(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Inputs inputs, int copy)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateZ8DD960(NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Signed(task, 0x46)), Signed(task, 0x44)), Signed(task, 0x48));
        matrix = matrix with { X = Signed(task, 0x1C), Y = Signed(task, 0x1E), Z = Signed(task, 0x20) };
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix), null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(task.ReadDword(0x4C))).Write(8, BitConverter.GetBytes(task.ReadDword(0x40))).Write(12, BitConverter.GetBytes((int)Signed(task, 0x50))).Write(0x1C, BitConverter.GetBytes(0xF0u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleSigh46DrawMesh88(temporary, task.ReadDword(0x4C)) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(46, task, copy) with { ResourceGeneration = task.ReadDword(0x4C) } });
        return Release(frame, 88);
    }

    internal static NativeBattleSigh46Frame CopyMaterial46(NativeBattleSigh46Frame frame, NativeBattleSigh46Resources resources, uint mesh, uint table, int selector)
    {
        ImmutableArray<byte> colors = resources.ReadColorStream(table, selector);
        int directory = unchecked((int)frame.State.ReadDword(mesh));
        uint group = checked(mesh + (uint)(directory / 4 * 4) + 0x1C);
        int count = unchecked((int)frame.State.ReadDword(group));
        if (count < 0 || count > colors.Length / 16)
        {
            throw new InvalidDataException("Sigh material copy exceeds its actual current GT4 group or color row.");
        }
        for (int face = 0; face < count; face++)
        {
            uint target = checked(group + 4 + (uint)face * 36);
            int[] fields = [0, 24, 28, 32];
            for (int color = 0; color < 4; color++)
            {
                frame = frame with { State = frame.State.Write(target + (uint)fields[color], colors.AsSpan().Slice(face * 16 + color * 4, 4)) };
            }
        }
        return frame;
    }

    internal static NativeBattleSigh46Frame ScrollVram46(NativeBattleSigh46Frame frame, short sourceX, short width, short destinationX, ushort phase, NativeBattleSigh46Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleSigh46VramCopy505EB0(new(sourceX, 256, width, unchecked((short)(256 - phase)), destinationX, unchecked((short)(256 + phase)))));
        if (phase != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleSigh46VramCopy505EB0(new(sourceX, unchecked((short)(512 - phase)), width, unchecked((short)phase), destinationX, 256)));
        }
        return frame;
    }

    private static NativeBattleModulePoolTask StoreMatrix46(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task.WriteDword(offset + 20, unchecked((uint)matrix.X)).WriteDword(offset + 24, unchecked((uint)matrix.Y)).WriteDword(offset + 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattlePackedMatrix32 PackedTaskMatrix46(NativeBattleModulePoolTask task, int offset)
    {
        (byte[] bytes, bool[] known) = TaskView(task, offset, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    internal static (NativeBattleSigh46Frame Frame, uint Address) AllocateNodeScratch(NativeBattleSigh46Frame frame, int size)
    {
        uint cursor = frame.State.ReadDword(0x26527A0);
        if (size < 0 || size > 180 || (size & 3) != 0 || cursor < NativeBattleEffectNodeContextMemory.Address + (uint)size || cursor > NativeBattleEffectNodeContextMemory.Address + 180)
        {
            throw new InvalidDataException("Sigh node allocation leaves its actual downward scratch owner.");
        }
        uint next = cursor - (uint)size;
        return (Dword(frame, 0x26527A0, next), next);
    }

    internal static NativeBattleSigh46Frame ReleaseNodeScratch(NativeBattleSigh46Frame frame, int size)
    {
        uint cursor = frame.State.ReadDword(0x26527A0);
        if (size < 0 || size > 180 || (size & 3) != 0 || cursor < NativeBattleEffectNodeContextMemory.Address || cursor > NativeBattleEffectNodeContextMemory.Address + 180 - (uint)size)
        {
            throw new InvalidDataException("Sigh node release exceeds its actual downward scratch top.");
        }
        return Dword(frame, 0x26527A0, cursor + (uint)size);
    }
}
