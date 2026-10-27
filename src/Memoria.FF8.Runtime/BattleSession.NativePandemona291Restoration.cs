using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Restoration(NativeBattlePandemona291Frame frame, uint address, int age, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources!;
        NativeBattlePandemona291State state = frame.Memory.Pandemona291!;
        int ground = state.Ground25562E0 ?? throw new NotSupportedException("Native Pandemona restoration requires its current ground.");
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona restoration requires its current center.");
        bool paused = (state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona restoration requires its actual pause DWORD.")) != 0;
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        NativeBattleVector? anchor = null;
        if (age < 377)
        {
            int elapsed = age - 335;
            uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona restoration requires its actual model buffer.");
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 726);
            uint sourceAddress = unchecked(buffer + 0x2C074);
            int sourceLength = file.Data.Length - 0x8E54;
            NativeBattleGfAnimationSource source = new(sourceAddress, arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(sourceAddress, sourceLength), sourceLength).ToImmutableArray());
            if (elapsed == 0)
            {
                arena = NativeBattlePandemona291Vortex.Reset6F1520(arena, address);
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(arena, start + 0x1F0, 0x1C0);
                arena = workspace.Initialize7016B0(source, 0x1C0).CopyTo(arena, start + 0x1F0);
                frame = frame with { Memory = frame.Memory with { Arena = arena } };
            }
            var anchored = NativeBattlePandemona291Late.Anchor6F0CC0(frame, address, resources.BodyAnchorInput);
            frame = anchored.Frame;
            anchor = anchored.Position;
            if (!paused)
            {
                NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Restoration.Spawn335(frame.Memory.Arena!, frame.Random, address, center);
                frame = frame with { Memory = frame.Memory with { Arena = NativeBattlePandemona291Trails.Advance6F19C0(spawned.Arena, address) }, Random = spawned.Random };
            }
            var targets = AdvanceNativePandemona291Targets(frame, address, age, anchor, graphics);
            frame = targets.Frame;
            graphics = targets.Graphics;
            NativeBattleActionMatrix basis = NativeBattlePandemona291Restoration.Basis50CBA0(0, -anchor.Value.Y, center - ground, anchor.Value);
            NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native Pandemona restoration requires its current view.");
            NativeBattlePandemona291SurfaceInputs inputs = new(source, NativeBattleGfMatrices.Compose56C2F0(view, basis), null, 0, -128, 0x1C0);
            var surface = DrawNativePandemona291Surface(frame, address, age, graphics, inputs);
            frame = surface.Frame;
            graphics = surface.Graphics;
            var layers = DrawNativePandemona291LateLayers(frame, basis, NativeBattlePandemona291Restoration.Layers335(elapsed), graphics);
            return (layers.Frame, layers.Graphics);
        }
        if (age < 417)
        {
            int elapsed = age - 377;
            if (elapsed == 0)
            {
                arena = NativeBattlePandemona291Vortex.Reset6F1520(frame.Memory.Arena!, address);
                frame = frame with { Memory = frame.Memory with { Arena = arena } };
            }
            if (!paused)
            {
                NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Vortex.Spawn6EDC66(frame.Memory.Arena!, frame.Random, address, resources.TrailTrigonometry);
                NativeBattleActionMatrix trailBasis = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, elapsed << 6, 22528, 8192) with { X = 0, Y = 0, Z = ground };
                arena = NativeBattlePandemona291Vortex.Advance6F1A30(spawned.Arena, address, trailBasis, 40, 2048, resources.TrailTrigonometry, (input, mac) =>
                {
                    NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters!;
                    frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers with { NormalInput = input, NormalInputKnown = null, Mac = [mac.X, mac.Y, mac.Z], Ir = [Math.Clamp(mac.X, short.MinValue, short.MaxValue), Math.Clamp(mac.Y, short.MinValue, short.MaxValue), Math.Clamp(mac.Z, short.MinValue, short.MaxValue)], Flags = mac.Flags } } };
                });
                frame = frame with { Memory = frame.Memory with { Arena = arena }, Random = spawned.Random };
            }
            var targets = AdvanceNativePandemona291Targets(frame, address, age, null, graphics);
            frame = targets.Frame;
            graphics = targets.Graphics;
            NativeBattleActionMatrix basis = NativeBattlePandemona291Restoration.Rotation701220(1024) with { X = 0, Y = 0, Z = ground };
            var layers = DrawNativePandemona291LateLayers(frame, basis, NativeBattlePandemona291Restoration.Layers377(elapsed), graphics);
            return DrawNativePandemona291Rings(layers.Frame, elapsed, layers.Graphics);
        }
        if (age < 440)
        {
            arena = frame.Memory.Arena!;
            if (age == 417 && !paused)
            {
                arena = NativeBattlePandemona291Restoration.Decelerate417(arena, address);
            }
            arena = NativeBattlePandemona291RisingChildren.Advance6F1B20(arena, address);
            return (frame with { Memory = frame.Memory with { Arena = arena } }, graphics);
        }
        var contacts = AdvanceNativePandemona291Contacts(frame, address, age, graphics);
        if (!paused && age < 480)
        {
            contacts = (contacts.Frame with { Memory = contacts.Frame.Memory with { Arena = NativeBattlePandemona291RisingChildren.Advance6F1B20(contacts.Frame.Memory.Arena!, address) } }, contacts.Graphics);
        }
        return contacts;
    }
}
