using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>78DC50's84-byte25C9D28 scene and78DEE0's236-byte25C9C38 camera workspace.</summary>
public sealed record NativeBattleAngelWing84SceneMemory([property: JsonRequired] NativeBattleEffectArena Settings, [property: JsonRequired] NativeBattleMoogleCameraState? Camera)
{
    public static NativeBattleAngelWing84SceneMemory Initialize(NativeBattleVector actor, NativeBattleVector target, NativeBattleMoogleCameraState? retainedCamera)
    {
        NativeBattleEffectArena settings = NativeBattleEffectArena.Clear();
        settings = WritePosition(settings, 0, actor).MarkUnknown(6, 2);
        settings = WritePosition(settings, 8, target).MarkUnknown(14, 2);
        short dx = unchecked((short)(target.X - actor.X));
        short dz = unchecked((short)(target.Z - actor.Z));
        ushort angle = unchecked((ushort)((unchecked((ushort)NativeWorldScriptedPathMath.NativeAngle(dx, dz)) - 2048) & 4095));
        NativeBattleAngelWing84SceneMemory memory = new(settings, retainedCamera);
        return memory.WriteWord(74, angle);
    }

    public void Validate()
    {
        ValidateStorage(Settings, 84);
        if (Camera is not null)
        {
            Camera.Validate();
        }
    }

    public ushort Word(int offset)
    {
        RequireRange(offset, 2, 84);
        return BinaryPrimitives.ReadUInt16LittleEndian(Settings.Read(offset, 2));
    }

    public short SignedWord(int offset) => unchecked((short)Word(offset));

    public NativeBattleAngelWing84SceneMemory WriteWord(int offset, ushort value)
    {
        RequireRange(offset, 2, 84);
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return this with { Settings = Settings.Write(offset, bytes) };
    }

    public NativeBattleAngelWing84SceneMemory WriteDword(int offset, uint value)
    {
        RequireRange(offset, 4, 84);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return this with { Settings = Settings.Write(offset, bytes) };
    }

    public NativeBattleAngelWing84SceneMemory ClearCamera() => this with { Camera = NativeBattleMoogleCamera.Reset732AF0() };

    public bool CanRequestPhase(short phase)
    {
        return SignedWord(68) == SignedWord(64) && SignedWord(68) == phase - 1;
    }

    private static NativeBattleEffectArena WritePosition(NativeBattleEffectArena settings, int offset, NativeBattleVector position)
    {
        Span<byte> bytes = stackalloc byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)position.X));
        BinaryPrimitives.WriteInt16LittleEndian(bytes[2..], unchecked((short)position.Y));
        BinaryPrimitives.WriteInt16LittleEndian(bytes[4..], unchecked((short)position.Z));
        return settings.Write(offset, bytes);
    }

    private static void ValidateStorage(NativeBattleEffectArena storage, int length)
    {
        if (storage is null)
        {
            throw new InvalidDataException("Native Angel Wing scene has no actual memory owner.");
        }
        storage.Validate();
        if (storage.Regions.Any(region => region.Offset > length - region.Data.Length) || storage.UnknownRegions.Any(region => region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Native Angel Wing scene bytes leave their original physical workspace.");
        }
    }

    private static void RequireRange(int offset, int count, int length)
    {
        if (offset < 0 || offset > length - count)
        {
            throw new InvalidDataException("Native Angel Wing scene access leaves its physical workspace.");
        }
    }
}
