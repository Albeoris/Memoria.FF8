namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal void HandleJunctionWheel(int x, int y, int delta)
    {
        if (!IsJunctionDashboard || delta == 0)
        {
            return;
        }
        var view = BuildView();
        var point = view.ToDesignPoint(x, y);
        if (junctionTransferModal)
        {
            if (junctionTransferColumns?.Bounds.Contains(point.X, point.Y) == true)
            {
                junctionTransferColumns.ScrollColumns(-Math.Sign(delta));
            }
            return;
        }
        if (junctionEditor == "gf" && junctionGuardianColumns?.Bounds.Contains(point.X, point.Y) == true)
        {
            junctionGuardianColumns.ScrollColumns(-Math.Sign(delta));
            return;
        }
        if (junctionEditor is "magic" or "command" or "passive" && junctionCandidateColumns?.Bounds.Contains(point.X, point.Y) == true)
        {
            junctionCandidateColumns.ScrollColumns(-Math.Sign(delta));
            return;
        }
        if (junctionScroll?.Bounds.Contains(point.X, point.Y) == true)
        {
            junctionScroll.ScrollBy(-Math.Sign(delta) * 64 * TextScale);
            return;
        }
        var dock = FocusableDescendants(view.Root).FirstOrDefault(element => element.Id == "impact-previous");
        if (dock is not null && point.Y >= dock.Bounds.Y && point.Y < view.DesignHeight - 48)
        {
            if (junctionReview)
            {
                int maximum = Math.Max(0, ((junctionPlan?.Changes.Count ?? 0) - 1) / 5);
                junctionImpactPage = Math.Clamp(junctionImpactPage - Math.Sign(delta), 0, maximum);
                _view = null;
            }
            else
            {
                PageJunctionCandidates(-Math.Sign(delta));
            }
        }
    }
}
