using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed record Ff7HighwayRider(int Slot, int Kind, int Variant, int X, int Z, int Status, int AttackTimer, int Lean)
{
    public bool Active => Status != 5;
}

public sealed partial class Ff7HighwayRuntime
{
    private sealed class Rider(int slot, int kind, int x = 0, int z = 0, int minimumZ = 0x1F400, int maximumZ = 0xBB800, int front = 20, int back = 100, int frontRadius = 36, int backRadius = 36)
    {
        internal int Slot { get; } = slot;
        internal int Kind { get; } = kind;
        internal int MinimumZ { get; } = minimumZ;
        internal int MaximumZ { get; } = maximumZ;
        internal int Front { get; } = front;
        internal int Back { get; } = back;
        internal int FrontRadius { get; } = frontRadius;
        internal int BackRadius { get; } = backRadius;
        internal int X = x;
        internal int Z = z;
        internal int VelocityX;
        internal int VelocityZ;
        internal int Variant;
        internal int Status;
        internal int AttackTimer;
        internal int Lean;
        internal int LeanRadius;
        internal int HitPoints;
        internal int Direction;
        internal int MovementTimer;
        internal int DamageTimer;
        internal Vector3 LastPosition;
        internal int FeintTimer;
        internal int CrashTimer;
        internal int CrashVelocity;
        internal int Path = -1;
        internal int PathPosition;
        internal int PathOffsetX;
        internal int PathOffsetZ;
        internal bool OnPath => Path >= 0;
        internal bool Active => Status != 5;
        internal Ff7HighwayRider View => new(Slot, Kind, Variant, X, Z, Status, AttackTimer, Lean);

        internal void SetPath(int path, int xOffset, int zOffset)
        {
            Path = path;
            PathPosition = 0;
            PathOffsetX = xOffset;
            PathOffsetZ = zOffset;
            Status = 6;
        }
    }

    private void AdvanceRiders(ushort held, ushort pressed)
    {
        SpawnEnemies();
        MovePlayer(riders[0], held, pressed);
        MoveVehicle(riders[1]);
        if (riders[5].Active)
        {
            MoveVehicle(riders[5]);
        }
        for (int slot = 2; slot <= 4; slot++)
        {
            if (riders[slot].Active)
            {
                MoveEnemy(riders[slot]);
            }
        }
        AdvanceAttack(riders[0]);
        if (riders[0].Path == -1)
        {
            _ = NextRandom() % 10;
            if (NextRandom() % 30 == 0)
            {
                Sound?.Invoke(2, 0x12D);
            }
        }
        if (riders[1].DamageTimer > 0 && --riders[1].DamageTimer == 0 && riders[1].Status == 1)
        {
            riders[1].Status = 0;
        }
        for (int slot = 2; slot <= 4; slot++)
        {
            AdvanceEnemyAction(riders[slot]);
        }
        foreach (Rider rider in riders)
        {
            if (rider.Active)
            {
                ClampToRoad(rider);
                rider.LastPosition = RiderPosition(rider);
            }
        }
        CollideRiders();
    }

    private void SpawnEnemies()
    {
        if (spawnDelay > 0)
        {
            spawnDelay--;
            return;
        }
        if (enemiesDisabled)
        {
            return;
        }
        for (int slot = 2; slot <= 4; slot++)
        {
            if (NextRandom() % 60 == 0 && !riders[slot].Active)
            {
                int variant = NextRandom() % 3;
                riders[slot] = new(slot, 2, (slot - 3) * 0xC800, -0xBB800)
                {
                    Variant = variant,
                    HitPoints = variant switch { 0 => 5, 1 => 60, _ => 30 }
                };
                enemies++;
            }
        }
    }

