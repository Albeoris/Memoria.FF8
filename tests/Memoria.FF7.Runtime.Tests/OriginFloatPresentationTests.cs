using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.Battles;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class OriginFloatPresentationTests
{
    public static void Run()
    {
        Check(NativeBattleFloatPresentation.Vertical50A5F0(0) == -250 && NativeBattleFloatPresentation.Vertical50A5F0(4) == -300 && NativeBattleFloatPresentation.Vertical50A5F0(12) == -200, "Float must retain the original phase, amplitude and negative arithmetic shift.");
        for (int phase = 0; phase < 256; phase++)
        {
            double biased = Math.Sin((phase << 8) * (double)0.0015339808f) * 4096d + 4503601774854144d;
            int sine = unchecked((int)((uint)BitConverter.DoubleToInt64Bits(biased) + 0x80000000u));
            short expected = unchecked((short)((-100 * ((sine + 4096) / 2) >> 12) - 200));
            Check(NativeBattleFloatPresentation.Vertical50A5F0((byte)phase) == expected, "Shared hover changed an original unsigned phase or biased conversion.");
        }
        Ff7BattleFloatPresentation clock = Ff7BattleSession.AdvanceOriginFloat(null, 10, true);
        Check(clock.Phase == 0 && clock.Offset == Ff7BattleSession.HostFloatOffset(0), "An older save must recover an initial presentation without replaying battle progress.");
        clock = Ff7BattleSession.AdvanceOriginFloat(clock, 130, true);
        Check(clock.Phase == 31 && clock.Remainder == 0 && clock.Offset == Ff7BattleSession.HostFloatOffset(30), "Float must use31/2 callbacks per second rather than the host15Hz drawing rate.");
        Ff7BattleFloatPresentation cold = JsonSerializer.Deserialize<Ff7BattleFloatPresentation>(JsonSerializer.Serialize(clock))!;
        Check(Ff7BattleSession.AdvanceOriginFloat(clock, 137, true) == Ff7BattleSession.AdvanceOriginFloat(cold, 137, true), "Float's partial presentation clock must continue identically after JSON restore.");
        Ff7BattleFloatPresentation suppressed = Ff7BattleSession.AdvanceOriginFloat(clock, 250, false);
        Check(suppressed.Phase == clock.Phase && suppressed.Offset == 0, "A source action must suppress hover without advancing its admitted phase.");
        Ff7BattleFloatPresentation wrapped = Ff7BattleSession.AdvanceOriginFloat(new(255, 0, 0, 0), 4, true);
        Check(wrapped.Phase == 0 && wrapped.Offset == Ff7BattleSession.HostFloatOffset(255), "Float must wrap the original unsigned timer after sampling it.");
        Installed();
        Console.WriteLine("PASS original Float hover arithmetic, cadence, actor and bone placement, cure, foreign pose lease and JSON continuation.");
    }

    private static void Installed()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed Float actor placement: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        using LgpArchive actors = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(actors, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, modifiers, 1, snapshot: snapshot, animations: animations);
        Ff7BattleSnapshot initial = Create().Capture();
        initial = initial with { Actors = initial.Actors.Select(actor => actor with { Actor = actor.Actor with { Statuses = 0x400 } }).ToArray() };
        Ff7BattleSnapshot floating = initial with { GuestOriginStatuses = initial.Actors.Select(actor => new Ff7BattleGuestOriginStatus(actor.Actor.Slot, Ff7GuestBattleStatuses.OriginFloat)).ToArray() };
        Ff7BattleSession ground = Create(initial);
        Ff7BattleSession live = Create(floating);
        Ff7BattleSession cold = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(floating))!);
        for (int tick = 0; tick < 64; tick++)
        {
            ground.Advance(waitForCommand: true);
            live.Advance(waitForCommand: true);
            cold = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(cold.Capture()))!);
            cold.Advance(waitForCommand: true);
            Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "An actual actor Float clock or retained pose changed after cold continuation.");
            foreach (Ff7BattleActorState actor in live.Actors)
            {
                int slot = actor.Actor.Slot;
                int offset = live.RenderActorFloatOffset(slot);
                Check(offset < 0 && live.RenderActorPlacement(slot).Position == ground.RenderActorPlacement(slot).Position + new Vector3(0, offset, 0), "Float must move every physical actor family through its retained posed placement.");
                Check(live.VisualPlacement(slot) == ground.VisualPlacement(slot), "Float must not accumulate into simulation movement, range or row placement.");
                Ff7BattleWorldJoint[] posed = live.Capture().ActorModels.Single(model => model.Slot == slot).Joints!;
                Ff7BattleWorldJoint[] grounded = ground.Capture().ActorModels.Single(model => model.Slot == slot).Joints!;
                for (int bone = 0; bone < posed.Length; bone++)
                {
                    Check(Vector3.Distance(posed[bone].ToVector(), grounded[bone].ToVector() + new Vector3(0, offset, 0)) < 0.001f, "A bone/effect/camera anchor failed to follow the presented body.");
                }
            }
        }
        Ff7BattleSnapshot cured = live.Capture() with { GuestOriginStatuses = [] };
        live = Create(cured);
        for (int tick = 0; tick < 4; tick++)
        {
            live.Advance(waitForCommand: true);
        }
        Check(live.Capture().ActorModels.All(model => model.Float is null && model.Render!.FloatOffset == 0), "Removing applied Float must release only its presentation owner.");
        Ff7BattleSession paused = Create(floating);
        Ff7BattleSession reference = Create(floating);
        for (int tick = 0; tick < 4; tick++)
        {
            paused.Advance(waitForCommand: true);
            reference.Advance(waitForCommand: true);
        }
        int target = paused.Actors.First(actor => actor.Actor.Slot >= 4).Actor.Slot;
        PoseLease lease = new(target);
        AttachLease(paused, lease);
        (int Clip, int Frame) held = paused.RenderActorPose(target);
        Ff7BattleActorModelState before = paused.Capture().ActorModels.Single(model => model.Slot == target);
        for (int tick = 0; tick < 80; tick++)
        {
            paused.Advance(waitForCommand: true);
            Check(paused.VisualPose(target) == held && paused.RenderActorPose(target) == held, "The active host pose lease must freeze live geometry and published body poses together.");
        }
        Ff7BattleActorModelState after = paused.Capture().ActorModels.Single(model => model.Slot == target);
        Check(after.Float!.Phase != before.Float!.Phase && after.Render!.Modifiers.Visible == before.Render!.Modifiers.Visible, "A pose lease must preserve the independently owned Float clock and model visibility.");
        Ff7BattleSnapshot saved = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(paused.Capture()))!;
        Ff7BattleSession restoredPaused = Create(saved);
        PoseLease restoredLease = new(target);
        AttachLease(restoredPaused, restoredLease);
        paused.Advance(waitForCommand: true);
        restoredPaused.Advance(waitForCommand: true);
        Check(JsonSerializer.Serialize(paused.Capture()) == JsonSerializer.Serialize(restoredPaused.Capture()), "The held clock must restore from its existing idle origin and rendered pose without adding a required save field.");
        lease.Paused = false;
        restoredLease.Paused = false;
        for (int tick = 0; tick < 4; tick++)
        {
            reference.Advance(waitForCommand: true);
            paused.Advance(waitForCommand: true);
            restoredPaused.Advance(waitForCommand: true);
        }
        Check(paused.RenderActorPose(target) == reference.RenderActorPose(target), "Releasing the pose lease must resume one ordinary callback without catching up the held idle interval.");
        Check(JsonSerializer.Serialize(paused.Capture()) == JsonSerializer.Serialize(restoredPaused.Capture()), "Lease release must continue identically after JSON restoration.");
    }

    private static void AttachLease(Ff7BattleSession session, PoseLease lease)
    {
        IFf7BattleGuestAttack?[] providers = (IFf7BattleGuestAttack?[])typeof(Ff7BattleSession).GetField("guestAttacks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        providers[0] = lease;
    }

    private sealed class PoseLease(int slot) : IFf7BattleGuestAttack, IFf7BattleGuestActorMotion
    {
        public bool Paused { get; set; } = true;
        public bool IsActorPosePaused(int actor) => Paused && actor == slot;
        public int ActorSlot => 0;
        public Guid EncounterId => Guid.Empty;
        public Ff7BattleGuestInvocation? Invocation => null;
        public long HostTick => 0;
        public bool ContactPublished => false;
        public bool Supports(Ff7BattleGuestAttackTarget target) => false;
        public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
        {
            throw new NotSupportedException();
        }
        public Ff7BattleGuestAttackUpdate Advance(long hostTick) => new(null, false);
        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
