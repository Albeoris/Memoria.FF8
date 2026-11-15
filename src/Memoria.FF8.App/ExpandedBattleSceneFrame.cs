using System.Collections.Immutable;

using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record ExpandedBattleSceneFrame(NativeBattleCameraPose Camera, NativeBattleCameraProjection Projection, ImmutableDictionary<string, NativeBattleVector> Positions);
