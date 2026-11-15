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

    internal Ff7ChocoboSimulation(PcChocoboCourse course, Ff7ChocoboNativeData native, ReadOnlySpan<byte> parameters, uint seed)
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
        int playerModel = jockey ? parameters[6] + 6 : 10;
        Racers = Enumerable.Range(0, 6).Select(index => new Racer { Lane = S(index * 224 / 6 + 16), Jockey = S(index), Model = S(index == 0 ? playerModel : index + 10), SoundCounter = S(index == 0 ? 30 : 0) }).ToArray();
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
            Racer racer = Racers[index];
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
            racer.CanSprint = -1;
            racer.OriginalLane = racer.Lane;
            racer.Position = racer.Origin = Sample(10, racer.Lane);
            racer.Target = Sample(11, racer.Lane);
            racer.AnimationStep = S(racer.Speed / animationDivisor);
            racer.Yaw = racer.TargetYaw = Direction(racer.Target.X - racer.Position.X, racer.Target.Y - racer.Position.Y, racer.Target.Z - racer.Position.Z);
        }
        if (specialOpponent)
        {
            Racer previous = Racers[1];
            Racer rival = Racers[0] with { Name = Racers[0].Name.ToArray(), Position = previous.Position, Origin = previous.Position, Target = previous.Target };
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
            Racer racer = Racers[index];
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

    internal Racer[] Racers { get; }
    internal uint RandomSeed => random;

    private int NextRandom()
    {
        random = unchecked(random * 214013 + 2531011);
        return (int)(random >> 16) & 0x7FFF;
    }

    private (short X, short Y, short Z) Sample(int index, int lane)
    {
        PcChocoboTrackElement section = course.Track[index];
        return (S((section.Left.X * lane + section.Right.X * (256 - lane)) / 256), S((section.Left.Y * lane + section.Right.Y * (256 - lane)) / 256), S((section.Left.Z * lane + section.Right.Z * (256 - lane)) / 256));
    }

    private short Direction(int x, int y, int z)
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

    internal sealed record Racer
    {
        public short Path { get; set; }
        public short NextPath { get; set; }
        public short Speed { get; set; }
        public short Type { get; set; }
        public short PreviousY { get; set; }
        public (short X, short Y, short Z) Position { get; set; }
        public int RemainderX { get; set; }
        public int RemainderZ { get; set; }
        public (short X, short Y, short Z) Target { get; set; }
        public short Yaw { get; set; }
        public short TargetYaw { get; set; }
        public (short X, short Y, short Z) Origin { get; set; }
        public short Dash { get; set; }
        public short SmoothedLane { get; set; }
        public short Behavior { get; set; }
        public short DesiredSpeed { get; set; }
        public short LaneBias { get; set; }
        public short Guide { get; set; }
        public short EffectiveSpeed { get; set; }
        public short CruiseSpeed { get; set; }
        public short TopSpeed { get; set; }
        public short Acceleration { get; set; }
        public short HandlingStep { get; set; }
        public short SpeedResponse { get; set; }
        public short Automatic { get; set; }
        public short Obedience { get; set; }
        public short Handling { get; set; }
        public short LaneStep { get; set; }
        public int Stamina { get; set; }
        public int MaxStamina { get; set; }
        public short Placement { get; set; }
        public short AnimationCounter { get; set; }
        public short SecondaryAnimationCounter { get; set; }
        public short AnimationStep { get; set; }
        public short PreviousAnimation { get; set; }
        public short Lane { get; set; }
        public short Finished { get; set; }
        public short Animation { get; set; }
        public short Rank { get; set; }
        public short Stagnation { get; set; }
        public short TerrainMask { get; set; }
        public byte[] Name { get; set; } = new byte[8];
        public short Model { get; set; }
        public short Jockey { get; set; }
        public short SoundCounter { get; set; }
        public short OriginalLane { get; set; }
        public short LastSprint { get; set; }
        public short CanSprint { get; set; }
        public short UnusedAbility { get; set; }
        public short UnusedFlag { get; set; }
        public short EarlySprint { get; set; }
        public short FixedSpeed { get; set; }
    }
}
