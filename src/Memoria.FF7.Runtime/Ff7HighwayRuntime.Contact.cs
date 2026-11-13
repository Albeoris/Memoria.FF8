using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7HighwayRuntime
{
    private void CollideRiders()
    {
        for (int first = 0; first < riders.Length - 1; first++)
        {
            Rider a = riders[first];
            if (!a.Active)
            {
                continue;
            }
            for (int second = first + 1; second < riders.Length; second++)
            {
                Rider b = riders[second];
                if (b.Active && ContactAngle(a, b) is int angle)
                {
                    ResolveContact(a, b, angle);
                }
            }
        }
    }

    private int? ContactAngle(Rider a, Rider b)
    {
        Vector3 aFront = Endpoint(a, a.Front);
        Vector3 aBack = Endpoint(a, a.Back);
        Vector3 bFront = Endpoint(b, b.Front);
        Vector3 bBack = Endpoint(b, b.Back);
        int extension = a.LeanRadius + b.LeanRadius;
        Vector3 delta;
        if (Vector3.Distance(bFront, aFront) < a.FrontRadius + b.FrontRadius + extension)
        {
            delta = bFront - aFront;
        }
        else if (Vector3.Distance(bFront, aBack) < a.BackRadius + b.FrontRadius + extension)
        {
            delta = bFront - aBack;
        }
        else if (Vector3.Distance(bBack, aFront) < a.FrontRadius + b.BackRadius + extension)
        {
            delta = bBack - aFront;
        }
        else if (Vector3.Distance(bBack, aBack) < a.BackRadius + b.BackRadius + extension)
        {
            delta = bBack - aBack;
        }
        else
        {
            return null;
        }
        return (math.Angle((int)delta.Z, (int)delta.X) - 0x800 + road.Segment(TrackPosition + (b.Z >> 8)).Yaw) & 0xFFF;
    }

    private Vector3 Endpoint(Rider rider, int offset)
    {
        int position = TrackPosition + (rider.Z >> 8);
        Vector3 center = road.Sample(position, rider.X >> 8);
        var segment = road.Segment(position);
        return center + new Vector3((math.Sine(segment.Yaw) * offset) >> 12, -(math.Sine(segment.Pitch) * offset) >> 12, (math.Cosine(segment.Yaw) * offset) >> 12);
    }

    private void ResolveContact(Rider a, Rider b, int angle)
    {
        int x = (math.Cosine(angle) * 5000) >> 12;
        int z = (math.Sine(angle) * 5000) >> 11;
        a.VelocityX = a.Slot == 0 ? x >> 1 : x;
        a.VelocityZ = a.Slot == 0 ? z >> 1 : z;
        b.VelocityX = -x;
        b.VelocityZ = -z;
        if (a.Slot >= 2 && b.Slot == 5)
        {
            a.VelocityX = x * 2;
            a.VelocityZ = z * 2;
            b.VelocityX = 0;
            b.VelocityZ = 0;
        }
        if (a.Slot >= 2 && (a.Status == 2) != (b.Status == 2))
        {
            DamageRider(a.Status == 2 ? b : a, 15);
        }
        if (a.Slot == 1)
        {
            int quadrant = ((angle + 0x800) & 0xFFF) >> 10;
            DamageGauge(quadrant switch { 0 => 2, 1 => 3, 2 => 1, _ => 4 }, 400);
        }
        if (a.Slot == 0 && b.Slot != 1)
        {
            DamageGauge(0, 64);
            if (b.Status != 2)
            {
                DamageRider(b, 1);
            }
        }
        if (b.Slot == 5)
        {
            DamageRider(a, 20);
        }
    }

    private void DamageRider(Rider rider, int damage)
    {
        if (rider.Kind == 1)
        {
            rider.Status = 1;
            rider.DamageTimer = 18;
            return;
        }
        if (rider.Kind != 2)
        {
            return;
        }
        if (rider.HitPoints <= 0)
        {
            rider.Status = 2;
            rider.CrashTimer = 59;
            rider.CrashVelocity = 0;
            Sound?.Invoke(1, 0x91);
            _ = NextRandom() % 3;
            if (enemies != 1 && CameraPath is null && FixedCameraPosition is null)
            {
                SetCameraPath(NextRandom() % 2 + 3, 40000);
            }
            enemies--;
        }
        else
        {
            rider.HitPoints -= damage;
            rider.Status = 1;
            rider.AttackTimer = 28;
        }
    }

    private void DamageGauge(int index, int damage)
    {
        if (index == 2 && gauges[index].Value < 30)
        {
            damage = 0;
        }
        gaugeFlash[index] = 0x70;
        Ff7HighwayGauge gauge = gauges[index];
        gauges[index] = gauge with { Value = (ushort)Math.Max(0, gauge.Value - damage) };
        if (gauges[index].Value != 0)
        {
            return;
        }
        int selected = index;
        for (int i = 0; i < 4; i++)
        {
            int next = (selected + i) % 4 + 1;
            if (gauges[next].Value != 0)
            {
                selected = next;
            }
        }
        if (selected != index)
        {
            gaugeFlash[selected] = 0x70;
            gauge = gauges[selected];
            gauges[selected] = gauge with { Value = (ushort)Math.Max(0, gauge.Value - damage) };
        }
    }
}
