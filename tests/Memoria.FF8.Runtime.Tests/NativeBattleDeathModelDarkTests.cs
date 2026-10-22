using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeathModelDarkTests
{
    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Initializer_clears_only_84_scratch_bytes_and_writes_only_reached_descriptor_fields(ushort effect)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect);
        NativeBattleDeathResources resources = new(null!, null!, [], [], null!, null!, null!, [], 0, 0) { Effect = effect };
        NativeBattleEffectArena module = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, profile.ModuleLength).ToArray());
        NativeBattleDeathState state = NativeBattleDeathState.FromColdProcess() with { Module = module };
        NativeBattleEffectMemory memory = profile.WithState(NativeBattleEffectMemory.FromColdProcess(), state);
        NativeBattleDeathFrame frame = new(memory, new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), null, null), new(1));
        uint address = profile.ModuleAddress(0x2742450);
        NativeBattleModulePoolTask task = new(1, 0, null, profile.Callback(0x8B6F70), new byte[308].ToImmutableArray());
        task = task.WriteDword(0x30, 0xDEADBEEF);
        NativeBattleDeathFrame initialized = NativeBattleDeathModelDark.Initialize(frame, address, task, resources);
        NativeBattleEffectArena actual = profile.RequireState(initialized.Memory).Module;
        int scratch = profile.ModuleOffset(0x273AE90);
        int descriptor = profile.ModuleOffset(0x2742878);
        Assert.All(actual.Read(scratch, 0x40), value => Assert.Equal((byte)0, value));
        Assert.Equal(profile.ModuleAddress(0x273B550), Dword(actual, scratch + 0x40));
        Assert.Equal(profile.ModuleAddress(0x2742878), Dword(actual, scratch + 0x44));
        Assert.Equal(address + 0x30, Dword(actual, scratch + 0x48));
        Assert.Equal(profile.ModuleAddress(0x273AE84), Dword(actual, scratch + 0x4C));
        Assert.Equal(profile.ModuleAddress(0x273AE84), Dword(actual, scratch + 0x50));
        Assert.Equal((byte)0xA5, actual.Read(scratch - 1, 1)[0]);
        Assert.Equal((byte)0xA5, actual.Read(scratch + 84, 1)[0]);
        Assert.All(actual.Read(descriptor, 4), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(profile.ModuleAddress(0x273B550), Dword(actual, descriptor + 4));
        Assert.All(actual.Read(descriptor + 8, 12), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 64, 1, 0, 0, 128, 128, 128, 0xA5, 255, 255, 255, 255, 0, 0 }, actual.Read(descriptor + 0x14, 18));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, actual.Read(descriptor + 38, 2));
        Assert.Throws<NotSupportedException>(() => actual.Read(4, 4));
        Assert.Equal(state.Cursor, profile.RequireState(initialized.Memory).Cursor);
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Cursor_is_one_relative_owner_and_publication_excludes_an_imported_raw_mirror(ushort effect)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect);
        NativeBattleDeathState cold = NativeBattleDeathState.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => cold.Module.Read(4, 4));
        NativeBattleDeathState imported = cold with { Module = cold.Module.Write(4, BitConverter.GetBytes(0x12345678u)), Cursor = 456 };
        NativeBattleEffectMemory memory = profile.WithState(NativeBattleEffectMemory.FromColdProcess(), imported);
        NativeBattleDeathState actual = profile.RequireState(memory);
        Assert.Equal(456, actual.Cursor);
        Assert.Throws<NotSupportedException>(() => actual.Module.Read(4, 4));
        Assert.Equal(imported.Module.Read(0, 4), actual.Module.Read(0, 4));
        Assert.Equal(imported.Module.Read(8, 4), actual.Module.Read(8, 4));
        Assert.Same(actual, profile.RequireState(profile.WithState(memory, actual)));
    }

    [Fact]
    public void Camera_preparation_preserves_composed_padding_and_unwritten_scratch_tail()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattlePackedMatrix32 world = NativeBattlePackedMatrix32.FromMatrix(identity with { X = 1, Y = 2, Z = 3 }, null);
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(identity with { Z = 100 }, 0x1234);
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 84).ToArray());
        NativeBattleEffectOwnedModelCameraStep result = NativeBattleEffectOwnedModelRendering.PrepareCamera84(memory, scratch, world, camera);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Scratch.Read(0x32, 2));
        Assert.All(result.Scratch.Read(0x40, 20), value => Assert.Equal((byte)0xA5, value));
        Assert.Throws<NotSupportedException>(() => result.Scratch.Read(18, 2));
        int translatedZ = world.ReadMatrix().Z + camera.ReadMatrix().Z;
        Assert.Equal(unchecked((uint)translatedZ), Dword(result.Scratch, 0x3C));
        Assert.Equal((int?)translatedZ, result.Memory.ScriptedColorRegisters!.Mac[2]);
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    }
}
