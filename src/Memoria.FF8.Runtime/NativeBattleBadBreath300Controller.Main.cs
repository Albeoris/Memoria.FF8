using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath300Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath300Controller
{
    public static ImmutableArray<byte> Normalize6B7CB0(ImmutableArray<byte> source)
    {
        NativeBattleBadBreath300Resources.ValidateMesh(source);
        byte[] bytes = source.ToArray();
        short minimum = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0x1C));
        short maximum = minimum;
        for (int index = 0; index < 164; index++)
        {
            short selected = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0x1C + index * 8));
            minimum = Math.Min(minimum, selected);
            maximum = Math.Max(maximum, selected);
        }
        int range = maximum - minimum;
        for (int index = 0; index < 165; index++)
        {
            int z = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(12 + index * 8));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), unchecked((short)((z << 12) / range)));
        }
        return bytes.ToImmutableArray();
    }

    public static NativeBattleBadBreath300Frame Deform66FBB0(NativeBattleBadBreath300Frame frame, uint sourceAddress, int phase, NativeBattleBadBreath300Resources resources)
    {
        return frame with { Memory = NativeBattleStripDeformation.Transform(frame.Memory, frame.State.ReadMesh(sourceAddress), phase, resources.TrigTable, ScratchOffset) };
    }

    private static (NativeBattleBadBreath300Frame Frame, int Result) AdvanceMain(NativeBattleBadBreath300Frame frame, uint address, NativeBattleBadBreath300Resources resources, NativeBattleBadBreath300Inputs inputs)
    {
        NativeBattleBadBreath300PointStep mouth = inputs.ReadMouth(frame);
        frame = mouth.Frame;
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x34, unchecked((ushort)mouth.Position.X)).WriteWord(0x36, unchecked((ushort)mouth.Position.Y)).WriteWord(0x38, unchecked((ushort)mouth.Position.Z));
        frame = Write(frame, address, task);
        int age = unchecked((short)task.ReadWord(12));
        int phase = age - 8;
        if ((uint)phase < 50)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 92);
            frame = frame with { Staging = allocation.Staging };
            frame = inputs.Execute(frame, new NativeBattleBadBreath300Clip(phase) { PilotIdentity = inputs.EmitNodePresentation is null ? null : NativeBattlePilotVisualLifetimes.Identity(300, Read(frame, address), 4) with { ResourceGeneration = 0 } });
            frame = LoadRotationZeroTranslation(frame, RotationZ7012C0(unchecked((short)task.ReadWord(0x2A))));
            frame = Deform66FBB0(frame, MeshAAddress, unchecked((short)task.ReadWord(0x2C)), resources);
            short sweep = unchecked((short)(NativeBattleCameraMath.Cosine(phase * 2048 / 50) * unchecked((short)task.ReadWord(0x24)) >> 12));
            int selected = unchecked((short)task.ReadWord(0x2E));
            if (selected < unchecked((short)task.ReadWord(14)))
            {
                int actor = Actor(task.ReadDword(0x10 + selected * 4));
                if (sweep - 300 <= Transform(inputs.ReadModel(actor)).Position28.X)
                {
                    (frame, uint childAddress) = Append(frame, 2, TargetCallback);
                    if (childAddress != 0)
                    {
                        NativeBattleBadBreath300AnchorStep anchor = inputs.ReadAnchor571400(frame, actor);
                        frame = anchor.Frame;
                        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
                        short y = unchecked((short)(Transform(model).Position28.Y + (model.MinimumY146 ?? throw new NotSupportedException("Bad Breath requires its actual target height.")) / 2));
                        NativeBattleModulePoolTask child = Read(frame, childAddress).WriteWord(12, 0).WriteByte(14, task.ReadByte(0x20 + selected)).WriteByte(15, (byte)actor).WriteWord(0x10, unchecked((ushort)anchor.Anchor.X)).WriteWord(0x12, unchecked((ushort)y)).WriteWord(0x14, unchecked((ushort)anchor.Anchor.Z)).WriteWord(0x16, unchecked((ushort)anchor.Anchor.W));
                        child = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(child, 0x18, NativeBattleGfAnimationWorkspace.Borrow(child, 0x18, 0x128).Initialize7016B0(resources.Target, 0x128));
                        frame = Write(frame, childAddress, child);
                    }
                    task = Read(frame, address).WriteWord(0x2E, unchecked((ushort)(selected + 1)));
                    frame = Write(frame, address, task);
                }
            }
            int dx = sweep - unchecked((short)task.ReadWord(0x34));
            int dy = unchecked((short)task.ReadWord(0x28)) - unchecked((short)task.ReadWord(0x36));
            int dz = unchecked((short)task.ReadWord(0x26)) - unchecked((short)task.ReadWord(0x38));
            int length = unchecked(NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dy * dy + dz * dz)) + 512);
            task = Read(frame, address).WriteDword(0x30, unchecked((uint)length));
            frame = Write(frame, address, task);
            short scaleZ = unchecked((short)length);
            if (phase < 16)
            {
                scaleZ = unchecked((short)(scaleZ * NativeBattleCameraMath.Sine(phase << 6) >> 12));
            }
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), allocation.Address, 92), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), 0, 92);
            NativeBattleEffectArena colorBefore = temporary;
            byte[] colorBytes = colorBefore.ReadSnapshot(4, 4).Data;
            uint prior = BinaryPrimitives.ReadUInt32LittleEndian(colorBytes);
            int gradient = (phase >> 2) % 3;
            int fraction = phase * 1024 & 4095;
            uint color = NativeBattleDuelSharedPackets.InterpolateColor56CC00(resources.Gradient[gradient], resources.Gradient[gradient + 1], 4096 - fraction, fraction, prior);
            if (phase < 8)
            {
                int sine = NativeBattleCameraMath.Sine(phase << 7);
                color = NativeBattleDuelSharedPackets.InterpolateColor56CC00(frame.State.ReadDword(BlackGlobal), color, 4096 - sine, sine, color);
            }
            else if (phase > 34)
            {
                int sine = NativeBattleCameraMath.Sine(((phase << 6) - 0x841) & unchecked((int)0xFFFFFC0));
                color = NativeBattleDuelSharedPackets.InterpolateColor56CC00(frame.State.ReadDword(BlackGlobal), color, sine, 4096 - sine, color);
            }
            NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(dx, dy, -dz).Matrix;
            NativeBattleActionMatrix scale = new([4096, 0, 0, 0, 4096, 0, 0, 0, scaleZ], 0, 0, 0, 4096);
            NativeBattleActionMatrix world = NativeBattleGfMatrices.Multiply56C090(basis, scale) with { X = unchecked((short)task.ReadWord(0x34)), Y = unchecked((short)task.ReadWord(0x36)), Z = unchecked((short)task.ReadWord(0x38)) };
            NativeBattleActionMatrix combined = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(combined, null);
            frame = frame with { State = frame.State.WriteView(PackedViewGlobal, PackedArena(packed), 32), TargetScaleStack = (frame.TargetScaleStack ?? throw new NotSupportedException("Bad Breath requires its current callback stack owner.")).StoreUInt32(-0x4C, unchecked((uint)world.X), 0x66F57D, "main-position") };
            frame = LoadView(frame, combined);
            temporary = WriteDword(temporary, 0, MeshAAddress);
            temporary = temporary.Write(4, BitConverter.GetBytes(color).AsSpan(0, 3));
            temporary = WriteWord(temporary, 12, (ushort)(phase * 10 & 127));
            temporary = WriteWord(temporary, 0x10, 0);
            temporary = WriteWord(temporary, 0x12, 0);
            temporary = WriteWord(temporary, 0x14, 256);
            temporary = WriteWord(temporary, 0x16, 256);
            temporary = WriteWord(temporary, 0x18, 0);
            temporary = WriteWord(temporary, 0x1A, 0);
            temporary = WriteWord(temporary, 0x1C, 128);
            temporary = WriteWord(temporary, 0x1E, 256);
            uint selectedVertices = frame.State.ReadDword(WorkingGlobal) + ScratchOffset;
            temporary = WriteDword(temporary, 0x20, selectedVertices);
            frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), allocation.Address, temporary, 92) };
            frame = inputs.Execute(frame, new NativeBattleBadBreath300DrawStrip(allocation.Address, MeshAAddress, selectedVertices) { PilotIdentity = inputs.EmitNodePresentation is null ? null : NativeBattlePilotVisualLifetimes.Identity(300, Read(frame, address), 1) with { ResourceGeneration = MeshAAddress } });
            frame = LoadRotationZeroTranslation(frame, RotationZ7012C0(unchecked((short)-unchecked((short)task.ReadWord(0x2A)))));
            frame = Deform66FBB0(frame, MeshBAddress, unchecked((short)task.ReadWord(0x2C)) + 1024, resources);
            frame = LoadView(frame, combined);
            frame = frame with { Staging = RequireStaging(frame).Write(allocation.Address, BitConverter.GetBytes(MeshBAddress)) };
            frame = inputs.Execute(frame, new NativeBattleBadBreath300DrawStrip(allocation.Address, MeshBAddress, selectedVertices) { PilotIdentity = inputs.EmitNodePresentation is null ? null : NativeBattlePilotVisualLifetimes.Identity(300, Read(frame, address), 2) with { ResourceGeneration = MeshBAddress } });
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 92) };
            if (phase is 0 or 49)
            {
                frame = React(frame, phase == 0 ? (byte)2 : (byte)1, inputs);
            }
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleBadBreath300Sound(resources.Sound, 128));
        }
        task = Read(frame, address).WriteWord(0x2A, unchecked((ushort)(Read(frame, address).ReadWord(0x2A) - 50))).WriteWord(0x2C, unchecked((ushort)(Read(frame, address).ReadWord(0x2C) + 200)));
        frame = Write(frame, address, task);
        return age == 58 ? (frame, 2) : (Write(frame, address, task.WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleBadBreath300Frame React(NativeBattleBadBreath300Frame frame, byte sequence, NativeBattleBadBreath300Inputs inputs)
    {
        for (byte actor = 0; actor < 3; actor++)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(actor);
            if ((model.Flags0 & 2) != 0 && ((model.Flags8 ?? throw new NotSupportedException("Bad Breath requires enabled party status flags.")) & 3) == 0 && actor != Actor(frame.State.ReadDword(CasterGlobal)))
            {
                frame = inputs.Execute(frame, new NativeBattleBadBreath300RingSequence(actor, sequence));
            }
        }
        return frame;
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleBadBreath300Frame frame) => frame.Staging ?? throw new NotSupportedException("Bad Breath requires its actual temporary memory.");
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleBadBreath300Frame frame) => new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
    private static NativeBattleBadBreath300Frame LoadRotationZeroTranslation(NativeBattleBadBreath300Frame frame, NativeBattleActionMatrix matrix) => frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, matrix, true, true, 0) } };
    private static NativeBattleBadBreath300Frame LoadView(NativeBattleBadBreath300Frame frame, NativeBattleActionMatrix matrix) => frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, matrix, true, true, null) } };
    private static NativeBattleActionMatrix RotationZ7012C0(short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        return new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
    }
    private static NativeBattleEffectArena PackedArena(NativeBattlePackedMatrix32 packed)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!packed.Known[index])
            {
                arena = arena.MarkUnknown(index, 1);
            }
        }
        return arena;
    }
    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena WriteDword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
