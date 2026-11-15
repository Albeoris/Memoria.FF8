using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A transient view of retained header bytes, including validity of fields copied from partially known graphics rows.</summary>
public sealed record NativeBattleWaterHeader([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known)
{
    public const int Length = 0x17C;

    [JsonIgnore]
    public NativeBattleCinematicActorContext? PresentationActors { get; init; }

    public static NativeBattleWaterHeader FromColdProcess() => new(new byte[Length].ToImmutableArray(), Enumerable.Repeat(true, Length).ToImmutableArray());
    public static NativeBattleWaterHeader Unknown() => new(new byte[Length].ToImmutableArray(), new bool[Length].ToImmutableArray());

    public void Validate()
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != Length || Known.Length != Length)
        {
            throw new InvalidDataException("Native header capture requires its complete byte and validity owners.");
        }
        PresentationActors?.Validate();
    }

    public ImmutableArray<byte> Read(int offset, int count)
    {
        Validate();
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            throw new InvalidDataException("Native header read leaves its retained byte owner.");
        }
        if (Known.Skip(offset).Take(count).Any(value => !value))
        {
            throw new NotSupportedException("Native header read requires known current source fields.");
        }
        return Bytes.AsSpan().Slice(offset, count).ToArray().ToImmutableArray();
    }
}

public sealed record NativeBattleWaterHeaderCaptureResult(NativeBattleWaterHeader Header, NativeBattleEffectArena Arena);

public static class NativeBattleWaterHeaderCapture
{
    public static NativeBattleWaterHeaderCaptureResult CaptureAA4E90(NativeBattleWaterHeader header, NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int slotIndex, BattleModelOwner caster, BattleModelOwner firstHeaderFirstActor, ImmutableArray<BattleModelOwner> contacts, BattleActorModels models, NativeBattleModelBoundary boundary)
    {
        NativeBattleCinematicActorContext context = header.PresentationActors ?? NativeBattleCinematicActorContext.FromGraphics(models);
        _ = context.ReadCachedActor(caster);
        NativeBattleCinematicActorContext captured = context.RefreshFlags(models).CaptureCurrentContacts(contacts, firstHeaderFirstActor);
        NativeBattleModelAliases aliases = boundary.CaptureAliases(models);
        HeaderWriter writer = new(header);
        writer.Byte(0x44, Alias(caster));
        writer.Byte(0x48, Alias(firstHeaderFirstActor));
        for (byte slot = 0; slot < NativeBattleGraphicsModelAddress.NativeRowCount; slot++)
        {
            NativeBattleGraphicsModel model = boundary.ReadOriginalCell(NativeBattleModelSlot.Decode(slot), models);
            writer.Word(0x4E + 2 * slot, unchecked((short)model.Flags0));
        }
        // The seventh original list byte shares the low byte of the first raw flags field.
        bool allOriginal = captured.CurrentActors.All(actor => aliases.TryEncodeExact(actor, out _));
        int listExtent = allOriginal ? NativeBattleGraphicsModelAddress.NativeRowCount : 6;
        for (int ordinal = 0; ordinal < captured.CurrentActors.Length; ordinal++)
        {
            byte? alias = Alias(captured.CurrentActors[ordinal]);
            if (ordinal < listExtent)
            {
                writer.Byte(0x48 + ordinal, alias);
            }
            if (alias is byte slot)
            {
                writer.Byte(0xCC + slot, EncodeByte(ordinal));
            }
        }
        BattleModelOwner first = captured.ReadFirstActor();
        arena = arena.WriteExecutionReference(bank.SlotOffset(arena, slotIndex) + 0x1B, 1, new BattleExecutionReference.Model(first), aliases.EncodeOperand(first, 1));
        writer.Byte(0x41, EncodeByte(captured.CurrentActors.Length));
        return new(writer.Freeze() with { PresentationActors = captured }, arena);

        byte? Alias(BattleModelOwner actor) => aliases.TryEncodeExact(actor, out byte alias) ? alias : null;
    }

