using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private NativeGameOverReceipt? pendingNativeGameOver;
    public NativeGameOverReceipt? NativeGameOver => services.NativeGameOver;

    private void ExecuteNativeGameOver(Fiber fiber)
    {
        // Return1 retains the issuing PC and ends only this actor's budget.
        fiber.Position--;
        if (!services.CanStoreNativeGameOver || !services.CanStoreFieldRequestPacket)
        {
            string message = "GAMEOVER requires native request-packet and terminal-loss ownership; instruction pointer and operands preserved.";
            LastFault = new(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, nameof(Jsm.Opcode.GAMEOVER), message);
            throw new EventScriptException(message);
        }
        NativeGameOverReceipt receipt = new(1, FieldId, programHash, NativeRuntimeProfile?.ExecutableHash, fiber.ObjectId, fiber.Script.Id, fiber.Position, Frame, 4);
        receipt.Validate();
        services.SetFieldRequestPacket((services.FieldRequestPacket ?? new()).WithGameOverRequest());
        pendingNativeGameOver = receipt;
    }

    private void AcceptNativeGameOverAfterCallback()
    {
        NativeGameOverReceipt? pending = pendingNativeGameOver;
        pendingNativeGameOver = null;
        if (pending is not null && !transitionRequested && services.FieldRequestPacket?.Kind == 4)
        {
            services.SetNativeGameOver(pending);
        }
    }

    private void ValidateNativeGameOverCheckpoint(NativeGameOverReceipt? receipt, long frame, string hash, int fieldId, IEnumerable<Fiber> restored)
    {
        receipt?.Validate();
        if (receipt is not null && (receipt.FieldId != fieldId || receipt.ProgramHash != hash || receipt.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || receipt.Frame != frame || !restored.Any(fiber => fiber.ObjectId == receipt.ObjectId && fiber.Script.Id == receipt.ScriptId && fiber.Position == receipt.Operation && !fiber.Complete) || program is null || (uint)receipt.Operation >= program.Operations.Count || program.Operations[receipt.Operation].Opcode != Jsm.Opcode.GAMEOVER))
        {
            throw new InvalidDataException("Native game-over receipt does not match its retained issuing VM source.");
        }
    }
}
