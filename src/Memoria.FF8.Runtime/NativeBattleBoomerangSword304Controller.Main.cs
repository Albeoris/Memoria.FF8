using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBoomerangSword304Controller
{
    private static (NativeBattleBoomerangSword304Frame Frame, int Result) AdvanceRoot(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleBoomerangSword304Resources resources, NativeBattleBoomerangSword304Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(frame.State.ReadDword(ArenaGlobal) + (alternate ? 0x16F0u : 0x116F0u)));
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, 0x25075B0, 0x669AF0);
            if (main != 0)
            {
                frame = ClearDwords701200(frame, main + 12, 40);
                NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                int count = Header(frame, packets).ReadByte(16);
                frame = Write(frame, main, Read(frame, main).WriteWord(14, checked((ushort)count)));
                for (int index = 0; index < count; index++)
                {
                    packets = inputs.ReadPackets(frame);
                    int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Boomerang Sword requires its current physical contacts.");
                    frame = Write(frame, main, Read(frame, main).WriteDword(32 + index * 4, ModelAddress(packets.ReadContact(first + index).Primary.ActorIndex)));
                }
                for (int left = 0; left < count - 1; left++)
                {
                    for (int right = left + 1; right < count; right++)
                    {
                        NativeBattleModulePoolTask task = Read(frame, main);
                        uint a = task.ReadDword(32 + left * 4);
                        uint b = task.ReadDword(32 + right * 4);
                        if (ModelTransform304(inputs.ReadModel(frame, a)).Position28.X < ModelTransform304(inputs.ReadModel(frame, b)).Position28.X)
                        {
                            frame = Write(frame, main, task.WriteDword(32 + left * 4, b).WriteDword(32 + right * 4, a));
                        }
                    }
                }
                frame = Write(frame, main, Read(frame, main).WriteWord(18, unchecked((ushort)-1000)).WriteWord(20, 1000).WriteWord(22, 0).WriteWord(24, 0));
                for (int index = 0; index < count; index++)
                {
                    uint target = Read(frame, main).ReadDword(32 + index * 4);
                    NativeBattleBoomerangSword304PointStep anchor = inputs.ReadAnchor571400(frame, target);
                    frame = WriteBytes(anchor.Frame, main + 44 + (uint)(index * 8), anchor.Point8, 8);
                    NativeBattleVector point = Point304(anchor.Point8);
                    NativeBattleModulePoolTask task = Read(frame, main);
                    task = task.WriteWord(22, unchecked((ushort)(task.ReadWord(22) + point.Y))).WriteWord(24, unchecked((ushort)(task.ReadWord(24) + point.Z)));
                    if (Signed(task, 18) > point.X)
                    {
                        task = task.WriteWord(18, unchecked((ushort)point.X));
                    }
                    if (Signed(task, 20) < point.X)
                    {
                        task = task.WriteWord(20, unchecked((ushort)point.X));
                    }
                    frame = Write(frame, main, task);
                }
                NativeBattleModulePoolTask current = Read(frame, main);
                int meanY = Signed(current, 22) / count;
                int meanZ = Signed(current, 24) / count;
                frame = Write(frame, main, current.WriteWord(18, unchecked((ushort)(current.ReadWord(18) - 500))).WriteWord(20, unchecked((ushort)(current.ReadWord(20) + 500))).WriteWord(22, unchecked((ushort)meanY)).WriteWord(24, unchecked((ushort)(meanZ - 1000))));
            }
        }
        int mains = 1;
        int trails = 1;
        int programmes = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, mains) = AdvancePool(frame, 1, resources, inputs);
            (frame, trails) = AdvancePool(frame, 2, resources, inputs);
            (frame, programmes) = AdvancePool(frame, 3, resources, inputs);
            (frame, _) = AdvancePool(frame, 4, resources, inputs);
        }
        if (Read(frame, address).ReadByte(15) != 0 && mains == 0 && trails == 0 && programmes == 0)
        {
            return (frame, 2);
        }
        root = Read(frame, address);
        return (Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleBoomerangSword304Frame Frame, int Result) AdvanceMain(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleBoomerangSword304Resources resources, NativeBattleBoomerangSword304Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        uint caster = frame.State.ReadDword(CasterGlobal);
        if (age == 4)
        {
            NativeBattleBoomerangSword304PointStep bone = inputs.ReadBone502170(frame, caster, 23, 0);
            frame = WriteBytes(bone.Frame, address + 148, bone.Point8, 6);
            frame = WriteBytes(frame, address + 156, TaskView(Read(frame, address), 148, 8), 8);
        }
        if (age == 5)
        {
            NativeBattleBoomerangSword304PointStep bone = inputs.ReadBone502170(frame, caster, 23, 0);
            frame = WriteBytes(bone.Frame, address + 140, bone.Point8, 6);
            frame = InitializePath304(frame, address);
        }
        int segment = 0;
        int parameter = 0;
        task = Read(frame, address);
        if ((uint)(age - 5) < 20)
        {
            parameter = unchecked(((age - 5) << 12) / 20 + 102);
            frame = Write(frame, address, task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + 51))));
        }
        else if ((uint)(age - 25) < 15)
        {
            int value = unchecked(((Signed(task, 14) + 1) * (age - 25)) << 12) / 15 + 136;
            segment = (value >> 12) + 1;
            parameter = value & 4095;
            frame = Write(frame, address, task.WriteWord(28, 1024));
        }
        else if ((uint)(age - 40) < 20)
        {
            segment = Signed(task, 14) + 2;
            parameter = unchecked(((age - 40) << 12) / 20 + 102);
            frame = Write(frame, address, task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) - 51))));
        }
        if ((uint)(age - 5) < 60)
        {
            NativeBattleBoomerangSword304PointStep trajectory = SamplePath304(frame, address, segment, parameter, inputs.Colors);
            frame = trajectory.Frame;
            NativeBattleVector point = Point304(trajectory.Point8);
            NativeBattleVector prior = Point304(TaskView(Read(frame, address), 148, 8));
            if (point != prior)
            {
                frame = WriteBytes(frame, address + 156, TaskView(Read(frame, address), 148, 8), 8);
            }
            frame = WriteBytes(frame, address + 148, trajectory.Point8, 8);
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(26, unchecked((ushort)(task.ReadWord(26) + 1024))));
            frame = inputs.Execute(frame, new NativeBattleBoomerangSword304ActorMeshMask124(caster, 2, 0));
            task = Read(frame, address);
            NativeBattleVector previous = Point304(TaskView(task, 156, 8));
            NativeBattleActionMatrix direction = NativeBattleFunguarLaser277.Basis50CBA0(previous.X - point.X, 0, point.Z - previous.Z).Matrix;
            NativeBattleActionMatrix spin = NativeBattlePandemona291Restoration.Rotation701220(Signed(task, 26));
            NativeBattleActionMatrix tilt = RotationZ304(Signed(task, 28));
            NativeBattleActionMatrix world = NativeBattleGfMatrices.Multiply56C090(direction, NativeBattleGfMatrices.Multiply56C090(tilt, spin));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(world, new(0, 0, 160));
            world = world with { X = unchecked(x + point.X), Y = unchecked(y + point.Y), Z = unchecked(z + point.Z) };
            (frame, uint temporary) = Allocate(frame, 88);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world), null));
            NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(resources.Mesh.Address)).Write(28, BitConverter.GetBytes(0u));
            frame = PublishContext(frame, temporary, context, 88);
            frame = inputs.Execute(frame, new NativeBattleBoomerangSword304DrawStatic88(temporary, resources.Mesh.Address));
            frame = Release(frame, 88);
            (frame, uint trail) = Append(frame, 0x25075A0, 0x66A730);
            if (trail != 0)
            {
                NativeBattleModulePoolTask born = Read(frame, trail).WriteWord(12, 0);
                for (int lane = 0; lane < 9; lane++)
                {
                    born = born.WriteWord(16 + lane * 2, unchecked((ushort)world.Rotation[lane]));
                }
                int time = age - 5;
                int fade = time < 16 ? time << 8 : time > 44 ? (60 - time) << 8 : 4096;
                born = born.WriteDword(36, unchecked((uint)world.X)).WriteDword(40, unchecked((uint)world.Y)).WriteDword(44, unchecked((uint)world.Z)).WriteWord(14, unchecked((ushort)fade));
                frame = Write(frame, trail, born);
            }
            task = Read(frame, address);
            int targetIndex = Signed(task, 16);
            if (age >= 25 && age < 40 && targetIndex < Signed(task, 14))
            {
                uint target = task.ReadDword(32 + targetIndex * 4);
                if (point.X < ModelTransform304(inputs.ReadModel(frame, target)).Position28.X)
                {
                    (frame, uint programme) = Append(frame, 0x2507590, 0x66A3F0);
                    frame = ApplyModelContacts66A2C0(frame, target, inputs);
                    if (programme != 0)
                    {
                        frame = Write(frame, programme, Read(frame, programme).WriteDword(12, target));
                        NativeBattleBoomerangSword304PointStep anchor = inputs.ReadAnchor571400(frame, target);
                        frame = WriteBytes(anchor.Frame, programme + 16, anchor.Point8, 8);
                        NativeBattleGfAnimationWorkspace workspace = new(TaskView(Read(frame, programme), 24, 460).Read(0, 460).ToImmutableArray());
                        workspace = workspace.Initialize7016B0(resources.Programme, 460);
                        frame = WriteBytes(frame, programme + 24, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), 460);
                    }
                    frame = SpawnParticles304(frame, PointView(point, 0), direction);
                    task = Read(frame, address);
                    frame = Write(frame, address, task.WriteWord(16, unchecked((ushort)(task.ReadWord(16) + 1))));
                }
            }
        }
        if (age == 5)
        {
            frame = inputs.Execute(frame, new NativeBattleBoomerangSword304Sound501330(resources.Sound, 0, 128));
        }
        task = Read(frame, address);
        short next = unchecked((short)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        return (frame, next >= 59 ? 2 : 0);
    }

    private static NativeBattleBoomerangSword304Frame InitializePath304(NativeBattleBoomerangSword304Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena start = TaskView(task, 140, 8);
        frame = WriteBytes(frame, address + 68, start, 8);
        frame = WriteBytes(frame, address + 76, start, 8);
        task = Read(frame, address);
        frame = WritePathPoint304(frame, address + 84, new(Signed(task, 20), unchecked((short)(Signed(task, 22) - 1000)), Signed(task, 24)));
        int count = Signed(task, 14);
        for (int index = 0; index < count; index++)
        {
            frame = WriteBytes(frame, address + 92 + (uint)(index * 8), TaskView(Read(frame, address), 44 + index * 8, 8), 8);
        }
        task = Read(frame, address);
        frame = WritePathPoint304(frame, address + 92 + (uint)(count * 8), new(Signed(task, 18), Signed(task, 22), Signed(task, 24)));
        NativeBattleVector end = new(unchecked((short)(Signed(task, 18) - 1000)), unchecked((short)(Signed(task, 22) - 3000)), Signed(task, 24));
        frame = WritePathPoint304(frame, address + 100 + (uint)(count * 8), end);
        return WritePathPoint304(frame, address + 108 + (uint)(count * 8), end);
    }

    public static NativeBattleBoomerangSword304PointStep SamplePath304(NativeBattleBoomerangSword304Frame frame, uint address, int segment, int parameter, NativeBattleSummonColors colors)
    {
        NativeBattleEffectArena a = TaskView(Read(frame, address), 68 + segment * 8, 8);
        NativeBattleEffectArena b = TaskView(Read(frame, address), 76 + segment * 8, 8);
        NativeBattleEffectArena c = TaskView(Read(frame, address), 84 + segment * 8, 8);
        NativeBattleEffectArena d = TaskView(Read(frame, address), 92 + segment * 8, 8);
        (frame, int length) = Distance304(frame, Point304(b), Point304(c));
        NativeBattleVector delta = Difference304(Point304(b), Point304(a));
        (frame, int firstLength) = Distance304(frame, delta, new(0, 0, 0));
        NativeBattleEffectArena first = b;
        if (firstLength > 0)
        {
            first = AddTangent304(b, delta, length, firstLength);
            NativeBattleWeightedPointsStep firstBlend = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Point304(b), Point304(first), unchecked((short)(4096 - parameter)), unchecked((short)parameter), colors);
            frame = frame with { Memory = firstBlend.Memory };
            first = WithPoint304(first, firstBlend.Position);
        }
        delta = Difference304(Point304(c), Point304(d));
        (frame, int secondLength) = Distance304(frame, delta, new(0, 0, 0));
        NativeBattleEffectArena second = c;
        if (secondLength > 0)
        {
            second = AddTangent304(c, delta, length, secondLength);
            NativeBattleWeightedPointsStep secondBlend = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Point304(c), Point304(second), unchecked((short)parameter), unchecked((short)(4096 - parameter)), colors);
            frame = frame with { Memory = secondBlend.Memory };
            second = WithPoint304(second, secondBlend.Position);
        }
        NativeBattleWeightedPointsStep weighted = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Point304(first), Point304(second), unchecked((short)(4096 - parameter)), unchecked((short)parameter), colors);
        return new(frame with { Memory = weighted.Memory }, PointView(weighted.Position));
    }

    private static (NativeBattleBoomerangSword304Frame Frame, int Length) Distance304(NativeBattleBoomerangSword304Frame frame, NativeBattleVector first, NativeBattleVector second)
    {
        NativeBattleVector delta = Difference304(first, second);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)delta.X), unchecked((ushort)delta.Y), unchecked((ushort)delta.Z));
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value));
        return (frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, length);
    }

    private static NativeBattleEffectArena AddTangent304(NativeBattleEffectArena origin, NativeBattleVector delta, int length, int divisor)
    {
        NativeBattleVector point = Point304(origin);
        return PointView(new(unchecked((short)(unchecked(delta.X * length) / divisor + point.X)), unchecked((short)(unchecked(delta.Y * length) / divisor + point.Y)), unchecked((short)(unchecked(delta.Z * length) / divisor + point.Z))));
    }

    public static NativeBattleBoomerangSword304Frame ApplyModelContacts66A2C0(NativeBattleBoomerangSword304Frame frame, uint modelAddress, NativeBattleBoomerangSword304Inputs inputs)
    {
        uint delta = unchecked(modelAddress - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= 7)
        {
            throw new InvalidDataException("Boomerang Sword contact matcher requires a physical graphics model.");
        }
        int actor = checked((int)(delta / 156));
        for (int index = 0; index < Header(frame, inputs.ReadPackets(frame)).ReadByte(16); index++)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Boomerang Sword requires its current physical contact base.");
            int physical = checked(first + index);
            if (packets.ReadContact(physical).Primary.ActorIndex == actor)
            {
                frame = inputs.Execute(frame, new NativeBattleBoomerangSword304Contact506690(physical));
            }
        }
        return frame;
    }

    private static NativeBattleBoomerangSword304Frame SpawnParticles304(NativeBattleBoomerangSword304Frame frame, NativeBattleEffectArena point, NativeBattleActionMatrix direction)
    {
        int angle = 0;
        for (int index = 0; index < 21; index++)
        {
            (frame, uint particle) = Append(frame, 0x2507580, 0x66A330);
            if (particle == 0)
            {
                continue;
            }
            frame = WriteBytes(frame, particle + 12, point, 8);
            frame = DrawRandom(frame, out ushort radiusRandom);
            int radius = (radiusRandom & 63) + 64;
            short x = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) / 4096));
            short y = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) / 4096));
            frame = DrawRandom(frame, out ushort depthRandom);
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(direction, new(x, y, unchecked((short)((depthRandom & 63) + 64))));
            NativeBattleModulePoolTask task = Read(frame, particle);
            short[] values = [velocity.X, velocity.Y, velocity.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteWord(20 + axis * 2, unchecked((ushort)values[axis])).WriteWord(28 + axis * 2, unchecked((ushort)(-values[axis] / 12)));
            }
            frame = Write(frame, particle, task);
            angle = unchecked(angle + 195);
        }
        return frame;
    }

    private static NativeBattleActionMatrix RotationZ304(int angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        return new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
    }

    private static NativeBattleVector Difference304(NativeBattleVector a, NativeBattleVector b) => new(unchecked((short)(a.X - b.X)), unchecked((short)(a.Y - b.Y)), unchecked((short)(a.Z - b.Z)));
    private static NativeBattleModelTransform ModelTransform304(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Boomerang Sword requires its actual model transform.");
    private static NativeBattleVector Point304(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
    private static NativeBattleEffectArena WithPoint304(NativeBattleEffectArena retained, NativeBattleVector point) => retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    private static NativeBattleBoomerangSword304Frame WritePathPoint304(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleVector point) => WriteBytes(frame, address, WithPoint304(ReadBytes(frame, address, 8), point), 8);

    public static NativeBattleActionHeaderSlot Header(NativeBattleBoomerangSword304Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(ParametersGlobal) != 0x1D99A78)
        {
            throw new NotSupportedException("Boomerang Sword requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Boomerang Sword parameter header leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