    private void MovePlayer(Rider rider, ushort held, ushort pressed)
    {
        if (FollowPath(rider))
        {
            return;
        }
        if (rider.AttackTimer == 0)
        {
            if ((pressed & 0x80) != 0)
            {
                rider.AttackTimer = 29;
            }
            else if ((pressed & 0x20) != 0)
            {
                rider.AttackTimer = -29;
            }
        }
        bool up = (held & 0x1000) != 0;
        bool down = (held & 0x4000) != 0;
        int? angle = null;
        if ((held & 0x8000) != 0)
        {
            angle = up ? 0x600 : down ? 0xA00 : 0x800;
        }
        else if ((held & 0x2000) != 0)
        {
            angle = up ? 0x200 : down ? 0xE00 : 0;
        }
        else if (down || up)
        {
            angle = down ? 0xC00 : 0x400;
        }
        if (rider.Status == 6)
        {
            angle = rider.X > 0xB400 ? 0x800 : rider.X < 0xA500 ? 0 : null;
        }
        if (rider.DamageTimer > 1)
        {
            rider.DamageTimer--;
            rider.X -= 0x600;
            rider.Z += 0x800;
        }
        if (angle is int direction)
        {
            Accelerate(rider, direction, 0x658);
            rider.Lean = Math.Clamp(rider.Lean + (math.Cosine(direction) >> 1), -0x4400, 0x4400);
            rider.LeanRadius = Math.Abs(rider.Lean >> 9);
        }
        else
        {
            if (rider.Lean >= 0x400)
            {
                rider.Lean -= 0xC00;
            }
            if (rider.Lean < -0x3FF)
            {
                rider.Lean += 0xC00;
            }
            if (rider.Lean is > -0x400 and < 0x400)
            {
                rider.Lean = 0;
            }
        }
        ApplyVelocity(rider);
    }

    private void MoveVehicle(Rider rider)
    {
        if (FollowPath(rider))
        {
            return;
        }
        if (rider.Slot == 5 && NextRandom() % 30 == 0)
        {
            Sound?.Invoke(2, 0x12E);
        }
        if (rider.Z > rider.MaximumZ)
        {
            rider.MovementTimer = 1;
            rider.Direction = 0xC00;
        }
        if (rider.Z < rider.MinimumZ)
        {
            rider.MovementTimer = 1;
            rider.Direction = 0x400;
        }
        if (rider.MovementTimer == 0)
        {
            int frequency = rider.Slot == 1 ? 0x800 : 0x100;
            if (NextRandom() % 4096 < frequency)
            {
                if (rider.Slot == 1)
                {
                    rider.Direction = NextRandom() % 4096;
                    rider.MovementTimer = 10;
                }
                else
                {
                    _ = NextRandom() % 4096;
                    rider.Direction = RelativeAngle(rider, riders[1]);
                    rider.MovementTimer = NextRandom() % 10000;
                }
            }
            if (rider.Slot == 1)
            {
                rider.VelocityX = rider.VelocityX * 8 / 10;
                rider.VelocityZ = rider.VelocityZ * 8 / 10;
                return;
            }
        }
        else
        {
            rider.MovementTimer--;
            Accelerate(rider, rider.Direction, rider.Slot == 1 ? (rider.Path == -1 ? 500 : 0) : 250);
        }
        if (rider.Slot == 1)
        {
            rider.X += rider.VelocityX;
            rider.Z += rider.VelocityZ;
            rider.VelocityX = rider.VelocityX * 8 / 10;
            rider.VelocityZ = rider.VelocityZ * 8 / 10;
        }
        else
        {
            ApplyVelocity(rider);
        }
    }

    private bool FollowPath(Rider rider)
    {
        if (!rider.OnPath)
        {
            return rider.Path == -2;
        }
        Vector3 position = SamplePath(rider.Path, rider.PathPosition);
        rider.X = ((int)position.X << 7) + rider.PathOffsetX;
        rider.Z = ((int)position.Z << 7) + rider.PathOffsetZ;
        int end = (start.Course.Paths[rider.Path].PointCount - 1) << 16;
        rider.PathPosition += 0x10000;
        if (rider.PathPosition >= end)
        {
            rider.Path = rider.Slot == 0 ? -1 : -2;
            if (rider.Slot == 0)
            {
                rider.Status = 0;
                rider.DamageTimer = 0x49;
            }
        }
        return true;
    }

