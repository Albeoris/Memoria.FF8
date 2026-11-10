using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldFieldSelection? SelectVehicleCockpit(WorldState state, ref NativeWorldSalaryState salary)
    {
        if (state.NativeFootRuntime is not null || state.NativeGardenRuntime is null && state.NativeRagnarokRuntime is null || salary.EventUi?.ScriptMode != 0 || NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) > 0)
        {
            return null;
        }
        NativeWorldInputHistory history = salary.InputHistory ?? throw new NotSupportedException("Vehicle cockpit input requires its sampled button history.");
        if ((history.CurrentButtons & 0x20) == 0 || history.PreviousButtons is not ushort previous || (previous & 0x20) != 0)
        {
            return null;
        }
        // 53FAC0 dispatches 544630 after late vehicle transfers on a rising menu edge.
        bool garden = state.NativeGardenRuntime is not null;
        bool landingPermitted = garden ? ResolveGardenFootPlacement(state)?.Accepted == true : PrepareRagnarokLanding(state) is not null;
        salary = salary.WithVehicleCockpitLanding(landingPermitted);
        return new WorldFieldSelection(-1, -1, -1, assets.FieldEntries.Entries[garden ? 65 : 44], []);
    }
}
