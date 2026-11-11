using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeRenzokukenEntryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData((ushort)141, false, 5)]
    [InlineData((ushort)160, false, 4)]
    [InlineData((ushort)160, true, 4)]
    [InlineData((ushort)141, true, 5)]
    public void Original_ordinary_Renzokuken_entry_owns_native_control_and_source_replacement_across_cold_restore(ushort variant, bool finisherPending, int contacts)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the original FFVIII installation.");
        using GameAssets game = new(root!);
        using BattleAssets battle = new(root!);
        NativeBattleGraphicsResource original = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeGlobalRandomState global = game.ReadNewGameGlobalRandom();
        NativeBattleGraphicsModel caster = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(new(0, 0, 2200), new(0, 0, 2200), 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref global);
        caster = NativeBattleGraphicsModels.SelectSequence(caster, 1);
        caster = NativeBattleSequenceMachine.Advance(caster, original, new(0, 0)).Model;
        caster = caster with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(caster, original.PoseSource!) };
        NativeBattleRenzokuken160WeaponTip standingTip = NativeBattleRenzokuken160Controller.ReadWeaponTip5FFEE0(caster, original);
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        CrossoverNativeRenzokukenEntryState state = CrossoverNativeRenzokukenEntry.Begin(game, 4, variant, finisherPending, random);
        Assert.Equal(variant, state.Frame.Effect.Binding.Effect);
        Assert.Equal(4, state.HostTarget);
        Assert.Equal((byte)3, state.Packets.ReadContact(0).Primary.ActorIndex);
        Assert.Equal((byte)5, state.Packets.Headers[0].ReadByte(1));
        Assert.Equal(contacts, state.Resources.ActorResource.Timeline.InputThresholds.Length);
        NativeBattleRenzokuken160State native = variant == 141 ? NativeBattleRenzokukenVariantController.ReadState(state.Frame).Core : state.Frame.Memory.Renzokuken160!;
        Assert.Equal(finisherPending, native.InitialControl);
        Assert.Equal(3, native.FirstTarget);
        Assert.Equal(random, state.Frame.Random);
        CrossoverNativeRenzokukenPrelude first = CrossoverNativeRenzokukenEntry.AdvancePrelude(state, caster);
        NativeBattleRenzokuken160ActorFlags flags = Assert.Single(first.Operations.OfType<NativeBattleRenzokuken160ActorFlags>());
        Assert.Equal((ushort)(caster.Flags0 | 0x800), flags.Flags);
        Assert.Empty(first.Operations.OfType<NativeBattleRenzokuken160ActorResource>());
        caster = caster with { Flags0 = flags.Flags };
        string saved = JsonSerializer.Serialize(CrossoverNativeRenzokukenEntry.Capture(first.State));
        CrossoverNativeRenzokukenEntryState warm = CrossoverNativeRenzokukenEntry.Restore(CrossoverNativeRenzokukenEntry.Capture(first.State));
        CrossoverNativeRenzokukenEntryState cold = CrossoverNativeRenzokukenEntry.Restore(JsonSerializer.Deserialize<CrossoverNativeRenzokukenEntryState>(saved)!);
        CrossoverNativeRenzokukenPrelude second = CrossoverNativeRenzokukenEntry.AdvancePrelude(warm, caster);
        CrossoverNativeRenzokukenPrelude restored = CrossoverNativeRenzokukenEntry.AdvancePrelude(cold, caster);
        Assert.Equal(JsonSerializer.Serialize(CrossoverNativeRenzokukenEntry.Capture(second.State)), JsonSerializer.Serialize(CrossoverNativeRenzokukenEntry.Capture(restored.State)));
        NativeBattleRenzokuken160ActorResource swap = Assert.Single(restored.Operations.OfType<NativeBattleRenzokuken160ActorResource>());
        Assert.Equal(finisherPending ? (byte)2 : (byte)1, swap.Sequence);
        (NativeBattleGraphicsModel replaced, NativeBattleGraphicsResource replacement) = CrossoverNativeRenzokukenEntry.ReplaceSource(caster, original, swap);
        Assert.Equal(swap.Sequence, replaced.Sequence);
        Assert.Equal(variant, replaced.ResourceMagicId);
        Assert.Equal(standingTip, NativeBattleRenzokuken160Controller.ReadWeaponTip5FFEE0(replaced, replacement));
        Assert.True(replacement.Contains(swap.Sequence));
        Assert.NotNull(replacement.LinkedPoseSource);
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root), "Select the original FFVII installation for the foreign approach.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session host = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7BattleSession arena = new(formation, host.Progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
        Ff7BattlePlacement source = arena.VisualPlacement(0);
        Ff7BattleActorState enemy = arena.Actors.Single(actor => actor.Actor.Slot == restored.State.HostTarget);
        ushort radius = animations.CollisionRadius(Ff7BattleAnimationResources.ModelPrefix(enemy.Identity, true));
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        (NativeBattleVector targetPoint, short yaw, _, int distance) = placement.ResolveContact(arena.VisualPlacement(restored.State.HostTarget), radius);
        NativeBattleCameraProjection projection = CrossoverNativeSpriteRenderer.Camera(arena.RenderCameraOutput, placement.Matrix);
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        CrossoverNativeRenzokukenProgram program = new(game, original, rules);
        ushort options = game.ReadNewGameLimitProgress().Options978 ?? throw new InvalidDataException("The original limit options are missing.");
        CrossoverNativeRenzokukenProgramState live = program.Begin(caster with { Flags0 = (ushort)(caster.Flags0 & ~0x800) }, restored.State.HostTarget, variant, finisherPending, distance, yaw, random, options, null);
        List<NativeBattleTriggerReceipt> timings = [];
        List<int> contactsAt = [];
        List<int> timingAt = [];
        List<(int Frame, byte Sequence)> targetAt = [];
        int sounds = 0;
        int triggerSounds = 0;
        int peakPackets = 0;
        bool coldEffect = false;
        bool coldTrigger = false;
        bool sawWidget = false;
        for (int callback = 0; callback < 300 && !live.Complete && !live.FinisherReady; callback++)
        {
            if (!coldEffect && live.Effect.Ordering?.Packets.Count > 0)
            {
                Cold();
                coldEffect = true;
            }
            if (live.Trigger.Widget is { Visible: true } widget)
            {
                Assert.Equal(variant, widget.SourceMagicId);
                Assert.True(restored.State.Resources.ActorResource.Segments.SequenceEqual(widget.Block.Segments));
                Assert.Equal(6, live.Trigger.Ui.Menu957);
                sawWidget = true;
            }
            if (live.Trigger.TriggerWindowOpen && live.PublishedContacts % 2 == 0)
            {
                live = live with { Trigger = live.Trigger.Press() };
                if (!coldTrigger)
                {
                    Cold();
                    Assert.True(live.Trigger.Widget!.PendingTrigger);
                    coldTrigger = true;
                }
            }
            CrossoverNativeRenzokukenProgramStep step = program.Advance(live, projection, 0);
            live = step.State;
            peakPackets = Math.Max(peakPackets, live.Effect.Ordering?.Packets.Count ?? 0);
            if (step.TargetCue is byte targetCue)
            {
                targetAt.Add((callback, targetCue));
            }
            if (step.Timing is not null)
            {
                timingAt.Add(callback);
            }
            if (step.Contact is { } timing)
            {
                timings.Add(timing);
                contactsAt.Add(callback);
            }
            sounds += step.Sounds.Length;
            if (step.TriggerSound)
            {
                triggerSounds++;
            }
        }
        Assert.True(coldEffect);
        Assert.True(coldTrigger);
        Assert.True(sawWidget);
        Assert.Equal(!finisherPending, live.Complete);
        Assert.Equal(finisherPending, live.FinisherReady);
        Assert.Equal(0u, live.Effect.Frame.Effect.ReturnedDescriptor);
        Assert.Null(live.Trigger.Widget);
        Assert.Null(live.Trigger.Pending);
        Assert.Equal(contacts, live.PublishedContacts);
        Assert.Equal(Enumerable.Range(1, contacts), timings.Select(timing => (int)timing.ContactIndex));
        Assert.All(timings, timing => Assert.Equal(contacts, timing.TotalContacts));
        Assert.All(timings, timing => Assert.Equal(timing.ContactIndex % 2 == 1, NativeGunbladeAttack.TriggerSucceeded(timing.Timing)));
        Assert.Equal((contacts + 1) / 2, triggerSounds);
        Assert.True(sounds >= 1);
        Assert.True(peakPackets > 69);
        Assert.NotEqual(6, live.Trigger.Ui.Menu957);
        NativeBattleRenzokuken160State finalCore = variant == 141 ? NativeBattleRenzokukenVariantController.ReadState(live.Effect.Frame).Core : live.Effect.Frame.Memory.Renzokuken160!;
        Assert.Equal(restored.State.Resources.ActorResource.Segments.Sum(segment => (int)segment), finalCore.Duration);
        Assert.Equal(128, Assert.Single(restored.Operations.OfType<NativeBattleRenzokuken160Stage>()).Intensity);
        NativeBattleRenzokuken160Sound sound = Assert.Single(restored.Operations.OfType<NativeBattleRenzokuken160Sound>());
        Assert.Equal((state.Resources.AppearanceSound, 0x8000, 128), (sound.Descriptor, sound.First, sound.Second));
        output.WriteLine($"Original Renzokuken{variant}: host4/alias3, descriptor {state.Frame.Effect.ReturnedDescriptor:X}, replacement {swap.Sequence}, retained tip {standingTip.Position}, MP approach {distance}, timings {string.Join(',', timings.Select(timing => timing.Timing))}, timing callbacks {string.Join(',', timingAt)}, B7 callbacks {string.Join(',', contactsAt)}, AF {string.Join(',', targetAt)}, peak {peakPackets} native packets, return {live.Returned}, finisher handoff {live.FinisherHandoff?.Callback:X}.");

        if (variant == 160 && finisherPending)
        {
            CrossoverNativeRoughDivideProgram rough = new(game, original, rules.SummonColors!);
            NativeBattleContactPresentation nativeContact = NativeBattleContactPresentations.Complete(9, 0, 0, 0, 255);
            NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(3, nativeContact.Sequence, nativeContact.Flags2, nativeContact.HitFlags3, 0, 0, 0);
            CrossoverNativeRoughDivideState divided = rough.Begin(live, contact);
            Assert.Equal(0x23CA1C8u, divided.Frame.Effect.ReturnedDescriptor);
            Assert.Equal(3, divided.Frame.FirstRecipient);
            Assert.Equal((byte)250, divided.Packets.Headers[1].ReadByte(1));
            int contactAt = -1;
            int posedAt = -1;
            int roughPeak = 0;
            bool roughCold = false;
            NativeBattleGraphicsResource prior = program.BindReplacement(live.Effect);
            for (int callback = 0; callback < 240 && !divided.Complete; callback++)
            {
                CrossoverNativeRoughDivideStep step = rough.Advance(divided, prior, () => new(targetPoint.X, targetPoint.Y, targetPoint.Z, 0), projection, 0);
                divided = step.State;
                roughPeak = Math.Max(roughPeak, divided.Ordering.Packets.Count);
                if (step.TargetCue == 6)
                {
                    Assert.Equal(-1, posedAt);
                    posedAt = callback;
                }
                if (step.Contact)
                {
                    Assert.Equal(-1, contactAt);
                    contactAt = callback;
                    string retainedRough = JsonSerializer.Serialize(CrossoverNativeRoughDivideProgram.Capture(divided));
                    divided = CrossoverNativeRoughDivideProgram.Restore(JsonSerializer.Deserialize<CrossoverNativeRoughDivideState>(retainedRough)!);
                    Assert.Equal(retainedRough, JsonSerializer.Serialize(CrossoverNativeRoughDivideProgram.Capture(divided)));
                    roughCold = true;
                }
            }
            Assert.True(roughCold);
            Assert.Equal(30, posedAt);
            Assert.Equal(65, contactAt);
            Assert.True(divided.Complete);
            Assert.True(roughPeak > 100);
            Assert.Equal((short)0, divided.StageDepth);
            Assert.Equal((byte)1, divided.Model.Sequence);
            output.WriteLine($"Original Rough Divide163: staged sequence and linked Revolver, AF-style cue at {posedAt}, contact at {contactAt}, peak {roughPeak} packets, cold and original common texture loader/return.");
        }

        void Cold()
        {
            string savedProgram = JsonSerializer.Serialize(CrossoverNativeRenzokukenProgram.Capture(live));
            live = CrossoverNativeRenzokukenProgram.Restore(JsonSerializer.Deserialize<CrossoverNativeRenzokukenProgramState>(savedProgram)!);
            Assert.Equal(savedProgram, JsonSerializer.Serialize(CrossoverNativeRenzokukenProgram.Capture(live)));
        }
    }
}
