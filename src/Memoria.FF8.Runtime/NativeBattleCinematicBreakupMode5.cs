using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicBreakupMode5Step(NativeBattleWaterExecutionFrame Frame, ImmutableArray<byte> Anchors);

public static class NativeBattleCinematicBreakupMode5
{
    public static NativeBattleCinematicBreakupMode5Step Initialize(NativeBattleWaterExecutionFrame frame, NativeBattleVector anchor, IReadOnlyList<NativeBattleControl18Face> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        byte[] records = new byte[checked(faces.Count * 4)];
        frame = frame.WriteTemporaryWord(0xD0, unchecked((ushort)anchor.X)).WriteTemporaryWord(0xD2, unchecked((ushort)anchor.Y)).WriteTemporaryWord(0xD4, unchecked((ushort)anchor.Z));
        for (int index = 0; index < faces.Count; index++)
        {
            NativeBattleControl18Face face = faces[index];
            int corners = face.Quad ? 4 : 3;
            if (face.Vertices.IsDefault || face.Vertices.Length != corners)
            {
                throw new InvalidDataException("Native radial breakup initialization requires each polygon's complete posed corners.");
            }
            int centerX = face.Vertices.Sum(value => value.X) / corners;
            int centerZ = face.Vertices.Sum(value => value.Z) / corners;
            (frame, int angle) = NativeBattleCinematicPath179.DirectionB66B30(frame, anchor.X, anchor.Z, centerX, centerZ);
            BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(index * 4), unchecked((ushort)angle));
            BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(index * 4 + 2), unchecked((ushort)frame.ReadTemporaryDword(0xFC)));
        }
        return new(frame, records.ToImmutableArray());
    }

    public static NativeBattleControl18SpawnStep Advance(ReadOnlySpan<byte> records, ReadOnlySpan<byte> polygonStates, ushort liveCount, ReadOnlySpan<byte> payload, IReadOnlyList<NativeBattleControl18Face> faces, short threshold, ReadOnlySpan<byte> anchors, NativeCrtRandomState random, NativeBattleControl18SpawnServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (payload.Length < 0x4C || BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(8, 2)) != 5)
        {
            throw new NotSupportedException("Native radial breakup requires its actual mode-five control payload.");
        }
        return NativeBattleControl18Spawn.Advance(records, polygonStates, liveCount, payload, faces, threshold, random, services with { Mode5Anchors = anchors.ToArray().ToImmutableArray() });
    }
}
