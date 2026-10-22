namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8DrawDust(uint Address) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8DrawSmoke(uint Address) : NativeBattleDoom8Operation;

public static class NativeBattleDoom8Particles
{
    public static NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame, uint address, NativeBattleActionPacketsState packets, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        frame.State.Validate();
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || task.Callback is not (0x8CD4B0 or 0x8CD670 or 0x8CDED0 or 0x8CDFB0))
        {
            throw new InvalidDataException("Doom particles require their actual callback and module.");
        }
        bool dust = task.Callback is 0x8CD4B0 or 0x8CD670;
        bool parent = task.Callback is 0x8CD4B0 or 0x8CDED0;
        NativeBattleCureFamilyPool expected = NativeBattleDoom8State.Pools[dust ? 5 : 6];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity || address < pool.BankAddress || address >= pool.BankAddress + pool.Stride * pool.Slots.Length)
        {
            throw new InvalidDataException("Doom particles require their physical task bank.");
        }
        if (parent)
        {
            frame = AdvanceParent(frame, address, dust, packets);
        }
        else if (dust)
        {
            frame = AdvanceDust(frame, address, execute);
        }
        else
        {
            frame = AdvanceSmoke(frame, address, execute);
        }
        return NativeBattleDoom8Controller.Finish(frame, address);
    }

    private static NativeBattleDoom8Frame AdvanceParent(NativeBattleDoom8Frame frame, uint address, bool dust, NativeBattleActionPacketsState packets)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                int offset = checked((int)(NativeBattleDoom8State.SharedAddress - NativeBattleDoom8State.BaseAddress));
                (byte[] bytes, bool[] known) = frame.State.Globals.ReadSnapshot(offset, 8);
                task = CopyBytes(task, 0x1C, bytes, known).WriteWord(0x1E, 0);
                frame = NativeBattleDoom8Controller.Write(frame, address, Next(task));
                break;
            case 1:
                short y = unchecked((short)(frame.State.ReadWord(0x2758564) + 256));
                task = task.WriteWord(0x1E, unchecked((ushort)(dust && y >= 0 ? 0 : y)));
                frame = NativeBattleDoom8Controller.Write(frame, address, task);
                if ((task.ReadByte(0x24) & 1) == 0)
                {
                    NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Doom particle creation requires its actual action parameters.");
                    NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(dust ? 5 : 6, dust ? 0x8CD670u : 0x8CDFB0u, dust ? 176 : 88, address, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target));
                    if (child.Address == 0)
                    {
                        throw new NotSupportedException("Doom particle creation exhausted its physical task pool before required native writes.");
                    }
                    frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Pools } };
                    NativeBattleModulePoolTask created = NativeBattleDoom8Controller.Read(frame, child.Address);
                    if (dust)
                    {
                        for (int index = 0; index < 4; index++)
                        {
                            int position = 0x6C + index * 8;
                            int velocity = 0x8C + index * 8;
                            int radius = (Draw(ref frame) & 255) + 256;
                            int angle = Draw(ref frame) & 4095;
                            created = CopyPacked(task, 0x1C, created, position);
                            created = AddWord(created, position, unchecked(NativeBattleCameraMath.Cosine(angle) * radius) / 4096);
                            created = AddWord(created, position + 2, (Draw(ref frame) & 255) - 128);
                            created = AddWord(created, position + 4, unchecked(NativeBattleCameraMath.Sine(angle) * radius) / 4096);
                            for (int axis = 0; axis < 3; axis++)
                            {
                                created = created.WriteWord(velocity + axis * 2, unchecked((ushort)((Draw(ref frame) & 1023) - 512)));
                            }
                        }
                    }
                    else
                    {
                        created = CopyPacked(task, 0x1C, created, 0x1C);
                    }
                    frame = NativeBattleDoom8Controller.Write(frame, child.Address, created);
                }
                task = NativeBattleDoom8Controller.Read(frame, address);
                if (Signed(task, 0x24) >= (dust ? 40 : 35))
                {
                    frame = NativeBattleDoom8Controller.Write(frame, address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Doom particle emitter signed phase exceeds its three handlers.");
        }
        return frame;
    }

    private static NativeBattleDoom8Frame AdvanceDust(NativeBattleDoom8Frame frame, uint address, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> execute)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = Next(task.WriteDword(0x4C, 0x1634F28).WriteWord(0x52, 16));
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (Signed(task, 0x50) > Signed(task, 0x52))
                {
                    task = Next(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Doom dust signed phase exceeds its three handlers.");
        }
        frame = NativeBattleDoom8Controller.Write(frame, address, task);
        for (int index = 0; index < 4; index++)
        {
            task = NativeBattleDoom8Controller.Read(frame, address);
            for (int axis = 0; axis < 3; axis++)
            {
                int velocityOffset = 0x8C + index * 8 + axis * 2;
                short velocity = Signed(task, velocityOffset);
                velocity = unchecked((short)(velocity - velocity / 16));
                task = task.WriteWord(velocityOffset, unchecked((ushort)velocity));
                task = AddWord(task, 0x6C + index * 8 + axis * 2, velocity / 16);
            }
            task = CopyPacked(task, 0x6C + index * 8, task, 0x1C);
            frame = NativeBattleDoom8Controller.Write(frame, address, task);
            frame = execute(frame, new NativeBattleDoom8DrawDust(address));
        }
        return frame;
    }

    private static NativeBattleDoom8Frame AdvanceSmoke(NativeBattleDoom8Frame frame, uint address, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> execute)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                int scale = Draw(ref frame) % 1023 + 3072;
                task = task.WriteDword(0x4C, 0x16350F0).WriteWord(0x50, 4096).WriteDword(0x30, (uint)scale).WriteDword(0x34, (uint)scale).WriteDword(0x38, (uint)scale);
                task = task.WriteWord(0x46, (ushort)(Draw(ref frame) & 4095));
                task = task.WriteWord(0x44, unchecked((ushort)(Draw(ref frame) % 768 - 384)));
                task = Next(task.WriteWord(0x48, unchecked((ushort)(Draw(ref frame) % 768 - 384))));
                break;
            case 1:
                task = AddWord(task, 0x50, -1024);
                if (Signed(task, 0x50) <= 0)
                {
                    task = Next(task.WriteWord(0x50, 0).WriteWord(0x54, 1));
                }
                break;
            case 2:
                task = AddWord(task, 0x54, -1);
                if (Signed(task, 0x54) <= 0)
                {
                    task = Next(task);
                }
                break;
            case 3:
                task = AddWord(task, 0x50, 1024);
                if (Signed(task, 0x50) >= 4096)
                {
                    task = Next(task.WriteWord(0x50, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Doom smoke signed phase exceeds its five handlers.");
        }
        task = task.WriteWord(0x46, unchecked((ushort)((task.ReadWord(0x46) + 128) & 4095)));
        return execute(NativeBattleDoom8Controller.Write(frame, address, task), new NativeBattleDoom8DrawSmoke(address));
    }

    private static int Draw(ref NativeBattleDoom8Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static NativeBattleModulePoolTask AddWord(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Signed(task, offset) + value)));
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

    private static NativeBattleModulePoolTask CopyPacked(NativeBattleModulePoolTask source, int sourceOffset, NativeBattleModulePoolTask destination, int destinationOffset)
    {
        source.RequirePayloadRange(sourceOffset, 8);
        source.ValidateUnknownPayloadRanges();
        byte[] bytes = source.Payload.AsSpan().Slice(sourceOffset - 12, 8).ToArray();
        bool[] known = Enumerable.Repeat(true, 8).ToArray();
        if (source.UnknownPayloadRanges is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                for (int index = Math.Max(sourceOffset, range.Offset); index < Math.Min(sourceOffset + 8, range.Offset + range.Length); index++)
                {
                    known[index - sourceOffset] = false;
                }
            }
        }
        return CopyBytes(destination, destinationOffset, bytes, known);
    }

    private static NativeBattleModulePoolTask CopyBytes(NativeBattleModulePoolTask task, int offset, byte[] bytes, bool[] known)
    {
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }
}
