namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8FrontAppend(NativeBattleDoom8Frame Frame, uint Address);

public static class NativeBattleDoom8Fronts
{
    public static NativeBattleDoom8Frame SpawnObservedMode(NativeBattleDoom8Frame frame, uint director, NativeBattleActionPacketsState packets, uint defaultColor44, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> execute)
    {
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || NativeBattleDoom8Controller.Read(frame, director).Callback != NativeBattleDoom8Controller.DirectorCallback)
        {
            throw new InvalidDataException("Doom mode creation requires its actual observed director.");
        }
        short mode = unchecked((short)frame.State.SharedWord(0x40));
        if (mode is not (2 or 4))
        {
            return frame;
        }
        ArgumentNullException.ThrowIfNull(execute);
        packets.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Doom mode creation requires its actual action parameters.");
        NativeBattleEffectTaskActors Select(uint pointer, sbyte header, sbyte target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, header, target);
        if (mode == 2)
        {
            frame = NativeBattleDoom8Nodes.Append8C5810(frame, director, NativeBattleDoom8Nodes.Callback, NativeBattleDoom8Nodes.SourceAddress, 0, 45, 2, Select).Frame;
            NativeBattleDoom8FrontAppend reaper = Append(frame, 8, NativeBattleDoom8Reaper.Callback, 320, director, Select);
            frame = execute(reaper.Frame, new NativeBattleDoom8ReaperInitializeModel(reaper.Address, 0x1636CE8, 3, defaultColor44));
            frame = Append(frame, 5, 0x8CD4B0, 176, director, Select).Frame;
            frame = Append(frame, 6, 0x8CDED0, 88, director, Select).Frame;
            return Append(frame, 5, 0x8CC1E0, 176, director, Select).Frame;
        }
        if (frame.State.ReadDword(NativeBattleDoom8Io.ControlPointerGlobal) != NativeBattleDoom8Io.ControlAddress)
        {
            throw new NotSupportedException("Doom mode four requires its executed IO control pointer.");
        }
        uint source = frame.State.ReadDword(NativeBattleDoom8Io.ControlAddress + 0x124);
        frame = Append8C5860(frame, director, 0x8C58B0, source, 0xC0, 0, 0, Select).Frame;
        NativeBattleDoom8FrontAppend overlay = Append(frame, 7, 0x8DDC30, 64, director, Select);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(overlay.Frame, overlay.Address).WriteDword(0x34, 0x163ACB4).WriteWord(0x38, 26);
        frame = NativeBattleDoom8Controller.Write(overlay.Frame, overlay.Address, task);
        return Append(frame, 5, 0x8CC2D0, 176, director, Select).Frame;
    }

    public static NativeBattleDoom8FrontAppend Append8C5860(NativeBattleDoom8Frame frame, uint parent, uint callback, uint source, short workspaceLength, short argument80, short argument82, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || callback is not (0x8C58B0 or 0x8C8050 or 0x8C8790))
        {
            throw new InvalidDataException("Doom ACT creation requires its actual callback and module.");
        }
        NativeBattleDoom8FrontAppend appended = Append(frame, 4, callback, 1112, parent, selectActors);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(appended.Frame, appended.Address).WriteWord(0x80, unchecked((ushort)argument80)).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)(int)workspaceLength)).WriteWord(0x82, unchecked((ushort)argument82));
        return appended with { Frame = NativeBattleDoom8Controller.Write(appended.Frame, appended.Address, task) };
    }

    private static NativeBattleDoom8FrontAppend Append(NativeBattleDoom8Frame frame, int pool, uint callback, int length, uint parent, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, length, parent, selectActors);
        if (appended.Address == 0)
        {
            throw new InvalidOperationException("Doom mode creation exhausted its original physical task pool.");
        }
        return new(frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }
}
