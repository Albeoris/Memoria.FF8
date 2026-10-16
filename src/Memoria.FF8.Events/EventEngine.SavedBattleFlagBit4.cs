using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSavedBattleFlagBit4(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || services is not GameSession session || session.State.CharacterVitals is null || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Native529EB0/raw358 requires one exact full DWORD and the current saved flag/dream alias owner";
        }
        session.State.SavedBattleFlags?.Validate();
        return null;
    }

    private int[] ExecuteNativeSavedBattleFlagBit4(Fiber fiber)
    {
        int raw = fiber.Stack.Peek();
        NativeSavedBattleFlagFieldWrite write = new(FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, raw);
        ((GameSession)services).SetNativeSavedBattleFlagBit4(write);
        return Pop(fiber, 1);
    }
}
