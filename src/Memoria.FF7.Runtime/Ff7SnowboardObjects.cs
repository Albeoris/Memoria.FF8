using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

internal readonly record struct Ff7SnowboardVisibleSegment(int Course, int Segment);
internal readonly record struct Ff7SnowboardObjectFrame(int ModelIndex, Vector3 PositionQ12, short Heading, int ScaleShift, ushort Fade, int DrawArgument)
{
    internal short Pitch { get; init; }
    internal short Roll { get; init; }
    internal int NativePrimitiveKind { get; init; }
    internal Vector3? NativePosition { get; init; }
}
internal readonly record struct Ff7SnowboardObjectSound(int Channel, int Sample, int Volume);

internal sealed class Ff7SnowboardObjects
{
    private readonly Ff7SnowboardObjectData objects;
    private readonly Ff7SnowboardNativeData native;
    private readonly bool[] consumed = new bool[604];
    private readonly Slot?[] slots = new Slot?[32];
    private readonly List<Ff7SnowboardVisibleSegment> visible = [];
    private readonly List<Ff7SnowboardObjectFrame> frames = [];
    private int nextSlot;
    private short lastImpactDistance;
    private ushort nativeClock;
    private short lastBounceClock;
    private short lastBounceVolume;

    internal Ff7SnowboardObjects(Ff7SnowboardObjectData objects, Ff7SnowboardNativeData native, short totalScore = 0)
    {
        if (objects.Producers.Count != consumed.Length)
        {
            throw new InvalidDataException("The snowboard object producer table has an invalid length.");
        }
        this.objects = objects;
        this.native = native;
        TotalScore = totalScore;
    }

    internal IReadOnlyList<Ff7SnowboardVisibleSegment> VisibleSegments => visible;
    internal IReadOnlyList<Ff7SnowboardObjectFrame> Frames => frames;
    internal short ScoreCollected { get; private set; }
    internal short TotalScore { get; }
    internal event Action<Ff7SnowboardObjectSound>? Sound;

    internal void Advance(Ff7SnowboardSimulation simulation, long tick, int first, int count, bool advance = true, bool presentationSample = true)
    {
        nativeClock = unchecked((ushort)tick);
        if (presentationSample)
        {
            BuildVisibleWindow(simulation.Navigation.CurrentCourse, simulation.Navigation.CurrentSegment, first, count);
        }
        foreach (Ff7SnowboardVisibleSegment segment in visible)
        {
            Activate(native.Courses[segment.Course].StartDistance + segment.Segment, simulation);
        }
        if (presentationSample)
        {
            frames.Clear();
        }
        foreach (Slot? slot in slots)
        {
            if (slot is null || !slot.Active)
            {
                continue;
            }
            if (slot.Distance < simulation.LogicalDistance)
            {
                slot.Active = false;
                continue;
            }
            if (advance)
            {
                Update(slot, simulation);
            }
            if (!slot.Active || slot.U16(0x8A) == ushort.MaxValue)
            {
                continue;
            }
            ushort fade = slot.U16(0x8A);
            if (presentationSample && fade != 0)
            {
                slot.Write16(0x8C, unchecked((short)(slot.I16(0x8C) + 1)));
                if ((slot.U16(0x8C) & 1) != 0)
                {
                    continue;
                }
            }
            if (presentationSample)
            {
                frames.Add(new(slot.I16(4), new(slot.I32(0x26), slot.I32(0x2A), slot.I32(0x2E)), slot.I16(0x20), slot.I16(8), fade, slot.I32(0x10)) { Pitch = slot.I16(0x1E), Roll = slot.I16(0x22), NativePrimitiveKind = slot.I32(0xC), NativePosition = new(slot.I32(0x26) >> 12, slot.I32(0x2A) >> 12, slot.I32(0x2E) >> 12) });
            }
            Contact(slot, simulation);
        }
    }

