using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One process-retained63E9C0 camera bank. Starting a program resets only its countdown.</summary>
public sealed record NativeBattleGuardianCameraBank([property: JsonRequired] ushort Opcode, [property: JsonRequired] ushort Remaining, [property: JsonRequired] int Accumulator, [property: JsonRequired] int Step, [property: JsonRequired] NativeBattleVector Current, [property: JsonRequired] NativeBattleVector Origin, [property: JsonRequired] NativeBattleVector Delta, [property: JsonRequired] short Curve)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleVector>? ControlPoints { get; init; }
}

public sealed record NativeBattleGuardianCameraProcess([property: JsonRequired] NativeBattleGuardianCameraBank Eye, [property: JsonRequired] NativeBattleGuardianCameraBank Target, [property: JsonRequired] int Sign, [property: JsonRequired] NativeBattleCameraPose? PreviousPose);

public sealed record NativeBattleGuardianCameraTask([property: JsonRequired] NativeBattleGuardianCameraProcess Process, [property: JsonRequired] ImmutableArray<byte> Program, [property: JsonRequired] int Offset, [property: JsonRequired] ushort Frame, [property: JsonRequired] bool Complete);

public sealed record NativeBattleGuardianCameraStep(NativeBattleGuardianCameraTask Task, NativeBattleCameraPose Pose, int Result, bool Published)
{
    public NativeBattleEffectArena? Temporary432 { get; init; }
}

