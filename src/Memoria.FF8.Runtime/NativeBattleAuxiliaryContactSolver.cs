using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Refines recipient auxiliary articulation within a fixed budget using exact native geometry; remaining contacts are reported without changing physical motion.</summary>
public static partial class NativeBattleAuxiliaryContactSolver
{
    private const int MaximumEvaluations = 8192;
    private const int NativeLatticeReserve = 64;
    private const int MaximumNonlinearIterations = 12;
    private const int MaximumLinearSweeps = 24;

    public static NativeBattleDecodedPose Solve(NativeBattlePoseSource rig, NativeBattleDecodedPose target, NativeBattleAuxiliaryContactCalibration calibration) => new Prepared(rig, calibration).Solve(target);

    internal static NativeBattleDecodedPose SolveObserved(NativeBattlePoseSource rig, NativeBattleDecodedPose target, NativeBattleAuxiliaryContactCalibration calibration, Action<int, NativeBattleAuxiliaryContactReport, NativeBattleAuxiliaryContactReport> observer) => new Prepared(rig, calibration).Solve(target, observer);

    public static NativeBattleAuxiliaryContactReport Inspect(NativeBattlePoseSource rig, NativeBattleDecodedPose pose, NativeBattleAuxiliaryContactCalibration calibration) => new Prepared(rig, calibration).Inspect(pose);

    internal sealed class Prepared
    {
        private readonly NativeBattlePoseSource rig;
        private readonly NativeBattleAuxiliaryContactCalibration calibration;
        private readonly int[] parents;
        private readonly int[] variables;
        private readonly int[] vertexBones;
        private readonly int[][] triangles;
        private readonly int[] clothIds;
        private readonly int[] bodyIds;
        private readonly int[][] segments;
        private readonly HashSet<int> physical;
        private readonly PreparedGeometry geometry;