    internal void BuildVisibleWindow(int course, int segment, int first, int count)
    {
        visible.Clear();
        segment -= first;
        if (segment < 0)
        {
            int previous = (int)(native.Courses[course].Descriptor >> 24);
            if (previous == 255)
            {
                segment = 0;
            }
            else
            {
                course = previous;
                segment = Math.Max(0, native.Courses[course].Tiles.Count + segment);
            }
        }
        Add();
        int pendingRight = 0;
        int rightCount = 0;
        while (true)
        {
            while (count-- > 0)
            {
                int tile = native.Courses[course].Tiles[segment];
                int tileOffset = tile * Ff7SnowboardTerrain.TileSize;
                short type = unchecked((short)(native.TileGeometry[tileOffset] | native.TileGeometry[tileOffset + 1] << 8));
                if (type == 3)
                {
                    uint connections = native.Courses[course].Connections;
                    course = unchecked((sbyte)connections);
                    pendingRight = unchecked((sbyte)(connections >> 8));
                    segment = 0;
                    count--;
                    rightCount = count;
                }
                else
                {
                    segment++;
                    if (segment >= native.Courses[course].Tiles.Count)
                    {
                        break;
                    }
                }
                Add();
            }
            if (pendingRight == 0)
            {
                break;
            }
            course = pendingRight;
            pendingRight = 0;
            segment = 0;
            count = rightCount;
            Add();
        }

        void Add()
        {
            if ((uint)course >= native.Courses.Count || (uint)segment >= native.Courses[course].Tiles.Count)
            {
                throw new InvalidDataException("The snowboard visible course window references an invalid native course segment.");
            }
            if (visible.Count < 20)
            {
                visible.Add(new(course, segment));
            }
        }
    }

    private void Activate(int distance, Ff7SnowboardSimulation simulation)
    {
        if ((uint)distance >= consumed.Length)
        {
            throw new InvalidDataException("The snowboard object activation is outside its native table.");
        }
        bool first = !consumed[distance];
        consumed[distance] = true;
        Ff7SnowboardObjectProducer producer = objects.Producers[distance];
        if (producer.Kind == Ff7SnowboardObjectProducerKind.Unsupported)
        {
            throw new NotSupportedException($"The snowboard object producer at logical distance {distance} is unsupported.");
        }
        if (producer.Kind == Ff7SnowboardObjectProducerKind.None && producer.PlaneTriangleBase < 0)
        {
            return;
        }
        if (first && producer.Kind != Ff7SnowboardObjectProducerKind.None && (producer.TotalScoreThreshold is not { } threshold || TotalScore > threshold))
        {
            Spawn(distance, producer);
            foreach (Ff7SnowboardObjectProducer additional in producer.Additional)
            {
                Spawn(distance, additional);
            }
        }
        if (producer.ChecksPlayerClearance)
        {
            Ff7SnowboardRiderController rider = simulation.Rider;
            ushort? reaction = ClearanceReaction(distance, simulation.LogicalDistance, simulation.Navigation.CurrentTriangle, rider.Script.ActionWord, rider.Motion.FloorHeight, rider.Motion.Position.Y);
            if (reaction is { } action)
            {
                rider.SetAction(action);
                Sound?.Invoke(new(2, 240, 0));
            }
        }
        if (producer.PlaneTriangleBase >= 0)
        {
            Ff7SnowboardRiderController rider = simulation.Rider;
            if (PlaneCollision(distance, simulation.LogicalDistance, simulation.Navigation.CurrentTriangle, rider.Script.ActionWord, rider.Motion.FloorHeight, rider.Motion.Position.Y, producer.PlaneTriangleBase, producer.PlaneThresholds))
            {
                Vector3 velocity = rider.Motion.Velocity;
                rider.Motion.Velocity = new(velocity.X, PlaneReactionVelocity(velocity.Y), velocity.Z);
                rider.SetAction(11);
                Sound?.Invoke(new(2, 268, 0));
            }
        }
    }

    private void Spawn(int distance, Ff7SnowboardObjectProducer producer)
    {
        for (int attempt = 0; attempt < slots.Length; attempt++)
        {
            int index = (nextSlot + attempt) & 31;
            if (slots[index] is { Active: true })
            {
                continue;
            }
            Slot slot = new(distance, producer);
            slots[index] = slot;
            nextSlot = (index + 1) & 31;
            return;
        }
    }

