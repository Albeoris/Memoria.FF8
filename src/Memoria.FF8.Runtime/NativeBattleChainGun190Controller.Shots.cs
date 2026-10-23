namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static NativeBattleChainGun190Frame PrepareShots(NativeBattleChainGun190Frame frame, NativeBattleChainGun190Inputs inputs)
    {
        for (int index = 0; index < 48; index++)
        {
            frame = Dword(frame, 0x2282E08u + (uint)(index * 16), 0);
        }
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
        int count = header.ReadByte(16);
        if (count is < 1 or > 3)
        {
            return frame;
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Chain Gun requires its actual physical contact pointer.");
        int Actor(int ordinal) => inputs.Packets.ReadContact(checked(first + ordinal)).Primary.ActorIndex;
        if (count == 1)
        {
            return FillShots(frame, 0, 12, Actor(0), inputs);
        }
        if (count == 2)
        {
            int low = Math.Min(Actor(0), Actor(1));
            int high = Math.Max(Actor(0), Actor(1));
            uint mask = (1u << low) | (1u << high);
            if (mask == 3)
            {
                frame = FillShots(frame, 0, 5, high, inputs);
                frame = InterpolateShots(frame, 5, 2, ModelPoint(frame, high, inputs), ModelPoint(frame, low, inputs));
                frame = FillShots(frame, 7, 5, low, inputs);
            }
            else if (mask == 5)
            {
                NativeBattleModelTransform upper = inputs.ReadModel(frame, high).Transform ?? throw new NotSupportedException("Chain Gun requires its current recipient position.");
                NativeBattleModelTransform lower = inputs.ReadModel(frame, low).Transform ?? throw new NotSupportedException("Chain Gun requires its current recipient position.");
                NativeBattleEffectArena midpoint = PointView(new(unchecked((short)((upper.Position28.X + lower.Position28.X) / 2)), 0, unchecked((short)((upper.Position28.Z + lower.Position28.Z) / 2))));
                frame = InterpolateShots(frame, 0, 2, midpoint, ModelPoint(frame, high, inputs));
                frame = FillShots(frame, 2, 3, high, inputs);
                frame = InterpolateShots(frame, 5, 4, ModelPoint(frame, high, inputs), ModelPoint(frame, low, inputs));
                frame = FillShots(frame, 9, 3, low, inputs);
            }
            else if (mask == 6)
            {
                frame = FillShots(frame, 0, 2, low, inputs);
                frame = InterpolateShots(frame, 2, 2, ModelPoint(frame, low, inputs), ModelPoint(frame, high, inputs));
                frame = FillShots(frame, 4, 3, high, inputs);
                frame = InterpolateShots(frame, 7, 2, ModelPoint(frame, high, inputs), ModelPoint(frame, low, inputs));
                frame = FillShots(frame, 9, 3, low, inputs);
            }
        }
        int[] actors = [Actor(0), Actor(1), Actor(2)];
        Array.Sort(actors);
        frame = FillShots(frame, 0, 2, actors[1], inputs);
        frame = InterpolateShots(frame, 2, 2, ModelPoint(frame, actors[1], inputs), ModelPoint(frame, actors[2], inputs));
        frame = FillShots(frame, 4, 3, actors[2], inputs);
        frame = InterpolateShots(frame, 7, 2, ModelPoint(frame, actors[2], inputs), ModelPoint(frame, actors[0], inputs));
        return FillShots(frame, 9, 3, actors[0], inputs);
    }

    public static NativeBattleChainGun190Frame FillShots(NativeBattleChainGun190Frame frame, int start, int count, int actor, NativeBattleChainGun190Inputs inputs)
    {
        RequireShots(start, count);
        for (int index = start; index < start + count; index++)
        {
            uint record = 0x2282E08u + (uint)(index * 16);
            frame = Dword(frame, record, 1);
            if ((index & 1) == 0)
            {
                frame = DrawRandom(frame, out ushort mode);
                if ((mode & 1) == 0)
                {
                    frame = Dword(frame, record + 4, uint.MaxValue);
                    frame = frame with { State = frame.State.WriteView(record + 8, ModelPoint(frame, actor, inputs), 8) };
                    continue;
                }
            }
            frame = Dword(frame, record + 4, checked((uint)actor));
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
        RequireShots(start, count);
        short x = BitConverter.ToInt16(from.Read(0, 2));
        short z = BitConverter.ToInt16(from.Read(4, 2));
        short stepX = unchecked((short)((BitConverter.ToInt16(to.Read(0, 2)) - x) / (count + 1)));
        short stepZ = unchecked((short)((BitConverter.ToInt16(to.Read(4, 2)) - z) / (count + 1)));
        for (int index = 0; index < count; index++)
        {
            uint record = 0x2282E08u + (uint)((start + index) * 16);
            frame = Dword(Dword(frame, record, 1), record + 4, uint.MaxValue);
            frame = Word(Word(Word(frame, record + 8, x + stepX * (index + 1)), record + 10, 0), record + 12, z + stepZ * (index + 1));
        }
        return frame;
    }

    private static NativeBattleEffectArena ModelPoint(NativeBattleChainGun190Frame frame, int actor, NativeBattleChainGun190Inputs inputs)
    {
        NativeBattleModelTransform transform = inputs.ReadModel(frame, actor).Transform ?? throw new NotSupportedException("Chain Gun requires its actual current model position and vertical WORD.");
        return PointView(transform.Position28, transform.Vertical34);
    }

    private static void RequireShots(int start, int count)
    {
        if (start < 0 || count < 0 || start > 48 - count)
        {
            throw new InvalidDataException("Chain Gun shot records leave their physical bank.");
        }
    }
}
