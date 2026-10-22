using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath131Controller
{
    public static (NativeBattleBadBreath131Frame Frame, int Result) AdvanceMain(NativeBattleBadBreath131Frame frame, uint address, NativeBattleBadBreath131Resources resources, NativeBattleBadBreath131Inputs inputs)
    {
        NativeBattleBadBreath131PointStep mouth = inputs.ReadBone(frame, Actor(frame.State.ReadDword(CasterGlobal)), 11, 8192);
        frame = mouth.Frame;
        NativeBattleModulePoolTask task = Vector(Read(frame, address), 0x2C, mouth.Position);
        frame = Write(frame, address, task);
        int age = Signed(task, 12);
        int phase = age - 15;
        if ((uint)phase < 50)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 92);
            frame = inputs.Execute(frame with { Staging = allocation.Staging }, new NativeBattleBadBreath131FlashEnvelope(phase));
            frame = LoadRotationZeroTranslation(frame, RotationZ7012C0(Signed(task, 0x22)));
            frame = Deform6B4140(frame, MeshAAddress, Signed(task, 0x24), resources);
            short sweep = unchecked((short)(NativeBattleCameraMath.Cosine(phase * 4096 / 50) >> 1));
            task = Read(frame, address);
            int selected = Signed(task, 0x26);
            if (phase >= 25 && selected < Signed(task, 14))
            {
                int actor = Actor(task.ReadDword(16 + selected * 4));
                if (sweep >= Transform(inputs.ReadModel(actor)).Position28.X - 1000)
                {
                    (frame, uint childAddress) = Append(frame, 2, TargetCallback);
                    if (childAddress != 0)
                    {
                        uint savedColor = inputs.ReadModel(actor).Color40 ?? throw new NotSupportedException("Bad Breath requires its actual saved target color.");
                        frame = Write(frame, childAddress, Read(frame, childAddress).WriteWord(12, 0).WriteByte(14, task.ReadByte(0x1C + selected)).WriteByte(15, checked((byte)actor)).WriteDword(0x18, savedColor));
                        NativeBattleBadBreath131AnchorStep anchor = inputs.ReadAnchor(frame, actor);
                        frame = anchor.Frame;
                        short angle = unchecked((short)(Transform(inputs.ReadModel(actor)).Angle36 - 200));
                        NativeBattleModulePoolTask child = Vector(Read(frame, childAddress), 16, new(anchor.Anchor.X, anchor.Anchor.Y, anchor.Anchor.Z)).WriteWord(0x16, unchecked((ushort)angle));
                        child = child.WriteBytes(0x1C, new NativeBattleGfAnimationWorkspace(child.ReadBytes(0x1C, 0x128).ToImmutableArray()).Initialize7016B0(resources.Target, 0x128).Data.AsSpan());
                        frame = Write(frame, childAddress, child);
                    }
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x26, unchecked((ushort)(selected + 1))));
                }
            }
            task = Read(frame, address);
            int dx = sweep - Signed(task, 0x2C);
            int dy = -500 - Signed(task, 0x2E);
            int dz = Signed(task, 0x20) - Signed(task, 0x30);
            int length = unchecked(NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dy * dy + dz * dz)) + 512);
            frame = Write(frame, address, task.WriteDword(0x28, unchecked((uint)length)));
            short scaleZ = unchecked((short)length);
            if (phase < 24)
            {
                scaleZ = unchecked((short)(scaleZ * (phase * 4096 / 24) >> 12));
            }
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), allocation.Address, 92), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), 0, 92);
            uint prior = BinaryPrimitives.ReadUInt32LittleEndian(temporary.ReadSnapshot(4, 4).Data);
            int gradient = (phase >> 2) % 3;
            int fraction = phase * 1024 & 4095;
            NativeBattleWeightedColorStep blended = NativeBattleWeightedColor56CC00.Blend(frame.Memory, resources.Gradient[gradient], resources.Gradient[gradient + 1], unchecked((short)(4096 - fraction)), unchecked((short)fraction), prior, inputs.Colors);
            frame = frame with { Memory = blended.Memory };
            uint color = blended.Color;
            if (phase < 8)
            {
                int sine = NativeBattleCameraMath.Sine(phase << 7);
                blended = NativeBattleWeightedColor56CC00.Blend(frame.Memory, frame.State.ReadDword(BlackGlobal), color, unchecked((short)(4096 - sine)), unchecked((short)sine), color, inputs.Colors);
                frame = frame with { Memory = blended.Memory };
                color = blended.Color;
            }
            else if (phase > 42)
            {
                int sine = NativeBattleCameraMath.Sine(unchecked((phase << 7) - 0x1481) & 0x1FFFFF80);
                blended = NativeBattleWeightedColor56CC00.Blend(frame.Memory, frame.State.ReadDword(BlackGlobal), color, unchecked((short)sine), unchecked((short)(4096 - sine)), color, inputs.Colors);
                frame = frame with { Memory = blended.Memory };
                color = blended.Color;
            }
            NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(dx, -dy, -dz).Matrix;
            NativeBattleActionMatrix scale = new([4096, 0, 0, 0, 4096, 0, 0, 0, scaleZ], 0, 0, 0, 4096);
            task = Read(frame, address);
            NativeBattleActionMatrix world = NativeBattleGfMatrices.Multiply56C090(basis, scale) with { X = Signed(task, 0x2C), Y = Signed(task, 0x2E), Z = Signed(task, 0x30) };
            NativeBattleActionMatrix combined = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), world);
            frame = frame with { State = frame.State.WriteView(PackedViewGlobal, PackedArena(NativeBattlePackedMatrix32.FromMatrix(combined, null)), 32) };
            frame = LoadView(frame, combined);
            temporary = temporary.Write(0, BitConverter.GetBytes(MeshAAddress)).Write(4, BitConverter.GetBytes(color).AsSpan(0, 3));
            temporary = temporary.Write(12, BitConverter.GetBytes(unchecked((ushort)(phase * 10 & 127))));
            temporary = temporary.Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(0x01000100u)).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x01000080u));
            uint selectedVertices = checked(Working(frame) + ScratchOffset);
            temporary = temporary.Write(0x20, BitConverter.GetBytes(selectedVertices));
            frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), allocation.Address, temporary, 92) };
            frame = inputs.Execute(frame, new NativeBattleBadBreath131DrawStrip92(allocation.Address, MeshAAddress, selectedVertices));
            task = Read(frame, address);
            frame = LoadRotationZeroTranslation(frame, RotationZ7012C0(unchecked((short)-Signed(task, 0x22))));
            frame = Deform6B4140(frame, MeshBAddress, Signed(task, 0x24) + 1024, resources);
            frame = LoadView(frame, combined);
            frame = frame with { Staging = RequireStaging(frame).Write(allocation.Address, BitConverter.GetBytes(MeshBAddress)) };
            frame = inputs.Execute(frame, new NativeBattleBadBreath131DrawStrip92(allocation.Address, MeshBAddress, selectedVertices));
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 92) };
        }
        if (age == 20)
        {
            frame = inputs.Execute(frame, new NativeBattleBadBreath131Sound(resources.Sound, 0, 128));
        }
        task = Read(frame, address).WriteWord(0x22, unchecked((ushort)(Read(frame, address).ReadWord(0x22) - 50))).WriteWord(0x24, unchecked((ushort)(Read(frame, address).ReadWord(0x24) + 200)));
        frame = Write(frame, address, task);
        return age >= 65 ? (frame, 2) : (Write(frame, address, task.WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleBadBreath131Frame frame) => frame.Staging ?? throw new NotSupportedException("Bad Breath requires its actual temporary stack.");
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleBadBreath131Frame frame) => new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
    private static NativeBattleBadBreath131Frame LoadRotationZeroTranslation(NativeBattleBadBreath131Frame frame, NativeBattleActionMatrix matrix)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, matrix, true, false, null);
        return frame with { Memory = NativeBattleModuleRenderCommands.ClearTranslation704070(frame.Memory with { GeometryRegisters = geometry }) };
    }
    private static NativeBattleBadBreath131Frame LoadView(NativeBattleBadBreath131Frame frame, NativeBattleActionMatrix matrix) => frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, matrix, true, true, null) } };
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
}
