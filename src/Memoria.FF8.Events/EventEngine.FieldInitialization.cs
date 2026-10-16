using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool synchronousFieldInitialization;

    private string? UnsupportedFieldInitializationInstruction(Fiber fiber, JsmOperation operation)
    {
        return synchronousFieldInitialization && operation.Opcode == Jsm.Opcode.WAIT && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || fiber.Stack.Peek() <= 0) ? "Native synchronous WAIT requires its exact positive DWORD countdown; wraparound polling is unsupported" : null;
    }

    private void RunFieldInitialization()
    {
        //52C650 completes each module, area, door, then actor before proceeding.
        //It does not call529FF0 or advance any physical/callback clock.
        IEnumerable<int> order = program!.NativeObjects.Where(id => program.Objects[id].BackgroundModuleIndex is not null)
            .Concat(program.NativeObjects.Where(id => program.Objects[id].IsArea))
            .Concat(program.NativeObjects.Where(id => program.Objects[id].IsDoor))
            .Concat(program.NativeObjects.Take(program.NativeActorCount));
        synchronousFieldInitialization = true;
        try
        {
            foreach (int objectId in order)
            {
                Fiber fiber = fibers.Single(item => item.Initialization && item.ObjectId == objectId);
                while (!fiber.Complete)
                {
                    if (!fiber.Script.ContainsOperation(fiber.Position))
                    {
                        throw Fault(fiber, "Native initialization instruction pointer left its original script.");
                    }
                    JsmOperation operation = program.Operations[fiber.Position];
                    if (operation.Opcode == Jsm.Opcode.RET && operation.Parameter < 9)
                    {
                        //Native stops BEFORE callingRET. 52C960 later discards
                        //the init stack and preserves the live result bank.
                        fiber.Complete = true;
                        break;
                    }
                    int position = fiber.Position;
                    Run(fiber, 1);
                    if (transitionRequested)
                    {
                        throw Fault(fiber, "Native field initialization transition requires its loader continuation.");
                    }
                    if (fiber.WaitReason == WaitReason.Frames && fiber.Position > position)
                    {
                        //A consumed native return3 is not a yield in52C650.
                        fiber.WaitReason = WaitReason.None;
                    }
                    if (fiber.WaitReason != WaitReason.None)
                    {
                        throw Fault(fiber, "Native synchronous initialization cannot advance callbacks to satisfy this instruction.");
                    }
                }
            }
        }
        finally
        {
            synchronousFieldInitialization = false;
        }
    }

    private void CompleteFieldInitialization()
    {
        NativeFieldPartyLoad? nativeLoad = PrepareNativeFieldPartyLoad();
        if (HasOpaqueInitializationTrace() && nativeLoad is null)
        {
            throw new NotSupportedException("Native initialization trace requires its original automatic field-party setup before compaction and trace reads.");
        }
        // Native 52BC00 rebuilds actor bindings directly after 52C650; it does
        // not call the 52D050 reserve producer used by REFRESHPARTY/menu return.
        CompactNativeActors();
        if (nativeLoad is not null)
        {
            ApplyNativeFieldPartyLoad(nativeLoad);
        }
        else
        {
            ApplyPendingArrival();
        }
        BindNativeAnimationResources();
        if (Scene.NativeFieldInitialization is NativeFieldInitializationProof proof)
        {
            Scene.NativeFieldInitialization = proof with { Stage = NativeFieldInitializationStage.Completed };
        }
        InitializeNativeDoorDriverAfterInitialization();
        InitializeNativeModelParts();
        defaultsStarted = true;
        foreach (EventObject obj in program!.Objects)
        {
            if ((actors[obj.Index].Active || IsNativeDoorRequest(obj.Index)) && obj.Scripts.Count > 1)
            {
                Fiber queued = Queue(obj.Index, obj.Scripts[1].Id, 7, false);
                if (NativeRuntimeProfile is { IsVerified: true })
                {
                    InitializeNativeDefault(queued);
                }
            }
        }
    }
}
