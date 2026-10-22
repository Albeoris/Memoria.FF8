using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8SecondaryColorTests
{
    public static IEnumerable<object[]> FadeCases()
    {
        foreach (bool quad in new[] { false, true })
        {
            yield return [quad, (byte)0];
            for (byte fade = 4; fade <= 60; fade += 4)
            {
                yield return [quad, fade];
            }
        }
    }

    [Theory]
    [MemberData(nameof(FadeCases))]
    public void Actual_Doom_fades_retain_secondary_bytes_and_primary_packet_state_after_JSON(bool quad, byte fade)
    {
        const uint primary = 0x02010203;
        const uint page = 0x003F1122;
        NativeBattleEffect20BodyPreparation baseline = Prepare(0, primary);
        NativeBattleEffect20BodyPreparation current = Prepare(fade, primary);
        Assert.Equal(new byte[] { fade, fade, fade }, current.Temporary8C.Read(0x88, 3));
        Assert.Throws<NotSupportedException>(() => current.Temporary8C.Read(0x8B, 1));
        Assert.Equal(primary, Dword(current.Temporary8C, 0x7C));
        Assert.Equal(JsonSerializer.Serialize(baseline.Memory.GeometryRegisters), JsonSerializer.Serialize(current.Memory.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(baseline.Memory.ScriptedColorRegisters), JsonSerializer.Serialize(current.Memory.ScriptedColorRegisters));
        Assert.Equal(JsonSerializer.Serialize(baseline.Memory.PrimitiveProcess), JsonSerializer.Serialize(current.Memory.PrimitiveProcess));
        NativeBattleModelRendererRegisters far = current.Memory.PrimitiveProcess!.ModelRendererRegisters!;
        Assert.Equal((uint?)48, far.FarRed);
        Assert.Equal((uint?)32, far.FarGreen);
        Assert.Equal((uint?)16, far.FarBlue);
        Assert.Equal(0xABCDu, far.SourceCursor);
        NativeBattleGeometryPrimitive packet = Emit(current, quad, page);
        Assert.Equal(JsonSerializer.Serialize(Emit(baseline, quad, page)), JsonSerializer.Serialize(packet));
        Assert.Equal(quad ? 0x2E010203u : 0x26010203u, packet.Words[1]);
        Assert.Equal(page, packet.Words[5]);
        Assert.NotEqual(page + 0x400000, packet.Words[5]);
        Assert.Equal(23, packet.Bucket);
        Assert.Equal(quad ? new[] { 101, 201, 301, 401 } : new[] { 101, 201, 301, 0 }, packet.VertexPayload4);
        Assert.Equal(quad ? 40 : 32, packet.Words.Length * 4);
        Assert.Equal(quad ? 0x09000000u : 0x07000000u, packet.Words[0]);
        NativeBattlePrimitiveProcess stored = Store(packet, current.Memory.PrimitiveProcess!);
        Assert.Equal(page, BinaryPrimitives.ReadUInt32LittleEndian(stored.ReadCommonPacket(20, 4)));
        Assert.Throws<NotSupportedException>(() => stored.ReadCommonPacket(30, 1));
        Assert.Equal(new byte[] { 0xA5 }, stored.ReadCommonPacket(31, 1));
        if (quad)
        {
            Assert.Throws<NotSupportedException>(() => stored.ReadCommonPacket(38, 1));
            Assert.Equal(new byte[] { 0xA5 }, stored.ReadCommonPacket(39, 1));
        }
        NativeBattleEffect20BodyPreparation cold = JsonSerializer.Deserialize<NativeBattleEffect20BodyPreparation>(JsonSerializer.Serialize(current))!;
        Assert.Equal(new byte[] { fade, fade, fade }, cold.Temporary8C.Read(0x88, 3));
        Assert.Throws<NotSupportedException>(() => cold.Temporary8C.Read(0x8B, 1));
        Assert.Equal(JsonSerializer.Serialize(packet), JsonSerializer.Serialize(Emit(cold, quad, page)));
        Assert.Equal(JsonSerializer.Serialize(stored), JsonSerializer.Serialize(Store(Emit(cold, quad, page), cold.Memory.PrimitiveProcess!)));
    }

    [Theory]
    [InlineData(false, 0x00010203u, 0x003F1122u, 0x24010203u)]
    [InlineData(true, 0x00010203u, 0x003F1122u, 0x2C010203u)]
    [InlineData(false, 0x00010203u, 0x02001122u, 0x26010203u)]
    [InlineData(true, 0x00010203u, 0x02001122u, 0x2E010203u)]
    [InlineData(false, 0x02010203u, 0xFE001122u, 0x26010203u)]
    [InlineData(true, 0x02010203u, 0xFE001122u, 0x2E010203u)]
    public void Secondary_bytes_do_not_change_native_primary_semi_flags_or_texture_page(bool quad, uint primary, uint page, uint command)
    {
        NativeBattleEffect20BodyPreparation current = Prepare(60, primary);
        NativeBattleEffect20BodyPreparation blueOnly = current with { Temporary8C = current.Temporary8C.Write(0x88, [0, 0, 1]) };
        NativeBattleGeometryPrimitive expected = Emit(Prepare(0, primary), quad, page);
        NativeBattleGeometryPrimitive actual = Emit(current, quad, page);
        Assert.Equal(command, actual.Words[1]);
        Assert.Equal(page, actual.Words[5]);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Emit(blueOnly, quad, page)));
        Assert.Equal(new byte[] { 0, 0, 1 }, blueOnly.Temporary8C.Read(0x88, 3));
    }

    private static NativeBattleEffect20BodyPreparation Prepare(byte fade, uint primary)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().Publish50FDF0(0x1234, 0xABCD);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [null, 0x11223344, 0x55667788] }, PrimitiveProcess = process };
        NativeBattleModulePoolTask task = new(0, 0, null, 0x8A2C70, new byte[140].ToImmutableArray());
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 140).ToArray()).MarkUnknown(0x8B, 1);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleEffect20BodyInputs inputs = new(identity with { X = 100, Y = 200, Z = 3000 }, 0x1234, identity, NativeBattlePackedMatrix32.FromMatrix(identity, 0x5678), 0x1968000, NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]), primary, fade, (current, _) => current);
        NativeBattleEffect20BodyPreparation result = NativeBattleEffect20BodyRendering.Prepare(memory, task, retained, inputs);
        return result with { Memory = result.Memory with { PrimitiveProcess = result.Memory.PrimitiveProcess!.PublishModelColor(Dword(result.Temporary8C, 0x7C)) } };
    }

    private static NativeBattleGeometryPrimitive Emit(NativeBattleEffect20BodyPreparation prepared, bool quad, uint page)
    {
        byte[] face = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(face, 0x9000);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), quad ? (ushort)3 : (ushort)0x7856);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), page);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xABCD4321);
        }
        NativeBattleProjectedVertex[] vertices = [new(XY(80, 80), 100), new(XY(160, 80), 200), new(XY(80, 160), 300), new(XY(160, 160), 400)];
        return NativeBattleGeometryPackets.EmitRaw50FDF0(face, vertices, quad, Dword(prepared.Temporary8C, 0x7C), 17, 4) ?? throw new InvalidOperationException("The original-format fixture must face the camera.");
    }

    private static NativeBattlePrimitiveProcess Store(NativeBattleGeometryPrimitive primitive, NativeBattlePrimitiveProcess process)
    {
        int length = primitive.Words.Length * 4;
        process = process.ApplyCommonWrite(0, Enumerable.Repeat((byte)0xA5, length).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, length).ToImmutableArray()).MarkCommonUnknown(30, 1);
        if (length == 40)
        {
            process = process.MarkCommonUnknown(38, 1);
        }
        return NativeBattleCommonPacketArena.StorePrimitive(process, 0, primitive, false);
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint XY(short x, short y) => unchecked((ushort)x | (uint)(ushort)y << 16);
}
