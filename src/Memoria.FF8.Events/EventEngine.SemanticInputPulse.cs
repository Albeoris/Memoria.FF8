using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Queues a retained logical edge for its current native input consumer; ordinary sampling and VM execution remain responsible for its result.</summary>
    public void QueueSemanticInputPulse(ReplayScriptGoal producer, ushort mappedPulse)
    {
        ValidateSemanticScriptGoal(producer);
        if (mappedPulse == 0 || (mappedPulse & ~0x50) != 0)
        {
            throw new InvalidDataException("Semantic input pulses require an actual mapped Cancel or Confirm edge.");
        }
        if (!producer.Inferred && producer.ExecutableHash is null || program!.Operations[producer.Operation].Opcode is not (Jsm.Opcode.KEYON or Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2))
        {
            throw new InvalidDataException("Semantic input pulse does not identify its authenticated native KEY consumer.");
        }
        if (!fibers.Any(fiber => !fiber.Complete && fiber.WaitReason != WaitReason.NativeHalt && fiber.ObjectId == producer.ObjectId && fiber.Script.Id == producer.ScriptId))
        {
            throw new InvalidOperationException("Semantic input pulse requires its current unfinished native input producer.");
        }
        RunReplay(ReplayBoundary(ReplayEntryKind.InputPulse) with { ScriptGoal = producer, MappedPulse = mappedPulse }, () =>
        {
            QueueButtonEdge(mappedPulse);
            return true;
        });
    }
}
