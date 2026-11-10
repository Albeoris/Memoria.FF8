using System.Numerics;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayEventCoverage
{
    partial void ExtendCoverageCatalog(List<AutoPlayEventCoverageEntry> catalog, ref bool complete, ref string reason)
    {
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        complete = plan.Complete;
        reason = plan.Reason;
        foreach (Ff7FieldEventCoverageTarget target in plan.Events)
        {
            AutoPlayEventCoverageEntry entry = target.Kind is Ff7FieldEventCoverageKind.Interaction or Ff7FieldEventCoverageKind.Conditional or Ff7FieldEventCoverageKind.ResetHazard
                ? new(target.Id, target.Availability)
                : new(target.Id, target.Availability, AutoPlayEventCoverageStatus.Deferred, Reason: target.Reason);
            int existing = catalog.FindIndex(value => value.Id == target.Id);
            if (existing >= 0)
            {
                catalog[existing] = entry;
            }
            else
            {
                catalog.Add(entry);
            }
        }
    }

    partial void ConfigureCoverageNavigation(Ff7AutoplayDriver driver, ref bool run)
    {
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        if (plan.WalkOnly)
        {
            run = false;
            AutoPlayEventCoverageEntry? next = ledger.Current?.Events.FirstOrDefault(value => value.Status == AutoPlayEventCoverageStatus.Available);
            driver.AvoidLinePush(new[] { 11, 14 }.Where(entity => next?.Id != $"line:{entity}:script:2").ToArray());
        }
    }

    partial void ChooseLineCoverageInput(Ff7AutoplayDriver driver, AutoPlayEventCoverageEntry entry, ref Ff7AutoplayInput input, ref bool handled)
    {
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        Ff7FieldEventCoverageTarget? target = plan.Events.SingleOrDefault(value => value.Id == entry.Id && value.Kind is Ff7FieldEventCoverageKind.Interaction or Ff7FieldEventCoverageKind.Conditional or Ff7FieldEventCoverageKind.ResetHazard);
        if (target is null)
        {
            return;
        }
        if (target.Kind == Ff7FieldEventCoverageKind.Conditional && Ff7FieldEventCoverageCatalog.RecognizesReunion(session))
        {
            ledger.Arm(entry.Id, $"{session.FieldGeneration}/{session.Machine.Tick}/{session.CaptureEventProgressWitness()}");
            Publish();
            var triangle = session.Field.Walkmesh[57];
            input = driver.Approach((triangle.A + triangle.B + triangle.C) / 3, triangle.Index);
            handled = true;
            return;
        }
        if (!entry.Id.StartsWith("line:", StringComparison.Ordinal))
        {
            return;
        }
        Ff7Line line = session.Host.Process.Lines.Single(value => value.Entity == target.Entity);
        if (target.Script == 6)
        {
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            Vector2 segment = new(line.X2 - line.X1, line.Y2 - line.Y1);
            Vector2 normal = Vector2.Normalize(new(-segment.Y, segment.X));
            Vector2 center = new((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f);
            Vector2 position = new(player.X4096 / 4096f, player.Y4096 / 4096f);
            if (Vector2.Dot(position - center, normal) < 0)
            {
                normal = -normal;
            }
            Vector2 outside = center + normal * (player.CollisionRadius * 2 + 1);
            input = driver.Steer(new Vector3(outside, line.Z1), run: !plan.WalkOnly);
            handled = true;
            return;
        }
        input = driver.Approach(new Vector3((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, (line.Z1 + line.Z2) / 2f), run: !plan.WalkOnly);
        handled = true;
    }
}
