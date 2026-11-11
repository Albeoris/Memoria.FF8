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
                Check(battle.DisplayedHit is { Source: 0, Target: 4 }, "A real source contact did not publish its host damage display.");
                if (previous == 1)
                {
                    cold = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture())));
                    cold.BindGuestAttack(new LimitProbe(id, source.Invocation, source.HostTick, source.PublishedContacts, source.ReadyCalls));
                }
            }
            death |= battle.HasActorDeathPresentation(4);
            if (previous == 2 && battle.Action is null)
            {
                released = true;
                break;
            }
        }
        Check(previous == 2 && death && released && source.Invocation is null, "The guest Limit failed to retain the final original KO presentation and release.");

        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);
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
            if (age is not (1 or 4))
            {
                return new(null, false);
            }
            PublishedContacts++;
            Ff7BattleGuestPhysicalHit hit = new(new(6, 24, 0, 0, 14, 0, 0, 24, 17), new(0x11, age == 1 ? (byte)1 : byte.MaxValue, 255, UsePhysicalStats: true) { SuppressCritical = true }, 0);
            return new(hit, false);
        }

        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
            HostTick = hostTick;
        }
    }
}
