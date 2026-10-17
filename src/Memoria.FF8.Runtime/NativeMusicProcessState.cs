using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeMusicBufferProducer
{
    Field52CD10,
    WorldWmset5306E0,
    WorldFile543340,
    Battle52CE30
}

public enum NativeMusicSelectionAuthority
{
    Field,
    World,
    Battle
}

public sealed record NativeMusicBufferIdentity([property: JsonRequired] byte EncodedAkaoId, [property: JsonRequired] NativeMusicBufferProducer Producer)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Producer))
        {
            throw new InvalidDataException("Native music buffer lost its actual load producer.");
        }
    }
}

public sealed record NativeMusicBootstrap(int Version, string Rule, string ExecutableHash);
public sealed record NativeMusicProcessState(int Version, bool? Pending, byte? Requested213, byte? Complete214, byte? Bank201, [property: JsonRequired] byte? Bank202, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeMusicBootstrap? Bootstrap = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicSelectionAuthority? SelectionAuthority { get; init; }
    // Names follow the BYTE201 value selecting the buffer, not the opposite load destination.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicBufferIdentity? Buffer0 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicBufferIdentity? Buffer1 { get; init; }

    public void Validate()
    {
        if (Version != 1 || Bank201 is > 1 || Pending == true && (Requested213 is null || Complete214 != 1) || SelectionAuthority is { } authority && !Enum.IsDefined(authority))
        {
            throw new InvalidDataException("Native music process lost its actual selector, pending request or completion byte.");
        }
        if (Bootstrap is { } bootstrap && (bootstrap.Version != 1 || bootstrap.Rule != "ff8-en-music-new-game-470560-52ca90-v1" || bootstrap.ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash))
        {
            throw new InvalidDataException("Native music bootstrap lacks authenticated New Game provenance.");
        }
        Buffer0?.Validate();
        Buffer1?.Validate();
    }

    public NativeMusicBufferIdentity ReadBuffer(byte bank)
    {
        return (bank switch { 0 => Buffer0, 1 => Buffer1, _ => throw new NotSupportedException("Native music buffer selection requires BYTE201 zero or one.") }) ?? throw new NotSupportedException("Native music playback requires the selected buffer's retained AKAO identity; historical requests cannot establish it.");
    }

    public NativeMusicProcessState LoadBuffer(byte bank, NativeMusicBufferIdentity identity)
    {
        identity.Validate();
        return bank switch { 0 => this with { Buffer0 = identity }, 1 => this with { Buffer1 = identity }, _ => throw new NotSupportedException("Native music load requires BYTE201 zero or one.") };
    }
}
