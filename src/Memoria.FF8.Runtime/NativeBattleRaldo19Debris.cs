namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRaldo19DebrisSprite(uint TaskAddress, uint ResourceAddress, ushort Frame, short DepthBias);

public static class NativeBattleRaldo19Debris
{
    public static NativeBattleRaldo19LeafStep Advance(NativeBattleRaldo19Frame frame, uint address, NativeBattleActionPacketsState packets, Func<NativeBattleRaldo19Frame, NativeBattleRaldo19DebrisSprite, NativeBattleRaldo19Frame> draw)
    {
        frame.ToGeneric();
        packets.Validate();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback is not (0x8A9F10 or 0x8AA1D0 or 0x8AAA30))
        {
            throw new NotSupportedException("Raldo debris requires its original spawner or particle callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (task.Callback == 0x8A9F10)
        {
            if (phase == 0)
            {
                frame = Spawn(frame, address, packets);
                hierarchy = new(frame.Effect.Pools);
                task = hierarchy.Read(address);
                if (Signed(task, 0x24) >= Signed(task, 0xB6) - 1)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(phase + 1)));
                    frame = Write(frame, address, task);
                }
            }
            else if (phase != 1)
            {
                throw new InvalidDataException("Raldo debris spawner phase leaves its original handlers.");
            }
        }
        else
        {
            bool high = task.Callback == 0x8AA1D0;
            if (phase == 0)
            {
                task = task.WriteDword(0x4C, high ? 0x1629FB0u : 0x162A0C0u).WriteWord(0x56, 0xFFF0).WriteWord(0x52, high ? (ushort)9 : (ushort)11).WriteByte(0x29, 1);
                frame = Write(frame, address, task);
            }
            else if (phase == 1)
            {
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (Signed(task, 0x50) > Signed(task, 0x52))
                {
                    task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteByte(0x29, 2);
                }
                frame = Write(frame, address, task);
            }
            else if (phase != 2)
            {
                throw new InvalidDataException("Raldo debris particle phase leaves its original handlers.");
            }
            for (int row = 0; row < 4; row++)
            {
                task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
                int position = 0x6C + row * 8;
                int velocity = 0x8C + row * 8;
                task = task.WriteDword(0x1C, task.ReadDword(position)).WriteDword(0x20, task.ReadDword(position + 4));
                frame = Write(frame, address, task);
                if ((task.ReadByte(0x26) & 4) == 0)
                {
                    frame = draw(frame, new(address, task.ReadDword(0x4C), task.ReadWord(0x50), Signed(task, 0x56)));
                }
                task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
                task = task.WriteWord(velocity + 2, unchecked((ushort)(task.ReadWord(velocity + 2) + task.ReadWord(0xAC + row * 2))));
                for (int axis = 0; axis < 3; axis++)
                {
                    int offset = velocity + axis * 2;
                    short damped = Damp(Signed(task, offset));
                    task = task.WriteWord(offset, unchecked((ushort)damped));
                    int coordinate = position + axis * 2;
                    task = task.WriteWord(coordinate, unchecked((ushort)(task.ReadWord(coordinate) + damped / 16)));
                }
                frame = Write(frame, address, task);
            }
        }
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        return new(frame with { Effect = frame.Effect with { Pools = complete ? hierarchy.Complete8DC530(address).Pools : hierarchy.Pools } }, complete ? 2 : 0);
    }

    public static short Damp(short velocity) => unchecked((short)(velocity - velocity / 4));

    private static NativeBattleRaldo19Frame Spawn(NativeBattleRaldo19Frame frame, uint parent, NativeBattleActionPacketsState packets)
    {
        for (int group = 0; group < 2; group++)
        {
            bool high = group == 1;
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            NativeBattleEffectHierarchyAppend allocated = hierarchy.Append8DC540(4, high ? 0x8AA1D0u : 0x8AAA30u, 184, parent, Select);
            if (allocated.Address == 0)
            {
                throw new NotSupportedException("Raldo debris reached an unsupported native null-allocation dereference.");
            }
            hierarchy = allocated.Hierarchy;
            NativeBattleModulePoolTask source = hierarchy.Read(parent);
            NativeBattleModulePoolTask task = hierarchy.Read(allocated.Address);
            NativeCrtRandomState random = frame.Random;
            for (int row = 0; row < 4; row++)
            {
                random = random.Draw(out ushort angleDraw);
                short angle = unchecked((short)(angleDraw % 0xFFF));
                random = random.Draw(out ushort radiusDraw);
                short radius = unchecked((short)((radiusDraw & 0xFF) + (high ? 0x80 : 0x40)));
                random = random.Draw(out ushort velocityDraw);
                short speed = unchecked((short)((velocityDraw & 0xFFF) + (high ? 0x800 : 0x100)));
                int position = 0x6C + row * 8;
                int velocity = 0x8C + row * 8;
                task = task.WriteDword(position, source.ReadDword(0x1C)).WriteDword(position + 4, source.ReadDword(0x20));
                task = task.WriteWord(position, unchecked((ushort)(task.ReadWord(position) + unchecked(NativeBattleCameraMath.Sine(angle) * radius) / 4096)));
                task = task.WriteWord(position + 4, unchecked((ushort)(task.ReadWord(position + 4) + unchecked(NativeBattleCameraMath.Cosine(angle) * radius) / 4096)));
                task = task.WriteWord(velocity, unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(angle) * speed) / 4096))).WriteWord(velocity + 4, unchecked((ushort)(unchecked(NativeBattleCameraMath.Cosine(angle) * speed) / 4096)));
                random = random.Draw(out ushort verticalDraw);
                if (high)
                {
                    task = task.WriteWord(velocity + 2, unchecked((ushort)(-0x800 - (verticalDraw & 0xFFF))));
                    random = random.Draw(out ushort accelerationDraw);
                    task = task.WriteWord(0xAC + row * 2, unchecked((ushort)(0xF0 + (accelerationDraw & 0x7F))));
                }
                else
                {
                    task = task.WriteWord(0xAC + row * 2, unchecked((ushort)(-0x30 - (verticalDraw & 0x1F))));
                }
            }
            frame = frame with { Random = random, Effect = frame.Effect with { Pools = hierarchy.Write(allocated.Address, task).Pools } };
        }
        return frame;

        NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, packets, pointer, group, target);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleRaldo19Frame Write(NativeBattleRaldo19Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
