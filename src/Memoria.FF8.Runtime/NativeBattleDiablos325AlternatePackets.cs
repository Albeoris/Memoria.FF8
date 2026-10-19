using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>657160/65CE80's three original linked commands and physical allocation strides.</summary>
public static class NativeBattleDiablos325AlternatePackets
{
    public static NativeBattleGfGeometryBatch Emit657030(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, int group, NativeBattleGfGeometryContext context, NativeBattleSummonColors? colors = null, Action<ushort>? observeDepth = null)
    {
        if (group is not (1 or 3 or 5 or 7))
        {
            throw new NotSupportedException("Native Diablos alternate packets require their reached flat or colored quad leaf.");
        }
        NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(face, vertices, group, context, colors, observeDepth: observeDepth);
        if (attempt.Primitive is not { } primitive)
        {
            return new([attempt], 0);
        }
        int body = attempt.PacketLength;
        if (group is 3 or 7)
        {
            NativeBattleGfGeometryAttempt mode = Mode(body, 8, 0xE1000220, primitive.Bucket);
            return new([mode, attempt], checked(body + 8));
        }
        int allocation = group == 1 ? 40 : 88;
        NativeBattleGfGeometryAttempt before = Mode(body, 8, 0xE1000220, primitive.Bucket);
        NativeBattleGfGeometryAttempt after = Mode(body + 8, allocation - body - 8, 0xE1000240, primitive.Bucket);
        return new([before, attempt, after], allocation);
    }

    private static NativeBattleGfGeometryAttempt Mode(int offset, int reserved, uint command, int bucket)
    {
        byte[] bytes = new byte[reserved];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x1000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, reserved).Select(index => index < 8 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
        return new(offset, bytes.ToImmutableArray(), mask, new(bucket, [0x1000000, command], [0, 0, 0, 0]));
    }
}
