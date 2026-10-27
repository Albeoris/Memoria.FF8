using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSnipeLaser302Controller
{
    private static (NativeBattleSnipeLaser302Frame Frame, int Result) AdvanceRoot(NativeBattleSnipeLaser302Frame frame, uint address, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(frame.State.ReadDword(ArenaGlobal) + (alternate ? 0x1710u : 0x11710u)));
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
                frame = InitializeSecondaryBanks(frame);
            }
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            byte ordinal = Read(frame, address).ReadByte(14);
            if (ordinal <= Header(frame, packets).ReadByte(17))
            {
                int first = Header(frame, packets, ordinal).FirstContactIndex ?? throw new NotSupportedException("Snipe Laser requires its selected header's physical contact.");
                uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
                for (int index = 0; index < 2; index++)
                {
                    (frame, uint main) = Append(frame, 0x25076B8, 0x66CD40);
                    if (main == 0)
                    {
                        continue;
                    }
                    frame = ClearDwords701200(frame, main + 12, 406);
                    frame = Write(frame, main, Read(frame, main).WriteWord(12, unchecked((ushort)(-index * 2))).WriteWord(14, checked((ushort)index)).WriteDword(16, target));
                    NativeBattleSnipeLaser302PointStep anchor = inputs.ReadBone502170(frame, target, 0xF1, 0);
                    frame = WriteBytes(anchor.Frame, main + 20, anchor.Point8, 6);
                    root = Read(frame, address);
                    frame = Write(frame, address, root.WriteByte(14, unchecked((byte)(root.ReadByte(14) + 1))));
                    int[] offsets = [28, 972, 1204];
                    int[] lengths = [944, 232, 432];
                    NativeBattleGfAnimationSource[] sources = [resources.ProgrammeA, resources.ProgrammeB, resources.ProgrammeC];
                    for (int programme = 0; programme < offsets.Length; programme++)
                    {
                        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(TaskView(Read(frame, main), offsets[programme], lengths[programme]), 0, lengths[programme]);
                        workspace = workspace.Initialize7016B0(sources[programme], lengths[programme]);
                        frame = WriteBytes(frame, main + (uint)offsets[programme], workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), lengths[programme]);
                    }
                }
            }
        }
        int mains = 1;
        int sprites = 1;
        int particles = 1;
        int programmes = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, mains) = AdvancePool(frame, 4, resources, inputs);
            (frame, sprites) = AdvancePool(frame, 3, resources, inputs);
            (frame, particles) = AdvancePool(frame, 2, resources, inputs);
            (frame, programmes) = AdvancePool(frame, 1, resources, inputs);
        }
        if (Read(frame, address).ReadWord(12) == 20)
        {
            frame = inputs.Execute(frame, new NativeBattleSnipeLaser302Sound501330(resources.Sound, 0, 128));
        }
        root = Read(frame, address);
        if (root.ReadByte(15) != 0 && mains == 0 && sprites == 0 && particles == 0 && programmes == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleSnipeLaser302Frame Frame, int Result) AdvanceMain(NativeBattleSnipeLaser302Frame frame, uint address, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age < 0)
        {
            return (Write(frame, address, task.WriteWord(12, unchecked((ushort)(age + 1)))), 0);
        }
        int clone = Signed(task, 14);
        int pointOffset = checked(clone * 8);
        if (pointOffset < 0 || pointOffset > resources.SourcePoints16.Length - 8)
        {
            throw new InvalidDataException("Snipe Laser clone leaves its actual source point table.");
        }
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, resources.SourcePoints16.AsSpan().Slice(pointOffset, 8));
        NativeBattleSnipeLaser302PointStep muzzle = ReadMuzzle66D880(frame, source, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), inputs);
        frame = muzzle.Frame;
        NativeBattleSnipeLaser302PointStep targetAnchor = inputs.ReadAnchor571400(frame, task.ReadDword(16));
        frame = targetAnchor.Frame;
        NativeBattleEffectArena target = targetAnchor.Point8;
        NativeBattleVector p = ReadPoint302(muzzle.Point8);
        NativeBattleVector q = ReadPoint302(target);
        NativeBattleActionMatrix horizontal = NativeBattleFunguarLaser277.Basis50CBA0(q.X - p.X, 0, p.Z - q.Z).Matrix;
        short[] reversed = horizontal.Rotation.ToArray();
        for (int lane = 0; lane < 9; lane++)
        {
            if (lane < 3 || lane >= 6)
            {
                reversed[lane] = unchecked((short)-reversed[lane]);
            }
        }
        NativeBattleActionMatrix reverse = horizontal with { Rotation = reversed.ToImmutableArray() };
        if ((uint)(age - 5) < 40)
        {
            NativeBattleSnipeLaser302PointStep transformed = TransformPoint66D810(frame, NativeBattlePackedMatrix32.FromMatrix(horizontal, null), muzzle.Point8, PointView(new(0, 0, 0)), inputs);
            frame = transformed.Frame;
            int phase = age - 15;
            int widthAngle;
            int cue;
            NativeBattleModelTransform targetTransform = inputs.ReadModel(frame, task.ReadDword(16)).Transform ?? throw new NotSupportedException("Snipe Laser requires its current recipient fields.");
            int scale = targetTransform.Scale38;
            int yOffset;
            if ((uint)phase < 20)
            {
                widthAngle = 4096 - NativeBattleCameraMath.Cosine(unchecked(phase << 10) / 20);
                cue = 0;
                yOffset = unchecked(scale * 2 - (unchecked(scale * widthAngle) >> 10));
            }
            else if ((uint)phase < 40)
            {
                cue = unchecked((phase - 20) << 9);
                widthAngle = unchecked(896 - (phase << 5));
                yOffset = unchecked(scale * 2 - (unchecked(unchecked(scale * (phase + 44)) << 2) >> 6));
            }
            else
            {
                widthAngle = 0;
                cue = 0;
                yOffset = unchecked(scale * 2);
            }
            q = q with { Y = unchecked((short)(q.Y + yOffset)) };
            NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(q.X - p.X, p.Y - q.Y, p.Z - q.Z).Matrix;
            NativeBattleVector origin = ReadPoint302(transformed.Point8);
            basis = basis with { X = origin.X, Y = origin.Y, Z = origin.Z };
            NativeBattleEffectArena caller = Caller302(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), basis), 0);
            frame = AdvanceWorkspace302(frame, address, 972, 232, caller, resources, inputs);
            if ((uint)phase < 40)
            {
                if (q.Y >= 0)
                {
                    int divisor = q.Y - p.Y;
                    q = new(unchecked((short)(unchecked((p.X - q.X) * p.Y) / divisor + p.X)), 0, unchecked((short)(unchecked((p.Z - q.Z) * p.Y) / divisor + p.Z)));
                }
                var beam = NativeBattleFunguarLaser277.Basis50CBA0(q.X - p.X, p.Y - q.Y, p.Z - q.Z);
                int length = beam.Length;
                short minimum = inputs.ReadModel(frame, task.ReadDword(16)).MinimumY146 ?? throw new NotSupportedException("Snipe Laser requires its current recipient minimum Y.");
                if (q.Y < minimum)
                {
                    length = unchecked(length << 1);
                }
                else if (q.Y < 0)
                {
                    if ((q.Y >= minimum - (minimum >> 3) || clone == 0) && (age & 1) == 0)
                    {
                        (frame, uint sprite) = Append(frame, 0x25076A8, 0x66D790);
                        if (sprite != 0)
                        {
                            frame = WriteBytes(frame, sprite + 12, PointView(q), 6);
                            frame = Write(frame, sprite, Read(frame, sprite).WriteWord(18, 0));
                        }
                    }
                }
                else
                {
                    (frame, uint programme) = Append(frame, 0x2507688, 0x66D670);
                    if (programme != 0)
                    {
                        NativeBattleModulePoolTask born = Read(frame, programme);
                        for (int lane = 0; lane < 9; lane++)
                        {
                            born = born.WriteWord(12 + lane * 2, unchecked((ushort)reverse.Rotation[lane]));
                        }
                        born = born.WriteDword(32, unchecked((uint)(int)q.X)).WriteDword(36, unchecked((uint)(int)q.Y)).WriteDword(40, unchecked((uint)(int)q.Z));
                        frame = Write(frame, programme, born);
                        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(TaskView(born, 44, 124), 0, 124);
                        workspace = workspace.Initialize7016B0(resources.ProgrammeD, 124);
                        frame = WriteBytes(frame, programme + 44, workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), 124);
                    }
                }
                short width = unchecked((short)(NativeBattleCameraMath.Sine(widthAngle >> 1) >> 3));
                NativeBattleActionMatrix diagonal = new([width, 0, 0, 0, width, 0, 0, 0, unchecked((short)length)], 0, 0, 0, 4096);
                NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(beam.Matrix, diagonal) with { X = origin.X, Y = origin.Y, Z = origin.Z };
                (frame, uint temporary) = Allocate(frame, 88);
                NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(resources.Mesh.Address));
                if (cue == 0)
                {
                    context = context.Write(28, BitConverter.GetBytes(0x30u));
                }
                else
                {
                    context = context.Write(8, new byte[3]).Write(12, BitConverter.GetBytes(cue)).Write(28, BitConverter.GetBytes(0xF0u));
                }
                frame = PublishContext(frame, temporary, context, 88);
                frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), scaled), null));
                frame = inputs.Execute(frame, new NativeBattleSnipeLaser302DrawStatic88(temporary, resources.Mesh.Address));
                frame = Release(frame, 88);
            }
        }
        if (clone == 0)
        {
            if ((uint)(age - 33) < 24)
            {
                short minimum = inputs.ReadModel(frame, task.ReadDword(16)).MinimumY146 ?? throw new NotSupportedException("Snipe Laser requires its current recipient minimum Y.");
                NativeBattleActionMatrix world = reverse with { X = Signed(task, 20), Y = minimum / 2, Z = Signed(task, 24) };
                frame = AdvanceWorkspace302(frame, address, 1204, 432, Caller302(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world), -128), resources, inputs);
            }
            if ((uint)(age - 36) < 2)
            {
                frame = SpawnParticles302(frame, address);
            }
            if (age == 43)
            {
                NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Snipe Laser requires its current first physical contact.");
                frame = inputs.Execute(frame, new NativeBattleSnipeLaser302Contact506690(first));
            }
        }
        if (age >= 50)
        {
            return (frame, 2);
        }
        task = Read(frame, address);
        return (Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), 0);
    }

    private static NativeBattleSnipeLaser302Frame SpawnParticles302(NativeBattleSnipeLaser302Frame frame, uint main)
    {
        for (int index = 0; index < 8; index++)
        {
            (frame, uint particle) = Append(frame, 0x2507698, 0x66D6D0);
            if (particle == 0)
            {
                continue;
            }
            frame = WriteBytes(frame, particle + 12, TaskView(Read(frame, main), 20, 8), 8);
            NativeBattleModulePoolTask task = Read(frame, particle).WriteWord(18, 0);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = DrawRandom(frame, out ushort random);
                short velocity = unchecked((short)((random & 511) - 256));
                task = task.WriteWord(20 + axis * 2, unchecked((ushort)velocity)).WriteWord(28 + axis * 2, unchecked((ushort)(-velocity / 12)));
            }
            frame = Write(frame, particle, task);
        }
        return frame;
    }

    private static NativeBattleSnipeLaser302Frame AdvanceWorkspace302(NativeBattleSnipeLaser302Frame frame, uint task, int offset, int length, NativeBattleEffectArena caller, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace() => NativeBattleGfAnimationWorkspace.Borrow(ReadBytes(frame, task + (uint)offset, length), 0, length);
        NativeBattleGfAnimationSource source = resources.ReadProgramme(Workspace().ResourceAddress);
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, task + (uint)offset, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan().Slice(0, length)), length);
            frame = DrawProgrammeNode66D3C0(frame, source, node, caller, resources, inputs);
            return Workspace();
        });
        return WriteBytes(frame, task + (uint)offset, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan().Slice(0, length)), length);
    }

    private static NativeBattleEffectArena Caller302(NativeBattleActionMatrix matrix, int depth) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes(depth)).Write(36, BitConverter.GetBytes(ArenaAddress + 0x1588u));
    private static NativeBattleVector ReadPoint302(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));

    public static NativeBattleActionHeaderSlot Header(NativeBattleSnipeLaser302Frame frame, NativeBattleActionPacketsState packets, int ordinal = 0)
    {
        if (frame.State.ReadDword(ParametersGlobal) != 0x1D99A78)
        {
            throw new NotSupportedException("Snipe Laser requires its actual current parameter pointer.");
        }
        int index = checked(Parameters(frame).HeaderIndex7C + ordinal);
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Snipe Laser parameter header leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
