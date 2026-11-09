using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public void RecordPreloadedMap(ushort map)
    {
        Process = Process with { PreloadedMap = map };
    }
}

public sealed partial class Ff7Session
{
    private FieldHostReply Preload(FieldPreloadCommand command)
    {
        if (command.Wait)
        {
            throw new NotSupportedException("FFVII native preload completion requires its original asynchronous readiness contract.");
        }
        Host.RecordPreloadedMap(command.Map);
        return new(true);
    }
}
