using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeShopTests
{
    public static void Run()
    {
        MenuOwnershipAndTiming();
        NativeModuleSounds();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original shop resources and transactions: configure original resources.");
            return;
        }
        Ff7ShopResources resources = Ff7ShopResources.Load(Ff7Installation.Discover(root));
        Ff7ShopDefinition shop = resources.Get(8);
        Check(shop.Stock.Select(item => item.NativeId).SequenceEqual(new[] { 0, 7, 8, 12, 52, 13, 14, 1, 70 }), "The original shop item IDs or ordering changed.");
        Check(shop.Stock.Select(item => item.Price).SequenceEqual(new uint[] { 50, 300, 80, 100, 50, 100, 100, 300, 500 }) && shop.Stock.All(item => item.CanSell), "The original shop item prices or native sale restrictions changed.");
        Check(shop.Stock[0].Name == "Potion" && resources.Get(7).Stock.Single(item => item.NativeId == 53).Name == "Restore", "Shop labels must use original item/materia namespaces.");
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [new(0, 98)], Materia = [], Gil = 1000 };
        Ff7ShopState state = new(123, 8, Ff7ShopPhase.Interactive, 0, 0);
        Check(Ff7NativeShopTransactions.MaximumQuantity(progress, resources, 8, Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Item, 0) == 1, "The native carried-item capacity must limit purchases to99.");
        Ff7ShopTransaction buy = new(1, Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Item, 0, 1);
        Ff7ShopTransactionResult bought = Ff7NativeShopTransactions.Apply(progress, state, resources, buy);
        Check(bought.Receipt.Applied && bought.Progress.Gil == 950 && bought.Progress.Inventory.Single().Quantity == 99 && progress.Gil == 1000 && progress.Inventory.Single().Quantity == 98, "Native buying must atomically charge exact Gil and add original inventory without mutating its source.");
        Ff7ShopState coldState = JsonSerializer.Deserialize<Ff7ShopState>(JsonSerializer.Serialize(bought.State))!;
        Ff7Progress coldProgress = JsonSerializer.Deserialize<Ff7Progress>(JsonSerializer.Serialize(bought.Progress))!;
        Ff7ShopTransactionResult repeated = Ff7NativeShopTransactions.Apply(coldProgress, coldState, resources, buy);
        Check(JsonSerializer.Serialize(repeated.Progress) == JsonSerializer.Serialize(bought.Progress) && repeated.Receipt == bought.Receipt, "A cold transaction retry charged or awarded twice.");
        Throws<InvalidDataException>(() => Ff7NativeShopTransactions.Apply(bought.Progress, bought.State, resources, buy with { Quantity = 2 }));
        Ff7ShopTransactionResult rejected = Ff7NativeShopTransactions.Apply(bought.Progress, bought.State, resources, buy with { Id = 2 });
        Check(!rejected.Receipt.Applied && rejected.Progress == bought.Progress, "A native capacity rejection mutated progress.");
        Ff7ShopTransactionResult sold = Ff7NativeShopTransactions.Apply(rejected.Progress, rejected.State, resources, new(3, Ff7ShopTransactionKind.Sell, Ff7ShopStockKind.Item, 0, 99));
        Check(sold.Receipt.Applied && sold.Progress.Gil == 3425 && sold.Progress.Inventory.Length == 0, "Native item selling must halve unit price before multiplying and clear an exhausted stack.");
        Ff7Progress poor = progress with { Gil = 49, Inventory = [] };
        Check(!Ff7NativeShopTransactions.Apply(poor, state, resources, buy).Receipt.Applied, "An unaffordable purchase was accepted.");
        Ff7Progress rich = progress with { Gil = uint.MaxValue - 1 };
        Check(Ff7NativeShopTransactions.Apply(rich, state, resources, new(1, Ff7ShopTransactionKind.Sell, Ff7ShopStockKind.Item, 0, 1)).Progress.Gil == uint.MaxValue, "Native normal-item sale Gil must saturate on unsigned overflow.");
        MateriaTransactions(progress, resources);
        Throws<NotSupportedException>(() => Ff7NativeShopTransactions.Apply(progress, state, resources, buy with { NativeId = 128 }));
        Throws<InvalidDataException>(() => (state with { Alpha = 1 }).Validate());
        Throws<InvalidDataException>(() => (bought.State with { LastTransaction = bought.Receipt with { Transaction = buy with { Quantity = 0 } } }).Validate());
        Console.WriteLine("PASS original shop resources, item constraints, typed atomic transactions and cold receipts");
    }

    private static void MenuOwnershipAndTiming()
    {
        Ff7FieldHost host = Host(new());
        FieldProgram program = new([0, 0x49, 0, 8, 8, 0x80, 0x50, 3, 99, 0x12, 0], [new ushort[32]]);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        Check(machine.Capture().Entities[0].ProgramCounter == 1 && host.Process.Menu is { Mode: 8, Completed: false, Shop.Phase: Ff7ShopPhase.Opening }, "The native field menu instruction must retain its waiting global request.");
        for (int callback = 0; callback < 17; callback++)
        {
            host.AdvanceNativeShop();
        }
        Ff7FieldHost coldHost = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        FieldMachine coldMachine = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, coldHost);
        for (int callback = 17; callback < 27; callback++)
        {
            host.AdvanceNativeShop();
            coldHost.AdvanceNativeShop();
            Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(coldHost.Capture()), "Cold shop opening changed its native alpha phase.");
        }
        Check(host.Process.Menu!.Shop is { Phase: Ff7ShopPhase.Interactive, Tick: 27, Alpha: 0, DisplayedAlpha: 0 }, "The native opening must enable input on its27th menu callback.");
        Throws<InvalidOperationException>(() => host.CompleteMenu(host.Process.Menu.RequestId));
        host.AdvanceNativeShop(true);
        coldHost.AdvanceNativeShop(true);
        for (int callback = 0; callback < 26; callback++)
        {
            host.AdvanceNativeShop();
            coldHost.AdvanceNativeShop();
            Check(!host.Process.Menu!.Completed && JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(coldHost.Capture()), "The closing native menu completed before its final displayed alpha255 callback.");
        }
        host.AdvanceNativeShop();
        coldHost.AdvanceNativeShop();
        Check(host.Process.Menu is { Completed: true, Shop.Phase: Ff7ShopPhase.Completed, Shop.Tick: 55 } && host.Process.GlobalCommand == Ff7GlobalCommand.Menu, "Shop completion must retain the global owner until the waiting field poll.");
        machine.AdvanceTick();
        coldMachine.AdvanceTick();
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 3) == 99 && host.Process.GlobalCommand == Ff7GlobalCommand.None && JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(coldMachine.Capture()), "The original four-byte field poll did not release the menu and continue in the same script pass.");
        Ff7FieldHost legacy = Host(new() { Menu = new(321, 8, 8), GlobalCommand = Ff7GlobalCommand.Menu, GlobalRequestId = 321 });
        legacy.AdvanceNativeShop();
        Check(legacy.Process.Menu!.Shop is { Tick: 1, Alpha: 245 }, "Absent old-save shop presentation must recover its original opening default without refusing the saved field owner.");
        Console.WriteLine("PASS native shop27/27 callback timing, global owner, field poll and cold continuation");
    }

    private static void NativeModuleSounds()
    {
        Ff7FieldHost host = Host(new());
        host.ConfigureNativeSoundResources(sound => new(22050, false, sound == 17));
        List<Ff7NativeSoundRequest> requests = [];
        host.NativeSoundRequested = requests.Add;
        host.SubmitNativeSound(new(5, 4));
        Check(requests.SequenceEqual(new[] { new Ff7NativeSoundRequest(5, 4) }) && host.Process.SoundChannelBufferPans[4]?.Sample == 4 && host.Process.SoundChannelPans[4] == 64 && host.Process.SoundChannelBaseFrequencies[4] == 22050, "Native UI audio must use the actual fifth resident channel and shared pan/frequency owner.");
        Check(host.Process.SoundChannelBufferPans.Take(4).All(buffer => buffer is null), "Native UI audio contaminated a field resident channel.");
        host.SubmitNativeSound(new(5, 17));
        host.NativeSoundLoopAlreadyPlaying = (_, _) => true;
        host.SubmitNativeSound(new(5, 17, 32));
        Check(host.Process.SoundChannelLoopIds[4] == 17 && host.ResolveNativeSoundPlaybackPan(new Ff7NativeSoundRequest(5, 17, 32), false) == 64, "A reused native UI loop must preserve its current resident-buffer pan.");
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Native UI channel5 controls changed after a cold restore.");
        Ff7FieldHost rejected = Host(new());
        rejected.ConfigureNativeSoundResources(sound => sound == 264 ? throw new NotSupportedException("Missing native sample binding.") : new(22050, false));
        string before = JsonSerializer.Serialize(rejected.Capture());
        Throws<NotSupportedException>(() => rejected.SubmitNativeSound(new(5, 264)));
        Check(before == JsonSerializer.Serialize(rejected.Capture()), "A missing native transaction sound binding mutated its controls before validation.");
        Console.WriteLine("PASS native UI channel5 sound controls, binding validation and cold continuation");
    }

    private static void MateriaTransactions(Ff7Progress baseline, Ff7ShopResources resources)
    {
        Ff7Progress progress = baseline with { Inventory = [], Materia = [new(49, 0), new(53, 123), new(23, 0xFFFFFF)] };
        Ff7ShopState state = new(321, 7, Ff7ShopPhase.Interactive, 0, 0);
        Check(resources.DescribeMateria(49).Name == "Fire" && resources.DescribeMateria(53).Name == "Restore" && resources.DescribeMateria(23).Name == "All", "Materia labels must resolve decimal native IDs through the selected original tables.");
        Check(resources.MateriaSalePrice(49, 0) == 0 && resources.MateriaSalePrice(23, 0) == 0 && resources.MateriaSalePrice(49, 0xFFFFFF) == 42000 && resources.MateriaSalePrice(23, 0xFFFFFF) == 1400000, "The recognized native AP/price1/mastered sale formula changed.");
        Ff7ShopTransactionResult sold = Ff7NativeShopTransactions.Apply(progress, state, resources, new(1, Ff7ShopTransactionKind.Sell, Ff7ShopStockKind.Materia, 53, 1, NativeSlot: 1));
        Check(sold.Receipt.Applied && sold.Progress.Gil == 1123 && sold.Progress.Materia.Select(materia => materia.NativeSlot).SequenceEqual(new int?[] { 0, 2 }), "A materia sale must remove its exact physical slot and preserve the hole and other AP values.");
        Ff7ShopTransactionResult bought = Ff7NativeShopTransactions.Apply(sold.Progress, sold.State, resources, new(2, Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Materia, 49, 1));
        Check(bought.Receipt.Applied && bought.Progress.Gil == 523 && bought.Progress.Materia.Single(materia => materia.NativeSlot == 1) is { MateriaId: 49, AbilityPoints: 0 }, "A materia purchase must fill the first actual empty slot with native AP0.");
        Ff7ShopState cold = JsonSerializer.Deserialize<Ff7ShopState>(JsonSerializer.Serialize(bought.State))!;
        Check(Ff7NativeShopTransactions.Apply(bought.Progress, cold, resources, bought.Receipt.Transaction).Receipt == bought.Receipt, "A cold materia transaction retry changed its receipt or replayed the allocation.");
        Ff7Progress full = baseline with { Materia = Enumerable.Range(0, 200).Select(_ => new Ff7MateriaState(49, 0)).ToArray() };
        Check(!Ff7NativeShopTransactions.Apply(full, state, resources, new(1, Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Materia, 49, 1)).Receipt.Applied, "A native200-slot materia inventory accepted an extra purchase.");
        Ff7Progress rich = baseline with { Gil = uint.MaxValue - 1, Materia = [new(23, 0xFFFFFF)] };
        Check(Ff7NativeShopTransactions.Apply(rich, state, resources, new(1, Ff7ShopTransactionKind.Sell, Ff7ShopStockKind.Materia, 23, 1, 0)).Progress.Gil == 1399998, "The original materia-sale direct DWORD addition must wrap rather than use the normal item-sale saturating helper.");
        Throws<InvalidDataException>(() => Ff7NativeMateriaInventory.Normalize([new(49, 0, 0), new(53, 0, 0)]));
        Throws<InvalidDataException>(() => Ff7NativeShopTransactions.Apply(progress, state, resources, new(1, Ff7ShopTransactionKind.Sell, Ff7ShopStockKind.Materia, 49, 1, 1)));
        Check((resources.MateriaAcquisitionFlags(44) & 2) != 0, "Native materia acquisition must retain its existing global flag publication contract.");
        Console.WriteLine("PASS native materia slot holes, first-empty allocation, AP sale prices and cold transaction identity");
    }

    private static Ff7FieldHost Host(Ff7FieldProcess process)
    {
        Ff7FieldResources field = new("shop-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
