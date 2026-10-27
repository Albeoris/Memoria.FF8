using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect242PrimitiveDepthTests
{
    [Theory]
    [MemberData(nameof(FractionalCases))]
    public void Selected_emit_routes_truncate_fractional_depth_and_preserve_rejected_writes(short type, uint handler, int packetLength, float scale, bool reject)
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame().WriteTemporaryDword(0x60, 0x5000);
        NativeBattleWaterMeshGroup group = Group(type);
        ImmutableArray<byte> records = Records(reject);
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown();
        NativeBattleScriptedColorRegisters colors = new([7, 8, 9], [11, 12, 13], [0x00112233, 0x00445566, 0x00778899], null);
        NativeBattleScriptedMeshSubmission Run(NativeBattleWaterExecutionFrame current, NativeBattleScriptedColorRegisters currentColors, NativeBattleGeometryRegisters currentGeometry) => NativeBattleScriptedMeshPackets.EmitSelected(current, group, handler, 0, records, [0xABCD000B, 0xFFFF0017, 35, 47], current.ReadTemporaryDword(0x60), NativeBattleScriptedLightingMemory.Unknown(), currentColors, pointer => checked((int)(pointer / 4)), currentGeometry, scale);
        NativeBattleScriptedMeshSubmission result = Run(frame, colors, geometry);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Equal(packetLength, attempt.Data.Length);
        Assert.Equal(reject ? 0 : packetLength, result.Packets.AllocationBytes);
        Assert.Equal((uint)(0x5000 + (reject ? 0 : packetLength)), result.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(new uint?[] { 0, 10, 20 }, result.Geometry!.Sxy);
        Assert.Equal(0u, result.Geometry.Mac0);
        Assert.Equal(geometry.Rotation20, result.Geometry.Rotation20);
        Assert.Equal(geometry.Translation, result.Geometry.Translation);
        Assert.Equal(geometry.V1, result.Geometry.V1);
        Assert.Equal(geometry.V2, result.Geometry.V2);
        int[] offsets = ScreenOffsets(type);
        foreach (int offset in offsets)
        {
            Assert.All(attempt.WriteMask.Skip(offset).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        }
        if (reject)
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(offsets.Length * 4, attempt.WriteMask.Count(state => state == NativeBattleGfPacketByteState.Known));
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[3]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[7]);
            Assert.Equal(colors.Mac, result.Registers.Mac);
            Assert.Equal(colors.Ir, result.Registers.Ir);
            Assert.Equal(colors.Rgb, result.Registers.Rgb);
            Assert.Equal(0u, result.Registers.Flags);
        }
        else
        {
            int sign = scale < 0 ? -1 : 1;
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.Equal(new[] { 5 * sign, 11 * sign, 17 * sign, type == 17 ? 23 * sign : 0 }, primitive.VertexPayload4);
            Assert.Equal((byte)(packetLength / 4 - 1), attempt.Data[3]);
            Assert.Equal(type == 9 ? (byte)0x34 : type == 17 ? (byte)0x38 : type == 7 ? (byte)0x30 : (byte)0x20, attempt.Data[type == 9 ? 7 : 15]);
        }
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result));
        NativeBattleScriptedMeshSubmission restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleScriptedMeshSubmission>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        NativeBattleScriptedMeshSubmission next = Run(result.Frame, result.Registers, result.Geometry!);
        NativeBattleScriptedMeshSubmission restoredNext = Run(restored.Frame, restored.Registers, restored.Geometry!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(next)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restoredNext)));
    }

    [Theory]
    [InlineData((short)6, 0xAEB5B0u)]
    [InlineData((short)7, 0xB27E90u)]
    [InlineData((short)9, 0xB283B0u)]
    [InlineData((short)17, 0xB28B00u)]
    public void Signed64_conversion_consumes_low_dword_instead_of_clamping_to_int32(short type, uint handler)
    {
        NativeBattleScriptedMeshSubmission result = NativeBattleScriptedMeshPackets.EmitSelected(NativeBattleWaterMeshPacketTests.Frame(), Group(type), handler, 0, Records(false), [65535, 65534, 32769, 1], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => checked((int)(pointer / 4)), NativeBattleGeometryRegisters.Unknown(), 65536f);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.Equal(new[] { -65536, -131072, -2147418112, type == 17 ? 65536 : 0 }, primitive.VertexPayload4);
    }

    [Theory]
    [InlineData((short)7)]
    [InlineData((short)9)]
    [InlineData((short)17)]
    public void Direct_shared_packet_leaf_truncates_negative_halves_without_changing_ot(short type)
    {
        uint? ot = null;
        int Resolve(uint pointer)
        {
            ot = pointer;
            return 27;
        }
        NativeBattleWaterMeshResult result = NativeBattleWaterMeshPackets.EmitSelected(NativeBattleWaterMeshPacketTests.Frame(), Group(type), Records(false), [11, 23, 35, 47], 0x5000, Resolve, depthScale: -0.5f);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.Equal(new[] { -5, -11, -17, type == 17 ? -23 : 0 }, primitive.VertexPayload4);
        Assert.Equal(27, primitive.Bucket);
        Assert.Equal(0x1D8E058u + 17 * 4 + 100, ot);
    }

    public static IEnumerable<object[]> FractionalCases()
    {
        (short Type, uint Handler, int PacketLength)[] formats = [(6, 0xAEB5B0, 28), (7, 0xB27E90, 36), (9, 0xB283B0, 40), (17, 0xB28B00, 44)];
        foreach (var format in formats)
        {
            foreach (float scale in new[] { 0.5f, -0.5f })
            {
                foreach (bool reject in new[] { false, true })
                {
                    yield return [format.Type, format.Handler, format.PacketLength, scale, reject];
                }
            }
        }
    }

    private static NativeBattleWaterMeshGroup Group(short type)
    {
        (int stride, int corners, int indices) = type switch
        {
            6 => (12, 3, 4),
            7 => (20, 3, 12),
            9 => (28, 3, 18),
            17 => (24, 4, 16),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        byte[] face = new byte[stride];
        for (int corner = 0; corner < (type == 6 ? 1 : corners); corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(corner * 4), 0x00808080);
        }
        for (int corner = 0; corner < corners; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(indices + corner * 2), (ushort)(corner * 8));
        }
        return new(type, 1, face.ToImmutableArray());
    }

    private static ImmutableArray<byte> Records(bool reject)
    {
        byte[] records = new byte[32];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(corner * 8), (uint)(corner * 10));
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(corner * 8 + 4), reject && corner == 0 ? 0x00460064u : 100u);
        }
        return records.ToImmutableArray();
    }

    private static int[] ScreenOffsets(short type) => type switch
    {
        6 => [16, 20, 24],
        7 => [16, 24, 32],
        9 => [8, 20, 32],
        17 => [16, 24, 32, 40],
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
