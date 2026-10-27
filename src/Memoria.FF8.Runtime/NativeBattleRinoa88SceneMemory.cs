using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>768E20/768E20's84-byte scene; source position copies include the native fourth WORD.</summary>
public sealed record NativeBattleRinoa88SceneMemory([property: JsonRequired] NativeBattleEffectArena Settings)
{
    public NativeBattleMoogleCameraState? Camera { get; init; }
    public bool? CameraAnchorPaddingKnown { get; init; }

    public static NativeBattleRinoa88SceneMemory Initialize(byte caster, byte target, IReadOnlyList<NativeBattleGraphicsModel> models, NativeBattleMoogleCameraState? retainedCamera, bool? retainedAnchorPaddingKnown)
    {
        if (models.Count != 7 || caster >= 7 || target >= 7)
        {
            throw new InvalidDataException("Native768E20 requires its actual seven model rows and selected actors.");
        }
        int selected = target;
        if ((uint)selected >= models.Count)
        {
            throw new InvalidDataException("Native768E20 selected target leaves its physical bank.");
        }
        NativeBattleModelTransform actor = models[caster].Transform ?? throw new NotSupportedException("Native768E20 requires its caster's actual position owner.");
        NativeBattleModelTransform enemy = models[selected].Transform ?? throw new NotSupportedException("Native768E20 requires its selected target model's actual position and scale.");
        NativeBattleRinoa88SceneMemory result = new(NativeBattleEffectArena.Clear()) { Camera = retainedCamera, CameraAnchorPaddingKnown = retainedAnchorPaddingKnown };
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

    public NativeBattleRinoa88SceneMemory WriteWord(int offset, ushort value)
    {
        RequireRange(offset, 2);
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        Settings.Write(offset, bytes);
        return this;
    }

    public bool CanRequestPhase(short phase) => SignedWord(68) == SignedWord(64) && SignedWord(68) == phase - 1;

    public void Validate()
    {
        Settings.Validate();
        Camera?.Validate();
        if (Camera is not null && CameraAnchorPaddingKnown is null)
        {
            throw new InvalidDataException("Native Rinoa88 camera requires its actual anchor-padding provenance.");
        }
        Settings.ValidateExtent(84);
    }

    private static void RequireRange(int offset, int count)
    {
        if (offset < 0 || offset > 84 - count)
        {
            throw new InvalidDataException("Native Rinoa88 scene access leaves its actual workspace.");
        }
    }
}
