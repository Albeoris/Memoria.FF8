using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeDrawPointSparkAnchor(short X, short Y, short Z, byte Status);
/// <summary>Original setup reset, not a simulated particle lifetime or current depletion state.</summary>
public sealed record NativeDrawPointReset(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, FieldFixedPosition RawPosition, ushort EffectiveDisplayMask, IReadOnlyList<NativeDrawPointSparkAnchor> Sparks, [property: JsonRequired] byte? SourceByte88);

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
        if (ReadDrawPointSourceByte() is null && (unchecked((byte)fiber.Stack.Peek()) & 1) == 0)
        {
            return "Native SETDRAWPOINT needs its actual BYTE88 when the consumed display byte does not determine the Boolean OR result";
        }
        if (actor.ModelId is < 0 or >= 65535 || actor.NativeMotionAuthority?.Version != 1 || program is null || !program.NativeObjects.Take(program.NativeActorCount).Contains(fiber.ObjectId) || raw is null || new[] { raw.X, raw.Y, raw.Z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
        {
            return "Native SETDRAWPOINT needs preserved modeled-actor raw placement before resetting its eight anchors";
        }
        return null;
    }

    private string? UnsupportedDrawPointAliases(bool includeDisplaySource)
    {
        if (variables.Any(pair => (pair.Key is >= 237 and <= 242 || includeDisplaySource && pair.Key is >= 85 and <= 88) && !IsKnownNativeByteAlias(pair.Key, pair.Value)))
        {
            return "Native draw-point bytes overlap a legacy scalar with unknown width; original byte-addressed aliases are required";
        }
        if (services.NativeVariableValues?.Any(pair => pair.Key <= 242 && pair.Key + pair.Value.Width > 240 || includeDisplaySource && pair.Key <= 88 && pair.Key + pair.Value.Width > 88 && (pair.Key != 88 || pair.Value.Width != 1 || !pair.Value.Value.IsExact)) == true)
        {
            return "Native draw-point setup requires its exact BYTE88 and cannot replace overlapping typed memory";
        }
        return null;
    }

    private byte? ReadDrawPointSourceByte()
    {
        return services.NativeVariableValues?.TryGetValue(88, out NativeMemoryValue? source) == true && source.Width == 1 && source.Value.IsExact ? checked((byte)source.Value.RequireExact()) : null;
    }

    private string? UnsupportedDrawPointAccess(JsmOperation operation)
    {
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
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
        if (displaySource && operation.Parameter == 88 && operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B && services.NativeVariableValues?.TryGetValue(88, out NativeMemoryValue? source) == true && source.Width == 1 && source.Value.IsExact)
        {
            return null;
        }
        if (operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B && !displaySource && TryReadDrawPointByte(operation.Parameter, out _))
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
        byte? source = ReadDrawPointSourceByte();
        byte parameter = unchecked((byte)fiber.Stack.Peek());
        ushort effective = source is byte known ? (ushort)(known | parameter) : parameter;
        int[] args = Pop(fiber, 1);
        NativeDrawPointSparkAnchor anchor = new((short)(raw.X >> 12), (short)(raw.Y >> 12), (short)(raw.Z >> 12), 1);
        Scene.DrawPointFieldStateVersion = 1;
        Scene.DrawPointEnabled = 1;
        Scene.DrawPointDisplayParameter = parameter;
        // Unknown loader BooleanB88 can only be consumed when bit0 is already set.
        // A known source is sampled in this reset receipt, not copied as another bank owner.
        Scene.DrawPointReset = new NativeDrawPointReset(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, raw, effective, Enumerable.Repeat(anchor, 8).ToArray(), source);
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
        bool validMask = reset.SourceByte88 is byte source ? reset.EffectiveDisplayMask == (source | scene.DrawPointDisplayParameter) : (scene.DrawPointDisplayParameter & 1) != 0 && reset.EffectiveDisplayMask == scene.DrawPointDisplayParameter;
        if (reset.Version != 1 || reset.FieldId != saved.FieldId || reset.FieldId < 72 || reset.Frame < 0 || reset.Frame > saved.Frame || scene.DrawPointFieldStateVersion != 1 || scene.DrawPointEnabled != 1 || !validMask || script is null || reset.Operation < script.Start || reset.Operation >= script.End || program.Operations[reset.Operation].Opcode != Jsm.Opcode.SETDRAWPOINT || reset.RawPosition is null || reset.Sparks is null || reset.Sparks.Count != 8 || new[] { reset.RawPosition.X, reset.RawPosition.Y, reset.RawPosition.Z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || reset.Sparks.Any(anchor => anchor is null || anchor.Status != 1 || anchor.X != (reset.RawPosition.X >> 12) || anchor.Y != (reset.RawPosition.Y >> 12) || anchor.Z != (reset.RawPosition.Z >> 12)))
        {
            throw new InvalidDataException("Saved draw-point reset lost its original setup provenance, mask or eight signed-WORD anchors.");
        }
    }
}
