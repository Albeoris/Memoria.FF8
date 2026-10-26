using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218FragmentMeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context96, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect218FragmentMesh
{
    public static NativeBattleEffect218FragmentMeshStep Draw635280(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants)
    {
        context.Validate();
        if (context.Regions.Any(region => region.Offset < 0 || region.Offset > 96 - region.Data.Length) || context.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > 96 - region.Length) || Dword(context, 0) != mesh.Address || Dword(context, 0x1C) != 0x3C)
        {
            throw new InvalidDataException("Effect218 fragment drawing requires its actual addressed caller context.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect218 fragment drawing requires its current loaded geometry registers."), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect218 fragment drawing requires its current projection controls.");
        NativeBattlePrimitiveProcess primitive = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect218 fragment drawing requires its current renderer process.");
        NativeBattleModelRendererRegisters far = primitive.ModelRendererRegisters ?? throw new NotSupportedException("Effect218 fragment drawing requires its current far-color registers.");
        (byte[] rgb, bool[] rgbKnown) = context.ReadSnapshot(8, 3);
        far = far with { FarRed = rgbKnown[0] ? (uint)rgb[0] << 4 : null, FarGreen = rgbKnown[1] ? (uint)rgb[1] << 4 : null, FarBlue = rgbKnown[2] ? (uint)rgb[2] << 4 : null };
        context = Put(Put(context, 4, checked(mesh.Address + 8)), 0x18, 0);
        short originX = Word(context, 0x58);
        short originY = Word(context, 0x5A);
        short originZ = Word(context, 0x5C);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int vertexEnd = cursor;
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < lengths.Length; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            cursor = checked(cursor + 4);
            context = Put(context, 0x20, checked(mesh.Address + (uint)cursor));
            if (count <= 0)
            {
                continue;
            }
            if (group is not (2 or 6))
            {
                throw new NotSupportedException("Effect218 fragment drawing requires its selected original FT3 and GT3 resource groups.");
            }
            for (int index = 0; index < count; index++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, lengths[group]).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    int selected = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    int source = checked(8 + selected);
                    if (source > vertexEnd - 8)
                    {
                        throw new InvalidDataException("Effect218 fragment vertex leaves its original counted vertex section.");
                    }
                    byte[] raw = mesh.Bytes.AsSpan().Slice(source, 8).ToArray();
                    NativeBattleVector point = new(unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(raw) - originX)), unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2)) - originY)), unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(4)) - originZ)));
                    BinaryPrimitives.WriteInt16LittleEndian(raw, point.X);
                    BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), point.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), point.Z);
                    context = context.Write(0x34 + corner * 8, raw);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, point, BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(6)));
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint flags = registers.Colors.Flags!.Value;
                context = Put(context, 0x30, flags);
                NativeBattleModuleProjectedVertex[] projected = Enumerable.Range(0, 3).Select(corner => new NativeBattleModuleProjectedVertex(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), flags)).ToArray();
                if ((flags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context = Put(context, 0x24, registers.Geometry.Mac0!.Value);
                    registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect218 fragment drawing requires its current ZSF3 control."));
                }
                NativeBattleEffectMeshPacketContext packetContext = new(0x3C, null, null, null, null, 0, true, null, 17, 2);
                NativeBattleGfGeometryBatch emitted = NativeBattleEffectMeshPackets.EmitTextured(face, corner => projected[corner], false, group == 6, packetContext, constants, allocated);
                foreach (NativeBattleGfGeometryAttempt original in emitted.Attempts)
                {
                    NativeBattleGfGeometryAttempt attempt = original;
                    if (attempt.Primitive is not null)
                    {
                        uint depth = registers.Geometry.Otz!.Value;
                        context = Put(context, 0x2C, depth);
                        attempt = attempt with { Primitive = attempt.Primitive with { Bucket = unchecked(17 + (unchecked((int)depth) >> 2)) } };
                        allocated = checked(allocated + attempt.PacketLength);
                    }
                    attempts.Add(attempt);
                }
                cursor = checked(cursor + lengths[group]);
                context = Put(context, 0x20, checked(mesh.Address + (uint)cursor));
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = far } };
        return new(memory, context, new(attempts.ToImmutable(), allocated));
    }

    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static short Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadInt16LittleEndian(context.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
}
