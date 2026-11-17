using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticIdleBattleDeclaration([property: JsonRequired] int PartyCount, [property: JsonRequired] int SettlePresents, [property: JsonRequired] int SamplePresents)
{
    internal void Validate(DiagnosticBattleRequest request)
    {
        if (PartyCount is not (3 or 11) || SettlePresents is < 30 or > 240 || SamplePresents is < 120 or > 1200 ||
            request.Driver is not null || request.Prelude is not null ||
            request.Target is not null || request.PrepareEntryBeforePresentation || request.AtbMode != BattleAtbMode.Normal ||
            request.Fixture is not { HoldEnemyCommands: true, Characters.Length: 0, GuardianForces.Length: 0, Limits: null, PocketStation: null, CombatParty.Length: 3 })
        {
            throw new InvalidDataException("Idle pacing needs three or eleven allies, held enemy commands, and no action preparation.");
        }
        _ = ReadEnemyCount(request.Roster);
    }

    internal int ReadEnemyCount(DiagnosticBattleRoster? roster)
    {
        return roster switch
        {
            { DonorCount: 4, MountedReserveSceneSlot: null, AuthoredFormationHash: null } => 5,
            { DonorCount: ExpandedBattleFormation.MaximumAddedEnemies, MountedReserveSceneSlot: null, AuthoredFormationHash: not null } when PartyCount == ExpandedBattleFormation.MaximumAllies => ExpandedBattleFormation.MaximumEnemies,
            _ => throw new InvalidDataException("Idle pacing needs one original enemy and four donors, or a hash-pinned original roster and ten donors with eleven allies.")
        };
    }
}

internal sealed partial class RecreateGame
{
    private DiagnosticIdleBattleProbe? diagnosticIdleBattle;

    private void StartDiagnosticIdleBattle(DiagnosticIdleBattleDeclaration declaration)
    {
        if (battleGpu is null || !diagnosticHostPhaseTiming.Enabled || !OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("MEMORIA_DIAGNOSTIC_VISUAL_CHANGES") == "1" || world?.Battle is not { Presentation: not null } battle ||
            world?.DiagnosticReceipt is not { } receipt)
        {
            throw new NotSupportedException("Idle pacing requires the Windows attached GPU host and pacing without the image-difference observer.");
        }

        diagnosticIdleBattle = new(declaration, battle, declaration.ReadEnemyCount(receipt.Preparation.Roster));
        battleGpu.ObserveTriangleBatches = true;
    }

    private void ObserveDiagnosticIdlePresent(long presentBegin, long presented, PresentationPacerObservation pacer, DiagnosticHostFramePhases? phases)
    {
        if (diagnosticIdleBattle is not { } idle)
        {
            return;
        }

        if (fieldFaulted || fieldRenderFaulted || world?.Battle != idle.Battle || battleGpu is not { ObserveTriangleBatches: true } gpu)
        {
            throw new InvalidOperationException("The idle presentation lost its live host.");
        }

        DiagnosticIdleBattleResult? result = idle.Presented(presentBegin, presented, pacer, phases, gpu.TriangleBatchMetrics);
        if (result is null)
        {
            return;
        }

        var graphics = GraphicsDevice.PresentationParameters;
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_OUTPUT_DIRECTORY") ?? throw new InvalidDataException("Idle pacing requires an owned report directory.");
        var report = new
        {
            initialization = world!.DiagnosticReceipt,
            graphics = new
            {
                adapter = GraphicsDevice.Adapter.Description,
                back_buffer_width = graphics.BackBufferWidth,
                back_buffer_height = graphics.BackBufferHeight,
                viewport_width = GraphicsDevice.Viewport.Width,
                viewport_height = GraphicsDevice.Viewport.Height,
                samples = graphics.MultiSampleCount,
                present_interval = graphics.PresentationInterval.ToString()
            },
            measurement = result
        };
        if (!WriteDiagnosticJson(Path.Combine(output, "idle-pacing.json"), report, false))
        {
            throw new IOException("The idle pacing result could not be written.");
        }

        diagnosticIdleBattle = null;
        gpu.ObserveTriangleBatches = false;
        world.EndDiagnosticBattle();
        ClearBattlePresentation();
        ExitGame();
    }
}

internal sealed record DiagnosticIdlePoint(long Presented, long Timestamp, long HostCallback, long GraphicsCallback, long SimulationFrame, NativeBattleCameraPose? Camera, int? Stage, long ThreadCpuTicks, double ProcessCpuMs, long AllocatedBytes, int Gen0, int Gen1, int Gen2);
internal sealed record DiagnosticIdleWindow(DiagnosticIdlePoint Start, DiagnosticIdlePoint End, DiagnosticPacingSummary Pacing);
internal sealed record DiagnosticIdleBattleResult(DiagnosticIdleWindow Entry, DiagnosticIdlePoint FirstCamera, DiagnosticIdlePoint FirstShown, string[] ShownPartyActors, string[] ShownEnemyActors, int DormantEnemies, DiagnosticIdleWindow Settled, int CameraChangesDuringSettled, uint ThreadId, GpuTriangleBatchMetrics SettledTriangleBatches);