    private void Update(Slot slot, Ff7SnowboardSimulation simulation)
    {
        if (slot.Kind == Ff7SnowboardObjectProducerKind.StaticDecoration)
        {
            return;
        }
        if (slot.Kind == Ff7SnowboardObjectProducerKind.TopplingObstacle)
        {
            UpdateToppling(slot);
            return;
        }
        if (slot.Kind == Ff7SnowboardObjectProducerKind.RetiringObstacle)
        {
            UpdateRetiring(slot);
            return;
        }
        if (slot.Kind == Ff7SnowboardObjectProducerKind.BouncingObstacle)
        {
            UpdateBouncing(slot, simulation);
            return;
        }
        if (slot.Kind == Ff7SnowboardObjectProducerKind.ImpactObstacle)
        {
            UpdateImpact(slot, simulation);
            return;
        }
        if (slot.Kind == Ff7SnowboardObjectProducerKind.BendingObstacle)
        {
            UpdateBending(slot);
            return;
        }
        if (slot.Kind == Ff7SnowboardObjectProducerKind.Obstacle)
        {
            short counter = slot.I16(0x84);
            if (counter != 0)
            {
                slot.Write16(0x84, unchecked((short)(counter + 1)));
                if (counter > 60)
                {
                    ushort fade = slot.U16(0x8A);
                    slot.Write16(0x8A, unchecked((short)(fade + 1)));
                    if (fade > 30)
                    {
                        slot.Active = false;
                    }
                }
            }
            else if (slot.Bytes[1] != 0)
            {
                if ((slot.Bytes[1] & 2) != 0)
                {
                    slot.Write16(0x14, 0);
                    Sound?.Invoke(new(2, 268, 0));
                }
                slot.Write16(0x84, 1);
            }
            return;
        }
        if (slot.CallbackPhase == 1)
        {
            short counter = slot.I16(0x88);
            slot.Write16(0x88, unchecked((short)(counter + 1)));
            if (counter > 120)
            {
                ushort fade = slot.U16(0x8A);
                slot.Write16(0x8A, unchecked((short)(fade + 1)));
                if (fade > 30)
                {
                    slot.Active = false;
                    return;
                }
            }
            short retiringFrame = slot.I16(0x84);
            slot.Write16(0x84, unchecked((short)(retiringFrame + 1)));
            slot.Write16(4, (short)(240 + ((retiringFrame >> 3) & 3)));
            slot.Write32(0x2A, unchecked(slot.I32(0x2A) + slot.I32(0x6E)));
            slot.Write32(0x6E, unchecked(slot.I32(0x6E) - 2048));
            return;
        }
        if (slot.Bytes[1] != 0)
        {
            slot.Active = false;
            Sound?.Invoke(new(3, 249, 0));
            ScoreCollected = unchecked((short)(ScoreCollected + objects.BalloonScores[slot.I32(0x3C)]));
            return;
        }
        short frame = slot.I16(0x84);
        slot.Write16(0x84, unchecked((short)(frame + 1)));
        slot.Write16(4, (short)(240 + ((frame >> 3) & 3)));
        int bob = Sine(unchecked(frame << 5));
        slot.Write32(0x2A, unchecked(slot.I32(0x2A) + bob * 2));
        if (Math.Abs(lastImpactDistance - slot.Distance) <= 1)
        {
            slot.CallbackPhase = 1;
        }
    }

    private void UpdateBending(Slot slot)
    {
        if (slot.CallbackPhase == 1)
        {
            short counter = slot.I16(0x84);
            slot.Write16(0x1E, BendingPitch(counter));
            slot.Write16(0x24, 0);
            slot.Write16(0x84, unchecked((short)(counter + 1)));
            RetireAfter(slot, counter, 120);
            return;
        }
        if (slot.CallbackPhase == 2)
        {
            short counter = slot.I16(0x84);
            slot.Write16(0x84, unchecked((short)(counter + 1)));
            RetireAfter(slot, counter, 60);
        }
        else if (slot.Bytes[1] != 0)
        {
            if ((slot.Bytes[1] & 2) != 0)
            {
                slot.Write16(0x14, 0);
                Sound?.Invoke(new(3, 240, 0));
                slot.CallbackPhase = 1;
            }
            else
            {
                slot.CallbackPhase = 2;
            }
        }
        if (slot.I16(4) - 234 == 2)
        {
            short frame = slot.I16(0x86);
            slot.Write16(0x86, unchecked((short)(frame + 1)));
            slot.Write16(0x22, unchecked((short)(Sine(Sine(unchecked(frame << 5)) >> 4) >> 3)));
            slot.Write16(0x24, 0);
        }
    }

