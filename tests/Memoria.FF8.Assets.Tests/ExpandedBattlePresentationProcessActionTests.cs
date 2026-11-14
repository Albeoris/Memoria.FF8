using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedBattlePresentationProcessActionTests
{
    private const int EntryBudget = 1200;
    private const int ActionBudget = 1500;
    private const int StandingReturnBudget = 240;
    private static readonly NativeBattleContactHalf NoSecondary = new(byte.MaxValue, 0, 0, 0, 0, 0, 0);

    [Theory]
    [InlineData(4, 5, 7, 15, 0)]
    [InlineData(11, 14, 7, 24, 14)]
    public void Installed_attack_then_fire_complete_their_own_descriptors_on_one_process(int allyCount, int enemyCount, byte attackRow, byte attackTarget, byte fireRow)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, allyCount, enemyCount, 4, expanded: true);
        BattleNativeAction fire = Assert.Single(assets.NativeActions, candidate => candidate.Command == 2 && candidate.Name == "Fire");
        selected = selected with
        {
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([fire.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        };
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        string logicalBefore = ExpandedBattlePresentationProcessEntryTests.LogicalState(logical);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        NativeBattleGraphicsRoster roster = process.Graphics.PresentationRoster!;

        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        BattleCombatant attackActor = logical.Combatants.Single(actor => actor.Id == roster[attackRow].ActorId);
        int attackRecord = Enumerable.Range(0, vitals.Records.Length).First(row => vitals.Records[row].CharacterId == attackActor.ModelId);
        BattleNativeAction attack = assets.ReadPlayerNativeAttackData(vitals, attackRecord);
        NativeBattleActionHeaderPreparation attackHeader = Prepare(attackRow, attack, selected.NativeCommandSequences);
        NativeBattleContactPacket attackPacket = Contact(attackTarget, attack, 137);
        Assert.NotEqual((byte)0, attackHeader.Sequence);
        Assert.Throws<InvalidOperationException>(() => process.BeginAction(attackHeader, [attackPacket]));
        for (int callback = 0; callback < EntryBudget && !process.EntryReady; callback++)
        {
            process.Advance(logical);
        }
        Assert.True(process.EntryReady, $"Installed entry stalled at callback {process.Graphics.Callback}.");
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);

        var originalModelOwners = Models(process);
        NativeBattleModelCpuResources originalCpu = process.ModelCpuResources;
        NativeBattleModelTextureCache originalCache = process.ModelTextureCache;
        NativeBattleStageTask originalStage = Assert.IsType<NativeBattleStageTask>(process.Stage.Task);
        ulong initialRandomCalls = process.GlobalRandomCalls;
        NativeBattleVector attackOrigin = process.Graphics.Models[attackRow].Transform!.Position28;
        NativeBattleVector initialDisplacement = HeaderDisplacement(process.Graphics.Models[attackRow]);
        MotionObservation initialMotion = ObserveMotion(process, attackRow);
        NativeBattleContactPacket invalidLate = attackPacket with { Primary = attackPacket.Primary with { ActorIndex = NativeBattleGraphicsModelAddress.MaximumRowCount } };
        NativeBattleGraphicsState beforeInvalid = process.Graphics;
        Assert.Throws<InvalidDataException>(() => process.BeginAction(attackHeader, [attackPacket, invalidLate]));
        Assert.Same(beforeInvalid, process.Graphics);
        Assert.Null(process.ActiveActionToken);

        long attackToken = process.BeginAction(attackHeader, [attackPacket]);
        Descriptor attackDescriptor = ReadDescriptor(process);
        Assert.Throws<InvalidOperationException>(() => process.BeginAction(attackHeader, [attackPacket]));
        ActionObservation attackResult = AdvanceAction(process, logical, attackToken, attackDescriptor, attackHeader, 0x50BB00, attackTarget);

        Assert.Equal(attackPacket, Assert.Single(attackResult.Contacts));
        Assert.True(attackResult.SawInlineContact, "Attack did not publish its AA callback.");
        Assert.True(attackResult.SourceSequenceProgressed, "The installed Attack sequence did not advance.");
        Assert.True(attackResult.SourceDisplaced && attackResult.MinimumDistance < attackResult.InitialDistance && attackResult.ApproachedAtContact,
            $"Attack did not approach its target by the first AA contact. Initial {attackResult.InitialDistance:F1}, minimum {attackResult.MinimumDistance:F1}, contact {attackResult.FirstContactDistance:F1}. Initial pose {initialMotion}, contact pose {attackResult.FirstContactMotion}.");
        MotionObservation completedMotion = ObserveMotion(process, attackRow);
        // Descriptor 104 can finish on the action callback bit before the standing sequence clears displacement.
        for (int callback = 0; callback < StandingReturnBudget && !ReturnedToStanding(process.Graphics.Models[attackRow], initialDisplacement); callback++)
        {
            process.Advance(logical);
            Assert.True(process.IsActionComplete(attackToken));
            Assert.Null(process.LastStepActionToken);
            Assert.Empty(process.LastStep.Contacts);
            ObserveDamage(process.Graphics, attackResult);
        }
        Assert.True(ReturnedToStanding(process.Graphics.Models[attackRow], initialDisplacement),
            $"Attack never returned to standing. Initial {initialMotion}, first contact {attackResult.FirstContactMotion}, descriptor completion {completedMotion}, final {ObserveMotion(process, attackRow)}.");
        Assert.Equal(initialDisplacement, HeaderDisplacement(process.Graphics.Models[attackRow]));
        Assert.Equal(attackOrigin, process.Graphics.Models[attackRow].Transform!.Position28);
        Assert.Contains(attackTarget, attackResult.ReactingRows);
        Assert.Contains(137, attackResult.DisplayedDamage);
        Assert.Equal(originalModelOwners, Models(process));

        NativeBattleActionHeaderPreparation fireHeader = Prepare(fireRow, fire, selected.NativeCommandSequences);
        Assert.NotEqual(attackHeader.Actor, fireHeader.Actor);
        ImmutableArray<NativeBattleContactPacket> firePackets = roster.EnemyRows.Select((row, index) => Contact(checked((byte)row), fire, checked((ushort)(201 + index)))).ToImmutableArray();
        Assert.Equal(enemyCount, firePackets.Length);
        long beforeFire = process.Graphics.Callback;
        long fireToken = process.BeginAction(fireHeader, firePackets);
        Descriptor fireDescriptor = ReadDescriptor(process);
        Assert.NotEqual(attackDescriptor.Serial, fireDescriptor.Serial);
        Assert.True(fireToken > attackToken);
        Assert.True(process.IsActionComplete(attackToken));
        ActionObservation fireResult = AdvanceAction(process, logical, fireToken, fireDescriptor, fireHeader, 0x50A9A0);

        Assert.Equal(firePackets.AsEnumerable(), fireResult.Contacts);
        Assert.Equal(enemyCount, fireResult.ReceivedDuringFire);
        Assert.True(fireResult.SourceSequenceProgressed, "The installed Fire casting sequence did not advance.");
        Assert.Equal(firePackets.Select(packet => packet.Primary.PresentedAmount).Order(), fireResult.DisplayedDamage.Order());
        Assert.True(process.IsActionComplete(attackToken));
        Assert.True(process.IsActionComplete(fireToken));
        Assert.Equal(beforeFire + fireResult.Frames, process.Graphics.Callback);
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);
        Assert.Equal(selected.StageId, process.Stage.ResourceStageId);
        Assert.Equal(originalStage.Callback, process.Stage.Task!.Callback);
        Assert.NotNull(process.Camera.Projection);
        Assert.True(process.GlobalRandomCalls >= initialRandomCalls);
        Assert.Same(originalCpu, process.ModelCpuResources);
        Assert.Same(originalCache, process.ModelTextureCache);
        Assert.Equal(originalModelOwners, Models(process));
        Assert.Equal(logicalBefore, ExpandedBattlePresentationProcessEntryTests.LogicalState(logical));

        process.Advance(logical);
        Assert.Null(process.LastStepActionToken);
        Assert.Empty(process.LastStep.Contacts);
    }

    private static NativeBattleActionHeaderPreparation Prepare(byte actor, BattleNativeAction action, ImmutableArray<byte> commandSequences)
    {
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(action.Command, 0, action.ActionId, commandSequences);
        NativeBattleActionDataReference? data = action.Command == 2 ? new(0x47E970, action.ActionId, action.Name) : null;
        return new(actor, action.Command, sequence, -1, action.ActionId, action.Command == 1 ? (ushort)0 : action.MagicId, data);
    }

    private static NativeBattleContactPacket Contact(byte target, BattleNativeAction action, ushort amount)
    {
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(action.DamagePresentation, 0, 0, (byte)(action.AttackFlags & 3), byte.MaxValue);
        return new(new(target, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, 0, amount, 0), NoSecondary);
    }

    private static Descriptor ReadDescriptor(ExpandedBattlePresentationProcess process)
    {
        NativeBattleDescriptorState descriptors = process.Graphics.Descriptors;
        int slot = Assert.Single(descriptors.Queue, index => descriptors.Packets[index].Command == 104);
        NativeBattleDescriptorPacket packet = descriptors.Packets[slot];
        Assert.Equal((byte)128, packet.PriorityState);
        Assert.Equal(0x1D280C4u, packet.Argument);
        Assert.DoesNotContain(descriptors.Queue, index => descriptors.Packets[index].Command == 10);
        return new(slot, packet.Serial, packet.Argument);
    }

    private static ActionObservation AdvanceAction(ExpandedBattlePresentationProcess process, BattleSession logical, long token, Descriptor descriptor, NativeBattleActionHeaderPreparation header, uint service, byte? movementTarget = null)
    {
        ActionObservation observed = new();
        NativeBattleVector? destination = movementTarget is byte row ? process.Graphics.Models[row].Transform!.Position28 : null;
        NativeBattleVector initialDisplacement = HeaderDisplacement(process.Graphics.Models[header.Actor]);
        if (destination is { } targetPosition)
        {
            observed.InitialDistance = WorldDistance(process.Graphics.Models[header.Actor], targetPosition);
            observed.MinimumDistance = observed.InitialDistance;
        }
        Assert.Equal(token, process.ActiveActionToken);
        Assert.False(process.IsActionComplete(token));
        for (int callback = 0; callback < ActionBudget && !process.IsActionComplete(token); callback++)
        {
            process.Advance(logical);
            observed.Frames++;
            Assert.Equal(token, process.LastStepActionToken);
            NativeBattleGraphicsState graphics = process.Graphics;
            if (graphics.ActionGraphics is { } action)
            {
                Assert.Equal(service, action.Callback);
                Assert.Equal(header.Actor, action.ActorIndex);
                Assert.Equal(header.Data, action.PendingData);
            }
            NativeBattleGraphicsModel source = graphics.Models[header.Actor];
            observed.SourceSequenceProgressed |= source.Sequence == header.Sequence && (source.SequenceFrame != 0 || source.SequenceState?.Offset46 > 0);
            if (destination is { } movementDestination)
            {
                double distance = WorldDistance(source, movementDestination);
                NativeBattleVector displacement = HeaderDisplacement(source);
                observed.SourceDisplaced |= displacement != default && displacement != initialDisplacement;
                observed.MinimumDistance = Math.Min(observed.MinimumDistance, distance);
                if (observed.FirstContactDistance is null && !process.LastStep.Contacts.IsEmpty)
                {
                    observed.FirstContactDistance = distance;
                    observed.FirstContactMotion = ObserveMotion(process, header.Actor);
                    observed.ApproachedAtContact = observed.SourceDisplaced && observed.MinimumDistance < observed.InitialDistance;
                }
            }
            observed.Contacts.AddRange(process.LastStep.Contacts);
            if (!process.LastStep.Contacts.IsEmpty)
            {
                Assert.False(process.IsActionComplete(token));
                observed.SawInlineContact |= graphics.ActionCallbackReady;
                if (graphics.OriginalEffect?.Binding.Effect == header.Effect && header.Effect != 0)
                {
                    observed.ReceivedDuringFire += process.LastStep.Contacts.Length;
                }
                foreach (NativeBattleContactPacket contact in process.LastStep.Contacts)
                {
                    NativeBattleGraphicsModel target = graphics.Models[contact.Primary.ActorIndex];
                    if (target.Sequence == contact.Primary.Sequence || target.SequenceState?.Pending3 == contact.Primary.Sequence)
                    {
                        observed.ReactingRows.Add(contact.Primary.ActorIndex);
                    }
                }
            }
            ObserveDamage(graphics, observed);
            if (!process.IsActionComplete(token))
            {
                Assert.Equal(token, process.ActiveActionToken);
                Assert.Contains(descriptor.Slot, graphics.Descriptors.Queue);
                NativeBattleDescriptorPacket pending = graphics.Descriptors.Packets[descriptor.Slot];
                Assert.Equal(descriptor.Serial, pending.Serial);
                Assert.Equal((ushort)104, pending.Command);
                Assert.Equal(descriptor.Argument, pending.Argument);
                observed.SawFinishedDescriptor |= pending.PriorityState == 255;
            }
        }

        Assert.True(process.IsActionComplete(token), $"Action 0x{service:X} stalled at callback {process.Graphics.Callback} after {observed.Contacts.Count} contacts.");
        Assert.Null(process.ActiveActionToken);
        NativeBattleDescriptorPacket completed = process.Graphics.Descriptors.Packets[descriptor.Slot];
        Assert.Equal(descriptor.Serial, completed.Serial);
        Assert.Equal((ushort)104, completed.Command);
        Assert.Equal(descriptor.Argument, completed.Argument);
        Assert.True(observed.SawFinishedDescriptor, "The exact descriptor never reached native completion state 255.");
        Assert.Equal((byte)0, completed.PriorityState);
        Assert.DoesNotContain(descriptor.Slot, process.Graphics.Descriptors.Queue);
        Assert.Equal(0, process.Camera.Mask & 0xFF);
        Assert.False(process.Camera.ForceCamera);
        return observed;
    }

    private static (int Row, byte? Model, int? Body, int? Linked)[] Models(ExpandedBattlePresentationProcess process)
    {
        NativeBattleGraphicsRoster roster = process.Graphics.PresentationRoster!;
        return roster.PartyRows.Concat(roster.EnemyRows).Select(row =>
        {
            NativeBattleGraphicsModel model = process.Graphics.Models[row];
            Assert.NotEqual(0, model.Flags0 & 2);
            int body = Assert.IsType<int>(model.ActiveResourceRow100);
            Assert.Equal(body, model.OriginalResourceRow104);
            Assert.Equal(row, process.ModelTextureCache.PresentationActors![body]);
            return (row, model.ModelId4, model.ActiveResourceRow100, model.LinkedTextureResourceIndex);
        }).ToArray();
    }

    private static NativeBattleVector HeaderDisplacement(NativeBattleGraphicsModel model)
    {
        NativeBattleSequenceState state = Assert.IsType<NativeBattleSequenceState>(model.SequenceState);
        return new(state.Header36, state.Header38, state.Header40);
    }

    private static bool ReturnedToStanding(NativeBattleGraphicsModel model, NativeBattleVector initialDisplacement)
    {
        return HeaderDisplacement(model) == initialDisplacement && (model.ResourceFlags44 & 0x40) != 0;
    }

    private static MotionObservation ObserveMotion(ExpandedBattlePresentationProcess process, byte row)
    {
        NativeBattleGraphicsModel model = process.Graphics.Models[row];
        NativeBattleActionMatrix matrix = Assert.IsType<NativeBattleActionMatrix>(model.GeometryWorldMatrix);
        return new(process.Graphics.Callback, model.ModelId4, model.Sequence, model.ResourceFlags44, HeaderDisplacement(model), (matrix.X, matrix.Y, matrix.Z));
    }

    private static void ObserveDamage(NativeBattleGraphicsState graphics, ActionObservation observed)
    {
        foreach (NativeBattleDamageTextDraw draw in graphics.Effects.TextDraws)
        {
            int amount = 0;
            foreach (byte glyph in draw.Glyphs.TakeWhile(glyph => glyph != 0))
            {
                Assert.InRange(glyph, (byte)56, (byte)65);
                amount = amount * 10 + glyph - 56;
            }
            observed.DisplayedDamage.Add(amount);
        }
    }

    private static double WorldDistance(NativeBattleGraphicsModel model, NativeBattleVector target)
    {
        NativeBattleActionMatrix matrix = Assert.IsType<NativeBattleActionMatrix>(model.GeometryWorldMatrix);
        double x = matrix.X - target.X;
        double z = matrix.Z - target.Z;
        return Math.Sqrt(x * x + z * z);
    }

    private sealed record Descriptor(int Slot, byte Serial, uint Argument);
    private sealed record MotionObservation(long Callback, byte? ModelId, byte? Sequence, ushort? ResourceFlags, NativeBattleVector Displacement, (int X, int Y, int Z) World);

    private sealed class ActionObservation
    {
        internal int Frames { get; set; }
        internal bool SourceSequenceProgressed { get; set; }
        internal bool SourceDisplaced { get; set; }
        internal bool ApproachedAtContact { get; set; }
        internal double InitialDistance { get; set; }
        internal double MinimumDistance { get; set; }
        internal double? FirstContactDistance { get; set; }
        internal MotionObservation? FirstContactMotion { get; set; }
        internal bool SawInlineContact { get; set; }
        internal bool SawFinishedDescriptor { get; set; }
        internal int ReceivedDuringFire { get; set; }
        internal List<NativeBattleContactPacket> Contacts { get; } = [];
        internal HashSet<byte> ReactingRows { get; } = [];
        internal HashSet<int> DisplayedDamage { get; } = [];
    }
}
