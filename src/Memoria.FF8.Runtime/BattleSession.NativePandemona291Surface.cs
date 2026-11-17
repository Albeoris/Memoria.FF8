using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291SurfaceInputs(NativeBattleGfAnimationSource Source, NativeBattleActionMatrix Parent, ImmutableArray<byte>? TextureKinds, byte Phase, short DepthBias, int WorkspaceLength, int WorkspaceOffset = 0x1F0);