internal sealed class DiagnosticIdleBattleProbe
{
    private readonly DiagnosticIdleBattleDeclaration declaration;
    private readonly string[] party;
    private readonly string[] enemies;
    private readonly int dormantEnemies;
    private readonly HashSet<string> geometryOwners = new(StringComparer.Ordinal);
    private readonly DiagnosticIdlePoint initial;
    private readonly uint thread = GetCurrentThreadId();
    private DiagnosticIdleBattlePointOfEntry? ready;
    private DiagnosticIdlePoint? firstCamera;
    private DiagnosticIdlePoint? firstShown;
    private DiagnosticIdlePoint? settledStart;
    private DiagnosticPresentationPacing? pacing;
    private long presented;
    private long seenVersion;
    private int cameraChanges;
    private NativeBattleCameraPose? lastSettledCamera;
    private GpuTriangleBatchMetrics settledBatchStart;

    internal BattleSession Battle { get; }

    internal DiagnosticIdleBattleProbe(DiagnosticIdleBattleDeclaration declaration, BattleSession battle, int enemyCount)
    {
        this.declaration = declaration;
        Battle = battle;
        party = battle.Combatants.Where(actor => actor.IsPlayer && actor.IsActive).Select(actor => actor.Id).Order(StringComparer.Ordinal).ToArray();
        enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && !actor.Hidden).Select(actor => actor.Id).Order(StringComparer.Ordinal).ToArray();
        dormantEnemies = battle.Combatants.Count(actor => !actor.IsPlayer && !actor.IsActive);
        if (party.Length != declaration.PartyCount || enemies.Length != enemyCount || battle.Combatants.Count(actor => !actor.IsPlayer && actor.IsActive) != enemyCount)
        {
            throw new InvalidDataException("The idle host did not allocate the declared live roster.");
        }

        initial = Capture(0, Stopwatch.GetTimestamp());
    }

    internal DiagnosticIdleBattleResult? Presented(long presentBegin, long timestamp, PresentationPacerObservation pacer, DiagnosticHostFramePhases? phases, GpuTriangleBatchMetrics batches)
    {
        presented++;
        if (Stopwatch.GetElapsedTime(initial.Timestamp, timestamp) > TimeSpan.FromSeconds(110) || Battle.Outcome != BattleOutcome.Active || Battle.ActiveAction is not null)
        {
            throw new InvalidOperationException("The idle host ran an action, ended, or exceeded its observation window.");
        }

        if (pacing is null)
        {
            pacing = new(Stopwatch.Frequency, 2048, timestamp, pacer);
        }
        else if (ready is null || settledStart is not null)
        {
            pacing.Record(presented, timestamp, presentBegin, timestamp, pacer, phases);
        }

        if (firstCamera is null && Battle.PreparedPresentation?.Camera is not null)
        {
            firstCamera = Capture(presented, timestamp);
        }
        if (firstShown is null && Battle.PreparedPresentation is { } prepared && prepared.Version != seenVersion)
        {
            seenVersion = prepared.Version;
            geometryOwners.Clear();
            foreach (NativeBattleLogicalResolvedCommand command in prepared.Primitives.LastCommands)
            {
                if (command.Pass != 0 && !command.Command.Pilot.IsEmpty && command.Command.Pilot.Identity.Bank == 0x502D40 && command.Command.PresentationActorId is { } actorId && prepared.Primitives.ActorOpacity(actorId) > 0)
                {
                    geometryOwners.Add(actorId);
                }
            }
            if (party.All(geometryOwners.Contains) && enemies.All(geometryOwners.Contains))
            {
                firstShown = Capture(presented, timestamp);
            }
        }

        if (ready is null && firstShown is not null && firstCamera is not null && Battle.NativeEntryReady && Battle.Presentation?.Camera.Mask == 0 && Battle.ObserveDiagnosticDrainBlockers().Count == 0)
        {
            ready = new(Capture(presented, timestamp), pacing.Complete());
        }
        if (ready is null)
        {
            return null;
        }

        if (settledStart is null)
        {
            if (presented - ready.Point.Presented >= declaration.SettlePresents)
            {
                settledStart = Capture(presented, timestamp);
                settledBatchStart = batches;
                lastSettledCamera = settledStart.Camera;
                pacing = new(Stopwatch.Frequency, declaration.SamplePresents, timestamp, pacer);
            }
            return null;
        }

        NativeBattleCameraPose? camera = Battle.PreparedPresentation?.Camera;
        if (camera != lastSettledCamera)
        {
            cameraChanges++;
            lastSettledCamera = camera;
        }
        if (presented - settledStart.Presented < declaration.SamplePresents)
        {
            return null;
        }

        DiagnosticIdlePoint finish = Capture(presented, timestamp);
        return new(new(initial, ready.Point, ready.Pacing), firstCamera!, firstShown!, party, enemies, dormantEnemies, new(settledStart, finish, pacing.Complete()), cameraChanges, thread, batches.Since(settledBatchStart));
    }

    private DiagnosticIdlePoint Capture(long frame, long timestamp)
    {
        if (GetCurrentThreadId() != thread || !GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user))
        {
            throw new InvalidOperationException("The idle measurements must use the same Windows render thread.");
        }

        using Process process = Process.GetCurrentProcess();
        NativeBattlePreparedPresentation? prepared = Battle.PreparedPresentation;
        return new(frame, timestamp, Battle.NativeHostCallbacks ?? 0, prepared?.Callback ?? 0, Battle.Frame, prepared?.Camera, prepared?.StageResourceId, kernel + user,
            process.TotalProcessorTime.TotalMilliseconds, GC.GetTotalAllocatedBytes(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }

    private sealed record DiagnosticIdleBattlePointOfEntry(DiagnosticIdlePoint Point, DiagnosticPacingSummary Pacing);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(nint handle, out long created, out long exited, out long kernel, out long user);
}
