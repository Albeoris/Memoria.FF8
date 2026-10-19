using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class PlayerSaveStore
{
    public string? OriginalDirectory { get; private set; }
    public IReadOnlyList<string> OriginalDirectoryCandidates { get; }

    public void SetOriginalDirectory(string path)
    {
        OriginalDirectory = OriginalSaveLocation.ValidateDirectory(path);
        entries.Clear();
    }

    public static int GetBank(int slot)
    {
        if (slot > TotalSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return slot <= MemoriaSlotCount ? 0 : 1 + (slot - MemoriaSlotCount - 1) / OriginalSlotsPerBank;
    }

    public static int GetFirstSlot(int bank)
    {
        ValidateBank(bank);
        return bank == 0 ? 1 : MemoriaSlotCount + (bank - 1) * OriginalSlotsPerBank + 1;
    }

    public static int GetBankSlotCount(int bank)
    {
        ValidateBank(bank);
        return bank == 0 ? MemoriaSlotCount : OriginalSlotsPerBank;
    }

    public void Transfer(int sourceSlot, int destinationSlot, Action<GameState>? validateContinuation = null)
    {
        if (destinationSlot <= 0 || destinationSlot > TotalSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationSlot));
        }
        string source = GetPath(sourceSlot);
        string destination = GetPath(destinationSlot);
        if (source.Equals(destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Choose a different destination slot.");
        }
        if (sourceSlot > MemoriaSlotCount && destinationSlot > MemoriaSlotCount)
        {
            SaveOriginal(destination, File.ReadAllBytes(source), validateContinuation);
        }
        else
        {
            Save(destinationSlot, new GameSession(Load(sourceSlot)), validateContinuation);
        }
        entries.Remove(destination);
    }

    private static void ValidateBank(int bank)
    {
        if (bank < 0 || bank >= BankCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bank));
        }
    }

    private string GetOriginalPath(int slot)
    {
        string original = OriginalDirectory ?? throw new InvalidOperationException("Choose the original FF8 save folder first.");
        int bank = GetBank(slot);
        return Path.Combine(original, $"slot{bank}_save{slot - GetFirstSlot(bank) + 1:00}.ff8");
    }

    private GameState LoadOriginal(string path)
    {
        GameState state = (loadOriginal ?? throw new InvalidOperationException("Original FF8 saves require initialized game resources."))(path);
        if (state.Scene == SceneKind.Title)
        {
            throw new InvalidDataException("This save contains no active game.");
        }
        return state;
    }

    private void SaveOriginal(string path, byte[] bytes, Action<GameState>? validateContinuation)
    {
        ValidatePath(path);
        NativePcSaveCodec.Decode(bytes);
        string temporary = path + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            GameState candidate = LoadOriginal(temporary);
            validateContinuation?.Invoke(candidate);
            File.Move(temporary, path, overwrite: true);
            entries.Remove(path);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static SaveSummary ReadOriginalSummary(string path)
    {
        byte[] block = NativePcSaveCodec.Decode(File.ReadAllBytes(path)).Block;
        int main = NativePcSaveData.MainOffset;
        bool world = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(main + 3312)) == 2;
        int fieldId = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(world ? NativePcSaveData.EventBankOffset + 84 : main + 3314));
        uint seconds = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(400));
        int place = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(388));
        int worldStart = main + 4624;
        int pose = block[worldStart + 105] switch { 48 => 36, 50 => 24, _ => 0 };
        int? worldX = world ? BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(worldStart + pose)) : null;
        int? worldZ = world ? BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(worldStart + pose + 4)) : null;
        return new SaveSummary(world ? SceneKind.WorldMap : SceneKind.Field, fieldId, "", new NativePlaytimeState(seconds, 0, 0), block.AsSpan(405, 3).ToArray().Where(id => id != 255).Select(id => (int)id).ToArray(), place, worldX, worldZ);
    }

    private static bool IsInDirectory(string path, string parent)
    {
        return path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
