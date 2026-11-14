using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRowChangeTests
{
    public static void Run()
    {
        PrimaryTiming();
        Ff7BattleAttack record = new(255, 255, 2, 12, 65535, 334, 334, 7, 25, 0x22, 0, 255, 255, 0x19, 255, uint.MaxValue, 0x8000, 65535);
        Ff7BattleDamageActor stats = new(32, 32, 32, 32, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals vitals = new(100, 100, 20, 20, 0);
        Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));
        Ff7BattleRng actual = Random();
        Ff7BattleRng expected = Random();
        expected.NextByte();
        expected.NextByte();
        Ff7BattleActionReceipt hit = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(record), record, actual);
        Check(hit.ToggleTargetRow && hit.SuppressNumericPopup && !hit.Damage.Missed && hit.Damage.Damage == 0 && hit.Source == vitals && hit.Target == vitals, "A zero-power phase4 action must retain its hit, no-number flag and row intent without numerical/status mutation.");
        Ff7BattleAttack powered = record with { Power = 16 };
        Ff7BattleActionReceipt poweredHit = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(powered), powered, Random());
        Check(!poweredHit.SuppressNumericPopup, "An authored powered row action must retain its numeric popup.");
        Check(JsonSerializer.Serialize(actual.Capture()) == JsonSerializer.Serialize(expected.Capture()), "Zero power must retain two magic-hit rolls and skip numerical variance.");
        Ff7BattleAttack inaccurate = record with { Accuracy = 0 };
        Ff7BattleActionReceipt miss = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats with { MagicEvade = 100 }, vitals, Ff7BattleDamageAction.FromAttack(inaccurate), inaccurate, Random());
        Check(miss.Damage.Missed && !miss.ToggleTargetRow, "A natural miss must not reach phase4 row publication.");
        Ff7BattleAttack noBypass = record with { SpecialFlags = (ushort)(record.SpecialFlags & ~0x100) };
        Ff7BattleActionReceipt empty = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(noBypass), noBypass, Random());
        Check(empty.Damage.Missed && !empty.ToggleTargetRow, "The native no-effect miss predicate must retain its special-word bypass bit.");
        Ff7BattleActionReceipt reflecting = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(noBypass) with { NativeReflectPending = true }, noBypass, Random());
        Check(!reflecting.Damage.Missed && reflecting.ToggleTargetRow, "Pending reflection must retain its original no-effect-miss bypass.");
        Ff7BattleDamageAction physical = new(0x11, 16, 255);
        int front = Ff7NativeBattleDamage.Resolve(stats, stats, physical, Random()).Damage;
        int back = Ff7NativeBattleDamage.Resolve(stats with { BackRow = true }, stats, physical, Random()).Damage;
        Check(front > 0 && back == front / 2, "The existing physical damage consumer must apply native row halving.");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original Electromag row action: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        Ff7SessionSave menuSource = original.Capture();
        using Ff7Session menu = Ff7Session.Restore(installation, menuSource with { Process = menuSource.Process with { PlayerLocked = false, MenuDisabled = false, Movie = null }, Progress = progress });
        menu.GuestFieldVitals = new Guest();
        byte[] menuBefore = menu.Progress.Characters.Single(character => character.Id == 0).NativeRecord;
        Check(menu.TryChangeMenuOrder(0, 0), "The ordinary Order menu must admit the guest's FFVII party row.");
        byte[] menuAfter = menu.Progress.Characters.Single(character => character.Id == 0).NativeRecord.ToArray();
        Check(menuAfter[32] == (menuBefore[32] ^ 1), "Order must toggle only the existing native row bit.");
        menuAfter[32] = menuBefore[32];
        Check(menuAfter.SequenceEqual(menuBefore), "Guest Order changed an unrelated dormant character byte.");
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(value => value.FormationId == 70);
        SceneBattleData data = SceneBattleData.Read(formation);
        SceneBattleAttack attack = data.Attacks.Single(value => value.Id == 0x366);
        Check(data.Enemies.Single().Placement.EnemyId == 88 && attack.Data.AdditionalEffect == 0x19 && attack.Data.Power == 0, "The original Hell Rider row action changed identity or contract.");
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        using LgpArchive battleArchive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magicArchive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(battleArchive, Ff7PcExecutableData.LoadBattlePlacement(installation), magicArchive, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        foreach (string prefix in new[] { "rt", "sb", "ru" })
        {
            foreach (int selector in new[] { 0x31, 0x32 })
            {
                BattleActionSequence sequence = animations.Get(prefix, selector);
                BattleActionPoseState pose = new();
                BattleActionPoseState coldPose = new();
                for (int frame = 0; frame < 300 && !pose.Completed; frame++)
                {
                    pose = sequence.Advance(pose).State;
                    coldPose = sequence.Advance(JsonSerializer.Deserialize<BattleActionPoseState>(JsonSerializer.Serialize(coldPose))!).State;
                    Check(pose == coldPose, "Original row reaction pose changed through JSON restore.");
                }
                Check(pose.Completed && pose.TerminalOpcode == 0xFE && pose.NextInstruction == 0 && pose.ClipFrame == 0 && pose.WaitFrames == 0, "Original row selectors must hand off through their FE/C0 canonical idle contract.");
                BattleActionPoseUpdate waiting = sequence.Advance(new(NextInstruction: 1, ClipIndex: 0, ClipFrame: animations.ClipFrameCount(prefix, 0) - 1, WaitFrames: 1));
                Check(!waiting.State.Completed && waiting.State.WaitFrames == 1 && waiting.Events.Any(value => value.Opcode == 0xC0), "FE must continue dispatch when the existing native wait counter is nonzero.");
            }
        }
        Check(BattleActionInstructions.ReadUntilIdle(new byte[] { 0xFE, 0xFF }, 1) is { Count: 2 } decoded && decoded[0].Operands.Length == 0, "FE must consume zero operands and leave a non-C0 successor intact.");
        Ff7BattleGuestVitalsBinding binding = new(Guid.NewGuid(), new('A', 64), 0, 0, 0, 0, 1821, 2209, 20, 10, 50, 50);
        foreach (byte layout in new byte[] { formation.Layout, 0, 1, 2, 3, 4 }.Distinct())
        {
            byte[] authoredLayout = formation.RawScene.ToArray();
            authoredLayout[8 + (formation.FormationId & 3) * 20 + 18] = layout;
            SceneFormation layoutFormation = formation with { Layout = layout, RawScene = authoredLayout };
            Ff7BattleSession initial = new(layoutFormation, progress, kernel, executable.NativeMateriaStatModifiers, 1);
            initial.BindGuestVitals(binding);
            Ff7BattleSnapshot start = initial.Capture();
            foreach (int target in new[] { 0, 1, 4 })
            {
                foreach (bool oldRow in new[] { false, true })
                {
                    Ff7BattleSnapshot queued = start with
                    {
                        Layout = layout,
                        Actors = start.Actors.Select(actor => (target < 3 ? actor.Actor.Slot < 3 : actor.Actor.Slot == target) ? actor with { Actor = actor.Actor with { Conditions = (actor.Actor.Conditions & ~0x40U) | (oldRow ? 0x40U : 0) } } : actor).ToArray(),
                        Queue = Ff7BattleCommandQueue.Enqueue(start.Queue, new(2, 4, 0x20, (ushort)attack.Slot, (ushort)(1 << target), 0x366))
                    };
                    Ff7BattleSession Restore(Ff7BattleSnapshot snapshot) => new(layoutFormation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
                    Ff7BattleSession warm = Restore(queued);
                    Ff7BattleSession cold = Restore(queued);
                    bool prepared = false;
                    bool applied = false;
                    for (int tick = 0; tick < 2000; tick++)
                    {
                        warm.Advance(waitForCommand: true);
                        cold.Advance(waitForCommand: true);
                        if (!prepared && warm.Action is { Applied: false })
                        {
                            Check(((warm.Actors.Single(actor => actor.Actor.Slot == target).Actor.Conditions & 0x40) != 0) == oldRow, "Row must not publish before contact.");
                            cold = Restore(warm.Capture());
                            prepared = true;
                        }
                        applied |= warm.LastHit is { AttackId: 0x366 };
                        if (applied && warm.Action is null)
                        {
                            break;
                        }
                    }
                    bool changes = layout <= 2 && target < 3;
                    Ff7BattleAiActor after = warm.Actors.Single(actor => actor.Actor.Slot == target).Actor;
                    Check(prepared && applied && warm.Action is null && ((after.Conditions & 0x40) != 0) == (oldRow ^ changes), $"Original Electromag row guard/publication failed: layout={layout}, target={target}, old={oldRow}.");
                    Check(warm.LastHit!.NativeResultMarker == (changes ? (sbyte?)(0x31 + (oldRow ? 1 : 0)) : null), "Native row marker must encode the old row and obey the slot/layout guard.");
                    Check(warm.LastHit.SuppressNumericPopup, "Original zero-power Electromag must preserve the native no-number popup flag through contact.");
                    Check(after.HitPoints == queued.Actors.Single(actor => actor.Actor.Slot == target).Actor.HitPoints && after.Statuses == queued.Actors.Single(actor => actor.Actor.Slot == target).Actor.Statuses, "Row changes must preserve HP and saved statuses.");
                    Check(warm.Actors.Single(actor => actor.Actor.Slot == 4).Actor.MagicPoints == start.Actors.Single(actor => actor.Actor.Slot == 4).Actor.MagicPoints - 12, "Original Electromag must spend its source casting cost exactly once.");
                    Equal(warm, cold);
                    cold = Restore(warm.Capture());
                    for (int tick = 0; tick < 8; tick++)
                    {
                        warm.Advance(waitForCommand: true);
                        cold.Advance(waitForCommand: true);
                    }
                    Equal(warm, cold);
                    Ff7Progress published = warm.PublishVitals(progress);
                    Ff7BattleSession next = new(formation, published, kernel, executable.NativeMateriaStatModifiers, 2);
                    next.BindGuestVitals(binding with { EncounterId = Guid.NewGuid() });
                    if (target < 3)
                    {
                        Check(((next.Actors.Single(actor => actor.Actor.Slot == target).Actor.Conditions & 0x40) != 0) == (oldRow ^ changes), "The FFVII party row must survive publication and the next battle's guest binding.");
                    }
                    if (target == 0)
                    {
                        Ff7BattleGuestContactContext context = warm.GuestContactContext(0, 4, new(stats, physical, 0), true);
                        Check(context.Source.BackRow == (oldRow ^ changes), "Outgoing guest physical contact must consume the current FFVII host row.");
                        byte[] preserved = published.Characters.Single(character => character.Id == 0).NativeRecord.ToArray();
                        preserved[32] = progress.Characters.Single(character => character.Id == 0).NativeRecord[32];
                        Check(preserved.SequenceEqual(progress.Characters.Single(character => character.Id == 0).NativeRecord), "Guest row publication must preserve every other dormant record byte.");
                    }
                }
            }
        }
        Ff7BattleSession animated = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
        animated.BindGuestVitals(binding);
        Ff7BattleSnapshot animatedStart = animated.Capture();
        animatedStart = animatedStart with { Queue = Ff7BattleCommandQueue.Enqueue(animatedStart.Queue, new(2, 4, 0x20, (ushort)attack.Slot, 7, 0x366)) };
        Ff7BattleSession AnimatedRestore(Ff7BattleSnapshot snapshot) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!, animations: animations);
        animated = AnimatedRestore(animatedStart);
        Ff7BattleSession animatedCold = AnimatedRestore(animatedStart);
        bool seenReaction = false;
        bool seenContact = false;
        bool restoredReaction = false;
        for (int tick = 0; tick < 4000; tick++)
        {
            animated.Advance(waitForCommand: true);
            animatedCold.Advance(waitForCommand: true);
            if (animated.Capture().ActorModels.Any(model => model.Slot < 3 && model.Reaction && model.Action is 0x31 or 0x32))
            {
                seenReaction = true;
                if (!restoredReaction)
                {
                    Equal(animated, animatedCold);
                    animatedCold = AnimatedRestore(animated.Capture());
                    restoredReaction = true;
                }
            }
            seenContact |= animated.LastHit is { AttackId: 0x366 };
            if (seenContact && animated.Action is null && !animated.Capture().ActorModels.Any(model => model.Reaction))
            {
                break;
            }
        }
        string reactionState = string.Join(";", animated.Capture().ActorModels.Select(model => $"slot{model.Slot}:action{model.Action:X},reaction{model.Reaction},ready{model.Ready},pending{model.PendingSince},pose{model.Pose}"));
        Check(seenReaction && seenContact && animated.Action is null, $"Original animated Electromag must publish literal row reactions and complete their idle handoff: reaction={seenReaction}, contact={seenContact}, action={animated.Action?.Command}, tick={animated.Tick}, lastHit={animated.LastHit}, models={reactionState}, prepared={JsonSerializer.Serialize(animated.Capture().Sounds?.PreparedImpacts)}, due={JsonSerializer.Serialize(animated.Capture().Sounds?.DueImpacts)}.");
        Ff7BattleSoundReceipt[] effectSounds = animated.Capture().SoundRequests.Where(sound => sound.Sound is 371 or 124 or 250).ToArray();
        Check(effectSounds.Count(sound => sound.ReleaseQueueBank is null && sound.PlayCached && sound.Sound == 371) == 1 && effectSounds.Count(sound => sound.ReleaseQueueBank is null && sound.PlayCached && sound.Sound == 124) == 1 && effectSounds.Count(sound => sound.ReleaseQueueBank is null && !sound.PlayCached && sound.Sound == 250) == 1, "Animated Electromag must publish both cached entry voices and its ordinary age25 voice exactly once.");
        Check(effectSounds.Where(sound => sound.ReleaseQueueBank is not null).Select(sound => (sound.Sound, sound.ReleaseQueueBank)).SequenceEqual(new[] { ((ushort)371, (int?)15), ((ushort)124, (int?)15), ((ushort)250, (int?)15) }), "Animated Electromag must publish the three native resource releases once, preserving queue-bank ownership.");
        Equal(animated, animatedCold);
        int soundCount = animated.Capture().SoundRequests.Length;
        for (int tick = 0; tick < 8; tick++)
        {
            animated.Advance(waitForCommand: true);
            animatedCold.Advance(waitForCommand: true);
        }
        Equal(animated, animatedCold);
        Check(animated.Capture().SoundRequests.Length == soundCount, "Cold next callbacks must not replay Electromag entry, ordinary or release sound receipts.");
        Console.WriteLine("Native Electromag zero-power hit/RNG, row slot/layout guards, markers, guest/native publication, physical consumers and cold/next-battle continuation passed.");
    }

    private static void PrimaryTiming()
    {
        int warmRandom = 0;
        int coldRandom = 0;
        Ff7BattlePrimaryContext Context(Func<int> random) => new((_, _, _) => throw new InvalidOperationException("Electromag must not read its unobservable entry capture."), _ => default, _ => 0, random, JointPoint: (slot, _) => new(slot * 100, -500, 800));
        Ff7BattlePrimaryState warm = Ff7BattlePrimaryEffects.ScheduleElectromag(new(), 4, 7, Context(() => ++warmRandom));
        Check(warm.Slots[0] is Ff7BattlePrimaryElectromagController && warm.Slots[1] is Ff7BattlePrimaryStageFade, "Electromag must allocate its native main owner before the entry fade child.");
        Check(warm.PositionedSounds.Select(sound => (sound.Sound, sound.PlayCached)).SequenceEqual(new[] { ((ushort)371, true), ((ushort)124, true) }), "Entry sounds must retain their direct cached-buffer owners.");
        Ff7BattlePrimaryState cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(warm))!;
        int contacts = 0;
        int releases = 0;
        for (int age = 0; age < 80; age++)
        {
            warm = Ff7BattlePrimaryEffects.Advance(warm, Context(() => ++warmRandom));
            cold = Ff7BattlePrimaryEffects.Advance(cold, Context(() => ++coldRandom));
            Check(JsonSerializer.Serialize(warm) == JsonSerializer.Serialize(cold) && warmRandom == coldRandom, "Electromag primary callback, visual RNG or receipt changed after JSON restore.");
            Check(warm.TargetReactions.Count == (age == 35 ? 3 : 0) && warm.ImpactTargets == (age == 35 ? 7 : 0), "Electromag must publish its captured target mask once at native main age35.");
            contacts += warm.TargetReactions.Count;
            Check(warm.PositionedSounds.Count == (age == 25 ? 1 : 0) && warm.PositionedSounds.All(sound => sound.Sound == 250 && !sound.PlayCached), "Age25 sound must retain the ordinary duplicated-buffer path.");
            Check(warm.SoundReleases.Count == (age == 54 ? 3 : 0), "Electromag must release cached resources exactly when its main increments to55.");
            if (age == 54)
            {
                Check(warm.SoundReleases.Select(release => (release.Sound, release.QueueBank)).SequenceEqual(new[] { ((ushort)371, 15), ((ushort)124, 15), ((ushort)250, 15) }), "Cleanup must retain all three resource IDs and the native asynchronous queue-bank index.");
            }
            releases += warm.SoundReleases.Count;
            Check(warm.Slots.OfType<Ff7BattlePrimaryElectromagController>().Count() == (age < 54 ? 1 : 0), "Electromag main must retire at new age55 without replaying its contacts.");
            if (age is 24 or 34 or 53)
            {
                cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(warm))!;
            }
        }
        Check(contacts == 3 && releases == 3 && !warm.HasCallbacks, "Electromag must finish all native callbacks with once-only reactions and resource releases.");
        Ff7BattleSoundUpdate play = Ff7NativeBattleSound.Emit(Ff7BattleSoundState.Empty, 371, 64, playCached: true);
        Ff7BattleSoundUpdate releaseSound = Ff7NativeBattleSound.Release(play.State, 371, 15);
        Check(play.Sounds.Single().PlayCached && play.Sounds.Single().ReleaseQueueBank is null && releaseSound.Sounds.Single().ReleaseQueueBank == 15 && !releaseSound.Sounds.Single().PlayCached && releaseSound.State.Sequence == 2, "Native sound receipts must distinguish cached play from queue-barrier resource release.");
        Ff7SoundVoicePosition cached = new(0, 371, new('A', 64), 22050, 1, 1, true, 0, 10, false, 0, 0) { Cached = true };
        Ff7AudioPresentationState voices = new() { Sounds = [cached, cached with { SoundId = 124 }, cached with { Channel = 1, Cached = false }] };
        voices.Validate();
        Ff7AudioPresentationState restoredVoices = JsonSerializer.Deserialize<Ff7AudioPresentationState>(JsonSerializer.Serialize(voices))!;
        restoredVoices.Validate();
        Check(restoredVoices.Sounds.SequenceEqual(voices.Sounds) && !JsonSerializer.Serialize(cached with { Cached = false }).Contains("Cached"), "Cached sound ownership must survive JSON while historical absent flags remain ordinary voices.");
        bool duplicateRejected = false;
        try
        {
            (voices with { Sounds = [cached, cached] }).Validate();
        }
        catch (InvalidDataException)
        {
            duplicateRejected = true;
        }
        Check(duplicateRejected, "Cached voices must reject duplicate resource owners while allowing a same-ID independent channel voice.");

    }

    private static void Equal(Ff7BattleSession warm, Ff7BattleSession cold)
    {
        Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Electromag continuation changed after JSON restore.");
    }

    private sealed class Guest : IFf7GuestFieldVitals
    {
        public Ff7GuestFieldVitalsView Read() => new(0, "Squall", 20, 1821, 2209);
        public void Recover(bool clearStatuses) => throw new InvalidOperationException("Row selection must not recover guest vitals.");
        public void AddHitPoints(short amount) => throw new InvalidOperationException("Row selection must not change guest HP.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
