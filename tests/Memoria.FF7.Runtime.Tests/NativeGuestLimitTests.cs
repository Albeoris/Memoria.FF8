using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGuestLimitTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP native guest Limit host requires MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession initial = Create();
        Guid id = Guid.NewGuid();
        initial.BindGuestVitals(new(id, new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17));
        Ff7BattleSnapshot state = initial.Capture();
        Ff7BattleActorState sleeping = state.Actors[0];
        state.Actors[0] = sleeping with { Gauge = ushort.MaxValue - 1, Actor = sleeping.Actor with { Statuses = Ff7NativeBattleActionResolver.Sleep }, Timers = new(0, 1, 0) };
        Ff7BattleSession battle = Create(state);
        LimitProbe source = new(id);
        battle.BindGuestAttack(source);
        battle.Advance();
        Check(source.ReadyCalls == 0 && battle.ReadyPlayer is null, "A sleeping guest must not roll Crisis at the gauge threshold.");
        for (int tick = 0; tick < 100 && battle.ReadyPlayer is null; tick++)
        {
            battle.Advance();
        }
        Check(source.ReadyCalls == 1 && battle.ReadyPlayer == 0, "The native guest Limit must get one ready admission.");
        battle.Advance();
        Check(battle.Commands(0).Count(command => command is { Command: Ff7BattleCommand.Limit, Argument: 0, ValidTargets: not 0 }) == 1 && source.ReadyCalls == 1, "Reading the menu consumed or duplicated Limit admission.");
        Check(battle.Submit(0, Ff7BattleCommand.Limit, 0, 0x10), "The host rejected a source-ready Limit.");
        int previous = 0;
        int presented = 0;
        bool death = false;
        bool released = false;
        Ff7BattleSession? cold = null;
        for (int tick = 0; tick < 300; tick++)
        {
            battle.Advance(waitForCommand: true);
            if (cold is not null)
            {
                cold.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "A multi-contact Limit diverged across cold restore.");
            }
            if (battle.Action?.GuestLimit is { } limit && limit.Contacts != previous)
            {
                previous = limit.Contacts;
                uint hp = battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints;
                Check(previous == 1 ? hp is > 0 and < 30 : previous == 2 && hp == 0, "Guest Limit contacts did not damage their target once each.");
                Check(limit.PendingHit is { Source: 0, Target: 4 } && limit.PresentedContacts == previous - 1 && battle.DisplayedHit?.Tick != battle.Tick, "A numerical receipt escaped its pending presentation boundary.");
                if (previous == 1)
                {
                    cold = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture())));
                    cold.BindGuestAttack(new LimitProbe(id, source.Invocation, source.HostTick, source.PublishedContacts, source.ReadyCalls));
                }
            }
            if (battle.Action?.GuestLimit is { } publication && publication.PresentedContacts != presented)
            {
                presented = publication.PresentedContacts;
                Check(presented == previous && publication.PendingHit is null && battle.DisplayedHit is { Source: 0, Target: 4 } display && display.Tick == battle.Tick, "A real source contact did not publish its host damage display.");
            }
            death |= battle.HasActorDeathPresentation(4);
            if (previous == 2 && battle.Action is null)
            {
                released = true;
                break;
            }
        }
        Check(previous == 2 && presented == 2 && death && released && source.Invocation is null, "The guest Limit failed to retain the final original KO presentation and release.");
        CheckMultipleGuests(original.Progress);

        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);

        void CheckMultipleGuests(Ff7Progress progress)
        {
            progress = progress with { Party = [0, 1, 255] };
            Ff7BattleSession live = CreatePair();
            Ff7BattleGuestVitalsBinding first = new(id, new('A', 64), 0, 0, 0, 0, 400, 486, 6, 14, 24, 17) { OriginName = "Squall" };
            Ff7BattleGuestVitalsBinding second = new(id, first.SourcePayloadSha256, 1, 1, 2, 2, 300, 600, 8, 12, 26, 19) { OriginName = "Quistis" };
            live.BindGuestVitals(first);
            live.ReprojectGuestVitals(0, new(486, 6, 14, 24, 17) { Dexterity = 24 });
            live.BindGuestSpeed(0, 20);
            live.BindGuestVitals(second);
            live.ReprojectGuestVitals(1, new(620, 8, 12, 27, 19) { Dexterity = 64 });
            live.BindGuestSpeed(1, 60);
            Check(live.Name(0) == "Squall" && live.Name(1) == "Quistis" && live.GuestVitalsBindings.SequenceEqual([first, second]), "The guest roster did not retain both actor identities.");

            Ff7BattleSnapshot pair = live.Capture();
            Ff7BattleGuestSlotState[] slots = pair.GuestSlots!;
            Check(slots[0].BaseDexterity == 20 && slots[1].BaseDexterity == 60 && slots[1].Projection?.MaximumHp == 620 && pair.Actors[1].Actor.HitPoints == 300, "The guest projections did not remain independent.");
            Check(pair.GuestVitals is null && pair.GuestProjection is null, "The multi-guest snapshot retained stale singular fields.");
            for (int slot = 0; slot < 2; slot++)
            {
                pair.Actors[slot] = pair.Actors[slot] with { Gauge = ushort.MaxValue - 1 };
            }
            int targetIndex = Array.FindIndex(pair.Actors, actor => actor.Actor.Slot == 4);
            Ff7BattleActorState target = pair.Actors[targetIndex];
            pair.Actors[targetIndex] = target with { Actor = target.Actor with { HitPoints = 6000, MaxHitPoints = 6000 } };
            live = CreatePair(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(pair)));
            AttackProbe left = new(id, 0);
            AttackProbe right = new(id, 1);
            live.BindGuestAttack(right);
            live.BindGuestAttack(left);
            live.Advance();
            Check(live.GetGuestLimitReadyAt(0) == live.Tick && live.GetGuestLimitReadyAt(1) == live.Tick && left.ReadyCalls == 1 && right.ReadyCalls == 1, "Each guest must receive its own Limit readiness event.");
            Check(live.Submit(0, Ff7BattleCommand.Attack, 0, 0x10) && live.Submit(1, Ff7BattleCommand.Attack, 0, 0x10), "The two ready guests could not stage their own actions.");
            for (int step = 0; step < 20 && live.Action is null; step++)
            {
                live.Advance(waitForCommand: true);
            }
            Check(live.Action?.Command.ActorSlot == 0 && left.Invocation is { ActorSlot: 0 } && right.Invocation is null && right.HostTick == live.Tick, "The host did not dispatch the first action or advance the second guest's idle clock.");

            Ff7BattleSession restored = CreatePair(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(live.Capture())));
            AttackProbe restoredLeft = new(id, 0, left.Invocation, left.HostTick, left.ContactPublished);
            AttackProbe restoredRight = new(id, 1, right.Invocation, right.HostTick, right.ContactPublished);
            restored.BindGuestAttack(restoredRight);
            restored.BindGuestAttack(restoredLeft);
            bool sawFirst = false;
            bool sawSecond = false;
            for (int step = 0; step < 40; step++)
            {
                live.Advance(waitForCommand: true);
                restored.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(restored.Capture()), "The multi-guest queue or vitals diverged after JSON restore.");
                sawFirst |= live.LastHit is { Source: 0, Target: 4 };
                sawSecond |= live.LastHit is { Source: 1, Target: 4 };
                if (sawSecond && live.Action is null)
                {
                    break;
                }
            }
            Check(sawFirst && sawSecond && live.Action is null && live.GetGuestLimitReadyAt(0) is null && live.GetGuestLimitReadyAt(1) is null && left.HostTick == live.Tick && right.HostTick == live.Tick, "The guests did not finish both queued attacks and clear their own readiness.");

            Ff7BattleSession CreatePair(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 2, snapshot: snapshot, animations: animations);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class LimitProbe(Guid encounter, Ff7BattleGuestInvocation? invocation = null, long tick = 0, int contacts = 0, int readyCalls = 0) : IFf7BattleGuestAttack, IFf7BattleGuestLimit
    {
        public int ActorSlot => 0;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation { get; private set; } = invocation;
        public long HostTick { get; private set; } = tick;
        public bool ContactPublished => PublishedContacts != 0;
        public bool LimitReady => ReadyCalls > 0 && Invocation is null;
        public int PublishedContacts { get; private set; } = contacts;
        public int ReadyCalls { get; private set; } = readyCalls;

        public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null;
        public bool SupportsLimit(Ff7BattleGuestAttackTarget target) => Supports(target);

        public void ObserveLimitReady(long hostTick, Ff7BattleAiActor source)
        {
            ReadyCalls++;
        }

        public void Begin(Ff7BattleGuestInvocation action, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
        {
            Check(action.GuestCommand == 5 && action.GuestAbility == 0, "Limit admission lost the original FFVIII command ID.");
            Invocation = action;
            HostTick = action.StartedAt;
        }

        public Ff7BattleGuestAttackUpdate Advance(long hostTick)
        {
            HostTick = hostTick;
            long age = hostTick - Invocation!.StartedAt;
            if (age == 8)
            {
                Invocation = null;
                return new(null, true);
            }
            if (age is 1 or 4)
            {
                return new(null, false) { LimitTargetCue = age == 1 ? (byte)4 : (byte)6 };
            }
            if (age is 3 or 6)
            {
                return new(null, false) { LimitPresentedContact = PublishedContacts };
            }
            if (age is not (2 or 5))
            {
                return new(null, false);
            }
            PublishedContacts++;
            Ff7BattleGuestPhysicalHit hit = new(new(6, 24, 0, 0, 14, 0, 0, 24, 17), new(0x11, age == 2 ? (byte)1 : byte.MaxValue, 255, UsePhysicalStats: true) { SuppressCritical = true }, 0);
            return new(hit, false);
        }

        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
            HostTick = hostTick;
        }
    }

    private sealed class AttackProbe(Guid encounter, int slot, Ff7BattleGuestInvocation? invocation = null, long tick = 0, bool contactPublished = false) : IFf7BattleGuestAttack, IFf7BattleGuestLimit
    {
        public int ActorSlot => slot;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation { get; private set; } = invocation;
        public long HostTick { get; private set; } = tick;
        public bool ContactPublished { get; private set; } = contactPublished;
        public bool LimitReady => false;
        public int PublishedContacts => 0;
        public int ReadyCalls { get; private set; }

        public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null;
        public bool SupportsLimit(Ff7BattleGuestAttackTarget target) => false;

        public void ObserveLimitReady(long hostTick, Ff7BattleAiActor source)
        {
            Check(source.Slot == slot, "A guest received another slot's readiness event.");
            ReadyCalls++;
        }

        public void Begin(Ff7BattleGuestInvocation action, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
        {
            Check(action.ActorSlot == slot && action.StartedAt == HostTick, "A guest received another slot's action or stale clock.");
            Invocation = action;
            ContactPublished = false;
        }

        public Ff7BattleGuestAttackUpdate Advance(long hostTick)
        {
            HostTick = hostTick;
            if (ContactPublished)
            {
                Invocation = null;
                return new(null, true);
            }
            ContactPublished = true;
            return new(new(new(6, 24, 0, 0, 14, 0, 0, 24, 17), new(0x11, 1, 255, UsePhysicalStats: true) { SuppressCritical = true }, 0), false);
        }

        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
            HostTick = hostTick;
        }
    }
}
