using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeWorldContactSpark? CaptureFootContactSpark(WorldState state, ref NativeWorldSalaryState salary)
    {
        NativeWorldProcess process = (session.State.WorldProcess ?? throw new NotSupportedException("Native contact sparks require their process state.")) with { PreviousContactC76784 = salary.PreviousContactC76784 };
        IReadOnlyList<(byte ShapeId, NativeWorldPathPosition Position)> objects;
        int primary;
        if (state.NativeRagnarokRuntime is { } ragnarok)
        {
            objects = ragnarok.Objects.Select(item => (item.ShapeId, item.Position)).ToArray();
            primary = ragnarok.PrimaryContact ?? throw new NotSupportedException("Native contact sparks require the preceding Ragnarok primary contact.");
        }
        else if (state.NativeGardenRuntime is { } garden)
        {
            objects = garden.Objects.Select(item => (item.ShapeId, item.Position)).ToArray();
            primary = garden.PrimaryContact;
        }
        else
        {
            // Ordinary allocations without the dense contact publication cannot establish this process word.
            salary = salary.WithPreviousContact(null);
            return null;
        }
        NativeWorldContactSpark? candidate = null;
        if (primary >= 0 && primary < objects.Count && objects[primary].ShapeId == 93)
        {
            NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(state);
            NativeWorldPathPosition body = new(state.X, state.NativeHeight, -state.Z);
            NativeWorldPathWords bodyWords = projection.RelativeWords(body);
            NativeWorldPathWords contactWords = projection.RelativeWords(objects[primary].Position);
            candidate = new(body, unchecked((short)(contactWords.X - bodyWords.X)), unchecked((short)(contactWords.Z - bodyWords.Z)));
        }
        NativeWorldContactSparkResult selected = NativeWorldContactSparkEmitter.Select(process, primary, objects.Count, candidate);
        salary = salary.WithPreviousContact(selected.Process.PreviousContactC76784);
        return selected.Spark;
    }
}
