using System.Globalization;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    public bool HasUnfinishedTour => ledger.Capture().Tour is { } saved && !new AutoPlayTourLedger(saved).Complete;

    public void RefreshTourCatalog()
    {
        AutoPlayTourState? saved = ledger.Capture().Tour;
        if (session.Field.Name != "mrkt2" && saved is null)
        {
            return;
        }
        AutoPlayTourLedger tour = new(saved ?? new("midgar-wall-market", [], CatalogReason: "Native optional-area classifications are pending."));
        AutoPlayTourFrontier? active = tour.ActiveLeaf;
        Ff7WallMarketTourPlan? plan = InspectTourSource(active);
        if (plan is null)
        {
            return;
        }
        string availability = $"{session.Machine.ProgramIdentity}:market-visit";
        string? parent = active?.Id;
        var frontiers = plan.Edges.Select(edge => new AutoPlayTourFrontier(edge.Id, plan.RootField, edge.DestinationName, $"{availability}:{edge.Kind}:{edge.Availability}", edge.Kind, edge.Reason, EntryRoute: edge.DestinationField.ToString(CultureInfo.InvariantCulture), ReturnRoute: edge.ReturnField?.ToString(CultureInfo.InvariantCulture), ParentId: parent));
        tour.PublishCatalog(frontiers, parent is null ? plan.Complete : saved!.CatalogComplete, parent is null ? plan.Reason : saved!.CatalogReason);
        if (parent is not null)
        {
            tour.PublishDirectory(new(plan.RootField, availability, plan.Complete, plan.Reason, parent));
        }
        ledger.UpdateTour(tour.Capture());
        Publish();
    }

    public bool TryChooseTourInput(Ff7AutoplayDriver driver, out Ff7AutoplayInput input)
    {
        input = default;
        AutoPlayTourState? saved = ledger.Capture().Tour;
        if (session.Field.Name != "mrkt2" && saved is null)
        {
            return false;
        }
        if (!ledger.CanLeave)
        {
            throw new NotSupportedException("FFVII optional-area touring requires the current native field event catalog to be complete first.");
        }
        if (saved is null)
        {
            RefreshTourCatalog();
            saved = ledger.Capture().Tour!;
        }
        AutoPlayTourLedger tour = new(saved);
        AutoPlayTourFrontier? active = tour.ActiveLeaf;
        if (active is not null && (session.Field.Name != active.Destination || active.EntryWitness is null))
        {
            throw new NotSupportedException("FFVII optional-area tour reached a field without its matching native entry witness.");
        }
        Ff7WallMarketTourPlan? plan = InspectTourSource(active);
        if (plan is not null)
        {
            Ff7WallMarketTourEdge? target = plan.Edges.FirstOrDefault(candidate => candidate.CatalogReady && saved.Frontiers.Any(edge => edge.Id == candidate.Id && edge.ParentId == active?.Id && edge.Status == AutoPlayTourStatus.Pending));
            if (target is not null)
            {
                input = driver.Gateway(target.DestinationField);
                return true;
            }
            if (!plan.Complete || active is null && !tour.Complete || active is not null && !tour.CanCompleteActiveVisit)
            {
                throw new NotSupportedException($"FFVII Wall Market optional-area frontier remains incomplete: {plan.Reason}");
            }
        }
        if (active is null)
        {
            return false;
        }
        if (!tour.CanCompleteActiveVisit || !int.TryParse(active.ReturnRoute, out int returnField))
        {
            throw new NotSupportedException("FFVII optional-area tour requires covered children and a reviewed native return route.");
        }
        input = driver.Gateway(returnField);
        return true;
    }

    private Ff7WallMarketTourPlan? InspectTourSource(AutoPlayTourFrontier? active)
    {
        if (active is null && session.Field.Name == "mrkt2")
        {
            return Ff7WallMarketTourCatalog.Inspect(session);
        }
        if (active is { Destination: "mrkt1", EntryWitness: not null } && session.Field.Name == active.Destination)
        {
            return Ff7WallMarketTourCatalog.InspectMainStreet(session);
        }
        return null;
    }

    private void ObserveTourCoverage(Ff7NativeTickObservation observation, string? coveredScope)
    {
        AutoPlayTourState? saved = ledger.Capture().Tour;
        if (saved is null || coveredScope is null || observation.Transition is null || observation.TransitionOwner is not null)
        {
            return;
        }
        AutoPlayTourLedger tour = new(saved);
        AutoPlayTourFrontier? active = tour.ActiveLeaf;
        string witness = $"{coveredScope}/{observation.Generation}/{observation.Tick}/{observation.ProgressWitness}";
        if (active is not null && MatchesGateway(active.Destination, active.Source, active.ReturnRoute))
        {
            if (!tour.CanCompleteActiveVisit)
            {
                return;
            }
            tour.CompleteVisit(active.Id, witness);
        }
        else
        {
            AutoPlayTourFrontier? entered = saved.Frontiers.SingleOrDefault(edge => edge.Status == AutoPlayTourStatus.Pending && edge.ParentId == active?.Id && MatchesGateway(edge.Source, edge.Destination, edge.EntryRoute));
            if (entered is null)
            {
                return;
            }
            tour.Begin(entered.Id, witness);
            tour.Arrive(entered.Id, $"{session.FieldGeneration}/{session.Field.Name}/{observation.Transition.Map}");
        }
        ledger.UpdateTour(tour.Capture());
        Publish();

        bool MatchesGateway(string source, string destination, string? route) =>
            observation.Field == source && session.Field.Name == destination && int.TryParse(route, out int map) && observation.Transition.Map == map;
    }
}
