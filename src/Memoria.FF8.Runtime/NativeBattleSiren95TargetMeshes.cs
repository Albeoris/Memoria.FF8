namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95TargetMeshServices(NativeBattleActionPacketsState Packets, Func<byte, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> Draw);

public static class NativeBattleSiren95TargetMeshes
{
    public static NativeBattleSiren95TaskStep Advance(NativeBattleSiren95Frame frame, uint address, NativeBattleSiren95TargetMeshServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (task.Callback == 0x7456A0)
        {
            switch (phase)
            {
                case 0:
                    NativeBattleModelTransform position = services.ReadModel(task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Native Siren target mesh emitter requires its current target position.");
                    task = task.WriteWord(0x1C, unchecked((ushort)position.Position28.X)).WriteWord(0x1E, unchecked((ushort)position.Position28.Y)).WriteWord(0x20, unchecked((ushort)position.Position28.Z)).WriteWord(0x22, unchecked((ushort)position.Vertical34));
                    Next();
                    break;
                case 1:
                    if (unchecked((short)task.ReadWord(0x24)) > 15)
                    {
                        Next();
                    }
                    break;
                case 2:
                    if (((task.ReadByte(0x2D) + task.ReadByte(0x24)) & 3) == 0)
                    {
                        NativeBattleGraphicsModel target = services.ReadModel(task.ReadByte(0x2D));
                        NativeBattleEffectLocalBounds bounds = target.GeometryBounds52 ?? throw new NotSupportedException("Native Siren target mesh emitter requires its current target bounds.");
                        NativeBattleModelTransform transform = target.Transform ?? throw new NotSupportedException("Native Siren target mesh emitter requires its current target position.");
                        frame = Put(frame, address, task);
                        var appended = NativeBattleSiren95Actors.AppendTask(frame, 0x2585EF0, 0x745840, 0x64, address, services.Packets);
                        frame = appended.Frame;
                        NativeBattleModulePoolTask child = Read(frame, appended.Address);
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int minimum = Axis(bounds.Minimum, axis);
                            int range = Axis(bounds.Maximum, axis) - minimum;
                            child = child.WriteWord(0x1C + axis * 2, unchecked((ushort)(minimum + unchecked(range * (Random() & 4095)) / 4096 + Axis(transform.Position28, axis))));
                        }
                        frame = Put(frame, appended.Address, child);
                        task = Read(frame, address);
                    }
                    NativeBattleSiren95State state = State();
                    uint director = state.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren target mesh emitter requires its current director pointer.");
                    if (unchecked((short)state.ReadWord(director + 0x40)) >= 7)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                        Next();
                    }
                    break;
                case 3:
                    break;
                default:
                    Unsupported();
                    break;
            }
        }
        else if (task.Callback == 0x745840)
        {
            switch (phase)
            {
                case 0:
                    task = task.WriteDword(0x4C, State().ReadDword(unchecked(State().ReadDword(0x258EAA0) + 0x148))).WriteDword(0x30, 256).WriteDword(0x34, 256).WriteDword(0x38, 256);
                    task = task.WriteWord(0x48, (ushort)(Random() & 4095)).WriteWord(0x5E, (ushort)((Random() & 15) + 16)).WriteWord(0x50, 4096);
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - 1024)));
                    if (unchecked((short)task.ReadWord(0x50)) <= 0)
                    {
                        task = task.WriteWord(0x50, 0).WriteWord(0x5C, (ushort)((Random() & 3) + 4));
                        Next();
                    }
                    break;
                case 2:
                    task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) - 1)));
                    if (unchecked((short)task.ReadWord(0x5C)) <= 0)
                    {
                        Next();
                    }
                    break;
                case 3:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 512)));
                    if (unchecked((short)task.ReadWord(0x50)) >= 4096)
                    {
                        task = task.WriteWord(0x50, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                        Next();
                    }
                    break;
                case 4:
                    break;
                default:
                    Unsupported();
                    break;
            }
            task = task.WriteWord(0x48, (ushort)((task.ReadWord(0x48) + task.ReadWord(0x5E)) & 4095));
            uint scale = unchecked(task.ReadDword(0x38) + 256);
            task = task.WriteDword(0x30, scale).WriteDword(0x34, scale).WriteDword(0x38, scale);
            frame = services.Draw(Put(frame, address, task), address);
            task = Read(frame, address);
        }
        else
        {
            throw new InvalidDataException("Native Siren target mesh requires its selected callback.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Put(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        NativeBattleSiren95State State() => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren target meshes require their retained module bytes.");
        ushort Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        void Unsupported() => throw new NotSupportedException($"Native Siren target mesh callback 0x{task.Callback:X8}, signed phase {phase} is unsupported.");
    }

    private static short Axis(NativeBattleVector point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
