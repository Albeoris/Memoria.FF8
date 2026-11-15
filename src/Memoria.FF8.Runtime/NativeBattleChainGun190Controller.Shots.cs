using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static NativeBattleChainGun190Frame PrepareShots(NativeBattleChainGun190Frame frame, NativeBattleChainGun190Inputs inputs)
    {
        if (frame.State.Hosted is { } hosted)
        {
            for (int index = 0; index < hosted.ShotCount; index++)
            {
                frame = FillShots(frame, index, 1, hosted.Contacts[index % hosted.Contacts.Length].Owner, inputs);
            }
            return frame;
        }
        for (int index = 0; index < RecordCount; index++)
        {
            frame = Dword(frame, frame.State.ShotAddress(index), 0);
        }
        NativeBattleActionHeaderSlot header = inputs.Packets.ReadHeader(Parameters(frame).Header);
        int count = header.ReadContactCount();
        if (count is < 1 or > 3)
        {
            return frame;
        }
        BattleContactCursor first = header.FirstContact;
        BattleModelOwner Actor(int ordinal) => inputs.Packets.ReadContact(first.Advance(ordinal)).Primary.Recipient;
        if (count == 1)
        {
            return FillShots(frame, 0, 12, Actor(0), inputs);
        }
        BattleModelOwner firstActor = Actor(0);
        BattleModelOwner secondActor = Actor(1);
        if (count == 2 && inputs.Packets.ModelAliases.TryEncodeExact(firstActor, out byte firstOperand) && inputs.Packets.ModelAliases.TryEncodeExact(secondActor, out byte secondOperand))
        {
            byte firstSlot = Math.Min(firstOperand, secondOperand);
            byte secondSlot = Math.Max(firstOperand, secondOperand);
            BattleModelOwner low = firstOperand <= secondOperand ? firstActor : secondActor;
            BattleModelOwner high = firstOperand <= secondOperand ? secondActor : firstActor;
            if ((firstSlot, secondSlot) == (0, 1))
            {
                frame = FillShots(frame, 0, 5, high, inputs);
                frame = InterpolateShots(frame, 5, 2, ModelPoint(frame, high, inputs), ModelPoint(frame, low, inputs));
                frame = FillShots(frame, 7, 5, low, inputs);
            }
            else if ((firstSlot, secondSlot) == (0, 2))
            {
                NativeBattleModelTransform upper = inputs.ReadModel(frame, high).Transform ?? throw new NotSupportedException("Chain Gun requires its current recipient position.");
                NativeBattleModelTransform lower = inputs.ReadModel(frame, low).Transform ?? throw new NotSupportedException("Chain Gun requires its current recipient position.");
                NativeBattleEffectArena midpoint = PointView(new(unchecked((short)((upper.Position28.X + lower.Position28.X) / 2)), 0, unchecked((short)((upper.Position28.Z + lower.Position28.Z) / 2))));
                frame = InterpolateShots(frame, 0, 2, midpoint, ModelPoint(frame, high, inputs));
                frame = FillShots(frame, 2, 3, high, inputs);
                frame = InterpolateShots(frame, 5, 4, ModelPoint(frame, high, inputs), ModelPoint(frame, low, inputs));
                frame = FillShots(frame, 9, 3, low, inputs);
            }
            else if ((firstSlot, secondSlot) == (1, 2))
            {
                frame = FillShots(frame, 0, 2, low, inputs);
                frame = InterpolateShots(frame, 2, 2, ModelPoint(frame, low, inputs), ModelPoint(frame, high, inputs));
                frame = FillShots(frame, 4, 3, high, inputs);
                frame = InterpolateShots(frame, 7, 2, ModelPoint(frame, high, inputs), ModelPoint(frame, low, inputs));
                frame = FillShots(frame, 9, 3, low, inputs);
            }
        }
        BattleModelOwner third = count == 2 ? inputs.ReadOriginalFallthroughRecipient(first.Advance(2)) : Actor(2);
        BattleModelOwner[] captured = [firstActor, secondActor, third];
        BattleModelOwner[] actors = captured.OrderBy(OriginalOperand).ToArray();
        frame = FillShots(frame, 0, 2, actors[1], inputs);
        frame = InterpolateShots(frame, 2, 2, ModelPoint(frame, actors[1], inputs), ModelPoint(frame, actors[2], inputs));
        frame = FillShots(frame, 4, 3, actors[2], inputs);
        frame = InterpolateShots(frame, 7, 2, ModelPoint(frame, actors[2], inputs), ModelPoint(frame, actors[0], inputs));
        return FillShots(frame, 9, 3, actors[0], inputs);

        byte OriginalOperand(BattleModelOwner actor)
        {
            if (!inputs.Packets.ModelAliases.TryEncodeExact(actor, out byte operand))
            {
                throw new InvalidDataException("Chain Gun requires an exact original alias for every authored shot owner.");
            }
            return operand;
        }
    }

    public static NativeBattleChainGun190Frame FillShots(NativeBattleChainGun190Frame frame, int start, int count, BattleModelOwner actor, NativeBattleChainGun190Inputs inputs)
    {
        RequireShots(frame.State, start, count);
        for (int index = start; index < start + count; index++)
        {
            uint record = frame.State.ShotAddress(index);
            frame = Dword(frame, record, 1);
            if ((index & 1) == 0)
            {
                frame = DrawRandom(frame, out ushort mode);
                if ((mode & 1) == 0)
                {
                    frame = frame with { State = frame.State.WritePositionedShot(record + 4) };
                    frame = frame with { State = frame.State.WriteView(record + 8, ModelPoint(frame, actor, inputs), 8) };
                    continue;
                }
            }
            frame = frame with { State = frame.State.WriteShotTarget(record + 4, actor, inputs.Packets.ModelAliases) };
            int bones = inputs.ReadBoneCount(frame, actor);
            if ((uint)bones > 255)
            {
                throw new InvalidDataException("Chain Gun requires its actual current body BYTE bone count.");
            }
            int selector = 0;
            if (bones != 0)
            {
                frame = DrawRandom(frame, out ushort selected);
                selector = selected % bones;
            }
            frame = DrawRandom(frame, out ushort distance);
            NativeBattleChainGun190PointStep point = inputs.ReadBone502170(frame, actor, selector, distance & 0xFFF);
            frame = point.Frame with { State = point.Frame.State.WriteView(record + 8, point.Point8, 6) };
        }
        return frame;
    }

    public static NativeBattleChainGun190Frame InterpolateShots(NativeBattleChainGun190Frame frame, int start, int count, NativeBattleEffectArena from, NativeBattleEffectArena to)
    {
        RequireShots(frame.State, start, count);
        short x = BitConverter.ToInt16(from.Read(0, 2));
        short z = BitConverter.ToInt16(from.Read(4, 2));
        short stepX = unchecked((short)((BitConverter.ToInt16(to.Read(0, 2)) - x) / (count + 1)));
        short stepZ = unchecked((short)((BitConverter.ToInt16(to.Read(4, 2)) - z) / (count + 1)));
        for (int index = 0; index < count; index++)
        {
            uint record = frame.State.ShotAddress(start + index);
            frame = Dword(frame, record, 1);
            frame = frame with { State = frame.State.WritePositionedShot(record + 4) };
            frame = Word(Word(Word(frame, record + 8, x + stepX * (index + 1)), record + 10, 0), record + 12, z + stepZ * (index + 1));
        }
        return frame;
    }

    private static NativeBattleEffectArena ModelPoint(NativeBattleChainGun190Frame frame, BattleModelOwner actor, NativeBattleChainGun190Inputs inputs)
    {
        NativeBattleModelTransform transform = inputs.ReadModel(frame, actor).Transform ?? throw new NotSupportedException("Chain Gun requires its actual current model position and vertical WORD.");
        return PointView(transform.Position28, transform.Vertical34);
    }

    private static void RequireShots(NativeBattleChainGun190State state, int start, int count)
    {
        if (start < 0 || count < 0 || start > (state.Hosted?.RecordCapacity ?? RecordCount) - count)
        {
            throw new InvalidDataException("Chain Gun shot records leave their owned bank.");
        }
    }
}
