using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>77FAE0/77FB60's84-byte scene; source position copies include the native fourth WORD.</summary>
public sealed record NativeBattleRinoa86SceneMemory([property: JsonRequired] NativeBattleEffectArena Settings)
{
    public static NativeBattleRinoa86SceneMemory Initialize(byte caster, IReadOnlyList<NativeBattleGraphicsModel> models)
    {
        if (models.Count != 7 || caster >= 7)
        {
            throw new InvalidDataException("Native77FAE0 requires its actual seven model rows and caster.");
        }
        NativeBattleModelTransform actor = models[caster].Transform ?? throw new NotSupportedException("Native77FAE0 requires its caster's original position and fourth WORD.");
        short minimumX = 0;
        short maximumX = 0;
        short minimumZ = 0;
        short maximumZ = 0;
        bool any = false;
        for (int index = 3; index < 7; index++)
        {
            if ((models[index].Flags0 & 2) == 0)
            {
                continue;
            }
            NativeBattleVector point = (models[index].Transform ?? throw new NotSupportedException("Native77FB60 requires each loaded enemy's actual position.")).Position28;
            if (!any)
            {
                minimumX = maximumX = point.X;
                minimumZ = maximumZ = point.Z;
                any = true;
            }
            else
            {
                minimumX = Math.Min(minimumX, point.X);
                maximumX = Math.Max(maximumX, point.X);
                minimumZ = Math.Min(minimumZ, point.Z);
                maximumZ = Math.Max(maximumZ, point.Z);
            }
        }
        NativeBattleVector target = new(unchecked((short)((minimumX + maximumX) / 2)), 0, unchecked((short)((minimumZ + maximumZ) / 2)));
        NativeBattleRinoa86SceneMemory result = new(NativeBattleEffectArena.Clear());
        result = result.WriteWord(0, unchecked((ushort)actor.Position28.X)).WriteWord(2, unchecked((ushort)actor.Position28.Y)).WriteWord(4, unchecked((ushort)actor.Position28.Z)).WriteWord(6, unchecked((ushort)actor.Vertical34));
        result = result.WriteWord(8, unchecked((ushort)target.X)).WriteWord(12, unchecked((ushort)target.Z));
        short dx = unchecked((short)(target.X - actor.Position28.X));
        short dz = unchecked((short)(target.Z - actor.Position28.Z));
        return result.WriteWord(74, unchecked((ushort)((unchecked((ushort)NativeWorldScriptedPathMath.NativeAngle(dx, dz)) - 2048) & 4095)));
    }

    public ushort Word(int offset)
    {
        RequireRange(offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(Settings.Read(offset, 2));
    }

    public short SignedWord(int offset) => unchecked((short)Word(offset));

    public NativeBattleRinoa86SceneMemory WriteWord(int offset, ushort value)
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
        if (Settings.Regions.Any(region => region.Offset > 84 - region.Data.Length) || Settings.UnknownRegions.Any(region => region.Offset > 84 - region.Length))
        {
            throw new InvalidDataException("Native Rinoa86 scene bytes leave their original84-byte workspace.");
        }
    }

    private static void RequireRange(int offset, int count)
    {
        if (offset < 0 || offset > 84 - count)
        {
            throw new InvalidDataException("Native Rinoa86 scene access leaves its actual workspace.");
        }
    }
}
