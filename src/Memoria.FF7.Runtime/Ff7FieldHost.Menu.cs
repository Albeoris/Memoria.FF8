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
        if (command.Mode is not (6 or 8 or 17))
        {
            throw new NotSupportedException($"FFVII menu mode {command.Mode} requires its native menu process.");
        }
        if (command.Mode == 8 && command.Parameter is < 0 or >= 80)
        {
            throw new NotSupportedException("The native shop index is outside the recognized executable directory.");
        }
        Process = Process with { Menu = new(requestId, command.Mode, command.Parameter, Shop: command.Mode == 8 ? new(requestId, command.Parameter) : null), GlobalCommand = Ff7GlobalCommand.Menu, GlobalRequestId = requestId };
        return new(false);
    }

    public void AdvanceNativeMenu()
    {
        if (Process.Menu is not { Mode: 17, Completed: false } menu || Process.GlobalCommand != Ff7GlobalCommand.Menu || Process.GlobalRequestId != menu.RequestId)
        {
            return;
        }
        FieldHostReply reply = ExternalCommand?.Invoke(new FieldMenuCommand(menu.Mode, menu.Parameter)) ?? throw new NotSupportedException("FFVII equipment removal requires its native progress owner.");
        if (!reply.Completed)
        {
            throw new NotSupportedException("FFVII equipment removal must complete within its native global process.");
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
