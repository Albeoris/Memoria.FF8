namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect20Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect20State State);
public sealed record NativeBattleEffect20LeafStep(NativeBattleEffect20Frame Frame, int Result);
public abstract record NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20Contact(int Index) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20Sound(NativeBattleModuleSound Sound) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20StageInitialize : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20StageFade(short Value) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20SpriteDraw(uint Address) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20ParticleDraw(uint Address) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20BodyDraw(uint Address) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20VibrationEnqueue(uint Address) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20VibrationStop(short Handle) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20ClearActorFlags124Bit2(int Actor) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20RingSequence(uint Address, int Count) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20Activate(int Actor) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20OverlayTask(uint Address) : NativeBattleEffect20Operation;
public sealed record NativeBattleEffect20Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect20Frame, NativeBattleEffect20Operation, NativeBattleEffect20Frame> Execute, Func<NativeBattleEffect20Frame, uint, NativeBattleEffect20LeafStep> AdvanceOverlay);