/// <summary>63E960/63E9C0's reached paired camera-program contracts over explicit retained owners.</summary>
public static partial class NativeBattleGuardianCameraProgram
{
    public static NativeBattleGuardianCameraProcess FromColdProcess()
    {
        NativeBattleGuardianCameraBank bank = new(0, 0, 0, 0, new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), 0);
        return new(bank, bank, 0, new(new(0, 0, 0), new(0, 0, 0), null, 0));
    }

    public static NativeBattleGuardianCameraTask Begin63E960(NativeBattleGuardianCameraProcess process, ImmutableArray<byte> program, int randomDivisor, Func<int>? random = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        RequireBanks(process);
        if (program.IsDefaultOrEmpty || (program.Length & 1) != 0)
        {
            throw new InvalidDataException("Native63E960 requires its original WORD program bytes.");
        }
        int sign = 1;
        if (randomDivisor != 0)
        {
            int value = (random ?? throw new NotSupportedException("Native63E960 requires its actual55CBD2 random owner."))();
            if (value < 0)
            {
                throw new InvalidDataException("Native55CBD2 requires its nonnegative CRT random result.");
            }
            sign = value % randomDivisor == 1 ? -1 : 1;
        }
        return new(process with { Sign = sign }, program, 0, 0, false);
    }

    public static NativeBattleGuardianCameraStep Advance63E9C0(NativeBattleGuardianCameraTask task, NativeBattleActionMatrix matrix, NativeBattleCameraPose pose, bool paused, ImmutableArray<short> scalarVariables = default, NativeBattleEffectArena? temporary432 = null)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(pose);
        RequireBanks(task.Process);
        matrix.Validate();
        if (task.Complete || task.Process.Sign is not (-1 or 1) || task.Program.IsDefaultOrEmpty || task.Offset < 0 || task.Offset > task.Program.Length || (task.Offset & 1) != 0)
        {
            throw new InvalidDataException("Native63E9C0 requires its live task, original program and retained sign.");
        }
        NativeBattleEffectArena? temporary = temporary432;
        NativeBattleGuardianCameraProcess process = task.Process;
        int offset = task.Offset;
        short? focal = pose.FocalLength;
        short roll = pose.Roll;
        if (!paused)
        {
            if (task.Frame == 0)
            {
                process = process with { Eye = process.Eye with { Remaining = 0 }, Target = process.Target with { Remaining = 0 } };
            }
            while (process.Eye.Remaining == 0 || process.Target.Remaining == 0)
            {
                ushort command = ReadWord();
                bool eye = (command & 0x40) != 0;
                ushort opcode = (ushort)(command & 0xFF3F);
                NativeBattleGuardianCameraBank selected = eye ? process.Eye : process.Target;
                NativeBattleGuardianCameraBank other = eye ? process.Target : process.Eye;
                selected = selected with { Opcode = opcode };
                if (opcode > 10)
                {
                    process = eye ? process with { Eye = selected } : process with { Target = selected };
                    return new(task with { Process = process, Offset = offset, Complete = true }, pose with { FocalLength = focal, Roll = roll }, 2, false) { Temporary432 = temporary };
                }
                switch (opcode)
                {
                    case 0:
                        selected = selected with { Remaining = unchecked((ushort)ReadScalar()) };
                        break;
                    case 1:
                        selected = selected with { Current = ReadVector() };
                        break;
                    case 2:
                    {
                        short duration = ReadScalar();
                        NativeBattleVector destination = ReadVector();
                        temporary = TemporaryPoint(temporary, 0, destination);
                        selected = selected with { Remaining = unchecked((ushort)duration), Delta = DivideDifference(destination, selected.Current, duration) };
                        break;
                    }
                    case 3:
                    {
                        short duration = ReadScalar();
                        short curve = unchecked((short)ReadWord());
                        NativeBattleVector destination = ReadVector();
                        temporary = TemporaryPoint(temporary, 0, destination);
                        selected = selected with { Remaining = unchecked((ushort)duration), Curve = curve, Accumulator = 0, Step = Divide(4096, duration), Origin = selected.Current, Delta = Subtract(destination, selected.Current) };
                        break;
                    }
                    case 4:
                    {
                        short duration = ReadScalar();
                        ushort descriptor = ReadWord();
                        int count = descriptor & 15;
                        ImmutableArray<NativeBattleVector>.Builder points = ImmutableArray.CreateBuilder<NativeBattleVector>(count + 1);
                        points.Add(selected.Current);
                        for (int index = 0; index < count; index++)
                        {
                            points.Add(ReadVector());
                        }
                        selected = selected with { Remaining = unchecked((ushort)duration), Curve = unchecked((short)(descriptor & 0xFFF0)), Accumulator = 0, Step = Divide(4096, duration), ControlPoints = points.MoveToImmutable() };
                        break;
                    }
                    case 5:
                    {
                        NativeBattleVector difference = Subtract(other.Current, selected.Current);
                        temporary = TemporaryPoint(temporary, 0, difference);
                        int distance = Distance(difference);
                        short duration = ReadScalar();
                        NativeBattleVector destination = ReadVector();
                        temporary = TemporaryPoint(temporary, 0, destination);
                        NativeBattleVector future = other.Current;
                        if (other.Opcode == 2)
                        {
                            future = Add(other.Current, ScaleWords(other.Delta, duration));
                        }
                        else if (other.Opcode == 3)
                        {
                            future = Add(other.Origin, other.Delta);
                        }
                        else if (other.Opcode == 4)
                        {
                            future = Points(other)[^1];
                        }
                        temporary = TemporaryPoint(temporary, 8, future)?.MarkUnknown(14, 2);
                        NativeBattleVector endDifference = Subtract(destination, future);
                        temporary = TemporaryPoint(temporary, 0, endDifference);
                        int endDistance = Distance(endDifference);
                        selected = selected with { Remaining = unchecked((ushort)duration), Origin = selected.Current, Delta = DivideDifference(destination, selected.Current, duration), Accumulator = distance, Step = Divide(unchecked(endDistance - distance), duration) };
                        break;
                    }
                    case 6:
                        focal = unchecked((short)ReadWord());
                        break;
                    case 8:
                        roll = unchecked((short)(ReadWord() * process.Sign));
                        break;
                    default:
                        throw new NotSupportedException($"Native63E9C0 opcode{opcode} requires its original reached contract.");
                }
                process = eye ? process with { Eye = selected } : process with { Target = selected };
            }
            process = process with { Eye = Update(process.Eye, process.Target, ref temporary) };
            process = process with { Target = Update(process.Target, process.Eye, ref temporary) };
        }
        NativeBattleCameraPose published = new(World(process.Eye.Current, 0), World(process.Target.Current, 8), focal, roll);
        process = process with { PreviousPose = pose };
        return new(task with { Process = process, Offset = offset, Frame = paused ? task.Frame : unchecked((ushort)(task.Frame + 1)) }, published, 0, true) { Temporary432 = temporary };

        ushort ReadWord()
        {
            if (offset > task.Program.Length - 2)
            {
                throw new InvalidDataException("Native63E9C0 program access exceeds its supplied original resource.");
            }
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(task.Program.AsSpan().Slice(offset, 2));
            offset += 2;
            return value;
        }

        short ReadScalar()
        {
            short value = unchecked((short)ReadWord());
            if (value >= 0)
            {
                return value;
            }
            int index = -value - 1;
            if (scalarVariables.IsDefault || index >= scalarVariables.Length)
            {
                throw new NotSupportedException("Native63F1E0 requires its actual retained24FBF48 signed-WORD variable bank.");
            }
            return scalarVariables[index];
        }

        NativeBattleVector ReadVector()
        {
            short x = unchecked((short)(ReadWord() * process.Sign));
            short y = unchecked((short)ReadWord());
            short z = unchecked((short)ReadWord());
            return new(x, y, z);
        }

        NativeBattleCameraPoint World(NativeBattleVector local, int destination)
        {
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, local);
            NativeBattleVector world = new(unchecked((short)(rotated.X + (short)matrix.X)), unchecked((short)(rotated.Y + (short)matrix.Y)), unchecked((short)(rotated.Z + (short)matrix.Z)));
            temporary = TemporaryPoint(temporary, destination, world);
            return new(world.X, world.Y, world.Z);
        }
    }

    private static NativeBattleGuardianCameraBank Update(NativeBattleGuardianCameraBank bank, NativeBattleGuardianCameraBank other, ref NativeBattleEffectArena? temporary)
    {
        switch (bank.Opcode)
        {
            case 2:
                bank = bank with { Current = Add(bank.Current, bank.Delta) };
                break;
            case 3:
            {
                int progress = unchecked(bank.Accumulator + bank.Step);
                int eased = Ease63F240(progress, bank.Curve);
                bank = bank with { Accumulator = progress, Current = Add(bank.Origin, ScaleQ12(bank.Delta, eased)) };
                break;
            }
            case 4:
            {
                int progress = unchecked(bank.Accumulator + bank.Step);
                int eased = Ease63F240(progress, bank.Curve);
                ImmutableArray<NativeBattleVector> points = Points(bank);
                NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
                for (int index = 0; index < points.Length; index++)
                {
                    byte[] bytes = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)points[index].X));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), unchecked((short)points[index].Y));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), unchecked((short)points[index].Z));
                    source = source.Write(index * 8, bytes);
                }
                NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(points.Length, source, 0, temporary is null ? NativeBattleEffectArena.Clear() : TemporarySlice(temporary, 16, 400));
                if (temporary is not null)
                {
                    temporary = TemporaryPublish(temporary, 16, work, 400);
                }
                NativeBattleEffectArena output = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(points.Length, work, eased, NativeBattleEffectArena.Clear(), 0);
                byte[] current = output.Read(0, 6);
                bank = bank with { Accumulator = progress, Current = new(BinaryPrimitives.ReadInt16LittleEndian(current), BinaryPrimitives.ReadInt16LittleEndian(current.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(current.AsSpan(4))) };
                break;
            }
            case 5:
            {
                NativeBattleVector origin = Add(bank.Origin, bank.Delta);
                int distance = unchecked(bank.Accumulator + bank.Step);
                int dx = other.Current.X - origin.X;
                int dy = other.Current.Y - origin.Y;
                int dz = other.Current.Z - origin.Z;
                temporary = TemporaryLongVector(temporary, 16, dx, dy, dz);
                NativeBattleVector direction = NativeBattlePoseMatrices.NormalizeLong56BD20(dx, dy, dz);
                temporary = TemporaryPoint(temporary, 0, direction);
                bank = bank with { Origin = origin, Accumulator = distance, Current = Subtract(other.Current, ScaleQ12(direction, distance)) };
                break;
            }
            case 7:
            case 9:
                throw new NotSupportedException($"Native63E9C0 retained opcode{bank.Opcode} requires its original animation contract.");
        }
        return bank with { Remaining = unchecked((ushort)(bank.Remaining - 1)) };
    }

    private static ImmutableArray<NativeBattleVector> Points(NativeBattleGuardianCameraBank bank)
    {
        return bank.ControlPoints is { IsDefaultOrEmpty: false } points ? points : throw new NotSupportedException("Native63E9C0 polynomial camera requires its actual retained control points.");
    }

    public static int Ease63F240(int progress, short curve)
    {
        int original = progress;
        int magnitude = Math.Abs((int)curve);
        if (curve < 0)
        {
            progress = unchecked(4096 - progress);
        }
        for (int iteration = 0; iteration < (magnitude >> 8); iteration++)
        {
            progress = NativeBattleCameraMath.Sine(progress / 4);
        }
        if (curve < 0)
        {
            progress = unchecked(4096 - progress);
        }
        if (curve == 0)
        {
            return progress;
        }
        int weight = (magnitude & 255) + 1;
        return unchecked((256 - weight) * original + weight * progress) >> 8;
    }

    private static NativeBattleVector Add(NativeBattleVector left, NativeBattleVector right) => new(unchecked((short)(left.X + right.X)), unchecked((short)(left.Y + right.Y)), unchecked((short)(left.Z + right.Z)));

    private static NativeBattleVector Subtract(NativeBattleVector left, NativeBattleVector right) => new(unchecked((short)(left.X - right.X)), unchecked((short)(left.Y - right.Y)), unchecked((short)(left.Z - right.Z)));

    private static NativeBattleVector ScaleWords(NativeBattleVector value, int scale) => new(unchecked((short)(value.X * scale)), unchecked((short)(value.Y * scale)), unchecked((short)(value.Z * scale)));

    private static NativeBattleVector ScaleQ12(NativeBattleVector value, int scale) => new(unchecked((short)(unchecked(value.X * scale) >> 12)), unchecked((short)(unchecked(value.Y * scale) >> 12)), unchecked((short)(unchecked(value.Z * scale) >> 12)));

    private static NativeBattleVector DivideDifference(NativeBattleVector destination, NativeBattleVector origin, short duration) => new(unchecked((short)Divide(destination.X - origin.X, duration)), unchecked((short)Divide(destination.Y - origin.Y, duration)), unchecked((short)Divide(destination.Z - origin.Z, duration)));

    private static int Divide(int value, int divisor)
    {
        if (divisor == 0)
        {
            throw new NotSupportedException("Native camera program reached a zero signed divisor.");
        }
        return value / divisor;
    }

    private static int Distance(NativeBattleVector value)
    {
        int square = unchecked(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
        if (square < 0)
        {
            throw new NotSupportedException("Native56BEC0 requires its original exceptional floating-point conversion for a negative squared distance.");
        }
        return (int)Math.Sqrt(square);
    }

    private static void RequireBanks(NativeBattleGuardianCameraProcess process)
    {
        if (process is null || process.Eye is null || process.Target is null)
        {
            throw new InvalidDataException("Native63E9C0 requires both explicit retained camera banks.");
        }
    }
}
