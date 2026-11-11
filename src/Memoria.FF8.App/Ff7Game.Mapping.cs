using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Game
{
    private CrossoverMappingMenu? mappingMenu;

    private void OpenWorldSwitchMapping(CombinedSessionSave saved)
    {
        if (mappingMenu is not null)
        {
            return;
        }
        if (!scene.CanSave)
        {
            scene.ShowNotice("Available only where saving is allowed.");
            return;
        }
        CrossoverMappingMenu menu = scene.CreateMappingMenu(launchOptions!.GameDirectory, ReadControlKeys(), Mouse.GetState());
        menu.Confirmed += (mappings, _) =>
        {
            try
            {
                RequestCrossover(saved with { ActiveGame = SessionGame.Ff8, Mappings = mappings });
                CloseWorldSwitchMapping();
            }
            catch (Exception failure) when (failure is IOException or InvalidDataException or InvalidOperationException or NotSupportedException)
            {
                menu.SetNotice(failure.Message);
            }
        };
        menu.Cancelled += CloseWorldSwitchMapping;
        mappingMenu = menu;
        scene.SetFocusPaused(true);
        keyboard.SuppressHeldUiInput();
    }

    private void CloseWorldSwitchMapping()
    {
        mappingMenu?.Dispose();
        mappingMenu = null;
        scene.SuppressInitialPresses(ReadControlKeys());
        scene.SetFocusPaused(!IsActive);
    }
}
