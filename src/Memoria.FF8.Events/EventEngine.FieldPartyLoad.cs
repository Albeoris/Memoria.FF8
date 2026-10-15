using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativeFieldEntryStackContract
{
    public const string ProgramHash = "72999554E8FE70C0F524A06CB0671D7460625FF8E6A2CB7A4D13F607839CF660";
    public const string WalkmeshHash = "78FD701FA5DFDE72CD0AC9CB9A46291EDFA6BEECFA24E90A88FBBB7D0DA34EC2";

}

public sealed partial class EventEngine
{
    public Func<int, int, int, int?>? ResolveNativeTriangleHeight { get; set; }
    public Func<int, System.Numerics.Vector3?>? ResolveNativeTriangleCentroid { get; set; }
    public Func<FieldTransition, bool>? CanUseNativeFieldEntryStack { get; set; }
    public string? NativeFieldWalkmeshHash { get; set; }

    private sealed record NativeFieldActorPlacement(int ObjectId, int X, int Y, int Z, int Triangle, int Direction);
    private sealed record NativeFieldActorAnimation(int ObjectId, int Idle, int Frames);
    private sealed record NativeFieldPartyLoad(int[] Bindings, NativeFieldActorPlacement[] Placements, PartyTraceState Trace, FieldFixedPosition PlayerPosition, NativeFieldActorAnimation[] Animations, NativeFieldEntryStackReceipt? EntryStack = null, int? ResetLeaderDefaults = null);