        public Prepared(NativeBattlePoseSource rig, NativeBattleAuxiliaryContactCalibration calibration)
        {
            rig.Validate();
            this.rig = rig;
            this.calibration = calibration;
            if (calibration.Vertices.Any(vertex => vertex.Bone >= rig.BoneCount) || calibration.PhysicalBones.Any(bone => bone >= rig.BoneCount))
            {
                throw new InvalidDataException("Auxiliary contact geometry leaves its selected recipient skeleton.");
            }
            NativeBattleDecodedPose rest = rig.Sample(0, 1, 0);
            parents = Enumerable.Range(0, rig.BoneCount).Select(bone => NativeBattlePoseRetargeter.Parent(rig, bone)).ToArray();
            physical = calibration.PhysicalBones.ToHashSet();
            HashSet<int> protectedBones = new(physical);
            foreach (int bone in physical)
            {
                for (int ancestor = parents[bone]; ancestor >= 0; ancestor = parents[ancestor])
                {
                    protectedBones.Add(ancestor);
                }
            }
            variables = Enumerable.Range(0, rig.BoneCount).Where(bone => !protectedBones.Contains(bone)).ToArray();
            HashSet<int> auxiliary = variables.ToHashSet();
            vertexBones = calibration.Vertices.Select(vertex => vertex.Bone).ToArray();
            triangles = calibration.Triangles.Select(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();
            clothIds = Enumerable.Range(0, triangles.Length).Where(index => triangles[index].Any(vertex => auxiliary.Contains(vertexBones[vertex]))).ToArray();
            bodyIds = Enumerable.Range(0, triangles.Length).Where(index => triangles[index].All(vertex => physical.Contains(vertexBones[vertex])) && triangles[index].Any(vertex => calibration.ContactBones.Contains(vertexBones[vertex]))).ToArray();
            segments = calibration.BodySegments.Select(segment => new[] { segment.A, segment.B }).ToArray();
            geometry = new(Read(rest), parents, vertexBones, triangles, clothIds, bodyIds, variables, segments);
        }

        public NativeBattleAuxiliaryContactReport Inspect(NativeBattleDecodedPose pose)
        {
            ValidatePose(pose);
            return Report(geometry.ReadFacts(Read(pose)));
        }

        public NativeBattleDecodedPose Solve(NativeBattleDecodedPose target, Action<int, NativeBattleAuxiliaryContactReport, NativeBattleAuxiliaryContactReport>? observer = null)
        {
            ValidatePose(target);
            if (variables.Length == 0 || clothIds.Length == 0 || bodyIds.Length == 0)
            {
                return target;
            }
            NativeState initialTarget = Read(target);
            Facts initialFacts = geometry.ReadFacts(initialTarget);
            if (initialFacts.CorrectableCrossings == 0 && initialFacts.Violation == 0)
            {
                return target;
            }
            NativeState Evaluate(Quaternion[]? auxiliary)
            {
                ImmutableArray<NativeBattleBonePose>.Builder bones = target.Bones.ToBuilder();
                if (auxiliary is not null)
                {
                    foreach (int bone in variables)
                    {
                        bones[bone] = new(NativeBattlePoseRetargeter.Angles(Matrix4x4.CreateFromQuaternion(auxiliary[bone])), target.Bones[bone].Scale);
                    }
                }
                NativeBattleDecodedPose evaluated = target with { Bones = bones.ToImmutable() };
                if (evaluated.Bones.AsSpan().SequenceEqual(target.Bones.AsSpan()))
                {
                    return initialTarget;
                }
                NativeState state = Read(evaluated);
                state.InvalidGeometry = geometry.HasCollapsedCorrection(initialTarget, state);
                return state;
            }
            Solution solution = Run(Evaluate, geometry, initialTarget, initialFacts, observer);
            NativeBattleDecodedPose corrected = solution.Pose;
            ImmutableArray<NativeBattleActionMatrix> originalGlobals = NativeBattlePoseMatrices.StageBoneMatrices(rig, target);
            ImmutableArray<NativeBattleActionMatrix> correctedGlobals = NativeBattlePoseMatrices.StageBoneMatrices(rig, corrected);
            foreach (int bone in physical)
            {
                if (corrected.Bones[bone] != target.Bones[bone] || !SameMatrix(originalGlobals[bone], correctedGlobals[bone]))
                {
                    throw new InvalidDataException("Auxiliary compilation changed a physical recipient joint.");
                }
            }
            if (corrected.Translation != target.Translation || corrected.HasScale != target.HasScale || corrected.Bones.Where((bone, index) => bone.Scale != target.Bones[index].Scale).Any())
            {
                throw new InvalidDataException("Auxiliary compilation changed native scales or root translation.");
            }
            return target with { Bones = corrected.Bones };
        }

        private void ValidatePose(NativeBattleDecodedPose pose)
        {
            if (pose.Bones.Length != rig.BoneCount)
            {
                throw new InvalidDataException("Auxiliary compilation requires the selected recipient's complete native pose.");
            }
        }

        private NativeState Read(NativeBattleDecodedPose pose)
        {
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, pose);
            Vector3[] points = new Vector3[vertexBones.Length];
            for (int vertex = 0; vertex < points.Length; vertex++)
            {
                NativeBattleAuxiliaryVertex definition = calibration.Vertices[vertex];
                NativeBattleActionMatrix matrix = matrices[definition.Bone];
                NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, definition.Position);
                points[vertex] = new(matrix.X + rotated.X, matrix.Y + rotated.Y, matrix.Z + rotated.Z);
            }
            return new()
            {
                Pose = pose,
                Points = points,
                Pivots = matrices.Select(matrix => new Vector3(matrix.X, matrix.Y, matrix.Z)).ToArray(),
                Globals = NativeBattlePoseRetargeter.GlobalRotations(rig, pose),
                Local = Quaternions(pose)
            };
        }

