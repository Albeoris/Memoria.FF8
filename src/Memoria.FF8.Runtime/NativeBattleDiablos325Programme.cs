using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325ProgrammeDraw(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleDiablos325Programme
{
    private static readonly int[] Lengths = [12, 12, 20, 24, 20, 24, 28, 36];

    public static NativeBattleDiablos325ProgrammeDraw Advance654654(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        int counter = unchecked((short)task.ReadWord(0xC));
        if (counter is < 9 or > 30)
        {
            throw new NotSupportedException("Native Diablos opening programme requires its current reached timeline window.");
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleDiablos325Resources resources = frame.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos requires its original opening programme resources.");
        if (resources.PrimitiveKinds.Length != 4 || resources.TextureKinds.Length != 4)
        {
            throw new NotSupportedException("Native Diablos requires its two original geometry selector tables.");
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos programme requires its current camera view.");
        short fade = counter < 17 ? checked((short)(256 + (17 - counter) * 512)) : (short)0;
        NativeBattleEffectMemory memory = frame.Memory;
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x264 - 12, 0xE0));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.OpeningProgramme, unchecked((int)state.Pause5180), (current, node) =>
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return current;
            }
            int index = unchecked((short)node.GeometryIndex);
            if ((uint)index >= resources.PrimitiveKinds.Length)
            {
                throw new InvalidDataException("Native Diablos programme geometry index leaves its original selector tables.");
            }
            if (resources.TextureKinds[index] == 0x31)
            {
                throw new NotSupportedException("Native Diablos special texture window requires its actually produced phase BYTE.");
            }
            NativeBattleGfAnimationSource source = resources.OpeningProgramme;
            int geometry = Int(source.Data, checked(8 + index * 4));
            int count = Int(source.Data, checked(geometry + 4));
            int selected = 0;
            ImmutableArray<NativeBattleVector> morph = [];
            if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
            {
                int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                selected = checked(geometry + 12 + shape * count * 8);
            }
            else
            {
                morph = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38);
                NativeBattleEffectArenaWriter writer = new(memory.Arena ?? throw new NotSupportedException("Native Diablos morph requires its current effect arena."));
                for (int vertex = 0; vertex < morph.Length; vertex++)
                {
                    byte[] xyz = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(xyz, morph[vertex].X);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), morph[vertex].Y);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), morph[vertex].Z);
                    writer.Write(checked(0xD94 + vertex * 8), xyz);
                }
                memory = memory with { Arena = writer.Freeze() };
            }
            uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
            if ((node.Header & 0x2000) != 0)
            {
                flags |= 12;
            }
            uint? far = node.Scalar36 != 0 ? node.Color32 : null;
            int cue = node.Scalar36;
            if (node.Scalar36 != 0)
            {
                flags |= 0xC0;
            }
            if (fade != 0)
            {
                flags |= 0xC0;
                far = 0;
                cue = fade;
            }
            NativeBattleActionMatrix matrix = Compose656B70(node, new(0, 0, unchecked((short)state.MidZ51E0)), view);
            NativeBattleGfGeometryContext context = new(flags, far ?? 0, cue, null, null, 512, 17, 2);
            NativeBattleGfGeometryBatch batch = resources.PrimitiveKinds[index] == 0x30
                ? NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, default, matrix, projection, context, colors, Vertex)
                : Alternate(source.Data, geometry, matrix, projection, context, colors, Vertex);
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos programme requires its current far-color controls.");
            NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
            registers = far is uint rgb ? registers.PublishModelColor(rgb) : registers with { FarRed = null, FarGreen = null, FarBlue = null };
            memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers } };
            attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + batch.AllocationBytes);
            return current;

            NativeBattleVector Vertex(int offset)
            {
                if (offset < 0 || (offset & 7) != 0 || offset / 8 >= count)
                {
                    throw new InvalidDataException("Native Diablos selected vertex leaves its actual shape bank.");
                }
                if (!morph.IsEmpty)
                {
                    return morph[offset / 8];
                }
                ReadOnlySpan<byte> bytes = source.Data.AsSpan().Slice(checked(selected + offset), 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
            }
        });
        byte[] payload = task.Payload.ToArray();
        advanced.Workspace.Data.AsSpan().CopyTo(payload.AsSpan(0x264 - 12, 0xE0));
        return new(memory, task with { Payload = payload.ToImmutableArray() }, new(attempts.ToImmutable(), allocated));
    }

    public static NativeBattleActionMatrix Compose656B70(NativeBattleGfAnimationNode node, NativeBattleVector position, NativeBattleActionMatrix view, NativeBattleVector? contextScale = null, NativeBattleActionMatrix? effectCamera = null)
    {
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = node.Vector8;
        if (contextScale is NativeBattleVector scale)
        {
            offset = new(unchecked((short)(offset.X * scale.X >> 12)), unchecked((short)(offset.Y * scale.Y >> 12)), unchecked((short)(offset.Z * scale.Z >> 12)));
        }
        if ((node.Header & 0x800) != 0)
        {
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(rotation, offset);
            offset = new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
        }
        if (contextScale is NativeBattleVector matrixScale)
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, matrixScale.X, matrixScale.Y, matrixScale.Z);
        }
        (int X, int Y, int Z) translated;
        if ((node.Header & 0x1000) != 0)
        {
            NativeBattleActionMatrix basis = effectCamera ?? throw new NotSupportedException("Native Diablos billboard node requires its actually published effect camera matrix.");
            var first = NativeBattleGfMatrices.Rotate56C3D0(view, position);
            var second = NativeBattleGfMatrices.Rotate56C3D0(basis, offset);
            translated = (unchecked(first.X + second.X), unchecked(first.Y + second.Y), unchecked(first.Z + second.Z));
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        else if ((node.Header & 0x200) != 0)
        {
            var first = NativeBattleGfMatrices.Rotate56C3D0(view, position);
            translated = (unchecked(first.X + offset.X), unchecked(first.Y + offset.Y), unchecked(first.Z + offset.Z));
        }
        else
        {
            NativeBattleVector combined = new(unchecked((short)(position.X + offset.X)), unchecked((short)(position.Y + offset.Y)), unchecked((short)(position.Z + offset.Z)));
            translated = NativeBattleGfMatrices.Rotate56C3D0(view, combined);
            rotation = NativeBattleGfMatrices.Multiply56C090(view, rotation);
        }
        NativeBattleVector localScale = node.Vector24;
        if (localScale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(rotation, new([localScale.X, 0, 0, 0, localScale.Y, 0, 0, 0, localScale.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(rotation, localScale.X, localScale.Y, localScale.Z);
        }
        return rotation with { X = unchecked(view.X + translated.X), Y = unchecked(view.Y + translated.Y), Z = unchecked(view.Z + translated.Z) };
    }

    private static NativeBattleGfGeometryBatch Alternate(ImmutableArray<byte> source, int geometry, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleGfGeometryContext context, NativeBattleSummonColors colors, Func<int, NativeBattleVector> vertex)
    {
        int cursor = checked(geometry + Int(source, geometry));
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        Dictionary<ushort, NativeBattleModuleProjectedVertex> projected = [];
        for (int group = 0; group < Lengths.Length; group++)
        {
            int count = Int(source, cursor);
            cursor = checked(cursor + 4);
            for (int index = 0; index < count; index++)
            {
                byte[] face = source.AsSpan().Slice(cursor, Lengths[group]).ToArray();
                Corners corners = new(face, offset =>
                {
                    if (!projected.TryGetValue(offset, out NativeBattleModuleProjectedVertex result))
                    {
                        result = NativeBattleModuleProjection.Project45FE10(matrix, vertex(offset * 4), projection);
                        projected.Add(offset, result);
                    }
                    return result;
                });
                NativeBattleGfGeometryBatch batch = NativeBattleDiablos325AlternatePackets.Emit657030(face, corners, group, context, colors);
                attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
                allocated = checked(allocated + batch.AllocationBytes);
                cursor = checked(cursor + Lengths[group]);
            }
        }
        return new(attempts.ToImmutable(), allocated);
    }

    private static int Int(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private sealed class Corners(byte[] face, Func<ushort, NativeBattleModuleProjectedVertex> read) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => 4;
        public NativeBattleModuleProjectedVertex this[int index] => read(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + index * 2, 2)));
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, 4).Select(index => this[index]).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
