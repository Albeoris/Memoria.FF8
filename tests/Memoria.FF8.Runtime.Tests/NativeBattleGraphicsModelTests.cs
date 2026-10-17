using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsModelTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(15, 255, 0)]
    [InlineData(16, 0, 3)]
    [InlineData(77, 1, 2)]
    [InlineData(77, 2, 1)]
    [InlineData(77, 3, 0)]
    [InlineData(77, 4, 4)]
    [InlineData(77, 8, 5)]
    [InlineData(77, 12, 5)]
    [InlineData(77, 16, 243)]
    [InlineData(77, 255, 5)]
    public void LoadingInitializesByte145FromTheModelIdentityAndCompleteInformationByte(byte modelId, byte information246, byte expected)
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0), modelId, 0, ref random, information246);
        Assert.Equal(expected, model.SequenceByte145);
        Assert.Equal((short)0, model.MinimumY146);
        Assert.InRange(model.BlinkCountdown144!.Value, (byte)15, (byte)59);
    }

    [Fact]
    public void ReloadResetsByte145AndPartyIdentityIgnoresThePhysicalSlotAndMissingEnemyMetadata()
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel prior = new(6, 3) { SequenceByte145 = 200 };
        NativeBattleGraphicsModel enemy = NativeBattleGraphicsModels.CompleteLoad(prior, 77, 0, ref random, 2);
        Assert.Equal((byte)1, enemy.SequenceByte145);
        NativeBattleGraphicsModel party = NativeBattleGraphicsModels.CompleteLoad(enemy with { SequenceByte145 = 90 }, 15, 0, ref random);
        Assert.Equal((byte)0, party.SequenceByte145);
    }

    [Fact]
    public void UnknownEnemyInformationFailsBeforeAdvancingTheSharedRandomState()
    {
        NativeGlobalRandomState random = new(1234);
        NativeGlobalRandomState before = random;
        NativeBattleGraphicsModel model = new(3, 3) { SequenceByte145 = 5 };
        Assert.Throws<InvalidDataException>(() => NativeBattleGraphicsModels.CompleteLoad(model, 77, 0, ref random));
        Assert.Equal(before, random);
        Assert.Equal((byte)5, model.SequenceByte145);
    }

    private static NativeBattleGraphicsRemoval AdvanceRemoval(NativeBattleGraphicsModel model, bool frozen, bool soundBusy, byte camera) => NativeBattleGraphicsModels.AdvanceRemoval(model, frozen, soundBusy, camera, new(0x00464B6E, 0x32808080, 1f / 4096, 4503601774854144d) { LinearFadeSourceB8BA94 = 0x00808080 });

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 3)]
    public void Sequence1000OverrideBypassesTheStatusGateWithoutChangingStatusOrModelFlags(bool force, byte expected)
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0), 0, 0x18, ref random) with { Flags0 = 0x203 };
        NativeBattleGraphicsModel selected = NativeBattleGraphicsModels.SelectSequence(model, 3, force);
        Assert.Equal(expected, selected.Sequence);
        Assert.Equal(model.Flags0, selected.Flags0);
        Assert.Equal(model.Flags8, selected.Flags8);
        Assert.Equal(model.StatusTimers, selected.StatusTimers);
    }

    [Fact]
    public void ClearingAndLoadingPreserveTheNativeFieldInitializationBoundary()
    {
        NativeBattleGraphicsModel[] bank = NativeBattleGraphicsModels.ClearBank();
        Assert.Equal(7, bank.Length);
        Assert.All(bank, model => Assert.Equal((ushort)0, model.Flags0));
        Assert.All(bank, model => Assert.Null(model.ResourceFlags44));
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel loaded = NativeBattleGraphicsModels.CompleteLoad(bank[3] with { ResourceMagicId = 160 }, 77, 0x80, ref random, 0);
        Assert.Equal((ushort)3, loaded.Flags0);
        Assert.Equal((byte)8, loaded.Sequence);
        Assert.Equal((ushort)1, loaded.ResourceFlags44);
        Assert.Equal(0x80u, loaded.Flags8);
        Assert.Null(loaded.Phase6);
        Assert.Null(loaded.Alpha7);
        Assert.Equal((byte)0, loaded.CameraSequence130);
        Assert.Null(loaded.ResourceMagicId);
        Assert.NotEqual(0u, random.State);
        string serialized = JsonSerializer.Serialize(loaded);
        Assert.Equal(serialized, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGraphicsModel>(serialized)));
    }

    [Fact]
    public void BlockedKindFiveCleanupRetainsItsNativeFadeFallthroughAndCanRepeatTheIdempotentRelease()
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.StartService(NativeBattleGraphicsModels.CompleteLoad(new(3, 0), 77, 0, ref random, 0), 5);
        NativeBattleGraphicsRemoval first = AdvanceRemoval(model, false, true, 1);
        Assert.True(first.ReleaseParticipant);
        Assert.Equal((byte)2, first.Model.Phase6);
        model = first.Model;
        for (int callback = 0; callback < 13; callback++)
        {
            NativeBattleGraphicsRemoval step = AdvanceRemoval(model, false, true, 1);
            Assert.False(step.ReleaseParticipant);
            model = step.Model;
        }
        Assert.Equal((byte)15, model.Phase6);
        model = AdvanceRemoval(model, false, true, 1).Model;
        Assert.Equal((byte)0, model.Phase6);
        Assert.True(AdvanceRemoval(model, false, true, 1).ReleaseParticipant);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void NativeRemovalReleasesTheAllocationBeforeGraphicalCleanupAndOnlyOnce(byte kind)
    {
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.StartRemoval(NativeBattleGraphicsModels.CompleteLoad(new(3, 0), 77, 0, ref random, 0), kind);
        NativeBattleGraphicsModel paused = AdvanceRemoval(model, true, false, 0).Model;
        Assert.Equal((byte)0, paused.Phase6);
        for (int callback = 0; callback < 15; callback++)
        {
            NativeBattleGraphicsRemoval step = AdvanceRemoval(model, false, false, 0);
            Assert.False(step.ReleaseParticipant);
            model = step.Model;
        }
        Assert.Equal(kind, model.Kind5);
        Assert.Equal((byte)15, model.Phase6);
        model = AdvanceRemoval(model, false, false, 0).Model;
        Assert.Equal((byte)5, model.Kind5);
        NativeBattleGraphicsRemoval release = AdvanceRemoval(model, false, true, 1);
        Assert.True(release.ReleaseParticipant);
        Assert.NotEqual((ushort)0, release.Model.Flags0);
        NativeBattleGraphicsRemoval pending = AdvanceRemoval(release.Model, false, true, 0);
        Assert.False(pending.ReleaseParticipant);
        Assert.Equal((byte)5, pending.Model.Kind5);
        NativeBattleGraphicsRemoval complete = AdvanceRemoval(pending.Model, false, false, 0);
        Assert.False(complete.ReleaseParticipant);
        Assert.Equal((ushort)0, complete.Model.Flags0);
        Assert.Equal((byte)0, complete.Model.Kind5);
    }
}
