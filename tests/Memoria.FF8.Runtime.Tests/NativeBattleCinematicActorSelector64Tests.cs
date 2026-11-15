using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorSelector64Tests
{
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);

    [Theory]
    [InlineData(0x0064, 4, 2)]
    [InlineData(0x1064, 6, 4)]
    [InlineData(0x8064, 5, 2)]
    [InlineData(0x9064, 5, 2)]
    public void SelectorFlagsPreserveSourcePriorityByteWidthAndPcWidth(ushort instruction, byte expected, uint width)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Arena = frame.Arena.Write(0x1000, [0x64, 0x10, 6, 0xAB]).Write(0x201A, [0xCC, 0, 0xDD]) };
        if ((instruction & 0x1000) == 0 || (instruction & 0x8000) != 0)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, Program, OriginalOwner(5), OriginalAliases);

        Assert.Equal(OriginalOwner(expected), NativeBattleCinematicActorAccess.ReadSlotActor(result));
        Assert.Equal(expected, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal((byte)0xCC, result.Arena.Read(0x201A, 1)[0]);
        Assert.Equal((byte)0xDD, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal(Address(0x1000) + width, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 4, Address(0x1000));

    [Theory]
    [InlineData(0x8064)]
    [InlineData(0x9064)]
    public void ActionModeTakesPrecedenceAndCanSelectAnUnaliasedOwner(ushort instruction)
    {
        BattleModelOwner unaliased = new BattleForeignPartyModel(new("added-caster"));
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, unaliased);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 4) };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, Program, unaliased, OriginalAliases);

        Assert.Equal(unaliased, NativeBattleCinematicActorAccess.ReadSlotActor(result));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x201B, 1));
        Assert.Equal(Address(0x1002), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void InlineModeConsumesOneByteAndAdvancesFourWithoutReadingPadding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1064);
        frame = frame with { Arena = frame.Arena.Write(0x1002, [6]).MarkUnknown(0x1003, 1) };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, Program, OriginalOwner(5), OriginalAliases);

        Assert.Equal(OriginalOwner(6), NativeBattleCinematicActorAccess.ReadSlotActor(result));
        Assert.Equal(Address(0x1004), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void DefaultModeReadsTheCachedOwnerWithoutAnInlineOperand()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x64);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 4) };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, Program, OriginalOwner(5), OriginalAliases);

        Assert.Equal(OriginalOwner(4), NativeBattleCinematicActorAccess.ReadSlotActor(result));
        Assert.Equal(Address(0x1002), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, BattleModelOwner? extra = null)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        BattleActorModels models = new(new[] { OriginalOwner(4), OriginalOwner(5), OriginalOwner(6) }.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new NativeBattleGraphicsModel(3))));
        if (extra is not null)
        {
            models = models.Add(extra, new NativeBattleGraphicsModel(3));
        }
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models).WithCurrentActors([OriginalOwner(4)]);
        return frame with { Core = frame.Core with { PresentationActors = context } };
    }
}
