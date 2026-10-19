namespace Memoria.FF8.Runtime;

public static class NativeBattleSiren95TargetParticles
{
    public static NativeBattleSiren95TaskStep Advance743250(NativeBattleSiren95Frame frame, uint address, NativeBattleActionPacketsState packets, Func<byte, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x743250)
        {
            throw new InvalidDataException("Native Siren target emitter requires its selected callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteByte(0x29, 1);
                frame = Put(frame, address, task);
                break;
            case 1:
                NativeBattleGraphicsModel target = readModel(task.ReadByte(0x2D));
                NativeBattleEffectLocalBounds bounds = target.GeometryBounds52 ?? throw new NotSupportedException("Native Siren target particles require their current target bounds.");
                NativeBattleModelTransform transform = target.Transform ?? throw new NotSupportedException("Native Siren target particles require their current target position.");
                var appended = NativeBattleSiren95Actors.AppendTask(frame, 0x257FA90, 0x7433C0, 0xE0, address, packets);
                frame = appended.Frame;
                NativeBattleModulePoolTask child = Read(frame, appended.Address);
                for (int corner = 0; corner < 4; corner++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        short minimum = Axis(bounds.Minimum, axis);
                        short maximum = Axis(bounds.Maximum, axis);
                        frame = frame with { Random = frame.Random.Draw(out ushort random) };
                        int delta = unchecked((maximum - minimum) * (random & 4095)) / 4096;
                        child = child.WriteWord(0x6C + corner * 8 + axis * 2, unchecked((ushort)(minimum + delta + Axis(transform.Position28, axis))));
                    }
                }
                frame = Put(frame, appended.Address, child);
                NativeBattleSiren95State shared = frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren target particles require their current director owner.");
                uint director = shared.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren target particles require their current director pointer.");
                task = Read(frame, address);
                if (unchecked((short)shared.ReadWord(director + 0x40)) >= 7)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = Put(frame, address, task);
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Siren target emitter signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = Put(frame, address, task);
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    private static short Axis(NativeBattleVector point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
