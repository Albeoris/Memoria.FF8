using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class GuestContinuationTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP guest continuation host requires MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = CounterFormation(SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300]);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        CheckContinuation(1, 0x10);
        CheckContinuation(17, 0x10);
        CheckContinuation(1, 0x30);
        Console.WriteLine("PASS guest continuation: single, seventeen-contact and group children retain one turn through deferred host responses.");

        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);

        void CheckContinuation(int repetitions, ushort targets)
        {
            Guid encounter = Guid.NewGuid();
            Ff7BattleSession initial = Create();
            initial.BindGuestVitals(new(encounter, new('A', 64), 0, 0, 0, 0, 400, 400, 6, 14, 24, 17));
            Ff7BattleSnapshot seed = initial.Capture();
            seed.Actors[0] = seed.Actors[0] with { Gauge = ushort.MaxValue - 1 };
            int enemy = Array.FindIndex(seed.Actors, actor => actor.Actor.Slot == 4);
            seed.Actors[enemy] = seed.Actors[enemy] with { Actor = seed.Actors[enemy].Actor with { HitPoints = 1, MaxHitPoints = 1 } };
            if ((targets & 0x20) != 0)
            {
                int other = Array.FindIndex(seed.Actors, actor => actor.Actor.Slot == 5);
                Check(other >= 0, "The group continuation fixture requires its second original enemy occurrence.");
                seed.Actors[other] = seed.Actors[other] with { Actor = seed.Actors[other].Actor with { HitPoints = 500, MaxHitPoints = 500 } };
            }
            Ff7BattleSession battle = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(seed)));
            Probe source = new(encounter, battle, repetitions, targets);
            battle.BindGuestAttack(source);
            for (int tick = 0; tick < 200 && battle.ReadyPlayer != 0; tick++)
            {
                battle.Advance();
            }
            Check(battle.ReadyPlayer == 0 && source.ReadyCalls == 1, "The parent must acquire exactly one ready turn.");
            Check(battle.SubmitGuestLimit(0, 4), "The original parent could not acquire its selected target.");
            bool parentSeen = false;
            bool childSeen = false;
            bool waitingSeen = false;
            bool blockedWaitSeen = false;
            bool finished = false;
            ushort retainedGauge = 0;
            int waitTicks = 0;
            for (int tick = 0; tick < 600; tick++)
            {
                battle.Advance(waitForCommand: true);
                Ff7BattleSnapshot current = battle.Capture();
                Ff7BattleActorState target = current.Actors.Single(actor => actor.Actor.Slot == 4);
                if (battle.Action?.GuestLimit is not null)
                {
                    if (!parentSeen)
                    {
                        retainedGauge = current.Actors[0].Gauge;
                    }
                    parentSeen = true;
                    Check(target.AiMemory[0] == 0 && !battle.HasActorDeathPresentation(4), "The parent published its deferred death callback or KO presentation early.");
                }
                if (battle.Action is { GuestContinuation: { } continuation, GuestComposite: { } composite } active)
                {
                    childSeen = true;
                    Check(parentSeen && active.Command.ActorSlot == 0 && continuation.Origin.CommandId == (byte)Ff7BattleCommand.Limit && continuation.CompletedContacts == 1, "The child lost its original parent turn and contact provenance.");
                    Check(active.Command.TargetMask == targets && composite.Contacts.Length == repetitions * System.Numerics.BitOperations.PopCount((uint)targets), "The child changed its frozen recipients or deduplicated repeated contacts.");
                    Check(composite.Contacts.Any(contact => contact.HitDescription.PreparedContact is { Invocation.TargetSlot: 4, TargetBefore.Hp: 0 }), "The original lethal parent recipient was removed from the child schedule.");
                    Check(current.Actors[0].Gauge == retainedGauge && battle.GetGuestLimitReadyAt(0) is not null && source.ReadyCalls == 1, "The parent-to-child handoff released or rerolled the original turn.");
                    if (!continuation.ResponsesPublished)
                    {
                        Check(target.AiMemory[0] == 0 && !battle.HasActorDeathPresentation(4), "A child contact or foreign hurt request finalized its deferred KO early.");
                    }
                    else
                    {
                        bool blocked = !battle.NativeActorModelsReady || current.PrimaryEffects?.HasCallbacks == true;
                        waitingSeen = true;
                        blockedWaitSeen |= blocked;
                        waitTicks++;
                        Check(target.AiMemory[0] == 1 && source.ChildCompletions == 1, $"The waiting child duplicated or omitted a final callback: deathCallbacks={target.AiMemory[0]}, childCompletions={source.ChildCompletions}.");
                        if ((targets & 0x20) != 0)
                        {
                            Ff7BattleActorState other = current.Actors.Single(actor => actor.Actor.Slot == 5);
                            Check(other.AiMemory[1] == 1 && other.AiMemory[2] == 1, "The group recipient's generic and physical counters must each publish once at final completion.");
                        }
                    }
                }
                if (childSeen && battle.Action is null)
                {
                    Check(waitingSeen && blockedWaitSeen && waitTicks > 0 && battle.NativeActorModelsReady && current.PrimaryEffects?.HasCallbacks != true && source.Invocation is null, "The child released before the deferred native model callbacks finished.");
                    Check(target.AiMemory[0] == 1 && source.ParentBegins == 1 && source.ChildBegins == 1 && source.Preparations == 1 && source.Published == repetitions * System.Numerics.BitOperations.PopCount((uint)targets), "The combined action duplicated a source invocation, contact, preparation or death callback.");
                    Check(current.Actors[0].Gauge < retainedGauge && battle.GetGuestLimitReadyAt(0) is null, "The completed parent and child failed to release their one ready turn.");
                    finished = true;
                    break;
                }
            }
            Check(finished && source.ForeignHitChecked, "The selected continuation did not reach its foreign-hit and final-release boundaries.");
        }
    }

    private static SceneFormation CounterFormation(SceneFormation original)
    {
        // Original encounter geometry with authored callback counters and no autonomous enemy turns.
        byte[] scene = original.RawScene.ToArray();
        scene.AsSpan(0xC80).Fill(255);
        const int bankLength = 65;
        for (int prototype = 0; prototype < 3; prototype++)
        {
            int bank = 6 + prototype * bankLength;
            BinaryPrimitives.WriteUInt16LittleEndian(scene.AsSpan(0xE80 + prototype * 2), checked((ushort)bank));
            for (int callback = 2; callback <= 4; callback++)
            {
                int offset = 32 + (callback - 2) * 11;
                BinaryPrimitives.WriteUInt16LittleEndian(scene.AsSpan(0xE80 + bank + callback * 2), checked((ushort)offset));
                byte address = callback == 3 ? (byte)0 : callback == 2 ? (byte)8 : (byte)16;
                byte[] increment = [0x11, address, 0, 0x01, address, 0, 0x60, 1, 0x30, 0x90, 0x73];
                increment.CopyTo(scene.AsSpan(0xE80 + bank + offset));
            }
        }
        return original with { RawScene = scene };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Probe(Guid encounter, Ff7BattleSession battle, int repetitions, ushort targets) : IFf7BattleGuestAttack, IFf7BattleGuestLimit, IFf7BattleGuestMagic, IFf7BattleGuestCompositeActions
    {
        private ImmutableArray<Ff7BattleGuestCompositeContact> contacts = [];
        private bool child;
        public int ActorSlot => 0;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation { get; private set; }
        public long HostTick { get; private set; }
        public bool ContactPublished => child ? Published != 0 : PublishedContacts != 0;
        public bool LimitReady => ReadyCalls > 0 && Invocation is null && ParentBegins == 0;
        public int PublishedContacts { get; private set; }
        public int Published { get; private set; }
        public int ParentBegins { get; private set; }
        public int ChildBegins { get; private set; }
        public int ChildCompletions { get; private set; }
        public int Preparations { get; private set; }
        public int ReadyCalls { get; private set; }
        public bool ForeignHitChecked { get; private set; }
        public IReadOnlyList<ushort> MagicIds => [1];
        public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null;
        public bool SupportsLimit(Ff7BattleGuestAttackTarget target) => Supports(target);
        public Action PrepareLimitReady(long hostTick, Ff7BattleAiActor source, int deadParty) => () => ReadyCalls++;
        public int MagicStock(ushort ability) => 1;
        public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => ability == 1 && (targets & (1 << target.Slot)) != 0;
        public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability) => new(new(6, 24, 24, 24, 14, 0, 0, 24, 17), new(0x26, 1, 255, UsePhysicalStats: true) { AlwaysHits = true, FixedDamage = 10 }, 0);
        public void PrepareMagic(Ff7BattleGuestPhysicalHit prepared) => throw new InvalidOperationException("The selected child requires its composite contact owner.");
        public Ff7BattleGuestCompositeAction DescribeCompositeAction(ushort ability) => new([new(DescribeMagic(ability), repetitions)]);
        public bool PrepareSource(ushort ability, Func<byte> nextRandomByte, out string? failure)
        {
            Preparations++;
            failure = null;
            return true;
        }
        public string? PrepareCompositeUnsupportedReason(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> values) => null;
        public void PrepareComposite(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> values)
        {
            contacts = values.ToImmutableArray();
        }
        public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
        {
            child = invocation.GuestAbility == 1;
            if (child)
            {
                ChildBegins++;
            }
            else
            {
                ParentBegins++;
            }
            Invocation = invocation;
            HostTick = invocation.StartedAt;
        }
        public Ff7BattleGuestAttackUpdate Advance(long hostTick)
        {
            HostTick = hostTick;
            long age = hostTick - Invocation!.StartedAt;
            if (!child)
            {
                if (age == 1)
                {
                    PublishedContacts++;
                    Ff7BattleGuestPhysicalHit hit = new(new(6, 24, 0, 0, 14, 0, 0, 24, 17), new(0x11, 255, 255, UsePhysicalStats: true) { SuppressCritical = true, AlwaysHits = true }, 0);
                    return new(hit, false);
                }
                if (age == 2)
                {
                    return new(null, false) { LimitPresentedContact = 1 };
                }
                Invocation = null;
                return new(null, true) { Continuation = new(1, targets) };
            }
            if (age == 1)
            {
                battle.PresentGuestReaction(0, 4);
                Check(!battle.HasActorDeathPresentation(4), "A foreign Hit request finalized a parent-deferred KO before its selected child.");
                ForeignHitChecked = true;
                return new(null, false);
            }
            if (Published < contacts.Length)
            {
                int ordinal = Published;
                Published++;
                return new(null, false) { CompositeContacts = [ordinal] };
            }
            ChildCompletions++;
            Invocation = null;
            return new(null, true);
        }
        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
        {
            HostTick = hostTick;
        }
    }
}
