using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7HighwayRuntime
{
    private readonly Ff7HighwayStart start;
    private readonly Ff7BattleCameraMath math;
    private readonly Ff7HighwayRoad road;
    private readonly Rider[] riders;
    private readonly Ff7HighwayGauge[] gauges;
    private readonly byte[] gaugeFlash = new byte[5];
    private int distance;
    private int speed;
    private int nextEvent;
    private int endingTimer = -1;
    private int spawnDelay = 120;
    private int enemies;
    private bool enemiesDisabled;
    private uint random;
    private int cameraStep;
    private bool fadeIn = true;

    internal Ff7HighwayRuntime(Ff7HighwayStart start, Ff7BattleCameraMath math, uint seed)
    {
        this.start = start;
        this.math = math;
        random = seed;
        road = new(start.Course, math);
        speed = start.InitialSpeed;
        gauges = start.Gauges.ToArray();
        riders = [new(0, 0, 0, 0x3E800, 0x1F400, 0xDAC00, 20, 100, 36, 36), new(1, 1, 0, 0x9C400, 0x5DC00, 0x9C400, -30, 100, 75, 75), new(2, 2), new(3, 2), new(4, 2), new(5, 10, 0x3E800, -7000, 0x1F400, 0x5DC00, -80, 75, 132, 240)];
        riders[2].Status = riders[3].Status = riders[4].Status = riders[5].Status = 5;
        foreach (Rider rider in riders)
        {
            rider.LastPosition = RiderPosition(rider);
        }
        Gauges = Array.AsReadOnly(gauges);
        FadeAlpha = 252;
    }

    public long Tick { get; private set; }
    public int TrackPosition => distance + 0x2300;
    public int TrackSegment => start.InitialTrackSegment + (distance >> 8);
    public bool FadingOut { get; private set; }
    public byte FadeAlpha { get; private set; }
    public bool InputEnabled => endingTimer < 0;
    public int? CameraPath { get; private set; }
    public int CameraPathPosition { get; private set; }
    public int? FixedCameraPosition { get; private set; }
    public int OverlayFlags { get; private set; }
    public IReadOnlyList<Ff7HighwayGauge> Gauges { get; }
    public IReadOnlyList<Ff7HighwayRoadSegment> Road => road.Window(distance);
    public IReadOnlyList<Ff7HighwayRider> Riders => Array.AsReadOnly(riders.Select(rider => rider.View).ToArray());
    public Ff7HighwayRider Player => riders[0].View;
    public Ff7HighwayRider Truck => riders[1].View;

    internal FieldHostRequest Request => start.Request;
    internal uint RandomSeed => random;
    internal bool Finished { get; private set; }
    internal event Action<int, int>? Sound;

    public Vector3 SampleRoad(int position, int lateralOffset = 0)
    {
        return road.Sample(position, lateralOffset);
    }

    public Vector3 SamplePath(int path, int position, bool positiveYAndZ = true)
    {
        return start.Course.Paths[path].Sample(checked((uint)position), positiveYAndZ);
    }

    internal void Advance(ushort held, ushort pressed)
    {
        if (Finished)
        {
            throw new InvalidOperationException("The FFVII highway has already finished.");
        }
        Tick++;
        ApplyCourseEvent();
        if (endingTimer >= 0)
        {
            if (endingTimer > 180)
            {
                speed = Math.Max(0, speed - 3);
            }
            if (endingTimer == 315)
            {
                FadingOut = true;
                fadeIn = false;
                FadeAlpha = 0;
            }
            if (FadingOut)
            {
                FadeAlpha = (byte)Math.Min(byte.MaxValue, FadeAlpha + 5);
            }
            if (endingTimer == 184)
            {
                Sound?.Invoke(3, 0x241);
                Sound?.Invoke(4, 0x241);
            }
            if (endingTimer == 203)
            {
                Sound?.Invoke(3, 0);
                Sound?.Invoke(4, 0);
            }
            Finished = endingTimer == 450;
            endingTimer++;
        }
        if (fadeIn && Tick > 1)
        {
            FadeAlpha = (byte)Math.Max(0, FadeAlpha - 4);
            fadeIn = FadeAlpha != 0;
        }
        distance += speed;
        AdvanceRiders(InputEnabled ? held : (ushort)0, InputEnabled ? pressed : (ushort)0);
        AdvanceGaugeDisplay();
        if (CameraPath is int path)
        {
            int end = ((start.Course.Paths[path].PointCount - 1) << 16) - 1;
            if (CameraPathPosition + cameraStep <= end)
            {
                CameraPathPosition += cameraStep;
            }
            else
            {
                CameraPath = null;
            }
        }
    }

    private void AdvanceGaugeDisplay()
    {
        for (int index = 0; index < gauges.Length; index++)
        {
            Ff7HighwayGauge gauge = gauges[index];
            byte state = gauge.PortraitState;
            if (NextRandom() % 30 == 0 && state == 1 && gauge.Value > 10)
            {
                state = 3;
            }
            if (state >= 2)
            {
                state--;
            }
            gauges[index] = gauge with { PortraitState = state, HitFlash = gaugeFlash[index] };
            gaugeFlash[index] = (byte)Math.Max(0, gaugeFlash[index] - 8);
        }
    }

    private void ApplyCourseEvent()
    {
        if (nextEvent >= start.Course.Events.Count)
        {
            return;
        }
        var entry = start.Course.Events[nextEvent];
        if (entry.Segment >= TrackSegment - 45)
        {
            return;
        }
        nextEvent++;
        switch (entry.Code)
        {
            case 5:
            case 19:
                FixedCameraPosition = TrackPosition + (entry.Code == 5 ? 0x29E0 : 0x14F0);
                CameraPath = null;
                break;
            case 6:
                FixedCameraPosition = null;
                CameraPath = null;
                break;
            case 8:
                enemiesDisabled = true;
                break;
            case 10:
                OverlayFlags |= 1;
                break;
            case 11:
                OverlayFlags &= ~1;
                break;
            case 12:
                OverlayFlags |= 2;
                break;
            case 13:
                OverlayFlags &= ~2;
                break;
            case 14:
                riders[5].Status = 0;
                Sound?.Invoke(3, 0x126);
                break;
            case 16:
                OverlayFlags |= 4;
                break;
            case 17:
                OverlayFlags &= ~4;
                break;
            case 18:
                SetCameraPath(5, 0x27100);
                break;
            case 20:
                riders[0].SetPath(7, -0xC800, 0x38400);
                riders[5].SetPath(6, 0xAA00, 0x1F400);
                riders[1].SetPath(8, -0xB400, 0x5DC00);
                SetCameraPath(2, 0x10000);
                endingTimer = 0;
                break;
            case 25:
                OverlayFlags |= 8;
                break;
            case 26:
                OverlayFlags &= ~8;
                break;
            default:
                throw new NotSupportedException($"FFVII highway story event {entry.Code} requires its native callback.");
        }
    }

    private void SetCameraPath(int path, int step)
    {
        CameraPath = path;
        CameraPathPosition = 0;
        FixedCameraPosition = null;
        cameraStep = step;
    }

    private int NextRandom()
    {
        random = unchecked(random * 214013 + 2531011);
        return (int)((random >> 16) & 32767);
    }
}
