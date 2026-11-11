namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private int[] ValidateNativePlayerGroundContacts(ActorState player, NativeJoinMovementProposal proposal, IReadOnlyList<NativePlayerGroundProbe> probes)
    {
        if (player.PushRadius is < 0 or > ushort.MaxValue || probes.Count != 3)
        {
            throw new NotSupportedException("Native player movement requires an unsigned radius and exactly three ordered ground probes; original state preserved.");
        }
        int[] offsets = [32, -32, 0];
        HashSet<int> pushContacts = [];
        for (int index = 0; index < probes.Count; index++)
        {
            NativePlayerGroundProbe probe = probes[index];
            int expectedX = unchecked(proposal.FixedX + player.PushRadius * NativeSine(proposal.Direction + offsets[index]));
            int expectedY = unchecked(proposal.FixedY - player.PushRadius * NativeCosine(proposal.Direction + offsets[index]));
            if (probe.FixedX != expectedX || probe.FixedY != expectedY || (probe.FixedX >> 12) is < short.MinValue or > short.MaxValue || (probe.FixedY >> 12) is < short.MinValue or > short.MaxValue || probe.NativeHeight is < short.MinValue or > short.MaxValue)
            {
                throw new NotSupportedException("Native player ground probes changed their original radius order, fixed coordinates or bounded plane height; original state preserved.");
            }
            (bool collision, int[] contacts) = EvaluateNativePlayerGroundContacts(player, probe);
            if (collision != probe.ActorCollision)
            {
                throw new NotSupportedException("Native player fan collision certificate differs from retained original peers; original state preserved.");
            }
            foreach (int contact in contacts)
            {
                ValidateNativePushDispatch(actors[contact]);
                pushContacts.Add(contact);
            }
        }
        return pushContacts.ToArray();
    }

    private (bool Collision, int[] PushContacts) EvaluateNativePlayerGroundContacts(ActorState player, NativePlayerGroundProbe probe, int ignoredContact = -1)
    {
        bool collision = false;
        List<int> contacts = [];
        if (player.ModelId is -1 or 65535)
        {
            return (false, []);
        }
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState peer = actors[objectId];
            if (!CanContactNativePlayerPeer(player, peer, ignoredContact))
            {
                continue;
            }
            if (!NativePlayerPeerTouchesGround(ReadNativePlayerContactPeer(player, peer), probe))
            {
                continue;
            }
            // Native47A720 sets +584 push contact independently of +588's
            // blocking flag. Ordinary leader collisions stop before steering.
            collision |= peer.CollisionEnabled;
            if (peer.PushEnabled && ResolvePartyActor(0)?.ObjectId == player.ObjectId)
            {
                contacts.Add(peer.ObjectId);
            }
        }
        return (collision, contacts.ToArray());
    }

    private readonly record struct NativePlayerContactPeer(int ObjectId, float X, float Y, FieldFixedPosition? Position, int Radius, bool CollisionEnabled);

    private static bool CanContactNativePlayerPeer(ActorState player, ActorState peer, int ignoredContact = -1)
    {
        return peer.ObjectId != player.ObjectId && peer.ObjectId != ignoredContact && peer.ModelId is not (-1 or 65535) && (peer.CollisionEnabled || peer.PushEnabled);
    }

    private NativePlayerContactPeer ReadNativePlayerContactPeer(ActorState player, ActorState peer)
    {
        if (peer.PushRadius is < 0 or > ushort.MaxValue)
        {
            throw new NotSupportedException("Native player contact peer radius exceeds its unsigned word; original state preserved.");
        }
        return new(peer.ObjectId, peer.X, peer.Y, ReadNativePlayerContactPosition(peer), (player.PushRadius + peer.PushRadius) / 2, peer.CollisionEnabled);
    }

    private Func<NativePlayerGroundProbe, bool> CaptureNativePlayerGroundCollision(ActorState player)
    {
        NativePlayerContactPeer[] peers = player.ModelId is -1 or 65535 ? [] : Scene.NativeActorObjectIds.Select(id => actors[id]).Where(peer => CanContactNativePlayerPeer(player, peer)).Select(peer => ReadNativePlayerContactPeer(player, peer)).ToArray();
        return probe =>
        {
            bool collision = false;
            foreach (NativePlayerContactPeer peer in peers)
            {
                // Every peer still participates in native geometry validation, including push-only peers.
                collision |= NativePlayerPeerTouchesGround(peer, probe) && peer.CollisionEnabled;
            }
            return collision;
        };
    }

    private static bool NativePlayerPeerTouchesGround(NativePlayerContactPeer peer, NativePlayerGroundProbe probe)
    {
        if (peer.Position is not { } raw)
        {
            if (!CertifyDistantNativePlayerPeer(peer, probe))
            {
                throw new NotSupportedException($"Native player fan may contact retained actor{peer.ObjectId} without preserved raw placement; original state preserved.");
            }
            return false;
        }
        int heightDifference = (raw.Z >> 12) - probe.NativeHeight;
        if (heightDifference is <= -127 or >= 128)
        {
            return false;
        }
        long dx = ((long)raw.X - probe.FixedX) >> 12;
        long dy = ((long)raw.Y - probe.FixedY) >> 12;
        long squared = dx * dx + dy * dy;
        if (squared > int.MaxValue)
        {
            throw new NotSupportedException("Native player peer distance needs an unproved wrapped comparison; original state preserved.");
        }
        return squared < unchecked(peer.Radius * peer.Radius);
    }

    private FieldFixedPosition? ReadNativePlayerContactPosition(ActorState actor)
    {
        if (actor.NativeJump is { } jump && actor.X == jump.FixedX / 4096f && actor.Y == jump.FixedY / 4096f && actor.Z == jump.FixedZ / 4096f)
        {
            return new FieldFixedPosition(jump.FixedX, jump.FixedY, jump.FixedZ);
        }
        if (ResolvePartyActor(0)?.ObjectId == actor.ObjectId && Scene.NativePlayerPosition is FieldFixedPosition leader && actor.X == leader.X / 4096f && actor.Y == leader.Y / 4096f && actor.Z == leader.Z / 4096f)
        {
            return leader;
        }
        // Versioned scripted state and integral original setup/trace positions
        // are already bounded by the common raw-position reader. A fractional
        // historical managed position is not promoted to native fixed bits.
        return ReadNativeScriptPosition(actor);
    }

    private static bool CertifyDistantNativePlayerPeer(NativePlayerContactPeer peer, NativePlayerGroundProbe probe)
    {
        if (!float.IsFinite(peer.X) || !float.IsFinite(peer.Y) || peer.X is < short.MinValue or > short.MaxValue || peer.Y is < short.MinValue or > short.MaxValue)
        {
            return false;
        }
        (long minX, long maxX) = NativePlayerPeerDifferenceInterval(peer.X, probe.FixedX);
        (long minY, long maxY) = NativePlayerPeerDifferenceInterval(peer.Y, probe.FixedY);
        long largestX = Math.Max(Math.Abs(minX), Math.Abs(maxX));
        long largestY = Math.Max(Math.Abs(minY), Math.Abs(maxY));
        if (largestX * largestX + largestY * largestY > int.MaxValue)
        {
            return false;
        }
        long closestX = minX <= 0 && maxX >= 0 ? 0 : Math.Min(Math.Abs(minX), Math.Abs(maxX));
        long closestY = minY <= 0 && maxY >= 0 ? 0 : Math.Min(Math.Abs(minY), Math.Abs(maxY));
        return closestX * closestX + closestY * closestY >= unchecked(peer.Radius * peer.Radius);
    }

    private static (long Min, long Max) NativePlayerPeerDifferenceInterval(float coordinate, int probe)
    {
        double fixedCoordinate = coordinate * 4096d;
        return ((long)Math.Floor((fixedCoordinate - 4096 - probe) / 4096), (long)Math.Floor((fixedCoordinate + 4096 - probe) / 4096));
    }
}
