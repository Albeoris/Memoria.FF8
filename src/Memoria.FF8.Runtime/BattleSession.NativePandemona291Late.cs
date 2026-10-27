using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Late(NativeBattlePandemona291Frame frame, uint address, int age, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona late scenes require their original resources.");
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona late scenes require their current module state.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona late scenes require their actual arena.");
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        if (age == 154)
        {
            arena = NativeBattlePandemona291Vortex.Reset6F1520(arena, address);
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        if (age < 184)
        {
            return (frame, graphics);
        }
        if (age >= 224)
        {
            return DrawNativePandemona291Folding(frame, address, age, graphics);
        }
        int elapsed = age - 184;
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona late scenes require their actual model buffer.");
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 724);
        int sourceOffset = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(buffer + 0x23220), file.Data.Length);
        NativeBattleGfAnimationSource source = NativeBattleGfAnimationSource.Borrow(unchecked(buffer + 0x23220), arena, sourceOffset, file.Data.Length);
        if (elapsed == 0)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(arena, start + 0x1F0, 0x22C);
            workspace = workspace.Initialize7016B0(source, 0x22C);
            arena = workspace.CopyTo(arena, start + 0x1F0);
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        var anchor = NativeBattlePandemona291Late.Anchor6F0CC0(frame, address, resources.BodyAnchorInput);
        frame = anchor.Frame;
        NativeBattleVector origin = anchor.Position;
        NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native Pandemona late scene requires its current view matrix.");
        NativeBattleActionMatrix translation = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], origin.X, origin.Y, origin.Z, 4096);
        NativeBattleActionMatrix parent = NativeBattleGfMatrices.Compose56C2F0(view, translation);
        NativeBattlePandemona291SurfaceInputs inputs = new(source, parent, resources.LateTextureKinds.AsSpan().Slice(12, 3).ToArray().ToImmutableArray(), unchecked((byte)(elapsed * elapsed)), -128, 0x22C);
        var drawn = DrawNativePandemona291Surface(frame, address, age, graphics, inputs);
        frame = drawn.Frame;
        graphics = drawn.Graphics;
        if ((frame.Memory.Pandemona291!.Pause2556258 ?? throw new NotSupportedException("Native Pandemona late scene requires its current pause DWORD.")) == 0)
        {
            NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Late.Spawn184(frame.Memory.Arena!, frame.Random, address, origin, elapsed, resources.TrailTrigonometry);
            arena = NativeBattlePandemona291Late.Advance6F1BA0(spawned.Arena, address);
            frame = frame with { Memory = frame.Memory with { Arena = arena }, Random = spawned.Random };
        }
        return (frame, graphics);
    }
}
