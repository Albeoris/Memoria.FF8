using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Actual54A7F0/54B460 foot-to-car selection, independent of the historical471F70 field packet.</summary>
public sealed record NativeWorldCarBoardingReceipt([property: JsonRequired] string Rule, [property: JsonRequired] WorldTransition OriginalPacket, [property: JsonRequired] long SourceWorldFrame, [property: JsonRequired] byte FootEntity, [property: JsonRequired] byte VehicleShape, [property: JsonRequired] NativeSavedWorldPose ParkedPose)
{
    public const string BoardingRule = "ff8-en-car-boarding-54a7f0-54b460-v1";

    public static NativeWorldCarBoardingReceipt Create(WorldState source, WorldTransition originalPacket)
    {
        NativeFootWorldRuntimeState foot = source.NativeFootRuntime ?? throw new InvalidOperationException("Car boarding requires the actual controlled foot body.");
        NativeWorldParkedCar parked = foot.ParkedCar ?? throw new InvalidOperationException("Car boarding requires the actual retained parked car.");
        foot.Validate();
        NativeSavedWorldPose pose = new(parked.Position.X, -parked.Position.Z, checked((short)parked.Position.Y), checked((ushort)parked.BodyYaw));
        var receipt = new NativeWorldCarBoardingReceipt(BoardingRule, originalPacket, source.Frame, foot.EntityType, parked.Car.ShapeId, pose);
        receipt.Validate(parked.Car);
        return receipt;
    }

    public void Validate(NativeCarWorldRuntimeState car)
    {
        int expectedFoot = car.Allocation is { } allocation ? allocation.Objects[allocation.ControlledObjectIndex].EntityType : 1;
        int originalEntity = OriginalPacket is null ? -1 : unchecked((byte)OriginalPacket.Mode);
        bool validOriginalEntity = originalEntity == FootEntity || originalEntity == car.EntityType || originalEntity is >= 16 and <= 22;
        if (Rule != BoardingRule || OriginalPacket is null || !validOriginalEntity || SourceWorldFrame < 0 || FootEntity > 2 || FootEntity != expectedFoot || VehicleShape != car.ShapeId || ParkedPose is null || ParkedPose.X is < -131072 or >= 131072 || ParkedPose.RawZ is < -98304 or >= 98304 || ParkedPose.Yaw >= 4096)
        {
            throw new InvalidDataException("Native car boarding lost its original packet, selected foot object or actual parked vehicle identity.");
        }
    }
}
