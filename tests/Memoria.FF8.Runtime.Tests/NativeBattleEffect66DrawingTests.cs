using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66DrawingTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    public void Static_dispatch_publishes_actual_context_and_current_registers_before_delivery(byte shift)
    {
        NativeBattleEffect66Frame frame = Frame();
        (frame, uint address) = Allocate(frame, 88);
        NativeBattleStaticMeshResource mesh = Triangle();
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x33u));
        frame = PublishContext(frame, address, context, 88);
        NativeBattleEffect66Resources resources = Resources() with { Meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh) };
        List<NativeBattleEffect66DeliverBatch> deliveries = [];
        NativeBattleEffect66Frame result = NativeBattleEffect66Drawing.DrawStatic(frame, address, mesh.Address, shift, CursorGlobal, resources, Inputs(deliveries, address, 88));
        NativeBattleEffect66DeliverBatch delivery = Assert.Single(deliveries);
        Assert.Equal(CursorGlobal, delivery.CursorGlobal);
        Assert.NotEmpty(delivery.Batch.Attempts);
        Assert.Equal(mesh.Address + 8, U(Context(result, address, 88), 4));
        Assert.Equal(0x33u, U(Context(result, address, 88), 0x1C));
        Assert.Throws<NotSupportedException>(() => Context(result, address, 88).Read(0x54, 4));
        Assert.NotNull(result.Memory.GeometryRegisters!.Sz[3]);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Sprite_dispatch_uses_current_matrix_and_retained_180_bytes_without_reallocation()
    {
        NativeBattleEffect66Frame frame = Frame();
        (frame, uint address) = Allocate(frame, 180);
        NativeBattleModuleResource sprite = Sprite();
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(sprite.Address)).Write(4, new byte[2]).Write(0x24, BitConverter.GetBytes((ushort)8));
        frame = PublishContext(frame, address, context, 180);
        NativeBattleEffect66Resources resources = Resources() with { Sprites = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(sprite.Address, sprite) };
        List<NativeBattleEffect66DeliverBatch> deliveries = [];
        NativeBattleEffect66Frame result = NativeBattleEffect66Drawing.DrawSprite(frame, address, 2, 0x1D8E054, resources, Inputs(deliveries, address, 180));
        NativeBattleEffectArena actual = Context(result, address, 180);
        Assert.Equal((ushort)0x108, W(actual, 0x24));
        Assert.Equal((ushort)1, W(actual, 0x26));
        Assert.Equal(ushort.MaxValue, W(actual, 0x28));
        Assert.Equal(new byte[] { 0xEF, 0xBE }, actual.Read(0x96, 2));
        Assert.Equal(2000u, U(actual, 0xA0));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4).ToArray(), result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4).ToArray());
        Assert.Equal(0x1D8E054u, Assert.Single(deliveries).CursorGlobal);
        NativeBattleEffect66Frame restored = JsonSerializer.Deserialize<NativeBattleEffect66Frame>(JsonSerializer.Serialize(result))!;
        restored = PublishContext(restored, address, Context(restored, address, 180).Write(4, new byte[2]), 180);
        result = PublishContext(result, address, actual.Write(4, new byte[2]), 180);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect66Drawing.DrawSprite(result, address, 2, CursorGlobal, resources, Inputs([], address, 180))), JsonSerializer.Serialize(NativeBattleEffect66Drawing.DrawSprite(restored, address, 2, CursorGlobal, resources, Inputs([], address, 180))));
    }

    private static ushort W(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleEffect66Frame Frame()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, unchecked((short)0xBEEF)), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, new(66, 65, new(66, Entry, Initializer), null, null) { Pools = [] }, new(123), NativeBattleEffect66State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect66Inputs Inputs(List<NativeBattleEffect66DeliverBatch> deliveries, uint address, int length) => new(Colors: NativeBattleGteColorInterpolation.CodeConstants45F270, ReadPackets: null!, ReadFlags: null!, ReadStep508500: null!, ReadView: null!, ReadProjection: _ => new(null, 100, 160, 108), ReadCameraRoll: _ => 0, ReadCameraPose: null!, ReadModel: null!, ReadPoseSource: null!, ReadWorld: null!, ReadBone502170: null!, ReadVibrationPlaying4A2900: null!, Execute: (frame, operation) =>
    {
        deliveries.Add(Assert.IsType<NativeBattleEffect66DeliverBatch>(operation));
        Assert.NotNull(frame.Memory.GeometryRegisters!.Sz[3]);
        _ = W(Context(frame, address, length), length == 180 ? 0x26 : 0x18);
        return frame;
    });

    private static NativeBattleEffect66Resources Resources() => new(new("texture", []), new("embedded", []), new("camera", []), new("vibration", []), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);

    private static NativeBattleStaticMeshResource Triangle()
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        short[] coordinates = [-100, -100, 0, 0, 100, -100, 0, 0, 0, 100, 0, 0];
        for (int index = 0; index < coordinates.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), coordinates[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x26808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 4);
        return new(0, MeshAddresses[0], bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[38];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x06010524);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), ushort.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 0x80000001);
        bytes[18] = bytes[20] = 2;
        bytes[19] = 100;
        bytes[21] = 0x2C;
        return new(0, SpriteAddresses[0], 0, 0, bytes.ToImmutableArray());
    }
}
