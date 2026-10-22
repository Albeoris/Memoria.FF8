using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHypnotize133ContextMemoryStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context40);
public sealed record NativeBattleHypnotize133BonePointStep(NativeBattleEffectMemory Memory, NativeBattleVector Position);

public static class NativeBattleHypnotize133Contexts
{
    public static NativeBattleHypnotize133ContextMemoryStep Screen(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 camera, NativeBattleVector casterAnchor, short? anchorW, short focalShadow, uint morphAddress, NativeBattleCameraProjection projection)
    {
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        registers = registers with { Geometry = LoadCamera(registers.Geometry, camera, true) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, casterAnchor, anchorW);
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Hypnotize screen context requires its inherited projection controls.");
        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
        uint screen = registers.Geometry.Sxy[2] ?? throw new InvalidDataException("Hypnotize requires its native projected caster anchor.");
        int x = unchecked((160 - unchecked((short)screen)) << 4);
        int y = unchecked((120 - unchecked((short)(screen >> 16))) << 4);
        int z = unchecked(focalShadow * 12);
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(x, y, z).Matrix;
        NativeBattleEffectArena context = EmptyContext(morphAddress, 0);
        for (int index = 0; index < 9; index++)
        {
            context = context.Write(index * 2, BitConverter.GetBytes(basis.Rotation[index]));
        }
        context = Put(context, 20, unchecked(-x));
        context = Put(context, 24, unchecked(-y));
        context = Put(context, 28, unchecked(focalShadow << 4));
        return new(Publish(memory, registers), context);
    }

    public static NativeBattleHypnotize133ContextMemoryStep Anchored(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 camera, NativeBattleVector bonePoint, short? localW, uint morphAddress)
    {
        NativeBattleGeometryRegisterStep registers = Transform(Registers(memory), camera, bonePoint, localW);
        NativeBattleEffectArena context = CameraRotation(camera, EmptyContext(morphAddress, -128));
        return new(Publish(memory, registers), AddCameraTranslation(camera, registers, context));
    }

    public static NativeBattleHypnotize133ContextMemoryStep Fading(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 camera, NativeBattleVector anchor, short factor, uint morphAddress, NativeBattleSummonColors constants)
    {
        camera.Validate();
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        NativeBattleEffectArena context = EmptyContext(morphAddress, -128);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)factor) } };
        for (int row = 0; row < 3; row++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, CameraWord(camera, row * 6), CameraWord(camera, row * 6 + 2), CameraWord(camera, row * 6 + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
            for (int axis = 0; axis < 3; axis++)
            {
                short value = unchecked((short)(registers.Colors.Ir[axis] ?? throw new InvalidDataException("Hypnotize fading context requires its native GPF result.")));
                context = context.Write(row * 6 + axis * 2, BitConverter.GetBytes(value));
            }
        }
        registers = Transform(registers, camera, anchor, factor);
        return new(Publish(memory, registers), AddCameraTranslation(camera, registers, context));
    }

    public static NativeBattleHypnotize133BonePointStep BonePoint(NativeBattleEffectMemory memory, NativeBattleActionMatrix currentWorld, NativeBattleActionMatrix currentPublishedBone, NativeBattleModuleAnchor localAnchor)
    {
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(currentWorld, currentPublishedBone);
        byte[] anchor = new byte[8];
        short[] values = [localAnchor.X, localAnchor.Y, localAnchor.Z, localAnchor.W];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(index * 2, 2), values[index]);
        }
        (NativeBattleEffectMemory updated, NativeBattleVector point) = NativeBattleBadBreath300Mouth.TransformComposed(Publish(memory, Registers(memory)), composed, anchor.ToImmutableArray());
        return new(updated, point);
    }

    private static NativeBattleGeometryRegisterStep Transform(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 camera, NativeBattleVector point, short? w)
    {
        registers = registers with { Geometry = LoadCamera(registers.Geometry, camera, false) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
        return NativeBattleGeometry.Transform(registers, false);
    }

    private static NativeBattleEffectArena EmptyContext(uint morphAddress, int depth) => NativeBattleEffectArena.Clear().MarkUnknown(0, 40).Write(32, BitConverter.GetBytes(depth)).Write(36, BitConverter.GetBytes(morphAddress));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, int value) => context.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
    private static NativeBattleEffectMemory Publish(NativeBattleEffectMemory memory, NativeBattleGeometryRegisterStep registers) => memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };

    private static NativeBattleEffectArena CameraRotation(NativeBattlePackedMatrix32 camera, NativeBattleEffectArena context)
    {
        camera.Validate();
        if (camera.Known.AsSpan(0, 18).Contains(false))
        {
            throw new NotSupportedException("Hypnotize context requires its actual camera rotation words.");
        }
        return context.Write(0, camera.Bytes.AsSpan(0, 18));
    }

    private static NativeBattleEffectArena AddCameraTranslation(NativeBattlePackedMatrix32 camera, NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena context)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            int mac = registers.Colors.Mac[axis] ?? throw new InvalidDataException("Hypnotize context requires its full native MAC translation.");
            context = Put(context, 20 + axis * 4, unchecked(mac + CameraDword(camera, 20 + axis * 4)));
        }
        return context;
    }

    private static NativeBattleGeometryRegisters LoadCamera(NativeBattleGeometryRegisters geometry, NativeBattlePackedMatrix32 camera, bool translation)
    {
        camera.Validate();
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => camera.Known[index] ? (byte?)camera.Bytes[index] : null).ToImmutableArray() };
        if (translation)
        {
            geometry = geometry with { Translation = [CameraDword(camera, 20), CameraDword(camera, 24), CameraDword(camera, 28)] };
        }
        return geometry;
    }

    private static ushort CameraWord(NativeBattlePackedMatrix32 camera, int offset)
    {
        if (camera.Known.AsSpan(offset, 2).Contains(false))
        {
            throw new NotSupportedException("Hypnotize requires its actual camera WORD.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(camera.Bytes.AsSpan(offset, 2));
    }

    private static int CameraDword(NativeBattlePackedMatrix32 camera, int offset)
    {
        if (camera.Known.AsSpan(offset, 4).Contains(false))
        {
            throw new NotSupportedException("Hypnotize requires its actual camera translation DWORD.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(camera.Bytes.AsSpan(offset, 4));
    }
}
