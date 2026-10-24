using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect76Lower;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect76MeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context92, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect76Meshes
{
    public static NativeBattleEffect76Frame Draw(NativeBattleEffect76Frame frame, uint temporary, uint source, uint cursorGlobal, bool custom, NativeBattleEffect76Resources resources, NativeBattleEffect76Inputs inputs)
    {
        if (custom)
        {
            NativeBattleEffect76MeshStep drawn = DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), Context(frame, temporary, 92), inputs.Colors);
            frame = frame with { Memory = drawn.Memory };
            frame = PublishContext(frame, temporary, drawn.Context92, 92);
            return inputs.Execute(frame, new NativeBattleEffect76DrawBatch(drawn.Batch, cursorGlobal));
        }
        NativeBattleStaticMeshContext88Step standard = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), Context(frame, temporary, 88), inputs.Colors);
        frame = frame with { Memory = standard.Memory };
        frame = PublishContext(frame, temporary, standard.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect76DrawBatch(standard.Batch, cursorGlobal));
    }

    public static NativeBattleEffect76MeshStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants)
    {
        uint flags = Dword(context, 0x1C);
        if (mesh.Address != 0xDE2470 || mesh.Bytes.Length != 376 || Dword(context, 0) != mesh.Address || flags is not (0x33 or 0xF3))
        {
            throw new NotSupportedException("Effect76 custom mesh requires its selected FT4 source and current finite flags.");
        }
        context = Long(Long(Long(context, 4, unchecked((int)(mesh.Address + 8))), 0x18, 0), 0x58, unchecked((int)0xE1000220));
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect76 mesh requires its current geometry registers."), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect76 mesh requires its actual projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect76 mesh requires its current primitive owner.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect76 mesh requires its actual far-color owner.")).PublishFarColor45DDA0(context.Read(8, 1)[0], context.Read(9, 1)[0], context.Read(10, 1)[0]);
        uint rgb = (uint)context.Read(8, 1)[0] | (uint)context.Read(9, 1)[0] << 8 | (uint)context.Read(10, 1)[0] << 16;
        short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Effect76 mesh requires its actual ZSF4.");
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor += 4;
            context = Long(context, 0x20, unchecked((int)(mesh.Address + (uint)cursor)));
            if (count != (group == 3 ? 8 : 0))
            {
                throw new NotSupportedException("Effect76 custom mesh leaves its reached FT4-only primitive directory.");
            }
            for (int index = 0; index < count; index++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, 24).ToArray();
                uint material = BinaryPrimitives.ReadUInt32LittleEndian(face);
                material = (flags & 1) != 0 ? material | 0x02000000 : material;
                material = (flags & 4) != 0 ? material & 0xFDFFFFFF : material;
                BinaryPrimitives.WriteUInt32LittleEndian(face, material);
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector position, short w) = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, position, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags!.Value;
                context = Long(context, 0x30, unchecked((int)firstFlags));
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    corners[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                if ((firstFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context = Long(context, 0x24, unchecked((int)registers.Geometry.Mac0!.Value));
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        (NativeBattleVector position, short w) = Vertex(3);
                        registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, position, w), projection, controls.Dqa, controls.Dqb);
                        corners[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, zsf4);
                    }
                    return corners[corner];
                }
                int cue = (flags & 0x40) != 0 ? unchecked((int)Dword(context, 12)) : 0;
                NativeBattleGfGeometryAttempt attempt = NativeBattleScanAltMeshPackets.EmitFace8DF200(face, ReadCorner, new(flags & ~3u, rgb, cue, null, null, 0, 17, 2), zsf4, constants, allocated);
                if (attempt.Primitive is { } primitive)
                {
                    uint depth = registers.Geometry.Otz!.Value;
                    context = Long(context, 0x2C, unchecked((int)depth));
                    if ((flags & 0x40) != 0)
                    {
                        registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, unchecked((uint)cue), far, constants).Registers };
                    }
                    attempt = attempt with { Primitive = primitive with { Bucket = unchecked(17 + ((int)depth >> 2)) } };
                    allocated += 44;
                }
                attempts.Add(attempt);
                cursor += 24;
                context = Long(context, 0x20, unchecked((int)(mesh.Address + (uint)cursor)));

                (NativeBattleVector Vertex, short W) Vertex(int corner)
                {
                    int offset = 8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    return (new(BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 4))), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 6)));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, context, new(attempts.ToImmutable(), allocated));
    }
}
