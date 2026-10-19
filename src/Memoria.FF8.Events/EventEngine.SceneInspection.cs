namespace Memoria.FF8.Events;

public sealed record FieldScriptInspection(int ObjectId, int ScriptId, int Start, int End, int RunningCount, int? Operation, string Status);

public sealed partial class EventEngine
{
    /// <summary>Optional host inspection gate. A suspended script retains its instruction, operands and requests.</summary>
    public Func<int, int, bool>? IsInspectionScriptEnabled { get; set; }

    public IReadOnlyList<FieldScriptInspection> InspectScripts()
    {
        if (program is null)
        {
            return [];
        }
        return program.Objects.SelectMany(obj => obj.Scripts.Select(script =>
        {
            Fiber[] running = fibers.Where(fiber => fiber.ObjectId == obj.Index && fiber.Script.Id == script.Id && !fiber.Complete).ToArray();
            Fiber? current = running.OrderByDescending(fiber => fiber.Priority).ThenBy(fiber => fiber.Sequence).FirstOrDefault();
            string status = current is null ? "Idle" : current.WaitReason == WaitReason.None ? "Running" : $"Waiting: {current.WaitReason}";
            return new FieldScriptInspection(obj.Index, script.Id, script.Start, script.End, running.Length, current?.Position, status);
        })).ToArray();
    }
}
