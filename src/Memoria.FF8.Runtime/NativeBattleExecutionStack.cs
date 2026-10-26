using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>An explicitly selected native process observation, rather than PE loader storage or a default stack address.</summary>
public sealed record NativeBattleExecutionStack([property: JsonRequired] uint FrameCallbackEsp, [property: JsonRequired] uint FrameReturnAddress, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] string CaptureSha256, [property: JsonRequired] int CapturedProcessId, [property: JsonRequired] int CapturedThreadId)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHeroCrtStackContract? HeroCrtStack { get; init; }

    public void Validate()
    {
        if (FrameCallbackEsp < 0x100 || (FrameCallbackEsp & 3) != 0 || FrameReturnAddress != 0x569C3D || ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || CaptureSha256 is null || CaptureSha256.Length != 64 || !CaptureSha256.All(Uri.IsHexDigit) || CapturedProcessId <= 0 || CapturedThreadId <= 0)
        {
            throw new InvalidDataException("Native execution stack requires its observed common WinMain frame callsite, aligned ESP, executable/capture identities and native process/thread lineage.");
        }
    }

    public NativeBattleStackFrame ReadCommonFrame()
    {
        Validate();
        return new(FrameCallbackEsp);
    }
}

/// <summary>A physical x86 stack reference; cdecl pending arguments are distinct from saved registers and locals.</summary>
public readonly record struct NativeBattleStackFrame(uint EntryEsp)
{
    public NativeBattleStackFrame EnterCdecl(int localBytes, int savedRegisterCount, int pendingArgumentBytes)
    {
        if (localBytes < 0 || savedRegisterCount < 0 || pendingArgumentBytes < 0 || (localBytes & 3) != 0 || (pendingArgumentBytes & 3) != 0)
        {
            throw new InvalidDataException("Native cdecl stack operands require nonnegative aligned locals and pending arguments.");
        }
        long bytes = checked((long)localBytes + 4L * savedRegisterCount + pendingArgumentBytes + 4);
        return new(checked((uint)(EntryEsp - bytes)));
    }

    public uint AddressAtOffset(int offset)
    {
        return checked((uint)((long)EntryEsp + offset));
    }
}
