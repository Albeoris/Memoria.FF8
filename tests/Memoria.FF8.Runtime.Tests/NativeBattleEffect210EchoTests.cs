using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210EchoTests
{
    private const int Header = 12;
    private const uint EchoAddress = 0x20E1678;

    [Fact]
    public void Capture_uses_current_selected_bone_and_two_physical_64_byte_banks_with_retained_padding()
    {
        NativeBattleModulePoolTask task = Echo();
        List<int> selected = [];
        for (int capture = 1; capture <= 3; capture++)
        {
            int current = capture;
            task = NativeBattleDuelEcho.Capture8200(task, Header, 3, bone =>
            {
                selected.Add(bone);
                return MatrixBytes(Identity(current, current + 1, 400 + current), false);
            });
        }
        Assert.Equal(new[] { 1, 1, 1 }, selected);
        Assert.Equal((byte)1, task.ReadByte(Header));
        Assert.Equal((byte)2, task.ReadByte(Header + 1));
        Assert.Equal((byte)1, task.ReadByte(Header + 2));
        Assert.Equal((byte)0, task.ReadByte(Header + 3));
        Assert.Equal(3u, task.ReadDword(Header + 136 + 20));
        Assert.Equal(2u, task.ReadDword(Header + 200 + 20));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(Header + 136 + 18));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(Header + 200 + 18));
        Assert.Equal((byte)0xA5, task.ReadByte(Header + 136 + 32));
        Assert.Equal((byte)0xA5, task.ReadByte(Header + 200 + 32));
        Assert.Equal(0x6F7310u, task.Callback);
        NativeBattleModulePoolTask restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task)))!);
        NativeBattleModulePoolTask Next(NativeBattleModulePoolTask source) => NativeBattleDuelEcho.Capture8200(source, Header, 3, _ => MatrixBytes(Identity(7, 8, 409), false));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(task))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(restored))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_history_still_publishes_116_byte_temporary_and_only_acknowledges_when_unfrozen(bool frozen)
    {
        EchoFrame frame = Fixture() with { Echo = Echo().WriteByte(Header, 1) };
        EchoFrame result = Draw(frame, frozen);
        Assert.Empty(result.Batches);
        Assert.Equal(frozen ? (byte)1 : (byte)0, result.Echo.ReadByte(Header));
        Assert.Equal((byte)0, result.Echo.ReadByte(Header + 1));
        uint temporary = NativeBattleTemporaryMemory.Address;
        Assert.Equal(temporary, U32(result.Staging, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal(1u, U32(result.Staging, temporary + 56));
        Assert.Equal(EchoAddress, U32(result.Staging, temporary + 108));
        Assert.Equal(0x196B000u, U32(result.Staging, temporary + 36));
        Assert.Equal((ushort)320, BinaryPrimitives.ReadUInt16LittleEndian(result.Staging.Read(temporary + 48, 2)));
        Assert.Equal((ushort)216, BinaryPrimitives.ReadUInt16LittleEndian(result.Staging.Read(temporary + 50, 2)));
        Assert.Equal((byte)0xA5, result.Staging.Read(temporary + 60, 1)[0]);
        Assert.DoesNotContain(true, result.Staging.ReadSnapshot(temporary + 18, 2).Known);
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), result.Staging.ReadResourcePointer(temporary + 32)!.Cpu);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Live_history_prepares_at_raw_bank_plus_32_and_frozen_draw_preserves_capture_ack(bool frozen)
    {
        EchoFrame frame = Fixture();
        NativeBattleActionMatrix current = Identity(30, 40, 400);
        frame = frame with { Echo = NativeBattleDuelEcho.Capture8200(frame.Echo, Header, 3, _ => MatrixBytes(current, false)) };
        EchoFrame result = Draw(frame, frozen);
        NativeBattleGfGeometryBatch batch = Assert.Single(result.Batches);
        Assert.NotNull(Assert.Single(batch.Attempts).Primitive);
        int raw = Header + 136;
        int prepared = raw + 32;
        NativeBattleActionMatrix expected = NativeBattleDuelEchoProjection.ComposeHistory460820(Identity(), current);
        NativeBattleActionMatrix actual = NativeBattleDuelEchoWalker.Matrix(result.Echo, prepared);
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Z, actual.Z);
        Assert.Equal(30u, result.Echo.ReadDword(raw + 20));
        Assert.Throws<NotSupportedException>(() => result.Echo.ReadWord(raw + 18));
        Assert.Throws<NotSupportedException>(() => result.Echo.ReadWord(prepared + 18));
        Assert.Equal(frozen ? (byte)1 : (byte)0, result.Echo.ReadByte(Header));
        Assert.Equal((byte)1, result.Echo.ReadByte(Header + 1));
        Assert.Contains((136 + 32, 18), result.EchoWrites);
        Assert.Contains((136 + 52, 12), result.EchoWrites);
        Assert.Equal(0x6F7310u, result.Echo.Callback);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Staging, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void Two_histories_draw_latest_then_previous_and_JSON_next_preserves_frozen_and_unfrozen_decay()
    {
        EchoFrame frame = Fixture();
        frame = frame with { Echo = NativeBattleDuelEcho.Capture8200(frame.Echo, Header, 3, _ => MatrixBytes(Identity(10, 0, 400), false)) };
        frame = frame with { Echo = NativeBattleDuelEcho.Capture8200(frame.Echo, Header, 3, _ => MatrixBytes(Identity(80, 0, 600), false)) };
        EchoFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<EchoFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        EchoFrame direct = Draw(frame, true);
        EchoFrame cold = Draw(restored, true);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(direct)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(cold)));
        Assert.Equal(2, direct.Batches.Length);
        Assert.Equal(0x26484848u, direct.Batches[0].Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x26242424u, direct.Batches[1].Attempts[0].Primitive!.Words[1]);
        Assert.Equal((byte)1, direct.Echo.ReadByte(Header));
        Assert.Equal((byte)2, direct.Echo.ReadByte(Header + 1));
        direct = Draw(direct, false);
        Assert.Equal((byte)0, direct.Echo.ReadByte(Header));
        Assert.Equal((byte)2, direct.Echo.ReadByte(Header + 1));
        direct = Draw(direct, false);
        Assert.Equal((byte)1, direct.Echo.ReadByte(Header + 1));
        direct = Draw(direct, false);
        Assert.Equal((byte)0, direct.Echo.ReadByte(Header + 1));
    }

    private sealed record EchoFrame(NativeBattleModulePoolTask Echo, NativeBattleFileStaging Staging, ImmutableArray<NativeBattleGfGeometryBatch> Batches, ImmutableArray<(int Offset, int Length)> EchoWrites);

    private static EchoFrame Fixture()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(address, Enumerable.Repeat((byte)0xA5, 116).ToArray()).MarkUnknown(address + 18, 2).WriteResourcePointer(address + 32, new(new(0, 0x123), null, 20));
        return new(Echo(), staging, [], []);
    }

    private static EchoFrame Draw(EchoFrame frame, bool frozen)
    {
        byte[] geometry = Geometry();
        NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = Identity(), SequenceState = new() { MeshMask124 = 1 }, Color40 = 0x606060 };
        return NativeBattleDuelEchoDrawing.Draw7EE0(frame, model, EchoAddress, Header, current => current.Echo, (current, task, writes) => current with { Echo = task, EchoWrites = current.EchoWrites.AddRange(writes) }, 0x1000000, (_, offset, length) => geometry.AsSpan(offset, length).ToArray(), 3, Identity(), new(null, 256, 160, 108), (current, address, bytes) => current with { Staging = current.Staging.Write(address, bytes.AsSpan()) }, (current, address, length) => current.Staging.ReadSnapshot(address, length), (current, batch) => current with { Batches = current.Batches.Add(batch) }, 0x155, 0x100, current => current.Staging, (current, staging) => current with { Staging = staging }, frozen);
    }

    private static NativeBattleModulePoolTask Echo()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6F7310, Enumerable.Repeat((byte)0xA5, 336).ToImmutableArray());
        task = NativeBattleDuelEcho.Initialize6A1230(task, Header, [1]);
        return task.MarkPayloadUnknown(Header + 136 + 32 + 18, 2).MarkPayloadUnknown(Header + 200 + 32 + 18, 2);
    }

    private static (byte[] Data, bool[] Known) MatrixBytes(NativeBattleActionMatrix matrix, bool paddingKnown)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, paddingKnown ? (ushort)0x1234 : null);
        return (packed.Bytes.ToArray(), packed.Known.ToArray());
    }

    private static byte[] Geometry()
    {
        byte[] data = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        Word(8, 1);
        Word(10, 1);
        Word(12, 3);
        short[] vertices = [-100, 0, 0, 100, 0, 0, 0, 100, 0];
        for (int index = 0; index < vertices.Length; index++)
        {
            Word(14 + index * 2, unchecked((ushort)vertices[index]));
        }
        Word(32, 1);
        Word(44, 0);
        Word(46, 1);
        Word(48, 2);
        return data;

        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
    }

    private static NativeBattleActionMatrix Identity(int x = 0, int y = 0, int z = 0) => NativeBattleCuragaMatrices.Identity8DD770() with { X = x, Y = y, Z = z };
    private static uint U32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
