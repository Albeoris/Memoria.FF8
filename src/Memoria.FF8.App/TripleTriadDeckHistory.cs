using System.Text.Json;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

/// <summary>Recent player hands belong to the user and survive loading another story snapshot.</summary>
internal sealed class TripleTriadDeckHistory(string path)
{
    public IReadOnlyList<int[]> Load()
    {
        if (!File.Exists(path))
        {
            return [];
        }
        int[][] decks = JsonSerializer.Deserialize<int[][]>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty Triple Triad deck history.");
        if (decks.Length > 3)
        {
            throw new InvalidDataException("Triple Triad history contains more than three decks.");
        }
        foreach (int[] deck in decks)
        {
            Validate(deck);
        }
        return decks;
    }

    public void Connect(TripleTriadController controller)
    {
        controller.SetRecentDecks(Load());
        controller.DeckConfirmed += Record;
    }

    public void Record(int[] deck)
    {
        Validate(deck);
        int[] composition = deck.Order().ToArray();
        int[][] decks = new[] { deck.ToArray() }.Concat(Load().Where(previous => !previous.Order().SequenceEqual(composition))).Take(3).ToArray();
        string destination = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(decks, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void Validate(int[]? deck)
    {
        if (deck is null || deck.Length is < 2 or > 50 || deck.Any(id => id is < 0 or >= 110))
        {
            throw new InvalidDataException("Triple Triad history requires valid card IDs per deck.");
        }
    }
}
