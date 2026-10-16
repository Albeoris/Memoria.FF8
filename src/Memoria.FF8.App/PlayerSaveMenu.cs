using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum PlayerSaveMenuMode
{
    Save,
    Load
}

internal sealed class PlayerSaveMenu
{
    private readonly PlayerSaveStore store;
    private readonly Dictionary<PlayerSaveMenuMode, MenuCursor> remembered = [];
    public const int EntriesPerPage = 5;
    public const int PageCount = PlayerSaveStore.SlotsPerBank / EntriesPerPage;

    public PlayerSaveMenu(PlayerSaveStore store)
    {
        this.store = store;
    }

    public bool IsOpen { get; private set; }
    public PlayerSaveMenuMode Mode { get; private set; }
    public int Selection { get; private set; }
    public int Bank { get; private set; }
    public int Page { get; private set; }
    public int SelectedSlot => Entries[Selection].Slot;
    public PlayerSaveEntry? QuickEntry => Mode == PlayerSaveMenuMode.Load ? Entries[0] : null;
    public IReadOnlyList<PlayerSaveEntry> VisibleEntries => Entries.Where(entry => entry.Slot > Bank * PlayerSaveStore.SlotsPerBank && entry.Slot <= (Bank + 1) * PlayerSaveStore.SlotsPerBank).Skip(Page * EntriesPerPage).Take(EntriesPerPage).ToArray();
    public bool ConfirmOverwrite { get; private set; }
    public string? Message { get; private set; }
    public IReadOnlyList<PlayerSaveEntry> Entries { get; private set; } = [];
    public event Action<MenuSound>? SoundRequested;

    public void Open(PlayerSaveMenuMode mode)
    {
        if (IsOpen)
        {
            RememberCursor();
        }
        Mode = mode;
        ConfirmOverwrite = false;
        Message = null;
        Entries = store.ReadEntries(mode == PlayerSaveMenuMode.Load);
        MenuCursor cursor = remembered.GetValueOrDefault(mode, new MenuCursor(mode == PlayerSaveMenuMode.Load ? 0 : 1, 0, 0));
        Bank = cursor.Bank;
        Page = cursor.Page;
        SelectSlot(cursor.Slot);
        IsOpen = true;
    }

    public void Close()
    {
        if (IsOpen)
        {
            RememberCursor();
        }
        IsOpen = false;
        ConfirmOverwrite = false;
    }

    public void Update(InputFrame input, Action<int> save, Action<int> load)
    {
        if (!IsOpen)
        {
            return;
        }
        if (input.Save || input.QuickLoad)
        {
            ConfirmOverwrite = false;
            try
            {
                if (input.QuickLoad)
                {
                    load(0);
                    Close();
                }
                else
                {
                    save(0);
                    Message = "Quick saved.";
                    Entries = store.ReadEntries(Mode == PlayerSaveMenuMode.Load);
                }
                SoundRequested?.Invoke(MenuSound.Confirm);
            }
            catch (Exception exception)
            {
                Message = $"{(input.QuickLoad ? "Quick load" : "Quick save")} failed: {exception.Message}";
                Console.Error.WriteLine(exception);
                SoundRequested?.Invoke(MenuSound.Cancel);
            }
            return;
        }
        if (input.Cancel)
        {
            SoundRequested?.Invoke(MenuSound.Cancel);
            if (ConfirmOverwrite)
            {
                ConfirmOverwrite = false;
                Message = null;
            }
            else
            {
                Close();
            }
            return;
        }
        if (input.MenuHorizontal != 0 || input.MenuPage != 0 || input.MenuVertical != 0)
        {
            (int Bank, int Page, int Selection) previous = (Bank, Page, Selection);
            if (input.MenuHorizontal != 0)
            {
                int nextBank = Math.Clamp(Bank + Math.Sign(input.MenuHorizontal), 0, PlayerSaveStore.BankCount - 1);
                int slot = SelectedSlot == 0 ? 0 : nextBank * PlayerSaveStore.SlotsPerBank + (SelectedSlot - 1) % PlayerSaveStore.SlotsPerBank + 1;
                Bank = nextBank;
                SelectSlot(slot);
            }
            else if (input.MenuPage != 0)
            {
                int nextPage = Math.Clamp(Page + Math.Sign(input.MenuPage), 0, PageCount - 1);
                int row = SelectedSlot == 0 ? 0 : (SelectedSlot - 1) % EntriesPerPage;
                SelectSlot(Bank * PlayerSaveStore.SlotsPerBank + nextPage * EntriesPerPage + row + 1);
            }
            else
            {
                int first = Bank * PlayerSaveStore.SlotsPerBank + 1;
                int last = first + PlayerSaveStore.SlotsPerBank - 1;
                int target = SelectedSlot == 0 ? first : SelectedSlot + Math.Sign(input.MenuVertical);
                if (Mode == PlayerSaveMenuMode.Load && input.MenuVertical < 0 && SelectedSlot == first)
                {
                    SelectSlot(0);
                }
                else if (SelectedSlot != 0 || input.MenuVertical > 0)
                {
                    SelectSlot(Math.Clamp(target, first, last));
                }
            }
            ConfirmOverwrite = false;
            Message = null;
            if (previous != (Bank, Page, Selection))
            {
                SoundRequested?.Invoke(MenuSound.Move);
            }
        }
        if (!input.Confirm)
        {
            return;
        }
        PlayerSaveEntry entry = Entries[Selection];
        if (Mode == PlayerSaveMenuMode.Load && !entry.CanLoad)
        {
            Message = entry.Details == "Empty" ? "This slot is empty." : entry.Details;
            SoundRequested?.Invoke(MenuSound.Cancel);
            return;
        }
        try
        {
            if (Mode == PlayerSaveMenuMode.Save && File.Exists(store.GetPath(entry.Slot)) && !ConfirmOverwrite)
            {
                ConfirmOverwrite = true;
                Message = $"Overwrite {entry.Label}?";
                SoundRequested?.Invoke(MenuSound.Confirm);
                return;
            }
            if (Mode == PlayerSaveMenuMode.Save)
            {
                save(entry.Slot);
            }
            else
            {
                load(entry.Slot);
            }
            SoundRequested?.Invoke(MenuSound.Confirm);
            Close();
        }
        catch (Exception exception)
        {
            ConfirmOverwrite = false;
            Message = $"{Mode} failed: {exception.Message}";
            Console.Error.WriteLine(exception);
            Entries = store.ReadEntries(Mode == PlayerSaveMenuMode.Load);
            SoundRequested?.Invoke(MenuSound.Cancel);
        }
    }

    private void SelectSlot(int slot)
    {
        Selection = slot == 0 ? 0 : Mode == PlayerSaveMenuMode.Load ? slot : slot - 1;
        if (slot != 0)
        {
            Bank = (slot - 1) / PlayerSaveStore.SlotsPerBank;
            Page = (slot - 1) % PlayerSaveStore.SlotsPerBank / EntriesPerPage;
        }
    }

    private void RememberCursor()
    {
        remembered[Mode] = new MenuCursor(SelectedSlot, Bank, Page);
    }

    private sealed record MenuCursor(int Slot, int Bank, int Page);
}
