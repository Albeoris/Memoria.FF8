using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePlaceIdContract()
    {
        static byte[] PlaceProgram(int value) => SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, value), (Jsm.Opcode.SETPLACE, 0), (Jsm.Opcode.RET, 0));
        byte[] unchanged = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState { WorldEntryMemory = NativeWorldEntryMemory.CreateCleared() });
        EventEngine engine = new(session);
        engine.LoadField(99, unchanged, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, inheritedPlaceId: 1);
        engine.Step();
        Require(engine.Scene.PlaceId == 1, "A world return did not seed its shared saved place WORD before destination scripts.");
        foreach (int operand in new[] { 132, -1, 0x123456, -0x123456 })
        {
            engine.LoadField(100, PlaceProgram(operand), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            int expected = unchecked((ushort)operand);
            Require(engine.Scene.PlaceId == expected, "SETPLACE did not retain the native low WORD operand.");
            engine.LoadField(101, unchanged, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            Require(engine.Scene.PlaceId == expected, "A field allocation reset the shared saved place WORD.");
            engine.Step();
            Require(engine.Scene.PlaceId == expected, "A field without SETPLACE replaced the inherited place WORD.");
            JsonElement saved = engine.Capture();
            EventEngine restored = new(new GameSession(new GameState { WorldEntryMemory = NativeWorldEntryMemory.CreateCleared() }));
            restored.LoadField(101, unchanged, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            restored.Restore(saved);
            Require(restored.Scene.PlaceId == expected && JsonElement.DeepEquals(saved, restored.Capture()), "The persisted place WORD did not survive independent capture/restore.");
            restored.LoadField(102, PlaceProgram(73), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            Require(restored.Scene.PlaceId == expected, "New field allocation changed the place before its script executed.");
            restored.Step();
            Require(restored.Scene.PlaceId == 73, "A destination SETPLACE did not replace the inherited place WORD.");
        }
    }
}