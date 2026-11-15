using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorIdentityB5Tests
{
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 6, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000);

    [Theory]
    [InlineData(0, 0, -8, -8)]
    [InlineData(6, 6, 24, 24)]
    [InlineData(0, 256, -8, 6)]
    [InlineData(6, 65535, -8, 6)]
    [InlineData(3, 4, 10, 6)]
    public void Actor_is_zero_extended_and_displacement_is_signed(byte actor, ushort expected, short displacement, int advance)
    {
        NativeBattleWaterExecutionFrame frame = Create(actor, expected, displacement);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorIdentityB5.Execute(frame, Program, OriginalAliases);
        Assert.Equal(unchecked(Program.Address + (uint)advance), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Unequal_branch_does_not_read_displacement()
    {
        NativeBattleWaterExecutionFrame frame = Create(3, 4, -8);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorIdentityB5.Execute(frame, Program, OriginalAliases);
        Assert.Equal(Program.Address + 6, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        NativeBattleWaterExecutionFrame equal = Create(4, 4, -8);
        equal = equal with { Arena = equal.Arena.MarkUnknown(0x1004, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicActorIdentityB5.Execute(equal, Program, OriginalAliases));
    }

    [Fact]
    public void Restored_current_slot_branches_identically()
    {
        NativeBattleWaterExecutionFrame frame = Create(6, 6, -10);
        NativeBattleWaterExecutionFrame restored = frame with { Core = frame.Core with { } };
        NativeBattleWaterExecutionFrame expected = NativeBattleCinematicActorIdentityB5.Execute(frame, Program, OriginalAliases);
        NativeBattleWaterExecutionFrame actual = NativeBattleCinematicActorIdentityB5.Execute(restored, Program, OriginalAliases);
        Assert.Equal(OriginalOwner(6), NativeBattleCinematicActorAccess.ReadSlotActor(restored));
        Assert.Equal(WaterSchedulerFixture.ReadCoreDword(expected, 0x2797450), WaterSchedulerFixture.ReadCoreDword(actual, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Create(byte actor, ushort expected, short displacement)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Program.Address);
        BattleModelOwner owner = OriginalOwner(actor);
        BattleActorModels models = new([new(owner, new NativeBattleGraphicsModel(3))]);
        frame = frame with { Core = frame.Core with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(models) } };
        frame = NativeBattleCinematicActorAccess.WriteSlotActor(frame, owner, OriginalAliases);
        byte[] instruction = new byte[6];
        BitConverter.GetBytes((ushort)0xB5).CopyTo(instruction, 0);
        BitConverter.GetBytes(expected).CopyTo(instruction, 2);
        BitConverter.GetBytes(displacement).CopyTo(instruction, 4);
        return (frame with { Arena = frame.Arena.Write(0x1000, instruction) }).WriteTemporaryWord(0x14A, 0xB5);
    }
}
