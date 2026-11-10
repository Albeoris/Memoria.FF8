using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    public bool HasUnfinishedTour => ledger.Capture().Tour is { } saved && !new AutoPlayTourLedger(saved).Complete;

    public void RefreshTourCatalog()
    {
        if (session.Field.Name != "mrkt2")
        {
            return;
        }
        Ff7WallMarketTourPlan plan = Ff7WallMarketTourCatalog.Inspect(session);
        AutoPlayTourState? saved = ledger.Capture().Tour;
        AutoPlayTourLedger tour = new(saved ?? new("midgar-wall-market", [], CatalogReason: "Native optional-area classifications are pending."));
        string availability = $"{session.Machine.ProgramIdentity}:market-visit";
        tour.PublishCatalog(plan.Edges.Select(edge => new AutoPlayTourFrontier(edge.Id, plan.RootField, edge.DestinationName, $"{availability}:{edge.Kind}:{edge.Availability}", edge.Kind, edge.Reason, EntryRoute: edge.DestinationField.ToString(System.Globalization.CultureInfo.InvariantCulture), ReturnRoute: edge.ReturnField?.ToString(System.Globalization.CultureInfo.InvariantCulture))), plan.Complete, plan.Reason);
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
        if (session.Field.Name == "mrkt2")
        {
            Ff7WallMarketTourPlan plan = Ff7WallMarketTourCatalog.Inspect(session);
            RefreshTourCatalog();
            AutoPlayTourLedger tour = new(ledger.Capture().Tour!);
            if (tour.Capture().Frontiers.Any(edge => edge.Status == AutoPlayTourStatus.Active))
            {
                throw new NotSupportedException("FFVII optional-area tour returned with an unfinished visit; its local event coverage and native return require resolution before another departure.");
            }
            AutoPlayTourFrontier? next = tour.Capture().Frontiers.FirstOrDefault(edge => edge.Status == AutoPlayTourStatus.Pending && plan.Edges.Any(target => target.Id == edge.Id && target.CatalogReady));
            if (next is null)
            {
                if (!plan.Complete || !tour.Complete)
                {
                    throw new NotSupportedException($"FFVII Wall Market optional-area frontier remains incomplete: {plan.Reason}");
                }
                return false;
            }
            Ff7WallMarketTourEdge target = plan.Edges.Single(edge => edge.Id == next.Id);
            if (!target.CatalogReady)
            {
                throw new NotSupportedException($"FFVII optional room {target.DestinationName} requires its native local event catalog before entry.");
            }
            input = driver.Gateway(target.DestinationField);
            return true;
        }
        AutoPlayTourLedger restored = new(saved!);
        AutoPlayTourFrontier? active = restored.Capture().Frontiers.SingleOrDefault(edge => edge.Status == AutoPlayTourStatus.Active);
        if (active is null)
        {
            return false;
        }
        if (session.Field.Name != active.Destination || active.EntryWitness is null)
        {
            throw new NotSupportedException("FFVII optional-area tour reached a field without its matching native entry witness.");
        }
        if (!int.TryParse(active.ReturnRoute, out int returnField))
        {
            throw new NotSupportedException("FFVII optional-area tour requires its reviewed native return route.");
        }
        input = driver.Gateway(returnField);
        return true;
    }

    private void ObserveTourCoverage(Ff7NativeTickObservation observation, string? coveredScope)
    {
        AutoPlayTourState? saved = ledger.Capture().Tour;
        if (saved is null || observation.Transition is null)
        {
            return;
        }
        AutoPlayTourLedger tour = new(saved);
        AutoPlayTourFrontier? active = saved.Frontiers.SingleOrDefault(edge => edge.Status == AutoPlayTourStatus.Active);
        if (active is not null)
        {
            if (observation.Field == active.Destination && int.TryParse(active.ReturnRoute, out int returnField) && observation.Transition.Map == returnField && session.Field.Name == active.Source)
            {
                if (coveredScope is null)
                {
                    return;
                }
                tour.CompleteVisit(active.Id, $"{coveredScope}/{observation.Generation}/{observation.Tick}/{observation.ProgressWitness}");
            }
            else
            {
                return;
            }
        }
        else
        {
            AutoPlayTourFrontier? entered = saved.Frontiers.FirstOrDefault(edge => edge.Status == AutoPlayTourStatus.Pending && edge.Source == observation.Field && edge.Destination == session.Field.Name && int.TryParse(edge.EntryRoute, out int destination) && observation.Transition.Map == destination);
            if (entered is null)
            {
                return;
            }
            if (coveredScope is null)
            {
                return;
            }
            tour.Begin(entered.Id, $"{coveredScope}/{observation.Generation}/{observation.Tick}/{observation.ProgressWitness}");
            tour.Arrive(entered.Id, $"{session.FieldGeneration}/{session.Field.Name}/{observation.Transition.Map}");
        }
        ledger.UpdateTour(tour.Capture());
        Publish();
    }
}
