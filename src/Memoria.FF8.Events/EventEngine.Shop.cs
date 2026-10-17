using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<NativeShopDefinitions>? ResolveNativeShopDefinitions { get; set; }

    private string? UnsupportedNativeShopRequest(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || partyMenu is not null || services.IsMoviePlaying || !services.CanStoreNativePartyMenuOwners || services.CharacterVitals is null || services.Economy is null || services.ShotAvailability is null || services.SalaryPresentation is null || services.ExamTimer is null || services.VibrationProcess is null || ResolveNativeShopDefinitions is null)
        {
            return "Native shop requires its actual menu, saved inventory/character/history, menu RNG and process owners before request mutation.";
        }
        if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "MENUSHOP requires its one exact DWORD operand before native BYTE narrowing.";
        }
        try
        {
            ValidateNativeFieldRequestPacketStorage();
            int raw = fiber.Stack.Peek();
            NativeShopDefinitions definitions = ResolveNativeShopDefinitions();
            definitions.Validate();
            if (definitions.ArgumentMap[unchecked((byte)raw)] == 21)
            {
                _ = PrepareNativeWeaponShopEntry(raw);
                return null;
            }
            if (services.ShopHistory is null || services.MenuRandom is null)
            {
                return "Ordinary shop requires its actual saved discovery history and menu RNG.";
            }
            byte catalog = definitions.ResolveCatalog(raw);
            uint abilities = services.CharacterVitals.ShopAbilityMask();
            NativeEconomy normalized = NativeShopTransaction.NormalizeInventory(services.Economy);
            NativeShopHistory history = services.ShopHistory.Refresh(definitions, catalog, abilities);
            _ = NativeShopTransaction.Begin(raw, definitions, normalized, services.CharacterVitals, history);
            services.MenuRandom.Validate();
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private int[] RequestNativeShop(Fiber fiber)
    {
        int raw = fiber.Stack.Peek();
        NativeShopDefinitions definitions = ResolveNativeShopDefinitions!();
        NativeFieldMenuKind kind = definitions.ArgumentMap[unchecked((byte)raw)] == 21 ? NativeFieldMenuKind.WeaponShop : NativeFieldMenuKind.Shop;
        NativePartyMenuState request = new(FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, NativePartyMenuPhase.Requested, 0, null, 0, 0, 0, null, kind, unchecked((byte)raw), 0, raw, null, null, null, null);
        PublishNativeScriptMenuRequest(23);
        int[] operands = Pop(fiber, 1);
        partyMenu = request;
        fiber.WaitReason = WaitReason.NativePartyMenu;
        return operands;
    }

    private (NativeEconomy Economy, NativeShopTransaction Shop, NativeShopHistory History, NativeMenuRandomState MenuRandom) PrepareNativeShopEntry(NativePartyMenuState current)
    {
        NativeShopDefinitions definitions = ResolveNativeShopDefinitions?.Invoke() ?? throw new NotSupportedException("Native shop definitions are unavailable.");
        int raw = current.ShopRawArgument ?? throw new InvalidDataException("Native shop request lost its original DWORD.");
        byte catalog = definitions.ResolveCatalog(raw);
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("Native shop GF/menu ability inputs are unknown.");
        uint abilities = vitals.ShopAbilityMask();
        NativeEconomy economy = NativeShopTransaction.NormalizeInventory(services.Economy ?? throw new NotSupportedException("Native shop inventory is unknown."));
        NativeShopHistory history = (services.ShopHistory ?? throw new NotSupportedException("Native saved shop history is unknown.")).Refresh(definitions, catalog, abilities);
        NativeShopTransaction shop = NativeShopTransaction.Begin(raw, definitions, economy, vitals, history);
        int calls = 2 * definitions.Catalogs[catalog].Count(entry => entry.ItemId != 0);
        NativeMenuRandomState random = (services.MenuRandom ?? throw new NotSupportedException("Native shop refresh has no current menu RNG owner.")).Advance(calls);
        return (economy, shop, history, random);
    }

    private bool BuyNativeShopItem(int itemId, int quantity)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.Shop, Shop: not null } current)
        {
            return false;
        }
        NativeShopTransaction next = current.Shop.Buy(itemId, quantity, out bool accepted);
        if (!accepted)
        {
            return false;
        }
        //4EC7C2/4EC859 change only window-local gil/counts. The saved bank
        //remains unchanged until4ECFE7 invokes4EB9F0 on the explicit exit.
        NativePartyMenuState nextMenu = current with { Shop = next, Callbacks = checked(current.Callbacks + 1) };
        PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        partyMenu = nextMenu;
        return true;
    }

    private bool SellNativeShopItem(int itemId, int quantity)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.Shop, Shop: not null } current)
        {
            return false;
        }
        NativeShopTransaction next = current.Shop.Sell(itemId, quantity, out bool accepted);
        if (!accepted)
        {
            return false;
        }
        NativePartyMenuState nextMenu = current with { Shop = next, Callbacks = checked(current.Callbacks + 1) };
        PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        partyMenu = nextMenu;
        return true;
    }

    private bool CloseNativeShop()
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.Shop, Shop: not null } current)
        {
            return false;
        }
        if (current.Shop.Past != services.CharacterVitals!.DreamEnabled)
        {
            throw new NotSupportedException("Native shop cannot commit after its current saved dream/gil selector changed.");
        }
        NativeEconomy economy = current.Shop.Commit(services.Economy!);
        NativePartyMenuState nextMenu = current with { Phase = NativePartyMenuPhase.Closing, Callbacks = checked(current.Callbacks + 1) };
        PublishPartyMenuCallback(services.CharacterVitals!, economy, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        partyMenu = nextMenu;
        return true;
    }

    private void ValidateNativeShopCheckpoint(Checkpoint saved, NativePartyMenuState menu)
    {
        if (menu.ShopRawArgument is not int raw || menu.NativeEntrySelection != unchecked((byte)raw) || saved.ShopHistory is null || saved.MenuRandom is null || saved.Economy is null || saved.CharacterVitals is null)
        {
            throw new InvalidDataException("Saved shop lost its original request BYTE/DWORD or actual saved/process owners.");
        }
        if (menu.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering)
        {
            if (menu.Shop is not null)
            {
                throw new InvalidDataException("Shop-local inventory appeared before its native constructor executed.");
            }
            return;
        }
        NativeShopTransaction shop = menu.Shop ?? throw new InvalidDataException("Active saved shop lacks its actual local inventory/gil.");
        shop.Validate();
        if (shop.RawArgument != raw || shop.InitOutSha256 != saved.Economy.InitOutSha256 || shop.Definitions.KernelHash != saved.CharacterVitals.KernelSha256 || shop.Past != saved.CharacterVitals.DreamEnabled || shop.AbilityMask != saved.CharacterVitals.ShopAbilityMask())
        {
            throw new InvalidDataException("Saved shop changed its original initialization, catalog or GF/gil selector.");
        }
        byte[] available = shop.Definitions.Catalogs[shop.Catalog].Select((entry, slot) => (entry, slot)).Where(item => item.entry.ItemId != 0 && saved.ShopHistory.Bytes[20 * shop.Catalog + item.slot] != 0).Select(item => item.entry.ItemId).ToArray();
        if (!shop.AvailableItems.SequenceEqual(available))
        {
            throw new InvalidDataException("Saved shop availability disagrees with the single saved discovery row.");
        }
    }
}
