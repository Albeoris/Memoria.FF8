using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>6EB6A0's flat and Gouraud four-corner stream lists, with unconditional texture-window packets.</summary>
public static class NativeBattleStorm11Geometry
{
    public static NativeBattleGfGeometryBatch Emit6EB6A0(ImmutableArray<byte> source, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int phase, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (source.IsDefault || vertices.IsDefault || vertices.Length != 308 * 8 || phase is < 0 or >= 50)
        {
            throw new InvalidDataException("Native Storm Breath stream requires its complete current308-vertex owner and stream phase.");
        }
        int fade = phase < 8 ? 4096 - NativeBattleCameraMath.Sine(phase << 7) : phase > 42 ? NativeBattleCameraMath.Sine(unchecked(((phase << 7) - 0x1481) & 0x1FFFFF80)) : 0;
        return Emit(source, vertices, matrix, projection, phase, fade, colors, pilotIdentity);
    }

    public static NativeBattleGfGeometryBatch Emit6CF0C0(ImmutableArray<byte> source, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int phase, NativeBattleSummonColors colors)
    {
        if (source.IsDefault || vertices.IsDefault || vertices.Length != 308 * 8 || phase is < 0 or >= 70)
        {
            throw new InvalidDataException("Native control stream requires its complete308-vertex owner and phase.");
        }
        int fade = phase < 8 ? 4096 - NativeBattleCameraMath.Sine(phase << 7) : phase > 62 ? NativeBattleCameraMath.Sine(unchecked(((phase << 7) - 0x1E81) & 0x1FFFFF80)) : 0;
        return Emit(source, vertices, matrix, projection, phase, fade, colors);
    }

    private static NativeBattleGfGeometryBatch Emit(ImmutableArray<byte> source, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int phase, int fade, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ushort scroll = (ushort)((phase & 7) << 4);
        int cursor = checked(BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan()) + 12);
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 2; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor));
            cursor = checked(cursor + 4);
            int stride = group == 0 ? 24 : 36;
            if (count < 0 || count > (source.Length - cursor) / stride)
            {
                throw new InvalidDataException("Native Storm Breath polygon list exceeds its original source.");
            }
            for (int index = 0; index < count; index++)
            {
                ImmutableArray<byte> face = source.Slice(cursor, stride);
                bool gouraud = group != 0;
                NativeBattleEffectMeshWindow window = new(scroll, 0, new(0, 0, 256, 256), new(0, 0, 128, 256), true);
                NativeBattleEffectMeshPacketContext context = new(fade == 0 ? 0u : gouraud ? 0x80u : 0x40u, 0, fade, null, null, 0, false, null, 17, 2, window);
                NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitTextured(face.AsSpan(), Corner, true, gouraud, context, colors, allocated, NativeBattleEffectMeshClip.OutsideScreen);
                if (pilotIdentity is { } identity)
                {
                    System.Numerics.Vector3 World(int corner)
                    {
                        int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + corner * 2)) * 4;
                        ReadOnlySpan<byte> xyz = vertices.AsSpan().Slice(offset, 6);
                        NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
                        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, position), projection);
                    }
                    batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive && primitive.Words.Length > 1 && ((primitive.Words[1] >> 24) & 0xE0) == 0x20 ? attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, World(0), World(1), World(2), World(3), 4) } } : attempt).ToImmutableArray() };
                }
                attempts.AddRange(batch.Attempts);
                allocated = checked(allocated + batch.AllocationBytes);
                cursor = checked(cursor + stride);

                NativeBattleModuleProjectedVertex Corner(int corner)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + corner * 2)) * 4;
                    if (offset < 0 || offset > vertices.Length - 6)
                    {
                        throw new InvalidDataException("Native Storm Breath face references an absent current vertex.");
                    }
                    ReadOnlySpan<byte> xyz = vertices.AsSpan().Slice(offset, 6);
                    NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
                    return NativeBattleModuleProjection.Project45FE10(matrix, position, projection);
                }
            }
            if (group == 0)
            {
                cursor = checked(cursor + 12);
            }
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
