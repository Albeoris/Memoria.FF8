using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleSoundTests
{
    public static void Run()
    {
        DelayedScriptSound();
        ImpactAndEscape();
        InstalledInputs();
        ReactionCallbacks();
    }

    private static void DelayedScriptSound()
    {
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleSound(new(), 0x2D6, 2);
        for (int callback = 0; callback < 2; callback++)
        {
            state = Step(state);
            Check(state.SoundIds.Count == 0, "D8 fired before its authored delay.");
        }

        state = Step(JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!);
        Ff7BattleSoundUpdate due = Ff7NativeBattleSound.Emit(Ff7BattleSoundState.Empty, state.SoundIds.Single(), Ff7NativeBattleSound.ActorPan(162));
        Check(due.Sounds.SequenceEqual(new[] { new Ff7BattleSoundReceipt(1, 0x2D6, 64) }) && !state.HasCallbacks, "D8 changed its sound word, callback boundary, current attacker or receipt.");
        Check(Step(state).SoundIds.Count == 0, "D8 repeated after consumption.");

        state = Step(Ff7BattleSecondaryEffects.ScheduleSound(state, 0x1A, 0));
        Ff7BattleSoundUpdate sameCallback = Ff7NativeBattleSound.Emit(due.State, state.SoundIds.Single(), Ff7NativeBattleSound.ActorPan(160));
        Check(sameCallback.Sounds.Single() == new Ff7BattleSoundReceipt(2, 0x1A, 64), "A zero-delay D8 must run in the same native presentation callback.");
        Check(Ff7NativeBattleSound.PointPan(-7) == 0 && Ff7NativeBattleSound.PointPan(160) == 64 && Ff7NativeBattleSound.PointPan(319) == 127 && Ff7NativeBattleSound.PointPan(320) == 127, "Native effect point panning must clamp projected coordinates before scaling.");
        Check(Ff7NativeBattleSound.ActorPan(-7) == 126 && Ff7NativeBattleSound.ActorPan(-4) == 0 && Ff7NativeBattleSound.ActorPan(319) == 126 && Ff7NativeBattleSound.ActorPan(320) == 0, "Native actor pan must use signed division and seven-bit wrapping.");

        static Ff7BattleSecondaryState Step(Ff7BattleSecondaryState current) => Ff7BattleSecondaryEffects.Advance(current, (_, _, _) => throw new InvalidOperationException("D8 sampled an actor pose."), _ => throw new InvalidOperationException("D8 sampled actor rotation."), () => throw new InvalidOperationException("D8 consumed CRT random state."));
    }

    private static void ImpactAndEscape()
    {
        Ff7BattleSoundUpdate hit = Ff7NativeBattleSound.Impact(Ff7BattleSoundState.Empty, 16, 3, 4, 160);
        Check(hit.Sounds.Single() == new Ff7BattleSoundReceipt(1, 16, 64), "Native hit sound must retain its target pan.");
        Check(Ff7NativeBattleSound.Impact(hit.State, -1, -1, 4, 160).Sounds.Count == 0, "Native sound sentinel must suppress an enemy miss.");
        Check(Ff7NativeBattleSound.Impact(hit.State, 16, -1, 4, -7).Sounds.Single() == new Ff7BattleSoundReceipt(2, 5, 126), "An enemy miss must select original sound 5.");
        Check(Ff7NativeBattleSound.Impact(hit.State, 16, -1, 0, -7).Sounds.Single().Sound == 16, "A player miss must retain its configured sound.");
        Ff7BattleSoundUpdate escape = Ff7NativeBattleSound.Escape(hit.State);
        Check(escape.Sounds.Single() == new Ff7BattleSoundReceipt(2, 0x185, 64), "Native escape must request its original centered sound once.");
        Ff7BattleSoundState cold = JsonSerializer.Deserialize<Ff7BattleSoundState>(JsonSerializer.Serialize(escape.State))!;
        Check(Ff7NativeBattleSound.Escape(cold).Sounds.Count == 0 && cold.Sequence == 2, "A restored escape must preserve its sound latch.");
        Check(Ff7NativeBattleSound.SelectChannel(0x20000, false) == 1 && Ff7NativeBattleSound.SelectChannel(0x20001, false) == 0 && Ff7NativeBattleSound.SelectChannel(0x20001, true) == 1, "Native battle sound must choose resident channel1 or the global stream from original metadata.");
    }

    private static void InstalledInputs()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        foreach ((int id, int[] expected) in new[] { (0, new[] { 18, 27, 5 }), (32, new[] { 16, 46, 47 }), (64, new[] { 50, 23, 5 }) })
        {
            ReadOnlySpan<byte> weapon = kernel[5].Data.Span.Slice(id * 44, 44);
            for (int index = 0; index < 3; index++)
            {
                Check(Ff7NativeBattleSound.WeaponImpactSound(weapon, critical: index == 1, missed: index == 2, berserk: false) == expected[index], "Native starting weapon impact sound changed its byte and high-bit projection.");
            }
        }

        foreach ((int id, uint length) in new[] { (5, 0x1A9Au), (6, 0xDACAu), (16, 0x3399u), (18, 0x2932u), (21, 0xC32u), (23, 0x337Eu), (26, 0x26FCu), (27, 0x4FD6u), (46, 0x2AF1u), (47, 0x21BEu), (50, 0x28C2u), (382, 0xD088u), (389, 0x4627u) })
        {
            Ff7SoundSample sample = Ff7SoundResources.Read(installation, id - 1);
            Check(sample.CompressedByteLength == length && !sample.Loop && Ff7NativeBattleSound.SelectChannel(sample.CompressedByteLength, sample.Loop) == 1, $"Original opening sound {id} changed its resident channel metadata.");
        }

        InstalledSessionSounds(installation, kernel);
        InstalledLimitSounds(installation, kernel);

        byte[] highSounds = kernel[5].Data.Span[..44].ToArray();
        highSounds[11] |= 7;
        Check(Ff7NativeBattleSound.WeaponImpactSound(highSounds, false, false, false) == 274 && Ff7NativeBattleSound.WeaponImpactSound(highSounds, true, false, false) == 283 && Ff7NativeBattleSound.WeaponImpactSound(highSounds, false, true, false) == 261, "Original weapon sound high bits must apply to each selected sound byte independently.");
        Check(Ff7NativeBattleSound.WeaponImpactSound(highSounds, false, false, true) == 363 && Ff7NativeBattleSound.WeaponImpactSound(highSounds, true, false, true) == 364 && Ff7NativeBattleSound.WeaponImpactSound(highSounds, false, true, true) == 5, "Berserk must select the native replacement hit, critical and miss sounds.");
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        foreach (string name in new[] { "rtab", "sbab", "ruab" })
        {
            BattleActionBank bank = BattleActionBank.Read(models.ReadEntry(models.Find(name).Single()));
            BattleActionInstruction sound = BattleActionInstructions.ReadUntilIdle(bank.Scripts[0x14], 44).First(instruction => instruction.Opcode == 0xD8);
            Check(sound.Operands.Span.SequenceEqual(new byte[] { 0, 0x1A, 0 }), "Original starting physical action must schedule sound26 with zero delay.");
        }
    }

    private static void ReactionCallbacks()
    {
        Ff7BattleReactionState state = Ff7BattleReactionClock.Schedule(null, 1, 2);
        state = Ff7BattleReactionClock.Schedule(state, 1, 4);
        state = Ff7BattleReactionClock.Schedule(state, 1, 7);
        List<int> callbacks = [];
        for (int callback = 0; callback <= 8; callback++)
        {
            if (callback == 3)
            {
                state = JsonSerializer.Deserialize<Ff7BattleReactionState>(JsonSerializer.Serialize(state))!;
            }
            Ff7BattleReactionUpdate update = Ff7BattleReactionClock.Advance(state, false);
            callbacks.AddRange(update.Targets.Select(_ => callback));
            state = update.State;
        }
        Check(callbacks.SequenceEqual(new[] { 2, 4, 7 }) && state.Pending.Length == 0, "Independent authored reaction callbacks must retain their own delays through restore.");
        state = Ff7BattleReactionClock.Schedule(null, 0x10, 0);
        state = Ff7BattleReactionClock.Schedule(state, 1, 0);
        Check(Ff7BattleReactionClock.Advance(state, true).Targets.Count == 0 && Ff7BattleReactionClock.Advance(state, false).Targets.SequenceEqual(new[] { 4, 0 }), "Overlapping native reaction schedules must publish independently in callback order.");
    }

    private static void InstalledSessionSounds(Ff7Installation installation, IReadOnlyList<KernelSection> kernel)
    {
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation));
        foreach (bool enemy in new[] { false, true })
        {
            Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
            Ff7BattleSnapshot snapshot = initial.Capture();
            SceneBattleAttack machineGun = initial.Data.Attacks.Single(attack => attack.Id == 0x110);
            Ff7BattleQueuedCommand command = enemy ? new(2, 4, 0x20, (ushort)machineGun.Slot, 1, machineGun.Id) : new(2, 0, (byte)Ff7BattleCommand.Attack, 0, 0x10);
            snapshot = snapshot with { Queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, command) };
            if (!enemy)
            {
                Ff7BattleSnapshot owned = snapshot with { Sounds = new([]) { DueEffects = [new(16, 64)] } };
                Ff7BattleSession isolated = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: owned, animations: animations);
                owned.Sounds!.DueEffects[0] = new(27, 64);
                Ff7BattleSnapshot captured = isolated.Capture();
                Check(captured.Sounds!.DueEffects.Single().Sound == 16, "Battle sound restore retained its caller's mutable effect queue.");
                captured.Sounds.DueEffects[0] = new(18, 64);
                Check(isolated.Capture().Sounds!.DueEffects.Single().Sound == 16, "Battle sound capture exposed its mutable effect queue.");
                Ff7BattleSnapshot invalid = snapshot with { Sounds = new([], -1) };
                ExpectInvalid(() => new Ff7BattleSession(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: invalid, animations: animations));
                invalid = snapshot with { Sounds = new([new(26, 256)]) };
                ExpectInvalid(() => new Ff7BattleSession(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: invalid, animations: animations));
                invalid = snapshot with { Sounds = new([], 1), SoundRequests = [new(1, 26, 64), new(1, 18, 64)] };
                ExpectInvalid(() => new Ff7BattleSession(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: invalid, animations: animations));
                invalid = snapshot with { SoundProjectedX = [160] };
                ExpectInvalid(() => new Ff7BattleSession(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: invalid, animations: animations));
            }
            Ff7BattleSession battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);
            List<Ff7BattleSoundReceipt> sounds = [];
            bool started = false;
            bool restored = false;
            for (int tick = 0; tick < 600; tick++)
            {
                battle.Advance(waitForCommand: true);
                started |= battle.Action?.Command == command;
                sounds.AddRange(battle.DrainSoundRequests());
                if (started && !restored && battle.Capture().Sounds?.PreparedImpacts.Length > 0)
                {
                    Ff7BattleSnapshot saved = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
                    battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: saved, animations: animations);
                    restored = true;
                }
                if (started && battle.Action is null)
                {
                    break;
                }
            }
            Check(started && restored && battle.Action is null, "Original sound fixture did not complete through a cold action restore.");
            Check(sounds.Count == 2 && sounds[0].Sound == (enemy ? 16 : 26) && sounds[1].Sound == (enemy ? 15 : battle.LastHit!.Damage.Critical ? 27 : 18), $"Original attack ({enemy}) sound order {string.Join(',', sounds.Select(sound => sound.Sound))}.");
            Check(sounds[0].Sequence == 1 && sounds[1].Sequence == 2 && battle.DrainSoundRequests().Length == 0, "Sound output receipts must retain consumption ordering through cold restore.");
        }
    }

    private static void InstalledLimitSounds(Ff7Installation installation, IReadOnlyList<KernelSection> kernel)
    {
        using Ff7Session original = Ff7Session.NewGame(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation));
        IReadOnlyList<Ff7BattleAttack> limits = Ff7PcExecutableData.LoadBattleLimits(installation);
        IReadOnlyList<byte> symbols = Ff7PcExecutableData.LoadBeatRushReel(installation);
        foreach ((int character, ushort attack, ushort[] expected) in new[] { (0, (ushort)0x80, new ushort[] { 26, 48, 27 }), (1, (ushort)0x87, new ushort[] { 40 }), (2, (ushort)0x95, new ushort[] { 347, 347, 347 }) })
        {
            Ff7Progress progress = original.Progress with { Party = [character, 255, 255] };
            Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations, limits: limits, beatRushSymbols: symbols);
            Ff7BattleSnapshot snapshot = initial.Capture();
            snapshot.Actors[0] = snapshot.Actors[0] with { LimitGauge = byte.MaxValue };
            Ff7BattleQueuedCommand command = new(2, 0, (byte)Ff7BattleCommand.Limit, attack, 0x10);
            snapshot = snapshot with { Queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, command), BeatRush = character == 2 ? new(0, 0x10, new(0, 0, true)) : null };
            Ff7BattleSession battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations, limits: limits, beatRushSymbols: symbols);
            List<Ff7BattleSoundReceipt> sounds = [];
            bool started = false;
            bool cold = false;
            for (int tick = 0; tick < 1200; tick++)
            {
                battle.Advance(waitForCommand: true);
                started |= battle.Action?.Command.Argument == attack;
                sounds.AddRange(battle.DrainSoundRequests());
                if (started && battle.Action is { } active)
                {
                    Check(active.Command.TargetMask != 0 && (active.Command.TargetMask & (active.Command.TargetMask - 1)) == 0, "The native Limit effect received a group after the final numerical selector.");
                }
                if (started && !cold && sounds.Count > 0)
                {
                    Ff7BattleSnapshot saved = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
                    battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: saved, animations: animations, limits: limits, beatRushSymbols: symbols);
                    cold = true;
                }
                if (started && battle.Action is null)
                {
                    break;
                }
            }
            Check(started && cold && battle.Action is null && sounds.Select(sound => sound.Sound).SequenceEqual(expected), $"Original Limit {attack:X2} changed sound callback order after restore: {string.Join(',', sounds.Select(sound => sound.Sound))}.");
        }
    }

    private static void ExpectInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("A corrupt native sound snapshot was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
