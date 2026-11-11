using System.Buffers.Binary;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>Read-only FFVII party/inventory projection for the existing shared menu.</summary>
internal static class Ff7PlayerMenuProjection
{
    internal static MenuData Create(Ff7Progress progress, IReadOnlyList<int> party, Ff7CharacterPresentationIdentity identities, IReadOnlyDictionary<int, string> itemNames, IReadOnlyDictionary<int, string> itemDescriptions, string location, TimeSpan elapsed, bool canSave, Func<Ff7CharacterDisplayIdentity, UiImageSource> portrait, IReadOnlyList<Ff7GuestFieldVitalsView>? guests = null)
    {
        MenuData data = new() { Location = location, Currency = checked((int)progress.Gil), PlayTime = elapsed, SaveAvailable = canSave, ShowSeeDRank = false };
        data.SupportedActions.UnionWith(["config", "ff7-save", "ff7-load", "ff7-world", "ff7-close", "ff7-materia-character", "ff7-materia-slot", "ff7-materia-equip", "ff7-character-menu-select", "ff7-order-apply", "ff7-phs-apply"]);
        int[] partyOrder = party.Where(character => character != 255).Distinct().ToArray();
        IEnumerable<Ff7CharacterState> ordered = partyOrder.Select(id => progress.Characters.Single(character => character.Id == id)).Concat(progress.Characters.Where(character => !partyOrder.Contains(character.Id) && (progress.AvailableCharacters & (1 << character.Id)) != 0));
        foreach (Ff7CharacterState character in ordered)
        {
            Ff7CharacterDisplayIdentity identity = identities.Resolve(character.Id, character.Name);
            MenuCharacter display = new() { Id = "ff7-character/" + character.Id, Name = identity.Name, InParty = party.Contains(character.Id), Level = character.Level, CurrentHp = character.HitPoints, HpDisplay = character.HitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture), Experience = checked((int)character.Experience), Portrait = portrait(identity) };
            display.Stats["MP"] = character.MagicPoints;
            ushort maximumHp = BinaryPrimitives.ReadUInt16LittleEndian(character.NativeRecord.AsSpan(56));
            display.MaximumHp = maximumHp is 0 or ushort.MaxValue ? null : maximumHp;
            if (guests?.SingleOrDefault(guest => guest.StoryCharacter == character.Id) is { } guest)
            {
                display.Name = guest.Name;
                display.Level = guest.Level;
                display.CurrentHp = guest.Hp;
                display.HpDisplay = guest.Hp.ToString(System.Globalization.CultureInfo.InvariantCulture);
                display.MaximumHp = guest.MaximumHp;
                display.Stats["MP"] = 0;
            }
            data.Characters.Add(display);
        }
        foreach (Ff7InventorySlot item in progress.Inventory.Where(item => item.Quantity > 0))
        {
            if (!itemNames.TryGetValue(item.ItemId, out string? name) || string.IsNullOrWhiteSpace(name) || !itemDescriptions.TryGetValue(item.ItemId, out string? description))
            {
                throw new NotSupportedException("The saved FFVII item requires its original menu name and description entries.");
            }
            data.Items.Add(new() { Id = "ff7-item/" + item.ItemId, Name = name, Quantity = item.Quantity, Description = description });
        }
        return data;
    }
}
