namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSandStorm155Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleSandStorm155State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleSandStorm155AnchorStep(NativeBattleSandStorm155Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleSandStorm155PointStep(NativeBattleSandStorm155Frame Frame, NativeBattleVector Point);
public sealed record NativeBattleSandStorm155Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleSandStorm155Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleSandStorm155Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleSandStorm155Frame, int, NativeBattleActionMatrix> ReadModelWorld, Func<NativeBattleSandStorm155Frame, int, NativeBattleSandStorm155AnchorStep> ReadAnchor571400, Func<NativeBattleSandStorm155Frame, ushort, NativeBattleSandStorm155PointStep> ReadTargetCenter, Func<NativeBattleSandStorm155Frame, NativeBattleSandStorm155Operation, NativeBattleSandStorm155Frame> Execute);

public abstract record NativeBattleSandStorm155Operation;
public sealed record NativeBattleSandStorm155Sound(uint Value, uint Flags, byte Pan) : NativeBattleSandStorm155Operation;
public sealed record NativeBattleSandStorm155ContactsAll(int HeaderIndex) : NativeBattleSandStorm155Operation;
public sealed record NativeBattleSandStorm155DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleSandStorm155Operation;
public sealed record NativeBattleSandStorm155DrawMesh88(uint TemporaryAddress, uint SourceAddress) : NativeBattleSandStorm155Operation;
