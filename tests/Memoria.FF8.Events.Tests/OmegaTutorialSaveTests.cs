using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void OmegaTutorialSaveContract()
    {
        byte[] block = new byte[NativePcSaveData.BlockSize];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(NativePcSaveData.SaveMapOffset + 2), 0x08FF);
        block[NativePcSaveData.MainOffset + 3282] = 255;
        block[NativePcSaveData.MainOffset + 3283] = 1;
        block[NativePcSaveData.MainOffset + 3298] = 128;
        block[NativePcSaveData.MainOffset + 3299] = 255;
        GameSession fresh = GameSession.CreateColdProcess();
        fresh.StartNewGame();
        GameSession session = new(fresh.State with { OriginalSave = new NativePcSaveData(block), EventState = null });
        EventEngine engine = new(session);
        engine.Scene.UnlockedTutorials.Add(42);
        engine.InitializeOriginalSave(NativeFieldRuntimeProfile.FF8En);
        Require(engine.Scene.UnlockedTutorials.SetEquals([0, 127]), "Original tutorial import changed bitmap bounds or retained stale availability.");
        SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(engine.Scene))!;
        Require(restored.UnlockedTutorials.SetEquals([0, 127]), "Tutorial availability did not survive its current scene JSON owner.");
    }
}
