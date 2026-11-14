using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldAudioTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original world audio: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        int[] sounds = [0, 0x0B, 0x90, 0xFA, 0x10A, 0x114, 0x132, 0x17D, 0x17F, 0x1B0, 0x1B1];
        List<int> soundSelectors = [];
        List<int> musicSelectors = [];
        foreach (int world in new[] { 0, 2, 3 })
        {
            using Ff7WorldResources resources = Ff7WorldResources.Load(installation, world);
            var code = resources.ReadEvents().Code;
            int soundCount = 0;
            for (int pc = 1; pc < code.Count; pc++)
            {
                int opcode = code[pc];
                if (opcode is (int)WorldOperation.Sound or (int)WorldOperation.Music)
                {
                    Check(pc >= 2 && code[pc - 2] == 0x110, "Shipped audio producers must use the audited immediate selector operand.");
                    (opcode == (int)WorldOperation.Sound ? soundSelectors : musicSelectors).Add(code[pc - 1]);
                    soundCount += opcode == (int)WorldOperation.Sound ? 1 : 0;
                }
                if (opcode is >= 0x110 and <= 0x11F or 0x200 or 0x201)
                {
                    pc++;
                }
            }
            Check(soundCount == (world == 0 ? 35 : world == 2 ? 1 : 0), "Original world programs must retain their audited sound instruction inventory.");
        }
        Check(soundSelectors.Distinct().Order().SequenceEqual(sounds), "All original script sound selectors must be covered by general one-shot publication.");
        Check(musicSelectors.SequenceEqual(new[] { 7, 6, 6, 6 }), "Original world music uses selectors6/7 and never the native out-of-table zero selector.");

        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        persistent[29] = 255;
        using Ff7WorldRuntime runtime = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        for (int callback = 0; callback < 700 && !runtime.CanSave; callback++)
        {
            if (runtime.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                runtime.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            runtime.Advance();
        }
        Check(runtime.CanSave, "Original world entry must initialize the audio fixture.");
        Ff7WorldProcess ready = runtime.Process;
        List<FieldAudioCommand> publications = [];
        runtime.AudioRequested = publications.Add;
        foreach (int sound in sounds)
        {
            runtime.Restore(ready);
            publications.Clear();
            Check(Command(runtime, WorldOperation.Sound, sound).Completed, "Every shipped sound selector must complete synchronously.");
            Check(publications.SequenceEqual(new[] { new FieldAudioCommand(0x20, 64, sound) }), "Script sounds must publish exactly once through the shared audio owner, including sound zero.");
            Check(runtime.Process.Audio!.SoundPriority == ready.Audio!.SoundPriority && runtime.Process.Audio.Publications == ready.Audio.Publications + 1, "Script one-shots must not replace the independent vehicle engine priority owner.");
            Check(runtime.Process.WhiteFadeCallbacks == (sound is 0x0B or 0x1B1 ? 2 : ready.WhiteFadeCallbacks), "Only the native flash sounds must start the two-render white flash.");
            Check(runtime.Process.Audio.BattleClearsFlash == (sound == 0x1B1), "Only sound1B1 marks the flash for cancellation on battle entry.");
        }
        foreach (int selector in new[] { 6, 7 })
        {
            runtime.Restore(ready);
            publications.Clear();
            Command(runtime, WorldOperation.Music, selector);
            Check(publications.Count == 1 && publications[0].Operation == 0x14 && publications[0].Parameter1 == 4 && runtime.Process.Audio is { PlaybackEnabled: false } song && song.Song == selector, "Shipped music selectors must publish the native transition and release the script playback flag.");
        }
        string before = JsonSerializer.Serialize(runtime.Process);
        try
        {
            Command(runtime, WorldOperation.Music, 0);
            throw new InvalidOperationException("An out-of-table music selector was accepted.");
        }
        catch (NotSupportedException)
        {
            Check(before == JsonSerializer.Serialize(runtime.Process), "Unsupported native out-of-table music must reject before changing the playback flag or saved state.");
        }
        using Ff7WorldRuntime cold = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        foreach (int sound in new[] { 0x0B, 0x1B1 })
        {
            runtime.Restore(ready);
            Command(runtime, WorldOperation.Sound, sound);
            cold.Restore(Cold(runtime.Process));
            Command(runtime, WorldOperation.Battle, 0);
            Command(cold, WorldOperation.Battle, 0);
            Check(runtime.Process.WhiteFadeCallbacks == (sound == 0x1B1 ? 0 : 2) && !runtime.Process.Audio!.BattleClearsFlash, "Battle entry must clear the1B1 flash marker without clearing an ordinary0B flash.");
            Check(runtime.Process.BattleState?.Pending is not null && JsonSerializer.Serialize(runtime.Process) == JsonSerializer.Serialize(cold.Process), "Cold audio continuation must preserve special flash cancellation and battle request state without executing a battle.");
        }
        runtime.Restore(ready);
        Command(runtime, WorldOperation.Sound, 0x1B1);
        cold.Restore(Cold(runtime.Process));
        runtime.Advance();
        cold.Advance();
        Check(JsonSerializer.Serialize(runtime.Process) == JsonSerializer.Serialize(cold.Process), "The next native callback must preserve independent cold flash and audio continuation.");
        Console.WriteLine("PASS shipped world sound/music inventory, general one-shot ownership, native battle flash cancellation, cold continuation and pre-mutation invalid music rejection");
    }

    private static WorldHostReply Command(Ff7WorldRuntime runtime, WorldOperation operation, int argument) => runtime.Execute(new(runtime.Process.Vm.NextRequestId, runtime.ControlledModel, runtime.ControlledModel, 0, new(operation, [argument])));

    private static Ff7WorldProcess Cold(Ff7WorldProcess state) => JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
