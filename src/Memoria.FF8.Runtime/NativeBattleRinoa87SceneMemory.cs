using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>774190/774190's84-byte scene; source position copies include the native fourth WORD.</summary>
public sealed record NativeBattleRinoa87SceneMemory([property: JsonRequired] NativeBattleEffectArena Settings)
{
    public NativeBattleMoogleCameraState? Camera { get; init; }

    public static NativeBattleRinoa87SceneMemory Initialize(byte caster, byte target, IReadOnlyList<NativeBattleGraphicsModel> models, Func<int, int> minimum501FF0, NativeBattleMoogleCameraState? retainedCamera)
    {
        if (models.Count != 7 || caster >= 7 || target >= 7)
        {
            throw new InvalidDataException("Native774190 requires its actual seven model rows and selected actors.");
        }
        int selected = minimum501FF0(target);
        if ((uint)selected >= models.Count)
        {
            throw new InvalidDataException("Native501FF0 selected model leaves its physical bank.");
        }
        NativeBattleModelTransform actor = models[caster].Transform ?? throw new NotSupportedException("Native774190 requires its caster's actual position owner.");
        NativeBattleModelTransform enemy = models[selected].Transform ?? throw new NotSupportedException("Native774190 requires its selected minimum model's actual position and scale.");
        NativeBattleRinoa87SceneMemory result = new(NativeBattleEffectArena.Clear()) { Camera = retainedCamera };
        Copy(0, actor);
        Copy(8, enemy);
        short dx = unchecked((short)(enemy.Position28.X - actor.Position28.X));
        short dz = unchecked((short)(enemy.Position28.Z - actor.Position28.Z));
        return result.WriteWord(74, unchecked((ushort)((unchecked((ushort)NativeWorldScriptedPathMath.NativeAngle(dx, dz)) - 2048) & 4095))).WriteWord(78, unchecked((ushort)enemy.Scale38));

        void Copy(int offset, NativeBattleModelTransform model)
        {
            result = result.WriteWord(offset, unchecked((ushort)model.Position28.X)).WriteWord(offset + 2, unchecked((ushort)model.Position28.Y)).WriteWord(offset + 4, unchecked((ushort)model.Position28.Z)).WriteWord(offset + 6, unchecked((ushort)model.Vertical34));
        }
    }

    public ushort Word(int offset)
    {
        RequireRange(offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(Settings.Read(offset, 2));
    }

    public short SignedWord(int offset) => unchecked((short)Word(offset));

    public NativeBattleRinoa87SceneMemory WriteWord(int offset, ushort value)
    {
        RequireRange(offset, 2);
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return this with { Settings = Settings.Write(offset, bytes) };
    }

    public bool CanRequestPhase(short phase) => SignedWord(68) == SignedWord(64) && SignedWord(68) == phase - 1;

    public void Validate()
    {
        Settings.Validate();
        Camera?.Validate();
        if (Settings.Regions.Any(region => region.Offset > 84 - region.Data.Length) || Settings.UnknownRegions.Any(region => region.Offset > 84 - region.Length))
        {
            throw new InvalidDataException("Native Rinoa87 scene bytes leave their original84-byte workspace.");
        }
    }

    private static void RequireRange(int offset, int count)
    {
        if (offset < 0 || offset > 84 - count)
        {
            throw new InvalidDataException("Native Rinoa87 scene access leaves its actual workspace.");
        }
    }
}
