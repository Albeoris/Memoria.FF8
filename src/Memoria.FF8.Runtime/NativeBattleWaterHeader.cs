using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A transient view of retained header bytes, including validity of fields copied from partially known graphics rows.</summary>
public sealed record NativeBattleWaterHeader([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known)
{
    public const int Length = 0x17C;

    public static NativeBattleWaterHeader FromColdProcess() => new(new byte[Length].ToImmutableArray(), Enumerable.Repeat(true, Length).ToImmutableArray());
    public static NativeBattleWaterHeader Unknown() => new(new byte[Length].ToImmutableArray(), new bool[Length].ToImmutableArray());

    public void Validate()
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != Length || Known.Length != Length)
        {
            throw new InvalidDataException("Native header capture requires its complete byte and validity owners.");
        }
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

public sealed record NativeBattleWaterHeaderCaptureResult(NativeBattleWaterHeader Header, NativeBattleEffectArena Arena, int CurrentHeaderIndex);

public static class NativeBattleWaterHeaderCapture
{
    public static NativeBattleWaterHeaderCaptureResult CaptureAA4E90(NativeBattleWaterHeader header, NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int slotIndex, byte caster, int currentHeaderIndex, byte firstHeaderFirstActor, ImmutableArray<byte> currentContacts, byte contactCount, ImmutableArray<NativeBattleGraphicsModel> graphics)
    {
        ValidateRows(graphics);
        if (caster >= 7 || currentHeaderIndex < 0 || currentHeaderIndex >= NativeBattleActionPacketsState.HeaderCapacity || firstHeaderFirstActor >= 7 || contactCount > 0 && (currentContacts.IsDefault || currentContacts.Length < contactCount))
        {
            throw new NotSupportedException("Native header capture requires its actual caster, selected header and contact owners.");
        }
        HeaderWriter writer = new(header);
        writer.Byte(0x44, caster);
        writer.Byte(0x48, firstHeaderFirstActor);
        for (int index = 0; index < 7; index++)
        {
            writer.Word(0x4E + 2 * index, unchecked((short)graphics[index].Flags0));
        }
        uint seen = 0;
        byte unique = 0;
        for (int index = 0; index < contactCount; index++)
        {
            byte actor = currentContacts[index];
            if (actor >= 7)
            {
                throw new NotSupportedException("Native header contact leaves the seven physical graphics rows.");
            }
            uint bit = 1u << (actor & 31);
            if ((seen & bit) == 0)
            {
                seen |= bit;
                writer.Byte(0x48 + unique, actor);
                writer.Byte(0xCC + actor, unique);
                unique++;
            }
        }
        NativeBattleWaterHeader result = writer.Freeze();
        arena = arena.Write(bank.SlotOffset(arena, slotIndex) + 0x1B, [result.Bytes[0x48]]);
        writer.Byte(0x41, unique);
        return new(writer.Freeze(), arena, currentHeaderIndex);
    }

    public static NativeBattleWaterHeader CacheRowsAA4B60(NativeBattleWaterHeader header, ImmutableArray<NativeBattleGraphicsModel> graphics)
    {
        ValidateRows(graphics);
        HeaderWriter writer = new(header);
        for (int index = 0; index < 7; index++)
        {
            NativeBattleGraphicsModel model = graphics[index];
            writer.Dword(0x60 + 4 * index, 0x1D972C0u + 0x9Cu * (uint)index);
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
        return writer.Freeze();
    }

    private static void ValidateRows(ImmutableArray<NativeBattleGraphicsModel> graphics)
    {
        if (graphics.IsDefault || graphics.Length != 7 || graphics.Where((model, index) => model is null || model.Index != index).Any())
        {
            throw new NotSupportedException("Native header capture requires all seven physical graphics rows in bank order.");
        }
    }

    private sealed class HeaderWriter
    {
        private readonly byte[] bytes;
        private readonly bool[] known;

        public HeaderWriter(NativeBattleWaterHeader header)
        {
            header.Validate();
            bytes = header.Bytes.ToArray();
            known = header.Known.ToArray();
        }

        public void Byte(int offset, byte value)
        {
            bytes[offset] = value;
            known[offset] = true;
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

        public NativeBattleWaterHeader Freeze() => new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}
