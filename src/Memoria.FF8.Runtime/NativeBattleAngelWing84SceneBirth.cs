namespace Memoria.FF8.Runtime;

/// <summary>78EB50's exact scene-owned task births and78EC30/78EC80 parameter stores.</summary>
public static class NativeBattleAngelWing84SceneBirth
{
    public static NativeBattleAngelWing84Frame Apply(NativeBattleAngelWing84Frame frame, NativeBattleAngelWing84PhaseBirth birth, NativeBattleAngelWing84Inputs inputs, Func<NativeBattleAngelWing84Frame, uint, NativeBattleAngelWing84Frame> initializeBody)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Angel Wing scene birth requires its original action parameters.");
        switch (birth.Phase)
        {
            case 1:
                uint body = Append(8, 0x7961E0, 340);
                frame = initializeBody(frame, body);
                Program(0x78ECD0, 0x171F2B0, 1184, 0);
                Append(6, 0x796FD0, 88);
                Append(6, 0x7972B0, 88);
                break;
            case 2:
                Nodes();
                break;
            case 3:
                Program(0x791260, 0x17217A8, 1928, 2);
                Nodes();
                break;
        }
        return frame;

        uint Append(int pool, uint callback, int bytes)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, bytes, birth.Parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            frame = frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
            return appended.Address;
        }

        void Nodes()
        {
            uint address = Append(3, 0x7913C0, 676);
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteDword(48, 0x171DE1C).WriteWord(664, 8).WriteWord(668, 0).WriteWord(670, 15));
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        }

        void Program(uint callback, uint source, uint workspaceBytes, ushort mode)
        {
            frame = AppendProgram(frame, birth.Parent, callback, source, workspaceBytes, mode, 0, inputs);
        }
    }

    public static NativeBattleAngelWing84Frame AppendProgram(NativeBattleAngelWing84Frame frame, uint parent, uint callback, uint source, uint workspaceBytes, ushort mode, ushort delay, NativeBattleAngelWing84Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native78EC80 requires its original action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, callback, 2096, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
        hierarchy = appended.Hierarchy;
        hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteWord(128, mode).WriteDword(116, source).WriteDword(120, workspaceBytes).WriteWord(130, delay));
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }

}
