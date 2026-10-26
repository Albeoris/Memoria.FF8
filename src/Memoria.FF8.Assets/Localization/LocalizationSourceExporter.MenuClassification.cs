using System.Buffers.Binary;

namespace Memoria.FF8.Assets.Localization;

public static partial class LocalizationSourceExporter
{
    private sealed partial class Reader
    {
        private void ClassifyMenuSection(int slot, string prefix, IReadOnlyDictionary<string, string?> metadata, byte[] header, byte[] content)
        {
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8 + 4));
            if (raw is 0 or uint.MaxValue || (ulong)raw - 1 + size > (ulong)content.Length)
            {
                throw new InvalidDataException("Menu section is outside its archive.");
            }
            byte[] body = content.AsSpan(checked((int)raw - 1), checked((int)size)).ToArray();
            string? classification = null;
            if (body.Length >= 8 && BinaryPrimitives.ReadUInt32LittleEndian(body) == 0x10)
            {
                WorldAssetReader.ReadTim(body, 0);
                classification = "Artwork: native TIM texture; use the artwork localization resource.";
            }
            else if (slot == 3 && body.Length >= 128 && body[0] == (byte)'S' && body[1] == (byte)'C')
            {
                classification = "Binary: PocketStation application/save payload, including its platform title and icon; not an FF8 string bank.";
            }
            else if (slot == 7)
            {
                classification = "Binary: native artwork sprite directory and placement records.";
            }
            else if (slot is >= 168 and <= 175 or 205)
            {
                classification = "Binary: native tutorial input WORD program; text operands address separate tutorial text banks.";
            }
            else if (slot is 176 or 178)
            {
                if (body.Length < 8 * 152)
                {
                    throw new InvalidDataException("Tutorial character template is incomplete.");
                }
                classification = "Binary: eight native tutorial character initialization records; character names belong to the saved name owner.";
            }
            else if (slot is 177 or 179)
            {
                if (body.Length < 16 * 68)
                {
                    throw new InvalidDataException("Tutorial guardian template is incomplete.");
                }
                classification = "Binary: sixteen native tutorial guardian initialization records; native initialization replaces the unused name buffers with saved guardian names.";
            }
            gaps.Add(new("Menus", "menu/mngrp.bin/" + prefix, classification ?? "Unsupported: opaque menu section has no established text or binary contract."));
        }
    }
}
