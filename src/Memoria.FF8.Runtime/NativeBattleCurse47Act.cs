using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurse47ActorIdentity(byte Flags, byte? ModelId);
public sealed record NativeBattleCurse47AnchorStep(NativeBattleCurse47Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleCurse47ActPreparation(NativeBattleCurse47Frame Frame, NativeBattleEffectArena Context92);
public sealed record NativeBattleCurse47ActInputs(Func<NativeBattleCurse47Frame, uint, NativeBattleGfAnimationSource> ReadSource, Func<NativeBattleCurse47Frame, int, NativeBattleCurse47ActorIdentity> ReadActorIdentity, Func<NativeBattleCurse47Frame, int, short, int, NativeBattleCurse47AnchorStep> ReadAnchor, Func<NativeBattleCurse47Frame, uint, NativeBattleCurse47ActPreparation> PrepareDraw, Func<NativeBattleCurse47Frame, uint, NativeBattleEffectArena, NativeBattleGfAnimationNode, NativeBattleCurse47Frame> EmitNode);

public static class NativeBattleCurse47Act
{
    public const uint FirstCallback = 0x82B640;
    public const uint SecondCallback = 0x82DD00;
    public const uint MidpointCallback = 0x82DE00;

    public static NativeBattleCurse47Frame ObserveMode(NativeBattleCurse47Frame frame, uint director, short mode, NativeBattleActionPacketsState packets)
    {
        if (NativeBattleCurse47Controller.Read(frame, director).Callback != NativeBattleCurse47Director.Callback)
        {
            throw new InvalidDataException("Curse mode births require their actual director task.");
        }
        if (mode == 1)
        {
            uint[] callbacks = [FirstCallback, SecondCallback, MidpointCallback];
            uint[] sources = [0x17BB134, 0x17BDF6C, 0x17C0DA4];
            for (int index = 0; index < 3; index++)
            {
                NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(4, callbacks[index], 648, director, Selector(frame, packets));
                NativeBattleModulePoolTask task = child.Hierarchy.Read(child.Address).WriteDword(0x74, sources[index]).WriteDword(0x78, index == 2 ? 500u : 340u).WriteWord(0x80, (ushort)index).WriteWord(0x82, 0);
                frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Write(child.Address, task).Pools } };
            }
            return NativeBattleCurse47Controller.Append(frame, 5, 0x831870, 176, director, packets);
        }
        if (mode == 2)
        {
            NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(3, 0x82DF60, 676, director, Selector(frame, packets));
            NativeBattleModulePoolTask task = child.Hierarchy.Read(child.Address).WriteDword(0x30, 0x17B97A4).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 0);
            return frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Write(child.Address, task).Pools } };
        }
        return frame;
    }

    public static NativeBattleCurse47Step Advance(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47ActInputs inputs)
    {
        Require(frame, address);
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (task.Callback != MidpointCallback || unchecked((short)task.ReadWord(0x24)) >= 15)
                {
                    frame = Initialize(frame, address, inputs);
                    if (task.Callback == MidpointCallback)
                    {
                        int? first = FindActor(frame, 55, inputs);
                        if (first is { } firstActor)
                        {
                            NativeBattleCurse47AnchorStep a = inputs.ReadAnchor(frame, firstActor, 15, 0);
                            frame = a.Frame;
                            int? second = FindActor(frame, 56, inputs);
                            if (second is { } secondActor)
                            {
                                NativeBattleCurse47AnchorStep b = inputs.ReadAnchor(frame, secondActor, 15, 0);
                                frame = b.Frame;
                                task = Read(frame, address).WriteWord(0x1C, unchecked((ushort)(Midpoint(a.Position.X, b.Position.X)))).WriteWord(0x1E, unchecked((ushort)(Midpoint(a.Position.Y, b.Position.Y)))).WriteWord(0x20, unchecked((ushort)(Midpoint(a.Position.Z, b.Position.Z))));
                                frame = Put(frame, address, task);
                            }
                        }
                    }
                    else
                    {
                        int? selected = FindActor(frame, task.Callback == FirstCallback ? (byte)56 : (byte)55, inputs);
                        if (selected is { } actor)
                        {
                            NativeBattleCurse47AnchorStep anchored = inputs.ReadAnchor(frame, actor, 15, 0);
                            frame = anchored.Frame;
                            task = Read(frame, address).WriteWord(0x1C, unchecked((ushort)anchored.Position.X)).WriteWord(0x1E, unchecked((ushort)anchored.Position.Y)).WriteWord(0x20, unchecked((ushort)anchored.Position.Z));
                            frame = Put(frame, address, task);
                        }
                    }
                    task = Read(frame, address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, task.Callback == MidpointCallback ? (ushort)0xFF80 : (ushort)0xFF00);
                    frame = Put(frame, address, task);
                    frame = AdvanceAnimation(frame, address, inputs).Frame;
                    frame = Next(frame, address);
                }
                break;
            case 1:
                NativeBattleCurse47Step advanced = AdvanceAnimation(frame, address, inputs);
                frame = advanced.Frame;
                if (advanced.Result == 0)
                {
                    frame = Put(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Curse ACT signed phase exceeds its three actual handlers.");
        }
        return NativeBattleCurse47Controller.Finish(frame, address);
    }

    public static NativeBattleCurse47Step AdvanceAnimation(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47ActInputs inputs)
    {
        Require(frame, address);
        NativeBattleCurse47ActPreparation prepared = inputs.PrepareDraw(frame, address);
        frame = prepared.Frame;
        NativeBattleModulePoolTask task = Read(frame, address);
        int size = Size(task);
        NativeBattleGfAnimationSource source = Source(frame, task.ReadDword(0x94), inputs);
        NativeBattleGfAnimationAdvance advanced = Workspace(task, size).Advance701970(source, unchecked((int)frame.State.ReadDword(0x264D82C)), (workspace, node) =>
        {
            frame = Put(frame, address, Read(frame, address).WriteBytes(0x94, workspace.Data.AsSpan()));
            frame = inputs.EmitNode(frame, address, prepared.Context92, node);
            return Workspace(Read(frame, address), size);
        });
        frame = Put(frame, address, Read(frame, address).WriteBytes(0x94, advanced.Workspace.Data.AsSpan()));
        return new(frame, advanced.Remaining);
    }

    private static NativeBattleCurse47Frame Initialize(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47ActInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int size = Size(task);
        NativeBattleGfAnimationWorkspace workspace = Workspace(task, size).Initialize7016B0(Source(frame, task.ReadDword(0x74), inputs), size);
        return Put(frame, address, task.WriteBytes(0x94, workspace.Data.AsSpan()));
    }
    private static int? FindActor(NativeBattleCurse47Frame frame, byte modelId, NativeBattleCurse47ActInputs inputs)
    {
        for (int index = 0; index < 7; index++)
        {
            NativeBattleCurse47ActorIdentity identity = inputs.ReadActorIdentity(frame, index);
            if ((identity.Flags & 2) == 0)
            {
                continue;
            }
            byte selected = identity.ModelId ?? throw new NotSupportedException("Curse ACT placement requires the actual enabled actor model identity.");
            if (selected == modelId)
            {
                return index;
            }
        }
        return null;
    }
    private static Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> Selector(NativeBattleCurse47Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Curse ACT birth requires its actual captured action selectors.");
        return (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target);
    }
    private static int Midpoint(int a, int b) => (unchecked((short)a) + unchecked((short)b)) / 2;
    private static int Size(NativeBattleModulePoolTask task)
    {
        int size = unchecked((int)task.ReadDword(0x78));
        if (size < 8 || size > 648 - 0x94)
        {
            throw new InvalidDataException("Curse ACT workspace exceeds its actual physical task.");
        }
        return size;
    }
    private static NativeBattleGfAnimationSource Source(NativeBattleCurse47Frame frame, uint pointer, NativeBattleCurse47ActInputs inputs)
    {
        NativeBattleGfAnimationSource source = inputs.ReadSource(frame, pointer);
        if (source.Address != pointer)
        {
            throw new InvalidDataException("Curse ACT source disagrees with its retained original pointer.");
        }
        return source;
    }
    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task, int size) => new(task.ReadBytes(0x94, size).ToImmutableArray());
    private static NativeBattleModulePoolTask Read(NativeBattleCurse47Frame frame, uint address) => NativeBattleCurse47Controller.Read(frame, address);
    private static NativeBattleCurse47Frame Put(NativeBattleCurse47Frame frame, uint address, NativeBattleModulePoolTask task) => NativeBattleCurse47Controller.Write(frame, address, task);
    private static NativeBattleCurse47Frame Next(NativeBattleCurse47Frame frame, uint address) => Put(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static void Require(NativeBattleCurse47Frame frame, uint address)
    {
        frame.State.Validate();
        NativeBattleCureFamilyPool expected = NativeBattleCurse47State.Pools[4];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (frame.Effect.Binding.Effect != 47 || frame.Effect.Binding.Entry != NativeBattleCurse47Controller.Entry || Read(frame, address).Callback is not (FirstCallback or SecondCallback or MidpointCallback) || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity || address < pool.BankAddress || address >= pool.BankAddress + pool.Stride * pool.Slots.Length || (address - pool.BankAddress) % pool.Stride != 0)
        {
            throw new InvalidDataException("Curse ACT requires its original selected callback and 648-byte task bank.");
        }
    }
}