    public static NativeBattleWaterHeader CacheRowsAA4B60(NativeBattleWaterHeader header, BattleActorModels models, NativeBattleModelBoundary boundary)
    {
        HeaderWriter writer = new(header);
        NativeBattleCinematicActorContext cached = (header.PresentationActors ?? throw new InvalidDataException("Cinematic actor cache requires its captured live context.")).RefreshCache(models);
        for (byte index = 0; index < NativeBattleGraphicsModelAddress.NativeRowCount; index++)
        {
            NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(index);
            NativeBattleGraphicsModel model = boundary.ReadOriginalCell(slot, models);
            writer.Dword(0x60 + 4 * index, NativeBattleGraphicsModelAddress.EncodeOriginalCell(slot));
            NativeBattleModelTransform? transform = model.Transform;
            writer.Word(0x84 + 8 * index, transform?.Position28.X);
            writer.Word(0x86 + 8 * index, transform?.Position28.Y);
            writer.Word(0x88 + 8 * index, transform?.Position28.Z);
            writer.Word(0x8A + 8 * index, transform?.Vertical34);
            writer.Word(0xD8 + 8 * index, model.Rotation12);
            writer.Word(0xDA + 8 * index, model.Rotation14);
            writer.Word(0xDC + 8 * index, model.Rotation16);
            writer.Word(0xDE + 8 * index, null);
            writer.Word(0x120 + 8 * index, transform?.Scale148.X);
            writer.Word(0x122 + 8 * index, transform?.Scale148.Y);
            writer.Word(0x124 + 8 * index, transform?.Scale148.Z);
            writer.Word(0x126 + 8 * index, transform?.Scale154);
        }
        return writer.Freeze() with { PresentationActors = cached };
    }

    internal static NativeBattleCinematicActorContext SeedRetainedActorFields(NativeBattleWaterHeader header, BattleActorModels models, NativeBattleModelBoundary boundary)
    {
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models);
        ImmutableDictionary<BattleModelOwner, int?>.Builder ordinals = context.ActorOrdinals.ToBuilder();
        Dictionary<byte, BattleModelOwner> originalOwners = [];
        foreach (BattleModelOwner owner in models.Owners)
        {
            if (boundary.TryEncodeExactActor(owner, models, out byte slot))
            {
                originalOwners.Add(slot, owner);
                if (header.Known[0xCC + slot] && header.Bytes[0xCC + slot] < models.Roster.Count)
                {
                    ordinals[owner] = header.Bytes[0xCC + slot];
                }
            }
        }
        context = context with { ActorOrdinals = ordinals.ToImmutable() };

        if (!header.Known[0x17A] || header.Bytes[0x17A] > 6)
        {
            return context;
        }
        int savedCount = header.Bytes[0x17A];
        ImmutableArray<BattleModelOwner>.Builder saved = ImmutableArray.CreateBuilder<BattleModelOwner>(savedCount);
        for (int index = 0; index < savedCount; index++)
        {
            byte slot = header.Bytes[0x174 + index];
            if (!header.Known[0x174 + index] || slot >= NativeBattleModelBoundary.CellCount ||
                !originalOwners.TryGetValue(slot, out BattleModelOwner? owner))
            {
                return context;
            }
            saved.Add(owner);
        }
        return context.WithSavedActors(saved.MoveToImmutable());
    }

    private static byte? EncodeByte(int value) => value <= byte.MaxValue ? (byte)value : null;

    private sealed class HeaderWriter
    {
        private readonly byte[] bytes;
        private readonly bool[] known;
        private readonly NativeBattleCinematicActorContext? presentationActors;

        public HeaderWriter(NativeBattleWaterHeader header)
        {
            header.Validate();
            bytes = header.Bytes.ToArray();
            known = header.Known.ToArray();
            presentationActors = header.PresentationActors;
        }

        public void Byte(int offset, byte? value)
        {
            bytes[offset] = value.GetValueOrDefault();
            known[offset] = value.HasValue;
        }

        public void Word(int offset, short? value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value.GetValueOrDefault());
            Array.Fill(known, value.HasValue, offset, 2);
        }

        public void Dword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(known, true, offset, 4);
        }

        public NativeBattleWaterHeader Freeze() => new(bytes.ToImmutableArray(), known.ToImmutableArray()) { PresentationActors = presentationActors };
    }
}
