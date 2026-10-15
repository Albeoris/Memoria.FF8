using Albeoris.Games.FF8.Jsm;
using System.Numerics;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedBackgroundShade(Fiber fiber, Jsm.Opcode opcode)
    {
        if (program!.Objects[fiber.ObjectId].BackgroundModuleIndex is not int module || module != fiber.BackgroundId)
        {
            return "Native background shade requires its calling background module";
        }
        if (fiber.NativeShadeOperation is int issued)
        {
            return opcode == Jsm.Opcode.BGSHADE && issued == fiber.Position && (Scene.BackgroundShades.ContainsKey(module) || Scene.BackgroundShadeLoops.ContainsKey(module)) ? null : "Issued BGSHADE lost its original instruction or module shade owner";
        }
        int count = opcode == Jsm.Opcode.RBGSHADELOOP ? 10 : opcode == Jsm.Opcode.BGSHADE ? 7 : 0;
        if (fiber.Stack.Count < count || fiber.Stack.Capture().Take(count).Any(value => !value.IsExact))
        {
            return "Native background shade requires its original exact operands before WORD/BYTE stores";
        }
        if (opcode == Jsm.Opcode.BGSHADESTOP && !Scene.BackgroundShades.ContainsKey(module) && !Scene.BackgroundShadeLoops.ContainsKey(module))
        {
            return "BGSHADESTOP requires its executed module RGB endpoint owner";
        }
        return null;
    }

    private int[] ExecuteBackgroundShade(Fiber fiber)
    {
        int[] operands = [];
        if (fiber.NativeShadeOperation is null)
        {
            operands = Pop(fiber, 7);
            Scene.BackgroundShadeLoops.Remove(fiber.BackgroundId);
            Scene.BackgroundShades[fiber.BackgroundId] = new BackgroundShade(unchecked((ushort)operands[0]), new Vector3(unchecked((byte)operands[1]), unchecked((byte)operands[2]), unchecked((byte)operands[3])), new Vector3(unchecked((byte)operands[4]), unchecked((byte)operands[5]), unchecked((byte)operands[6])), Frame, fiber.BackgroundId);
            fiber.NativeShadeOperation = fiber.Position - 1;
        }
        else if (Scene.BackgroundShadeLoops.TryGetValue(fiber.BackgroundId, out BackgroundShadeLoop? loop) ? loop.Remaining == 0 : Scene.BackgroundShades[fiber.BackgroundId].FadeFrames == 0 || Frame - Scene.BackgroundShades[fiber.BackgroundId].StartFrame > Scene.BackgroundShades[fiber.BackgroundId].FadeFrames)
        {
            fiber.NativeShadeOperation = null;
            return operands;
        }
        //5208B0 returns1 on issue, even duration0; only a later poll returns2.
        fiber.Position--;
        fiber.WaitReason = WaitReason.NativeBackgroundShade;
        return operands;
    }

    private void StopBackgroundShade(int module)
    {
        if (Scene.BackgroundShadeLoops.Remove(module, out BackgroundShadeLoop? loop))
        {
            Vector3 from = loop.FirstToSecond ? loop.FirstColor : loop.SecondColor;
            Vector3 target = loop.FirstToSecond ? loop.SecondColor : loop.FirstColor;
            Scene.BackgroundShades[module] = new BackgroundShade(0, from, target, Frame, module);
        }
        else
        {
            Scene.BackgroundShades[module] = Scene.BackgroundShades[module] with { FadeFrames = 0 };
        }
        //520AD0 clears412/flags600;475480 publishes retained target426..428.
    }

    private void StepBackgroundShadeControl(int module)
    {
        if (!Scene.BackgroundShadeLoops.TryGetValue(module, out BackgroundShadeLoop? shade) || shade.Remaining != 0)
        {
            return;
        }
        //52AEF0 runs after this module's VM, before the renderer consumes414.
        if (shade.HoldRemaining == 0)
        {
            bool forward = !shade.FirstToSecond;
            int duration = forward ? shade.FadeFirst : shade.FadeSecond;
            shade = shade with { FirstToSecond = forward, Elapsed = 0, Remaining = duration == 0 ? 0 : duration + 1, HoldRemaining = forward ? shade.HoldFirst : shade.HoldSecond };
        }
        Scene.BackgroundShadeLoops[module] = shade with { HoldRemaining = unchecked((byte)(shade.HoldRemaining - 1)) };
    }

    private static Vector3 SampleBackgroundShade(Vector3 from, Vector3 target, int duration, int elapsed)
    {
        if (duration == 0 || duration <= elapsed)
        {
            return target;
        }
        byte Channel(float first, float last) => unchecked((byte)((int)first + unchecked((short)elapsed) * ((int)last - (int)first) / unchecked((short)duration)));
        return new Vector3(Channel(from.X, target.X), Channel(from.Y, target.Y), Channel(from.Z, target.Z));
    }

    private void ValidateBackgroundShades(Checkpoint saved)
    {
        bool Color(Vector3 value) => new[] { value.X, value.Y, value.Z }.All(channel => float.IsFinite(channel) && channel == MathF.Truncate(channel) && channel is >= 0 and <= 255);
        bool Module(int module) => program!.Objects.Any(obj => obj.BackgroundModuleIndex == module);
        foreach ((int module, BackgroundShade shade) in saved.Scene.BackgroundShades)
        {
            if (!Module(module) || shade.ModuleIndex != module || shade.FadeFrames is < 0 or > ushort.MaxValue || shade.StartFrame < 0 || shade.StartFrame > saved.Frame + 1 || !Color(shade.FirstColor) || !Color(shade.SecondColor) || saved.Scene.BackgroundShadeLoops.ContainsKey(module))
            {
                throw new InvalidDataException("Saved background shade lost its module, WORD duration, renderer clock or BYTE endpoints.");
            }
        }
        foreach ((int module, BackgroundShadeLoop shade) in saved.Scene.BackgroundShadeLoops)
        {
            int duration = shade.FirstToSecond ? shade.FadeFirst : shade.FadeSecond;
            if (!Module(module) || shade.ModuleIndex != module || (uint)shade.ObjectId >= program!.Objects.Count || program.Objects[shade.ObjectId].BackgroundModuleIndex != module || shade.FadeFirst is < 0 or > ushort.MaxValue || shade.FadeSecond is < 0 or > ushort.MaxValue || shade.HoldFirst is < 0 or > byte.MaxValue || shade.HoldSecond is < 0 or > byte.MaxValue || shade.HoldRemaining is < 0 or > byte.MaxValue || shade.Remaining < 0 || (shade.Remaining == 0 ? shade.Elapsed != 0 : duration == 0 || shade.Elapsed < 0 || shade.Elapsed > duration || shade.Remaining != duration + 1 - shade.Elapsed) || !Color(shade.FirstColor) || !Color(shade.SecondColor) || !Color(shade.CurrentColor))
            {
                throw new InvalidDataException("Saved background shade loop lost its native WORD phase, BYTE holds or module endpoints.");
            }
        }
    }
}
