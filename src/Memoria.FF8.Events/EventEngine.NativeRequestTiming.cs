using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeRequestTiming(Fiber fiber, JsmOperation operation)
    {
        if (NativeRuntimeProfile is { IsVerified: true } && !fiber.Initialization && operation.Opcode == Jsm.Opcode.REQ && (uint)operation.Parameter < program!.NativeObjects.Count && program.NativeObjects[operation.Parameter] == fiber.ObjectId && !IsNativeSelfRequest(fiber, operation))
        {
            return "Native self REQ outside a retained physical actor requires its original dense BYTE598 identity comparison";
        }
        return null;
    }

    private bool IsNativeSelfRequest(Fiber fiber, JsmOperation operation) => NativeRuntimeProfile is { IsVerified: true } && !fiber.Initialization && operation.Opcode == Jsm.Opcode.REQ && (uint)operation.Parameter < program!.NativeActorCount && program.NativeObjects[operation.Parameter] == fiber.ObjectId && Scene.NativeActorObjectIds.Contains(fiber.ObjectId);

    private bool NativeRequestEndsTurn(Fiber fiber, JsmOperation operation)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Initialization)
        {
            return false;
        }
        if (operation.Opcode is Jsm.Opcode.REQSW or Jsm.Opcode.REQEW or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW)
        {
            // Native51CED0/51D060 and51D1F0/51D360/51D530 return3 on completion:
            // advance the PC, but yield the remaining entity instruction budget.
            return true;
        }
        if (operation.Opcode != Jsm.Opcode.REQ)
        {
            return false;
        }
        //51CD60 advances the self caller PC itself and returns4, even for
        //an occupied slot. Cross-entity/null requests return3 and yield.
        return !IsNativeSelfRequest(fiber, operation);
    }

    private IEnumerable<ActorState> NativeCallbackActors()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || !defaultsStarted)
        {
            return actors;
        }
        // Native529FF0 runs actors, doors, areas, then background modules.
        // The original pointer table instead places modules before areas.
        IEnumerable<int> actorIds = Scene.NativeActorObjectIds;
        IEnumerable<int> doorIds = program!.NativeObjects.Where(id => program.Objects[id].IsDoor);
        IEnumerable<int> areaIds = program.NativeObjects.Where(id => program.Objects[id].IsArea);
        IEnumerable<int> moduleIds = program.NativeObjects.Where(id => program.Objects[id].BackgroundModuleIndex is not null);
        return actorIds.Concat(doorIds).Concat(areaIds).Concat(moduleIds).Select(id => actors[id]);
    }
}
