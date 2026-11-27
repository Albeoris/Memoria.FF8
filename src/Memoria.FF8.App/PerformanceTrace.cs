using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Memoria.FF8;

/// <summary>Game-thread capture of consecutive presented frames. Subphase timings are inclusive wall times.</summary>
internal sealed class PerformanceTrace
{
    private readonly StringBuilder row = new(512);
    private StreamWriter? writer;
    private long previousPresent;
    private long firstPresent;
    private long previousAllocation;
    private TimeSpan previousGcPause;
    private long previousPacingWaitTicks;
    private long nextFlush;
    private long frames;
    private long finalizedBytes;

    internal bool IsRecording => writer is not null;
    internal string? FilePath { get; private set; }
    internal long BytesWritten => writer?.BaseStream.Position ?? finalizedBytes;
    internal string? Error { get; private set; }

    internal bool Start(string directory)
    {
        if (IsRecording)
        {
            return false;
        }

        Error = null;
        FilePath = null;
        finalizedBytes = 0;
        firstPresent = 0;
        frames = 0;
        try
        {
            Directory.CreateDirectory(directory);
            string name = $"performance-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv";
            string path = Path.GetFullPath(Path.Combine(directory, name));
            FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize: 1);
            writer = new StreamWriter(stream, new UTF8Encoding(false), 8192);
            FilePath = path;
            writer.WriteLine("frame,elapsed_ms,scene,field_id,encounter_id,interval_ms,update_ms,scene_update_ms,battle_presentation_update_ms,draw_ms,scene_render_ms,post_present_ms,present_ms,pacing_wait_ms,software_limit_fps,game_thread_allocated_bytes,gc0_total,gc1_total,gc2_total,gc_pause_ms,battle_callbacks,battle_setup_ms,battle_requests_effects_ms,battle_packets_contacts_status_ms,battle_models_ms,battle_stage_commit_ms,battle_camera_sound_ms,battle_ordering_ms,battle_render_tail_ms,battle_scene_replay_ms,battle_completion_ms");
            Flush(Stopwatch.GetTimestamp());
            return true;
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            Error = failure.Message;
            Stop();
            return false;
        }
    }

    internal void Presented(string scene, int? fieldId, int? encounterId, long presentBegin, long presentEnd, PresentationPacerObservation pacer, in DiagnosticHostFramePhases phases)
    {
        if (writer is null)
        {
            return;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread();
        TimeSpan gcPause = GC.GetTotalPauseDuration();
        if (firstPresent == 0)
        {
            firstPresent = presentEnd;
        }
        else
        {
            DiagnosticNativeFramePhases native = phases.Native;
            long sceneRender = phases.SceneRender + phases.BattleSceneRender;
            frames++;
            try
            {
                row.Clear();
                row.Append(CultureInfo.InvariantCulture, $"{frames},{Ms(presentEnd - firstPresent):F3},{scene},{fieldId},{encounterId}");
                row.Append(CultureInfo.InvariantCulture, $",{Ms(presentEnd - previousPresent):F3},{Ms(phases.Update):F3},{Ms(phases.SceneUpdate):F3},{Ms(phases.BattlePresentationUpdate):F3},{Ms(phases.Draw):F3},{Ms(sceneRender):F3}");
                row.Append(CultureInfo.InvariantCulture, $",{Ms(phases.PostPresent):F3},{Ms(presentEnd - presentBegin):F3},{Ms(pacer.PacingWaitTicks - previousPacingWaitTicks):F3},{pacer.FrameLimit},{allocated - previousAllocation}");
                row.Append(CultureInfo.InvariantCulture, $",{GC.CollectionCount(0)},{GC.CollectionCount(1)},{GC.CollectionCount(2)},{(gcPause - previousGcPause).TotalMilliseconds:F3},{native.Callbacks}");
                row.Append(CultureInfo.InvariantCulture, $",{Ms(native.Setup):F3},{Ms(native.RequestsAndEffects):F3},{Ms(native.PacketsContactsAndStatus):F3},{Ms(native.Models):F3},{Ms(native.StageAndProducerCommit):F3}");
                row.Append(CultureInfo.InvariantCulture, $",{Ms(native.CameraAndSound):F3},{Ms(native.OrderingExecution):F3},{Ms(native.RenderTail):F3},{Ms(native.SceneReplay):F3},{Ms(native.Completion):F3}");
                writer.WriteLine(row);
                if (presentEnd >= nextFlush)
                {
                    Flush(presentEnd);
                }
            }
            catch (Exception failure) when (IsFileFailure(failure))
            {
                Error = failure.Message;
                Stop();
            }
        }
        previousAllocation = allocated;
        previousGcPause = gcPause;
        previousPresent = presentEnd;
        previousPacingWaitTicks = pacer.PacingWaitTicks;
    }

    internal void Stop()
    {
        StreamWriter? active = writer;
        if (active is null)
        {
            return;
        }
        finalizedBytes = active.BaseStream.Position;
        writer = null;
        try
        {
            using (active)
            {
                active.Flush();
                finalizedBytes = active.BaseStream.Position;
            }
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            Error ??= failure.Message;
        }
    }

    private void Flush(long now)
    {
        writer!.Flush();
        nextFlush = now + Stopwatch.Frequency / 4;
    }

    private static bool IsFileFailure(Exception failure) => failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
    private static double Ms(long ticks) => ticks * (1000d / Stopwatch.Frequency);
}
