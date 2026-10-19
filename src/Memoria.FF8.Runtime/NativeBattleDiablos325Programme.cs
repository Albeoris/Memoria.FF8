using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325ProgrammeDraw(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets);

public static partial class NativeBattleDiablos325Programme
{
    private static readonly int[] Lengths = [12, 12, 20, 24, 20, 24, 28, 36];

    public static NativeBattleDiablos325ProgrammeDraw Advance654654(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        int counter = unchecked((short)task.ReadWord(0xC));
        if (counter is < 9 or > 245)
        {
            throw new NotSupportedException("Native Diablos opening programme requires its current reached timeline window.");
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleDiablos325Resources resources = frame.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos requires its original opening programme resources.");
        if (resources.PrimitiveKinds.Length != 4 || resources.TextureKinds.Length != 4)
        {
            throw new NotSupportedException("Native Diablos requires its two original geometry selector tables.");
        }
        _ = projection.View ?? throw new NotSupportedException("Native Diablos programme requires its current camera view.");
        short fade = counter < 17 ? checked((short)(256 + (17 - counter) * 512)) : (short)0;
        NativeBattleEffectMemory memory = frame.Memory;
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x264 - 12, 0xE0));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.OpeningProgramme, unchecked((int)state.Pause5180), (current, node) =>
        {
            NativeBattleDiablos325NodeDraw drawn = Emit656B70(memory, resources.OpeningProgramme, node, projection, colors, new(0, 0, unchecked((short)state.MidZ51E0)), 512, fade, resources.PrimitiveKinds, resources.TextureKinds);
            memory = drawn.Memory;
            attempts.AddRange(drawn.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + drawn.Packets.AllocationBytes);
            return current;
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
