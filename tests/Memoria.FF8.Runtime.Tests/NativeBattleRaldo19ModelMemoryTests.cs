using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRaldo19ModelMemoryTests
{
    [Fact]
    public void Capture_preserves_full_packed_XY_and_ZW_and_leaves_unowned_bytes_unknown()
    {
        NativeBattleGraphicsModel model = Model();
        NativeBattleEffectArena captured = NativeBattleRaldo19ModelMemory.Capture(model);
        Assert.Equal(0xFEDC8123u, Dword(captured, 0x1C));
        Assert.Equal(0xABCD7654u, Dword(captured, 0x20));
        Assert.Equal((ushort)0x9876, Word(captured, 0x24));
        Assert.All(captured.ReadSnapshot(0x26, 0x9C - 0x26).Known, known => Assert.False(known));
        NativeBattleGraphicsModel replaced = NativeBattleRaldo19ModelMemory.Apply(model, 0x20, Bytes(0xF00D8001), 0, 4);
        Assert.Equal((short)-32767, replaced.Transform!.Position28.Z);
        Assert.Equal(unchecked((short)0xF00D), replaced.Transform.Vertical34);
        Assert.Equal(model.Transform!.Position28.X, replaced.Transform!.Position28.X);
        Assert.Equal(model.Transform.Position28.Y, replaced.Transform.Position28.Y);
        Assert.Equal(model.Transform!.Angle36, replaced.Transform.Angle36);
        Assert.Equal(model.GeometryWorldMatrix, replaced.GeometryWorldMatrix);
        replaced = NativeBattleRaldo19ModelMemory.Apply(replaced, 0x1C, Bytes(0x80007FFF), 0, 4);
        Assert.Equal(32767, replaced.Transform!.Position28.X);
        Assert.Equal(-32768, replaced.Transform.Position28.Y);
        Assert.Equal((short)-32767, replaced.Transform.Position28.Z);
        Assert.Equal(unchecked((short)0xF00D), replaced.Transform.Vertical34);
    }

    [Fact]
    public void Rotation_DWORD_restore_propagates_unknown_padding_without_losing_known_rotation()
    {
        NativeBattleGraphicsModel model = Model();
        NativeBattleEffectArena saved = NativeBattleRaldo19ModelMemory.Capture(model with { Rotation16 = -1234, RotationPadding18 = null });
        Assert.All(saved.ReadSnapshot(0x12, 2).Known, known => Assert.False(known));
        NativeBattleGraphicsModel restored = NativeBattleRaldo19ModelMemory.Apply(model, 0x10, saved, 0x10, 4);
        Assert.Equal((short)-1234, restored.Rotation16);
        Assert.Null(restored.RotationPadding18);
        Assert.Equal(model.Rotation12, restored.Rotation12);
        Assert.Equal(model.Rotation14, restored.Rotation14);
        restored = NativeBattleRaldo19ModelMemory.Apply(restored, 0x10, Bytes(0xFEDC8000), 0, 4);
        Assert.Equal(short.MinValue, restored.Rotation16);
        Assert.Equal((ushort)0xFEDC, restored.RotationPadding18);
    }

    [Fact]
    public void Flags_BYTE_store_preserves_upper_BYTE_and_WORD_store_replaces_both()
    {
        NativeBattleGraphicsModel model = Model() with { Flags0 = 0xAB02 };
        model = NativeBattleRaldo19ModelMemory.Apply(model, 0, Bytes(0x06), 0, 1);
        Assert.Equal((ushort)0xAB06, model.Flags0);
        model = NativeBattleRaldo19ModelMemory.Apply(model, 0, Bytes(0xCD02), 0, 2);
        Assert.Equal((ushort)0xCD02, model.Flags0);
    }

    [Theory]
    [InlineData(0x54, -32768)]
    [InlineData(0x58, -1)]
    [InlineData(0x5C, 32767)]
    public void Sign_extended_world_translation_is_separate_from_actual_packed_position(int offset, int value)
    {
        NativeBattleGraphicsModel model = Model();
        NativeBattleGraphicsModel updated = NativeBattleRaldo19ModelMemory.Apply(model, offset, Bytes(unchecked((uint)value)), 0, 4);
        Assert.Equal(model.Transform, updated.Transform);
        Assert.Equal(offset == 0x54 ? value : 111, updated.GeometryWorldMatrix!.X);
        Assert.Equal(offset == 0x58 ? value : 222, updated.GeometryWorldMatrix.Y);
        Assert.Equal(offset == 0x5C ? value : 333, updated.GeometryWorldMatrix.Z);
        Assert.Equal(model.GeometryWorldMatrix!.Rotation, updated.GeometryWorldMatrix.Rotation);
        Assert.Throws<NotSupportedException>(() => NativeBattleRaldo19ModelMemory.Apply(model with { GeometryWorldMatrix = null }, offset, Bytes(0), 0, 4));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(0x0C, 2)]
    [InlineData(0x0E, 4)]
    [InlineData(0x12, 2)]
    [InlineData(0x1E, 4)]
    [InlineData(0x22, 2)]
    [InlineData(0x54, 2)]
    [InlineData(0x60, 4)]
    public void Unsupported_field_widths_fail_before_writing(int offset, int length)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleRaldo19ModelMemory.Apply(Model(), offset, Bytes(0), 0, length));
    }

    private static NativeBattleGraphicsModel Model() => new(0, 2)
    {
        Rotation12 = 1,
        Rotation14 = 2,
        Rotation16 = 3,
        RotationPadding18 = 0x4321,
        Transform = new(new(0, 0, 0), new(unchecked((short)0x8123), unchecked((short)0xFEDC), 0x7654), unchecked((short)0xABCD), unchecked((short)0x9876), 4096),
        GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 111, 222, 333, 4096)
    };

    private static NativeBattleEffectArena Bytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return NativeBattleEffectArena.Clear().Write(0, bytes);
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
}
