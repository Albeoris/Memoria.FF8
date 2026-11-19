using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Compiles recipient auxiliary articulation with exact native geometry; it owns no live or persisted state.</summary>
public static class NativeBattleAuxiliaryContactSolver
{
    private const int MaximumEvaluations = 8192;
    private const int ExactTargetReserve = 256;
    private const int NativeLatticeReserve = 64;
    private const int MaximumHomotopyAttempts = 64;
    private const int MaximumNonlinearIterations = 12;
    private const int MaximumLinearSweeps = 24;

    public static NativeBattleDecodedPose Solve(NativeBattlePoseSource rig, NativeBattleDecodedPose target, NativeBattleAuxiliaryContactCalibration calibration) => new Prepared(rig, calibration).Solve(target);

    public static NativeBattleAuxiliaryContactReport Inspect(NativeBattlePoseSource rig, NativeBattleDecodedPose pose, NativeBattleAuxiliaryContactCalibration calibration) => new Prepared(rig, calibration).Inspect(pose);

    internal sealed class Prepared
    {
        private readonly NativeBattlePoseSource rig;
        private readonly NativeBattleAuxiliaryContactCalibration calibration;
        private readonly NativeBattleDecodedPose rest;
        private readonly int[] parents;
        private readonly int[] variables;
        private readonly int[] vertexBones;
        private readonly int[][] triangles;
        private readonly int[] clothIds;
        private readonly int[] bodyIds;
        private readonly int[][] segments;
        private readonly HashSet<int> physical;
        private readonly Quaternion[] restLocal;

        public Prepared(NativeBattlePoseSource rig, NativeBattleAuxiliaryContactCalibration calibration)
        {
            rig.Validate();
            this.rig = rig;
            this.calibration = calibration;
            if (calibration.Vertices.Any(vertex => vertex.Bone >= rig.BoneCount) || calibration.PhysicalBones.Any(bone => bone >= rig.BoneCount))
            {
                throw new InvalidDataException("Auxiliary contact geometry leaves its selected recipient skeleton.");
            }
            rest = rig.Sample(0, 1, 0);
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
            restLocal = Quaternions(rest);
        }

        public NativeBattleAuxiliaryContactReport Inspect(NativeBattleDecodedPose pose)
        {
            ValidatePose(pose);
            NativeState target = Read(pose);
            return Run((progress, auxiliary) => progress == 0 ? Read(rest) : target, parents, vertexBones, triangles, clothIds, bodyIds, variables, segments, true).Report;
        }

