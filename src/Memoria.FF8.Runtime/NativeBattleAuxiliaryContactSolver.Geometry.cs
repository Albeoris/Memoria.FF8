using System.Numerics;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAuxiliaryContactSolver
{
    // Resource-only preparation is immutable and shared by independent frame solves.
    private sealed class PreparedGeometry
    {
        private readonly NativeState rest;
        private readonly int[] parents;
        private readonly int[] vertexBones;
        private readonly int[] variables;
        private readonly Quaternion[] canonical;
        private readonly List<Layer> layers;
        private readonly (int A, int B)[] orderedEdges;
        private readonly double[] restLengths;
        private readonly bool[,] influences;
        private readonly bool[] auxiliaryInfluenced;
        private readonly int[][] clothFaces;
        private readonly (int[] Cloth, int[] Body)[] contactPairs;

        public PreparedGeometry(NativeState rest, int[] parents, int[] vertexBones, int[][] triangles, int[] clothIds, int[] bodyIds, int[] variables, int[][] semanticSegments)
        {
            this.rest = rest;
            this.parents = parents;
            this.vertexBones = vertexBones;
            this.variables = variables;
            canonical = (Quaternion[])rest.Local.Clone();
            layers = Calibrate(rest, vertexBones, triangles, clothIds, bodyIds, semanticSegments);
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
            orderedEdges = edges.Where(edge => Vector3.DistanceSquared(rest.Points[edge.Item1], rest.Points[edge.Item2]) > 0).OrderBy(edge => edge.Item1).ThenBy(edge => edge.Item2).Select(edge => (edge.Item1, edge.Item2)).ToArray();
            restLengths = orderedEdges.Select(edge => (double)Vector3.Distance(rest.Points[edge.A], rest.Points[edge.B])).ToArray();
            influences = new bool[variables.Length, vertexBones.Length];
            auxiliaryInfluenced = new bool[vertexBones.Length];
            clothFaces = clothIds.Select(index => triangles[index]).ToArray();
            for (int variable = 0; variable < variables.Length; variable++)
            {
                for (int vertex = 0; vertex < vertexBones.Length; vertex++)
                {
                    for (int bone = vertexBones[vertex]; bone >= 0; bone = parents[bone])
                    {
                        if (bone == variables[variable])
                        {
                            influences[variable, vertex] = true;
                            auxiliaryInfluenced[vertex] = true;
                            break;
                        }
                    }
                }
            }
            contactPairs = clothIds.SelectMany(cloth => bodyIds.Where(body => !triangles[cloth].Any(vertex => triangles[body].Contains(vertex))).Select(body => (triangles[cloth], triangles[body]))).ToArray();
            RestFacts = ReadFacts(rest);
        }

        public NativeState Rest => rest;
        public Quaternion[] Canonical => canonical;
        public int[] Variables => variables;
        public Facts RestFacts { get; }

        public Facts ReadFacts(NativeState state)
        {
            Facts facts = new Facts { InvalidGeometry = state.InvalidGeometry };
            if (state.InvalidGeometry)
            {
                facts.Violation = double.PositiveInfinity;
                return facts;
            }
            foreach (Layer layer in layers)
            {
                Vector3 outward = Vector3.TransformNormal(layer.LocalNormal, state.Globals[vertexBones[layer.Body]]);
                Constraint? constraint = ReadPanelWitness(layer, state.Points, outward);
                if (constraint is null)
                {
                    continue;
                }
                facts.Constraints.Add(constraint);
                if (constraint.Value < 0)
                {
                    if (constraint.Hard)
                    {
                        facts.Violation += constraint.Value * constraint.Value;
                    }
                    else
                    {
                        facts.SoftViolation += constraint.Value * constraint.Value;
                    }
                }
            }
            foreach (var pair in contactPairs)
            {
                int[] cloth = pair.Cloth;
                int[] body = pair.Body;
                if (!Crosses(state.Points, cloth, body))
                {
                    continue;
                }
                facts.Crossings++;
                if (ImmutableBoundaryCrossing(state.Points, cloth, body))
                {
                    facts.ImmutableCrossings++;
                    continue;
                }
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
                Vector3 bodyPoint = state.Points[penetrating];
                double value = Vector3.Dot(state.Points[cloth[0]] - bodyPoint, normal);
                if (value < 0)
                {
                    Vector3 barycentric = PlaneProjectionWeights(bodyPoint, state.Points[cloth[0]], state.Points[cloth[1]], state.Points[cloth[2]]);
                    facts.Constraints.Add(new Constraint { Body = penetrating, Cloth = cloth, Weights = barycentric, Normal = normal, Value = value, Kind = "CurrentSignedVertexFace" });
                    facts.Violation += value * value;
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
                Quaternion current = state.Local[bone];
                Quaternion original = canonical[bone];
                double dot = (double)current.X * original.X + (double)current.Y * original.Y + (double)current.Z * original.Z + (double)current.W * original.W;
                double firstNorm = (double)current.X * current.X + (double)current.Y * current.Y + (double)current.Z * current.Z + (double)current.W * current.W;
                double secondNorm = (double)original.X * original.X + (double)original.Y * original.Y + (double)original.Z * original.Z + (double)original.W * original.W;
                double angle = 2 * Math.Acos(Math.Min(1, Math.Abs(dot) / Math.Sqrt(firstNorm * secondNorm)));
                facts.RestRotation += angle * angle;
            }
            return facts;
        }

        public bool HasCollapsedCorrection(NativeState original, NativeState candidate)
        {
            foreach (int[] face in clothFaces)
            {
                Vector3 before = Vector3.Cross(original.Points[face[1]] - original.Points[face[0]], original.Points[face[2]] - original.Points[face[0]]);
                Vector3 after = Vector3.Cross(candidate.Points[face[1]] - candidate.Points[face[0]], candidate.Points[face[2]] - candidate.Points[face[0]]);
                if (!float.IsFinite(after.X) || !float.IsFinite(after.Y) || !float.IsFinite(after.Z) || before.LengthSquared() > 0 && after.LengthSquared() == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public double[] RestDirection(NativeState state)
        {
            double[] result = new double[variables.Length * 3];
            for (int variable = 0; variable < variables.Length; variable++)
            {
                int bone = variables[variable];
                Matrix4x4 current = Matrix4x4.CreateFromQuaternion(state.Local[bone]);
                Matrix4x4 desired = Matrix4x4.CreateFromQuaternion(canonical[bone]);
                Quaternion relative = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Matrix4x4.Transpose(current) * desired));
                if (relative.W < 0 || relative.W == 0 && (relative.X < 0 || relative.X == 0 && (relative.Y < 0 || relative.Y == 0 && relative.Z < 0)))
                {
                    relative = new(-relative.X, -relative.Y, -relative.Z, -relative.W);
                }
                double length = Math.Sqrt((double)relative.X * relative.X + (double)relative.Y * relative.Y + (double)relative.Z * relative.Z);
                if (length == 0)
                {
                    continue;
                }
                double factor = 2 * Math.Atan2(length, relative.W) / length;
                result[variable * 3] = relative.X * factor;
                result[variable * 3 + 1] = relative.Y * factor;
                result[variable * 3 + 2] = relative.Z * factor;
            }
            return result;
        }

        public Constraint? ContactAt(Constraint witness, NativeState state)
        {
            int[] face = witness.Cloth;
            Vector3 cross = Vector3.Cross(state.Points[face[1]] - state.Points[face[0]], state.Points[face[2]] - state.Points[face[0]]);
            if (cross.LengthSquared() == 0)
            {
                return null;
            }
            Vector3 normal = Vector3.Normalize(cross);
            if (Vector3.Dot(normal, witness.Normal) < 0)
            {
                normal = -normal;
            }
            Vector3 body = state.Points[witness.Body];
            return new Constraint { Body = witness.Body, Cloth = face, Weights = PlaneProjectionWeights(body, state.Points[face[0]], state.Points[face[1]], state.Points[face[2]]), Normal = normal, Value = Vector3.Dot(state.Points[face[0]] - body, normal), Kind = witness.Kind };
        }

        private bool ImmutableBoundaryCrossing(Vector3[] points, int[] cloth, int[] body)
        {
            if (body.Any(vertex => auxiliaryInfluenced[vertex]))
            {
                return false;
            }
            Vector3 clothNormal = Vector3.Cross(points[cloth[1]] - points[cloth[0]], points[cloth[2]] - points[cloth[0]]);
            Vector3 bodyNormal = Vector3.Cross(points[body[1]] - points[body[0]], points[body[2]] - points[body[0]]);
            if (clothNormal.LengthSquared() == 0 || bodyNormal.LengthSquared() == 0)
            {
                return false;
            }
            for (int corner = 0; corner < 3; corner++)
            {
                int a = cloth[corner];
                int b = cloth[(corner + 1) % 3];
                if (!auxiliaryInfluenced[a] && !auxiliaryInfluenced[b] && Segment(points[a], points[b], points[body[0]], points[body[1]], points[body[2]]))
                {
                    return true;
                }
            }
            return false;
        }

        public double[] Jacobian(Constraint constraint, NativeState state)
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

        public Quaternion[] Apply(Quaternion[] original, double[] delta, double fraction)
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
}