    private void UpdateToppling(Slot slot)
    {
        if (slot.CallbackPhase == 1)
        {
            short counter = slot.I16(0x84);
            slot.Write16(0x84, unchecked((short)(counter + 1)));
            short pitch = unchecked((short)(slot.I16(0x1E) + TopplingPitchDelta(counter)));
            slot.Write16(0x1E, pitch);
            slot.Write16(0x24, 0);
            if (pitch > 1152)
            {
                slot.CallbackPhase = 2;
            }
            return;
        }
        if (slot.CallbackPhase == 2)
        {
            short counter = slot.I16(0x86);
            slot.Write16(0x86, unchecked((short)(counter + 1)));
            RetireAfter(slot, counter, 60);
            return;
        }
        if (slot.Bytes[1] == 0)
        {
            return;
        }
        if ((slot.Bytes[1] & 2) != 0)
        {
            slot.Write16(0x14, 0);
            slot.CallbackPhase = 1;
            Sound?.Invoke(new(1, 268, 127));
        }
        else
        {
            slot.CallbackPhase = 2;
        }
    }

    private void UpdateRetiring(Slot slot)
    {
        if (slot.CallbackPhase != 0)
        {
            short counter = slot.I16(0x84);
            slot.Write16(0x84, unchecked((short)(counter + 1)));
            RetireAfter(slot, counter, 60);
            return;
        }
        if (slot.Bytes[1] == 0)
        {
            return;
        }
        if ((slot.Bytes[1] & 2) != 0)
        {
            slot.Write16(0x14, 0);
            Sound?.Invoke(new(2, 268, 0));
        }
        slot.CallbackPhase = 1;
    }

    private void UpdateBouncing(Slot slot, Ff7SnowboardSimulation simulation)
    {
        if (slot.CallbackPhase == 0)
        {
            if (slot.Distance - simulation.LogicalDistance > 3)
            {
                return;
            }
            short delay = slot.I16(0x84);
            slot.Write16(0x84, unchecked((short)(delay - 1)));
            if (delay != 0)
            {
                slot.Write16(0x8A, -1);
                return;
            }
            slot.Write16(0x8A, 0);
            slot.Write16(0x14, 160);
            slot.Write32(0x6E, 32768);
            slot.Write16(0x84, unchecked((short)slot.I32(0x48)));
            slot.Write16(0x88, 0);
            slot.Write32(0x26, slot.I32(0x3C));
            slot.Write32(0x2A, unchecked(slot.I32(0x40) + ((simulation.Rider.NextRandom() & 15) << 16)));
            slot.Write32(0x2E, slot.I32(0x44));
            slot.CallbackPhase = 1;
            return;
        }
        short remaining = slot.I16(0x84);
        slot.Write16(0x84, unchecked((short)(remaining - 1)));
        if (remaining == 0)
        {
            if (slot.I16(0x86) != 0)
            {
                slot.Active = false;
            }
            else
            {
                slot.Write16(0x14, 0);
                slot.Write16(0x84, (short)((simulation.Rider.NextRandom() & 31) + 8));
                slot.CallbackPhase = 0;
                slot.Write16(0x8A, -1);
            }
            return;
        }
        if (remaining < 16)
        {
            slot.Write16(0x8A, unchecked((short)(slot.I16(0x8A) + 1)));
        }
        if (slot.Bytes[1] != 0)
        {
            slot.Bytes[1] = 0;
            slot.Write16(0x14, 0);
            slot.Write16(0x86, 1);
            Sound?.Invoke(new(2, 240, 0));
        }
        slot.Write32(0x26, unchecked(slot.I32(0x26) + slot.I32(0x6A)));
        slot.Write32(0x2E, unchecked(slot.I32(0x2E) + slot.I32(0x72)));
        slot.Write32(0x2A, unchecked(slot.I32(0x2A) + slot.I32(0x6E)));
        slot.Write32(0x6E, unchecked(slot.I32(0x6E) + 4096));
        slot.Write16(0x22, unchecked((short)(slot.I16(0x22) + (unchecked(-slot.I32(0x6A)) >> 11))));
        slot.Write16(0x24, 0);
        if (slot.I32(0x2A) > slot.I32(0x32))
        {
            slot.Write32(0x2A, slot.I32(0x32));
            slot.Write32(0x6E, unchecked(-slot.I32(0x6E)) >> 1);
            short bounce = slot.I16(0x88);
            slot.Write16(0x88, unchecked((short)(bounce + 1)));
            if (bounce < 3)
            {
                BounceSound(unchecked((short)((7 - (slot.Distance - simulation.LogicalDistance)) << 4)));
            }
        }
    }

