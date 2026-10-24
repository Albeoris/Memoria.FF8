using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80ControllerTests
{
    [Fact]
    public void State_request_changes_only_pending_word_and_requires_previous_acknowledgement()
    {
        NativeBattleEffect80Frame frame = Frame();
        (NativeBattleEffect80Frame requested, bool accepted) = RequestControlState7ABC40(frame, 1);
        Assert.True(accepted);
        Assert.Equal(1, requested.State.ReadWord(TargetAddress + 0x44));
        Assert.Equal(0, requested.State.ReadWord(TargetAddress + 0x40));
        Assert.Equal(0, requested.State.ReadWord(TargetAddress + 0x42));
        (NativeBattleEffect80Frame blocked, bool next) = RequestControlState7ABC40(requested, 2);
        Assert.False(next);
        Assert.Same(requested, blocked);
    }

    [Fact]
    public void Control_copies_pending_state_after_the_birth_check_preserving_one_callback_delay()
    {
        NativeBattleEffect80Frame frame = Frame();
        frame = Write(frame, 0x200, Read(frame, 0x200).WriteByte(0x29, 12));
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x44, 1) };
        NativeBattleEffect80Frame result = AdvanceControl(frame, 0x200, null!, null!).Frame;
        Assert.Equal(0, result.State.ReadWord(TargetAddress + 0x40));
        Assert.Equal(1, result.State.ReadWord(TargetAddress + 0x42));
        Assert.Equal(1, result.State.ReadWord(TargetAddress + 0x46));
        Assert.Equal(1, Read(result, 0x200).ReadWord(0x24));
    }

    [Fact]
    public void Body_initializer_writes_descriptor_and_context_without_clearing_descriptor_gaps()
    {
        NativeBattleEffect80Frame frame = Frame();
        frame = frame with { State = frame.State.Write(DescriptorAddress, Enumerable.Repeat((byte)0xA5, DescriptorLength).ToArray()) };
        NativeBattleEffect80Frame result = InitializeBody7B4480(frame, PhysicalModelAddress(5), 5);
        Assert.Equal(DescriptorAddress, result.State.ReadDword(BodyContextAddress + 0x60));
        Assert.Equal(RowsAddress, result.State.ReadDword(DescriptorAddress + 4));
        Assert.Equal(0x1D8E054u, result.State.ReadDword(BodyContextAddress + 0x68));
        Assert.Equal(0x1D8E054u, result.State.ReadDword(BodyContextAddress + 0x6C));
        Assert.Equal(5, result.State.ReadWord(BodyContextAddress + 0xA8));
        Assert.Equal(0xA5A5A5A5u, result.State.ReadDword(DescriptorAddress));
        Assert.Equal(0xA5, result.State.Read(DescriptorAddress + 0x1F, 1)[0]);
        Assert.Equal(0x140, result.State.ReadWord(DescriptorAddress + 0x18));
        Assert.Equal(uint.MaxValue, result.State.ReadDword(DescriptorAddress + 0x20));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(-1, 17)]
    [InlineData(119, 23)]
    [InlineData(120, 23)]
    public void Act_height_lookup_clamps_only_upper_bound_and_preserves_point_w(short age, short height)
    {
        NativeBattleEffect80Frame frame = Frame();
        uint selected = age < 0 ? AgeTableAddress - 2 : AgeTableAddress + 238;
        NativeBattleEffect80Resources resources = Resources() with { StaticTables = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(selected, BitConverter.GetBytes(height).ToImmutableArray()) };
        NativeBattleEffect80Inputs inputs = Inputs(NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x7FFF0010u)).Write(4, BitConverter.GetBytes(0xABCD0020u)));
        NativeBattleEffect80Frame result = PositionAct7AC060(frame, 0x200, age, resources, inputs);
        Assert.Equal(unchecked((ushort)(0x7FFF + height)), Read(result, 0x200).ReadWord(0x1E));
        Assert.Equal(0xABCD, Read(result, 0x200).ReadWord(0x22));
    }

    [Theory]
    [InlineData(79)]
    [InlineData(151)]
    public void Emission_outside_signed_native_age_range_reads_no_resource_or_position(int age)
    {
        NativeBattleEffect80Frame frame = Frame();
        frame = Write(frame, 0x200, Read(frame, 0x200).WriteWord(0x24, (ushort)age));
        Assert.Same(frame, NativeBattleEffect80Particles.Emit7B06A0(frame, 0x200, null!, null!));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(81)]
    public void First_emission_lookup_reads_current_mutable_pointer_halves(int age)
    {
        NativeBattleEffect80Frame frame = Frame();
        frame = frame with { State = frame.State.BindInitializedSource(Resources(), TrailPointerAddress).WriteDword(TrailPointerAddress, 0) };
        frame = Write(frame, 0x200, Read(frame, 0x200).WriteWord(0x24, (ushort)age));
        Assert.Same(frame, NativeBattleEffect80Particles.Emit7B06A0(frame, 0x200, null!, null!));
    }

    [Fact]
    public void Emission_endpoint_is_local_and_preserves_body_task_point_and_w()
    {
        NativeBattleEffect80Frame frame = Frame();
        frame = Write(frame, 0x200, Read(frame, 0x200).WriteWord(0x24, 82).WriteDword(0x1C, 0x12345678).WriteDword(0x20, 0xABCD9ABC));
        NativeBattleEffect80Resources resources = Resources() with { StaticTables = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(BirthTableAddress, BitConverter.GetBytes((ushort)1).ToImmutableArray()).Add(AgeTableAddress, BitConverter.GetBytes((short)0).ToImmutableArray()) };
        NativeBattleEffect80Inputs inputs = Inputs(NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x7FFF0010u)).Write(4, BitConverter.GetBytes(0xFEDC0020u)));
        NativeBattleEffect80Frame result = NativeBattleEffect80Particles.Emit7B06A0(frame, 0x200, resources, inputs);
        Assert.Equal(0x12345678u, Read(result, 0x200).ReadDword(0x1C));
        Assert.Equal(0xABCD9ABCu, Read(result, 0x200).ReadDword(0x20));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 4; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, result.Random);
    }

    [Fact]
    public void Trail_pair_uses_twelve_shared_crt_draws_and_keeps_private_lcg_unchanged()
    {
        NativeBattleEffect80Resources resources = Resources();
        NativeBattleEffect80Frame frame = NativeBattleEffect80Trails.Initialize7B43F0(Frame(), resources);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 12; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect80Frame result = NativeBattleEffect80Particles.BirthPair7B0860(frame, new(1, 2, 3), new(4, 5, 6), 2, resources);
        Assert.Equal(expected, result.Random);
        Assert.Equal(frame.State.ReadDword(TrailRandomGlobal), result.State.ReadDword(TrailRandomGlobal));
        Assert.Equal((ushort)2048, result.State.ReadWord(TrailAddress + 0x301E));
        Assert.Equal((ushort)3000, result.State.ReadWord(TrailAddress + 0x300E));
        Assert.Equal((ushort)2, result.State.ReadWord(TrailAddress + 0x3026));
        Assert.Equal((ushort)4096, result.State.ReadWord(TrailAddress + 0x302E));
    }

    private static NativeBattleEffect80Frame Frame()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 0xA0, 1);
        pool = pool with { Slots = [pool.Slots[0].WriteByte(0x28, 1) with { Callback = ControlCallback }] };
        NativeBattleOriginalEffectState effect = new(80, 79, new(80, Entry, Initializer), null, null) { Pools = [pool] };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), NativeBattleEffect80State.FromColdProcess());
    }

    private static NativeBattleEffect80Inputs Inputs(NativeBattleEffectArena point) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, null!, null!, null!, (_, _) => point, null!, null!, null!);
    private static NativeBattleEffect80Resources Resources() => new(new("texture", []), new("camera", []), new("vibration", []), ImmutableDictionary<uint, NativeBattleEffect80NodeResourceBank>.Empty, InitializedSourceLengths.ToImmutableDictionary(pair => pair.Key, pair => pair.Key == TrailPointerAddress ? BitConverter.GetBytes(TrailAddress).ToImmutableArray() : new byte[pair.Value].ToImmutableArray()), [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, TargetAddress);
}
