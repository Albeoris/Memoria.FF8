namespace Memoria.FF8;

internal sealed class AutoPlayIdleDeadline(TimeProvider clock, TimeSpan limit) : Memoria.AutoPlay.AutoPlayIdleDeadline(clock, limit);
