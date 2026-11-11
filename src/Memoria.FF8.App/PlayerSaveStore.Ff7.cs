using Memoria.FF7.Resources;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class PlayerSaveStore
{
    private readonly string? gameDirectory;
    private readonly string? legacyDirectory;
    internal SessionGame Game { get; }
    internal int MaximumSlot => Game == SessionGame.Ff7 ? MemoriaSlotCount + 150 : TotalSlotCount;
    internal int BankSize(int bank)
    {
        ValidateBank(bank);
        return Game == SessionGame.Ff7 && bank > 0 ? 75 : GetBankSlotCount(bank);
    }

    internal int FirstSlot(int bank)
    {
        ValidateBank(bank);
        return Game == SessionGame.Ff7 && bank > 0 ? MemoriaSlotCount + (bank - 1) * 75 + 1 : GetFirstSlot(bank);
    }

    internal string SlotLabel(int slot)
    {
        if (Game != SessionGame.Ff7)
        {
            return GetLabel(slot);
        }
        if (slot <= 0)
        {
            return "FFVII Quick Save";
        }
        return slot <= MemoriaSlotCount ? $"FFVII Memoria, Slot {slot:00}" : $"ORIGINAL FFVII {(slot - MemoriaSlotCount - 1) / 15:00}, Slot {(slot - MemoriaSlotCount - 1) % 15 + 1:00}";
    }

    private string ValidateOriginalDirectory(string path)
    {
        if (Game == SessionGame.Ff8)
        {
            return OriginalSaveLocation.ValidateDirectory(path);
        }
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("Choose an existing FFVII save folder.");
        }
        return fullPath;
    }

    private string GetFf7OriginalPath(int slot)
    {
        if (slot <= MemoriaSlotCount || slot > MaximumSlot)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        string folder = OriginalDirectory ?? throw new InvalidOperationException("Choose the original FFVII save folder first.");
        return Path.Combine(folder, $"save{(slot - MemoriaSlotCount - 1) / 15:00}.ff7");
    }

    private Ff7Installation Ff7Installation => Memoria.FF7.Resources.Ff7Installation.Discover(gameDirectory ?? throw new InvalidOperationException("Select the FINAL FANTASY VII installation first."));

    internal CombinedSessionSave LoadFf7(int slot)
    {
        EnsureFf7();
        string path = GetPath(slot);
        if (slot > MemoriaSlotCount)
        {
            Ff7NativeSaveSlot native = Ff7NativeSaveBank.Decode(File.ReadAllBytes(path)).GetSlot((slot - MemoriaSlotCount - 1) % 15);
            return new CombinedSessionSave { ActiveGame = SessionGame.Ff7, Ff7 = Ff7NativeSaveCodec.Import(native, Ff7Installation) };
        }
        CombinedSessionSave saved = CrossoverSaveAdapter.ReadEnvelope(path) ?? throw new InvalidDataException("This is not an FFVII save.");
        if (saved.ActiveGame != SessionGame.Ff7)
        {
            throw new InvalidDataException("Switch to the FFVIII world before loading FFVIII saves.");
        }
        return saved;
    }

    internal void SaveFf7(int slot, CombinedSessionSave saved)
    {
        EnsureFf7();
        saved.Validate();
        if (saved.ActiveGame != SessionGame.Ff7 || saved.Ff7 is null)
        {
            throw new InvalidDataException("Only the current FFVII world can be saved in the FFVII bank.");
        }
        if (slot < 0 || slot > MaximumSlot)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        if (slot > MemoriaSlotCount)
        {
            string path = GetPath(slot);
            Ff7NativeSaveBank bank = File.Exists(path) ? Ff7NativeSaveBank.Decode(File.ReadAllBytes(path)) : Ff7NativeSaveBank.Create();
            Ff7NativeSaveSlot native = Ff7NativeSaveCodec.Export(saved.Ff7, Ff7Installation);
            byte[] bytes = bank.WithSlot((slot - MemoriaSlotCount - 1) % 15, native).Encode();
            WriteFf7Bank(path, bytes);
            return;
        }
        if (slot != 0)
        {
            string path = Path.Combine(directory, $"slot-{slot:00}.json");
            ValidatePath(path);
            EnsureCurrentWorldTarget(path);
            CombinedSessionCodec.Save(path, saved);
            return;
        }
        List<QuickSaveFile> files = FindQuickSaves();
        int sequence = files.Where(file => file.Slot != int.MinValue).Select(file => -file.Slot).DefaultIfEmpty(0).Max();
        if (sequence == int.MaxValue)
        {
            throw new InvalidOperationException("The quick save sequence is exhausted.");
        }
        string target = NextQuickPath(sequence);
        ValidatePath(target);
        QuickSaveFile[] obsolete = files.Skip(quickSaveCount - 1).ToArray();
        ValidateRemoval(obsolete);
        CombinedSessionCodec.Save(target, saved);
        if (files.Count > 0 && File.GetLastWriteTimeUtc(target) <= files[0].SavedAtUtc)
        {
            File.SetLastWriteTimeUtc(target, files[0].SavedAtUtc.AddMilliseconds(1));
        }
        foreach (QuickSaveFile file in obsolete)
        {
            File.Delete(file.Path);
        }
    }

    internal void TransferFf7(int source, int destination)
    {
        EnsureFf7();
        if (destination <= 0 || destination > MaximumSlot)
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }
        if (source == destination)
        {
            throw new InvalidOperationException("Choose a different destination slot.");
        }
        if (source > MemoriaSlotCount && destination > MemoriaSlotCount)
        {
            Ff7NativeSaveSlot native = Ff7NativeSaveBank.Decode(File.ReadAllBytes(GetPath(source))).GetSlot((source - MemoriaSlotCount - 1) % 15);
            if (native.IsEmpty || !native.ChecksumValid)
            {
                throw new InvalidDataException("The source FFVII save is empty or corrupt.");
            }
            string target = GetPath(destination);
            Ff7NativeSaveBank bank = File.Exists(target) ? Ff7NativeSaveBank.Decode(File.ReadAllBytes(target)) : Ff7NativeSaveBank.Create();
            WriteFf7Bank(target, bank.WithSlot((destination - MemoriaSlotCount - 1) % 15, native).Encode());
            return;
        }
        SaveFf7(destination, LoadFf7(source));
    }

    private void WriteFf7Bank(string path, byte[] bytes)
    {
        ValidatePath(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private PlayerSaveEntry ReadFf7Entry(int slot, string path, DateTime? savedAt)
    {
        string label = SlotLabel(slot);
        try
        {
            ValidatePath(path);
            if (!File.Exists(path))
            {
                return new(slot, label, null, "Empty", false) { DetailsSystemId = "save.slot.empty" };
            }
            savedAt ??= File.GetLastWriteTime(path);
            if (slot > MemoriaSlotCount)
            {
                Ff7NativeSaveSlot native = Ff7NativeSaveBank.Decode(File.ReadAllBytes(path)).GetSlot((slot - MemoriaSlotCount - 1) % 15);
                if (native.IsEmpty)
                {
                    return new(slot, label, null, "Empty", false) { DetailsSystemId = "save.slot.empty" };
                }
                if (!native.ChecksumValid)
                {
                    throw new InvalidDataException("The FFVII slot checksum is invalid.");
                }
                return new(slot, label, savedAt, native.Location + " · " + native.Name + (native.Module == 1 ? "" : " · Native import for this game module is unsupported."), native.Module == 1) { CanCopy = true, ActiveGame = SessionGame.Ff7, SavedAtUtc = File.GetLastWriteTimeUtc(path), PlayTime = TimeSpan.FromSeconds(native.ElapsedSeconds) };
            }
            CombinedSessionSave? saved = CrossoverSaveAdapter.ReadEnvelope(path);
            if (saved?.ActiveGame != SessionGame.Ff7)
            {
                return new(slot, label, null, "Empty", false) { DetailsSystemId = "save.slot.empty" };
            }
            return new(slot, label, savedAt, saved.Ff7!.World is null ? saved.Ff7.FieldName : "World map", true) { ActiveGame = SessionGame.Ff7, SavedAtUtc = File.GetLastWriteTimeUtc(path) };
        }
        catch (Exception error) when (IsUnavailable(error))
        {
            return new(slot, label, savedAt, "Unavailable: " + error.Message, false) { DetailsSystemId = "save.slot.unavailable", DetailsError = error.Message };
        }
    }

    private void EnsureFf7()
    {
        if (Game != SessionGame.Ff7)
        {
            throw new InvalidOperationException("Switch to the FFVII world before accessing its save bank.");
        }
    }

    private string ResolveMemoriaReadPath(string name)
    {
        string current = Path.Combine(directory, name);
        if (!File.Exists(current) && legacyDirectory is not null)
        {
            string legacy = Path.Combine(legacyDirectory, name);
            if (File.Exists(legacy) && IsCurrentWorldSave(legacy))
            {
                return legacy;
            }
        }
        return current;
    }

    private string NextQuickPath(int sequence)
    {
        while (sequence < int.MaxValue)
        {
            string path = GetQuickPath(++sequence);
            if (!File.Exists(path))
            {
                return path;
            }
        }
        throw new InvalidOperationException("The quick save sequence is exhausted.");
    }

    private void EnsureCurrentWorldTarget(string path)
    {
        if (File.Exists(path) && !IsCurrentWorldSave(path))
        {
            throw new InvalidOperationException("This filename belongs to the other game's bank. Choose a different slot to preserve that save.");
        }
    }

    private bool IsCurrentWorldSave(string path)
    {
        try
        {
            CombinedSessionSave? saved = CrossoverSaveAdapter.ReadEnvelope(path);
            return Game == SessionGame.Ff7 ? saved?.ActiveGame == SessionGame.Ff7 : saved?.ActiveGame != SessionGame.Ff7;
        }
        catch (Exception error) when (IsUnavailable(error))
        {
            return Game != SessionGame.Ff7 || IsInDirectory(path, directory);
        }
    }
}