    private void BounceSound(short volume)
    {
        if (volume < lastBounceVolume && nativeClock - lastBounceClock <= 10)
        {
            return;
        }
        lastBounceClock = unchecked((short)nativeClock);
        lastBounceVolume = volume;
        Sound?.Invoke(new(4, 427, volume));
    }

    private static void RetireAfter(Slot slot, short counter, int delay)
    {
        if (counter <= delay)
        {
            return;
        }
        ushort fade = slot.U16(0x8A);
        slot.Write16(0x8A, unchecked((short)(fade + 1)));
        if (fade > 30)
        {
            slot.Active = false;
        }
    }

    internal static short BendingPitch(short counter) => unchecked((short)((-Sine(Sine(unchecked(counter << 5)) >> 4)) >> 3));
    internal static short TopplingPitchDelta(short counter) => unchecked((short)(Sine(counter >> 1) >> 2));

    internal static ushort? ClearanceReaction(int producerDistance, int logicalDistance, int triangle, ushort actionWord, float floorHeightQ12, float positionYQ12)
    {
        if (producerDistance != logicalDistance || triangle is < 24 or > 47 || (actionWord & 255) is 10 or 12)
        {
            return null;
        }
        int clearance = (int)(((double)floorHeightQ12 - positionYQ12) / 4096d);
        if (clearance is >= 80 and < 448)
        {
            return 12;
        }
        if (clearance is >= 448 and < 456)
        {
            return 10;
        }
        return null;
    }

    internal static bool PlaneCollision(int producerDistance, int logicalDistance, int triangle, ushort actionWord, float floorHeightQ12, float positionYQ12, int triangleBase, IReadOnlyList<short> thresholds)
    {
        if (producerDistance != logicalDistance || (actionWord & 255) == 11)
        {
            return false;
        }
        short index = unchecked((short)((triangle - triangleBase) >> 1));
        if (index < 0)
        {
            return false;
        }
        if (index >= thresholds.Count)
        {
            throw new InvalidDataException("The snowboard plane collision references an invalid native triangle threshold.");
        }
        int clearance = (int)((double)floorHeightQ12 - positionYQ12) >> 12;
        return clearance > thresholds[index];
    }

    internal static float PlaneReactionVelocity(float velocity)
    {
        int amount = (int)velocity;
        if (amount < 0)
        {
            amount = unchecked(-amount);
        }
        return Math.Max(amount, 262144);
    }

    private static int Sine(int angle)
    {
        float degrees = angle * 0.087890625f;
        float radians = (float)(degrees * 0.017453292519944444d);
        return (int)(Math.Sin(radians) * 4096d);
    }

    internal static Matrix4x4 BuildTerrainTransform(short courseAngle, Vector3 nativeCoursePosition, Vector3 nativeBasePosition)
    {
        float radians = -courseAngle * 0.0015339808305725455f;
        float sine = (float)Math.Sin(radians);
        float cosine = (float)Math.Cos(radians);
        return new(cosine, 0, sine, 0, 0, 1, 0, 0, -sine, 0, cosine, 0, unchecked((int)nativeCoursePosition.X - (int)nativeBasePosition.X), unchecked((int)nativeCoursePosition.Y - (int)nativeBasePosition.Y), unchecked((int)nativeCoursePosition.Z - (int)nativeBasePosition.Z), 1);
    }

    internal static Matrix4x4 BuildObjectTransform(Ff7SnowboardObjectFrame frame, Vector3 nativeBasePosition)
    {
        (float sx, float cx) = Trigonometry(frame.Pitch);
        (float sy, float cy) = Trigonometry(frame.Heading);
        (float sz, float cz) = Trigonometry(frame.Roll);
        double[] x = [1, 0, 0, 0, cx, sx, 0, -sx, cx];
        double[] y = [cy, 0, -sy, 0, 1, 0, sy, 0, cy];
        double[] z = [cz, sz, 0, -sz, cz, 0, 0, 0, 1];
        double[] rotation = Multiply(Multiply(y, x), z);
        float[] coefficients = new float[9];
        for (int index = 0; index < coefficients.Length; index++)
        {
            short quantized = unchecked((short)(int)(rotation[index] * 4096d));
            short scaled = unchecked((short)(quantized << frame.ScaleShift));
            coefficients[index] = scaled / 4096f;
        }
        Vector3 position = frame.NativePosition ?? new Vector3((int)frame.PositionQ12.X >> 12, (int)frame.PositionQ12.Y >> 12, (int)frame.PositionQ12.Z >> 12);
        return new(coefficients[0], coefficients[1], coefficients[2], 0, coefficients[3], coefficients[4], coefficients[5], 0, coefficients[6], coefficients[7], coefficients[8], 0, unchecked((int)position.X - (int)nativeBasePosition.X), unchecked((int)position.Y - (int)nativeBasePosition.Y), unchecked((int)position.Z - (int)nativeBasePosition.Z), 1);
    }

