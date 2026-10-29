using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRushOverlayProgramTests
{
    [Fact]
    public void OriginalFiveDwordProgramDrawsFourTimesThenCompletesWithoutReplacingScratch()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleFullscreenOverlayProgram program = game.ReadNativeRushOverlayProgram();
        Assert.Equal(0x1547144u, program.Address);
        Assert.Equal(5, program.Commands.Length);
        Assert.All(program.Commands.Take(4), command => Assert.Equal(0u, command >> 24));
        Assert.Equal(0xFFu, program.Commands[4] >> 24);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x259DB80, 0x259D900, 0x40, 1)]);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(0, NativeBattleFullscreenOverlay.Callback, 0x40);
        hierarchy = append.Hierarchy.Write(append.Address, append.Hierarchy.Read(append.Address).WriteDword(0x34, program.Address));
        NativeBattleFullscreenOverlayFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { FullscreenOverlay = NativeBattleFullscreenOverlayState.Unknown() }, hierarchy.Pools, NativeBattleLogicalOrdering.Clear());
        for (int callback = 0; callback < 4; callback++)
        {
            frame = frame with { Ordering = NativeBattleLogicalOrdering.Clear() };
            NativeBattleFullscreenOverlayFrame restored = NativeBattleSnapshotJson.Deserialize<NativeBattleFullscreenOverlayFrame>(NativeBattleSnapshotJson.Serialize(frame))!;
            NativeBattleFullscreenOverlayStep step = NativeBattleFullscreenOverlay.Advance8DDEC0(frame, append.Address, 0, program);
            NativeBattleFullscreenOverlayStep coldStep = NativeBattleFullscreenOverlay.Advance8DDEC0(restored, append.Address, 0, program);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(coldStep));
            Assert.Equal(0, step.Result);
            Assert.Equal(5, step.Frame.Ordering.ProxyCursor);
            Assert.Equal(program.Commands[callback], step.Frame.Pools[0].Slots[0].ReadDword(0x30));
            frame = step.Frame;
        }
        string retained = NativeBattleSnapshotJson.Serialize(frame.Memory.FullscreenOverlay);
        NativeBattleFullscreenOverlayStep terminal = NativeBattleFullscreenOverlay.Advance8DDEC0(frame, append.Address, 0, program);
        Assert.Equal(2, terminal.Result);
        Assert.Equal(0, terminal.Frame.Pools[0].Head);
        NativeBattleEffectHierarchyStep released = new NativeBattleEffectHierarchy(terminal.Frame.Pools).Advance(0, (current, _) => new(current, terminal.Result));
        Assert.Equal(0, released.Survivors);
        Assert.Null(released.Hierarchy.Pools[0].Head);
        Assert.Equal(retained, NativeBattleSnapshotJson.Serialize(terminal.Frame.Memory.FullscreenOverlay));
        Assert.Equal((ushort)5, terminal.Frame.Pools[0].Slots[0].ReadWord(0x24));
    }
}
