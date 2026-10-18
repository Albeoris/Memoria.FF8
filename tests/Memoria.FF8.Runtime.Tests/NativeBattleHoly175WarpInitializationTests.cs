using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175WarpInitializationTests
{
    private static readonly Lazy<NativeBattleHoly175WarpInitializationResult> InitializedGrid = new(() => NativeBattleHoly175WarpInitialization.InitializeGrid5D2210(NativeBattleHoly175WarpState.Unknown(), new(1)));

    [Fact]
    public void GridInitializesConsumedFieldsAndPointersWhileLeavingProjectedAndScreenWordsUnknown()
    {
        NativeBattleHoly175WarpInitializationResult initialized = InitializedGrid.Value;
        NativeBattleHoly175WarpState state = initialized.State;
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 1118; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, initialized.Random);
        Assert.Equal((short)-168, S(state.GridA.Read(4, 2)));
        Assert.Equal((short)-120, S(state.GridA.Read(6, 2)));
        Assert.Equal((short)3, S(state.GridA.Read(10, 2)));
        Assert.Equal((short)0, S(state.GridA.Read(12, 2)));
        Assert.Equal((short)0, S(state.GridA.Read(14, 2)));
        Assert.Equal((short)152, S(state.GridA.Read(314 * 16 + 4, 2)));
        Assert.Equal((short)104, S(state.GridA.Read(314 * 16 + 6, 2)));
        Assert.Equal((short)320, S(state.GridA.Read(314 * 16 + 12, 2)));
        Assert.Throws<NotSupportedException>(() => state.GridA.Read(0, 4));
        Assert.Throws<NotSupportedException>(() => state.GridB.Read(0, 4));
        Assert.Throws<NotSupportedException>(() => state.Faces.Read(12, 2));
        Assert.Equal(NativeBattleHoly175WarpState.GridAAddress, U(state.Faces.Read(0, 4)));
        Assert.Equal(NativeBattleHoly175WarpState.GridAAddress + 16, U(state.Faces.Read(4, 4)));
        Assert.Equal(NativeBattleHoly175WarpState.GridBAddress, U(state.Faces.Read(8, 4)));
        uint lastA = NativeBattleHoly175WarpState.GridAAddress + (13u * 21 + 19) * 16;
        Assert.Equal(new uint[] { lastA, lastA + 16, lastA + 21 * 16, lastA + 22 * 16, NativeBattleHoly175WarpState.GridBAddress + 279 * 16 }, state.PointPointerCache!.Value.ToArray());
        Assert.Equal(0x07000000u, U(state.PhysicalPackets.Read(0, 4)));
        Assert.Equal(0x26808080u, U(state.PhysicalPackets.Read(4, 4)));
        Assert.Equal(0x01390000u, U(state.PhysicalPackets.Read(12, 4)));
        Assert.Throws<NotSupportedException>(() => state.PhysicalPackets.Read(8, 4));
        Assert.Throws<NotSupportedException>(() => state.PhysicalPackets.Read(40, 4));
        Assert.Equal(32, NativeBattleHoly175WarpState.PacketOffset(0, 0, 1));
        Assert.Equal(64, NativeBattleHoly175WarpState.PacketOffset(0, 1, 0));
        Assert.Equal(0x13Bu, U(state.PhysicalPackets.Read(NativeBattleHoly175WarpState.PacketOffset(19, 0, 0) + 12, 4)) >> 16);
        Assert.Equal(state.PhysicalPackets.Read(12, 4), state.PhysicalPackets.Read(44, 4));
        state.Validate();
    }

    [Fact]
    public void FaceInitializationUsesRetainedPointersTruncatedCentersAndExactFourDrawOrder()
    {
        NativeBattleHoly175WarpInitializationResult grid = InitializedGrid.Value;
        string json = JsonSerializer.Serialize(grid.State);
        NativeBattleHoly175WarpState restored = JsonSerializer.Deserialize<NativeBattleHoly175WarpState>(json)!;
        NativeBattleHoly175WarpInitializationResult first = NativeBattleHoly175WarpInitialization.InitializeFaces5D25D0(grid.State, grid.Random);
        NativeBattleHoly175WarpInitializationResult second = NativeBattleHoly175WarpInitialization.InitializeFaces5D25D0(restored, grid.Random);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        byte[] face = first.State.Faces.Read(0, 36);
        int sumX = 0;
        int sumY = 0;
        for (int corner = 0; corner < 3; corner++)
        {
            byte[] point = grid.State.ReadPoint(U(face.AsSpan(corner * 4, 4).ToArray()), 4, 4);
            sumX += S(point);
            sumY += S(point.AsSpan(2).ToArray());
        }
        Assert.Equal(unchecked((short)(sumX / 3)), S(face.AsSpan(12).ToArray()));
        Assert.Equal(unchecked((short)(sumY / 3)), S(face.AsSpan(14).ToArray()));
        Assert.Equal((short)0, S(face.AsSpan(18).ToArray()));
        Assert.Equal((short)4096, S(face.AsSpan(26).ToArray()));
        NativeCrtRandomState expected = grid.Random.Draw(out ushort firstDraw);
        expected = expected.Draw(out ushort secondDraw);
        expected = expected.Draw(out ushort thirdDraw);
        expected = expected.Draw(out ushort fourthDraw);
        Assert.Equal((short)(firstDraw % 6 + 1), S(face.AsSpan(28).ToArray()));
        Assert.Equal((short)(secondDraw % 25 + 4), S(face.AsSpan(30).ToArray()));
        Assert.Equal((short)(-2 - thirdDraw % 8), S(face.AsSpan(32).ToArray()));
        Assert.Equal((short)(fourthDraw % 352 + 320), S(face.AsSpan(34).ToArray()));
        for (int draw = 4; draw < 4480; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, first.Random);
        Assert.Equal(1120 * 36, first.State.Faces.Read(0, 1120 * 36).Length);
        Assert.Equal(grid.State.PhysicalPackets, first.State.PhysicalPackets);
        Assert.Throws<NotSupportedException>(() => first.State.GridA.Read(0, 4));
    }

    [Fact]
    public void KnownWarmProjectionAndPacketSxySurviveGridAndFaceInitialization()
    {
        NativeBattleHoly175WarpState unknown = NativeBattleHoly175WarpState.Unknown();
        byte[] marker = [0x12, 0x34, 0x56, 0x78];
        NativeBattleHoly175WarpState warm = unknown with { GridA = unknown.GridA.Write(0, marker), GridB = unknown.GridB.Write(0, marker), PhysicalPackets = unknown.PhysicalPackets.Write(8, marker).Write(40, marker) };
        NativeBattleHoly175WarpInitializationResult grid = NativeBattleHoly175WarpInitialization.InitializeGrid5D2210(warm, new(17));
        NativeBattleHoly175WarpInitializationResult faces = NativeBattleHoly175WarpInitialization.InitializeFaces5D25D0(grid.State, grid.Random);
        Assert.Equal(marker, faces.State.GridA.Read(0, 4));
        Assert.Equal(marker, faces.State.GridB.Read(0, 4));
        Assert.Equal(marker, faces.State.PhysicalPackets.Read(8, 4));
        Assert.Equal(marker, faces.State.PhysicalPackets.Read(40, 4));
        Assert.Throws<NotSupportedException>(() => faces.State.PhysicalPackets.Read(16, 4));
        Assert.Throws<InvalidDataException>(() => faces.State.ReadPoint(NativeBattleHoly175WarpState.GridAAddress + 1, 4, 4));
        Assert.Throws<InvalidDataException>(() => (faces.State with { GridA = faces.State.GridA.Write(NativeBattleHoly175WarpState.GridALength, marker) }).Validate());
        Assert.Throws<NotSupportedException>(() => NativeBattleHoly175WarpInitialization.InitializeFaces5D25D0(unknown, new(1)));
    }

    private static short S(byte[] bytes) => BinaryPrimitives.ReadInt16LittleEndian(bytes);
    private static uint U(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
}
