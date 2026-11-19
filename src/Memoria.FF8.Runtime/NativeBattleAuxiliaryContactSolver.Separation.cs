using System.Numerics;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAuxiliaryContactSolver
{
    private sealed record SeparationAxis(int Kind, int ClothEdge, int BodyEdge, int Sign);

    internal static double SeparationDerivative(Vector3 clothSupport, Vector3 bodySupport, Vector3 unitAxis, float axisLength, Vector3 axisVelocity, Vector3 clothVelocity, Vector3 bodyVelocity)
    {
        double along = DotVector(unitAxis, axisVelocity);
        double dx = (axisVelocity.X - unitAxis.X * along) / axisLength;
        double dy = (axisVelocity.Y - unitAxis.Y * along) / axisLength;
        double dz = (axisVelocity.Z - unitAxis.Z * along) / axisLength;
        Vector3 support = bodySupport - clothSupport;
        return DotVector(bodyVelocity - clothVelocity, unitAxis) + support.X * dx + support.Y * dy + support.Z * dz;
    }

    private static double DotVector(Vector3 first, Vector3 second) => (double)first.X * second.X + (double)first.Y * second.Y + (double)first.Z * second.Z;

    private sealed partial class PreparedGeometry
    {
        private Constraint? SelectSeparation(NativeState state, int[] cloth, int[] body, bool preferSharedOutward)
        {
            Vector3 clothNormal = Vector3.Cross(state.Points[cloth[1]] - state.Points[cloth[0]], state.Points[cloth[2]] - state.Points[cloth[0]]);
            Vector3 bodyNormal = Vector3.Cross(state.Points[body[1]] - state.Points[body[0]], state.Points[body[2]] - state.Points[body[0]]);
            if (Vector3.Cross(clothNormal, bodyNormal).LengthSquared() == 0)
            {
                return null;
            }
            Constraint? selected = null;
            double selectedCost = double.PositiveInfinity;
            bool selectedOutward = false;
            Vector3 restNormal = Vector3.Cross(restPoints[cloth[1]] - restPoints[cloth[0]], restPoints[cloth[2]] - restPoints[cloth[0]]);
            Vector3 restBody = (restPoints[body[0]] + restPoints[body[1]] + restPoints[body[2]]) / 3;
            int bodySide = Math.Sign(DotVector(restBody - restPoints[cloth[0]], restNormal));
            Vector3 outward = -bodySide * Vector3.Normalize(clothNormal);
            for (int kind = 0; kind < 3; kind++)
            {
                int clothEdges = kind == 2 ? 3 : 1;
                int bodyEdges = kind == 2 ? 3 : 1;
                for (int clothEdge = 0; clothEdge < clothEdges; clothEdge++)
                {
                    for (int bodyEdge = 0; bodyEdge < bodyEdges; bodyEdge++)
                    {
                        for (int sign = 1; sign >= -1; sign -= 2)
                        {
                            Constraint? candidate = SeparationAt(state, cloth, body, new(kind, clothEdge, bodyEdge, sign), outward);
                            if (candidate is null || candidate.Derivative is not { } derivative)
                            {
                                continue;
                            }
                            double norm = Dot(derivative, derivative);
                            if (norm == 0)
                            {
                                continue;
                            }
                            double cost = candidate.Value * candidate.Value / norm;
                            bool prefersOutward = preferSharedOutward && candidate.OutwardResponse > 0;
                            if (selected is null || prefersOutward && !selectedOutward || prefersOutward == selectedOutward && (cost < selectedCost || cost == selectedCost && candidate.Value > selected.Value))
                            {
                                selected = candidate;
                                selectedCost = cost;
                                selectedOutward = prefersOutward;
                            }
                        }
                    }
                }
            }
            return selected;
        }

        private Constraint? SeparationAt(NativeState state, int[] cloth, int[] body, SeparationAxis specification, Vector3 outward = default)
        {
            Vector3 first = state.Points[cloth[1]] - state.Points[cloth[0]];
            Vector3 second = state.Points[cloth[2]] - state.Points[cloth[0]];
            Vector3 bodyFirst = state.Points[body[1]] - state.Points[body[0]];
            Vector3 bodySecond = state.Points[body[2]] - state.Points[body[0]];
            int edgeA = specification.ClothEdge;
            int edgeB = specification.BodyEdge;
            Vector3 clothEdge = state.Points[cloth[(edgeA + 1) % 3]] - state.Points[cloth[edgeA]];
            Vector3 bodyEdge = state.Points[body[(edgeB + 1) % 3]] - state.Points[body[edgeB]];
            Vector3 raw = specification.Kind switch
            {
                0 => Vector3.Cross(first, second),
                1 => Vector3.Cross(bodyFirst, bodySecond),
                _ => Vector3.Cross(clothEdge, bodyEdge)
            };
            raw *= specification.Sign;
            float length = raw.Length();
            if (length == 0 || !float.IsFinite(length))
            {
                return null;
            }
            Vector3 axis = raw / length;
            int supportA = cloth[0];
            int supportB = body[0];
            for (int corner = 1; corner < 3; corner++)
            {
                if (DotVector(state.Points[cloth[corner]], axis) > DotVector(state.Points[supportA], axis))
                {
                    supportA = cloth[corner];
                }
                if (DotVector(state.Points[body[corner]], axis) < DotVector(state.Points[supportB], axis))
                {
                    supportB = body[corner];
                }
            }
            double[] row = new double[variables.Length * 3];
            for (int variable = 0; variable < variables.Length; variable++)
            {
                if (!influences[variable, cloth[0]] && !influences[variable, cloth[1]] && !influences[variable, cloth[2]])
                {
                    continue;
                }
                int bone = variables[variable];
                Matrix4x4 parent = parents[bone] < 0 ? Matrix4x4.Identity : state.Globals[parents[bone]];
                for (int coordinate = 0; coordinate < 3; coordinate++)
                {
                    Vector3 direction = Vector3.TransformNormal(coordinate == 0 ? Vector3.UnitX : coordinate == 1 ? Vector3.UnitY : Vector3.UnitZ, parent);
                    Vector3 Velocity(int vertex) => influences[variable, vertex] ? Vector3.Cross(direction, state.Points[vertex] - state.Pivots[bone]) : Vector3.Zero;
                    Vector3 velocity = specification.Kind switch
                    {
                        0 => Vector3.Cross(Velocity(cloth[1]) - Velocity(cloth[0]), second) + Vector3.Cross(first, Velocity(cloth[2]) - Velocity(cloth[0])),
                        1 => Vector3.Zero,
                        _ => Vector3.Cross(Velocity(cloth[(edgeA + 1) % 3]) - Velocity(cloth[edgeA]), bodyEdge)
                    };
                    row[variable * 3 + coordinate] = SeparationDerivative(state.Points[supportA], state.Points[supportB], axis, length, velocity * specification.Sign, Velocity(supportA), Vector3.Zero);
                }
            }
            Vector3 ProbeVelocity(int vertex) => auxiliaryInfluenced[vertex] ? outward : Vector3.Zero;
            Vector3 probeAxisVelocity = specification.Kind switch
            {
                0 => Vector3.Cross(ProbeVelocity(cloth[1]) - ProbeVelocity(cloth[0]), second) + Vector3.Cross(first, ProbeVelocity(cloth[2]) - ProbeVelocity(cloth[0])),
                1 => Vector3.Zero,
                _ => Vector3.Cross(ProbeVelocity(cloth[(edgeA + 1) % 3]) - ProbeVelocity(cloth[edgeA]), bodyEdge)
            };
            return new Constraint
            {
                Body = supportB,
                Cloth = cloth,
                BodyTriangle = body,
                EscapeAxis = specification,
                Normal = axis,
                Derivative = row,
                OutwardResponse = SeparationDerivative(state.Points[supportA], state.Points[supportB], axis, length, probeAxisVelocity * specification.Sign, ProbeVelocity(supportA), Vector3.Zero),
                Value = DotVector(state.Points[supportB] - state.Points[supportA], axis),
                Kind = "TriangleSeparation"
            };
        }
    }
}
