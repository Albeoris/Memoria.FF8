using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315SpriteTests
{
    [Fact]
    public void Delayed_particle_flags_A_use_actual_caller_scales_and_extended_frame_contract()
    {
        (NativeBattleEffectMemory memory, NativeBattleEffectArena context, NativeBattleModuleResource resource) = Fixture();
        Assert.Throws<InvalidDataException>(() => NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, _ => resource, Projection, 321));
        NativeBattleModuleSpriteDraw actual = NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, _ => resource, Projection, 321, 17, 2, true);
        Assert.Equal((ushort)0x10A, W(actual.Temporary180, 0x24));
        Assert.Equal((ushort)1, W(actual.Temporary180, 0x26));
        Assert.Equal((short)-1, unchecked((short)W(actual.Temporary180, 0x28)));
        Assert.Equal(resource.Address + 18, U(actual.Temporary180, 0x2C));
        Assert.Equal(1u, U(actual.Temporary180, 0x30));
        Assert.Equal(4096u, U(actual.Temporary180, 0x14));
        Assert.Equal(1u, U(actual.Temporary180, 0xB0));
        NativeBattleActionMatrix before = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [1312, 0, 0, 0, 1312, 0, 0, 0, 1312], X = 20, Y = 30, Z = 2000 };
        NativeBattleActionMatrix rolled = NativeBattleGfMatrices.RotateZ56D090(before, -321);
        int[] scales = [4096, 2560, 4096];
        for (int index = 0; index < 9; index++)
        {
            short expected = unchecked((short)(unchecked(rolled.Rotation[index] * scales[index / 3]) >> 12));
            Assert.Equal(expected, unchecked((short)W(actual.Temporary180, 0x84 + index * 2)));
        }
        Assert.Equal(new byte[] { 0xEF, 0xBE }, actual.Temporary180.Read(0x96, 2));
        Assert.Equal(new int[] { 20, 30, 2000 }, Enumerable.Range(0, 3).Select(index => unchecked((int)U(actual.Temporary180, 0x98 + index * 4))).ToArray());
        Assert.NotEmpty(actual.Batch.Attempts);
        Assert.Contains(actual.Batch.Attempts, attempt => attempt.Primitive is not null);
        Assert.NotNull(actual.Memory.ScriptedColorRegisters!.Flags);
        Assert.NotNull(actual.Memory.GeometryRegisters!.Sz[3]);
        Assert.Equal(memory.ScriptedLighting, actual.Memory.ScriptedLighting);
    }

    [Fact]
    public void Reused_180_bytes_and_current_sprite_registers_resume_with_caller_scale_opt_in()
    {
        (NativeBattleEffectMemory memory, NativeBattleEffectArena context, NativeBattleModuleResource resource) = Fixture();
        NativeBattleModuleSpriteDraw first = NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, _ => resource, Projection, 0, allowCallerScale: true);
        NativeBattleModuleSpriteDraw restored = JsonSerializer.Deserialize<NativeBattleModuleSpriteDraw>(JsonSerializer.Serialize(first))!;
        NativeBattleModuleSpriteDraw Next(NativeBattleModuleSpriteDraw retained) => NativeBattleModuleSpriteDrawing.Draw571C80(retained.Memory, retained.Temporary180.Write(4, [0, 0]), _ => resource, Projection, 0, allowCallerScale: true);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
        Assert.Equal((short)-1, unchecked((short)W(Next(restored).Temporary180, 0x28)));
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleEffectArena Context, NativeBattleModuleResource Resource) Fixture()
    {
        NativeBattleActionMatrix matrix = new([1312, 0, 0, 0, 1312, 0, 0, 0, 1312], 20, 30, 2000, 4096);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, unchecked((short)0xBEEF)), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        byte[] bytes = new byte[38];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x06010524);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 0x80000001);
        Span<byte> quad = bytes.AsSpan(18, 20);
        quad[0] = quad[2] = 2;
        quad[1] = 100;
        quad[3] = 0x2C;
        quad[4] = 10;
        quad[5] = 20;
        BinaryPrimitives.WriteUInt16LittleEndian(quad.Slice(14), 0x123);
        NativeBattleModuleResource resource = new(0, NativeBattleEffect315Profile.SpriteAddresses[0], 0, 0, bytes.ToImmutableArray());
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(resource.Address)).Write(4, [0, 0]).Write(12, BitConverter.GetBytes(4096)).Write(16, BitConverter.GetBytes(2560)).Write(0x24, BitConverter.GetBytes((ushort)0xA));
        return (memory, context, resource);
    }

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static ushort W(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
