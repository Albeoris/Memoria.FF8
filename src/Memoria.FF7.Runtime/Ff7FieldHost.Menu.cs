using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply Menu(long requestId, FieldMenuCommand command)
    {
        if (Process.Menu is { } existing && existing.RequestId == requestId)
        {
            if (existing.Completed)
            {
                Process = Process with { GlobalCommand = Ff7GlobalCommand.None, GlobalRequestId = null };
                return new(true);
            }
            return new(false);
        }
        if (Process.GlobalCommand != Ff7GlobalCommand.None)
        {
            return new(false);
        }
        if (command.Mode is not (6 or 7 or 8 or 17 or 21))
        {
            throw new NotSupportedException($"FFVII menu mode {command.Mode} requires its native menu process.");
        }
        if (command.Mode == 8 && command.Parameter is < 0 or >= 80)
        {
            throw new NotSupportedException("The native shop index is outside the recognized executable directory.");
        }
        if (command.Mode == 7 && command.Parameter != 0)
        {
            throw new NotSupportedException($"FFVII party selector parameter {command.Parameter} requires its native roster contract.");
        }
        Process = Process with { Menu = new(requestId, command.Mode, command.Parameter, Shop: command.Mode == 8 ? new(requestId, command.Parameter) : null), GlobalCommand = Ff7GlobalCommand.Menu, GlobalRequestId = requestId };
        return new(false);
    }

    public void AdvanceNativeMenu()
    {
        if (Process.Menu is not { Mode: 17 or 21, Completed: false } menu || Process.GlobalCommand != Ff7GlobalCommand.Menu || Process.GlobalRequestId != menu.RequestId)
        {
            return;
        }
        FieldHostReply reply = ExternalCommand?.Invoke(new FieldMenuCommand(menu.Mode, menu.Parameter)) ?? throw new NotSupportedException("FFVII automatic menu operation requires its native progress owner.");
        if (!reply.Completed)
        {
            throw new NotSupportedException("FFVII automatic menu operation must complete within its native global process.");
        }
        CompleteMenu(menu.RequestId);
    }

    public void CompleteMenu(long requestId)
    {
        Ff7Menu menu = Process.Menu ?? throw new InvalidOperationException("No FFVII menu is active.");
        if (menu.RequestId != requestId || Process.GlobalCommand != Ff7GlobalCommand.Menu || Process.GlobalRequestId != requestId)
        {
            throw new InvalidOperationException("Only the current FFVII menu owner can complete it.");
        }
        if (menu.Mode == 8 && menu.Shop?.Phase != Ff7ShopPhase.Completed)
        {
            throw new InvalidOperationException("The native shop must finish its closing process before the field menu can complete.");
        }
        Process = Process with { Menu = menu with { Completed = true } };
    }
}

public sealed partial class Ff7Session
{
    public bool CompleteStoryPartyMenu(long requestId, IReadOnlyList<int> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (Host.Process.Menu is not { Mode: 7, Parameter: 0, Completed: false } menu || menu.RequestId != requestId || Host.Process.GlobalCommand != Ff7GlobalCommand.Menu || Host.Process.GlobalRequestId != requestId)
        {
            return false;
        }
        if (members.Count != 3 || members.Distinct().Count() != 3 || members.Any(id => (uint)id > 8))
        {
            return false;
        }
        int selected = members.Aggregate(0, (mask, id) => mask | 1 << id);
        int active = Progress.Party.Where(id => id != 255).Aggregate(0, (mask, id) => mask | 1 << id);
        if ((selected & ~Progress.AvailableCharacters) != 0 || (selected & Progress.LockedCharacters & ~active) != 0 || Progress.Party.Where((id, index) => id != 255 && (Progress.LockedCharacters & 1 << id) != 0 && members[index] != id).Any())
        {
            return false;
        }
        Ff7Progress next = Progress with { Party = members.ToArray() };
        next.Validate();
        Host.CompleteMenu(requestId);
        PublishFieldParty(next);
        return true;
    }

    public void CompleteNamingMenu(long requestId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Ff7Menu menu = Host.Process.Menu ?? throw new InvalidOperationException("No FFVII naming menu is active.");
        if (menu.Mode != 6 || menu.RequestId != requestId || menu.Completed)
        {
            throw new InvalidOperationException("The requested FFVII naming menu does not own this continuation.");
        }
        if (name.Length is < 1 or > 9 || name.Any(character => character is < ' ' or > '~'))
        {
            throw new ArgumentException("FFVII names require one through nine supported font characters.", nameof(name));
        }
        Ff7CharacterState character = Progress.Characters.Single(character => character.Id == menu.Parameter);
        byte[] record = (byte[])character.NativeRecord.Clone();
        record.AsSpan(16, 12).Fill(255);
        for (int index = 0; index < name.Length; index++)
        {
            record[16 + index] = (byte)(name[index] - 32);
        }
        Ff7Progress updated = Progress with { Characters = Progress.Characters.Select(current => current.Id == character.Id ? current with { Name = name, NativeRecord = record } : current).ToArray() };
        updated.Validate();
        Host.CompleteMenu(requestId);
        Progress = updated;
    }
}
