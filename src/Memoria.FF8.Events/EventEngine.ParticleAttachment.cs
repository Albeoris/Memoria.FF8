using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeParticleAttachmentAnchor(int Slot, byte Value, int InstanceIndex, int ObjectId, short X, short Y, short Z);

public sealed partial class EventEngine
{
    private int PrepareNativeParticleSetInstance(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, FieldId, programHash) != true || Scene.NativeFieldInitialization.Stage != NativeFieldInitializationStage.Completed)
        {
            throw new NotSupportedException("PARTICLESET requires the actual finalized native physical instance layout; precompaction BYTE598 is unsupported.");
        }
        int instance = Scene.NativeActorObjectIds.IndexOf(fiber.ObjectId);
        if ((uint)instance >= byte.MaxValue)
        {
            throw new NotSupportedException("PARTICLESET requires its retained physical caller and executed compact BYTE598 instance.");
        }
        return instance;
    }

    /// <summary>Resolves the current actor-attached emitter origin consumed by473C50; this does not advance or render the PMD pool.</summary>
    public NativeParticleAttachmentAnchor? ReadNativeParticleAttachment(int slot)
    {
        if ((uint)slot >= 16)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        if (!Scene.NativeParticleSlots.TryGetValue(slot, out byte value))
        {
            throw new NotSupportedException("The current particle slot byte is unknown; absent sparse authority is not a disabled emitter.");
        }
        if (value == 0)
        {
            return null;
        }
        if ((value & 0x80) == 0)
        {
            throw new NotSupportedException("The current particle slot requires its resource-defined PMD emitter consumer.");
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, FieldId, programHash) != true || Scene.NativeFieldInitialization.Stage != NativeFieldInitializationStage.Completed)
        {
            throw new NotSupportedException("A live particle attachment requires its actual current compact native body layout.");
        }
        int instance = value & 0x7F;
        if ((uint)instance >= Scene.NativeActorObjectIds.Count)
        {
            throw new NotSupportedException("The particle attachment names an absent current native body instance.");
        }
        int objectId = Scene.NativeActorObjectIds[instance];
        ActorState actor = actors[objectId];
        FieldFixedPosition raw = ReadNativeScriptPosition(actor) ?? throw new NotSupportedException("The particle attachment requires the current raw native body coordinates.");
        //473C50 resolves the body each emission and divides toward zero before signed-WORD stores.
        // TODO(TD-008): This anchor does not claim PMD pool/RNG evolution or particle pixels.
        return new NativeParticleAttachmentAnchor(slot, value, instance, objectId, unchecked((short)(raw.X / 4096)), unchecked((short)(raw.Y / 4096)), unchecked((short)(raw.Z / 4096)));
    }
}
