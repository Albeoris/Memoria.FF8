using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated immutable inputs for native522770/52D1E0 and495530's command projection.</summary>
public sealed record NativeDrawPointCatalog(string ExecutableHash, string KernelHash, ImmutableArray<byte> PointBytes, ImmutableArray<byte> CommandIds, string NoEligibleMessage, string CommonMessageHash)
{
    /// <summary>Original common message bank, indexed by the native message ID, including unexpanded control tags.</summary>
    public ImmutableArray<string> CommonMessages { get; init; }
    public ImmutableArray<string> CommonMessageHashes { get; init; }
    public ImmutableArray<ImmutableArray<byte>> CommonMessageBytes { get; init; }
    public ImmutableArray<string> MagicNames { get; init; }

    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash) || !Hash(KernelHash) || PointBytes.IsDefault || PointBytes.Length != 256 || CommandIds.IsDefault || CommandIds.Length != 19 || string.IsNullOrEmpty(NoEligibleMessage) || !Hash(CommonMessageHash))
        {
            throw new InvalidDataException("Native draw-point catalog lacks its original point bytes, command mapping or common message7.");
        }
        if (!CommonMessages.IsDefault || !CommonMessageHashes.IsDefault || !CommonMessageBytes.IsDefault || !MagicNames.IsDefault)
        {
            ValidateInteractionResources();
        }
    }

    public void ValidateInteractionResources()
    {
        if (CommonMessages.IsDefault || CommonMessages.Length != 8 || CommonMessages.Any(string.IsNullOrEmpty) || CommonMessageHashes.IsDefault || CommonMessageHashes.Length != 8 || CommonMessageHashes.Any(value => !Hash(value)) || CommonMessageBytes.IsDefault || CommonMessageBytes.Length != 8 || CommonMessageBytes.Any(value => value.IsDefaultOrEmpty || value[^1] != 0) || MagicNames.IsDefault || MagicNames.Length != 57 || MagicNames.Any(value => value is null) || CommonMessages[7] != NoEligibleMessage || CommonMessageHashes[7] != CommonMessageHash)
        {
            throw new InvalidDataException("Native draw-point interaction requires original common messages0..7, their terminated bytes and kernel magic names.");
        }
    }

    public string Message(int id)
    {
        if (id == 7 && CommonMessages.IsDefault)
        {
            return NoEligibleMessage;
        }
        ValidateInteractionResources();
        return CommonMessages[id];
    }

    public string MessageHash(int id)
    {
        if (id == 7 && CommonMessageHashes.IsDefault)
        {
            return CommonMessageHash;
        }
        ValidateInteractionResources();
        return CommonMessageHashes[id];
    }

    /// <summary>Native522770 substitutes magic-name and selected-character operands; variable7 is formatted from its actual published value.</summary>
    public string FormatMessage(int id, int magicId, int? characterId = null, int? quantity = null)
    {
        ValidateInteractionResources();
        if ((uint)magicId >= MagicNames.Length)
        {
            throw new NotSupportedException("Draw-point text references a magic name outside the original kernel table.");
        }
        string text = Message(id).Replace("{Name 32}", MagicNames[magicId], StringComparison.Ordinal);
        if (characterId is int identity)
        {
            text = text.Replace("{Char Squall}", CharacterTag(identity), StringComparison.Ordinal);
        }
        if (quantity is int count)
        {
            text = text.Replace("{Var 55}", count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        return text;
    }

    /// <summary>Native522770 stage2 preserves the original Cancel/header, field-order identities, capacity color and junction marker.</summary>
    public string FormatSelection(int[] fieldParty, NativeCharacterVitals vitals, int magicId, byte eligibleCombatSlots)
    {
        ValidateInteractionResources();
        vitals.Validate();
        if (fieldParty is null || fieldParty.Length > 3)
        {
            throw new InvalidDataException("Draw selection accepts at most three physical field-party identity bytes.");
        }
        string[] lines = Message(4).Split("{Line}", StringSplitOptions.None);
        if (lines.Length != 5)
        {
            throw new InvalidDataException("Original draw selection template lost its header, Cancel or three character rows.");
        }
        List<string> selected = [lines[0], lines[1]];
        for (int slot = 0; slot < fieldParty.Length && fieldParty[slot] != 255; slot++)
        {
            int identity = fieldParty[slot];
            int combat = Array.FindIndex(vitals.CombatParty, row => row != 255 && vitals.Records[row].CharacterId == identity);
            if (combat < 0)
            {
                throw new NotSupportedException("Draw selection identity has no current native combat-slot projection.");
            }
            NativeCharacterVital character = vitals.Records[vitals.CombatParty[combat]];
            string line = lines[slot + 2].Replace("{Char Squall}", CharacterTag(identity), StringComparison.Ordinal);
            if (NativeCharacterVitals.GetMagicCapacity(character, magicId) == 0 && (eligibleCombatSlots & (1 << combat)) != 0)
            {
                line = line.Replace("{Color Disabled}", "{Color White}", StringComparison.Ordinal);
            }
            if (NativeCharacterVitals.IsMagicJunctioned(character, magicId))
            {
                int nameEnd = line.IndexOf('}', line.IndexOf("{Char ", StringComparison.Ordinal)) + 1;
                if (nameEnd == 0 || line.Length < nameEnd + 3 || line[nameEnd + 1] != ' ' || line[nameEnd + 2] != ' ')
                {
                    throw new InvalidDataException("Original draw selection junction marker lost its character-row padding.");
                }
                line = line[..(nameEnd + 1)] + "{Key 65}" + line[(nameEnd + 3)..];
            }
            selected.Add(line);
        }
        return string.Join("{Line}", selected);
    }
    public static string CharacterTag(int characterId)
    {
        return characterId switch
        {
            0 => "{Char Squall}",
            1 => "{Char Zell}",
            2 => "{Char Irvine}",
            3 => "{Char Quistis}",
            4 => "{Char Rinoa}",
            5 => "{Char Selphie}",
            6 => "{Char Seifer}",
            7 => "{Char Edea}",
            8 => "{Char Laguna}",
            9 => "{Char Kiros}",
            10 => "{Char Ward}",
            _ => throw new NotSupportedException("Draw-point character text requires an original saved identity0..10.")
        };
    }
    public bool HasDrawCommand(NativeCharacterVital character)
    {
        for (int slot = 0; slot < 4; slot++)
        {
            byte ability = unchecked((byte)(character.Junction.Commands >> (8 * slot)));
            if (ability is >= 20 and < 39 && CommandIds[ability - 20] == 6)
            {
                return true;
            }
        }
        return false;
    }

    private static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
