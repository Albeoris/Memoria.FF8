using System.Buffers.Binary;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedStandingResourceTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Native_AC_restores_the_admitted_resource_after_dispatch_snapshots_a_staged_bank(bool hasOriginalAlias)
    {
        BattlePartyActorModel ally = new(new("ally"));
        BattleEnemyActorModel enemy = new(new("enemy"));
        BattleModelRoster roster = new([ally, enemy]);
        NativeBattleModelAliases aliases = new(roster, hasOriginalAlias ? [(NativeBattleModelSlot.Decode(0), ally)] : []);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        BattleSession session = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        byte[] originalBytes = new byte[30];
        BinaryPrimitives.WriteUInt16LittleEndian(originalBytes, 13);
        BinaryPrimitives.WriteUInt16LittleEndian(originalBytes.AsSpan(4), 28);
        BinaryPrimitives.WriteUInt16LittleEndian(originalBytes.AsSpan(26), 29);
        originalBytes[28] = 5;
        originalBytes[29] = 6;
        NativeBattleGraphicsResource original = new([.. originalBytes]) { AnimationFrames = [4, 4, 4, 4, 4, 4, 4] };
        NativeBattleHostedTestFixture.InstallModelBindings(session, new NativeBattleGraphicsBinding("ally", 1, original));

        uint row = NativeBattleFileStaging.Address + 0x200;
        uint skeletonAddress = row + 0x100;
        uint animationAddress = row + 0x200;
        uint sequenceAddress = row + 0x300;
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animation = [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0];
        byte[] stagedBytes = [2, 0, 6, 0, 8, 0, 0xAC, 2, 0];
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown()
            .Write(skeletonAddress, skeleton)
            .Write(animationAddress, animation)
            .Write(sequenceAddress, stagedBytes)
            .WriteResourcePointer(row, new(null, skeletonAddress, skeleton.Length))
            .WriteResourcePointer(row + 8, new(null, animationAddress, animation.Length))
            .WriteResourcePointer(row + 12, new(null, sequenceAddress, stagedBytes.Length));
        NativeBattleGraphicsModel source = new(3)
        {
            ModelId4 = 1,
            Flags2 = 8,
            Flags8 = 0,
            Sequence = 1,
            ResourceFlags44 = 1,
            SequenceState = new() { Default2 = 1 },
            OriginalResourceRow104 = 0x10,
            ActiveStagedResourceRow100 = row
        };
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => owner == ally ? source : new(0)) with { EntryReady = true };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(session.Presentation!, graphics);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeFileStaging))!.SetValue(session.Presentation!, staging);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeModelCpuResources))!.SetValue(session.Presentation!, cpu);
        Field("nativeActionPackets").SetValue(session, NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases));
        Field("hostedBattlePresentation").SetValue(session, true);

        NativeBattleActionHeaderPreparation closing = new(ally, 238, 1, -1, 0, 0, null);
        BattlePresentationProcess.PreparedHostedDispatch dispatch = session.Presentation!.PrepareHostedAction(closing, [new(new(enemy, 0, 1, 0, 0, 0, 0), null)]);
        session.Presentation!.PublishHostedAction(dispatch);
        NativeBattleGraphicsResource staged = dispatch.Action!.Source;
        Assert.Same(staged, Field("presentationSourceResource").GetValue(session));
        Assert.Equal(stagedBytes, staged.SequenceSection.ToArray());
        Assert.False(staged.Contains(13));

        NativeBattleGraphicsResource restored = ReadResource(session, "ReadOriginalPresentationModelResource", ally);
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(ally, source, staged, new(null, null) { OriginalResource = restored });
        Assert.Contains(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.RestoreStanding);
        Assert.Null(step.Model.ActiveStagedResourceRow100);
        Assert.Equal(0x10, step.Model.ActiveResourceRow100);
        Assert.Equal((byte)2, step.Model.Sequence);
        Assert.Equal((byte)5, step.Model.SequenceState!.Pose);
        graphics = session.Presentation!.Graphics with { ActorModels = graphics.ActorModels.Set(ally, step.Model) };

        Assert.Same(original, restored);
        Assert.Same(original, ReadResource(session, "NativeGraphicsPoseResource", graphics, ally));
        Assert.True(original.Contains(13));
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;

    private static NativeBattleGraphicsResource ReadResource(BattleSession session, string method, params object[] arguments)
    {
        return Assert.IsType<NativeBattleGraphicsResource>(typeof(BattleSession).GetMethod(method, PrivateInstance)!.Invoke(session, arguments));
    }
}