    private static (float Sine, float Cosine) Trigonometry(short angle)
    {
        float degrees = angle * 0.087890625f;
        double radians = degrees * 0.017453292519944444d;
        return ((float)Math.Sin((float)radians), (float)Math.Cos(radians));
    }

    private static double[] Multiply(double[] left, double[] right)
    {
        double[] result = new double[9];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                result[row * 3 + column] = (float)(left[row * 3] * right[column] + left[row * 3 + 1] * right[3 + column] + left[row * 3 + 2] * right[6 + column]);
            }
        }
        return result;
    }

    private void UpdateImpact(Slot slot, Ff7SnowboardSimulation simulation)
    {
        if (slot.CallbackPhase == 1)
        {
            if (slot.I32(0x2A) > slot.I32(0x32))
            {
                slot.Active = false;
                return;
            }
            slot.Distance = unchecked((short)simulation.LogicalDistance);
            slot.Write32(0x26, unchecked(slot.I32(0x26) + slot.I32(0x6A)));
            slot.Write32(0x2A, unchecked(slot.I32(0x2A) + slot.I32(0x6E)));
            slot.Write32(0x2E, unchecked(slot.I32(0x2E) + slot.I32(0x72)));
            slot.Write32(0x6E, unchecked(slot.I32(0x6E) + 4096));
            slot.Write16(0x1E, unchecked((short)(slot.I16(0x1E) - 32)));
            slot.Write16(0x24, 0);
            if (slot.I32(0x6E) > 0)
            {
                slot.Write16(0x8A, unchecked((short)(slot.I16(0x8A) + 1)));
            }
            return;
        }
        if (slot.CallbackPhase == 2)
        {
            short counter = slot.I16(0x84);
            slot.Write16(0x84, unchecked((short)(counter + 1)));
            if (counter > 60)
            {
                ushort fade = slot.U16(0x8A);
                slot.Write16(0x8A, unchecked((short)(fade + 1)));
                if (fade > 30)
                {
                    slot.Active = false;
                }
            }
            return;
        }
        if (slot.Bytes[1] == 0)
        {
            if (slot.Jumping)
            {
                short counter = slot.I16(0x84);
                slot.Write16(0x84, unchecked((short)(counter - 1)));
                if (counter < 0)
                {
                    slot.Write32(0x6E, -32768);
                    slot.Write16(0x84, (short)((simulation.Rider.NextRandom() & 31) + 32));
                }
                slot.Write32(0x2A, unchecked(slot.I32(0x2A) + slot.I32(0x6E)));
                slot.Write32(0x6E, unchecked(slot.I32(0x6E) + 4096));
                if (slot.I32(0x2A) > slot.I32(0x32))
                {
                    slot.Write32(0x2A, slot.I32(0x32));
                    slot.Write32(0x6E, 0);
                }
            }
            return;
        }
        lastImpactDistance = unchecked((short)slot.Distance);
        if ((slot.Bytes[1] & 2) != 0)
        {
            slot.Write32(0x6A, slot.I32(0x7C));
            slot.Write32(0x6E, -163840);
            slot.Write32(0x72, slot.I32(0x80));
            Sound?.Invoke(new(3, 240, 0));
            slot.Write16(0x14, 0);
            slot.CallbackPhase = 1;
        }
        else
        {
            slot.CallbackPhase = 2;
        }
    }

    private static void Contact(Slot slot, Ff7SnowboardSimulation simulation)
    {
        Ff7SnowboardRiderController rider = simulation.Rider;
        int action = rider.Script.ActionWord & 255;
        if (slot.Distance != simulation.LogicalDistance || (action is 10 or 12 or 14 && rider.ActionTick < 8))
        {
            return;
        }
        Vector3 position = rider.Motion.Position;
        int x = (int)position.X >> 12;
        int y = (int)position.Y >> 12;
        int z = (int)position.Z >> 12;
        int? row = ContactRow(x, y, z, slot.I32(0x26) >> 12, slot.I32(0x2A) >> 12, slot.I32(0x2E) >> 12, slot.I16(0x14), slot.I16(0x16), slot.I16(0x18));
        if (row is null)
        {
            return;
        }
        slot.Bytes[1] |= 1;
        if (slot.Bytes[0x1A] == 255)
        {
            return;
        }
        simulation.RestorePreviousMotion();
        int responseRow = row.Value;
        if (rider.Motion.Speed > 256)
        {
            responseRow += 2;
            slot.Bytes[1] |= 2;
            slot.Write32(0x7C, (int)rider.Motion.Velocity.X);
            slot.Write32(0x80, (int)rider.Motion.Velocity.Z);
        }
        int response = slot.Bytes[0x1A + responseRow];
        Vector3 velocity = rider.Motion.Velocity;
        if (response == 0)
        {
            rider.Motion.Velocity = new(0, velocity.Y, 0);
            return;
        }
        if (response == 128)
        {
            response = action == 11 || rider.Motion.Speed <= 640 ? 12 : 8;
        }
        if (response == 8)
        {
            if (simulation.Navigation.CurrentTriangle % 12 >= 6)
            {
                response++;
            }
            rider.Motion.Velocity = new(velocity.X - velocity.X * 0.5f, velocity.Y, velocity.Z - velocity.Z * 0.5f);
        }
        rider.SetAction((ushort)response);
    }

    internal static int? ContactRow(int x, int y, int z, int objectX, int objectY, int objectZ, short radius, short height, short splitHeight)
    {
        if (radius == 0 || y < objectY - height || y > objectY + 256)
        {
            return null;
        }
        int dx = Math.Abs(x - objectX);
        int dz = Math.Abs(z - objectZ);
        if (dx > radius || dz > radius || (int)Math.Sqrt(unchecked(dx * dx + dz * dz)) > radius)
        {
            return null;
        }
        return y < objectY - splitHeight ? 1 : 0;
    }

    private sealed class Slot
    {
        internal readonly byte[] Bytes = new byte[0x90];
        internal int Distance;
        internal readonly Ff7SnowboardObjectProducerKind Kind;
        internal readonly bool Jumping;
        internal bool Active = true;
        internal int CallbackPhase;

        internal Slot(int distance, Ff7SnowboardObjectProducer producer)
        {
            Distance = distance;
            Kind = producer.Kind;
            Jumping = producer.Kind == Ff7SnowboardObjectProducerKind.ImpactObstacle && producer.Variant == 2;
            producer.Template.CopyTo(Bytes, 4);
            Write16(0x20, producer.Heading);
            Write32(0x26, producer.X);
            Write32(0x2A, unchecked(producer.Y - (Kind == Ff7SnowboardObjectProducerKind.Balloon ? 0x80000 : 0)));
            Write32(0x2E, producer.Z);
            if (Kind == Ff7SnowboardObjectProducerKind.Balloon)
            {
                Write32(0x3C, producer.Variant);
                Write32(0x10, producer.Variant);
            }
            if (Kind == Ff7SnowboardObjectProducerKind.Obstacle)
            {
                Write16(4, (short)(237 + producer.Variant));
            }
            if (Kind == Ff7SnowboardObjectProducerKind.ImpactObstacle)
            {
                Write16(4, (short)(234 + producer.Variant));
                Write32(0x32, producer.Y);
            }
            if (Kind == Ff7SnowboardObjectProducerKind.BendingObstacle)
            {
                Write16(4, (short)(234 + producer.Variant));
            }
            if (Kind == Ff7SnowboardObjectProducerKind.TopplingObstacle)
            {
                Write16(4, (short)(230 + producer.Variant));
            }
            if (Kind == Ff7SnowboardObjectProducerKind.RetiringObstacle)
            {
                Write16(4, (short)(257 + producer.Variant));
            }
            if (producer.Bounce is { } bounce)
            {
                Write32(0x3C, bounce.X);
                Write32(0x40, bounce.Y);
                Write32(0x44, bounce.Z);
                Write32(0x32, bounce.FloorY);
                Write32(0x48, bounce.ActiveTicks);
                Write32(0x6A, bounce.HorizontalVelocity);
                Write32(0x72, bounce.HorizontalVelocity);
                Write16(0x8A, -1);
            }
        }

        internal short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan(offset));
        internal ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan(offset));
        internal int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan(offset));
        internal void Write16(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(Bytes.AsSpan(offset), value);
        internal void Write32(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(Bytes.AsSpan(offset), value);
    }
}
