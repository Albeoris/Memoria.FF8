using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314ControllerTests
{
    [Fact]
    public void Fragment_expiration_releases_the_hint_before_reading_its_rotation_pointer()
    {
        NativeBattleEffect314Frame frame = Frame();
        uint main = CommonAddress + MainOffset;
        uint fragment = main + FragmentOffset + 3 * FragmentStride;
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)3)).Write(0xE, BitConverter.GetBytes((ushort)1)).Write(0x10, BitConverter.GetBytes(uint.MaxValue));
        frame = WriteBytes(frame, fragment, row, FragmentStride);
        NativeBattleEffect314Frame result = AdvanceFragments6642F0(frame, main, null!);
        Assert.Equal(new byte[2], ReadBytes(result, fragment + 6, 2).Read(0, 2));
        Assert.Equal(fragment, Read(result, main).ReadDword(0x4724));
        Assert.Equal(frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Equal(frame.Memory.ScriptedColorRegisters, result.Memory.ScriptedColorRegisters);
    }

    [Fact]
    public void Fragment_physics_rotates_all_four_vectors_then_adds_signed_WORD_velocity()
    {
        NativeBattleEffect314Frame frame = Frame();
        uint main = CommonAddress + MainOffset;
        uint fragment = main + FragmentOffset;
        uint matrix = main + 0x4728;
        NativeBattleEffectArena rotation = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)4096)).Write(8, BitConverter.GetBytes((short)4096)).Write(16, BitConverter.GetBytes((short)4096));
        frame = WriteBytes(frame, matrix, rotation, 20);
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)32767)).Write(2, BitConverter.GetBytes(short.MinValue)).Write(6, BitConverter.GetBytes((ushort)3)).Write(8, BitConverter.GetBytes((short)-2)).Write(10, BitConverter.GetBytes((short)5)).Write(12, BitConverter.GetBytes((short)7)).Write(14, BitConverter.GetBytes((ushort)2)).Write(16, BitConverter.GetBytes(matrix));
        short[] vertices = [1, 0, 0, 0, 1, 0, 0, 0, 1, 2, 3, 4];
        row = row.Write(0x14, vertices.SelectMany(BitConverter.GetBytes).ToArray());
        frame = WriteBytes(frame, fragment, row, FragmentStride);
        NativeBattleEffect314Frame result = AdvanceFragments6642F0(frame, main, null!);
        Assert.Equal(new short[] { 32765, -32763, 7 }.SelectMany(BitConverter.GetBytes).ToArray(), ReadBytes(result, fragment, 6).Read(0, 6));
        Assert.Equal(vertices.SelectMany(BitConverter.GetBytes).ToArray(), ReadBytes(result, fragment + 0x14, 24).Read(0, 24));
        Assert.Equal(new byte[] { 1, 0 }, ReadBytes(result, fragment + 14, 2).Read(0, 2));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Particle_program_consumes_WORD_operands_with_native_wrap_and_terminates_without_publishing_a_yield_PC()
    {
        NativeBattleEffect314Frame frame = Frame();
        uint program = ProgramAddresses[0];
        ushort[] words = [55, 0x1002, 0xFFFF, 2, 3, 0x1003, 2, 0xFFFD, 0xFFFC, 0x1012, 1, 2, 3, 0x1014, 0x10FF];
        ImmutableArray<byte> bytes = words.SelectMany(BitConverter.GetBytes).ToImmutableArray();
        NativeBattleEffect314Resources resources = new(null!, new("camera", []), new("vibration", []), [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(program, bytes));
        uint offsets = CommonAddress + MainOffset + 0x20;
        NativeBattleEffect314AddressStep creation = SpawnParticle664CD0(frame, resources, 0x1D97494, offsets, program);
        uint row = offsets + 55 * 16;
        frame = WriteBytes(creation.Frame, row + 8, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x7FFF)).Write(2, BitConverter.GetBytes((ushort)0xFFFF)).Write(4, BitConverter.GetBytes((ushort)0xFFFD)), 6);
        NativeBattleEffect314Step result = AdvanceParticle664D20(frame, creation.Address, resources, null!);
        Assert.Equal(2, result.Result);
        NativeBattleModulePoolTask task = Read(result.Frame, creation.Address);
        Assert.Equal(1, task.ReadWord(0x1C));
        Assert.Equal(0xFFFF, task.ReadWord(0x1E));
        Assert.Equal(0xFFFF, task.ReadWord(0x20));
        Assert.Equal(new byte[] { 0, 0x80, 1, 0, 0, 0 }, ReadBytes(result.Frame, row + 8, 6).Read(0, 6));
        Assert.Equal(program + 2, task.ReadDword(0x18));
    }

    [Fact]
    public void Particle_creation_clears_the_actual_nine_DWORD_span_including_the_next_physical_header()
    {
        NativeBattleEffect314Frame frame = Frame();
        NativeBattleEffectTaskPool particles = frame.Effect.Pools[2];
        particles = particles.WriteSlot(1, particles.Slots[1].UpdateHeader(0x20, 0xABCD, particles.Slots[1].Next, 0x12345678));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, particles) } };
        uint program = ProgramAddresses[0];
        NativeBattleEffect314Resources resources = new(null!, null!, null!, [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(program, [55, 0, 0xFF, 0x10]));
        NativeBattleEffect314AddressStep result = SpawnParticle664CD0(frame, resources, 0x1D97494, CommonAddress + MainOffset + 0x20, program);
        Assert.Equal(CommonAddress, result.Address);
        NativeBattleModulePoolTask particle = Read(result.Frame, result.Address);
        Assert.Equal(ParticleCallback, particle.Callback);
        Assert.Equal(0x1D97494u, particle.ReadDword(0xC));
        Assert.Equal(CommonAddress + MainOffset + 0x20u, particle.ReadDword(0x10));
        Assert.Equal(CommonAddress + MainOffset + 0x20u + 55 * 16, particle.ReadDword(0x14));
        Assert.Equal(program + 2, particle.ReadDword(0x18));
        Assert.Equal(new byte[20], particle.ReadBytes(0x1C, 20));
        NativeBattleModulePoolTask next = result.Frame.Effect.Pools[2].Slots[1];
        Assert.Equal(0, next.Flags);
        Assert.Equal(0, next.State);
        Assert.Null(next.Next);
        Assert.Equal(0x12345678u, next.Callback);
    }

    [Fact]
    public void Spark_allocation_uses_the_retained_hint_then_scans_from_the_original_first_record()
    {
        NativeBattleEffect314Frame frame = Frame();
        uint main = CommonAddress + MainOffset;
        uint first = main + SparkOffset;
        uint hint = first + 16 * 9;
        frame = WriteBytes(frame, main + 0xF20, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(hint)), 4);
        (NativeBattleEffect314Frame result, uint selected) = AllocateSpark6643D0(frame, main);
        Assert.Equal(hint, selected);
        Assert.Equal(hint + 16, Read(result, main).ReadDword(0xF20));
        frame = WriteBytes(result, hint + 16 + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)1)), 2);
        (result, selected) = AllocateSpark6643D0(frame, main);
        Assert.Equal(first, selected);
        Assert.Equal(first + 16, Read(result, main).ReadDword(0xF20));
    }

    [Fact]
    public void Cold_globals_are_zero_while_unimported_live_globals_remain_unknown()
    {
        Assert.Equal(0u, NativeBattleEffect314State.FromColdProcess().ReadDword(RandomGlobal));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect314State.FromUnknown().ReadDword(RandomGlobal));
    }

    private static NativeBattleEffect314Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(314, 313, new(314, Entry, Initializer), new(3, 0, 1, 0, 0, 0), null);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        NativeBattleEffect314Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect with { Pools = [root] }, new(1), NativeBattleEffect314State.FromColdProcess().DetachBank(RootBank).WriteDword(WorkGlobal, CommonAddress));
        return InitializeChildBanks(frame);
    }
}
