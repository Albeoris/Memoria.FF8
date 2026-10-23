using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath129Controller
{
    public static (NativeBattleBreath129Frame Frame, int Result) AdvanceMain(NativeBattleBreath129Frame frame, uint address, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        int envelope = age < 4 ? age << 9 : age > 69 ? unchecked((73 - age) << 9) : 2048;
        if (envelope > unchecked((int)frame.State.ReadDword(StageGlobal)))
        {
            frame = frame with { State = frame.State.WriteDword(StageGlobal, unchecked((uint)envelope)) };
        }
        uint caster = frame.State.ReadDword(CasterGlobal);
        if (age < 20 && (age & 1) == 0)
        {
            uint row = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, caster));
            frame = SpawnFibres(frame, row, 0x1229318u + (uint)(((age >> 1) & 3) * 8), 0x1229338, 12, 7, 0x100);
        }
        NativeBattleBreath129PointStep mouth = inputs.ReadBone502170(frame, caster, 9, 0x2000);
        frame = WriteBytes(mouth.Frame, address + 0x30, mouth.Point8, 6);
        int actPhase = age - 28;
        if ((uint)actPhase < 45)
        {
            task = Read(frame, address);
            NativeBattleVector source = TaskVector(task, 0x30);
            NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { X = source.X, Y = source.Y, Z = source.Z };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            NativeBattleBreath129ActContext context = new(NativeBattlePackedMatrix32.FromMatrix(matrix, null), -128, Working(frame) + 0x35F0);
            frame = inputs.AdvanceAct(frame, address + 0x38, resources.Act(0x12239B8), context).Frame;
            NativeBattleBreath129ActStep loop = inputs.AdvanceAct(frame, address + 0x140, resources.Act(0x1226098), context);
            frame = loop.Frame;
            if (loop.Remaining == 0)
            {
                frame = ResetWorkspace(frame, address, 0x140, 0x68, resources.Act(0x1226098));
            }
            if (actPhase <= 32)
            {
                task = Read(frame, address);
                source = TaskVector(task, 0x30);
                local = NativeBattlePandemona291Restoration.Rotation701270(inputs.ReadHeading(frame, caster)) with { X = source.X, Y = source.Y, Z = source.Z };
                matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
                frame = inputs.AdvanceAct(frame, address + 0x1A8, resources.Act(0x122688C), new(NativeBattlePackedMatrix32.FromMatrix(matrix, null), 0, Working(frame) + 0x35F0)).Frame;
            }
        }
        int phase = age - 30;
        if ((uint)phase < 43)
        {
            (frame, uint temporary) = Allocate(frame, 92);
            task = Read(frame, address);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(RotationZ7012C0(Signed(task, 0x28)), 0));
            frame = NativeBattleBreath129Deformation.Transform(frame, 0x12272DC, Signed(task, 0x2A), resources);
            task = Read(frame, address);
            int firstIndex = Signed(task, 0x20);
            int nextIndex = Signed(task, 0x22);
            NativeBattleBreath129PointStep first = inputs.ReadBone502170(frame, task.ReadDword(16 + firstIndex * 4), 0xF1, 0);
            frame = first.Frame;
            NativeBattleBreath129PointStep second = inputs.ReadBone502170(frame, Read(frame, address).ReadDword(16 + nextIndex * 4), 0xF1, 0);
            frame = second.Frame;
            NativeBattleVector a = Point(first.Point8);
            NativeBattleVector b = Point(second.Point8);
            int ratio = Signed(Read(frame, address), 0x24);
            NativeBattleVector target = new(unchecked((short)(a.X + (unchecked((b.X - a.X) * ratio) >> 12))), unchecked((short)(a.Y + (unchecked((b.Y - a.Y) * ratio) >> 12))), unchecked((short)(a.Z + (unchecked((b.Z - a.Z) * ratio) >> 12))));
            if (phase >= 5)
            {
                task = Read(frame, address);
                short nextProgress = unchecked((short)(Signed(task, 0x24) + Signed(task, 0x26)));
                frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)nextProgress)));
                if (nextProgress >= 4096)
                {
                    frame = SpawnTarget(frame, address, resources, inputs);
                    task = Read(frame, address);
                    int next = Signed(task, 0x22);
                    int following = Math.Min(next + 1, Signed(task, 14) - 1);
                    frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(Signed(task, 0x24) - 4096))).WriteWord(0x20, unchecked((ushort)next)).WriteWord(0x22, unchecked((ushort)following)));
                }
            }
            NativeBattleEffectArena context = Context(frame, temporary, 92);
            if (phase < 8)
            {
                context = context.Write(4, new byte[4]).Write(8, BitConverter.GetBytes(4096 - NativeBattleCameraMath.Sine(phase << 7)));
            }
            else if (phase > 35)
            {
                context = context.Write(4, new byte[4]).Write(8, BitConverter.GetBytes(NativeBattleCameraMath.Sine(unchecked((phase << 7) - 0x1101) & 0x1FFFFF80)));
            }
            else
            {
                context = context.Write(8, new byte[4]);
            }
            task = Read(frame, address);
            NativeBattleVector origin = TaskVector(task, 0x30);
            int dx = target.X - origin.X;
            int dy = target.Y - origin.Y;
            int dz = target.Z - origin.Z;
            int length = unchecked(NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dy * dy + dz * dz)) + 512);
            frame = Write(frame, address, task.WriteDword(0x2C, unchecked((uint)length)));
            NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(dx, -dy, -dz).Matrix;
            NativeBattleActionMatrix scale = new([4096, 0, 0, 0, 4096, 0, 0, 0, unchecked((short)length)], 0, 0, 0, 4096);
            NativeBattleActionMatrix world = NativeBattleGfMatrices.Multiply56C090(basis, scale) with { X = origin.X, Y = origin.Y, Z = origin.Z };
            NativeBattleActionMatrix combined = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(combined, null);
            frame = frame with { State = frame.State.WriteView(CompositeMatrix, PackedArena(packed), 32) };
            frame = LoadMatrix(frame, packed);
            context = context.Write(0, BitConverter.GetBytes(0x12272DCu)).Write(14, BitConverter.GetBytes(unchecked((ushort)((phase & 7) << 4)))).Write(16, new byte[4]).Write(20, BitConverter.GetBytes((ushort)256)).Write(22, BitConverter.GetBytes((ushort)256)).Write(24, new byte[4]).Write(28, BitConverter.GetBytes((ushort)256)).Write(30, BitConverter.GetBytes((ushort)128)).Write(32, BitConverter.GetBytes(Working(frame) + 0x35F0));
            frame = PublishContext(frame, temporary, context, 92);
            frame = NativeBattleBreath129Mesh.Draw6B5A50(frame, temporary, 17, 2, inputs);
            frame = Release(frame, 92);
            if ((phase & 1) == 0)
            {
                NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
                frame = frame with { Random = random };
                int count = BitConverter.ToInt16(resources.Read(0x1229A28, 2).AsSpan());
                frame = SpawnStream6B61B0(frame, 0x1229A28, sample * count >> 15, resources);
            }
        }
        task = Read(frame, address);
        NativeBattlePackedMatrix32 yaw = NativeBattlePackedMatrix32.FromMatrix(RotationZ7012C0(unchecked((short)-Signed(task, 0x28))), 0);
        frame = frame with { State = frame.State.WriteView(YawMatrix, PackedArena(yaw), 20) };
        (frame, int curves) = Scan(frame, 4, resources, inputs);
        if (Signed(Read(frame, address), 12) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleBreath129Sound(resources.Sound, 0, 128));
        }
        task = Read(frame, address).WriteWord(0x28, unchecked((ushort)(Signed(Read(frame, address), 0x28) - 100))).WriteWord(0x2A, unchecked((ushort)(Signed(Read(frame, address), 0x2A) + 200)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) >= 73 && curves == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)))), 0);
    }

    private static NativeBattleBreath129Frame SpawnTarget(NativeBattleBreath129Frame frame, uint mainAddress, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        (frame, uint address) = Append(frame, 2, 0x6B5FC0);
        if (address == 0)
        {
            return frame;
        }
        NativeBattleModulePoolTask main = Read(frame, mainAddress);
        int index = Signed(main, 0x20);
        uint actor = main.ReadDword(16 + index * 4);
        int actorIndex = checked((int)((actor - 0x1D972C0) / 156));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteByte(14, main.ReadByte(0x1C + index)).WriteByte(15, checked((byte)actorIndex)));
        NativeBattleBreath129PointStep point = inputs.ReadBone502170(frame, actor, 0xF1, 0);
        frame = WriteBytes(point.Frame, address + 16, point.Point8, 6);
        NativeBattleVector position = Point(point.Point8);
        NativeBattleVector source = TaskVector(Read(frame, mainAddress), 0x30);
        int angle = NativeWorldScriptedPathMath.NativeAngle(position.X - source.X, position.Z - source.Z);
        frame = Write(frame, address, Read(frame, address).WriteWord(22, unchecked((ushort)angle)));
        frame = NativeBattleBreath129Body.Capture6B73F0(frame, actor, address + 0x128, 4, resources, inputs);
        uint count = ReadDword(frame, Working(frame) + 0x35E0);
        if (count >= 3)
        {
            throw new InvalidDataException("Breath selected-body list leaves its three actual pointer entries.");
        }
        frame = StoreDword(frame, Working(frame) + 0x35E4 + count * 4, actor);
        frame = StoreDword(frame, Working(frame) + 0x35E0, count + 1);
        return ResetWorkspace(frame, address, 24, 0x110, resources.Act(0x1225468));
    }

    private static NativeBattleBreath129Frame ResetWorkspace(NativeBattleBreath129Frame frame, uint address, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(task.ReadBytes(offset, size).ToImmutableArray()).Initialize7016B0(source, size);
        return Write(frame, address, task.WriteBytes(offset, workspace.Data.AsSpan()));
    }

    private static NativeBattleVector TaskVector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
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
