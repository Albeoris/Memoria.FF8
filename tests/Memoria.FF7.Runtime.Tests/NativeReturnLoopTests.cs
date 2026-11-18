using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeReturnLoopTests
{
    public static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_RETURN_LOOP_SAVE") ?? throw new InvalidOperationException("A native return fixture is required.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A return audit requested a choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        Dictionary<string, int> seen = [];
        string? prior = null;
        controller.Start();
        for (int callback = 0; callback < 512 && controller.Running && session.Battle is null; callback++)
        {
            Ff7Actor actor = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            string phase = $"{policy.State.Active?.EventId}/{policy.State.Active?.Accepted}/{policy.State.Active?.Returned}:goal={policy.State.NavigationGoal?.EventId}@{policy.State.NavigationGoal?.NavigationKey.Split(':').Last()}:ladder={actor.Ladder?.Mode}";
            if (phase != prior)
            {
                Console.WriteLine($"PHASE callback{callback}, tick{session.Host.Process.Tick}, {phase}, tri{actor.Triangle}, XYZ{actor.X4096}/{actor.Y4096}/{actor.Z4096}, step{actor.Ladder?.Step}/{actor.Ladder?.Steps}, request{actor.Ladder?.RequestId}, prefix{policy.State.NativeRoute is not null}");
                prior = phase;
            }
            string key = JsonSerializer.Serialize(new { actor.Triangle, actor.X4096, actor.Y4096, actor.Z4096, actor.Direction, actor.Ladder?.Mode, actor.Ladder?.Step, phase });
            if (seen.TryGetValue(key, out int first) && callback - first > 100)
            {
                Console.WriteLine($"REPEAT callback{callback}, first{first}, period{callback - first}, error{controller.Error}");
                throw new InvalidOperationException("Ordinary native navigation repeated its consumed source/goal cycle.");
            }
            seen.TryAdd(key, callback);
            controller.Advance(1);
        }
        Console.WriteLine($"END tick{session.Host.Process.Tick}, status{policy.State.Status}, battle{session.Battle is not null}, error{controller.Error}");
        if (session.Battle is null || controller.Error is not null)
        {
            throw new InvalidOperationException("The ordinary controller did not reach a genuine native boundary after observing the retained sources.");
        }
        if (!source.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("The original native return fixture was changed.");
        }
    }
}
