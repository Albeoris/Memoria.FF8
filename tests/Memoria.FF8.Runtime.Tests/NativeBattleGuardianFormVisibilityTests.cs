using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianFormVisibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LandingRestoresEveryParticipantAndPreservesPreviouslyHiddenModels(bool roundTrip)
    {
        NativeBattleGuardianFormPresentation presentation = Presentation(GuardianFormInvocationMode.EntryOnly, 2);
        if (roundTrip)
        {
            presentation = NativeBattleTestSnapshots.RestoreJson<NativeBattleGuardianFormPresentation>(NativeBattleTestSnapshots.Json(presentation))!;
        }
        ImmutableArray<NativeBattleGraphicsModel> current = HiddenModels();
        ImmutableArray<NativeBattleGraphicsModel> restored = (ImmutableArray<NativeBattleGraphicsModel>)typeof(BattleSession).GetMethod("RestoreGuardianFormVisibility", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [current, presentation])!;
        AssertVisibility(restored, presentation);
        for (int index = 0; index < restored.Length; index++)
        {
            Assert.Equal(current[index].Flags0 & ~0xC, restored[index].Flags0 & ~0xC);
            Assert.Equal(current[index].Flags2!.Value & ~4, restored[index].Flags2!.Value & ~4);
            Assert.Equal(current[index].Flags8, restored[index].Flags8);
        }
    }

    [Theory]
    [InlineData(GuardianFormInvocationMode.EntryOnly, 1, false)]
    [InlineData(GuardianFormInvocationMode.FinisherWithoutEntry, 5, false)]
    [InlineData(GuardianFormInvocationMode.FinisherWithoutEntry, 5, true)]
    public void CinematicReturnRestoresAllParticipantsAndTheCapturedHumanVisibility(GuardianFormInvocationMode mode, byte phase, bool skip)
    {
        BattleSession battle = Session();
        NativeBattleGuardianFormPresentation presentation = Presentation(mode, phase) with { SkipRequested = skip };
        NativeBattleGraphicsState graphics = new(HiddenModels(), ["actor", "other", null, "enemy", null, null, null], 0x10, 0) { GuardianFormPresentation = presentation };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, graphics);
        Return(battle);
        Assert.Null(battle.NativeGraphics!.GuardianFormPresentation);
        AssertVisibility(battle.NativeGraphics.Models, presentation);
        Assert.Equal(presentation.HumanModel.Flags0, battle.NativeGraphics.Models[0].Flags0);
        Assert.Equal(presentation.HumanModel.Flags2, battle.NativeGraphics.Models[0].Flags2);
    }

    [Fact]
    public void ActiveReturnPreservesLaterVisibilityChangesToOtherParticipants()
    {
        BattleSession battle = Session();
        NativeBattleGuardianFormPresentation presentation = Presentation(GuardianFormInvocationMode.EntryOnly, 3);
        ImmutableArray<NativeBattleGraphicsModel> current = HiddenModels();
        NativeBattleGraphicsState graphics = new(current, ["actor", "other", null, "enemy", null, null, null], 0, 0) { GuardianFormPresentation = presentation };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, graphics);
        Return(battle);
        Assert.Equal(current.Skip(1), battle.NativeGraphics!.Models.Skip(1));
    }

    [Fact]
    public void InactiveRowsKeepUnknownSecondaryFlags()
    {
        NativeBattleGuardianFormPresentation presentation = Presentation(GuardianFormInvocationMode.EntryOnly, 2);
        ImmutableArray<NativeBattleGraphicsModel> current = HiddenModels().SetItem(6, new(6, 0xC));
        ImmutableArray<NativeBattleGraphicsModel> restored = (ImmutableArray<NativeBattleGraphicsModel>)typeof(BattleSession).GetMethod("RestoreGuardianFormVisibility", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [current, presentation])!;
        Assert.Null(restored[6].Flags2);
        Assert.Equal((ushort)0, restored[6].Flags0);
    }

    private static BattleSession Session()
    {
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommonEffectTexture = CommonTexture(),
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            Combatants = [new() { Id = "actor", Name = "Actor", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
    }

    private static NativeBattleEffectInitializerAsset CommonTexture()
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        return new("common.tim", data.ToImmutableArray());
    }

    private static NativeBattleGuardianFormPresentation Presentation(GuardianFormInvocationMode mode, byte phase)
    {
        return new("actor", 1, mode, 0, phase, new(0, 6) { ModelId4 = 0, Flags2 = 4 }, NativeBattleCameraState.FromColdProcess(), NativeBattleStageState.FromColdProcess(), [6, 2, 10, 2, 6, 14, 0], 0)
        {
            ModelVisibilityFlags2 = [4, 0, 0, 0, 4, 4, null],
            HumanFocalLength = 512
        };
    }

    private static ImmutableArray<NativeBattleGraphicsModel> HiddenModels()
    {
        return Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, (ushort)(index == 6 ? 0x40C : 0x40E)) { Flags2 = 0x8C, Flags8 = (uint)index }).ToImmutableArray();
    }

    private static void AssertVisibility(ImmutableArray<NativeBattleGraphicsModel> models, NativeBattleGuardianFormPresentation presentation)
    {
        for (int index = 0; index < models.Length; index++)
        {
            Assert.Equal(presentation.ModelVisibility[index] & 0xC, models[index].Flags0 & 0xC);
            Assert.Equal((presentation.ModelVisibilityFlags2[index] ?? 0) & 4, (models[index].Flags2 ?? 0) & 4);
        }
        Assert.Equal(2, models[1].Flags0 & 6);
        Assert.Equal(2, models[3].Flags0 & 6);
        Assert.Equal(6, models[4].Flags0 & 6);
        Assert.Equal(0, models[6].Flags0 & 2);
    }

    private static void Return(BattleSession battle)
    {
        BattleGuardianFormState form = new("actor", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 1) { EntryCompleted = true };
        typeof(BattleSession).GetMethod("ReturnGuardianFormPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [battle.Combatants[0], form]);
    }
}
