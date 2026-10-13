using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeDrawPointSparkAnchor(short X, short Y, short Z, byte Status);
/// <summary>Original setup reset, not a simulated particle lifetime or current depletion state.</summary>
public sealed record NativeDrawPointReset(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, FieldFixedPosition RawPosition, ushort EffectiveDisplayMask, IReadOnlyList<NativeDrawPointSparkAnchor> Sparks);

public sealed partial class EventEngine
{
    private string? UnsupportedDrawPointSetup(Fiber fiber, JsmOperation operation)
    {
        bool display = operation.Opcode == Jsm.Opcode.SETDRAWPOINT;
        if (FieldId < 72 || fiber.Stack.Count == 0)
        {
            return "Native draw-point setup requires its original operand in an ordinary field";
        }
        string? aliases = UnsupportedDrawPointAliases(display);
        if (aliases is not null)
        {
            return aliases;
        }
        if (!display)
        {
            return null;
        }
        ActorState actor = actors[fiber.ObjectId];
        FieldFixedPosition? raw = ReadNativeScriptPosition(actor);
        if (unchecked((byte)fiber.Stack.Peek()) != 1 || actor.ModelId is < 0 or >= 65535 || actor.NativeMotionAuthority?.Version != 1 || program is null || !program.NativeObjects.Take(program.NativeActorCount).Contains(fiber.ObjectId) || raw is null || new[] { raw.X, raw.Y, raw.Z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
        {
            return "Native SETDRAWPOINT needs low-BYTE1 and preserved modeled-actor raw placement; other display masks or unknown setup remain unsupported";
        }
        return null;
    }

    private string? UnsupportedDrawPointAliases(bool includeDisplaySource)
    {
        return variables.Keys.Any(address => address is >= 237 and <= 242 || includeDisplaySource && address is >= 85 and <= 88) ? "Native draw-point bytes overlap a legacy scalar with unknown width; original byte-addressed aliases are required" : null;
    }

    private string? UnsupportedDrawPointAccess(JsmOperation operation)
    {
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        long last = (long)operation.Parameter + width - 1;
        bool displaySource = operation.Parameter <= 88 && last >= 88;
        if (width == 0 || !displaySource && !(operation.Parameter <= 242 && last >= 240))
        {
            return null;
        }
        if (operation.Opcode == Jsm.Opcode.PSHM_B && !displaySource && TryReadDrawPointByte(operation.Parameter, out _))
        {
            return UnsupportedDrawPointAliases(false);
        }
        return "Native draw-point access needs a known exact byte; B88, overlapping WORD/DWORD and unbound byte writes preserve original PC and operands";
    }

    private bool TryReadDrawPointByte(int address, out int value)
    {
        value = 0;
        if (address == 242 && Scene.DrawPointIndex is byte index)
        {
            value = index;
            return true;
        }
        if (Scene.DrawPointFieldStateVersion == 1 && address is 240 or 241)
        {
            value = address == 240 ? Scene.DrawPointEnabled : Scene.DrawPointDisplayParameter;
            return true;
        }
        return false;
    }

    private int[] SetNativeDrawPointDisplay(Fiber fiber)
    {
        FieldFixedPosition raw = ReadNativeScriptPosition(actors[fiber.ObjectId])!;
        int[] args = Pop(fiber, 1);
        NativeDrawPointSparkAnchor anchor = new((short)(raw.X >> 12), (short)(raw.Y >> 12), (short)(raw.Z >> 12), 1);
        Scene.DrawPointFieldStateVersion = 1;
        Scene.DrawPointEnabled = 1;
        Scene.DrawPointDisplayParameter = (byte)args[0];
        // Native B88 is Boolean, so selected mask1 makes its full OR result1.
        // Particle presentation is explicitly omitted; no B88 value is invented.
        Scene.DrawPointReset = new NativeDrawPointReset(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, raw, 1, Enumerable.Repeat(anchor, 8).ToArray());
        return args;
    }

    private int[] SetNativeDrawPointIndex(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        Scene.DrawPointIndex = unchecked((byte)((byte)args[0] - 1));
        return args;
    }

    private void ValidateDrawPointCheckpoint(Checkpoint saved)
    {
        SceneEventState scene = saved.Scene;
        if (scene.DrawPointFieldStateVersion is < 0 or > 1 || scene.DrawPointFieldStateVersion == 0 && (scene.DrawPointEnabled != 0 || scene.DrawPointDisplayParameter != 0 || scene.DrawPointReset is not null) || scene.DrawPointReset is null && (scene.DrawPointEnabled != 0 || scene.DrawPointDisplayParameter != 0))
        {
            throw new InvalidDataException("Saved draw-point field bytes lack their native reset or setup authority.");
        }
        if (scene.DrawPointReset is not NativeDrawPointReset reset)
        {
            return;
        }
        EventObject? owner = program!.Objects.FirstOrDefault(actor => actor.Index == reset.ObjectId);
        EventScript? script = owner?.Scripts.FirstOrDefault(script => script.Id == reset.ScriptId);
        if (reset.Version != 1 || reset.FieldId != saved.FieldId || reset.FieldId < 72 || reset.Frame < 0 || reset.Frame > saved.Frame || scene.DrawPointFieldStateVersion != 1 || scene.DrawPointEnabled != 1 || scene.DrawPointDisplayParameter != 1 || reset.EffectiveDisplayMask != 1 || script is null || reset.Operation < script.Start || reset.Operation >= script.End || program.Operations[reset.Operation].Opcode != Jsm.Opcode.SETDRAWPOINT || reset.RawPosition is null || reset.Sparks is null || reset.Sparks.Count != 8 || new[] { reset.RawPosition.X, reset.RawPosition.Y, reset.RawPosition.Z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || reset.Sparks.Any(anchor => anchor is null || anchor.Status != 1 || anchor.X != (reset.RawPosition.X >> 12) || anchor.Y != (reset.RawPosition.Y >> 12) || anchor.Z != (reset.RawPosition.Z >> 12)))
        {
            throw new InvalidDataException("Saved draw-point reset lost its original setup provenance, mask or eight signed-WORD anchors.");
        }
    }
}
