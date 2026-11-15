using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorRotation72Tests
{
    [Fact]
    public void Signed_actor_rotation_WORDs_publish_fixed_point_then_republish_current_scale_without_operands()
    {
        BattleModelOwner selected = OriginalOwner(3);
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicTypedTestFixture.BindActor(Fixture(), selected);
        short[] rotation = [short.MinValue, -1, short.MaxValue];
        int reads = 0;
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicActorRotation72.Execute(current, owner =>
        {
            reads++;
            Assert.Equal(selected, owner);
            return new(2) { Rotation12 = rotation[0], Rotation14 = rotation[1], Rotation16 = rotation[2] };
        });
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(1, reads);
        for (int lane = 0; lane < 3; lane++)
        {
            Assert.Equal(unchecked((uint)(rotation[lane] << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x50 + lane * 4));
            Assert.Equal(unchecked((ushort)rotation[lane]), WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8C + lane * 2));
        }
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(0xA5A5, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x92));

        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(restored)));
        Assert.Equal(2, reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejects_missing_or_retired_actor_before_reading_pose_and_preserves_slot(bool retired)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        if (retired)
        {
            frame = NativeBattleCinematicTypedTestFixture.BindActor(frame, OriginalOwner(3));
            frame = frame with { Core = frame.Core with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(OriginalModels([(0, new NativeBattleGraphicsModel(0))]).Models) } };
        }
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, 0x12345678);
        string before = NativeBattleTestSnapshots.Json(frame);
        Assert.ThrowsAny<InvalidDataException>(() => NativeBattleCinematicActorRotation72.Execute(frame, _ => throw new InvalidOperationException("Unexpected actor read.")));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(frame));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        return WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0xA5A5);
    }
}

internal static class NativeBattleCinematicTypedTestFixture
{
    internal static NativeBattleWaterExecutionFrame WithContext(NativeBattleWaterExecutionFrame frame)
    {
        BattleActorModels models = OriginalModels(Enumerable.Range(0, 7).Select(slot => (slot, new NativeBattleGraphicsModel(0)))).Models;
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, NativeBattleScriptedEffectMemory.Address);
        return frame with { Core = frame.Core with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(models) } };
    }

    internal static NativeBattleWaterContextServices Services()
    {
        return new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => null, OriginalPackets, () => OriginalAliases, BattleContactTaskStorageContract.OriginalArena);
    }

    internal static NativeBattleWaterExecutionFrame BindActor(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner)
    {
        return NativeBattleCinematicActorAccess.WriteSlotActor(WithContext(frame), owner, OriginalAliases);
    }
}
