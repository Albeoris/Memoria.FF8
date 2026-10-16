namespace Memoria.FF8.Runtime;

public static partial class NativeEventMemoryOwnership
{
    public static IReadOnlyList<NativeEventMemoryRegion> RootRegions(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return WithCardModuleRegions(RootRegions(state.SeeD is not null, state.Economy is not null, state.WorldControls is not null, state.EventRandom?.DrawPoints is not null, state.CharacterVitals is not null, state.DiscProcess is not null, state.FieldFade is not null, state.WorldEntryMemory is not null, state.CardInventory is not null, state.ExamTimer?.TimerMemoryActivated == true || state.ExamTimer?.Event182Mask4 is not null || state.ExamTimer?.Event183Mask1 is not null, state.ExamTimer?.TimerMemoryActivated == true), state.CardInventory);
    }

    public static IReadOnlyList<NativeEventMemoryRegion> RootRegions(IEventServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return WithCardModuleRegions(RootRegions(services.SeeD is not null, services.Economy is not null, services.WorldControls is not null, services.EventRandom?.DrawPoints is not null, services.CharacterVitals is not null, services.DiscProcess is not null, services.FieldFade is not null, services.WorldEntryMemory is not null, services.CardInventory is not null, services.ExamTimer?.TimerMemoryActivated == true || services.ExamTimer?.Event182Mask4 is not null || services.ExamTimer?.Event183Mask1 is not null, services.ExamTimer?.TimerMemoryActivated == true), services.CardInventory);
    }

    private static IReadOnlyList<NativeEventMemoryRegion> WithCardModuleRegions(List<NativeEventMemoryRegion> regions, NativeCardInventory? cards)
    {
        if (cards?.CollectionFlags is not null)
        {
            regions.Add(new(1523, 1, NativeEventMemoryOwner.Cards));
        }
        if (cards?.TripleTriadStatistics is not null)
        {
            regions.Add(new(1524, 6, NativeEventMemoryOwner.Cards));
        }
        if (cards?.TripleTriadRandomState is not null)
        {
            regions.Add(new(1532, 4, NativeEventMemoryOwner.Cards));
        }
        return regions;
    }

    private static List<NativeEventMemoryRegion> RootRegions(bool seeD, bool economy, bool controls, bool drawPoints, bool junction, bool disc, bool fade, bool worldEntry, bool cards, bool timerFlags, bool timerMemory)
    {
        List<NativeEventMemoryRegion> regions = [];
        if (seeD)
        {
            regions.Add(new(8, 4, NativeEventMemoryOwner.SeeD));
            regions.Add(new(16, 4, NativeEventMemoryOwner.SeeD));
            regions.Add(new(184, 4, NativeEventMemoryOwner.SeeD));
        }
        if (economy)
        {
            regions.Add(new(72, 8, NativeEventMemoryOwner.Economy));
        }
        if (controls)
        {
            regions.Add(new(103, 5, NativeEventMemoryOwner.WorldControls));
            regions.Add(new(207, 1, NativeEventMemoryOwner.WorldControls));
            regions.Add(new(209, 1, NativeEventMemoryOwner.WorldControls));
        }
        if (drawPoints)
        {
            regions.Add(new(116, 66, NativeEventMemoryOwner.DrawPoints));
        }
        if (junction)
        {
            regions.Add(new(188, 6, NativeEventMemoryOwner.Junction));
        }
        if (disc)
        {
            regions.Add(new(204, 1, NativeEventMemoryOwner.Disc));
        }
        if (fade)
        {
            regions.Add(new(216, 24, NativeEventMemoryOwner.Fade));
        }
        if (worldEntry)
        {
            regions.Add(new(256, 12, NativeEventMemoryOwner.WorldEntry));
        }
        if (cards)
        {
            regions.Add(new(1408, 115, NativeEventMemoryOwner.Cards));
        }
        if (timerFlags)
        {
            regions.Add(new(182, 2, NativeEventMemoryOwner.Timer));
        }
        if (timerMemory)
        {
            regions.Add(new(205, 2, NativeEventMemoryOwner.Timer));
        }
        return regions;
    }
}