    private NativeFieldPartyLoad? PrepareNativeFieldPartyLoad()
    {
        if (ResolveNativeTriangleHeight is null)
        {
            return null;
        }
        if (pendingArrival is not FieldTransition arrival)
        {
            return PrepareNativeOpeningCorridorPartyLoad();
        }
        if (ResolvePartyTraceTriangle is null || arrival.X is < short.MinValue or > short.MaxValue || arrival.Y is < short.MinValue or > short.MaxValue || arrival.X != MathF.Truncate(arrival.X) || arrival.Y != MathF.Truncate(arrival.Y))
        {
            throw new NotSupportedException("Native field entry requires the proved coordinate packet and original triangle/plane services.");
        }
        int[] retained = program!.NativeObjects.Take(program.NativeActorCount).Where(id => actors[id].Active).ToArray();
        int[] bindings = Enumerable.Repeat(0xFF, 3).ToArray();
        for (int slot = 0; slot < 3; slot++)
        {
            byte character = (byte)Scene.Party[slot];
            int match = Array.FindIndex(retained, id => (byte)actors[id].CharacterId == character);
            if (match >= 0)
            {
                bindings[slot] = match;
            }
        }
        if (bindings[0] == 0xFF)
        {
            throw new NotSupportedException("Native field entry has no retained leader binding.");
        }
        bool resetLeader = arrival.Triangle == short.MaxValue;
        int entryTriangle = resetLeader ? 0 : arrival.Triangle;
        System.Numerics.Vector3? centroid = null;
        if (resetLeader || arrival.X == short.MaxValue)
        {
            centroid = ResolveNativeTriangleCentroid?.Invoke(entryTriangle) ?? throw new NotSupportedException($"Native field entry requires the original triangle{entryTriangle} signed centroid.");
            if (new[] { centroid.Value.X, centroid.Value.Y, centroid.Value.Z }.Any(value => !float.IsFinite(value) || value is < short.MinValue or > short.MaxValue || value != MathF.Truncate(value)))
            {
                throw new NotSupportedException("Native field entry centroid exceeds its original signed WORD XYZ contract.");
            }
        }
        NativeFieldActorPlacement[] placements = new NativeFieldActorPlacement[retained.Length];
        for (int index = 0; index < retained.Length; index++)
        {
            ActorState actor = actors[retained[index]];
            bool partyActor = bindings.Contains(index);
            int x = partyActor ? (int)(centroid?.X ?? arrival.X) : (int)actor.X;
            int y = partyActor ? (int)(centroid?.Y ?? arrival.Y) : (int)actor.Y;
            int triangle = partyActor ? entryTriangle : actor.WalkmeshTriangle;
            int height = partyActor && centroid is System.Numerics.Vector3 center ? (int)center.Z : ResolveNativeTriangleHeight(triangle, x, y) ?? throw new NotSupportedException($"Native field entry actor{actor.ObjectId} has no original triangle{triangle} plane.");
            placements[index] = new NativeFieldActorPlacement(actor.ObjectId, x, y, height, triangle, partyActor ? arrival.Direction : (byte)actor.Direction);
        }
        NativeFieldActorPlacement leader = placements[bindings[0]];
        PartyTraceSample?[] first = new PartyTraceSample?[64];
        PartyTraceSample?[] second = new PartyTraceSample?[64];
        if (Scene.FieldLoadFirstRecordZero is PartyTraceSample oldFirst)
        {
            first[0] = oldFirst with { X = (short)leader.X, Y = (short)leader.Y, FirstFlag = 1, SecondFlag = 1 };
        }
        if (Scene.FieldLoadSecondRecordZero is PartyTraceSample oldSecond)
        {
            second[0] = oldSecond with { X = (short)leader.X, Y = (short)leader.Y, FirstFlag = 1, SecondFlag = 1 };
        }
        int fixedX = checked(leader.X * 4096);
        int fixedY = checked(leader.Y * 4096);
        int speed = 69020 * 20 >> 9;
        int firstObservableRecord = bindings[2] != 0xFF ? 34 : bindings[1] != 0xFF ? 49 : 64;
        bool stackCarry = NativeRuntimeProfile is { IsVerified: true } && NativeFieldWalkmeshHash is { Length: 64 } hash && hash.All(Uri.IsHexDigit) && CanUseNativeFieldEntryStack?.Invoke(arrival) == true;
        int? winnerLocal = null;
        int firstWinnerRecord = -1;
        int firstCarriedRecord = -1;
        int carriedTriangle = -1;
        for (int index = 63; index >= 1; index--)
        {
            int x = fixedX / 4096;
            int y = fixedY / 4096;
            int? triangle = ResolvePartyTraceTriangle(x, y, leader.Z);
            if (stackCarry)
            {
                if (triangle is int winner)
                {
                    winnerLocal = winner;
                    if (firstWinnerRecord < 0)
                    {
                        firstWinnerRecord = index;
                    }
                }
                else if (winnerLocal is int residue)
                {
                    // At this exact 47A900 call site, 477B00's winner local
                    // B-88 survives the intervening 47A680 and loop tail.
                    // No packet triangle or earlier loader initializes it.
                    triangle = residue;
                    if (firstCarriedRecord < 0)
                    {
                        firstCarriedRecord = index;
                        carriedTriangle = residue;
                    }
                }
            }
            if (triangle is int selected)
            {
                int height = ResolveNativeTriangleHeight(selected, (short)x, (short)y) ?? throw new NotSupportedException("Native field entry trace selected an invalid original plane.");
                bool fallback = height >= leader.Z + 310 || height <= leader.Z - 310;
                PartyTraceSample sample = new((short)(fallback ? leader.X : x), (short)(fallback ? leader.Y : y), (short)(fallback ? leader.Z : height), selected, leader.Direction, 0);
                first[index] = sample;
                second[index] = sample;
            }
            else if (index >= firstObservableRecord)
            {
                throw new NotSupportedException($"Native field entry follower would observe record{index} with no containing original triangle.");
            }
            // Without a proved call-site stack history, a no-winner WORD
            // remains opaque; backward stepping is independent of lookup.
            fixedX = unchecked(fixedX + speed * NativeSine(leader.Direction) / -256);
            fixedY = unchecked(fixedY + speed * NativeCosine(leader.Direction) / 256);
        }
        NativeFieldEntryStackReceipt? entryStack = null;
        if (stackCarry && firstCarriedRecord >= 0)
        {
            entryStack = new NativeFieldEntryStackReceipt(2, NativeFieldWalkmeshHash!, arrival, firstWinnerRecord, firstCarriedRecord, carriedTriangle);
        }
        for (int slot = 1; slot < 3; slot++)
        {
            if (bindings[slot] == 0xFF)
            {
                continue;
            }
            PartyTraceSample sample = (slot == 1 ? first[49] : second[34])!;
            NativeFieldActorPlacement original = placements[bindings[slot]];
            placements[bindings[slot]] = original with { X = sample.X, Y = sample.Y, Z = sample.Z, Triangle = sample.Triangle };
        }
        return new NativeFieldPartyLoad(bindings, placements, new PartyTraceState(0, 15, 30, first, second), new FieldFixedPosition(leader.X * 4096, leader.Y * 4096, leader.Z * 4096), PrepareNativeFieldAnimations(retained, resetLeader ? leader.ObjectId : null), entryStack, resetLeader ? leader.ObjectId : null);
    }

