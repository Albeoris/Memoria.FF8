using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Actor DWORD352 eye mask0x380000 and the synchronous532340 texture copy. Null means historical state is unknown.</summary>
public sealed record NativeActorEyes([property: JsonRequired] int Version, [property: JsonRequired] uint Mode, [property: JsonRequired] int? BoundModelId, [property: JsonRequired] byte? BackendHighNibble114, [property: JsonRequired] NativeEyeCopy? Patch);

public sealed partial class EventEngine
{
    public Func<int, NativeFieldEyeDefinition?>? ReadNativeEyes { get; set; }

    private NativeActorEyes PrepareNativeEyes(ActorState actor, bool closed)
    {
        int? bound = actor.NativeAnimationHeader?.BoundModelId;
        if (NativeRuntimeProfile is not { IsVerified: true } || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || bound is not int model || model < 0)
        {
            throw new NotSupportedException("Native eye command requires an allocated physical actor and its registered model.");
        }
        NativeFieldEyeDefinition definition = ReadNativeEyes?.Invoke(model) ?? throw new NotSupportedException("Native eye command requires the actual registered model eye bank.");
        NativeEyeCopy? copy = definition.Copy(closed);
        if (copy is not null)
        {
            foreach (NativeEyeCopy prior in actors.Select(other => other.NativeEyes?.Patch).OfType<NativeEyeCopy>())
            {
                if (prior.TextureHash == copy.TextureHash && prior.DestinationXWords < copy.SourceXWords + copy.WidthWords && prior.DestinationXWords + prior.WidthWords > copy.SourceXWords && prior.DestinationY < copy.SourceY + copy.Height && prior.DestinationY + prior.Height > copy.SourceY)
                {
                    throw new NotSupportedException("Native eye source was modified by an earlier retained texture copy.");
                }
            }
        }
        return new NativeActorEyes(1, closed ? 0x200000u : 0x100000u, model, copy is null ? null : closed ? (byte)0x20 : (byte)0x40, copy);
    }

    private string? UnsupportedNativeEyes(Fiber fiber, Albeoris.Games.FF8.Jsm.JsmOperation operation)
    {
        if (operation.Opcode is not (Albeoris.Games.FF8.Jsm.Jsm.Opcode.OPENEYES or Albeoris.Games.FF8.Jsm.Jsm.Opcode.CLOSEEYES))
        {
            return null;
        }
        try
        {
            PrepareNativeEyes(actors[fiber.ObjectId], operation.Opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.CLOSEEYES);
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentException or ArithmeticException)
        {
            return exception.Message;
        }
    }

    private void ValidateNativeEyes(IReadOnlyList<ActorState> savedActors)
    {
        foreach (ActorState actor in savedActors)
        {
            if (actor.NativeEyes is not { } eyes)
            {
                continue;
            }
            if (eyes.Version != 1 || eyes.Mode is not (0 or 0x80000 or 0x100000 or 0x200000) || eyes.Mode == 0 && (eyes.BoundModelId is not null || eyes.BackendHighNibble114 is not null || eyes.Patch is not null))
            {
                throw new InvalidDataException("Saved native eye mode is invalid.");
            }
            if (eyes.Mode == 0)
            {
                continue;
            }
            if (eyes.BoundModelId != actor.NativeAnimationHeader?.BoundModelId || eyes.BoundModelId is not int model || ReadNativeEyes?.Invoke(model) is not { } definition || !program!.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId))
            {
                throw new InvalidDataException("Saved native eye copy lost its original registered resource.");
            }
            NativeEyeCopy? expected = eyes.Mode == 0x80000 ? null : definition.Copy(eyes.Mode == 0x200000);
            byte? backend = definition.Bank.Length == 0 ? null : eyes.Mode == 0x80000 ? (byte)0x10 : eyes.Mode == 0x200000 ? (byte)0x20 : (byte)0x40;
            if (eyes.Mode == 0x80000 && actor.NativeEyeBlinkParameter611 is null || eyes.BackendHighNibble114 != backend || JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(eyes.Patch))
            {
                throw new InvalidDataException("Saved native eye copy disagrees with the authenticated resource rectangle.");
            }
        }
    }
}
