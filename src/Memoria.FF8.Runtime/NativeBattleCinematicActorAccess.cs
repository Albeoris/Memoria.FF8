using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Reads cinematic actor data from its hosted context or original header fields.</summary>
public static class NativeBattleCinematicActorAccess
{
    private const uint HeaderPointer = 0x279744C;

    public static int ActorCapacity(NativeBattleWaterExecutionFrame frame) => ReadContext(frame)?.Roster.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount;

    public static uint ReadModelPointer(NativeBattleWaterExecutionFrame frame, int row, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? actors.ReadCachedActor(row).ModelAddress : CoreDword(frame, NativeField(frame, 0x60, row, 4, headerAddress));
    }

    public static ushort ReadCachedFlags0(NativeBattleWaterExecutionFrame frame, int row, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? actors.ReadCachedActor(row).Flags0 : CoreWord(frame, NativeField(frame, 0x4E, row, 2, headerAddress));
    }

    public static short ReadPositionWord(NativeBattleWaterExecutionFrame frame, int row, int lane, uint? headerAddress = null)
    {
        ValidateLane(lane);
        if (ReadContext(frame, headerAddress) is not { } actors)
        {
            return NativeWord(frame, 0x84, row, lane, headerAddress);
        }
        NativeBattleCinematicActorCache actor = actors.ReadCachedActor(row);
        return RequireWord(lane == 3 ? actor.Vertical34 : VectorLane(actor.Position28, lane));
    }

    public static short ReadRotationWord(NativeBattleWaterExecutionFrame frame, int row, int lane, uint? headerAddress = null)
    {
        ValidateLane(lane);
        if (ReadContext(frame, headerAddress) is not { } actors)
        {
            return NativeWord(frame, 0xD8, row, lane, headerAddress);
        }
        NativeBattleCinematicActorCache actor = actors.ReadCachedActor(row);
        return RequireWord(lane switch
        {
            0 => actor.Rotation12,
            1 => actor.Rotation14,
            2 => actor.Rotation16,
            _ => null
        });
    }

    public static short ReadScaleWord(NativeBattleWaterExecutionFrame frame, int row, int lane, uint? headerAddress = null)
    {
        ValidateLane(lane);
        if (ReadContext(frame, headerAddress) is not { } actors)
        {
            return NativeWord(frame, 0x120, row, lane, headerAddress);
        }
        NativeBattleCinematicActorCache actor = actors.ReadCachedActor(row);
        return RequireWord(lane == 3 ? actor.Scale154 : VectorLane(actor.Scale148, lane));
    }

    public static NativeBattleVector ReadPosition(NativeBattleWaterExecutionFrame frame, int row, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? RequireVector(actors.ReadCachedActor(row).Position28) : NativeVector(frame, 0x84, row, headerAddress);
    }

    public static NativeBattleVector ReadRotation(NativeBattleWaterExecutionFrame frame, int row, uint? headerAddress = null)
    {
        if (ReadContext(frame, headerAddress) is not { } actors)
        {
            return NativeVector(frame, 0xD8, row, headerAddress);
        }
        NativeBattleCinematicActorCache actor = actors.ReadCachedActor(row);
        return new(RequireWord(actor.Rotation12), RequireWord(actor.Rotation14), RequireWord(actor.Rotation16));
    }

    public static NativeBattleVector ReadScale(NativeBattleWaterExecutionFrame frame, int row, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? RequireVector(actors.ReadCachedActor(row).Scale148) : NativeVector(frame, 0x120, row, headerAddress);
    }

    public static byte ReadActorOrdinal(NativeBattleWaterExecutionFrame frame, int row, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? actors.ReadActorOrdinal(row) : NativeByte(frame, 0xCC, row, headerAddress);
    }

    public static int ReadCurrentCount(NativeBattleWaterExecutionFrame frame, bool signedNative = false, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? actors.CurrentActors.Length : NativeCount(frame, 0x41, signedNative, headerAddress);
    }

    public static byte ReadCurrentActor(NativeBattleWaterExecutionFrame frame, int index, uint? headerAddress = null)
    {
        if (ReadContext(frame, headerAddress) is not { } actors)
        {
            return NativeByte(frame, 0x48, index, headerAddress);
        }
        return index == 0 && actors.CurrentActors.IsEmpty ? actors.ReadFirstActor() : ReadActor(actors.CurrentActors, index);
    }

    public static int ReadSavedCount(NativeBattleWaterExecutionFrame frame, bool signedNative, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? actors.ReadSavedActors().Length : NativeCount(frame, 0x17A, signedNative, headerAddress);
    }

    public static byte ReadSavedActor(NativeBattleWaterExecutionFrame frame, int index, uint? headerAddress = null)
    {
        return ReadContext(frame, headerAddress) is { } actors ? ReadActor(actors.ReadSavedActors(), index) : NativeByte(frame, 0x174, index, headerAddress);
    }

    internal static NativeBattleCinematicActorContext? ReadContext(NativeBattleWaterExecutionFrame frame, uint? headerAddress = null)
    {
        NativeBattleCinematicActorContext? context = frame.Core.PresentationActors;
        if (context is not null && (headerAddress ?? CoreDword(frame, HeaderPointer)) != NativeBattleScriptedEffectMemory.Address)
        {
            throw new InvalidDataException("Hosted cinematic actors require the current scripted-effect header owner.");
        }
        return context;
    }

    private static uint NativeField(NativeBattleWaterExecutionFrame frame, uint offset, int row, int stride, uint? headerAddress)
    {
        return unchecked((headerAddress ?? CoreDword(frame, HeaderPointer)) + offset + (uint)(row * stride));
    }

    private static short NativeWord(NativeBattleWaterExecutionFrame frame, uint offset, int row, int lane, uint? headerAddress)
    {
        return unchecked((short)CoreWord(frame, NativeField(frame, offset + (uint)(lane * 2), row, 8, headerAddress)));
    }

    private static NativeBattleVector NativeVector(NativeBattleWaterExecutionFrame frame, uint offset, int row, uint? headerAddress)
    {
        byte[] data = frame.Core.Read(NativeField(frame, offset, row, 8, headerAddress), 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(4)));
    }

    private static byte NativeByte(NativeBattleWaterExecutionFrame frame, uint offset, int index, uint? headerAddress) => frame.Core.Read(NativeField(frame, offset, index, 1, headerAddress), 1)[0];

    private static int NativeCount(NativeBattleWaterExecutionFrame frame, uint offset, bool signed, uint? headerAddress)
    {
        byte raw = NativeByte(frame, offset, 0, headerAddress);
        return signed ? unchecked((sbyte)raw) : raw;
    }

    private static short? VectorLane(NativeBattleVector? value, int lane) => lane switch
    {
        0 => value?.X,
        1 => value?.Y,
        _ => value?.Z
    };

    private static short RequireWord(short? value) => value ?? throw new NotSupportedException("Hosted cinematic cache requires the retained actor field.");

    private static NativeBattleVector RequireVector(NativeBattleVector? value) => value ?? throw new NotSupportedException("Hosted cinematic cache requires the retained actor vector.");

    private static void ValidateLane(int lane)
    {
        if ((uint)lane >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(lane));
        }
    }

    private static byte ReadActor(ImmutableArray<byte> actors, int index)
    {
        if ((uint)index >= (uint)actors.Length)
        {
            throw new InvalidDataException("Hosted cinematic actor index leaves its retained selection.");
        }
        return actors[index];
    }
}