    private NativeFieldPartyLoad? PrepareNativeOpeningCorridorPartyLoad()
    {
        // Original startup kind0 fills all records from the initialized leader,
        // rather than manufacturing a coordinate-arrival packet or prior trail.
        if (FieldId != 229 || !Scene.Party.SequenceEqual(new[] { 0, 255, 255 }) || program is null)
        {
            return null;
        }
        int[] retained = program.NativeObjects.Take(program.NativeActorCount).Where(id => actors[id].Active).ToArray();
        int leaderIndex = Array.FindIndex(retained, id => actors[id].CharacterId == 0);
        if (leaderIndex < 0)
        {
            throw new NotSupportedException("Opening corridor startup has no initialized retained player.");
        }
        NativeFieldActorPlacement[] placements = new NativeFieldActorPlacement[retained.Length];
        for (int index = 0; index < retained.Length; index++)
        {
            ActorState actor = actors[retained[index]];
            if (actor.NativeMotionAuthority is not { Version: 1, FacingHold: 0 } || actor.X != MathF.Truncate(actor.X) || actor.Y != MathF.Truncate(actor.Y) || actor.X is < short.MinValue or > short.MaxValue || actor.Y is < short.MinValue or > short.MaxValue || actor.Direction != MathF.Truncate(actor.Direction) || actor.Direction is < 0 or > byte.MaxValue)
            {
                throw new NotSupportedException("Opening corridor startup requires original initialized actor coordinates and motion scalars.");
            }
            int height = ResolveNativeTriangleHeight!(actor.WalkmeshTriangle, (int)actor.X, (int)actor.Y) ?? throw new NotSupportedException($"Opening corridor actor{actor.ObjectId} has no original explicit-triangle plane.");
            placements[index] = new NativeFieldActorPlacement(actor.ObjectId, (int)actor.X, (int)actor.Y, height, actor.WalkmeshTriangle, (byte)actor.Direction);
        }
        NativeFieldActorPlacement leader = placements[leaderIndex];
        ActorState player = actors[leader.ObjectId];
        if (player.PushRadius != 48 || !TryReadNativeBaseAnimations(player, out int idle, out int walk, out int run) || new[] { idle, walk, run }.Any(animation => AnimationFrameCount?.Invoke(player.RenderModelId, animation) is not > 0))
        {
            throw new NotSupportedException("Opening corridor startup requires original player radius and full idle/walk/run metadata.");
        }
        PartyTraceSample sample = new((short)leader.X, (short)leader.Y, (short)leader.Z, leader.Triangle, leader.Direction, 0);
        PartyTraceSample?[] first = Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray();
        PartyTraceSample?[] second = Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray();
        return new NativeFieldPartyLoad([leaderIndex, 255, 255], placements, new PartyTraceState(0, 15, 30, first, second), new FieldFixedPosition(leader.X * 4096, leader.Y * 4096, leader.Z * 4096), PrepareNativeFieldAnimations(retained));
    }

    private bool ValidNativeFieldEntryStackReceipt(NativeFieldEntryStackReceipt receipt)
    {
        if (receipt.Version != 2 || receipt.Arrival is not FieldTransition arrival || arrival.FieldId != FieldId || !float.IsFinite(arrival.X) || !float.IsFinite(arrival.Y) || arrival.X is < short.MinValue or > short.MaxValue || arrival.Y is < short.MinValue or > short.MaxValue || arrival.X != MathF.Truncate(arrival.X) || arrival.Y != MathF.Truncate(arrival.Y) || arrival.Triangle is < 0 or > ushort.MaxValue || arrival.Direction is < 0 or > byte.MaxValue || receipt.WalkmeshHash != NativeFieldWalkmeshHash || NativeRuntimeProfile is not { IsVerified: true } || CanUseNativeFieldEntryStack?.Invoke(arrival) != true || ResolvePartyTraceTriangle is null || ResolveNativeTriangleHeight is null || receipt.FirstWinnerRecord is < 1 or > 63 || receipt.FirstCarriedRecord < 1 || receipt.FirstCarriedRecord >= receipt.FirstWinnerRecord || receipt.CarriedTriangle < 0)
        {
            return false;
        }
        int triangle = arrival.Triangle == short.MaxValue ? 0 : arrival.Triangle;
        System.Numerics.Vector3? centroid = arrival.X == short.MaxValue || arrival.Triangle == short.MaxValue ? ResolveNativeTriangleCentroid?.Invoke(triangle) : null;
        if ((arrival.X == short.MaxValue || arrival.Triangle == short.MaxValue) && centroid is null)
        {
            return false;
        }
        int x = (int)(centroid?.X ?? arrival.X);
        int y = (int)(centroid?.Y ?? arrival.Y);
        int? height = centroid is System.Numerics.Vector3 center ? (int)center.Z : ResolveNativeTriangleHeight(triangle, x, y);
        if (height is not int z || x is < short.MinValue or > short.MaxValue || y is < short.MinValue or > short.MaxValue)
        {
            return false;
        }
        int fixedX = x * 4096;
        int fixedY = y * 4096;
        int? winner = null;
        int firstWinner = -1;
        for (int index = 63; index >= 1; index--)
        {
            int? selected = ResolvePartyTraceTriangle(fixedX / 4096, fixedY / 4096, z);
            if (selected is int known)
            {
                winner = known;
                if (firstWinner < 0)
                {
                    firstWinner = index;
                }
            }
            else if (winner is int carried)
            {
                return firstWinner == receipt.FirstWinnerRecord && index == receipt.FirstCarriedRecord && carried == receipt.CarriedTriangle;
            }
            int speed = 69020 * 20 >> 9;
            fixedX = unchecked(fixedX + speed * NativeSine(arrival.Direction) / -256);
            fixedY = unchecked(fixedY + speed * NativeCosine(arrival.Direction) / 256);
        }
        return false;
    }

