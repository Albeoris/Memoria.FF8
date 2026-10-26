namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218IoPoll(NativeBattleEffect218Frame Frame, bool Busy);
public sealed record NativeBattleEffect218VibrationStep(NativeBattleEffect218Frame Frame, int Handle);
public sealed record NativeBattleEffect218Inputs(Func<NativeBattleEffect218Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect218Frame, NativeBattleEffect218IoPoll> PollIo508500, Func<NativeBattleEffect218Frame, NativeBattleEffect218VibrationStep> StartVibration, Func<NativeBattleEffect218Frame, int, bool> IsVibrationActive, Func<NativeBattleEffect218Frame, NativeBattleEffect218Operation, NativeBattleEffect218Frame> Execute);

public abstract record NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218Camera63E960 : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218LoadStream501860(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218PlayStream5018C0 : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218Sound501330(uint SourceAddress) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218QueueFile508480(short FileId, uint DestinationAddress) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218UploadTim505E30(uint SourceAddress) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218StageFlags(byte AndMask, byte OrMask) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218Text4876F0(int Actor, ushort Text, int Duration) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218SelectSequence505C00(uint ModelAddress, ushort Sequence) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218ModelFlags(uint ModelAddress, byte AndMask, byte OrMask) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218CapturePoint571400(uint ModelAddress, uint DestinationAddress) : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218Contact506690 : NativeBattleEffect218Operation;
public sealed record NativeBattleEffect218StopVibration4A2940(int Handle) : NativeBattleEffect218Operation;
