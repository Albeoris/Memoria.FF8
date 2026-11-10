using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleCameraTrack(Ff7BattleCameraPoint Point, ushort Pc = 255, ushort Wait = 0, bool Linear = false, bool Inverted = false);
public enum Ff7BattleCameraCallbackKind
{
    Tween,
    FollowPosition,
    FollowFocus,
    Spiral
}

public sealed record Ff7BattleCameraCallback(int Track, Ff7BattleCameraMotion? Motion, Ff7BattleCameraCallbackKind Kind = Ff7BattleCameraCallbackKind.Tween, int Actor = -1, int Bone = 0, Ff7BattleCameraPoint Offset = default, short Remaining = 0, bool TargetFacing = false, short Angle = 0, short AngleStep = 0, short Radius = 0, short RadiusStep = 0, short YStep = 0);
public sealed record Ff7BattleCameraScriptState(short CameraId, byte InitialCamera, byte IdleCamera, Ff7BattleCameraTrack[] Tracks, Ff7BattleCameraCallback?[] Callbacks)
{
    public bool ReturningToIdle { get; init; }
    public byte PresentationCounter { get; init; }
    public sbyte SelectedTrack { get; init; }
    public sbyte ActiveAction { get; init; }
}
public sealed record Ff7BattleCameraActorInput(Ff7BattleCameraModelParameters Model, short ScaleControl, short CurrentYaw, short IdleYaw, short ActionAnimation);
public sealed record Ff7BattleCameraActors(int Attacker, int Target, Func<int, int, int> JointSelector, Func<int, int, Ff7BattleCameraPoint> Joint, Func<int, Ff7BattleCameraActorInput> Actor, Func<Ff7BattleCameraPoint> GroupCenter, Ff7BattleCameraMath Math);
public readonly record struct Ff7BattleCameraScriptUpdate(Ff7BattleCameraScriptState State, int? TargetWrite);

public static class Ff7NativeBattleCameraScripts
{
    public static Ff7BattleCameraScriptState Copy(Ff7BattleCameraScriptState state) => state with { Tracks = (Ff7BattleCameraTrack[])state.Tracks.Clone(), Callbacks = (Ff7BattleCameraCallback?[])state.Callbacks.Clone() };

