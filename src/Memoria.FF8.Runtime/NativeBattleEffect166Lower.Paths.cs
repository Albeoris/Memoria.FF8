using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Lower
{
    private static NativeBattleEffect166Step InitializePath(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        uint pathAddress = Path(task);
        if (Signed(task, 12) == 0)
        {
            NativeBattleEffectArena path = frame.State.ReadView(pathAddress, 820);
            NativeBattleEffectArena center = frame.State.ReadView(0x23C3248, 8);
            path = Copy(Copy(path, 0, center, 0, 8), 8, center, 0, 8);
            foreach (int offset in new[] { 0, 4, 8, 12 })
            {
                path = Word(path, offset, S(path, offset) + Roll(ref frame) % 7400 - 3700);
            }
            int dx = S(path, 8) - S(path, 0);
            int dz = S(path, 12) - S(path, 4);
            int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dz * dz));
            path = Copy(Copy(path, 16, path, 0, 8), 48, path, 8, 8);
            path = Word(Word(Word(path, 26, 0), 34, 0), 42, 0);
            foreach (int axis in new[] { 0, 4 })
            {
                int middle = (S(path, axis) + S(path, 8 + axis)) >> 1;
                path = Word(path, 32 + axis, middle);
                path = Word(path, 24 + axis, (S(path, axis) + middle) >> 1);
                path = Word(path, 40 + axis, (middle + S(path, 8 + axis)) >> 1);
            }
            foreach (int offset in new[] { 24, 28, 32, 36, 40, 44 })
            {
                path = Word(path, offset, S(path, offset) + Roll(ref frame) % unchecked(length * 2) - length);
            }
            (frame, uint temporary) = Allocate(frame, 400);
            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(5, path, 16, Context(frame, temporary, 400));
            frame = PublishContext(frame, temporary, work, 400);
            for (int sample = 0; sample < 48; sample++)
            {
                path = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(5, work, sample * 4096 / 47, path, 56 + sample * 8);
            }
            frame = frame with { State = frame.State.WriteView(pathAddress, path, 820) };
            if (inputs.CapturePresentation)
            {
                for (int sample = 0; sample < 48; sample++)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, pathAddress + 56, sample);
                }
            }
            frame = Release(frame, 400);
            (frame, uint beam) = Append(frame, 0x23C0540, 0x5E6030);
            NativeBattleModulePoolTask child = Read(frame, beam).WriteWord(12, 0).WriteWord(14, task.ReadWord(14)).WriteWord(24, 0).WriteWord(26, 2);
            child = child.WriteWord(28, unchecked((ushort)(Roll(ref frame) % 512 + 512)));
            frame = Write(frame, beam, child);
            (frame, uint history) = Append(frame, 0x23C0540, 0x5E7AE0);
            frame = Write(frame, history, Read(frame, history).WriteWord(12, 0).WriteWord(14, task.ReadWord(14)).WriteWord(24, 0).WriteWord(26, 2));
            for (int index = 0; index < 3; index++)
            {
                frame = frame with { State = frame.State.WriteDword(pathAddress + 440u + (uint)(index * 20), 0) };
            }
            (frame, uint ribbon) = Append(frame, 0x23C0540, 0x5E7ED0);
            frame = Write(frame, ribbon, Read(frame, ribbon).WriteWord(12, 0).WriteWord(14, task.ReadWord(14)).WriteWord(24, 0).WriteWord(26, 2));
        }
        return Clock(frame, address, Read(frame, address), 16);
    }

    private static NativeBattleEffect166Step AdvanceBeam(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena row = frame.State.ReadView(checked(Path(task) + 56u + (uint)(Signed(task, 24) * 8)), 8);
        NativeBattleVector anchor = Vector(frame.State.ReadView(0x23C3260, 8));
        int dx = S(row, 0) - anchor.X;
        int dy = S(row, 2) - anchor.Y;
        int dz = S(row, 4) - anchor.Z;
        (int x, int y, int z) = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(NativeBattleSleepingGas168.NormalizeSquare(dx, dy, dz));
        byte[] axis = resources.ReadStatic(0xD78050, 12);
        NativeBattleModuleAxisAngle align = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(axis), BinaryPrimitives.ReadInt32LittleEndian(axis.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(axis.AsSpan(8)), x, y, z);
        NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(align.Angle, align.Axis), 0, 0, 0, 4096);
        NativeBattleActionMatrix scale = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), Signed(task, 28), Signed(task, 28), unchecked(length << 12) / 5350);
        NativeBattleActionMatrix local = scale with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, scale).Rotation, X = anchor.X, Y = anchor.Y, Z = anchor.Z };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattlePoseMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 108);
        NativeBattleEffectArena context = Put(Put(Put(Context(frame, temporary, 108), 0, 0xD719E4), 8, 0), 0x1C, 0x33);
        context = Put(context, 0x20, 0x0030307F);
        int age = Signed(task, 12);
        if (age == 0 || age >= 16)
        {
            context = Put(Put(context, 12, unchecked((uint)(age == 0 ? 2048 : (age - 16) << 9))), 0x1C, 0xF3);
        }
        NativeBattleEffect166Mesh108Step drawn = NativeBattleEffect166Mesh108.DrawLoaded(frame.Memory, resources.ReadMesh(0xD719E4), inputs.ReadProjection(frame), context, inputs.Colors, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(166, task) with { ResourceGeneration = 0xD719E4 } : null);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
        frame = Deliver(frame, drawn.Batch, inputs);
        frame = Release(frame, 108);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 24) + Signed(task, 26)));
        int velocity = Signed(task, 26);
        task = task.WriteWord(24, unchecked((ushort)next)).WriteWord(26, unchecked((ushort)(velocity > 2 ? velocity - 1 : velocity))).WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)));
        return new(Write(frame, address, task), next >= 48 ? 2 : 0);
    }
}
