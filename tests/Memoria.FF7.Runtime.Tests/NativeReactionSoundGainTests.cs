using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeReactionSoundGainTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original actor reaction sound gain: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[600];
        IReadOnlyList<short> materia = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        const int slot = 5;
        string prefix = Ff7BattleAnimationResources.ModelPrefix(formation.Enemies[slot - 4].EnemyId, true);
        BattleActionBank bank = BattleActionBank.Read(Read(prefix + "ab"));
        BattleModelData model = BattleModelData.Read(Read(prefix + "aa"), Read(prefix + "da"));
        const short action = 3;
        IReadOnlyList<BattleActionInstruction> instructions = BattleActionInstructions.ReadUntilIdle(bank.Scripts[action], model.BodyClips.Count);
        BattleActionInstruction[] gains = instructions.Where(instruction => instruction.Opcode == 0xA3).ToArray();
        Console.WriteLine($"Original reaction {prefix}ab/{action}: {Convert.ToHexString(bank.Scripts[action].Span)}; A3 {string.Join(',', gains.Select(instruction => $"{instruction.Offset}:{instruction.Operands.Span[0]}"))}");
        Check(gains.Length == 1 && gains[0].Offset == 5 && gains[0].Operands.Span[0] == 120, "Original linked reaction A3 duration changed.");
        Ff7BattleSession initial = new(formation, progress, kernel, materia, 1, animations: animations);
        Ff7BattleSnapshot source = initial.Capture();
        source = source with { Intro = null, ActorModels = source.ActorModels.Select(actor => actor.Slot == slot ? actor with { Action = action, Pose = null, Reaction = true, Ready = false, LinkedAction = true, PendingSince = -1 } : actor).ToArray() };
        Ff7BattleSession battle = Restore(source);
        Ff7BattleSession cold = Restore(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(source))!);
        List<byte> observed = [];
        for (int callback = 0; callback < 2000; callback++)
        {
            Step(battle, slot);
            Step(cold, slot);
            Ff7BattleSnapshot current = battle.Capture();
            Check(JsonSerializer.Serialize(current) == JsonSerializer.Serialize(cold.Capture()), "Original reaction sound gain diverged after cold continuation.");
            Ff7BattleSoundReceipt[] receipts = battle.DrainSoundRequests();
            Check(JsonSerializer.Serialize(receipts) == JsonSerializer.Serialize(cold.DrainSoundRequests()), "Cold reaction sound receipts diverged.");
            foreach (Ff7BattleSoundReceipt receipt in receipts.Where(receipt => receipt.ChannelGain is not null))
            {
                Check(receipt.Channel == 1 && receipt.Sound == 0, "A3 must change channel1 gain without emitting a sample.");
                Check(receipt.ChannelGain == 0, "A3 must fade channel1 toward silence.");
                observed.Add(receipt.ChannelGainDuration);
                original.Host.ApplyBattleSoundGain(receipt);
                Check(original.Host.Process.SoundChannelVolumeFades[0] is { Target: 0, RatePerSecond: -63 }, "Original A3 duration120 must preserve current gain127 and produce a two-second fade toward0.");
            }
            cold = Restore(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            if (!current.ActorModels.Single(actor => actor.Slot == slot).Reaction)
            {
                break;
            }
        }
        Check(observed.SequenceEqual(gains.Select(instruction => instruction.Operands.Span[0])), "Original A3 gain events were lost, repeated or changed ordering.");
        Check(!battle.Capture().ActorModels.Single(actor => actor.Slot == slot).Reaction, "The original linked reaction did not reach idle.");
        Check(JsonSerializer.Serialize(source.Actors) == JsonSerializer.Serialize(battle.Capture().Actors), "A3 changed actor vitals, progress or conditions.");
        Ff7FieldHost audio = new(original.Field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(original.Host.Capture()))!);
        audio.AdvanceAudioClock(TimeSpan.FromSeconds(1), paused: true);
        Check(audio.Process.SoundChannelVolumes[0] == 127, "Pause must preserve the global channel fade.");
        audio.AdvanceAudioClock(TimeSpan.Zero);
        Check(audio.Process.SoundChannelVolumes[0] == 127, "The first live native audio callback must initialize the fade without moving it.");
        audio.AdvanceAudioClock(TimeSpan.FromSeconds(1));
        Check(audio.Process.SoundChannelVolumes[0] == 64, "A3's native integer rate must lower127 to64 after one second.");
        Ff7FieldHost restoredAudio = new(original.Field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(audio.Capture()))!);
        audio.AdvanceAudioClock(TimeSpan.FromSeconds(2));
        restoredAudio.AdvanceAudioClock(TimeSpan.FromSeconds(2));
        Check(audio.Process.SoundChannelVolumes[0] == 0 && audio.Process.SoundChannelVolumeFades[0] is null && JsonSerializer.Serialize(audio.Capture()) == JsonSerializer.Serialize(restoredAudio.Capture()), "Cold A3 fade must reach0 and retire on the same clock.");
        Ff7BattleSoundReceipt immediate = Ff7NativeBattleSound.SetChannelGain(Ff7BattleSoundState.Empty, 1, 0).Sounds.Single();
        Check(immediate.ChannelGainDuration == 0, "Historical immediate gain receipts must keep zero duration.");
        original.Host.ApplyBattleSoundGain(immediate);
        Check(original.Host.Process.SoundChannelVolumes[0] == 0 && original.Host.Process.SoundChannelVolumeFades[0] is null, "Zero-duration A3 must immediately publish silence and replace a pending fade.");
        Console.WriteLine("PASS original A3 reaction, channel1 fade, idle transfer and cold callback equality.");

        byte[] Read(string name) => models.ReadEntry(models.Find(name).Single());
        Ff7BattleSession Restore(Ff7BattleSnapshot snapshot) => new(formation, progress, kernel, materia, 1, snapshot: snapshot, animations: animations);
    }

    private static void Step(Ff7BattleSession battle, int slot)
    {
        typeof(Ff7BattleSession).GetMethod("AdvanceActorReactionModels", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [slot, slot + 1]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
