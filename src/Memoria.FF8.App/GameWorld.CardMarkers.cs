using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private CardArtwork? cardArtwork;
    private long cardArtworkGeneration = -1;

    private IReadOnlyList<CardMarkerVisual> CreateCardMarkers(TimeSpan elapsed)
    {
        if (session.State.Scene != SceneKind.Field)
        {
            return [];
        }
        IReadOnlyList<RareCardOffer> offers = Events.GetRareCardOffers();
        if (offers.Count == 0)
        {
            return [];
        }
        if (cardArtworkGeneration != (resources?.Generation ?? 0))
        {
            cardArtwork = null;
            cardArtworkGeneration = resources?.Generation ?? 0;
        }
        cardArtwork ??= CardArtwork.Load(assets.GameDirectory, resources);
        // An opponent may hold several unique cards. Show each for one complete turn.
        long turn = (long)Math.Floor(Math.Max(0, elapsed.TotalSeconds) / 8);
        return offers.GroupBy(offer => offer.ObjectId).Select(group =>
        {
            RareCardOffer[] cards = group.OrderBy(offer => offer.CardId).ToArray();
            return new CardMarkerVisual(group.Key, cardArtwork.GetFront(cards[turn % cards.Length].CardId), cardArtwork.Back);
        }).ToArray();
    }
}
