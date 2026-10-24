using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect64LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64ProgrammeTests
{
    [Fact]
    public void Birth_clears_only_actual_cache_and_restores_pose_after_position_capture()
    {
        NativeBattleEffect64Frame frame = Frame(1, 0, 0, 4096, 17, -128);
        uint address = Common + ProgrammeOffset;
        frame = Write(frame, address, Read(frame, address).MarkPayloadUnknown(0x20, ProgrammeStride - 0x20));
        byte[] source = new byte[16];
        BitConverter.GetBytes(8).CopyTo(source, 4);
        byte[] descriptors = new byte[24];
        BitConverter.GetBytes(0x13433C8u).CopyTo(descriptors, 0);
        BitConverter.GetBytes(152).CopyTo(descriptors, 4);
        NativeBattleEffect64Resources resources = Resources() with { Acts = [new(0x13433C8, source.ToImmutableArray())], ProgrammeDescriptors = descriptors.ToImmutableArray() };
        List<string> calls = [];
        (NativeBattleEffect64Frame result, uint next) = NativeBattleEffect64Programme.Birth6E61E0(frame, Script, 0x1D975CC, Row, false, resources, Inputs(calls));
        NativeBattleModulePoolTask task = Read(result, address);
        Assert.Equal(new[] { nameof(NativeBattleEffect64ApplyOffsetPose662C00), "bone:1:0", nameof(NativeBattleEffect64RestorePose508C90) }, calls);
        Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0 }, task.ReadBytes(12, 6));
        Assert.Equal(0x13473D0u, task.ReadDword(0x1C));
        Assert.Equal(0x13433C8u, task.ReadDword(0x20));
        Assert.Equal(new byte[148], task.ReadBytes(0x24, 148));
        Assert.Throws<NotSupportedException>(() => task.ReadByte(0x20 + 152));
        Assert.Equal(Script + 12, next);
    }

    [Fact]
    public void Full_programme_bank_consumes_six_operands_without_pose_or_random_changes()
    {
        NativeBattleEffect64Frame frame = Frame(58, 0, 1, 4096, 1024, -128);
        (frame, _) = Append(frame, ProgrammeDescriptor, 0x6E6310);
        List<string> calls = [];
        (NativeBattleEffect64Frame result, uint next) = NativeBattleEffect64Programme.Birth6E61E0(frame, Script, 0x1D975CC, Row, false, Resources(), Inputs(calls));
        Assert.Equal(Script + 12, next);
        Assert.Empty(calls);
        Assert.Equal(frame.Random, result.Random);
        Assert.Same(frame, result);
    }

    [Fact]
    public void Programme_failure_still_requires_the_last_operand_before_allocation()
    {
        NativeBattleEffect64Frame frame = Frame();
        (frame, _) = Append(frame, ProgrammeDescriptor, 0x6E6310);
        NativeBattleEffectArena script = frame.State.ReadScriptSource(Script).MarkUnknown(10, 2);
        frame = frame with { State = frame.State.ReplaceScriptSource(Script, script) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect64Programme.Birth6E61E0(frame, Script, 0, Row, false, Resources(), Inputs()));
    }
}
