using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMotionPreemptionTests
{
    public static void Run()
    {
        CheckJumpPreemption(0);
        CheckJumpPreemption(4);
        CheckOriginal();
        Console.WriteLine("PASS native priority preemption and jump restart");
    }

    private static void CheckJumpPreemption(int updaterTicks)
    {
        PcWalkmeshTriangle floor = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        Ff7FieldResources field = new("motion-fixture", null!, null!, null!, [], [floor], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 3, new() { Actors = [new() { Slot = 0, Entity = 0, EntityBound = true, Model = 0, Animation = 2, DefaultAnimation = 2, LastAnimationFrame = 2 }] }) { Movement = new([floor], new short[512], new byte[129]) };
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        entries[2] = 13;
        FieldProgram program = new([0, 0xC0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0xBA, 2, 1, 0], [entries]);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        FieldVmSnapshot bound = machine.Capture();
        bound.Entities[0] = bound.Entities[0] with { ModelBound = true };
        machine = FieldMachine.Restore(program, bound, host);
        Check(machine.TryRequestScript(0, 1, 6), "The native jump script was not admitted.");
        machine.AdvanceTick();
        FieldHostRequest original = machine.Capture().PendingRequests!.Single(request => request.Command is FieldActorJump);
        for (int tick = 0; tick < updaterTicks; tick++)
        {
            host.AdvanceActors();
        }
        Check(host.GetActor(0).Jump?.Completed == (updaterTicks == 4), "The jump did not retain its expected native active or completed-but-unpolled state.");
        Check(!machine.TryRequestScript(0, 2, 7) && host.GetActor(0).Jump is not null, "A rejected lower-priority request cancelled the movement owner.");
        Check(machine.TryRequestScript(0, 2, 5), "The higher-priority animation script was not admitted.");
        Check(host.GetActor(0).Jump is null && !machine.Capture().PendingRequests!.Any(request => request.Id == original.Id), "Successful preemption retained the cancelled jump mode or its pending host request.");
        bool restarted = false;
        for (int tick = 0; tick < 16; tick++)
        {
            machine.AdvanceTick();
            host.AdvanceActors();
            restarted = machine.Capture().PendingRequests!.Any(request => request.Command is FieldActorJump && request.Id != original.Id);
            if (restarted)
            {
                break;
            }
        }
        Check(restarted && host.GetActor(0).Jump is not null, "The resumed lower script consumed stale completion instead of restarting its native jump.");
        long newOwner = host.GetActor(0).Jump!.RequestId;
        host.Cancel(original);
        Check(host.GetActor(0).Jump?.RequestId == newOwner, "Cancelling an obsolete receipt cleared a different movement owner.");
    }

    private static void CheckOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PREEMPTION_SAVE");
        string? oldPath = Environment.GetEnvironmentVariable("MEMORIA_FF7_PREEMPTION_ORIGINAL");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(oldPath))
        {
            Console.WriteLine("SKIP original motion preemption: configure resources and the reviewed original/corrected continuation fixtures.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        byte[] original = File.ReadAllBytes(oldPath);
        using MemoryStream oldStream = new(original, writable: false);
        Ff7SessionSave obsolete = CombinedSessionCodec.Read(oldStream).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        bool rejected = false;
        try
        {
            using Ff7Session unsupported = Ff7Session.Restore(installation, obsolete);
        }
        catch (NotSupportedException error) when (error.Message.Contains("obsolete preemption ownership contract", StringComparison.Ordinal))
        {
            rejected = true;
        }
        Check(rejected, "An unsupported stale movement owner was silently resumed.");
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        FieldHostRequest animation = saved.Vm.PendingRequests!.Single(request => request.Command is FieldAnimation { Blocking: true });
        bool restarted = false;
        for (int callback = 0; callback < 16; callback++)
        {
            session.SetInput(0, 0);
            session.AdvanceTick(advanceClock: false);
            FieldHostRequest[] pending = session.Machine.Capture().PendingRequests ?? [];
            restarted = !pending.Any(request => request.Id == animation.Id) && pending.Any(request => request.Entity == animation.Entity && request.Command is FieldActorJump);
            if (restarted)
            {
                break;
            }
        }
        Check(restarted, "The corrected original continuation did not complete the higher-priority animation and restart the suspended jump.");
        Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(saved.Progress), JsonSerializer.SerializeToNode(session.Progress)), "The ownership check changed recorded rewards, inventory or RNG.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)) && original.AsSpan().SequenceEqual(File.ReadAllBytes(oldPath)), "The ownership check changed its standalone sources.");
        Console.WriteLine("PASS corrected original animation completion and native jump restart");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
