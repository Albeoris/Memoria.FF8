using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum PlayerSaveMenuMode
{
    Save,
    Load,
    QuickLoad,
    QuickSave
}

internal sealed class PlayerSaveMenu
{
    private readonly PlayerSaveStore store;
    private readonly Dictionary<(PlayerSaveMenuMode Mode, int Bank), int> rememberedSlots = [];
    private readonly Dictionary<PlayerSaveMenuMode, int> rememberedBanks = [];
    private MenuCursor? transferSource;
    private bool confirmRequested;
    private bool cancelRequested;
    public const int EntriesPerPage = 5;

    public PlayerSaveMenu(PlayerSaveStore store)
    {
        this.store = store;
    }

    public bool IsOpen { get; private set; }
    public PlayerSaveMenuMode Mode { get; private set; }
    public int Selection { get; private set; }
    public int Bank { get; private set; }
    public int Page { get; private set; }
    public int BankSlotCount => PlayerSaveStore.GetBankSlotCount(Bank);
    public int PageCount => Math.Max(1, (Entries.Count + EntriesPerPage - 1) / EntriesPerPage);
    public int SelectedSlot => Entries.Count == 0 ? 0 : Entries[Selection].Slot;
    public bool IsQuick => Mode is PlayerSaveMenuMode.QuickSave or PlayerSaveMenuMode.QuickLoad;
    public bool IsSaving => Mode is PlayerSaveMenuMode.Save or PlayerSaveMenuMode.QuickSave;
    public bool CanTransfer => !IsQuick && !IsTransferring && Entries.Count > 0 && Entries[Selection].CanLoad;
    public IReadOnlyList<PlayerSaveEntry> VisibleEntries => Entries.Skip(Page * EntriesPerPage).Take(EntriesPerPage).ToArray();
    public bool ConfirmOverwrite { get; private set; }
    public bool IsTransferring => transferSource is not null;
    public int? TransferSource => transferSource?.Slot;
    public Func<int, bool>? BeforeBankChange { get; set; }
    public Func<bool>? RequiresMemoriaFormat { get; set; }
    public string? Message { get; private set; }
    public IReadOnlyList<PlayerSaveEntry> Entries { get; private set; } = [];
    public event Action<MenuSound>? SoundRequested;

    public static string GetBankLabel(int bank)
    {
        return bank switch
        {
            0 => "Memoria",
            1 => "Original I",
            2 => "Original II",
            _ => throw new ArgumentOutOfRangeException(nameof(bank))
        };
    }

    public void Open(PlayerSaveMenuMode mode)
    {
        if (IsOpen)
        {
            RememberCursor();
        }
        Mode = mode;
        transferSource = null;
        ConfirmOverwrite = false;
        Message = null;
        confirmRequested = false;
        cancelRequested = false;
        Bank = IsQuick ? 0 : rememberedBanks.GetValueOrDefault(mode);
        if (IsSaving && RequiresMemoriaFormat?.Invoke() == true)
        {
            Bank = 0;
            Message = MagicUiText.Get("MemoriaDestination");
        }
        Page = 0;
        RefreshEntries();
        if (IsQuick)
        {
            Selection = 0;
            Page = 0;
            Message = Mode == PlayerSaveMenuMode.QuickLoad && Entries.Count == 0 ? "No quick saves." : null;
        }
        else
        {
            SetSelectedSlot(rememberedSlots.GetValueOrDefault((mode, Bank), Entries.FirstOrDefault()?.Slot ?? PlayerSaveStore.GetFirstSlot(Bank)));
        }
        IsOpen = true;
    }

    public void Close()
    {
        if (IsOpen)
        {
            RememberCursor();
        }
        IsOpen = false;
        confirmRequested = false;
        cancelRequested = false;
        transferSource = null;
        ConfirmOverwrite = false;
    }

