namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleChocoMogCameraTrack(Ff7BattleCameraPoint Point = default, Ff7BattleCameraPoint Step = default, int Remaining = 0, int Actor = -1, int Bone = -1);
public sealed record Ff7BattleChocoMogCameraState(Ff7BattleChocoMogCameraTrack Position, Ff7BattleChocoMogCameraTrack Focus, int ProgramCounter = 0, bool Retired = false)
{
    public Ff7BattleChocoMogCameraState() : this(new(), new())
    {
    }
}

public static class Ff7BattleChocoMogCamera
{
    public static Ff7BattleChocoMogCameraState Advance(Ff7BattleChocoMogCameraState state, IReadOnlyList<short> script)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(script);
        if (state.Retired)
        {
            return state;
        }

        int pc = state.ProgramCounter;
        Ff7BattleChocoMogCameraTrack position = state.Position;
        Ff7BattleChocoMogCameraTrack focus = state.Focus;
        bool retired = false;
        while (position.Remaining == 0 || focus.Remaining == 0)
        {
            int instruction = Read();
            if (instruction == -1)
            {
                retired = true;
                break;
            }
            if ((instruction & ~0xCF) != 0)
            {
                throw new NotSupportedException($"FFVII Choco/Mog camera instruction {instruction:X4} is unsupported.");
            }
            bool eye = (instruction & 0x40) != 0;
            Ff7BattleChocoMogCameraTrack track = eye ? position : focus;
            switch (instruction & 15)
            {
                case 0:
                    track = track with { Remaining = Duration(), Step = default };
                    break;
                case 1:
                    track = track with { Point = Point() };
                    break;
                case 2:
                    int remaining = Duration();
                    Ff7BattleCameraPoint destination = Point();
                    track = track with { Remaining = remaining, Step = Delta(track.Point, destination, remaining) };
                    break;
                case 6:
                    int actor = Read();
                    int bone = Read();
                    if (actor is < 0 or > 9 || bone < -1)
                    {
                        throw new InvalidDataException("FFVII Choco/Mog camera anchor is outside its native actor bank.");
                    }
                    track = track with { Actor = actor, Bone = bone };
                    break;
                case 7:
                    track = track with { Actor = -1, Bone = -1 };
                    break;
                case 8:
                    if (Read() != 0)
                    {
                        throw new NotSupportedException("FFVII Fat Chocobo camera requires its original zero track-reset operand.");
                    }
                    break;
                default:
                    throw new NotSupportedException($"FFVII Choco/Mog camera instruction {instruction:X4} is unsupported.");
            }
            if (eye)
            {
                position = track;
            }
            else
            {
                focus = track;
            }
        }

        if (!retired)
        {
            position = Step(position);
            focus = Step(focus);
        }
        return new(position, focus, pc, retired);

        short Read()
        {
            if ((uint)pc >= script.Count)
            {
                throw new InvalidDataException("FFVII Choco/Mog camera crossed its original program boundary.");
            }
            return script[pc++];
        }

        short Duration()
        {
            short duration = Read();
            if (duration <= 0)
            {
                throw new InvalidDataException("FFVII Choco/Mog camera requires a positive authored duration.");
            }
            return duration;
        }

        Ff7BattleCameraPoint Point() => new(Read(), Read(), Read());
    }

    public static Ff7BattleCameraView View(Ff7BattleChocoMogCameraState state, int source, Func<int, int, Ff7BattleCameraPoint> joint)
    {
        return new(World(state.Position), World(state.Focus));

        Ff7BattleCameraPoint World(Ff7BattleChocoMogCameraTrack track) => track.Actor >= 0 ? Add(track.Point, joint(track.Actor == 0 ? source : track.Actor, track.Bone)) : track.Point;
    }

    private static Ff7BattleChocoMogCameraTrack Step(Ff7BattleChocoMogCameraTrack track) => track with { Point = Add(track.Point, track.Step), Remaining = track.Remaining - 1 };

    private static Ff7BattleCameraPoint Add(Ff7BattleCameraPoint left, Ff7BattleCameraPoint right) => new(unchecked((short)(left.X + right.X)), unchecked((short)(left.Y + right.Y)), unchecked((short)(left.Z + right.Z)));

    private static Ff7BattleCameraPoint Delta(Ff7BattleCameraPoint point, Ff7BattleCameraPoint destination, int duration) => new(unchecked((short)((destination.X - point.X) / duration)), unchecked((short)((destination.Y - point.Y) / duration)), unchecked((short)((destination.Z - point.Z) / duration)));
}