        public NativeBattleDecodedPose Solve(NativeBattleDecodedPose target)
        {
            ValidatePose(target);
            if (variables.Length == 0 || clothIds.Length == 0 || bodyIds.Length == 0)
            {
                return target;
            }
            NativeBattleAuxiliaryContactReport original = Inspect(target);
            if (original.ClothBodyCrossings == 0 && original.ViolatingLayers == 0)
            {
                return target;
            }
            Quaternion[] targetLocal = Quaternions(target);
            NativeState Evaluate(double progress, Quaternion[]? auxiliary)
            {
                ImmutableArray<NativeBattleBonePose>.Builder bones = rest.Bones.ToBuilder();
                for (int bone = 0; bone < rig.BoneCount; bone++)
                {
                    if (physical.Contains(bone))
                    {
                        if (progress == 1)
                        {
                            bones[bone] = target.Bones[bone];
                        }
                        else if (progress != 0)
                        {
                            NativeBattleVector rotation = NativeBattlePoseRetargeter.Angles(Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(restLocal[bone], targetLocal[bone], (float)progress)));
                            NativeBattleVector scale = Lerp(rest.Bones[bone].Scale, target.Bones[bone].Scale, progress);
                            bones[bone] = new(rotation, scale);
                        }
                    }
                    else if (auxiliary is not null && Array.BinarySearch(variables, bone) >= 0)
                    {
                        bones[bone] = new(NativeBattlePoseRetargeter.Angles(Matrix4x4.CreateFromQuaternion(auxiliary[bone])), target.Bones[bone].Scale);
                    }
                    else if (progress == 1 && Array.BinarySearch(variables, bone) < 0)
                    {
                        bones[bone] = target.Bones[bone];
                    }
                }
                NativeBattleVector translation = progress == 1 ? target.Translation : Lerp(rest.Translation, target.Translation, progress);
                return Read(new(translation, bones.ToImmutable(), progress == 0 ? rest.HasScale : target.HasScale));
            }
            Solution solution = Run(Evaluate, parents, vertexBones, triangles, clothIds, bodyIds, variables, segments, false);
            NativeBattleDecodedPose corrected = Evaluate(1, solution.Local).Pose;
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
                Local = Quaternions(pose),
                MappedKey = string.Join(';', calibration.PhysicalBones.Select(bone => pose.Bones[bone].ToString())) + pose.Translation
            };
        }

        private static Quaternion[] Quaternions(NativeBattleDecodedPose pose) => pose.Bones.Select(bone => Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(NativeBattlePoseRetargeter.LocalRotation(bone.Rotation)))).ToArray();

        private static NativeBattleVector Lerp(NativeBattleVector a, NativeBattleVector b, double progress) => new(checked((short)Math.Round(a.X + (b.X - a.X) * progress)), checked((short)Math.Round(a.Y + (b.Y - a.Y) * progress)), checked((short)Math.Round(a.Z + (b.Z - a.Z) * progress)));

        private static bool SameMatrix(NativeBattleActionMatrix a, NativeBattleActionMatrix b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.Scale == b.Scale && a.Rotation.AsSpan().SequenceEqual(b.Rotation.AsSpan());
    }

    private sealed class NativeState
    {
        public required NativeBattleDecodedPose Pose;
        public Vector3[] Points = [];
        public Vector3[] Pivots = [];
        public Matrix4x4[] Globals = [];
        public Quaternion[] Local = [];
        public string MappedKey = "";
    }

    private sealed record Solution(Quaternion[] Local, NativeBattleAuxiliaryContactReport Report);

    private sealed class Layer
    {
        public int Body;
        public int[] Cloth = [];
        public Vector3 Weights;
        public Vector3 LocalNormal;
        public double RestGap;
    }

    private sealed class Constraint
    {
        public int Body;
        public int[] Cloth = [];
        public Vector3 Weights;
        public Vector3 Normal;
        public double Value;
        public string Kind = "";
    }

    private sealed class Facts
    {
        public readonly List<Constraint> Constraints = [];
        public int Crossings;
        public double Violation;
        public double Strain;
        public double RestRotation;
    }

    private static Solution Run(Func<double, Quaternion[]?, NativeState> evaluate, int[] parents, int[] vertexBones, int[][] triangles, int[] clothIds, int[] bodyIds, int[] variables, int[][] semanticSegments, bool inspect)
    {
        int evaluations = 0;
        NativeState rest = evaluate(0, null);
        evaluations++;
        Quaternion[] canonical = (Quaternion[])rest.Local.Clone();
        List<Layer> layers = Calibrate(rest, vertexBones, triangles, clothIds, bodyIds, semanticSegments);
        HashSet<(int, int)> edges = new HashSet<(int, int)>();
        foreach (int triangle in clothIds)
        {
            int[] t = triangles[triangle];
            for (int corner = 0; corner < 3; corner++)
            {
                int a = t[corner];
                int b = t[(corner + 1) % 3];
                if (vertexBones[a] != vertexBones[b])
                {
                    edges.Add(a < b ? (a, b) : (b, a));
                }
            }
        }
        (int A, int B)[] orderedEdges = edges.Where(edge => Vector3.DistanceSquared(rest.Points[edge.Item1], rest.Points[edge.Item2]) > 0).OrderBy(edge => edge.Item1).ThenBy(edge => edge.Item2).Select(edge => (edge.Item1, edge.Item2)).ToArray();
        double[] restLengths = orderedEdges.Select(edge => (double)Vector3.Distance(rest.Points[edge.A], rest.Points[edge.B])).ToArray();
        bool[,] influences = new bool[variables.Length, vertexBones.Length];
        for (int variable = 0; variable < variables.Length; variable++)
        {
            for (int vertex = 0; vertex < vertexBones.Length; vertex++)
            {
                for (int bone = vertexBones[vertex]; bone >= 0; bone = parents[bone])
                {
                    if (bone == variables[variable])
                    {
                        influences[variable, vertex] = true;
                        break;
                    }
                }
            }
        }
        Quaternion[] current = (Quaternion[])canonical.Clone();
        double progress = 0;
        double step = 1;
        NativeState acceptedState = rest;
        Facts acceptedFacts = ReadFacts(rest);
        bool forceExactTarget = false;
        int stageEvaluationLimit = MaximumEvaluations;
        if (inspect)
        {
            NativeState inspected = evaluate(1, null);
            return Finish(inspected, ReadFacts(inspected));
        }
        if (acceptedFacts.Crossings != 0 || acceptedFacts.Violation > 0)
        {
            throw Unsupported("recipient rest is not feasible", acceptedFacts);
        }
        for (int attempt = 0; attempt < MaximumHomotopyAttempts && progress < 1 && evaluations < MaximumEvaluations; attempt++)
        {
            bool exactTargetPhase = forceExactTarget || evaluations >= MaximumEvaluations - ExactTargetReserve || attempt == MaximumHomotopyAttempts - 1;
            stageEvaluationLimit = exactTargetPhase ? MaximumEvaluations : MaximumEvaluations - ExactTargetReserve;
            int nonlinearLimit = exactTargetPhase ? stageEvaluationLimit - NativeLatticeReserve : stageEvaluationLimit;
            double next = exactTargetPhase ? 1 : Math.Min(1, progress + step);
            NativeState state = evaluate(next, current);
            evaluations++;
            Quaternion[] trialLocal = (Quaternion[])state.Local.Clone();
            Facts facts = ReadFacts(state);
            for (int iteration = 0; iteration < MaximumNonlinearIterations && !Feasible(facts) && evaluations < nonlinearLimit; iteration++)
            {
                Constraint[] active = facts.Constraints.Where(item => item.Value < 0).ToArray();
                if (active.Length == 0)
                {
                    break;
                }
                double[][] rows = active.Select(constraint => Jacobian(constraint, state)).ToArray();
                double[] delta = Project(rows, active.Select(item => -item.Value).ToArray(), out double[] dual);
                // Improve normalized shared-edge strain only in the active contact tangent space.
                double[] gradient = new double[variables.Length * 3];
                List<(double Residual, double[] Derivative)> edgeRows = EdgeRows(state);
                foreach (var edge in edgeRows)
                {
                    Add(gradient, edge.Derivative, edge.Residual);
                }
                for (int sweep = 0; sweep < MaximumLinearSweeps; sweep++)
                {
                    for (int row = 0; row < rows.Length; row++)
                    {
                        double norm = Dot(rows[row], rows[row]);
                        if (dual[row] > 0 && norm > 0)
                        {
                            Add(gradient, rows[row], -Dot(gradient, rows[row]) / norm);
                        }
                    }
                }
                double numerator = 0;
                double denominator = 0;
                foreach (var edge in edgeRows)
                {
                    double direction = Dot(edge.Derivative, gradient);
                    numerator += (edge.Residual + Dot(edge.Derivative, delta)) * direction;
                    denominator += direction * direction;
                }
                if (denominator > 0)
                {
                    Add(delta, gradient, -numerator / denominator);
                }
                bool improved = false;
                for (double fraction = 1; fraction >= 1.0 / 4096 && evaluations < nonlinearLimit; fraction /= 2)
                {
                    NativeState candidate = evaluate(next, Apply(trialLocal, delta, fraction));
                    evaluations++;
                    Facts candidateFacts = ReadFacts(candidate);
                    if (Better(candidateFacts, facts))
                    {
                        state = candidate;
                        facts = candidateFacts;
                        trialLocal = (Quaternion[])state.Local.Clone();
                        improved = true;
                        break;
                    }
                }
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
                            NativeState candidate = evaluate(next, Apply(trialLocal, unit, 1));
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
                if (!improved)
                {
                    break;
                }
            }
            if (facts.Crossings == 0 && !Feasible(facts))
            {
                RepairLattice(next, ref state, ref facts);
                trialLocal = (Quaternion[])state.Local.Clone();
            }
            if (Feasible(facts))
            {
                progress = next;
                current = trialLocal;
                acceptedState = state;
                acceptedFacts = facts;
                step = Math.Min(1 - progress, step * 2);
            }
            else
            {
                if (exactTargetPhase)
                {
                    NotSupportedException exception = Unsupported($"exact target after canonical body progress {progress:R}", facts);
                    exception.Data[nameof(NativeBattleDecodedPose)] = state.Pose;
                    throw exception;
                }
                if (evaluations >= stageEvaluationLimit)
                {
                    forceExactTarget = true;
                    continue;
                }
                step /= 2;
                NativeState finer = evaluate(Math.Min(1, progress + step), current);
                evaluations++;
                if (finer.MappedKey == acceptedState.MappedKey)
                {
                    forceExactTarget = true;
                }
            }
        }
        if (progress != 1 || !Feasible(acceptedFacts))
        {
            throw Unsupported($"fixed compilation budget at body progress {progress:R}", acceptedFacts);
        }
        return Finish(acceptedState, acceptedFacts);

        Solution Finish(NativeState state, Facts facts) => new(state.Local, new(facts.Crossings, facts.Constraints.Count(item => item.Kind == "OwnRestOutwardLayer" && item.Value < 0), (float)facts.Constraints.Where(item => item.Kind == "OwnRestOutwardLayer").Select(item => Math.Max(0, -item.Value)).DefaultIfEmpty().Max(), (float)facts.Strain));

        NotSupportedException Unsupported(string reason, Facts facts) => new($"Auxiliary contact compilation cannot satisfy {reason}; native evaluations {evaluations}, crossings {facts.Crossings}, squared layer/contact residual {facts.Violation:R}; witnesses {string.Join("; ", facts.Constraints.Where(item => item.Value < 0).Take(3).Select(item => $"{item.Kind} body {item.Body}, cloth [{string.Join(',', item.Cloth)}], signed {item.Value:R}"))}.");

        bool Feasible(Facts facts) => facts.Crossings == 0 && facts.Violation == 0;

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

        void RepairLattice(double at, ref NativeState state, ref Facts facts)
        {
            int limit = Math.Min(stageEvaluationLimit, evaluations + NativeLatticeReserve);
            for (int iteration = 0; iteration < MaximumNonlinearIterations && !Feasible(facts) && evaluations < limit; iteration++)
            {
                Constraint[] active = facts.Constraints.Where(item => item.Value < 0).ToArray();
                NativeState original = state;
                double[][] rows = active.Select(item => Jacobian(item, original).Select(value => value * Math.Tau / 4096).ToArray()).ToArray();
                List<double[]> candidates = [];
                double[] coupled = Project(rows, active.Select(item => -item.Value).ToArray(), out _);
                candidates.Add(coupled.Select(value => Math.Sign(value) * Math.Ceiling(Math.Abs(value))).ToArray());
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
                        candidates.Add(units);
                        double[] adjacent = (double[])units.Clone();
                        adjacent[coordinate] += sign;
                        candidates.Add(adjacent);
                    }
                }
                Facts before = facts;
                foreach (double[] units in candidates)
                {
                    if (evaluations >= limit)
                    {
                        break;
                    }
                    NativeState candidate = evaluate(at, Apply(original.Local, units.Select(value => value * Math.Tau / 4096).ToArray(), 1));
                    evaluations++;
                    Facts candidateFacts = ReadFacts(candidate);
                    if (candidateFacts.Crossings == 0 && Better(candidateFacts, facts))
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

        Facts ReadFacts(NativeState state)
        {
            Facts facts = new Facts();
            foreach (Layer layer in layers)
            {
                Vector3 normal = Vector3.TransformNormal(layer.LocalNormal, state.Globals[vertexBones[layer.Body]]);
                Vector3 cloth = Weighted(state.Points, layer.Cloth, layer.Weights);
                double value = Vector3.Dot(cloth - state.Points[layer.Body], normal);
                Constraint constraint = new Constraint { Body = layer.Body, Cloth = layer.Cloth, Weights = layer.Weights, Normal = normal, Value = value, Kind = "OwnRestOutwardLayer" };
                facts.Constraints.Add(constraint);
                if (value < 0)
                {
                    facts.Violation += value * value;
                }
            }
            foreach (int clothId in clothIds)
            {
                int[] cloth = triangles[clothId];
                foreach (int bodyId in bodyIds)
                {
                    int[] body = triangles[bodyId];
                    if (cloth.Intersect(body).Any() || !Crosses(state.Points, cloth, body))
                    {
                        continue;
                    }
                    facts.Crossings++;
                    Vector3 restCross = Vector3.Cross(rest.Points[cloth[1]] - rest.Points[cloth[0]], rest.Points[cloth[2]] - rest.Points[cloth[0]]);
                    Vector3 currentCross = Vector3.Cross(state.Points[cloth[1]] - state.Points[cloth[0]], state.Points[cloth[2]] - state.Points[cloth[0]]);
                    if (restCross.LengthSquared() == 0 || currentCross.LengthSquared() == 0)
                    {
                        continue;
                    }
                    Vector3 restNormal = Vector3.Normalize(restCross);
                    Vector3 restCentre = (rest.Points[body[0]] + rest.Points[body[1]] + rest.Points[body[2]]) / 3;
                    float sign = MathF.Sign(Vector3.Dot(restCentre - rest.Points[cloth[0]], restNormal));
                    if (sign == 0)
                    {
                        continue;
                    }
                    Vector3 normal = -sign * Vector3.Normalize(currentCross);
                    int penetrating = body.OrderBy(vertex => Vector3.Dot(state.Points[cloth[0]] - state.Points[vertex], normal)).First();
                    Vector3 point = Closest(state.Points[penetrating], state.Points[cloth[0]], state.Points[cloth[1]], state.Points[cloth[2]], out Vector3 barycentric);
                    double value = Vector3.Dot(point - state.Points[penetrating], normal);
                    if (value < 0)
                    {
                        facts.Constraints.Add(new Constraint { Body = penetrating, Cloth = cloth, Weights = barycentric, Normal = normal, Value = value, Kind = "CurrentSignedVertexFace" });
                        facts.Violation += value * value;
                    }
                }
            }
            for (int index = 0; index < orderedEdges.Length; index++)
            {
                var edge = orderedEdges[index];
                double residual = (Vector3.Distance(state.Points[edge.A], state.Points[edge.B]) - restLengths[index]) / restLengths[index];
                facts.Strain += residual * residual;
            }
            foreach (int bone in variables)
            {
                double angle = 2 * Math.Acos(Math.Min(1, Math.Abs(Quaternion.Dot(state.Local[bone], canonical[bone]))));
                facts.RestRotation += angle * angle;
            }
            return facts;
        }

        double[] Jacobian(Constraint constraint, NativeState state)
        {
            double[] row = new double[variables.Length * 3];
            for (int variable = 0; variable < variables.Length; variable++)
            {
                int bone = variables[variable];
                Matrix4x4 parent = parents[bone] < 0 ? Matrix4x4.Identity : state.Globals[parents[bone]];
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 direction = Vector3.TransformNormal(axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ, parent);
                    Vector3 derivative = Vector3.Zero;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int vertex = constraint.Cloth[corner];
                        float weight = corner == 0 ? constraint.Weights.X : corner == 1 ? constraint.Weights.Y : constraint.Weights.Z;
                        if (influences[variable, vertex])
                        {
                            derivative += weight * Vector3.Cross(direction, state.Points[vertex] - state.Pivots[bone]);
                        }
                    }
                    row[variable * 3 + axis] = Vector3.Dot(derivative, constraint.Normal);
                }
            }
            return row;
        }

        List<(double Residual, double[] Derivative)> EdgeRows(NativeState state)
        {
            List<(double, double[])> rows = new List<(double, double[])>();
            for (int index = 0; index < orderedEdges.Length; index++)
            {
                var edge = orderedEdges[index];
                Vector3 line = state.Points[edge.A] - state.Points[edge.B];
                float length = line.Length();
                if (length == 0 || restLengths[index] == 0)
                {
                    continue;
                }
                Vector3 direction = line / length;
                double[] row = new double[variables.Length * 3];
                for (int variable = 0; variable < variables.Length; variable++)
                {
                    int bone = variables[variable];
                    Matrix4x4 parent = parents[bone] < 0 ? Matrix4x4.Identity : state.Globals[parents[bone]];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Vector3 rotation = Vector3.TransformNormal(axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ, parent);
                        Vector3 derivative = (influences[variable, edge.A] ? Vector3.Cross(rotation, state.Points[edge.A] - state.Pivots[bone]) : Vector3.Zero) - (influences[variable, edge.B] ? Vector3.Cross(rotation, state.Points[edge.B] - state.Pivots[bone]) : Vector3.Zero);
                        row[variable * 3 + axis] = Vector3.Dot(derivative, direction) / restLengths[index];
                    }
                }
                rows.Add(((length - restLengths[index]) / restLengths[index], row));
            }
            return rows;
        }

        Quaternion[] Apply(Quaternion[] original, double[] delta, double fraction)
        {
            Quaternion[] proposed = (Quaternion[])original.Clone();
            for (int variable = 0; variable < variables.Length; variable++)
            {
                Vector3 vector = new Vector3((float)(delta[variable * 3] * fraction), (float)(delta[variable * 3 + 1] * fraction), (float)(delta[variable * 3 + 2] * fraction));
                float angle = vector.Length();
                if (angle > 0)
                {
                    Matrix4x4 currentMatrix = Matrix4x4.CreateFromQuaternion(original[variables[variable]]);
                    Matrix4x4 adjustment = Matrix4x4.CreateFromAxisAngle(vector / angle, angle);
                    proposed[variables[variable]] = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(currentMatrix * adjustment));
                }
            }
            return proposed;
        }
    }

    private static bool Better(Facts candidate, Facts current)
    {
        if (candidate.Violation != current.Violation)
        {
            return candidate.Violation < current.Violation;
        }
        if (candidate.Crossings != current.Crossings)
        {
            return candidate.Crossings < current.Crossings;
        }
        if (candidate.Strain != current.Strain)
        {
            return candidate.Strain < current.Strain;
        }
        return candidate.RestRotation < current.RestRotation;
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

    private static Vector3 Weighted(Vector3[] points, int[] triangle, Vector3 weights) => points[triangle[0]] * weights.X + points[triangle[1]] * weights.Y + points[triangle[2]] * weights.Z;

    private static List<Layer> Calibrate(NativeState rest, int[] bones, int[][] triangles, int[] clothIds, int[] bodyIds, int[][] segments)
    {
        List<Layer> layers = new List<Layer>();
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
                if (Ray(origin, direction, rest.Points[cloth[0]], rest.Points[cloth[1]], rest.Points[cloth[2]], out float distance, out Vector3 weights) && distance > length && distance < nearestHit)
                {
                    nearestHit = distance;
                    selected = new Layer { Body = body, Cloth = cloth, Weights = weights, LocalNormal = Vector3.TransformNormal(direction, Matrix4x4.Transpose(rest.Globals[bones[body]])), RestGap = distance - length };
                }
            }
            if (selected != null)
            {
                layers.Add(selected);
            }
        }
        return layers;
    }

    private static bool Ray(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance, out Vector3 weights)
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
        return u >= 0 && v >= 0 && u + v <= 1 && distance >= 0;
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
        if (!Ray(first, last - first, a, b, c, out float fraction, out Vector3 barycentric))
        {
            return false;
        }
        return fraction > 0 && fraction < 1 && barycentric.X > 0 && barycentric.Y > 0 && barycentric.Z > 0;
    }

    private static Vector3 Closest(Vector3 point, Vector3 a, Vector3 b, Vector3 c, out Vector3 weights)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0)
        {
            weights = new Vector3(1, 0, 0);
            return a;
        }
        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3)
        {
            weights = new Vector3(0, 1, 0);
            return b;
        }
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            float v = d1 / (d1 - d3);
            weights = new Vector3(1 - v, v, 0);
            return a + v * ab;
        }
        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6)
        {
            weights = new Vector3(0, 0, 1);
            return c;
        }
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            float w = d2 / (d2 - d6);
            weights = new Vector3(1 - w, 0, w);
            return a + w * ac;
        }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            weights = new Vector3(0, 1 - w, w);
            return b + w * (c - b);
        }
        float inverse = 1 / (va + vb + vc);
        weights = new Vector3(1 - vb * inverse - vc * inverse, vb * inverse, vc * inverse);
        return a * weights.X + b * weights.Y + c * weights.Z;
    }
}
