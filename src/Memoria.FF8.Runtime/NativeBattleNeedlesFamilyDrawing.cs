using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNeedlesPrepareStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context220);
public sealed record NativeBattleNeedlesDrawStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context220, NativeBattleGfGeometryBatch Batch);
public sealed record NativeBattleNeedlesSparkStep(NativeBattleEffectMemory Memory, NativeBattlePackedMatrix32 Matrix);

public static class NativeBattleNeedlesFamilyDrawing
{
    public const int ContextLength = 220;

    // The caller copies the addressed vertex32 and face80 resources into6C and8C, and its camera32 into2C before scanning records.
    public static NativeBattleNeedlesPrepareStep PrepareNeedle(NativeBattleEffectMemory memory, NativeBattleEffectArena context220, NativeBattleEffectArena slot56, NativeBattlePackedMatrix32 camera)
    {
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        registers = registers with { Geometry = LoadPacked(registers.Geometry, camera, true, false) };
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, Word(slot56, 0x18 + column * 2), Word(slot56, 0x1E + column * 2), Word(slot56, 0x24 + column * 2));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int row = 0; row < 3; row++)
            {
                context220 = context220.Write(0x4C + row * 6 + column * 2, BitConverter.GetBytes(unchecked((short)(registers.Colors.Ir[row] ?? throw new InvalidDataException("Needle rotation requires its native IR result.")))));
            }
        }
        registers = registers with { Geometry = LoadPacked(registers.Geometry, camera, false, true) };
        short z = unchecked((short)Word(slot56, 0x34));
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)Word(slot56, 0x2C)), unchecked((short)Word(slot56, 0x30)), z), z < 0 ? (short)-1 : (short)0);
        registers = NativeBattleGeometry.Transform(registers);
        for (int axis = 0; axis < 3; axis++)
        {
            context220 = Put(context220, 0x60 + axis * 4, unchecked((uint)(registers.Colors.Mac[axis] ?? throw new InvalidDataException("Needle translation requires its native MAC result."))));
        }
        NativeBattlePackedMatrix32 composed = Packed(context220, 0x4C);
        registers = registers with { Geometry = LoadPacked(registers.Geometry, composed, true, true) };
        return new(Publish(memory, registers), context220);
    }

    public static NativeBattleNeedlesDrawStep DrawNeedle(NativeBattleEffectMemory memory, NativeBattleEffectArena context220, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Needle drawing requires its actual GTE controls.");
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleActionMatrix? pilotMatrix = pilotIdentity is null ? null : NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(registers.Geometry).ReadMatrix();
        for (int index = 0; index < 4; index++)
        {
            byte[] face = context220.Read(0x8C + index * 20, 20);
            System.Numerics.Vector3[]? pilotWorld = pilotIdentity is null ? null : new System.Numerics.Vector3[3];
            for (int corner = 0; corner < 3; corner++)
            {
                int vertex = 0x6C + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2, 2)) * 4;
                if (vertex < 0x6C || vertex > 0x8C - 8)
                {
                    throw new InvalidDataException("Needle face leaves its four copied packed vertices.");
                }
                NativeBattleVector point = new(unchecked((short)Word(context220, vertex)), unchecked((short)Word(context220, vertex + 2)), unchecked((short)Word(context220, vertex + 4)));
                registers = NativeBattleGeometry.LoadVector(registers, corner, point, unchecked((short)Word(context220, vertex + 6)));
                if (pilotWorld is not null)
                {
                    pilotWorld[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix!, point), projection);
                }
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint flags = registers.Colors.Flags ?? throw new InvalidDataException("Needle projection requires its FLAG result.");
            context220 = Put(context220, 12, flags);
            NativeBattleModuleProjectedVertex[] projected = Enumerable.Range(0, 3).Select(corner => new NativeBattleModuleProjectedVertex(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), flags)).ToArray();
            if ((flags & 0x60000) == 0)
            {
                registers = NativeBattleGeometry.Clip45EE10(registers);
                context220 = Put(context220, 0, registers.Geometry.Mac0!.Value);
            }
            NativeBattleMeshPacketVariant variant = new(null, null, false, true)
            {
                ObserveArea = area =>
                {
                    if (area > 0)
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Needle drawing requires its actual ZSF3."));
                    }
                }
            };
            NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, projected, 4, new(0, null, null, null, null, null, 0, tableBaseBucket, depthShift), constants, allocated, variant: variant);
            if (attempt.Primitive is { } primitive)
            {
                uint otz = registers.Geometry.Otz ?? throw new InvalidDataException("Needle drawing requires its native OTZ result.");
                attempt = attempt with { Primitive = primitive with { Bucket = checked(tableBaseBucket + (int)(otz >> depthShift)) } };
                if (pilotIdentity is { } identity && pilotWorld is not null)
                {
                    NativeBattleGeometryPrimitive emitted = attempt.Primitive!;
                    attempt = attempt with { Primitive = emitted with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 4 + index) }, pilotWorld[0], pilotWorld[1], pilotWorld[2], default, 3) } };
                }
                context220 = Put(context220, 8, otz);
                allocated = checked(allocated + 28);
            }
            attempts.Add(attempt);
        }
        return new(Publish(memory, registers), context220, new(attempts.ToImmutable(), allocated));
    }

    public static NativeBattleNeedlesSparkStep PrepareSpark(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 retainedMatrix, NativeBattlePackedMatrix32 camera, NativeBattleVector anchor, short? anchorW, short scale, int depth, short focalShadow, NativeBattleCameraProjection projection)
    {
        retainedMatrix.Validate();
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        byte[] bytes = retainedMatrix.Bytes.ToArray();
        bool[] known = retainedMatrix.Known.ToArray();
        foreach (int offset in new[] { 0, 8, 16 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), scale);
            known[offset] = true;
            known[offset + 1] = true;
        }
        registers = registers with { Geometry = LoadPacked(registers.Geometry, camera, true, true) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, anchor, anchorW);
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Needle spark requires its inherited GTE controls.");
        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
        uint screen = registers.Geometry.Sxy[2] ?? throw new InvalidDataException("Needle spark requires its projected screen position.");
        int[] translation = [unchecked((short)screen) / 8 - 160, unchecked((short)(screen >> 16)) / 8 - 108, unchecked(focalShadow + depth)];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20 + axis * 4, 4), translation[axis]);
            Array.Fill(known, true, 20 + axis * 4, 4);
        }
        NativeBattlePackedMatrix32 result = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        registers = registers with { Geometry = LoadPacked(registers.Geometry, result, true, false) };
        return new(Publish(memory, registers), result);
    }

    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
    private static NativeBattleEffectMemory Publish(NativeBattleEffectMemory memory, NativeBattleGeometryRegisterStep registers) => memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static NativeBattleGeometryRegisters LoadPacked(NativeBattleGeometryRegisters registers, NativeBattlePackedMatrix32 packed, bool rotation, bool translation)
    {
        packed.Validate();
        if (rotation)
        {
            registers = registers with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        }
        if (translation)
        {
            int[] values = Enumerable.Range(0, 3).Select(axis =>
            {
                int offset = 20 + axis * 4;
                if (packed.Known.AsSpan(offset, 4).Contains(false))
                {
                    throw new NotSupportedException("Needle drawing requires its actual matrix translation DWORDs.");
                }
                return BinaryPrimitives.ReadInt32LittleEndian(packed.Bytes.AsSpan(offset, 4));
            }).ToArray();
            registers = registers with { Translation = values.Select(value => (int?)value).ToImmutableArray() };
        }
        return registers;
    }
}
