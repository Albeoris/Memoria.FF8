namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165Contact(int RelativeIndex) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Rotation(byte Actor, NativeBattleVector Rotation, ushort? Padding) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Echo(byte Actor, uint TemporaryAddress) : NativeBattleLionHeart165Operation;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep Advance5EC140(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        for (int index = 0; index < 15; index++)
        {
            int at = ReadDword(frame.Staging, 0xD88718u + (uint)index * 4);
            if (counter < at)
            {
                break;
            }
            if (counter != at)
            {
                continue;
            }
            uint local = 0xD88758u + (uint)index * 8;
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(inputs.ReadModel(frame.Parameters.Actor78).GeometryWorldMatrix ?? throw new NotSupportedException("Native Lion Heart contacts require their actual actor geometry matrix."), new(ReadWord(frame.Staging, local), ReadWord(frame.Staging, local + 2), ReadWord(frame.Staging, local + 4)));
            NativeBattleVector point = new(unchecked((short)(ReadWord(frame.Staging, 0x23C58B8) + offset.X)), unchecked((short)(ReadWord(frame.Staging, 0x23C58BA) + offset.Y)), unchecked((short)(ReadWord(frame.Staging, 0x23C58BC) + offset.Z)));
            if (index > 3)
            {
                point = new(unchecked((short)((point.X + ReadWord(frame.Staging, 0x23C6730)) >> 1)), unchecked((short)((point.Y + ReadWord(frame.Staging, 0x23C6732)) >> 1)), unchecked((short)((point.Z + ReadWord(frame.Staging, 0x23C6734)) >> 1)));
            }
            uint row = 0x23C32F8u + (uint)index * 28;
            NativeBattleFileStaging staging = Point(Point(frame.Staging, 0x23C6730, point), row, point);
            staging = Forget(Forget(staging, 0x23C6736, 2), row + 6, 2);
            staging = CopyPhysical(CopyPhysical(staging, 0x23C58C8, row + 8, 16), 0x23C58C8, 0x23C6850, 16);
            frame = frame with { Staging = staging };
            int sparks = Append(0x5EC550);
            PointTask(sparks, point);
            Marker(sparks);
            int burst = Append(0x5EC7D0);
            PointTask(burst, point);
            Marker(burst);
            int narrow = Append(0x5ECB40);
            SetTask(narrow, 14, index);
            SetTask(narrow, 24, Rand() % 4096);
            int speed = (Rand() % 2560 + 3584) / 3;
            SetTask(narrow, 30, speed);
            SetTask(narrow, 28, speed);
            int wide = Append(0x5ECCD0);
            SetTask(wide, 14, index);
            SetTask(wide, 24, Rand() % 4096);
            int secondSpeed = (Rand() % 3072 + 3072) / 3;
            SetTask(wide, 30, secondSpeed);
            SetTask(wide, 28, secondSpeed + 1408);
            int radial = Append(0x5ECE60);
            Marker(radial);
            SetTask(radial, 26, index);
            if (index < 14)
            {
                short duration = index == 0 ? (short)8 : unchecked((short)(ReadDword(frame.Staging, 0xD8871Cu + (uint)index * 4) - at));
                short destination = unchecked((short)(ReadWord(frame.Staging, 0xD887D0u + (uint)index * 4) - 4000));
                foreach (byte actor in TargetRing(frame, inputs))
                {
                    frame = inputs.Execute(frame, new NativeBattleLionHeart165ModelFlags(actor, 0x1000, 0));
                    int motion = Append(0x5ED1A0);
                    SetTask(motion, 14, actor);
                    SetTask(motion, 26, duration);
                    SetTask(motion, 28, destination);
                }
            }
            frame = ApplyNextContact(frame, inputs);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))) with { Next = pool.Slots[slot].Next };
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, task.ReadWord(12) >= 110 ? 2 : 0);

        int Append(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            SetTask(created, 12, 0);
            return created;
        }
        void SetTask(int created, int offset, int value) => pool = pool with { Slots = pool.Slots.SetItem(created, pool.Slots[created].WriteWord(offset, unchecked((ushort)value))) };
        void PointTask(int created, NativeBattleVector point)
        {
            SetTask(created, 16, point.X);
            SetTask(created, 18, point.Y);
            SetTask(created, 20, point.Z);
            pool = pool with { Slots = pool.Slots.SetItem(created, pool.Slots[created].MarkPayloadUnknown(22, 2)) };
        }
        void Marker(int created)
        {
            int marker = ReadDword(frame.Staging, 0x23C6940);
            SetTask(created, 14, marker);
            frame = frame with { Staging = WriteDword(frame.Staging, 0x23C6940, unchecked((uint)(marker + 1))) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleLionHeart165ChildStep Advance5ED1A0(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        byte actor = checked((byte)Word(task, 14));
        NativeBattleModelTransform transform = inputs.ReadModel(actor).Transform ?? throw new NotSupportedException("Native Lion Heart target motion requires its actual raw model position.");
        if (counter == 0)
        {
            NativeBattleModuleAnchor point = inputs.ReadPosition(actor);
            task = task.WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, unchecked((ushort)point.Y)).WriteWord(20, unchecked((ushort)point.Z)).MarkPayloadUnknown(22, 2).WriteWord(32, unchecked((ushort)transform.Position28.Y)).WriteWord(34, unchecked((ushort)(Word(task, 28) - point.Y)));
        }
        int delta = Word(task, 34);
        short duration = Word(task, 26);
        if (delta <= 0 && duration == 0)
        {
            throw new NotSupportedException("Native Lion Heart target sine motion requires its nonzero signed duration.");
        }
        int movement = delta <= 0 ? unchecked(delta * NativeBattleCameraMath.Sine((counter << 10) / duration)) >> 12 : -70 * counter;
        frame = inputs.Execute(frame, new NativeBattleLionHeart165Position(actor, transform.Position28 with { Y = unchecked((short)(movement + Word(task, 32))) }, transform.Vertical34));
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, Word(task, 12) >= duration ? 2 : 0);
    }

    public static NativeBattleLionHeart165ChildStep Advance5ED4E0(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        if (counter % 12 == 1)
        {
            (pool, int created) = pool.Append(0x5ED590);
            NativeBattleModulePoolTask child = pool.Slots[created].WriteWord(12, 0);
            frame = frame with { Random = frame.Random.Draw(out ushort first) };
            frame = frame with { Random = frame.Random.Draw(out ushort second) };
            frame = frame with { Random = frame.Random.Draw(out ushort third) };
            child = child.WriteWord(24, (ushort)((first % 8) << 9)).WriteWord(26, (ushort)((second % 4) << 9)).WriteWord(28, 8192).WriteWord(32, (ushort)(third % 16 + 6));
            pool = pool with { Slots = pool.Slots.SetItem(created, child) };
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1))) with { Next = pool.Slots[slot].Next };
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, task.ReadWord(12) >= 99 ? 2 : 0);
    }

    private static NativeBattleLionHeart165Frame ApplyNextContact(NativeBattleLionHeart165Frame frame, NativeBattleLionHeart165Inputs inputs)
    {
        int index = ReadDword(frame.Staging, 0x23C6920);
        frame = frame with { Staging = WriteDword(frame.Staging, 0x23C6920, unchecked((uint)(index + 1))) };
        return inputs.Execute(frame, new NativeBattleLionHeart165Contact(index));
    }

    private static IEnumerable<byte> TargetRing(NativeBattleLionHeart165Frame frame, NativeBattleLionHeart165Inputs inputs)
    {
        byte first = frame.FirstRecipient;
        yield return first;
        int? next = inputs.ReadModel(first).ReadLinkedModelIndex140();
        HashSet<int> seen = [first];
        while (next is int actor && actor != first)
        {
            if (!seen.Add(actor))
            {
                throw new InvalidDataException("Native Lion Heart target ring does not close at its original first target.");
            }
            NativeBattleGraphicsModel model = inputs.ReadModel(checked((byte)actor));
            if ((model.Flags0 & 2) != 0)
            {
                yield return checked((byte)actor);
            }
            next = model.ReadLinkedModelIndex140() ?? throw new InvalidDataException("Native Lion Heart linked target ring terminates before closing.");
        }
    }
}
