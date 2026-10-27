using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Folding(NativeBattlePandemona291Frame frame, uint address, int age, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona folding requires its current resources.");
        NativeBattlePandemona291State state = frame.Memory.Pandemona291!;
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona folding requires its actual current buffer.");
        int ground = state.Ground25562E0 ?? throw new NotSupportedException("Native Pandemona folding requires its current ground.");
        bool paused = (state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona folding requires its actual pause DWORD.")) != 0;
        NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native Pandemona folding requires its current view.");
        NativeBattleVector? anchor = null;
        if (age is >= 224 and < 254)
        {
            int elapsed = age - 224;
            NativeBattleGfAnimationSource source = resources.FoldingProgram ?? throw new NotSupportedException("Native Pandemona folding requires its original programme.");
            if (elapsed == 0)
            {
                arena = NativeBattlePandemona291Vortex.Reset6F1520(arena, address);
                arena = Initialize(arena, source, 0x1D8);
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            NativeBattleActionMatrix basis = NativeBattlePandemona291Restoration.Rotation701270(2048) with { X = 0, Y = 0, Z = unchecked(ground - 1024) };
            basis = NativeBattlePoseMatrices.Scale56BEF0(basis, 4096, 4096, 5120);
            NativeBattlePandemona291SurfaceInputs inputs = new(source, NativeBattleGfMatrices.Compose56C2F0(view, basis), resources.LateTextureKinds.AsSpan().Slice(4, 4).ToArray().ToImmutableArray(), unchecked((byte)(elapsed << 5)), -128, 0x1D8);
            var drawn = DrawNativePandemona291Surface(frame, address, age, graphics, inputs);
            frame = drawn.Frame;
            graphics = drawn.Graphics;
            if (!paused)
            {
                NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Late.Spawn224(frame.Memory.Arena!, frame.Random, address, ground);
                frame = frame with { Memory = frame.Memory with { Arena = NativeBattlePandemona291Late.Advance6F1BA0(spawned.Arena, address) }, Random = spawned.Random };
            }
        }
        if (age is >= 254 and < 320)
        {
            int begin = age < 284 ? 254 : 284;
            int elapsed = age - begin;
            int sourceRelative = begin == 254 ? 0 : 0x464C;
            int workspaceLength = begin == 254 ? 0x8C : 0x11C;
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 726);
            uint sourceAddress = unchecked(buffer + 0x23220u + (uint)sourceRelative);
            int sourceLength = file.Data.Length - sourceRelative;
            NativeBattleGfAnimationSource source = NativeBattleGfAnimationSource.Borrow(sourceAddress, arena, NativeBattleWaterExecutionFields.ArenaOffset(sourceAddress, sourceLength), sourceLength);
            if (elapsed == 0)
            {
                arena = begin == 254 ? NativeBattlePandemona291Vortex.Reset6F1520(arena, address) : NativeBattlePandemona291Late.Convert284(arena, address);
                arena = Initialize(arena, source, workspaceLength);
                frame = frame with { Memory = frame.Memory with { Arena = arena } };
            }
            var anchored = NativeBattlePandemona291Late.Anchor6F0CC0(frame, address, resources.BodyAnchorInput);
            frame = anchored.Frame;
            anchor = anchored.Position;
            NativeBattleActionMatrix basis = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], anchor.Value.X, anchor.Value.Y, anchor.Value.Z, 4096);
            NativeBattlePandemona291SurfaceInputs inputs = new(source, NativeBattleGfMatrices.Compose56C2F0(view, basis), resources.LateTextureKinds.AsSpan().Slice(0, 2).ToArray().ToImmutableArray(), unchecked((byte)(elapsed << 5)), unchecked((short)(begin == 254 ? 64 : 128)), workspaceLength);
            var drawn = DrawNativePandemona291Surface(frame, address, age, graphics, inputs);
            frame = drawn.Frame;
            graphics = drawn.Graphics;
            int fade = elapsed << 8;
            if (begin == 254 || fade < 4096)
            {
                NativeBattleStaticMeshResource backdrop = resources.LateBackdrop ?? throw new NotSupportedException("Native Pandemona requires its original backdrop.");
                basis = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, 2048, 4096, 4096) with { X = -500, Y = 0, Z = ground };
                NativeBattleEsunaMeshContext context = new(begin == 254 ? 0x2030u : 0x20F0u, begin == 254 ? null : 0u, begin == 254 ? null : fade, 0, 0, unchecked((ushort)((elapsed << 5) & 127)), new(0, 0, 256, 256), new(0, 128, 256, 128), 17, 2);
                var background = DrawNativePandemona291PreparedMesh(frame, backdrop, NativeBattleGfMatrices.Compose56C2F0(view, basis), context, null, graphics, null);
                frame = background.Frame;
                graphics = background.Graphics;
            }
            if (!paused)
            {
                if (begin == 254)
                {
                    NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Late.Spawn184(frame.Memory.Arena!, frame.Random, address, anchor.Value, elapsed, resources.TrailTrigonometry, 2, 0, 4096);
                    frame = frame with { Memory = frame.Memory with { Arena = spawned.Arena }, Random = spawned.Random };
                    frame = NativeBattlePandemona291LateParticles.Spawn254(frame, ground);
                    frame = frame with { Memory = frame.Memory with { Arena = NativeBattlePandemona291Late.Advance6F1BA0(frame.Memory.Arena!, address) } };
                }
                else
                {
                    frame = frame with { Memory = frame.Memory with { Arena = NativeBattlePandemona291RisingChildren.Advance6F1B20(frame.Memory.Arena!, address) } };
                }
            }
        }
        if (age >= 335)
        {
            return DrawNativePandemona291Restoration(frame, address, age, graphics);
        }
        var targets = AdvanceNativePandemona291Targets(frame, address, age, anchor, graphics);
        return (targets.Frame, targets.Graphics);

        NativeBattleEffectArena Initialize(NativeBattleEffectArena current, NativeBattleGfAnimationSource source, int length)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(current, start + 0x1F0, length);
            return workspace.Initialize7016B0(source, length).CopyTo(current, start + 0x1F0);
        }
    }
}
