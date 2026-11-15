using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

internal sealed partial class Ff7ChocoboSimulation
{
    private readonly PcChocoboCourse course;
    private readonly Ff7ChocoboNativeData native;
    private readonly bool jockey;
    private readonly bool specialOpponent;
    private readonly int animationDivisor;
    private uint random;

    internal Ff7ChocoboSimulation(PcChocoboCourse course, Ff7ChocoboNativeData native, ReadOnlySpan<byte> parameters, uint seed, Ff7ChocoboSimulationState? saved = null, Ff7ChocoboRacerState[]? previous = null)
    {
        if (parameters.Length != 29 || parameters[23] >= native.Classes.Count)
        {
            throw new InvalidDataException("FFVII Chocobo entry parameters require their complete native layout and a supported class.");
        }
        this.course = course;
        this.native = native;
        jockey = parameters[8] != 0 || parameters[9] != 0;
        specialOpponent = parameters[22] != 0;
        animationDivisor = (parameters[23] + 3) * 2;
        random = seed;
        if (saved is not null)
        {
            saved.Validate(course.Track.Count);
            Racers = saved.Racers.Select(racer => racer with { Name = racer.Name.ToArray() }).ToArray();
            random = saved.RandomSeed;
            FinishTimer = saved.FinishTimer;
            FirstFinisher = saved.FirstFinisher;
            SecondFinished = saved.SecondFinished;
            lastRacer = saved.LastRacer;
            consumedCameraEvents.UnionWith(saved.ConsumedCameraEvents);
            return;
        }
        int playerModel = jockey ? parameters[6] + 6 : 10;
        Racers = new Ff7ChocoboRacerState[6];
        for (int index = 0; index < Racers.Length; index++)
        {
            Ff7ChocoboRacerState racer = previous is null ? new() : previous[index] with { Name = previous[index].Name.ToArray() };
            racer.Lane = S(index * 224 / 6 + 16);
            racer.Jockey = S(index);
            racer.Model = S(index == 0 ? playerModel : index + 10);
            if (index == 0)
            {
                racer.SoundCounter = 30;
            }
            Racers[index] = racer;
        }
        for (int swap = 0; swap < 40; swap++)
        {
            int first = NextRandom() % 6;
            int second = NextRandom() % 6;
            (Racers[first].Lane, Racers[second].Lane) = (Racers[second].Lane, Racers[first].Lane);
        }
        int lastSprint = parameters[7] == 0 ? 0x3DE : 0x320;
        Ff7ChocoboClass rank = native.Classes[parameters[23]];
        for (int index = 0; index < Racers.Length; index++)
        {
            Ff7ChocoboRacerState racer = Racers[index];
            if (jockey && index == 0)
            {
                racer.TopSpeed = BinaryPrimitives.ReadInt16LittleEndian(parameters[10..]);
                racer.CruiseSpeed = BinaryPrimitives.ReadInt16LittleEndian(parameters[12..]);
                racer.SpeedResponse = parameters[14];
                racer.Obedience = parameters[15];
                racer.Type = S(NextRandom() % 4);
                racer.Stamina = racer.MaxStamina = BinaryPrimitives.ReadUInt16LittleEndian(parameters[26..]);
                racer.Behavior = parameters[18];
                racer.TerrainMask = S(parameters[17] switch { 1 => 1, 2 => 2, 3 or 4 => 0, _ => 3 });
                racer.Handling = parameters[16];
            }
            else
            {
                racer.TopSpeed = S(rank.TopSpeed + NextRandom() % rank.TopSpeedRange);
                racer.CruiseSpeed = S(rank.CruiseSpeed + NextRandom() % rank.CruiseSpeedRange);
                racer.SpeedResponse = 50;
                racer.Obedience = 100;
                racer.Type = S(NextRandom() % 4);
                racer.Stamina = racer.MaxStamina = rank.TopSpeed + NextRandom() % 300;
                racer.Behavior = S((NextRandom() & 7) != 0 ? 0 : 2);
                racer.TerrainMask = 3;
                racer.Handling = S((NextRandom() & 1) * 50 + 50);
            }
            racer.Automatic = 1;
            (racer.Guide, racer.LaneStep) = racer.Handling switch
            {
                < 40 => (S(3), S((40 - racer.Handling) / 10)),
                < 60 => (S(2), S((60 - racer.Handling) / 5)),
                < 80 => (S(1), S((80 - racer.Handling) / 5)),
                _ => (S(0), S((110 - racer.Handling) / 5))
            };
            racer.HandlingStep = S(racer.Handling / 8);
            racer.LastSprint = S(lastSprint);
            racer.Rank = S(index);
            racer.Speed = S(racer.CruiseSpeed / 2);
            racer.DesiredSpeed = racer.CruiseSpeed;
            racer.Path = 10;
            racer.NextPath = 11;
            racer.Stagnation = 0;
            racer.CanSprint = -1;
            racer.EarlySprint = 0;
            racer.UnusedFlag = 0;
            racer.UnusedAbility = 0;
            racer.FixedSpeed = 0;
            racer.OriginalLane = racer.Lane;
            racer.Position = racer.Origin = Sample(10, racer.Lane);
            racer.Target = Sample(11, racer.Lane);
            racer.AnimationStep = S(racer.Speed / animationDivisor);
            racer.Placement = 0;
            racer.AnimationCounter = 0;
            racer.Finished = 0;
            racer.Animation = 0;
            racer.Yaw = racer.TargetYaw = Direction(racer.Target.X - racer.Position.X, racer.Target.Y - racer.Position.Y, racer.Target.Z - racer.Position.Z);
        }
        if (specialOpponent)
        {
            Ff7ChocoboRacerState originalRival = Racers[1];
            Ff7ChocoboRacerState rival = Racers[0] with { Name = Racers[0].Name.ToArray(), Position = originalRival.Position, Origin = originalRival.Position, Target = originalRival.Target };
            rival.MaxStamina += rival.MaxStamina / 4;
            rival.Stamina += rival.Stamina / 4;
            rival.CruiseSpeed = S(rival.CruiseSpeed + rival.CruiseSpeed / 10);
            rival.TopSpeed = S(rival.TopSpeed + rival.TopSpeed / 10);
            rival.SpeedResponse = rival.Handling = 100;
            rival.Model = 9;
            rival.Type = 3;
            rival.TerrainMask = 0;
            rival.Jockey = 1;
            Racers[1] = rival;
        }
        int[] names = Enumerable.Range(0, 45).ToArray();
        for (int swap = 0; swap < 200; swap++)
        {
            int first = NextRandom() % names.Length;
            int second = NextRandom() % names.Length;
            (names[first], names[second]) = (names[second], names[first]);
        }
        for (int index = 0; index < Racers.Length; index++)
        {
            Ff7ChocoboRacerState racer = Racers[index];
            if (specialOpponent && index == 1)
            {
                racer.Name = native.SpecialName.ToArray();
            }
            else
            {
                native.Names[names[index]].AsSpan(0, 6).CopyTo(racer.Name);
                racer.Name[6] = 255;
            }
        }
        if (jockey)
        {
            parameters[..6].CopyTo(Racers[0].Name);
            Racers[0].Name[6] = 255;
        }
    }