    private void MoveEnemy(Rider rider)
    {
        int acceleration = rider.Variant == 0 && enemies == 3 ? 1000 : rider.Status == 1 ? 0 : rider.Variant == 0 ? 1000 : 800;
        int direction = RelativeAngle(rider, riders[1]);
        if (enemiesDisabled)
        {
            acceleration = 4000;
            direction = 0xC00;
        }
        else if (rider.Variant == 0 && riders[0].AttackTimer != 0 && RiderDistance(rider, riders[0]) < 500)
        {
            acceleration = 1300;
            direction = RelativeAngle(rider, riders[0]) + 0x800;
        }
        else if (rider.Variant == 2)
        {
            if (rider.FeintTimer == 0 && NextRandom() % 100 == 0)
            {
                rider.FeintTimer = NextRandom() % 90;
            }
            if (rider.FeintTimer > 0)
            {
                direction = RelativeAngle(rider, riders[0]) + 0x200;
                rider.FeintTimer--;
            }
        }
        if (!enemiesDisabled && rider.Z < rider.MinimumZ)
        {
            acceleration = 2000;
            direction = 0x400;
        }
        Accelerate(rider, direction, acceleration);
        ApplyVelocity(rider);
        rider.Z = Math.Min(rider.Z, rider.MaximumZ);
        if (enemiesDisabled && rider.Z < -5000)
        {
            rider.Status = 5;
        }
    }

    private void AdvanceAttack(Rider player)
    {
        if (player.AttackTimer == 0)
        {
            return;
        }
        player.AttackTimer -= Math.Sign(player.AttackTimer);
        if (player.AttackTimer is not (>= -22 and <= -11 or >= 11 and <= 19))
        {
            return;
        }
        if (player.AttackTimer is -22 or 19)
        {
            Sound?.Invoke(2, 5);
        }
        for (int index = 2; index <= 4; index++)
        {
            Rider target = riders[index];
            if (target.Status is not (0 or 1) || RiderDistance(player, target) >= 160)
            {
                continue;
            }
            int angle = RelativeAngle(player, target) & 0xFFF;
            bool hit = player.AttackTimer < 0 ? angle is >= 1 and <= 0x383 or >= 0xE71 : angle is >= 0x47D and <= 0x98F;
            if (hit)
            {
                Sound?.Invoke(2, 0x12);
                DamageRider(target, 50);
            }
        }
    }

    private void AdvanceEnemyAction(Rider rider)
    {
        if (rider.Status == 1 && --rider.AttackTimer < 0)
        {
            rider.Status = 0;
        }
        if (rider.Status == 2)
        {
            if (--rider.CrashTimer < 0)
            {
                rider.Status = 5;
            }
            rider.CrashVelocity += 0x200;
            rider.Z -= rider.CrashVelocity;
        }
    }

    private void ClampToRoad(Rider rider)
    {
        int width = road.Segment(TrackPosition + (rider.Z >> 8)).Width;
        int limit = ((width >> 1) - 0x60) << 8;
        if (rider.Slot == 5)
        {
            limit -= 0x7800;
        }
        rider.X = Math.Clamp(rider.X, -limit, limit);
        if (rider.Slot == 0)
        {
            rider.Z = Math.Clamp(rider.Z, rider.MinimumZ, rider.MaximumZ);
        }
    }

    private void Accelerate(Rider rider, int angle, int acceleration)
    {
        rider.VelocityX += (math.Cosine(angle) * acceleration) >> 12;
        rider.VelocityZ += (math.Sine(angle) * acceleration) >> 11;
    }

    private static void ApplyVelocity(Rider rider)
    {
        rider.VelocityX = rider.VelocityX * 8 / 10;
        rider.VelocityZ = rider.VelocityZ * 8 / 10;
        rider.X += rider.VelocityX;
        rider.Z += rider.VelocityZ;
    }

    private int RelativeAngle(Rider from, Rider to)
    {
        return math.Angle(to.Z - from.Z, to.X - from.X);
    }

    private int RiderDistance(Rider from, Rider to)
    {
        return (int)Vector3.Distance(from.LastPosition, to.LastPosition);
    }

    private Vector3 RiderPosition(Rider rider)
    {
        return road.Sample(TrackPosition + (rider.Z >> 8), rider.X >> 8);
    }
}