    public bool SelectBank(int bank)
    {
        if (!IsOpen || IsQuick || bank == Bank)
        {
            return false;
        }
        if (bank < 0 || bank >= PlayerSaveStore.BankCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bank));
        }
        if (IsSaving && !IsTransferring && bank != 0 && RequiresMemoriaFormat?.Invoke() == true)
        {
            Message = MagicUiText.Get("MemoriaDestination");
            SoundRequested?.Invoke(MenuSound.Cancel);
            return false;
        }
        if (IsTransferring && (bank == 0) == (PlayerSaveStore.GetBank(TransferSource!.Value) == 0))
        {
            Message = "Choose a bank of the other save type.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return false;
        }
        if (!ChangeBank(bank))
        {
            return false;
        }
        ConfirmOverwrite = false;
        Message = IsTransferring ? TransferPrompt() : null;
        SoundRequested?.Invoke(MenuSound.Move);
        return true;
    }

    public void SelectSlot(int slot)
    {
        if (!IsOpen || !Entries.Any(entry => entry.Slot == slot) || SelectedSlot == slot)
        {
            return;
        }
        SetSelectedSlot(slot);
        ConfirmOverwrite = false;
        Message = IsTransferring ? TransferPrompt() : null;
        SoundRequested?.Invoke(MenuSound.Move);
    }

    public void RequestConfirm()
    {
        confirmRequested = IsOpen;
    }

    public void RequestCancel()
    {
        cancelRequested = IsOpen;
    }

    public void RequestPage(int direction)
    {
        if (IsOpen)
        {
            Navigate(new InputFrame(0, 0, false, false, false, false, MenuPage: Math.Sign(direction)));
        }
    }

    public void RequestTransfer()
    {
        if (IsOpen && CanTransfer)
        {
            BeginTransfer();
        }
    }

    public void CancelTransfer()
    {
        if (IsOpen && IsTransferring)
        {
            RestoreTransferSource();
            Message = "Copy cancelled.";
            SoundRequested?.Invoke(MenuSound.Cancel);
        }
    }

    public void Update(InputFrame input, Action<int> save, Action<int> load, Action<int, int>? transfer = null)
    {
        if (!IsOpen)
        {
            return;
        }
        bool confirm = input.Confirm || confirmRequested;
        bool cancel = input.Cancel || cancelRequested;
        confirmRequested = false;
        cancelRequested = false;
        if (IsTransferring && input.Save && !input.QuickLoad)
        {
            Message = "Finish or cancel copying before quick saving.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return;
        }
        if (input.Save || input.QuickLoad)
        {
            ConfirmOverwrite = false;
            try
            {
                if (input.QuickLoad)
                {
                    Open(PlayerSaveMenuMode.QuickLoad);
                }
                else
                {
                    save(0);
                    Message = "Quick saved.";
                    int selected = SelectedSlot;
                    RefreshEntries();
                    SetSelectedSlot(selected);
                }
                SoundRequested?.Invoke(MenuSound.Confirm);
            }
            catch (Exception exception)
            {
                ReportFailure(input.QuickLoad ? "Quick load" : "Quick save", exception);
            }
            return;
        }
        if (cancel)
        {
            SoundRequested?.Invoke(MenuSound.Cancel);
            if (ConfirmOverwrite)
            {
                ConfirmOverwrite = false;
                Message = IsTransferring ? TransferPrompt() : null;
            }
            else if (IsTransferring)
            {
                RestoreTransferSource();
                Message = "Copy cancelled.";
            }
            else
            {
                Close();
            }
            return;
        }
        if (input.CardChallenge && !IsQuick && !IsTransferring)
        {
            BeginTransfer();
            return;
        }
        if (input.MenuHorizontal != 0 || input.MenuPage != 0 || input.MenuVertical != 0)
        {
            Navigate(input);
        }
        if (!confirm)
        {
            return;
        }
        if (Entries.Count == 0)
        {
            Message = "No quick saves.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return;
        }
        if (IsSaving && !IsTransferring && Bank != 0 && RequiresMemoriaFormat?.Invoke() == true)
        {
            ChangeBank(0);
            ConfirmOverwrite = false;
            Message = MagicUiText.Get("MemoriaDestination");
            return;
        }
        PlayerSaveEntry entry = Entries[Selection];
        if (!IsTransferring && !IsSaving && !entry.CanLoad)
        {
            RejectSource(entry);
            return;
        }
        try
        {
            if ((IsTransferring || IsSaving && !IsQuick) && File.Exists(store.GetPath(entry.Slot)) && !ConfirmOverwrite)
            {
                ConfirmOverwrite = true;
                Message = $"Overwrite {entry.Label}?";
                SoundRequested?.Invoke(MenuSound.Confirm);
                return;
            }
            if (IsTransferring)
            {
                int source = TransferSource!.Value;
                if (transfer is null)
                {
                    store.Transfer(source, entry.Slot);
                }
                else
                {
                    transfer(source, entry.Slot);
                }
                RestoreTransferSource();
                Message = $"Copied to {entry.Label}.";
            }
            else
            {
                if (IsSaving)
                {
                    save(entry.Slot);
                }
                else
                {
                    load(entry.Slot);
                }
                Close();
            }
            SoundRequested?.Invoke(MenuSound.Confirm);
        }
        catch (Exception exception)
        {
            ReportFailure(IsTransferring ? "Copy" : Mode.ToString(), exception);
            RefreshEntries();
        }
    }

    private void Navigate(InputFrame input)
    {
        if (!IsQuick && input.MenuHorizontal != 0)
        {
            ConfirmOverwrite = false;
            Message = IsTransferring ? TransferPrompt() : null;
            SelectBank(Math.Clamp(Bank + Math.Sign(input.MenuHorizontal), 0, PlayerSaveStore.BankCount - 1));
            return;
        }
        (int Page, int Selection) previous = (Page, Selection);
        if (input.MenuPage != 0)
        {
            int nextPage = Math.Clamp(Page + Math.Sign(input.MenuPage), 0, PageCount - 1);
            Selection = Math.Min(nextPage * EntriesPerPage + Selection % EntriesPerPage, Math.Max(0, Entries.Count - 1));
        }
        else
        {
            Selection = Math.Clamp(Selection + Math.Sign(input.MenuVertical), 0, Math.Max(0, Entries.Count - 1));
        }
        Page = Selection / EntriesPerPage;
        ConfirmOverwrite = false;
        Message = IsTransferring ? TransferPrompt() : null;
        if (previous != (Page, Selection))
        {
            SoundRequested?.Invoke(MenuSound.Move);
        }
    }

    private void BeginTransfer()
    {
        if (Entries.Count == 0)
        {
            return;
        }
        PlayerSaveEntry entry = Entries[Selection];
        if (!entry.CanLoad)
        {
            RejectSource(entry);
            return;
        }
        RememberCursor();
        transferSource = new MenuCursor(SelectedSlot, Bank, Page);
        if (!ChangeBank(Bank == 0 ? 1 : 0))
        {
            transferSource = null;
            return;
        }
        ConfirmOverwrite = false;
        Message = TransferPrompt();
        SoundRequested?.Invoke(MenuSound.Confirm);
    }

    private bool ChangeBank(int bank)
    {
        try
        {
            if (BeforeBankChange?.Invoke(bank) == false)
            {
                Message = "Bank selection cancelled.";
                SoundRequested?.Invoke(MenuSound.Cancel);
                return false;
            }
            int row = Selection % EntriesPerPage;
            int page = Math.Min(Page, (PlayerSaveStore.GetBankSlotCount(bank) - 1) / EntriesPerPage);
            int slot = rememberedSlots.GetValueOrDefault((Mode, bank), PlayerSaveStore.GetFirstSlot(bank) + page * EntriesPerPage + row);
            IReadOnlyList<PlayerSaveEntry> nextEntries = store.ReadEntries(bank, Mode == PlayerSaveMenuMode.Load && !IsTransferring);
            RememberCursor();
            Bank = bank;
            Entries = nextEntries;
            SetSelectedSlot(slot);
            return true;
        }
        catch (Exception exception)
        {
            ReportFailure("Bank selection", exception);
            return false;
        }
    }

    private string TransferPrompt()
    {
        return $"Copy {PlayerSaveStore.GetLabel(TransferSource!.Value)}: choose a destination.";
    }

    private void RestoreTransferSource()
    {
        MenuCursor source = transferSource!;
        transferSource = null;
        Bank = source.Bank;
        Page = source.Page;
        ConfirmOverwrite = false;
        RefreshEntries();
        SetSelectedSlot(source.Slot);
    }

    private void RejectSource(PlayerSaveEntry entry)
    {
        Message = entry.Details == "Empty" ? "This slot is empty." : entry.Details;
        SoundRequested?.Invoke(MenuSound.Cancel);
    }

    private void ReportFailure(string action, Exception exception)
    {
        ConfirmOverwrite = false;
        Message = $"{action} failed: {exception.Message}";
        Console.Error.WriteLine(exception);
        SoundRequested?.Invoke(MenuSound.Cancel);
    }

    private void RefreshEntries()
    {
        Entries = Mode switch
        {
            PlayerSaveMenuMode.QuickLoad => store.ReadQuickEntries(),
            PlayerSaveMenuMode.QuickSave => [new PlayerSaveEntry(0, "New Quick Save", null, "Create a rotating quick save.", false)],
            _ => store.ReadEntries(Bank, Mode == PlayerSaveMenuMode.Load && !IsTransferring)
        };
        Selection = Math.Clamp(Selection, 0, Math.Max(0, Entries.Count - 1));
        Page = Selection / EntriesPerPage;
    }

    private void SetSelectedSlot(int slot)
    {
        Selection = 0;
        for (int index = 0; index < Entries.Count; index++)
        {
            if (Entries[index].Slot == slot)
            {
                Selection = index;
                break;
            }
        }
        Page = Selection / EntriesPerPage;
    }

    private void RememberCursor()
    {
        if (!IsQuick && !IsTransferring)
        {
            rememberedSlots[(Mode, Bank)] = SelectedSlot;
            rememberedBanks[Mode] = Bank;
        }
    }

    private sealed record MenuCursor(int Slot, int Bank, int Page);
}
