using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayLineWaypointTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original field resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_WAYPOINT_SAVE") ?? throw new InvalidOperationException("Select an external interrupted line approach.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7AutoPlayActiveEvent intent = source.AutoPlay?.Active ?? throw new InvalidOperationException("The fixture has no recorded native movement intent.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected dialogue branch before the recorded world boundary."));
        bool owned = false;
        session.NativeTickCompleted += tick => owned |= tick.TransitionOwner?.Entity == intent.Entity && tick.TransitionOwner.Priority == 1;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 256 && controller.Running && session.Battle is null && session.World is null; step++)
        {
            controller.Advance(1);
        }
        if (controller.Error is not null || session.World is null || !owned)
        {
            throw new InvalidOperationException($"The native line approach failed to reach its owned world handoff: {controller.Error ?? policy.State.Status}; steps={controller.Steps}.");
        }
        if (!bytes.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("The original line-approach fixture changed.");
        }
        Console.WriteLine($"PASS original committed-waypoint release and source-owned world handoff after {controller.Steps} normal steps; persisted watchdog and source unchanged");
    }
}
