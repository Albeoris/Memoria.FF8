using static Memoria.FF8.Runtime.NativeBattleEffect323Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect323Controller
{
    private const int PhysicalControlCount = PhysicalRecipientCount + 7;

    public static NativeBattleEffect323Step AdvanceMain659DD0(NativeBattleEffect323Frame frame, uint address, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        BattleModelOwner caster = frame.State.ReadModelAddress(CasterGlobal);
        if (age == 11)
        {
            NativeBattleEffect323PointStep point = inputs.Point502170(frame, caster, 24, 2048, TaskView(Read(frame, address), 0xA4, 8));
            frame = Write(point.Frame, address, CopyTask(CopyTask(Read(point.Frame, address), 0xA4, point.Output8, 8), 0xAC, point.Output8, 8).WriteWord(0x1C, 1024));
        }
        if (age == 12)
        {
            NativeBattleEffect323PointStep point = inputs.Point502170(frame, caster, 24, 2048, TaskView(Read(frame, address), 0x9C, 8));
            frame = Write(point.Frame, address, CopyTask(Read(point.Frame, address), 0x9C, point.Output8, 8));
            frame = InitializeControls(frame, address);
        }
        if (age is >= 12 and <= 63)
        {
            int count = Signed(Read(frame, address), 14);
            (int segment, int parameter) = PathParameter(age, count);
            frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)(Read(frame, address).ReadWord(0x1C) + 77))));
            NativeBattleEffect323PointStep sampled = inputs.SampleTrajectory(frame, ReadControls(frame, address, count), segment, parameter, UnknownPoint());
            frame = sampled.Frame;
            NativeBattleEffectArena previous = TaskView(Read(frame, address), 0xA4, 8);
            NativeBattleVector old = Vector(previous);
            NativeBattleVector current = Vector(sampled.Output8);
            if (old != current)
            {
                frame = Write(frame, address, CopyTask(Read(frame, address), 0xAC, previous, 8));
            }
            frame = Write(frame, address, CopyTask(Read(frame, address), 0xA4, sampled.Output8, 8).WriteWord(0x1A, unchecked((ushort)(Read(frame, address).ReadWord(0x1A) + 1024))));
            frame = inputs.Execute(frame, new NativeBattleEffect323MeshMask(caster, 0xFFFFFFFD, 0));
            NativeBattleVector history = Vector(TaskView(Read(frame, address), 0xAC, 8));
            NativeBattleActionMatrix world = BuildWorld(history, current, Signed(Read(frame, address), 0x1A), Signed(Read(frame, address), 0x1C));
            frame = DrawMainMesh(frame, address, world, resources, inputs);
            frame = BirthMesh(frame, age, world);
            frame = TriggerRecipient(frame, address, age, sampled.Output8, resources, inputs);
        }
        if (age == 12)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect323Sound501330(SoundAddress, 0, 128));
        }
        if (age == 29)
        {
            NativeBattleEffect323PointStep point = inputs.Point502170(frame, caster, 24, 2048, UnknownPoint());
            frame = point.Frame;
            int count = Signed(Read(frame, address), 14);
            int offset = (count + 5) * 8;
            NativeBattleEffectArena controls = ReadControls(frame, address, count);
            controls = CopyBytes(controls, 0, NativeBattleEffectArena.CreateUnknown(controls.Bound), 0, controls.Bound);
            controls = CopyBytes(point.Output8, 0, controls, offset, 8);
            controls = CopyBytes(point.Output8, 0, controls, offset + 8, 8);
            controls.WriteUInt16(offset + 12, unchecked((ushort)(controls.ReadUInt16(offset + 12) - 1000)));
            frame = PublishControls(frame, address, controls);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        if (age + 1 >= 64)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect323MeshMask(caster, uint.MaxValue, 2));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    public static (int Segment, int Parameter) PathParameter(int age, int count)
    {
        RequireRecipientCount(count);
        if (age is < 12 or > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }
        if (age < 27)
        {
            return (0, ((age - 12) << 12) / 15 + 136);
        }
        if (age < 35)
        {
            return (1, (age - 27) * 512 + 256);
        }
        if (age < 45)
        {
            int value = count * (age - 35) * 4096 / 10 + 204;
            return ((value >> 12) + 2, value & 4095);
        }
        int tail = (age - 45) * 8192 / 20 + 204;
        return (count + (tail >> 12) + 2, tail & 4095);
    }

    public static NativeBattleEffect323Frame InitializeControls(NativeBattleEffect323Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int count = Signed(task, 14);
        RequireRecipientCount(count);
        NativeBattleEffectArena caster = TaskView(task, 0x9C, 8);
        NativeBattleVector point = Vector(caster);
        int physicalLength = Math.Min(count + 7, PhysicalControlCount) * 8;
        NativeBattleEffectArena controls = NativeBattleEffectArena.CreateUnknown((count + 7) * 8).Write(physicalLength, new byte[(count + 7) * 8 - physicalLength]);
        controls = CopyBytes(TaskView(task, 0x44, physicalLength), 0, controls, 0, physicalLength);
        controls = CopyBytes(caster, 0, controls, 0, 8);
        controls = CopyBytes(caster, 0, controls, 8, 8);
        controls.WriteUInt16(4, unchecked((ushort)(controls.ReadUInt16(4) - 500)));
        controls = CopyBytes(PointView(new(Signed(task, 0x14), unchecked((short)(Signed(task, 0x16) - 1000)), Signed(task, 0x18))), 0, controls, 16, 6);
        for (int index = 0; index < count; index++)
        {
            NativeBattleEffectArena anchors = index < PhysicalRecipientCount ? TaskView(task, 0x2C + index * 8, 8) : frame.State.ExtendedAnchors ?? throw new InvalidDataException("Effect323 lost its expanded anchor owner.");
            int sourceOffset = index < PhysicalRecipientCount ? 0 : (index - PhysicalRecipientCount) * 8;
            controls = CopyBytes(anchors, sourceOffset, controls, (index + 3) * 8, 8);
        }
        int offset = (count + 3) * 8;
        controls = CopyBytes(PointView(new(Signed(task, 0x12), Signed(task, 0x16), Signed(task, 0x18))), 0, controls, offset, 6);
        NativeBattleVector midpoint = new(unchecked((short)((point.X + Signed(task, 0x12)) / 2)), unchecked((short)(Signed(task, 0x16) - 1500)), unchecked((short)((point.Z + Signed(task, 0x18)) / 2)));
        controls = CopyBytes(PointView(midpoint), 0, controls, offset + 8, 6);
        controls = CopyBytes(caster, 0, controls, offset + 16, 8);
        controls = CopyBytes(caster, 0, controls, offset + 24, 8);
        return PublishControls(frame, address, controls);
    }

    private static NativeBattleEffectArena ReadControls(NativeBattleEffect323Frame frame, uint address, int count)
    {
        int length = (count + 7) * 8;
        int physicalLength = Math.Min(length, PhysicalControlCount * 8);
        NativeBattleEffectArena physical = TaskView(Read(frame, address), 0x44, physicalLength);
        if (length == physicalLength)
        {
            return physical;
        }
        NativeBattleEffectArena extended = frame.State.ExtendedPath ?? throw new InvalidDataException("Effect323 lost its expanded path owner.");
        NativeBattleEffectArena combined = CopyBytes(physical, 0, NativeBattleEffectArena.CreateUnknown(length), 0, physicalLength);
        return CopyBytes(extended, 0, combined, physicalLength, length - physicalLength);
    }

    private static NativeBattleEffect323Frame PublishControls(NativeBattleEffect323Frame frame, uint address, NativeBattleEffectArena controls)
    {
        int physicalLength = Math.Min(controls.Bound, PhysicalControlCount * 8);
        frame = Write(frame, address, CopyTask(Read(frame, address), 0x44, controls, physicalLength));
        if (controls.Bound > physicalLength)
        {
            int extraLength = controls.Bound - physicalLength;
            NativeBattleEffectArena suffix = CopyBytes(controls, physicalLength, NativeBattleEffectArena.CreateUnknown(extraLength), 0, extraLength);
            frame = frame with { State = frame.State with { ExtendedPath = suffix } };
        }
        return frame;
    }

    public static NativeBattleActionMatrix BuildWorld(NativeBattleVector history, NativeBattleVector current, short spin, short tilt)
    {
        NativeBattleActionMatrix x = NativeBattlePandemona291Restoration.Rotation701220(spin);
        short sine = unchecked((short)NativeBattleCameraMath.Sine(tilt));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(tilt));
        NativeBattleActionMatrix z = new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(history.X - current.X, 0, current.Z - history.Z).Matrix;
        return NativeBattleGfMatrices.Multiply56C090(basis, NativeBattleGfMatrices.Multiply56C090(z, x)) with { X = current.X, Y = current.Y, Z = current.Z };
    }

    private static NativeBattleEffect323Frame DrawMainMesh(NativeBattleEffect323Frame frame, uint taskAddress, NativeBattleActionMatrix world, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleActionMatrix viewWorld = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(viewWorld, null));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xFE13ECu)).Write(0x1C, BitConverter.GetBytes(0u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = NativeBattleEffect323Lower.DrawMesh572200(frame, temporary, resources, inputs, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(323, Read(frame, taskAddress)) with { ResourceGeneration = NativeBattleEffect323Lower.Dword323(context, 0) } : null);
        return Release(frame, 88);
    }

    public static int MeshStrength(int age)
    {
        int relative = age - 12;
        return relative < 16 ? relative << 8 : relative <= 49 ? 4096 : (65 - relative) << 8;
    }

    private static NativeBattleEffect323Frame BirthMesh(NativeBattleEffect323Frame frame, int age, NativeBattleActionMatrix world)
    {
        (frame, uint task) = Append(frame, MeshDescriptor, 0x65AA80);
        if (task == 0)
        {
            return frame;
        }
        NativeBattleEffectArena packed = PackedArena(NativeBattlePackedMatrix32.FromMatrix(world, null));
        frame = WriteBytes(frame, task + 0x10, packed, 18);
        frame = WriteBytes(frame, task + 0x24, CopyBytes(packed, 20, NativeBattleEffectArena.Clear(), 0, 12), 12);
        return Write(frame, task, Read(frame, task).WriteWord(12, 0).WriteWord(14, unchecked((ushort)MeshStrength(age))));
    }

    public static NativeBattleEffect323Frame TriggerRecipient(NativeBattleEffect323Frame frame, uint main, int age, NativeBattleEffectArena sampled, NativeBattleEffect323Resources resources, NativeBattleEffect323Inputs inputs)
    {
        if (age is < 35 or > 45)
        {
            return frame;
        }
        NativeBattleModulePoolTask task = Read(frame, main);
        int count = Signed(task, 14);
        bool extended = count > PhysicalRecipientCount;
        int next = Signed(task, 0x10);
        if ((!extended && age == 45) || next >= count)
        {
            return frame;
        }
        if (next < 0)
        {
            throw new NotSupportedException("Effect323 requires its native nonnegative recipient cursor.");
        }
        short x = Vector(sampled).X;
        while (next < count)
        {
            BattleModelOwner target = next < PhysicalRecipientCount ? task.ReadExecutionReference<BattleExecutionReference.Model>(0x20 + next * 4, 4).Owner : frame.State.ExtendedRecipients[next - PhysicalRecipientCount];
            if (x >= ModelX(frame, target, inputs))
            {
                break;
            }
            (frame, uint programme) = Append(frame, ProgrammeDescriptor, 0x65A720);
            frame = NativeBattleEffect323Lower.AllMatchingContacts65A5F0(frame, target, inputs);
            if (programme != 0)
            {
                NativeBattleModulePoolTask programmeTask = Read(frame, programme);
                programmeTask.WriteExecutionReference(12, 4, new BattleExecutionReference.Model(target), inputs.ReadPackets(frame).ModelAliases.EncodeAddress(target));
                frame = Write(frame, programme, programmeTask);
                NativeBattleEffect323PointStep anchor = inputs.Anchor571400(frame, target, TaskView(Read(frame, programme), 0x10, 8));
                frame = Write(anchor.Frame, programme, CopyTask(Read(anchor.Frame, programme), 0x10, anchor.Output8, 8));
                frame = NativeBattleEffect323Programme.Initialize(frame, programme + WorkspaceOffset, ProgrammeAddress, WorkspaceLength, resources);
            }
            frame = BirthParticles(frame, main, sampled);
            next++;
            task = Read(frame, main).WriteWord(0x10, checked((ushort)next));
            frame = Write(frame, main, task);
            if (!extended)
            {
                break;
            }
        }
        return frame;
    }

    public static NativeBattleEffect323Frame BirthParticles(NativeBattleEffect323Frame frame, uint main, NativeBattleEffectArena sampled)
    {
        NativeBattleEffectArena point = sampled.Write(6, BitConverter.GetBytes((short)0));
        NativeBattleVector current = Vector(point);
        NativeBattleVector previous = Vector(TaskView(Read(frame, main), 0xAC, 8));
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(previous.X - current.X, 0, current.Z - previous.Z).Matrix;
        int yaw = 0;
        for (int index = 0; index < 21; index++)
        {
            (frame, uint particle) = Append(frame, ParticleDescriptor, 0x65A660);
            if (particle == 0)
            {
                continue;
            }
            frame = frame with { Random = frame.Random.Draw(out ushort radiusRandom) };
            int radius = (radiusRandom & 63) + 64;
            frame = frame with { Random = frame.Random.Draw(out ushort depthRandom) };
            NativeBattleVector local = new(unchecked((short)(NativeBattleCameraMath.Sine(yaw) * radius / 4096)), unchecked((short)(NativeBattleCameraMath.Cosine(yaw) * radius / 4096)), (short)((depthRandom & 63) + 64));
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(basis, local);
            NativeBattleModulePoolTask value = CopyTask(Read(frame, particle), 12, point, 8);
            value = CopyTask(value, 0x14, PutPoint(NativeBattleEffectNodeContextMemory.Copy(TaskView(value, 0x14, 8), 0, NativeBattleEffectArena.CreateUnknown(8), 0, 8), 0, velocity), 6);
            value = value.WriteWord(0x1C, unchecked((ushort)(-velocity.X / 12))).WriteWord(0x1E, unchecked((ushort)(-velocity.Y / 12))).WriteWord(0x20, unchecked((ushort)(-velocity.Z / 12)));
            frame = Write(frame, particle, value);
            yaw += 195;
        }
        return frame;
    }

    private static NativeBattleEffectArena UnknownPoint() => NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
}