        private static Quaternion[] Quaternions(NativeBattleDecodedPose pose) => pose.Bones.Select(bone => Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(NativeBattlePoseRetargeter.LocalRotation(bone.Rotation)))).ToArray();


        private static bool SameMatrix(NativeBattleActionMatrix a, NativeBattleActionMatrix b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.Scale == b.Scale && a.Rotation.AsSpan().SequenceEqual(b.Rotation.AsSpan());
    }

    private sealed class NativeState
    {
        public required NativeBattleDecodedPose Pose;
        public Vector3[] Points = [];
        public Vector3[] Pivots = [];
        public Matrix4x4[] Globals = [];
        public Quaternion[] Local = [];
        public bool InvalidGeometry;
    }

    private sealed record Solution(NativeBattleDecodedPose Pose, NativeBattleAuxiliaryContactReport Report);

    private sealed class Layer
    {
        public int Body;
        public Vector3 LocalNormal;
        public required Panel Panel;
        public int OutwardPolarity;
    }

    private sealed class Constraint
    {
        public int Body;
        public int[] Cloth = [];
        public Vector3 Weights;
        public Vector3 Normal;
        public double Value;
        public string Kind = "";
        public bool Hard = true;
        public double[]? Derivative;
        public int[]? BodyTriangle;
        public SeparationAxis? EscapeAxis;
        public double OutwardResponse;
    }

    private sealed class Facts
    {
        public readonly List<Constraint> Constraints = [];
        public int Crossings;
        public int ImmutableCrossings;
        public int CorrectableCrossings => Crossings - ImmutableCrossings;
        public double Violation;
        public double SoftViolation;
        public double Strain;
        public double RestRotation;
        public bool InvalidGeometry;
    }

    private static Solution Run(Func<Quaternion[]?, NativeState> evaluate, PreparedGeometry geometry, NativeState initialTarget, Facts initialFacts, Action<int, NativeBattleAuxiliaryContactReport, NativeBattleAuxiliaryContactReport>? observer)
    {
        int evaluations = 0;
        int[] variables = geometry.Variables;
        NativeState bestTarget = initialTarget;
        Facts bestTargetFacts = initialFacts;
        int nonlinearLimit = MaximumEvaluations - NativeLatticeReserve;
        NativeState state = evaluate(geometry.Canonical);
        evaluations++;
        Quaternion[] trialLocal = (Quaternion[])state.Local.Clone();
        Facts facts = ReadFacts(state);
        for (int iteration = 0; iteration < MaximumNonlinearIterations && !Feasible(facts) && evaluations < nonlinearLimit; iteration++)
        {
            Constraint[] active = facts.Constraints.Where(item => item.Hard && item.Value < 0).ToArray();
            if (active.Length == 0)
            {
                break;
            }
            double[][] rows = active.Select(constraint => Jacobian(constraint, state)).ToArray();
            double[] delta = Project(rows, active.Select(item => -item.Value).ToArray(), out _);
            bool improved = false;
            NativeState lineState = state;
            Facts lineFacts = facts;
            for (double fraction = 1; fraction >= 1.0 / 4096 && evaluations < nonlinearLimit; fraction /= 2)
            {
                NativeState candidate = evaluate(Apply(trialLocal, delta, fraction));
                evaluations++;
                Facts candidateFacts = ReadFacts(candidate);
                if (Better(candidateFacts, lineFacts))
                {
                    lineState = candidate;
                    lineFacts = candidateFacts;
                    improved = true;
                }
            }
            state = lineState;
            facts = lineFacts;
            trialLocal = (Quaternion[])state.Local.Clone();
            if (!improved && evaluations < nonlinearLimit)
            {
                NativeState bestState = state;
                Facts bestFacts = facts;
                for (int coordinate = 0; coordinate < variables.Length * 3 && evaluations < nonlinearLimit; coordinate++)
                {
                    foreach (int sign in new[] { -1, 1 })
                    {
                        if (evaluations >= nonlinearLimit)
                        {
                            break;
                        }
                        double[] unit = new double[variables.Length * 3];
                        unit[coordinate] = sign * Math.Tau / 4096;
                        NativeState candidate = evaluate(Apply(trialLocal, unit, 1));
                        evaluations++;
                        Facts candidateFacts = ReadFacts(candidate);
                        if (Better(candidateFacts, bestFacts))
                        {
                            bestState = candidate;
                            bestFacts = candidateFacts;
                            improved = true;
                        }
                    }
                }
                state = bestState;
                facts = bestFacts;
                trialLocal = (Quaternion[])state.Local.Clone();
            }
            observer?.Invoke(iteration + 1, Report(facts), Report(bestTargetFacts));
            if (!improved)
            {
                break;
            }
        }
        if (!Feasible(facts))
        {
            RepairLattice(ref state, ref facts);
        }
        if (Feasible(bestTargetFacts))
        {
            NativeState refined = bestTarget;
            Facts refinedFacts = bestTargetFacts;
            MinimizeRest(ref refined, ref refinedFacts);
        }
        if (bestTarget.InvalidGeometry)
        {
            throw new InvalidDataException("Auxiliary refinement has no valid complete native target pose.");
        }
        return new(bestTarget.Pose, Report(bestTargetFacts));

        bool Feasible(Facts facts) => !facts.InvalidGeometry && facts.CorrectableCrossings == 0 && facts.Violation == 0;

        double[] Project(double[][] rows, double[] required, out double[] dual)
        {
            double[] delta = new double[variables.Length * 3];
            dual = new double[rows.Length];
            for (int sweep = 0; sweep < MaximumLinearSweeps; sweep++)
            {
                for (int row = 0; row < rows.Length; row++)
                {
                    double norm = Dot(rows[row], rows[row]);
                    if (norm == 0)
                    {
                        continue;
                    }
                    double updated = Math.Max(0, dual[row] + (required[row] - Dot(rows[row], delta)) / norm);
                    Add(delta, rows[row], updated - dual[row]);
                    dual[row] = updated;
                }
            }
            return delta;
        }

        void MinimizeRest(ref NativeState state, ref Facts facts)
        {
            int limit = Math.Min(MaximumEvaluations, evaluations + 256);
            for (int iteration = 0; iteration < MaximumNonlinearIterations && evaluations < limit; iteration++)
            {
                NativeState original = state;
                Facts originalFacts = facts;
                double[] desired = geometry.RestDirection(original);
                NativeState? blocked = null;
                Facts? blockedFacts = null;
                for (int pass = 0; pass < 2 && evaluations < limit; pass++)
                {
                    double[] direction = desired;
                    if (pass == 1)
                    {
                        if (blocked is null || blockedFacts is null)
                        {
                            break;
                        }
                        List<Constraint> barriers = originalFacts.Constraints.Where(item => item.Hard && item.Kind == "OwnRestOutwardLayer").ToList();
                        foreach (Constraint witness in blockedFacts.Constraints.Where(item => item.Hard && item.Kind == "TriangleSeparation"))
                        {
                            if (geometry.ContactAt(witness, original) is Constraint barrier)
                            {
                                barriers.Add(barrier);
                            }
                        }
                        if (barriers.Count == 0)
                        {
                            break;
                        }
                        double[][] rows = barriers.Select(item => Jacobian(item, original)).ToArray();
                        double[] correction = Project(rows, barriers.Select((item, index) => -item.Value - Dot(rows[index], desired)).ToArray(), out _);
                        direction = (double[])desired.Clone();
                        Add(direction, correction, 1);
                    }
                    for (double fraction = 1; fraction >= 1.0 / 4096 && evaluations < limit; fraction /= 2)
                    {
                        NativeState candidate = evaluate(Apply(original.Local, direction, fraction));
                        evaluations++;
                        Facts candidateFacts = ReadFacts(candidate);
                        if (Feasible(candidateFacts) && candidateFacts.RestRotation < facts.RestRotation)
                        {
                            state = candidate;
                            facts = candidateFacts;
                        }
                        else if (!Feasible(candidateFacts))
                        {
                            blocked = candidate;
                            blockedFacts = candidateFacts;
                        }
                    }
                    if (!ReferenceEquals(state, original))
                    {
                        break;
                    }
                }
                if (ReferenceEquals(state, original))
                {
                    break;
                }
            }
        }

        void RepairLattice(ref NativeState state, ref Facts facts)
        {
            int limit = Math.Min(MaximumEvaluations, evaluations + NativeLatticeReserve);
            for (int iteration = 0; iteration < MaximumNonlinearIterations && !Feasible(facts) && evaluations < limit; iteration++)
            {
                Constraint[] active = facts.Constraints.Where(item => item.Hard && item.Value < 0).ToArray();
                NativeState original = state;
                double[][] rows = active.Select(item => Jacobian(item, original).Select(value => value * Math.Tau / 4096).ToArray()).ToArray();
                List<double[]> candidates = [];
                void AddCandidate(double[] proposal)
                {
                    if (!candidates.Any(existing => existing.AsSpan().SequenceEqual(proposal)))
                    {
                        candidates.Add(proposal);
                    }
                }
                double[] coupled = Project(rows, active.Select(item => -item.Value).ToArray(), out _);
                AddCandidate(coupled.Select(value => Math.Sign(value) * Math.Ceiling(Math.Abs(value))).ToArray());
                AddCandidate(coupled.Select(Math.Truncate).ToArray());
                AddCandidate(coupled.Select(Math.Floor).ToArray());
                AddCandidate(coupled.Select(value => Math.Round(value)).ToArray());
                AddCandidate(coupled.Select(Math.Ceiling).ToArray());
                for (int coordinate = 0; coordinate < variables.Length * 3; coordinate++)
                {
                    foreach (int sign in new[] { -1, 1 })
                    {
                        double required = 0;
                        bool compatible = true;
                        for (int row = 0; row < rows.Length; row++)
                        {
                            double derivative = rows[row][coordinate] * sign;
                            if (derivative <= 0)
                            {
                                compatible = false;
                                break;
                            }
                            required = Math.Max(required, -active[row].Value / derivative);
                        }
                        if (!compatible || required >= 2048)
                        {
                            continue;
                        }
                        double[] units = new double[variables.Length * 3];
                        units[coordinate] = Math.Ceiling(required) * sign;
                        AddCandidate(units);
                        double[] adjacent = (double[])units.Clone();
                        adjacent[coordinate] += sign;
                        AddCandidate(adjacent);
                    }
                }
                Facts before = facts;
                foreach (double[] units in candidates)
                {
                    if (evaluations >= limit)
                    {
                        break;
                    }
                    NativeState candidate = evaluate(Apply(original.Local, units.Select(value => value * Math.Tau / 4096).ToArray(), 1));
                    evaluations++;
                    Facts candidateFacts = ReadFacts(candidate);
                    if (candidateFacts.CorrectableCrossings <= facts.CorrectableCrossings && Better(candidateFacts, facts))
                    {
                        state = candidate;
                        facts = candidateFacts;
                    }
                }
                if (ReferenceEquals(before, facts))
                {
                    break;
                }
            }
        }

        Facts ReadFacts(NativeState candidate)
        {
            Facts candidateFacts = ReferenceEquals(candidate, initialTarget) ? initialFacts : geometry.ReadFacts(candidate);
            if (!candidate.InvalidGeometry && Better(candidateFacts, bestTargetFacts))
            {
                bestTarget = candidate;
                bestTargetFacts = candidateFacts;
            }
            return candidateFacts;
        }
        double[] Jacobian(Constraint constraint, NativeState state) => geometry.Jacobian(constraint, state);
        Quaternion[] Apply(Quaternion[] original, double[] delta, double fraction) => geometry.Apply(original, delta, fraction);
    }

    private static NativeBattleAuxiliaryContactReport Report(Facts facts) => new(facts.Crossings, facts.Constraints.Count(item => item.Kind == "OwnRestOutwardLayer" && item.Value < 0), (float)facts.Constraints.Where(item => item.Kind == "OwnRestOutwardLayer").Select(item => Math.Max(0, -item.Value)).DefaultIfEmpty().Max(), (float)facts.Strain)
    {
        ImmutableBoundaryCrossings = facts.ImmutableCrossings,
        HardViolatingLayers = facts.Constraints.Count(item => item.Kind == "OwnRestOutwardLayer" && item.Hard && item.Value < 0),
        MaximumHardLayerPenetration = (float)facts.Constraints.Where(item => item.Kind == "OwnRestOutwardLayer" && item.Hard).Select(item => Math.Max(0, -item.Value)).DefaultIfEmpty().Max(),
        HardResidualSquared = facts.Violation,
        CanonicalRestEnergy = facts.RestRotation,
        SoftResidualSquared = facts.SoftViolation
    };

    private static bool Better(Facts candidate, Facts current)
    {
        if (candidate.InvalidGeometry)
        {
            return false;
        }
        if (current.InvalidGeometry)
        {
            return true;
        }
        if (candidate.Violation != current.Violation)
        {
            return candidate.Violation < current.Violation;
        }
        if (candidate.CorrectableCrossings != current.CorrectableCrossings)
        {
            return candidate.CorrectableCrossings < current.CorrectableCrossings;
        }
        if (candidate.RestRotation != current.RestRotation)
        {
            return candidate.RestRotation < current.RestRotation;
        }
        if (candidate.SoftViolation != current.SoftViolation)
        {
            return candidate.SoftViolation < current.SoftViolation;
        }
        if (candidate.Strain != current.Strain)
        {
            return candidate.Strain < current.Strain;
        }
        return false;
    }

    private static double Dot(double[] a, double[] b)
    {
        double result = 0;
        for (int index = 0; index < a.Length; index++)
        {
            result += a[index] * b[index];
        }
        return result;
    }

    private static void Add(double[] target, double[] source, double scale)
    {
        for (int index = 0; index < target.Length; index++)
        {
            target[index] += source[index] * scale;
        }
    }

    private static List<Layer> Calibrate(NativeState rest, int[] bones, int[][] triangles, int[] clothIds, int[] bodyIds, int[][] segments)
    {
        List<Layer> layers = new List<Layer>();
        Dictionary<int, Panel> panels = BuildPanels(triangles, clothIds);
        foreach (int body in bodyIds.SelectMany(index => triangles[index]).Distinct().OrderBy(index => index))
        {
            Vector3 point = rest.Points[body];
            Vector3 origin = Vector3.Zero;
            float nearest = float.PositiveInfinity;
            foreach (int[] segment in segments)
            {
                Vector3 a = rest.Pivots[segment[0]];
                Vector3 line = rest.Pivots[segment[1]] - a;
                if (line.LengthSquared() == 0)
                {
                    continue;
                }
                Vector3 candidate = a + line * Math.Clamp(Vector3.Dot(point - a, line) / line.LengthSquared(), 0, 1);
                float distance = Vector3.DistanceSquared(candidate, point);
                if (distance < nearest)
                {
                    nearest = distance;
                    origin = candidate;
                }
            }
            Vector3 radial = point - origin;
            float length = radial.Length();
            if (length == 0)
            {
                continue;
            }
            Vector3 direction = radial / length;
            float nearestHit = float.PositiveInfinity;
            Layer? selected = null;
            foreach (int clothId in clothIds)
            {
                int[] cloth = triangles[clothId];
                if (cloth.Contains(body))
                {
                    continue;
                }
                if (LineTriangle(origin, direction, rest.Points[cloth[0]], rest.Points[cloth[1]], rest.Points[cloth[2]], out float distance, out _) && distance > length && distance < nearestHit)
                {
                    nearestHit = distance;
                    Panel panel = panels[clothId];
                    int[] oriented = panel.OriginalFaces[clothId];
                    Vector3 normal = Vector3.Cross(rest.Points[oriented[1]] - rest.Points[oriented[0]], rest.Points[oriented[2]] - rest.Points[oriented[0]]);
                    selected = new Layer { Body = body, Panel = panel, OutwardPolarity = MathF.Sign(Vector3.Dot(normal, direction)), LocalNormal = Vector3.TransformNormal(direction, Matrix4x4.Transpose(rest.Globals[bones[body]])) };
                }
            }
            if (selected != null)
            {
                if (selected.Panel.Ambiguous || selected.OutwardPolarity == 0)
                {
                    throw new NotSupportedException("Auxiliary rest coverage requires an orientable, manifold panel and a defined outward crossing.");
                }
                layers.Add(selected);
            }
        }
        return layers;
    }

    private static bool LineTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance, out Vector3 weights)
    {
        Vector3 first = b - a;
        Vector3 second = c - a;
        Vector3 cross = Vector3.Cross(direction, second);
        float determinant = Vector3.Dot(first, cross);
        distance = 0;
        weights = Vector3.Zero;
        if (determinant == 0)
        {
            return false;
        }
        Vector3 relative = origin - a;
        float u = Vector3.Dot(relative, cross) / determinant;
        Vector3 q = Vector3.Cross(relative, first);
        float v = Vector3.Dot(direction, q) / determinant;
        distance = Vector3.Dot(second, q) / determinant;
        weights = new Vector3(1 - u - v, u, v);
        return u >= 0 && v >= 0 && u + v <= 1;
    }

    private static bool Crosses(Vector3[] points, int[] a, int[] b)
    {
        for (int edge = 0; edge < 3; edge++)
        {
            if (Segment(points[a[edge]], points[a[(edge + 1) % 3]], points[b[0]], points[b[1]], points[b[2]]) || Segment(points[b[edge]], points[b[(edge + 1) % 3]], points[a[0]], points[a[1]], points[a[2]]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Segment(Vector3 first, Vector3 last, Vector3 a, Vector3 b, Vector3 c)
    {
        if (!LineTriangle(first, last - first, a, b, c, out float fraction, out Vector3 barycentric))
        {
            return false;
        }
        return fraction > 0 && fraction < 1 && barycentric.X > 0 && barycentric.Y > 0 && barycentric.Z > 0;
    }

}