    public static void Validate(Ff7BattleCameraScriptState state, Func<short, byte, int, Ff7BattleCameraComponent, ReadOnlyMemory<byte>> script)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(script);
        if (state.Tracks is null || state.Tracks.Length != 8 || state.Tracks.Any(track => track is null) || state.Callbacks is null || state.Callbacks.Length != 16 || state.IdleCamera >= 3 || (uint)state.SelectedTrack >= 4 || state.ActiveAction is < 0 or > 1)
        {
            throw new InvalidDataException("FFVII camera snapshot has invalid native track, callback or selector owners.");
        }
        for (int index = 0; index < state.Tracks.Length; index++)
        {
            Ff7BattleCameraTrack track = state.Tracks[index];
            if (index % 4 == 3)
            {
                continue;
            }
            ReadOnlyMemory<byte> source = script(state.CameraId, state.InitialCamera, index % 4, index < 4 ? Ff7BattleCameraComponent.Position : Ff7BattleCameraComponent.Focus);
            if (track.Pc != 255 && track.Pc >= source.Length)
            {
                throw new InvalidDataException("FFVII camera snapshot script PC crosses its native resource.");
            }
        }
        foreach (Ff7BattleCameraCallback? callback in state.Callbacks)
        {
            if (callback is null)
            {
                continue;
            }
            if ((uint)callback.Track >= 8 || callback.Track % 4 == 3 || (uint)callback.Kind > (uint)Ff7BattleCameraCallbackKind.Spiral)
            {
                throw new InvalidDataException("FFVII camera callback references an invalid track or numerical owner.");
            }
            if (callback.Kind == Ff7BattleCameraCallbackKind.Tween)
            {
                if (callback.Motion is not { Remaining: > 0 } motion || motion.Initialized && !state.Tracks[callback.Track].Linear && motion.Total <= 0)
                {
                    throw new InvalidDataException("FFVII camera tween lost its retained native duration.");
                }
            }
            else if (callback.Remaining <= 0 || callback.Kind == Ff7BattleCameraCallbackKind.Spiral && callback.Track >= 4 || callback.Kind == Ff7BattleCameraCallbackKind.FollowPosition && callback.Track >= 4 || callback.Kind == Ff7BattleCameraCallbackKind.FollowFocus && callback.Track < 4 || callback.Kind is Ff7BattleCameraCallbackKind.FollowPosition or Ff7BattleCameraCallbackKind.FollowFocus && (uint)callback.Actor >= 10)
            {
                throw new InvalidDataException("FFVII camera callback has an invalid duration, actor or component.");
            }
        }
    }

    public static Ff7BattleCameraScriptState Create(short cameraId, byte initialCamera, SceneCameraPlacement initial, byte presentationCounter = 0)
    {
        Ff7BattleCameraTrack[] tracks = new Ff7BattleCameraTrack[8];
        for (int track = 0; track < tracks.Length; track++)
        {
            System.Numerics.Vector3 point = track < 4 ? initial.Position : initial.Focus;
            tracks[track] = new(new(checked((short)point.X), checked((short)point.Y), checked((short)point.Z)));
        }
        return new(cameraId, initialCamera, 0, tracks, new Ff7BattleCameraCallback?[16]) { ReturningToIdle = true, PresentationCounter = presentationCounter };
    }

    public static Ff7BattleCameraScriptState AdvanceOuterCallback(Ff7BattleCameraScriptState state) => state with { PresentationCounter = unchecked((byte)(state.PresentationCounter + 1)) };

    public static Ff7BattleCameraScriptState Start(Ff7BattleCameraScriptState state, short cameraId)
    {
        if (cameraId == -4)
        {
            return state;
        }
        return state with { CameraId = cameraId, Tracks = state.Tracks.Select(track => track with { Pc = 255 }).ToArray(), Callbacks = new Ff7BattleCameraCallback?[16], SelectedTrack = Ff7NativeBattleCamera.SelectTrack(state.SelectedTrack, state.PresentationCounter) };
    }

    public static Ff7BattleCameraScriptUpdate Advance(Ff7BattleCameraScriptState state, bool live, bool suspended, Func<short, byte, int, Ff7BattleCameraComponent, ReadOnlyMemory<byte>> script, IReadOnlyList<SceneCameraPlacement> idle, SceneCameraPlacement displayed, Func<int, int> cosine, Ff7BattleCameraActors? actors = null)
    {
        if (!live || suspended)
        {
            return new(state, null);
        }
        if (state.Tracks.Length != 8 || state.Callbacks.Length != 16 || idle.Count != 3 || state.IdleCamera >= 3)
        {
            throw new InvalidDataException("FFVII camera scripts require eight track rows, sixteen callback slots and three idle placements.");
        }
        state = state with { Tracks = (Ff7BattleCameraTrack[])state.Tracks.Clone(), Callbacks = (Ff7BattleCameraCallback?[])state.Callbacks.Clone() };
        int target = actors?.Target ?? -1;
        int? targetWrite = null;
        for (int track = 0; track < 3; track++)
        {
            Execute(track + 4, Ff7BattleCameraComponent.Focus);
            Execute(track, Ff7BattleCameraComponent.Position);
        }
        for (int slot = 0; slot < state.Callbacks.Length; slot++)
        {
            if (state.Callbacks[slot] is Ff7BattleCameraCallback callback)
            {
                Ff7BattleCameraTrack track = state.Tracks[callback.Track];
                if (callback.Kind == Ff7BattleCameraCallbackKind.Tween)
                {
                    Ff7BattleCameraMotionUpdate update = Ff7NativeBattleCameraMotion.Advance(callback.Motion ?? throw new InvalidDataException("FFVII camera tween lost its native motion."), track.Point, track.Linear, cosine);
                    state.Tracks[callback.Track] = track with { Point = update.Point };
                    state.Callbacks[slot] = update.Motion is null ? null : callback with { Motion = update.Motion };
                }
                else if (callback.Kind == Ff7BattleCameraCallbackKind.Spiral)
                {
                    Ff7BattleCameraMath math = RequireActors().Math;
                    short angle = unchecked((short)(callback.Angle + callback.AngleStep));
                    Ff7BattleCameraPoint focus = state.Tracks[callback.Track + 4].Point;
                    Ff7BattleCameraPoint point = new(unchecked((short)(focus.X + (unchecked(math.Sine(angle) * callback.Radius) >> 9))), unchecked((short)(track.Point.Y + callback.YStep)), unchecked((short)(focus.Z + (unchecked(math.Cosine(angle) * callback.Radius) >> 9))));
                    state.Tracks[callback.Track] = track with { Point = point };
                    state.Callbacks[slot] = callback.Remaining == 1 ? null : callback with { Angle = angle, Radius = unchecked((short)(callback.Radius + callback.RadiusStep)), Remaining = unchecked((short)(callback.Remaining - 1)) };
                }
                else
                {
                    Ff7BattleCameraPoint point = Relative(callback.Actor, callback.Bone, callback.Offset, callback.Track, callback.TargetFacing, callback.Kind == Ff7BattleCameraCallbackKind.FollowFocus, false);
                    state.Tracks[callback.Track] = track with { Point = point };
                    state.Callbacks[slot] = callback.Remaining == 1 ? null : callback with { Remaining = unchecked((short)(callback.Remaining - 1)) };
                }
            }
        }
        return new(state, targetWrite);

        Ff7BattleCameraActors RequireActors() => actors ?? throw new InvalidDataException("FFVII relative camera requires its native actor geometry inputs.");

        Ff7BattleCameraPoint Relative(int actor, int bone, Ff7BattleCameraPoint offset, int track, bool targetFacing, bool focus, bool height)
        {
            Ff7BattleCameraActors context = RequireActors();
            if (actor == 15)
            {
                Ff7BattleCameraPoint group = context.GroupCenter();
                return new(unchecked((short)(group.X + offset.X)), unchecked((short)(group.Y + offset.Y)), unchecked((short)(group.Z + offset.Z)));
            }
            if ((uint)actor >= 10)
            {
                throw new InvalidDataException("FFVII camera references an absent native presentation actor.");
            }
            Ff7BattleCameraPoint joint = context.Joint(actor, bone);
            Ff7BattleCameraActorInput input = context.Actor(actor);
            return focus
                ? Ff7NativeBattleCameraGeometry.Focus(joint, offset, input.Model.FocusYOffset, input.ActionAnimation, height)
                : Ff7NativeBattleCameraGeometry.Position(joint, offset, input.Model, input.ScaleControl, state.Tracks[track].Inverted, targetFacing ? input.IdleYaw : input.CurrentYaw, context.Math);
        }

        void Register(Ff7BattleCameraCallback callback)
        {
            int slot = Array.FindIndex(state.Callbacks, entry => entry is null);
            if (slot < 0)
            {
                throw new InvalidDataException("FFVII camera exhausted its sixteen native callback slots.");
            }
            state.Callbacks[slot] = callback;
        }

        void Execute(int index, Ff7BattleCameraComponent component)
        {
            Ff7BattleCameraTrack current = state.Tracks[index];
            if (current.Pc == 255)
            {
                current = current with { Pc = 0, Wait = 0, Linear = false, Inverted = false };
            }
            ReadOnlyMemory<byte> bytes = script(state.CameraId, state.InitialCamera, index % 4, component);
            int budget = 4096;
            bool running = true;
            while (running && budget-- > 0)
            {
                state.Tracks[index] = current;
                byte opcode = Byte();
                switch (opcode)
                {
                    case 0xD9:
                        current = current with { Point = Point(component == Ff7BattleCameraComponent.Position ? displayed.Position : displayed.Focus) };
                        break;
                    case 0xDA when component == Ff7BattleCameraComponent.Position:
                    case 0xDB when component == Ff7BattleCameraComponent.Focus:
                        current = current with { Linear = false };
                        break;
                    case 0xDB when component == Ff7BattleCameraComponent.Position:
                    case 0xDC when component == Ff7BattleCameraComponent.Focus:
                        current = current with { Linear = true };
                        break;
                    case 0xDC when component == Ff7BattleCameraComponent.Position:
                        current = current with { Inverted = true };
                        break;
                    case 0xF1 when component == Ff7BattleCameraComponent.Position:
                        current = current with { Inverted = false };
                        break;
                    case 0xDD:
                        byte selected = Byte();
                        if (selected >= 3)
                        {
                            throw new InvalidDataException("FFVII camera selected an absent idle placement.");
                        }
                        state = state with { IdleCamera = selected };
                        break;
                    case 0xDE:
                        if (Byte() != 0)
                        {
                            target = 3;
                            targetWrite = target;
                        }
                        break;
                    case 0xDF:
                        target = 15;
                        targetWrite = target;
                        break;
                    case 0xE1:
                        current = current with { Point = IdlePoint() };
                        break;
                    case 0xE2:
                        Move(IdlePoint(), unchecked((sbyte)Byte()));
                        break;
                    case 0xE6:
                        Ff7BattleCameraPoint destination = Vector();
                        Move(destination, unchecked((sbyte)Byte()));
                        break;
                    case 0xE4:
                    case 0xE5:
                        int movingActor = opcode == 0xE4 ? RequireActors().Attacker : target;
                        int movingBone = Bone(movingActor);
                        Ff7BattleCameraPoint movingOffset = Vector();
                        short movingDuration = Byte();
                        Move(Relative(movingActor, movingBone, movingOffset, index, opcode == 0xE5, component == Ff7BattleCameraComponent.Focus, true), movingDuration);
                        break;
                    case 0xE7 when component == Ff7BattleCameraComponent.Position:
                    case 0xE9 when component == Ff7BattleCameraComponent.Position:
                    case 0xE8 when component == Ff7BattleCameraComponent.Focus:
                    case 0xEA when component == Ff7BattleCameraComponent.Focus:
                        bool followTarget = opcode is 0xE9 or 0xEA;
                        int followingActor = followTarget ? target : RequireActors().Attacker;
                        if ((uint)followingActor >= 10)
                        {
                            throw new NotSupportedException("FFVII camera follow callback requires a native physical actor cache.");
                        }
                        int followingBone = Bone(followingActor);
                        Ff7BattleCameraPoint followingOffset = Vector();
                        short followingDuration = unchecked((sbyte)Byte());
                        Register(new(index, null, component == Ff7BattleCameraComponent.Position ? Ff7BattleCameraCallbackKind.FollowPosition : Ff7BattleCameraCallbackKind.FollowFocus, followingActor, followingBone, followingOffset, followingDuration, followTarget));
                        break;
                    case 0xF0 when component == Ff7BattleCameraComponent.Position:
                    case 0xF7 when component == Ff7BattleCameraComponent.Position:
                    case 0xF8 when component == Ff7BattleCameraComponent.Focus:
                    case 0xF9 when component == Ff7BattleCameraComponent.Focus:
                        bool immediateTarget = opcode is 0xF7 or 0xF8;
                        int immediateActor = immediateTarget ? target : RequireActors().Attacker;
                        int immediateBone = Bone(immediateActor);
                        Ff7BattleCameraPoint immediateOffset = Vector();
                        current = current with { Point = Relative(immediateActor, immediateBone, immediateOffset, index, immediateTarget, component == Ff7BattleCameraComponent.Focus, true) };
                        running = false;
                        break;
                    case 0xF8 when component == Ff7BattleCameraComponent.Position:
                        short angleStep = Word();
                        short radius = Word();
                        short angle = Word();
                        short radiusStep = Word();
                        short yStep = Word();
                        short spiralDuration = Word();
                        Register(new(index, null, Ff7BattleCameraCallbackKind.Spiral, Remaining: spiralDuration, Angle: angle, AngleStep: angleStep, Radius: radius, RadiusStep: radiusStep, YStep: yStep));
                        break;
                    case 0xF9 when component == Ff7BattleCameraComponent.Position:
                    case 0xFA when component == Ff7BattleCameraComponent.Focus:
                        Ff7BattleCameraPoint absolute = Vector();
                        current = current with { Point = absolute };
                        break;
                    case 0xF5:
                        ushort wait = unchecked((ushort)(short)(sbyte)Byte());
                        current = current with { Wait = wait };
                        break;
                    case 0xF4:
                        if (current.Wait != 0)
                        {
                            current = current with { Wait = unchecked((ushort)(current.Wait - 1)), Pc = unchecked((ushort)(current.Pc - 1)) };
                            running = false;
                        }
                        break;
                    case 0xFF:
                        current = current with { Pc = unchecked((ushort)(current.Pc - 1)) };
                        running = false;
                        break;
                    default:
                        throw new NotSupportedException($"FFVII camera {state.CameraId} {component} opcode {opcode:X2} at {current.Pc - 1:X4} is unsupported.");
                }
            }
            if (running)
            {
                throw new InvalidDataException("FFVII camera script exceeded its bounded immediate instruction budget.");
            }
            state.Tracks[index] = current;

            byte Byte()
            {
                if (current.Pc >= bytes.Length)
                {
                    throw new InvalidDataException("FFVII camera script crossed its resource boundary.");
                }
                byte value = bytes.Span[current.Pc];
                current = current with { Pc = unchecked((ushort)(current.Pc + 1)) };
                return value;
            }

            short Word()
            {
                byte low = Byte();
                return unchecked((short)(low | Byte() << 8));
            }

            Ff7BattleCameraPoint Vector() => new(Word(), Word(), Word());
            Ff7BattleCameraPoint IdlePoint() => Point(component == Ff7BattleCameraComponent.Position ? idle[state.IdleCamera].Position : idle[state.IdleCamera].Focus);

            int Bone(int actor)
            {
                int selector = unchecked((sbyte)Byte());
                return actor == 15 ? 0 : RequireActors().JointSelector(actor, selector);
            }

            void Move(Ff7BattleCameraPoint destination, short duration)
            {
                Register(new(index, new(current.Point, destination, duration)));
            }
        }
    }

    private static Ff7BattleCameraPoint Point(System.Numerics.Vector3 point) => new(checked((short)point.X), checked((short)point.Y), checked((short)point.Z));
}
