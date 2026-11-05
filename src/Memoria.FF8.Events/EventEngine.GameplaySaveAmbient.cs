using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private GameplaySaveFieldAmbient CaptureGameplaySaveAmbient()
    {
        static GameplaySaveFieldColor Color(Vector3 color)
        {
            if (new[] { color.X, color.Y, color.Z }.Any(value => !float.IsFinite(value) || value != MathF.Truncate(value) || value is < 0 or > 255))
            {
                throw new InvalidDataException("Gameplay module color requires exact BYTE channels.");
            }
            return new((byte)color.X, (byte)color.Y, (byte)color.Z);
        }
        GameplaySaveFieldAmbient result = new(
            Scene.BackgroundShades.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldShade(checked((ushort)pair.Value.FadeFrames), Color(pair.Value.FirstColor), Color(pair.Value.SecondColor), pair.Value.StartFrame)),
            Scene.BackgroundShadeLoops.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldShadeLoop(pair.Value.ObjectId, checked((ushort)pair.Value.FadeFirst), checked((ushort)pair.Value.FadeSecond), Color(pair.Value.FirstColor), Color(pair.Value.SecondColor), checked((byte)pair.Value.HoldFirst), checked((byte)pair.Value.HoldSecond), pair.Value.FirstToSecond, pair.Value.Elapsed, pair.Value.Remaining, checked((byte)pair.Value.HoldRemaining), Color(pair.Value.CurrentColor))),
            Scene.BackgroundScrollPositions.ToDictionary(pair => pair.Key, pair => new GameplaySaveFieldScrollPhase(pair.Value.X, pair.Value.Y, pair.Value.TargetX, pair.Value.TargetY, pair.Value.PreviousX, pair.Value.PreviousY, pair.Value.Counter, pair.Value.Mode, pair.Value.Status, pair.Value.Duration)));
        ValidateGameplayAmbient(result, Frame);
        return result;
    }

    private static BackgroundScrollPosition GameplayScroll(GameplaySaveFieldScrollPhase phase) => new(phase.X, phase.Y, phase.TargetX, phase.TargetY, phase.PreviousX, phase.PreviousY, phase.Counter, phase.Mode, phase.Status, phase.Duration, NativeLinearVersion: phase.Mode == 4 ? 1 : 0);

    private void ValidateGameplayAmbient(GameplaySaveFieldAmbient saved, long frame)
    {
        saved.Validate(frame);
        bool Module(int module) => program!.Objects.Any(obj => obj.BackgroundModuleIndex == module);
        if (saved.Shades.Keys.Any(module => !Module(module)) || saved.ShadeLoops.Any(pair => !Module(pair.Key) || (uint)pair.Value.ObjectId >= program!.Objects.Count || program.Objects[pair.Value.ObjectId].BackgroundModuleIndex != pair.Key) || saved.Scrolls.Any(pair => !ValidBackgroundScrollPosition(pair.Key, GameplayScroll(pair.Value), Scene)))
        {
            throw new InvalidDataException("Gameplay ambient continuation does not match the current module or scroll renderer contract.");
        }
    }

    private void RestoreGameplayAmbient(GameplaySaveFieldAmbient? saved, long frame)
    {
        saved ??= new([], [], []);
        ValidateGameplayAmbient(saved, frame);
        static Vector3 Color(GameplaySaveFieldColor color) => new(color.Red, color.Green, color.Blue);
        Scene.BackgroundShades.Clear();
        foreach ((int module, GameplaySaveFieldShade shade) in saved.Shades)
        {
            Scene.BackgroundShades.Add(module, new(shade.Duration, Color(shade.First), Color(shade.Second), shade.StartFrame, module));
        }
        Scene.BackgroundShadeLoops.Clear();
        foreach ((int module, GameplaySaveFieldShadeLoop loop) in saved.ShadeLoops)
        {
            Scene.BackgroundShadeLoops.Add(module, new(loop.ObjectId, module, loop.FadeFirst, loop.FadeSecond, Color(loop.First), Color(loop.Second), loop.HoldFirst, loop.HoldSecond, loop.FirstToSecond, loop.Elapsed, loop.Remaining, loop.HoldRemaining, Color(loop.Current)));
        }
        Scene.BackgroundScrollPositions.Clear();
        foreach ((int layer, GameplaySaveFieldScrollPhase phase) in saved.Scrolls)
        {
            Scene.BackgroundScrollPositions.Add(layer, GameplayScroll(phase));
        }
        if (saved.Scrolls.Values.Any(value => value.Mode == 4) && CanRestoreLinearBackgroundScroll?.Invoke(Scene) == false)
        {
            throw new InvalidDataException("Gameplay layer scroll is unsupported by the selected current renderer.");
        }
    }

    private void ValidateGameplayAmbientWait(Fiber fiber)
    {
        if (fiber.NativeShadeOperation is int operation)
        {
            if (fiber.Complete || operation != fiber.Position || (uint)operation >= program!.Operations.Count || program.Operations[operation].Opcode != Jsm.Opcode.BGSHADE || fiber.WaitReason != WaitReason.NativeBackgroundShade || program.Objects[fiber.ObjectId].BackgroundModuleIndex != fiber.BackgroundId || !Scene.BackgroundShades.ContainsKey(fiber.BackgroundId) && !Scene.BackgroundShadeLoops.ContainsKey(fiber.BackgroundId))
            {
                throw new InvalidDataException("Gameplay BGSHADE wait lost its consumed operands, issued instruction or module owner.");
            }
        }
        else if (fiber.WaitReason == WaitReason.NativeBackgroundShade)
        {
            throw new InvalidDataException("Gameplay BGSHADE wait has no issued instruction.");
        }
        if (fiber.WaitReason == WaitReason.BackgroundScroll)
        {
            int? layer = (uint)fiber.Position >= program!.Operations.Count ? null : program.Operations[fiber.Position].Opcode switch
            {
                Jsm.Opcode.SCROLLSYNC => 0,
                Jsm.Opcode.SCROLLSYNC2 when fiber.Stack.Count > 0 && fiber.Stack.Capture()[0].IsExact => fiber.Stack.Peek(),
                _ => null
            };
            if (fiber.Complete || layer is not int selected || !Scene.BackgroundScrollPositions.ContainsKey(selected))
            {
                throw new InvalidDataException("Gameplay scroll wait lost its instruction, unconsumed layer or phase owner.");
            }
        }
    }
}
