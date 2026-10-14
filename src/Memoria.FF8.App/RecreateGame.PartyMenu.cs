using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private int partyMenuCandidate;

    private void UpdatePartyMenu(TimeSpan elapsed, InputFrame input)
    {
        if (events!.PartyMenu!.Phase == NativePartyMenuPhase.Active)
        {
            IReadOnlyList<byte> candidates = events.PartyMenuCandidates;
            if (candidates.Count > 0)
            {
                partyMenuCandidate = Math.Clamp(partyMenuCandidate + input.MenuVertical, 0, candidates.Count - 1);
            }
            if (input.Confirm)
            {
                bool full = events.PartyMenu.TemporaryCombatParty!.All(row => row != 255);
                world!.Apply(full ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.SelectPartyMember, candidates.Count > 0 ? candidates[partyMenuCandidate] : -1));
                fieldSeconds = 0;
                return;
            }
        }
        fieldSeconds += options.ControlStep ? 0 : elapsed.TotalSeconds;
        while (fieldSeconds + 1e-9 >= 1d / EventEngine.SimulationFps && events.PartyMenu is not null)
        {
            fieldSeconds -= 1d / EventEngine.SimulationFps;
            world!.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        if (events.PartyMenu is null)
        {
            fieldSeconds = 0;
            partyMenuCandidate = 0;
        }
    }

    private void DrawPartyMenu(Rectangle bounds)
    {
        // TODO(TD-011): original menu raster/cursor presentation remains unsupported
        float scale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        batch.Draw(pixel, bounds, Color.Black * 0.8f);
        Rectangle panel = new((int)(24 * scale), (int)(24 * scale), bounds.Width - (int)(48 * scale), bounds.Height - (int)(48 * scale));
        DrawPanel(panel);
        NativePartyMenuState menu = events!.PartyMenu!;
        string Name(byte row) => row switch
        {
            0 => events.Scene.Names.GetValueOrDefault(0, "Squall"),
            1 => "Zell",
            2 => "Irvine",
            3 => "Quistis",
            4 => events.Scene.Names.GetValueOrDefault(1, "Rinoa"),
            5 => "Selphie",
            6 => "Seifer",
            7 => "Edea",
            _ => throw new NotSupportedException("Party menu labels require native normal character identities.")
        };
        string party = menu.TemporaryCombatParty is null ? "Opening party menu" : string.Join(" / ", menu.TemporaryCombatParty.Select(row => row == 255 ? "Empty" : Name(row)));
        text.Draw(batch, "Party", 22 * scale, new Vector2(panel.X + 16 * scale, panel.Y + 12 * scale), Color.White);
        text.Draw(batch, party, 16 * scale, new Vector2(panel.X + 16 * scale, panel.Y + 46 * scale), Color.White);
        IReadOnlyList<byte> candidates = events.PartyMenuCandidates;
        for (int index = 0; index < candidates.Count; index++)
        {
            text.Draw(batch, (index == partyMenuCandidate ? "> " : "  ") + Name(candidates[index]), 16 * scale, new Vector2(panel.X + 16 * scale, panel.Y + (82 + index * 24) * scale), index == partyMenuCandidate ? Color.White : Color.Gray);
        }
        text.Draw(batch, menu.Phase == NativePartyMenuPhase.FieldReentry ? "Native field reentry is unsupported" : menu.TemporaryCombatParty?.All(row => row != 255) == true ? "Enter: confirm party" : "Arrows: select   Enter: add member", 14 * scale, new Vector2(panel.X + 16 * scale, panel.Bottom - 32 * scale), Color.White);
    }
}
