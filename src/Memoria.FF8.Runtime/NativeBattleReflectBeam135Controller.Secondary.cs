using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleReflectBeam135Controller
{
    public static (NativeBattleReflectBeam135Frame Frame, int Result) AdvanceSecondary(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 12) < 13)
        {
            uint selector = task.ReadDword(0x10);
            uint selectorOffset = unchecked(selector - BoneSelectorsAddress);
            if ((selectorOffset & 1) != 0 || selectorOffset / 2 >= (uint)resources.BoneSelectors.Length)
            {
                throw new InvalidDataException("Reflect Beam secondary requires its actual original bone selector.");
            }
            int caster = Actor(frame.State.ReadDword(CasterGlobal));
            NativeBattleActionMatrix bone = inputs.ReadCurrentPublishedBone32(frame, caster, resources.BoneSelectors[(int)(selectorOffset / 2)]).ReadMatrix();
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(bone, null));
            (frame, NativeBattleVector anchor) = SecondaryTransform(frame, new(0, 0, -4096));
            NativeBattleEffectArena context = SecondaryContext(frame, NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, SecondaryXyz(anchor)), -128);
            NativeBattleReflectBeam135ActStep head = inputs.AdvanceAct(frame, address, resources.SecondaryAct, 0x1C, 0x1F8, context);
            frame = head.Frame;
            short[] rotation = new short[9];
            for (int row = 0; row < 3; row++)
            {
                var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(bone.Rotation[row * 3], bone.Rotation[row * 3 + 1], bone.Rotation[row * 3 + 2]);
                rotation[row * 3] = unchecked((short)normalized.X);
                rotation[row * 3 + 1] = unchecked((short)normalized.Y);
                rotation[row * 3 + 2] = unchecked((short)normalized.Z);
            }
            NativeBattleActionMatrix local = new(rotation.ToImmutableArray(), anchor.X, anchor.Y, anchor.Z, 4096);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
            int scale = unchecked((Signed(Read(frame, address), 12) + 1) * 4096) / 13;
            int radius = unchecked(scale * 4);
            (frame, NativeBattleVector endpoint) = SecondaryTransform(frame, new(0, 0, unchecked((short)radius)));
            if (endpoint.Y <= 0)
            {
                int divisor = endpoint.Y - anchor.Y;
                short x = unchecked((short)(anchor.X + unchecked((anchor.X - endpoint.X) * anchor.Y) / divisor));
                short z = unchecked((short)(anchor.Z + unchecked((anchor.Z - endpoint.Z) * anchor.Y) / divisor));
                NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, SecondaryXyz(new(x, 0, z)));
                task = Read(frame, address).WriteWord(0xE, 1);
                task = task.WriteBytes(0x14, point.ReadSnapshot(0, 8).Data).MarkPayloadUnknown(0x1A, 2);
                frame = Write(frame, address, task);
                NativeBattleGeometryRegisterStep registers = SecondaryRegisters(frame);
                registers = registers with { Colors = registers.Colors with { Ir = [x - anchor.X, -anchor.Y, z - anchor.Z] } };
                registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
                frame = SecondaryPublish(frame, registers);
                int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value));
                radius = unchecked(length + (length >> 4));
            }
            short planar = unchecked((short)(NativeBattleCameraMath.Sine(scale >> 1) >> 3));
            frame = frame with { State = frame.State.WriteWord(ScaleMatrixGlobal, unchecked((ushort)planar)).WriteWord(ScaleMatrixGlobal + 8, unchecked((ushort)planar)).WriteWord(ScaleMatrixGlobal + 16, unchecked((ushort)radius)) };
            byte[] matrixBytes = frame.State.Read(ScaleMatrixGlobal, 18);
            short[] diagonal = new short[9];
            for (int lane = 0; lane < 9; lane++)
            {
                diagonal[lane] = BinaryPrimitives.ReadInt16LittleEndian(matrixBytes.AsSpan(lane * 2));
            }
            NativeBattleActionMatrix scaled = local with { Rotation = NativeBattleGfMatrices.Multiply56C090(local, new(diagonal.ToImmutableArray(), 0, 0, 0, 4096)).Rotation };
            NativeBattleActionMatrix viewed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), scaled);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(viewed, null));
            (frame, uint temporaryAddress) = Allocate(frame, 88);
            NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 88).Write(0, BitConverter.GetBytes(StaticMeshAddress)).Write(0x1C, BitConverter.GetBytes(0x30u));
            frame = PublishContext(frame, temporaryAddress, temporary, 88);
            frame = inputs.Execute(frame, new NativeBattleReflectBeam135DrawStatic88(temporaryAddress, StaticMeshAddress));
            frame = Release(frame, 88);
        }
        else if (task.ReadWord(0xE) == 0)
        {
            return (frame, 2);
        }
        task = Read(frame, address);
        if (task.ReadWord(0xE) != 0)
        {
            NativeBattleReflectBeam135ActStep tail = inputs.AdvanceAct(frame, address, resources.ImpactAct, 0x214, 0x58, SecondaryContext(frame, TaskView(task, 0x14, 8), 0));
            frame = tail.Frame;
            if (tail.Remaining == 0)
            {
                frame = Write(frame, address, Read(frame, address).WriteWord(0xE, 0));
            }
        }
        task = Read(frame, address);
        return (Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), 0);
    }

    private static NativeBattleEffectArena SecondaryContext(NativeBattleReflectBeam135Frame frame, NativeBattleEffectArena point, int depth) => point.Write(8, BitConverter.GetBytes(depth)).Write(12, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));

    private static (NativeBattleReflectBeam135Frame Frame, NativeBattleVector Point) SecondaryTransform(NativeBattleReflectBeam135Frame frame, NativeBattleVector point)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(SecondaryRegisters(frame), 0, point, null);
        registers = NativeBattleGeometry.Transform(registers);
        NativeBattleVector transformed = new(unchecked((short)(registers.Colors.Ir[0] ?? throw new NotSupportedException("Reflect Beam requires its actual transformed IR output."))), unchecked((short)(registers.Colors.Ir[1] ?? throw new NotSupportedException("Reflect Beam requires its actual transformed IR output."))), unchecked((short)(registers.Colors.Ir[2] ?? throw new NotSupportedException("Reflect Beam requires its actual transformed IR output."))));
        return (SecondaryPublish(frame, registers), transformed);
    }

    private static NativeBattleGeometryRegisterStep SecondaryRegisters(NativeBattleReflectBeam135Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Reflect Beam secondary requires its actual geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Reflect Beam secondary requires its actual arithmetic registers."));
    private static NativeBattleReflectBeam135Frame SecondaryPublish(NativeBattleReflectBeam135Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

    private static byte[] SecondaryXyz(NativeBattleVector point)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
        return bytes;
    }
}