    internal Ff7ChocoboRacerState[] Racers { get; }
    internal PcChocoboCourse Course => course;
    internal uint RandomSeed => random;

    internal Ff7ChocoboSimulationState Capture()
    {
        return new(Racers.Select(racer => racer with { Name = racer.Name.ToArray() }).ToArray(), random, FinishTimer, FirstFinisher, SecondFinished, lastRacer, consumedCameraEvents.Order().ToArray());
    }

    private int NextRandom()
    {
        random = unchecked(random * 214013 + 2531011);
        return (int)(random >> 16) & 0x7FFF;
    }

    internal Ff7ChocoboPoint Sample(int index, int lane)
    {
        PcChocoboTrackElement section = course.Track[index];
        return (S((section.Left.X * lane + section.Right.X * (256 - lane)) / 256), S((section.Left.Y * lane + section.Right.Y * (256 - lane)) / 256), S((section.Left.Z * lane + section.Right.Z * (256 - lane)) / 256));
    }

    internal short Direction(int x, int y, int z)
    {
        float squared = (float)((double)x * x + (double)y * y + (double)z * z);
        float length = (float)Math.Sqrt(squared);
        float scale = length > 0 ? (float)(4096.0 / length) : 0;
        short nx = S((int)((double)x * scale));
        short nz = S((int)((double)z * scale));
        if (nx > 4095)
        {
            return 0x400;
        }
        if (nx < -4095)
        {
            return 0xC00;
        }
        int offset = (nx + 4096) & 0x1FFF;
        return S((native.Angles[nz > 0 ? offset : 8192 - offset] + (nz > 0 ? 0x400 : 0xC00)) & 0xFFF);
    }

    private static short S(int value) => unchecked((short)value);

}
