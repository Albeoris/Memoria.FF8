using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Reads cinematic actor data from its captured model owners.</summary>
public static class NativeBattleCinematicActorAccess
{
    private const uint HeaderPointer = 0x279744C;

    public static BattleModelOwner ReadSlotActor(NativeBattleWaterExecutionFrame frame)
    {
        BattleModelOwner actor = frame.Arena.ReadExecutionReference<BattleExecutionReference.Model>(SlotOffset(frame, 0x1B, 1), 1).Owner;
        _ = ReadContext(frame).ReadCachedActor(actor);
        return actor;
    }

    internal static NativeBattleWaterExecutionFrame WriteSlotActor(NativeBattleWaterExecutionFrame frame, BattleModelOwner actor, NativeBattleModelAliases aliases)
    {
        _ = ReadContext(frame).ReadCachedActor(actor);
        return frame with { Arena = WriteActorOperand(frame.Arena, SlotOffset(frame, 0x1B, 1), actor, aliases.EncodeOperand(actor, 1)) };
    }

    internal static NativeBattleWaterExecutionFrame WriteSlotOriginalCell(NativeBattleWaterExecutionFrame frame, NativeBattleModelSlot slot)
    {
        return frame with { Arena = frame.Arena.WriteExecutionReference(SlotOffset(frame, 0x1B, 1), 1, new BattleExecutionReference.CinematicOriginalCell(slot), [slot.Offset]) };
    }

    internal static NativeBattleModelSlot? ReadSlotOriginalCell(NativeBattleWaterExecutionFrame frame)
    {
        return frame.Arena.TryReadExecutionReference<BattleExecutionReference.CinematicOriginalCell>(SlotOffset(frame, 0x1B, 1), 1, out var selected) ? selected.Slot : null;
    }

    internal static NativeBattleEffectArena WriteActorOperand(NativeBattleEffectArena arena, int offset, BattleModelOwner actor, ImmutableArray<byte>? encoded)
    {
        BattleExecutionReference.Model reference = new(actor);
        if (encoded is null)
        {
            // A texture transfer may overlap this slot byte. There is no physical store for an unencoded actor.
            Span<byte> bytes = stackalloc byte[1];
            Span<bool> known = stackalloc bool[1];
            arena.ReadWithKnownness(offset, bytes, known);
            byte? retained = known[0] ? bytes[0] : arena.TryReadExecutionReference<BattleExecutionReference.Model>(offset, 1, out var previous) ? previous.CinematicTextureByte : null;
            reference = reference with { CinematicTextureByte = retained };
        }
        return arena.WriteExecutionReference(offset, 1, reference, encoded);
    }

    public static ushort ReadCachedFlags0(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress).ReadCachedActor(owner).Flags0;
    }

    public static short ReadPositionWord(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, int lane, uint? headerAddress = null)
    {
        ValidateLane(lane);
        NativeBattleCinematicActorCache actor = ReadContext(frame, headerAddress).ReadCachedActor(owner);
        return RequireWord(lane == 3 ? actor.Vertical34 : VectorLane(actor.Position28, lane));
    }

    public static short ReadRotationWord(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, int lane, uint? headerAddress = null)
    {
        ValidateLane(lane);
        NativeBattleCinematicActorCache actor = ReadContext(frame, headerAddress).ReadCachedActor(owner);
        return RequireWord(lane switch
        {
            0 => actor.Rotation12,
            1 => actor.Rotation14,
            2 => actor.Rotation16,
            _ => null
        });
    }

    public static short ReadScaleWord(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, int lane, uint? headerAddress = null)
    {
        ValidateLane(lane);
        NativeBattleCinematicActorCache actor = ReadContext(frame, headerAddress).ReadCachedActor(owner);
        return RequireWord(lane == 3 ? actor.Scale154 : VectorLane(actor.Scale148, lane));
    }

    public static NativeBattleVector ReadPosition(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, uint? headerAddress = null)
    {
        return RequireVector(ReadContext(frame, headerAddress).ReadCachedActor(owner).Position28);
    }

    public static NativeBattleVector ReadRotation(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, uint? headerAddress = null)
    {
        NativeBattleCinematicActorCache actor = ReadContext(frame, headerAddress).ReadCachedActor(owner);
        return new(RequireWord(actor.Rotation12), RequireWord(actor.Rotation14), RequireWord(actor.Rotation16));
    }

    public static NativeBattleVector ReadScale(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, uint? headerAddress = null)
    {
        return RequireVector(ReadContext(frame, headerAddress).ReadCachedActor(owner).Scale148);
    }

    public static int ReadActorOrdinal(NativeBattleWaterExecutionFrame frame, BattleModelOwner owner, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress).ReadActorOrdinal(owner);
    }

    public static int ReadCurrentCount(NativeBattleWaterExecutionFrame frame, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress).CurrentActors.Length;
    }

    public static BattleModelOwner ReadCurrentActor(NativeBattleWaterExecutionFrame frame, int index, uint? headerAddress = null)
    {
        NativeBattleCinematicActorContext context = ReadContext(frame, headerAddress);
        return index == 0 && context.CurrentActors.IsEmpty ? context.ReadFirstActor() : ReadActor(context.CurrentActors, index);
    }

    public static int ReadSavedCount(NativeBattleWaterExecutionFrame frame, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress).ReadSavedActors().Length;
    }

    public static BattleModelOwner ReadSavedActor(NativeBattleWaterExecutionFrame frame, int index, uint? headerAddress = null)
    {
        return ReadActor(ReadContext(frame, headerAddress).ReadSavedActors(), index);
    }

    internal static NativeBattleCinematicActorContext ReadContext(NativeBattleWaterExecutionFrame frame, uint? headerAddress = null)
    {
        if ((headerAddress ?? CoreDword(frame, HeaderPointer)) != NativeBattleScriptedEffectMemory.Address)
        {
            throw new InvalidDataException("Cinematic actors require the current scripted-effect header owner.");
        }
        return frame.Core.PresentationActors ?? throw new InvalidDataException("Cinematic actor context has not been captured.");
    }

    private static short? VectorLane(NativeBattleVector? value, int lane) => lane switch
    {
        0 => value?.X,
        1 => value?.Y,
        _ => value?.Z
    };

    private static short RequireWord(short? value) => value ?? throw new NotSupportedException("Cinematic cache requires the retained actor field.");

    private static NativeBattleVector RequireVector(NativeBattleVector? value) => value ?? throw new NotSupportedException("Cinematic cache requires the retained actor vector.");

    private static void ValidateLane(int lane)
    {
        if ((uint)lane >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(lane));
        }
    }

    private static BattleModelOwner ReadActor(ImmutableArray<BattleModelOwner> actors, int index)
    {
        if ((uint)index >= (uint)actors.Length)
        {
            throw new InvalidDataException("Cinematic actor index leaves its retained selection.");
        }
        return actors[index];
    }
}
