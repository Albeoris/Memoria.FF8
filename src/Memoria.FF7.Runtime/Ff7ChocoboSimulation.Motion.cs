using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal sealed partial class Ff7ChocoboSimulation
{
    private int lastRacer;
    private readonly HashSet<int> consumedCameraEvents = [];

    internal int FinishTimer { get; private set; }
    internal int FirstFinisher { get; private set; } = -1;
    internal bool SecondFinished { get; private set; }
    internal event Action<int, int>? CameraEvent;
    internal event Action<int>? AnimationEvent;
    internal event Action<int, int, int>? Audio;

    internal void AdvanceRacers(bool started, ushort held, int countdown)
    {
        int[] order = Enumerable.Range(0, Racers.Length).ToArray();
        int[] positions = Racers.Select(racer => (int)racer.Path).ToArray();
        for (int first = 0; first < Racers.Length; first++)
        {
            for (int second = first; second < Racers.Length; second++)
            {
                if (positions[first] < positions[second])
                {
                    (positions[first], positions[second]) = (positions[second], positions[first]);
                    (order[first], order[second]) = (order[second], order[first]);
                }
            }
            Racers[order[first]].Rank = S(first);
        }
        lastRacer = order[^1];
        for (int index = 0; index < Racers.Length; index++)
        {
            Racer racer = Racers[index];
            if (!started)
            {
                racer.Animation = 3;
                racer.AnimationStep = 0x100;
                racer.Speed = 10;
            }
            else if (racer.Speed != 0)
            {
                Move(index, held, countdown);
                racer.AnimationStep = S(racer.Speed / animationDivisor);
            }
            else
            {
                FinishTimer++;
                racer.Animation = S(racer.Placement == 1 ? 2 : specialOpponent && racer.Jockey == 1 ? racer.Placement == 2 ? 3 : 4 : racer.Placement == 6 ? 4 : 3);
                racer.AnimationStep = 0x100;
            }
            racer.AnimationCounter = S(racer.AnimationCounter + racer.AnimationStep);
            racer.SecondaryAnimationCounter = S(racer.SecondaryAnimationCounter + racer.AnimationStep);
            if ((racer.AnimationCounter >> 8) >= native.AnimationLengths[racer.Animation])
            {
                if (racer.PreviousAnimation != racer.Animation)
                {
                    racer.PreviousAnimation = racer.Animation;
                    racer.AnimationCounter = 0;
                }
                else
                {
                    racer.AnimationCounter = S(racer.AnimationCounter & 255);
                }
            }
            Collide(index);
        }
    }

    private void Collide(int index)
    {
        Racer racer = Racers[index];
        for (int otherIndex = 0; otherIndex < Racers.Length; otherIndex++)
        {
            if (otherIndex == index)
            {
                continue;
            }
            Racer other = Racers[otherIndex];
            int dx = racer.Position.X - other.Position.X;
            int dz = racer.Position.Z - other.Position.Z;
            int distance = (int)Math.Sqrt(unchecked(dx * dx + dz * dz));
            if (distance < 150)
            {
                if (other.Finished != 0)
                {
                    other.Speed = racer.Speed;
                    racer.Speed = 10;
                }
                if (other.Speed == 0 || racer.Speed == 0)
                {
                    continue;
                }
                int otherInverse = 0x80000 / other.Speed;
                short angle = Direction(dx, 0, dz);
                int inverse = 0x80000 / racer.Speed;
                if (racer.Jockey == 0 && racer.SoundCounter < 0)
                {
                    racer.SoundCounter = 30;
                    // Volume is selected by the presentation camera; sample choice owns a shared CRT draw.
                    int sample = (NextRandom() & 1) + 0x114;
                    Audio?.Invoke(0x2B, racer.Lane / 2, sample);
                }
                if (otherInverse != 0)
                {
                    racer.Position = (S(racer.Position.X + native.Trigonometry.Sine(angle) / otherInverse), racer.Position.Y, S(racer.Position.Z - native.Trigonometry.Cosine(angle) / otherInverse));
                    racer.Speed = S(racer.Speed - racer.Speed / 8);
                    if (racer.Speed < 100)
                    {
                        racer.Speed = 100;
                    }
                }
                if (inverse != 0)
                {
                    other.Position = (S(other.Position.X - native.Trigonometry.Sine(angle) / inverse), other.Position.Y, S(other.Position.Z + native.Trigonometry.Cosine(angle) / inverse));
                }
            }
            if (racer.Behavior == 2 && distance < 170)
            {
                NextRandom();
            }
        }
    }

    private void Move(int index, ushort held, int countdown)
    {
        Racer racer = Racers[index];
        if (racer.Finished != 0)
        {
            racer.Speed = S(racer.Speed - (racer.Placement + 1) * 80 - racer.Speed / 64);
            if (racer.Speed < 100)
            {
                racer.Speed = 0;
            }
            else
            {
                racer.Animation = 0;
                racer.Dash = 0;
                if (racer.Behavior == 2)
                {
                    racer.AnimationCounter = 0;
                    racer.Animation = 1;
                    racer.Speed = S(racer.Speed - 50);
                }
            }
        }
        int speed = racer.Speed;
        PcChocoboTrackElement next = course.Track[racer.NextPath];
        if (Cross(next.Right.X - next.Left.X, 0, next.Right.Z - next.Left.Z, racer.Position.X - next.Left.X, 0, racer.Position.Z - next.Left.Z).Y >= 0)
        {
            racer.Stagnation = 0;
            int path = 0;
            for (int probe = racer.Path - 5; probe < racer.Path + 20; probe++)
            {
                path = (probe + course.Track.Count) % course.Track.Count;
                PcChocoboTrackElement section = course.Track[path];
                if (Cross(section.Right.X - section.Left.X, 0, section.Right.Z - section.Left.Z, racer.Position.X - section.Left.X, 0, racer.Position.Z - section.Left.Z).Y < 0)
                {
                    break;
                }
            }
            racer.Path = racer.NextPath;
            racer.NextPath = S((path + course.Track.Count) % course.Track.Count);
            UpdateSection(index, held, countdown);
            racer.Target = Sample(racer.NextPath, racer.Lane);
            var lookahead = Sample((racer.NextPath + 1) % course.Track.Count, racer.Lane);
            racer.TargetYaw = Direction(lookahead.X - racer.Position.X, 0, lookahead.Z - racer.Position.Z);
            int camera = course.Track[racer.Path].CameraEvent;
            if (camera != 0 && !consumedCameraEvents.Contains(racer.Path))
            {
                CameraEvent?.Invoke(camera, index);
                consumedCameraEvents.Add(racer.Path - 1);
                consumedCameraEvents.Add(racer.Path);
                consumedCameraEvents.Add(racer.Path + 1);
            }
            int animation = S(course.Track[racer.Path].Right.Attributes);
            if (animation != 0)
            {
                AnimationEvent?.Invoke(animation);
            }
        }
        else
        {
            racer.Stagnation = S(racer.Stagnation + 1);
            if (racer.Stagnation > 15 && racer.Finished == 0)
            {
                racer.Path = S(racer.Path + 2);
                racer.NextPath = S(racer.NextPath + 2);
                racer.Position = Sample(racer.Path, racer.Lane);
                racer.Target = Sample(racer.NextPath, racer.Lane);
                racer.Speed = racer.EffectiveSpeed = racer.DesiredSpeed = racer.CruiseSpeed;
                racer.Stagnation = 0;
                racer.Yaw = racer.TargetYaw = Direction(racer.Target.X - racer.Position.X, 0, racer.Target.Z - racer.Position.Z);
            }
        }
        if (racer.Path > course.Track.Count - 10 && racer.Finished == 0)
        {
            racer.Finished = -1;
            racer.Target = racer.Origin;
            racer.Placement = S(Racers.Max(candidate => candidate.Placement) + 1);
            if (racer.Placement == 1)
            {
                FirstFinisher = index;
                if (!jockey)
                {
                    Audio?.Invoke(0x10, 0x3B, 0);
                }
            }
            if (racer.Placement == 2)
            {
                SecondFinished = true;
            }
            if (jockey && index == 0)
            {
                Audio?.Invoke(0x10, racer.Placement == 1 ? 0x3B : 0x3C, 0);
            }
        }
        if (racer.Yaw - racer.TargetYaw > 3000)
        {
            racer.Yaw = S(racer.Yaw - 4096);
        }
        if (racer.Yaw - racer.TargetYaw < -3000)
        {
            racer.Yaw = S(racer.Yaw + 4096);
        }
        racer.Yaw = S((racer.Yaw * 3 + racer.TargetYaw) / 4);
        if (speed != 0)
        {
            racer.RemainderX = unchecked(racer.RemainderX + native.Trigonometry.Sine(racer.Yaw) * speed / 64);
            racer.RemainderZ = unchecked(racer.RemainderZ - native.Trigonometry.Cosine(racer.Yaw) * speed / 64);
            racer.Position = (S(racer.Position.X + racer.RemainderX / 4096), racer.Position.Y, S(racer.Position.Z + racer.RemainderZ / 4096));
            racer.RemainderX &= unchecked((int)0x80000FFF);
            racer.RemainderZ &= unchecked((int)0x80000FFF);
        }
        next = course.Track[racer.NextPath];
        PcChocoboTrackElement current = course.Track[racer.Path];
        var normal = Cross(next.Left.X - current.Left.X, next.Left.Y - current.Left.Y, next.Left.Z - current.Left.Z, next.Right.X - next.Left.X, next.Right.Y - next.Left.Y, next.Right.Z - next.Left.Z);
        int height = normal.Y != 0 ? unchecked((current.Left.X - racer.Position.X) * normal.X + (current.Left.Z - racer.Position.Z) * normal.Z + normal.Y * current.Left.Y) / normal.Y : racer.Position.Y;
        racer.PreviousY = S((racer.PreviousY + height) / 2);
        racer.Position = (racer.Position.X, racer.PreviousY, racer.Position.Z);
    }

    private static (int X, int Y, int Z) Cross(int ax, int ay, int az, int bx, int by, int bz)
    {
        float x = (float)((double)(ay / 4096f) * (bz / 4096f) - (double)(az / 4096f) * (by / 4096f));
        float y = (float)((double)(az / 4096f) * (bx / 4096f) - (double)(ax / 4096f) * (bz / 4096f));
        float z = (float)((double)(ax / 4096f) * (by / 4096f) - (double)(ay / 4096f) * (bx / 4096f));
        return ((int)((double)x * 4096), (int)((double)y * 4096), (int)((double)z * 4096));
    }

    private void UpdateSection(int index, ushort held, int countdown)
    {
        Racer racer = Racers[index];
        if (index != 0 || !jockey || racer.Automatic != 0)
        {
            if (NextRandom() < 5000)
            {
                switch (racer.Type)
                {
                    case 3:
                        if (racer.LastSprint < racer.Path)
                        {
                            racer.Lane = S((racer.Lane * 7 + racer.OriginalLane) / 8);
                        }
                        goto case 0;
                    case 0:
                        int leader = Array.FindIndex(Racers, candidate => candidate.Rank == 0);
                        if (leader != index)
                        {
                            racer.Acceleration = S(Racers[leader].Rank - racer.Rank > 30 ? 10 : racer.DesiredSpeed > racer.CruiseSpeed * 5 / 4 ? -5 : racer.DesiredSpeed < racer.CruiseSpeed ? 3 : 0);
                        }
                        break;
                    case 1:
                        if (lastRacer == index)
                        {
                            if (racer.Dash == 0)
                            {
                                racer.Dash = S(NextRandom() & 40);
                            }
                        }
                        else
                        {
                            racer.Acceleration = S(racer.DesiredSpeed > racer.CruiseSpeed * 5 / 4 ? -5 : racer.DesiredSpeed < racer.CruiseSpeed * 9 / 10 ? 3 : 0);
                        }
                        break;
                    case 2:
                        if (racer.Stamina > racer.MaxStamina / 3)
                        {
                            racer.Acceleration = 20;
                            if (racer.LastSprint > racer.Path && racer.CanSprint != 0)
                            {
                                racer.Dash = 10;
                            }
                        }
                        else
                        {
                            racer.Acceleration = S(racer.DesiredSpeed > racer.CruiseSpeed * 10 / 9 ? -10 : 2);
                        }
                        break;
                }
                if (racer.LastSprint < racer.Path && racer.Type != 2)
                {
                    if (racer.CanSprint != 0)
                    {
                        racer.Dash = 50;
                    }
                    racer.Lane = S((racer.Lane * 7 + racer.OriginalLane) / 8);
                }
            }
            racer.SmoothedLane = S((racer.SmoothedLane + racer.Lane) / 2);
        }
        else if (racer.Stamina > 0 && racer.Finished == 0)
        {
            if ((held & 0x80) != 0)
            {
                racer.Acceleration = 2;
            }
            if ((held & 0x40) != 0)
            {
                racer.Acceleration = -2;
            }
            if ((held & 0x20) != 0)
            {
                racer.Dash = 2;
            }
        }
        if ((NextRandom() & 63) < racer.Obedience && racer.Dash == 0)
        {
            racer.DesiredSpeed = S(racer.DesiredSpeed + racer.DesiredSpeed * racer.Acceleration / 100);
            racer.DesiredSpeed = S(Math.Min(racer.DesiredSpeed, racer.CruiseSpeed * 5 / 4));
            if (racer.Stamina < 0)
            {
                racer.DesiredSpeed = racer.CruiseSpeed;
            }
            if (racer.DesiredSpeed > racer.CruiseSpeed)
            {
                racer.Stamina -= (racer.DesiredSpeed - racer.CruiseSpeed) / 128;
            }
            racer.Acceleration = S(racer.Acceleration - Math.Sign(racer.Acceleration));
        }
        if (racer.Dash != 0)
        {
            racer.Dash = S(Math.Max(0, racer.Dash - 1));
            racer.Stamina -= racer.MaxStamina / 300;
            if (racer.Stamina <= 0)
            {
                racer.Dash = 0;
                racer.Stamina = 0;
            }
            racer.Animation = S(racer.Dash == 0 ? 0 : 1);
            racer.EffectiveSpeed = S(racer.EarlySprint != 0 && countdown < 150 ? 5500 : racer.TopSpeed);
        }
        else if (racer.FixedSpeed != 0)
        {
            racer.EffectiveSpeed = racer.CruiseSpeed;
        }
        else
        {
            racer.EffectiveSpeed = S(racer.EffectiveSpeed + (racer.DesiredSpeed - racer.EffectiveSpeed) * racer.SpeedResponse / 100);
        }
        if (racer.Rank > 1 && racer.Behavior == 1)
        {
            racer.EffectiveSpeed = S(racer.EffectiveSpeed * 9 / 10);
        }
        if (racer.Automatic == 0 && jockey)
        {
            if ((NextRandom() & 63) < racer.Obedience)
            {
                if ((held & 0x8000) != 0)
                {
                    racer.Lane = S(racer.Lane + racer.LaneStep + racer.Obedience / 32);
                }
                if ((held & 0x2000) != 0)
                {
                    racer.Lane = S(racer.Lane - racer.LaneStep - racer.Obedience / 32);
                }
            }
            if (held != 0 && racer.Obedience < (NextRandom() & 31))
            {
                racer.Lane = S(racer.Lane + ((NextRandom() & 3) - 1) * (25 - racer.Obedience / 2));
                if (racer.Stamina > 0 && (NextRandom() & 7) == 0)
                {
                    racer.Stamina -= racer.MaxStamina / 500;
                    if (racer.Behavior == 2)
                    {
                        racer.Dash = 10;
                    }
                    else
                    {
                        racer.Speed = S(racer.Speed - racer.Speed / 4);
                    }
                }
            }
        }
        else
        {
            int delta = course.Guide.Span[racer.Path * 8 + racer.Guide] - racer.Lane;
            if (delta < 0 && -delta > racer.LaneStep)
            {
                delta = -racer.LaneStep + racer.LaneBias;
            }
            else if (delta > racer.LaneStep)
            {
                delta = racer.LaneStep + racer.LaneBias;
            }
            for (int other = 0; other < Racers.Length; other++)
            {
                if (other == index)
                {
                    continue;
                }
                if (Math.Abs(racer.Path - Racers[other].Path) < 20 && ((Math.Abs(racer.Lane - Racers[other].Lane) < 16 && (NextRandom() & 31) == 0) || racer.Finished != 0))
                {
                    delta += ((NextRandom() & 7) - 3) * 16;
                }
            }
            racer.Lane = S(racer.Lane + delta);
        }
        if (racer.Finished == 0)
        {
            racer.Speed = S(unchecked((sbyte)course.Guide.Span[racer.Path * 8 + racer.Guide + 4]) * racer.EffectiveSpeed / 1024 + racer.EffectiveSpeed);
            if (racer.Stamina <= 5)
            {
                racer.Speed = S(racer.Speed - racer.Speed / 4);
                racer.Animation = 5;
            }
            else if (lastRacer == index)
            {
                racer.Speed = S(racer.Speed + racer.Speed / 8);
            }
        }
        if (racer.Dash == 0 && racer.Rank < 3 && racer.Path < racer.LastSprint)
        {
            racer.Speed = S(racer.Speed - racer.Speed / (racer.Rank + (racer.Type == 2 ? 8 : 7)));
        }
        int terrain = racer.TerrainMask & course.Track[racer.Path].TerrainFlags;
        if (terrain != 0)
        {
            racer.Speed = S(racer.Speed - racer.Speed * terrain / 4);
        }
        racer.Lane = S(Math.Clamp((int)racer.Lane, 48, 216));
    }
}
