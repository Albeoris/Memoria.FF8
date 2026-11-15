namespace Memoria.FF8.Assets;

/// <summary>Editable destination for a single-model GLB replacement.</summary>
public sealed record ModelImportTarget(int Version, string ModelId, string Mode, string Bank = "embedded", int? ClipSlot = null)
{
    public void Validate()
    {
        if (Version != 1)
        {
            throw new InvalidDataException($"Unsupported model import metadata version {Version}.");
        }
        if (!IsModelId(ModelId) || !IsBank(Bank))
        {
            throw new InvalidDataException("Model import metadata contains an unsupported model address or clip bank.");
        }
        if (Mode == "clip")
        {
            if (ClipSlot is null or < 0 or > 65535)
            {
                throw new InvalidDataException("Clip import metadata requires a slot between 0 and 65535.");
            }
        }
        else if (Mode is not ("model" or "geometry") || ClipSlot is not null || Bank != "embedded")
        {
            throw new InvalidDataException("Model import metadata contains an unsupported mode, bank or clip slot.");
        }
    }

    public static bool IsBank(string? bank) => bank is { Length: > 0 and < 128 } && bank.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static bool IsModelId(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }
        string[] parts = id.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')))
        {
            return false;
        }
        return parts.Length == 3 && parts[0] == "field" && int.TryParse(parts[2], out int fieldIndex) && fieldIndex >= 0
            || parts.Length == 2 && parts[0] == "battle" && (parts[1].EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || parts[1].EndsWith(".x", StringComparison.OrdinalIgnoreCase))
            || parts.Length == 2 && parts[0] == "world" && int.TryParse(parts[1], out int shape) && shape is >= 0 and < 64
            || parts.Length == 3 && parts[0] == "world" && parts[1] == "static" && int.TryParse(parts[2], out int staticShape) && staticShape is >= 64 and <= 255;
    }
}