    private NativeFieldActorAnimation[] PrepareNativeFieldAnimations(int[] retained, int? resetLeader = null)
    {
        List<NativeFieldActorAnimation> animations = [];
        foreach (int objectId in retained)
        {
            ActorState actor = actors[objectId];
            if (actor.RenderModelId is -1 or ushort.MaxValue)
            {
                continue;
            }
            int idle = 0;
            if ((objectId != resetLeader && !TryReadNativeBaseAnimations(actor, out idle, out _, out _)) || AnimationFrameCount?.Invoke(actor.RenderModelId, idle) is not int frames || frames <= 0)
            {
                throw new NotSupportedException($"Native normal field loader actor{objectId} requires decoded full idle animation metadata before initialization.");
            }
            animations.Add(new NativeFieldActorAnimation(objectId, idle, frames));
        }
        return animations.ToArray();
    }

    private void ApplyNativeFieldPartyLoad(NativeFieldPartyLoad load)
    {
        if (load.ResetLeaderDefaults is int leaderObjectId)
        {
            // Native477C90's triangle7FFF branch selects triangle0 and original locomotion defaults.
            ActorState leader = actors[leaderObjectId];
            leader.AnimationSpeed = 16;
            if (leader.NativeAnimationHeader is { } animation)
            {
                leader.NativeAnimationHeader = animation with { Increment = 16 };
            }
            leader.BaseAnimationId = 0;
            leader.WalkAnimationId = 1;
            leader.RunAnimationId = 2;
            leader.NativeBaseAnimationVersion = 1;
            leader.MovementSpeed = 69020 * 20 >> 9;
            leader.PushRadius = 48;
        }
        foreach (NativeFieldActorPlacement placement in load.Placements)
        {
            ActorState actor = actors[placement.ObjectId];
            actor.X = placement.X;
            actor.Y = placement.Y;
            actor.Z = placement.Z;
            actor.WalkmeshTriangle = placement.Triangle;
            actor.Direction = placement.Direction;
        }
        foreach (NativeFieldActorAnimation animation in load.Animations)
        {
            // 52B4F0(1) calls 52B840(0): every retained model selects idle591,
            // full decoded range and phase0, with its native looping flag set.
            ActorState actor = actors[animation.ObjectId];
            actor.AnimationId = animation.Idle;
            actor.AnimationFirst = 0;
            actor.AnimationLast = animation.Frames - 1;
            actor.AnimationFrame = 0;
            actor.AnimationKeep = false;
            actor.AnimationLoop = true;
            actor.AnimationPlaying = true;
            actor.BaseFirstFrame = 0;
            actor.BaseLastFrame = animation.Frames - 1;
        }
        Scene.FieldPartyNativeActorIds.Clear();
        Scene.FieldPartyNativeActorIds.AddRange(load.Bindings);
        foreach (int binding in load.Bindings.Where(binding => binding != byte.MaxValue).Distinct())
        {
            ActorState member = actors[Scene.NativeActorObjectIds[binding]];
            NativeActorPartyFlags flags = member.NativePartyFlags ?? throw new NotSupportedException("Native52BC00 party binding requires allocated actor DWORD352 mask ownership.");
            member.NativePartyFlags = flags with { AutoAnimation4 = true };
        }
        for (int slot = 0; slot < 3; slot++)
        {
            variables[194 + slot] = load.Bindings[slot];
        }
        RecordPartyBindingMemory();
        Scene.PartyTrace = load.Trace;
        Scene.NativePlayerPosition = load.PlayerPosition;
        // Native 477C90 clears 1CDBFED during the actual normal field setup.
        Scene.NativePlayerEarlyRetryState = 0;
        Scene.NativePartyTraceVersion = 1;
        Scene.NativeFieldLoadVersion = 1;
        Scene.NativeFieldEntryStack = load.EntryStack;
        Scene.FieldLoadFirstRecordZero = null;
        Scene.FieldLoadSecondRecordZero = null;
        pendingArrival = null;
    }
}
