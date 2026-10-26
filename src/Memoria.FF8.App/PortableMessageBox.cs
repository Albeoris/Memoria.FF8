using System.Runtime.InteropServices;
using SDL3;

namespace Memoria.FF8;

internal static class PortableMessageBox
{
    internal static bool Confirm(string message, string title)
    {
        SDL.MessageBoxButtonData cancel = new() { ButtonID = 0, Text = "Cancel", Flags = SDL.MessageBoxButtonFlags.EscapekeyDefault };
        SDL.MessageBoxButtonData accept = new() { ButtonID = 1, Text = "Continue", Flags = 0 };
        int size = Marshal.SizeOf<SDL.MessageBoxButtonData>();
        nint buttons = Marshal.AllocHGlobal(size * 2);
        try
        {
            Marshal.StructureToPtr(cancel, buttons, false);
            Marshal.StructureToPtr(accept, buttons + size, false);
            SDL.MessageBoxData data = new() { Title = title, Message = message, Flags = SDL.MessageBoxFlags.Warning, NumButtons = 2, Buttons = buttons };
            if (!SDL.ShowMessageBox(in data, out int selected))
            {
                throw new IOException($"Cannot show the confirmation dialog: {SDL.GetError()}");
            }
            return selected == 1;
        }
        finally
        {
            Marshal.DestroyStructure<SDL.MessageBoxButtonData>(buttons);
            Marshal.DestroyStructure<SDL.MessageBoxButtonData>(buttons + size);
            Marshal.FreeHGlobal(buttons);
        }
    }
}
