namespace Memoria.FF8.Runtime;

public static partial class NativeBattleJunk44Controller
{
    private static (NativeBattleJunk44Frame Frame, int Result) AdvanceRoot(NativeBattleJunk44Frame frame, uint address, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(0x10) != 0;
        frame = Dword(frame, 0x2545174, checked(frame.State.ReadDword(0x2545180) + (alternate ? 0x12D4u : 0x112D4u)));
        frame = Write(frame, address, root.WriteDword(0x10, alternate ? 0u : 1u));
        if (Signed(root, 0x0C) == 1 && root.ReadByte(0x0F) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(0x0F, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, 0x2545140, 0x6E7E00);
            if (main == 0)
            {
                throw new InvalidDataException("Junk's freshly reset main bank must admit its sole task.");
            }
            frame = ClearDwords701200(frame, main + 12, 16);
            int count = Header(frame, inputs.ReadPackets(frame)).ReadByte(16);
            if (count == 0 || count > 3)
            {
                throw new NotSupportedException("Junk's recipient mean requires one to three physical records in its main task.");
            }
            for (int index = 0; index < count; index++)
            {
                NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                NativeBattleActionHeaderSlot header = Header(frame, packets);
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Junk requires its current physical contact pointer.");
                uint modelAddress = ModelAddress(packets.ReadContact(checked(first + index)).Primary.ActorIndex);
                NativeBattleModelTransform transform = inputs.ReadModel(frame, modelAddress).Transform ?? throw new NotSupportedException("Junk requires its current recipient packed position.");
                frame = Write(frame, main, Read(frame, main).WriteDword(0x14 + index * 16, modelAddress));
                frame = WriteBytes(frame, main + 0x18u + (uint)(index * 16), PointView(transform.Position28, transform.Vertical34), 8);
                NativeBattleJunk44PointStep anchor = inputs.ReadAnchor571400(frame, modelAddress);
                frame = anchor.Frame;
                NativeBattleModulePoolTask task = Read(frame, main);
                for (int axis = 0; axis < 3; axis++)
                {
                    short value = BitConverter.ToInt16(anchor.Point8.Read(axis * 2, 2));
                    task = task.WriteWord(0x44 + axis * 2, unchecked((ushort)(task.ReadWord(0x44 + axis * 2) + value)));
                }
                frame = Write(frame, main, task);
            }
            NativeBattleModulePoolTask mean = Read(frame, main);
            for (int axis = 0; axis < 3; axis++)
            {
                mean = mean.WriteWord(0x44 + axis * 2, unchecked((ushort)(Signed(mean, 0x44 + axis * 2) / count)));
            }
            frame = DrawRandom(Write(frame, main, mean), out ushort seed);
            frame = Write(frame, main, Read(frame, main).WriteWord(0x10, seed));
            frame = frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x128C) };
        }
        root = Read(frame, address);
        if (root.ReadByte(0x0F) != 0 && Signed(root, 0x0C) < 114)
        {
            foreach (int pool in new[] { 4, 3, 2, 1 })
            {
                (frame, _) = AdvancePool(frame, pool, resources, inputs);
            }
        }
        root = Read(frame, address);
        if (Signed(root, 0x0C) == 115)
        {
            uint destination = checked(frame.State.ReadDword(0x2545180) + 0x12D4);
            frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(NativeBattleEffectIo.Queue5341D0(frame.Memory, 360, destination, 1), 361, destination, 0) };
        }
        if (Read(frame, address).ReadByte(0x0F) != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleJunk44AdvanceIo534210());
        }
        root = Read(frame, address);
        if (Signed(root, 0x0C) > 115)
        {
            NativeBattleJunk44IoStep poll = inputs.ReloadComplete(frame);
            return (poll.Frame, poll.Complete ? 2 : 0);
        }
        return (Write(frame, address, root.WriteWord(0x0C, unchecked((ushort)(root.ReadWord(0x0C) + 1)))), 0);
    }

    private static (NativeBattleJunk44Frame Frame, int Result) AdvanceMain(NativeBattleJunk44Frame frame, uint address, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        int age = Signed(Read(frame, address), 0x0C);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleJunk44Sound501330(resources.Sound, 0, 128));
        }
        switch (age)
        {
            case 1:
                frame = SpawnBoneSprites6E81A0(frame, 3, 0x17, 200, 2000, 2000, resources, inputs);
                frame = SpawnBoneSprites6E81A0(frame, 3, 0x15, 200, 2000, 2000, resources, inputs);
                frame = inputs.Execute(frame, new NativeBattleJunk44CameraWord1D97712(150));
                break;
            case 8:
            case 22:
                frame = SpawnBoneSprites6E81A0(frame, 3, 0x15, 200, 2000, 2000, resources, inputs);
                frame = inputs.Execute(frame, new NativeBattleJunk44CameraWord1D97712(150));
                break;
            case 15:
            case 29:
                frame = SpawnBoneSprites6E81A0(frame, 3, 0x17, 200, 2000, 2000, resources, inputs);
                frame = inputs.Execute(frame, new NativeBattleJunk44CameraWord1D97712(150));
                break;
        }
        if ((uint)(age - 3) < 35)
        {
            int phase = (age - 3) % 7;
            if (phase == 0)
            {
                for (int index = 0; index < Header(frame, inputs.ReadPackets(frame)).ReadByte(16); index++)
                {
                    RequireRecipientRecord(index);
                    frame = DrawRandom(frame, out ushort value);
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x20 + index * 16, (ushort)((value & 0x1FF) + 0x100)));
                }
            }
            int cosine = NativeBattleCuragaMatrices.Cosine8DDRotation(unchecked((short)(phase * 2048 / 7 + 0x124)));
            for (int index = 0; index < Header(frame, inputs.ReadPackets(frame)).ReadByte(16); index++)
            {
                RequireRecipientRecord(index);
                NativeBattleModulePoolTask task = Read(frame, address);
                int offset = 0x14 + index * 16;
                uint modelAddress = task.ReadDword(offset);
                int product = unchecked(Signed(task, offset + 12) * cosine);
                short y = unchecked((short)(Signed(task, offset + 6) - product / 4096));
                frame = inputs.Execute(frame, new NativeBattleJunk44ModelPositionY(modelAddress, y));
                if (phase == 6)
                {
                    NativeBattleModelTransform transform = inputs.ReadModel(frame, modelAddress).Transform ?? throw new NotSupportedException("Junk's current recipient sprite birth requires its actual packed position.");
                    frame = SpawnPointSprites6E8360(frame, 3, PointView(transform.Position28, transform.Vertical34), 300, 2000, 2000, resources, inputs);
                    frame = inputs.Execute(frame, new NativeBattleJunk44SelectSequence505C00(modelAddress, 4));
                }
            }
        }
        if ((uint)(age - 15) < 32 && (age - 15) % 3 == 0)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            uint selector = unchecked((uint)(Signed(task, 0x10) + ((age - 15) >> 2)));
            int table = (int)(selector % 6);
            frame = DrawRandom(frame, out ushort zRandom);
            short z = unchecked((short)(Signed(Read(frame, address), 0x48) + (zRandom & 0x7FF) - 0x400));
            frame = DrawRandom(frame, out ushort xRandom);
            short x = unchecked((short)(Signed(Read(frame, address), 0x44) + (xRandom & 0xFFF) - 0x800));
            (frame, uint debris) = SpawnDebris6E8470(frame, x, z, 0, table, resources, inputs);
            if (debris != 0)
            {
                frame = DrawRandom(frame, out ushort velocity);
                NativeBattleModulePoolTask child = Read(frame, debris).WriteWord(0x1C, unchecked((ushort)((velocity * 800 >> 15) - 400)));
                frame = DrawRandom(frame, out ushort angle);
                child = child.WriteWord(0x1E, angle).WriteWord(0x20, 0);
                frame = DrawRandom(frame, out ushort xSpin);
                child = child.WriteWord(0x24, unchecked((ushort)((xSpin & 255) - 128)));
                frame = DrawRandom(frame, out ushort zSpin);
                frame = Write(frame, debris, child.WriteWord(0x26, unchecked((ushort)((zSpin & 255) - 128))));
            }
        }
        if (age == 62)
        {
            for (int index = 0; index < Header(frame, inputs.ReadPackets(frame)).ReadByte(16); index++)
            {
                RequireRecipientRecord(index);
                uint modelAddress = Read(frame, address).ReadDword(0x14 + index * 16);
                NativeBattleJunk44PointStep anchor = inputs.ReadAnchor571400(frame, modelAddress);
                frame = anchor.Frame;
                short x = BitConverter.ToInt16(anchor.Point8.Read(0, 2));
                short z = BitConverter.ToInt16(anchor.Point8.Read(4, 2));
                (frame, uint debris) = SpawnDebris6E8470(frame, x, z, modelAddress, 6, resources, inputs);
                if (debris != 0)
                {
                    NativeBattleModulePoolTask child = Read(frame, debris).WriteWord(0x12, 24);
                    frame = DrawRandom(frame, out ushort velocity);
                    child = child.WriteWord(0x1C, unchecked((ushort)((velocity & 0x1FF) - 0x500)));
                    frame = DrawRandom(frame, out ushort angle);
                    frame = Write(frame, debris, child.WriteWord(0x1E, angle).WriteWord(0x20, 0).WriteWord(0x24, 0).WriteWord(0x26, 0));
                }
            }
        }
        NativeBattleModulePoolTask main = Read(frame, address);
        short nextAge = unchecked((short)(main.ReadWord(0x0C) + 1));
        frame = Write(frame, address, main.WriteWord(0x0C, unchecked((ushort)nextAge)));
        if (nextAge < 100)
        {
            return (frame, 0);
        }
        for (int index = 0; index < Header(frame, inputs.ReadPackets(frame)).ReadByte(16); index++)
        {
            RequireRecipientRecord(index);
            NativeBattleModulePoolTask task = Read(frame, address);
            frame = inputs.Execute(frame, new NativeBattleJunk44RestoreModelPosition(task.ReadDword(0x14 + index * 16), TaskView(task, 0x18 + index * 16, 8)));
        }
        return (frame, 2);
    }

    private static void RequireRecipientRecord(int index)
    {
        if ((uint)index >= 3)
        {
            throw new NotSupportedException("Junk recipient record leaves its three physical main-task rows.");
        }
    }

    public static NativeBattleActionHeaderSlot Header(NativeBattleJunk44Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(0x2545104) != 0x1D99A78)
        {
            throw new NotSupportedException("Junk requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Junk parameter header pointer leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
