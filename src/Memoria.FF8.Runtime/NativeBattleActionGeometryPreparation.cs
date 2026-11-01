using System.Numerics;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleActionGeometryRules
{
    public static NativeBattleGraphicsState PrepareGeometry50D300(NativeBattleGraphicsState graphics, int actorIndex, ushort sourceMask, ushort physicalMask, NativeBattleActionGeometryGlobals globals, Func<NativeBattleGraphicsState, int, byte, short, NativeBattleVector> readBone)
    {
        if (actorIndex is < 0 or >= 7 || graphics.Models.IsDefault || graphics.Models.Length != 7)
        {
            throw new InvalidDataException("Native50D300 requires an actor in its seven-model physical bank.");
        }
        if (physicalMask == 0 || (physicalMask & ~127) != 0)
        {
            throw new NotSupportedException("Native50D300 requires a nonempty seven-bit physical target mask.");
        }
        int recipientIndex = BitOperations.Log2(physicalMask);
        NativeBattleActionGeometry context = new(checked((byte)actorIndex), sourceMask)
        {
            PhysicalTargetMask = physicalMask,
            TargetIndex = checked((byte)recipientIndex),
            ContactDistance = globals.ContactDistance,
            TargetYaw = globals.TargetYaw
        };
        bool group = BitOperations.PopCount(physicalMask) >= 2;
        for (int index = 0; index < 7; index++)
        {
            if ((physicalMask & (1 << index)) != 0)
            {
                NativeBattleGraphicsModel selected = graphics.Models[index];
                ushort flags = selected.Flags2 ?? throw new NotSupportedException("Native50D300 target grouping requires its model flags WORD2.");
                graphics = graphics with { Models = graphics.Models.SetItem(index, selected with { Flags2 = group ? (ushort)(flags | 0x10) : (ushort)(flags & ~0x10) }) };
            }
        }
        NativeBattleGraphicsModel model = graphics.Models[actorIndex];
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native50D300 action geometry requires its actor position/scale WORDs.");
        if (group)
        {
            return PrepareMatrices50D300(graphics with { Models = graphics.Models.SetItem(actorIndex, model with { Rotation12 = 0, Rotation14 = actorIndex >= 3 ? (short)2048 : (short)0, Rotation16 = 0 }), ActionContext = context }, readBone);
        }
        NativeBattleGraphicsModel recipient = graphics.Models[recipientIndex];
        NativeBattleModelTransform recipientTransform = recipient.Transform ?? throw new NotSupportedException("Native50D300 action geometry requires its recipient position/scale WORDs.");
        NativeBattleVector first = new(transform.Position28.X, transform.Angle36, transform.Position28.Z);
        NativeBattleVector second = recipient.ModelId4 is 96 or 97 ? readBone(graphics, recipientIndex, 241, 2048) : new(recipientTransform.Position28.X, recipientTransform.Angle36, recipientTransform.Position28.Z);
        second = second with { Y = recipientTransform.Angle36 };
        short scale = DistanceScale(first, second);
        short? yaw = actorIndex == recipientIndex ? context.TargetYaw : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(second.X - first.X, second.Z - first.Z));
        short actorYaw = actorIndex == recipientIndex ? actorIndex >= 3 ? (short)2048 : (short)0 : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(first.X - second.X, first.Z - second.Z));
        bool self = sourceMask == 1 << actorIndex;
        context = context with
        {
            DistanceScale = scale,
            UnadjustedDistance = self ? 1000 : 5000 * scale >> 12,
            ContactDistance = self ? 1000 : ContactDistance(scale, transform.Scale38, recipientTransform.Scale38),
            TargetYaw = yaw
        };
        model = model with
        {
            Rotation12 = actorIndex == recipientIndex ? (short)0 : model.Rotation12,
            Rotation14 = actorYaw,
            Rotation16 = actorIndex == recipientIndex ? (short)0 : model.Rotation16
        };
        return PrepareMatrices50D300(graphics with { Models = graphics.Models.SetItem(actorIndex, model), ActionContext = context }, readBone);
    }
}
