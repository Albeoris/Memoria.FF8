using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMaterialRandomOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MATERIAL_RANDOM_SAVE") ?? throw new InvalidOperationException("Select a native random-execution fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        string nativeBefore = JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), session.Progress });
        FieldScriptAnalysis presentation = session.Machine.AnalyzeStochasticPresentation(12, 3, session.ReadSpatialRandomDomain());
        FieldScriptAnalysis execution = session.Machine.AnalyzeNativeRandomExecution(12, 3, session.ReadSpatialRandomDomain());
        Ff7AutoPlayTarget target = new Ff7AutoPlayDiscovery(session).Observe().Events.Single(item => item.Entity == 12 && item.Script == 3);
        Console.WriteLine($"Native random analysis: cosmetic={presentation.Complete}, execution={execution.Complete}, discovered={target.AnalysisComplete}, reason={target.SupportReason}, nativeRandom={execution.NativeRandomExecutionDomain is not null}");
        if (presentation.Complete || !execution.Complete || execution.StochasticPresentation is not null || execution.NativeRandomExecutionDomain is null || !target.AnalysisComplete || target.NativeRandomExecutionState is null || target.PossibleDestinationFields.Length != 0 || nativeBefore != JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), session.Progress }))
        {
            throw new InvalidOperationException("Native gameplay randomness was not represented separately from presentation equivalence.");
        }
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected ASK before native slap gameplay."));
        int draws = 0;
        bool nativeRequest = false;
        session.Machine.ScriptDispatched += request => nativeRequest |= request is { SourceEntity: 12, SourceProgramCounter: 0x858, TargetEntity: 4, TargetScript: 5, Accepted: true };
        session.Host.NativeOperationExecuted += (request, reply) => draws += reply.Completed && request.Command is FieldRandomCommand ? 1 : 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.Host.Process.Movie is { PreparedId: 32 } || session.MapId == 66, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 60 && controller.Steps < 4000 && controller.Running && controller.Error is null && !controller.Completed)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native slap continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, admitted={nativeRequest}, draws={draws}, receivedHits={session.Machine.Variables.ReadByte(FieldBank.MapByte, 15)}/{session.Machine.Variables.ReadByte(FieldBank.MapByte, 16)}, movie={session.Host.Process.Movie?.PreparedId}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !nativeRequest || draws == 0 || !controller.Completed || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native random gameplay did not execute its real requests, inputs and onward boundary.", controller.Failure);
        }
    }
}
