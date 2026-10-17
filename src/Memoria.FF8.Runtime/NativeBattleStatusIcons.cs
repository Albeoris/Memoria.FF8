using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusIconResource([property: JsonRequired] byte StatusBit, [property: JsonRequired] short CameraX, [property: JsonRequired] short CameraY, [property: JsonRequired] short Scale, [property: JsonRequired] NativeBattleModuleResource Sprite);

/// <summary>The distinct C7EF54 status directory and C7EF70 camera-space placement rows, outside the42-entry module directory.</summary>
public sealed record NativeBattleStatusIconResources([property: JsonRequired] uint DefaultHeader, [property: JsonRequired] ImmutableArray<NativeBattleStatusIconResource> Entries)
{
    public void Validate()
    {
        if (DefaultHeader >> 24 < 128 || Entries.IsDefault || Entries.Length != 5)
        {
            throw new InvalidDataException("Native5711C0 requires its negative default descriptor and five original status sprite rows.");
        }
        ReadOnlySpan<byte> bits = [5, 6, 7, 8, 12];
        for (int index = 0; index < Entries.Length; index++)
        {
            NativeBattleStatusIconResource entry = Entries[index];
            if (entry is null || entry.StatusBit != bits[index] || entry.Sprite is null || entry.Sprite.Kind != 0)
            {
                throw new InvalidDataException("Native5711C0 status sprite rows must preserve their physical selector order and kind0 frame layout.");
            }
            entry.Sprite.Validate();
        }
    }
}

public sealed record NativeBattleStatusIconResult(byte TimerAfter, NativeBattleModuleDraw? Draw);
public sealed record NativeBattleStatusIconStep(NativeBattleGraphicsModel Model, ImmutableArray<NativeBattleModuleDraw> Draws);

public static class NativeBattleStatusIcons
{
    public static NativeBattleStatusIconStep Advance50A165(NativeBattleGraphicsModel model, uint graphicsFlags, NativeBattleStatusIconResources? resources, Func<NativeBattleGraphicsModel, NativeBattleVector> readAnchor)
    {
        if ((model.Flags0 & 2) == 0)
        {
            return new(model, []);
        }
        ushort flags2 = model.Flags2 ?? throw new NotSupportedException("Native502B56 status icons require the loaded model WORD2.");
        if ((flags2 & 4) != 0 || (graphicsFlags & 0x410) != 0)
        {
            return new(model, []);
        }
        uint statuses = model.Flags8 ?? throw new NotSupportedException("Native50A165 requires the actual model graphics status DWORD8.");
        if ((statuses & 0x11E0) == 0 || (model.Flags0 & 4) != 0)
        {
            return new(model, []);
        }
        NativeBattleStatusIconResources original = resources ?? throw new NotSupportedException("Native5711C0 requires its authenticated C7EF54 status sprite bindings; old missing metadata is unknown.");
        original.Validate();
        if (model.StatusTimers.IsDefault || model.StatusTimers.Length != 32)
        {
            throw new InvalidDataException("Native50A193 requires the original32 graphics status timer bytes.");
        }
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        for (int bit = 0; bit < 32; bit++)
        {
            if ((statuses & 0x11E0 & (1u << bit)) == 0)
            {
                continue;
            }
            byte timer = model.StatusTimers[bit] ?? throw new NotSupportedException($"Native50A193 requires the actual graphics status timer BYTE{bit}.");
            NativeBattleStatusIconResult result = Advance5711C0(model, bit, timer, graphicsFlags, original, readAnchor);
            if (result.Draw is { } draw)
            {
                draws.Add(draw);
            }
            model = model with { StatusTimers = model.StatusTimers.SetItem(bit, result.TimerAfter) };
        }
        return new(model, draws.ToImmutable());
    }

    public static NativeBattleStatusIconResult Advance5711C0(NativeBattleGraphicsModel model, int statusBit, byte timer, uint graphicsFlags, NativeBattleStatusIconResources resources, Func<NativeBattleGraphicsModel, NativeBattleVector> readAnchor)
    {
        resources.Validate();
        int index = statusBit switch
        {
            5 => 0,
            6 => 1,
            7 => 2,
            8 => 3,
            12 => 4,
            _ => -1
        };
        if (index < 0)
        {
            return new(unchecked((byte)((int)resources.DefaultHeader >> 24)), null);
        }
        NativeBattleStatusIconResource entry = resources.Entries[index];
        NativeBattleModuleSpriteFrame frame = entry.Sprite.SpriteFrame(timer);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native5710EE requires the model scaleWORD38 for its original icon depth displacement.");
        NativeBattleVector point = readAnchor(model);
        short distance = unchecked((short)(transform.Scale38 * 2000 >> 12));
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = entry.Sprite,
            SpriteFrame = frame,
            Phase = timer,
            Anchor = new(point.X, point.Y, point.Z, 0),
            Scale = entry.Scale,
            DepthOffset = -distance,
            SpriteFlags = 8,
            SpriteCameraXOffset = entry.CameraX,
            SpriteCameraYOffset = entry.CameraY
        };
        byte next = (graphicsFlags & 1) != 0 ? timer : frame.NextOffset < 0 ? (byte)0 : unchecked((byte)(timer + 1));
        return new(next, draw);
    }
}
