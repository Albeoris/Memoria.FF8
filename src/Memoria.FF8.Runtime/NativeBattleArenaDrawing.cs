using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Common host surface binding for native packet producers and deferred surface images.</summary>
public static class NativeBattleArenaDrawing
{
    public static NativeBattleLogicalOrdering BindCaptureRegion(NativeBattleLogicalOrdering ordering, NativeBattleArenaCaptureMaterial material)
    {
        material.Validate();
        ImmutableArray<NativeBattleLogicalPacket> packets = ordering.Packets.Select(packet => !packet.Pilot.IsEmpty && SamplesCapture(packet.Words, material) ? packet with { PilotGeometry = BindCapture(packet.PilotGeometry!, packet.Words, material) } : packet).ToImmutableArray();
        return new(ordering.Table.ToImmutableArray(), ordering.Proxies.ToImmutableArray(), packets, ordering.ProxyCursor, ordering.TableBuffer, ordering.OtherTable?.ToImmutableArray());
    }

    public static NativeBattleGfGeometryBatch BindCapture(NativeBattleGfGeometryBatch batch, NativeBattleArenaCaptureMaterial material)
    {
        material.Validate();
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = BindCapture(primitive, material) } : attempt).ToImmutableArray() };
    }

    public static NativeBattleGfGeometryBatch BindCaptureRegion(NativeBattleGfGeometryBatch batch, NativeBattleArenaCaptureMaterial material)
    {
        material.Validate();
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive && SamplesCapture(primitive.Words, material) ? attempt with { Primitive = BindCapture(primitive, material) } : attempt).ToImmutableArray() };
    }

    private static bool SamplesCapture(ImmutableArray<uint> words, NativeBattleArenaCaptureMaterial material)
    {
        words = PolygonWords(words);
        if (words.Length < 2 || ((words[1] >> 24) & 0xE4) != 0x24)
        {
            return false;
        }
        byte command = (byte)(words[1] >> 24);
        int corners = (command & 8) != 0 ? 4 : 3;
        int step = (command & 16) != 0 ? 3 : 2;
        if (words.Length <= 3 + (corners - 1) * step)
        {
            throw new InvalidDataException("Arena capture sampling requires every textured polygon corner.");
        }
        ushort page = (ushort)(words[3 + step] >> 16);
        int depth = page >> 7 & 3;
        if (depth == 3)
        {
            throw new NotSupportedException("Arena capture sampling requires a supported native texture depth.");
        }
        int pixelsPerWord = 4 >> depth;
        for (int corner = 0; corner < corners; corner++)
        {
            uint value = words[3 + corner * step];
            float x = (page & 15) * 64 + (value & 255) / (float)pixelsPerWord;
            int y = (page & 16) * 16 + (int)(value >> 8 & 255);
            if (x < material.NativeX || x > material.NativeX + material.Width || y < material.NativeY || y > material.NativeY + material.Height)
            {
                return false;
            }
        }
        return true;
    }

    private static NativeBattleGeometryPrimitive BindCapture(NativeBattleGeometryPrimitive primitive, NativeBattleArenaCaptureMaterial material)
    {
        NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry ?? throw new NotSupportedException("Arena image sampling requires retained primitive geometry.");
        return primitive with { PilotGeometry = BindCapture(geometry, primitive.Words, material) };
    }

    public static NativeBattlePilotVisualGeometry BindCapture(NativeBattlePilotVisualGeometry geometry, ImmutableArray<uint> words, NativeBattleArenaCaptureMaterial material)
    {
        words = PolygonWords(words);
        byte command = (byte)(words[1] >> 24);
        if ((command & 0xE4) != 0x24)
        {
            throw new NotSupportedException("Arena image sampling requires a textured native polygon.");
        }
        int corners = (command & 8) != 0 ? 4 : 3;
        int step = (command & 16) != 0 ? 3 : 2;
        ushort page = (ushort)(words[3 + step] >> 16);
        int depth = page >> 7 & 3;
        int pixelsPerWord = depth == 0 ? 4 : depth == 1 ? 2 : depth == 2 ? 1 : throw new NotSupportedException("Arena image sampling requires a supported native texture depth.");
        ImmutableArray<Vector2>.Builder uv = ImmutableArray.CreateBuilder<Vector2>(corners);
        for (int corner = 0; corner < corners; corner++)
        {
            uint value = words[3 + corner * step];
            uv.Add(new(((page & 15) * 64 - material.NativeX + (value & 255) / (float)pixelsPerWord) / material.Width, ((page & 16) * 16 - material.NativeY + (value >> 8 & 255)) / (float)material.Height));
        }
        return geometry with { ForeignSurface = new(material, uv.ToImmutable(), Enumerable.Repeat(Vector4.One, corners).ToImmutableArray()) };
    }

    private static ImmutableArray<uint> PolygonWords(ImmutableArray<uint> words)
    {
        int command = 1;
        while (command < words.Length && words[command] >> 24 is >= 0xE1 and <= 0xE6)
        {
            command++;
        }
        return words.Slice(command - 1, words.Length - command + 1);
    }

    public static NativeBattleGfGeometryBatch Deform<TVertex>(NativeBattleArenaGeometry geometry, Func<NativeBattleVector, TVertex> deform, NativeBattlePilotVisualIdentity identity, Func<ImmutableArray<NativeBattleRawPolygon>, ImmutableArray<TVertex>, NativeBattlePilotVisualIdentity, NativeBattleGfGeometryBatch> emit)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleArenaFace face = geometry.Faces[index];
            NativeBattleForeignModelFace nativeFace = new(NativePoint(face.A), NativePoint(face.B), NativePoint(face.C), face.Surface);
            NativeBattleRawMesh triangle = nativeFace.Triangle();
            byte[] polygon = new byte[20];
            triangle.Polygons[0].Bytes.AsSpan().CopyTo(polygon);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(16), 0x24808080);
            ImmutableArray<TVertex> vertices = [deform(nativeFace.A), deform(nativeFace.B), deform(nativeFace.C)];
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleGfGeometryBatch batch = emit([new(false, polygon.ToImmutableArray())], vertices, selected);
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                NativeBattleGeometryPrimitive? primitive = attempt.Primitive;
                if (primitive?.PilotGeometry is { } pilot)
                {
                    primitive = primitive with { PilotGeometry = pilot with { ForeignSurface = face.Surface } };
                }
                attempts.Add(attempt with { ByteOffsetRelative = allocated + attempt.ByteOffsetRelative, Primitive = primitive });
            }
            allocated += batch.AllocationBytes;
        }
        return new(attempts.ToImmutable(), allocated);
    }

    private static NativeBattleVector NativePoint(Vector3 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z) || point.X < short.MinValue || point.X > short.MaxValue || point.Y < short.MinValue || point.Y > short.MaxValue || point.Z < short.MinValue || point.Z > short.MaxValue)
        {
            throw new NotSupportedException("Native arena deformation requires local surface vertices within its signed coordinate domain.");
        }
        return new(checked((short)NativePrecision53.RoundInteger(point.X)), checked((short)NativePrecision53.RoundInteger(point.Y)), checked((short)NativePrecision53.RoundInteger(point.Z)));
    }
}
