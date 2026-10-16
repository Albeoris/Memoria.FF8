using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSetWitch(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || services is not GameSession session || session.State.SavedBattleFlags is null && session.State.CharacterVitals is null || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Native SETWITCH requires one exact full DWORD and the canonical saved flag/dream alias owner";
        }
        session.State.SavedBattleFlags?.Validate();
        return null;
    }

    private int[] ExecuteNativeSetWitch(Fiber fiber)
    {
        int raw = fiber.Stack.Peek();
        NativeSavedBattleFlagFieldWrite write = new(FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, raw);
        ((GameSession)services).SetNativeWitch(write);
        return Pop(fiber, 1);
    }
}
