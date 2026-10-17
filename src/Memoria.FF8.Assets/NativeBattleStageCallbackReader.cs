using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStageCallbackReader
{
    // Full35/45-byte callback equality, with each relative CALL target verified against authenticated FF8_EN.
    // Original50E510 initializer mapping.
    private static readonly ImmutableArray<NativeBattleStageCallback> Callbacks =
    [
        new(0x51b0a0, false), new(0x51afc0, false), new(0x51aee0, false), new(0x51ae00, false),
        new(0x51ad20, false), new(0x51ac40, false), new(0x51ab60, false), new(0x51aa80, false),
        new(0x51a9a0, true), new(0x51a8c0, false), new(0x51a7e0, false), new(0x51a700, false),
        new(0x51a620, false), new(0x51a540, false), new(0x51a460, false), new(0x51a380, false),
        new(0x51a2a0, true), new(0x51a1c0, false), new(0x51a0e0, false), new(0x519fc0, null),
        new(0x519ee0, true), new(0x519e00, false), new(0x519d20, false), new(0x519c40, false),
        new(0x519b60, false), new(0x519a80, false), new(0x5199a0, false), new(0x5198c0, false),
        new(0x5197e0, false), new(0x519700, false), new(0x519620, true), new(0x519540, true),
        new(0x519460, false), new(0x519380, false), new(0x5192a0, false), new(0x5191c0, false),
        new(0x5190e0, false), new(0x518f50, null), new(0x518a50, false), new(0x518970, false),
        new(0x518890, false), new(0x5187b0, false), new(0x5186d0, false), new(0x5185f0, false),
        new(0x518440, null), new(0x518290, null), new(0x5180c0, null), new(0x517fc0, false),
        new(0x517ee0, true), new(0x517e00, false), new(0x517d20, false), new(0x517c30, null),
        new(0x517b40, null), new(0x517a60, false), new(0x517980, true), new(0x5178a0, false),
        new(0x5177c0, true), new(0x5176e0, false), new(0x517600, false), new(0x517520, false),
        new(0x517440, false), new(0x517360, false), new(0x517280, false), new(0x5171a0, false),
        new(0x5170c0, false), new(0x516fe0, false), new(0x516f00, false), new(0x516e20, false),
        new(0x516d40, false), new(0x516c60, false), new(0x516b80, false), new(0x516aa0, false),
        new(0x5169c0, false), new(0x5168e0, false), new(0x516800, false), new(0x516720, false),
        new(0x516640, false), new(0x516560, false), new(0x516480, false), new(0x5163a0, true),
        new(0x5162c0, false), new(0x5161e0, false), new(0x516100, false), new(0x516020, false),
        new(0x515f40, false), new(0x515e60, false), new(0x515d80, false), new(0x515ca0, false),
        new(0x515bc0, false), new(0x515ae0, false), new(0x515a00, false), new(0x515920, false),
        new(0x5156d0, null), new(0x5155f0, false), new(0x515510, false), new(0x515430, false),
        new(0x515350, false), new(0x515270, false), new(0x515190, false), new(0x5150b0, false),
        new(0x514fd0, false), new(0x514ef0, false), new(0x514e10, false), new(0x514d30, false),
        new(0x514c50, false), new(0x514b70, false), new(0x514a90, false), new(0x5149a0, null),
        new(0x5148b0, null), new(0x5147d0, false), new(0x5146f0, false), new(0x514610, false),
        new(0x514530, false), new(0x514450, false), new(0x514370, false), new(0x514290, false),
        new(0x5141b0, false), new(0x5140d0, false), new(0x513ff0, false), new(0x513f10, false),
        new(0x513e30, false), new(0x513d50, false), new(0x513c70, false), new(0x513b90, false),
        new(0x513ab0, false), new(0x5139d0, false), new(0x5138f0, false), new(0x513810, false),
        new(0x513730, false), new(0x513650, false), new(0x513570, false), new(0x513490, false),
        new(0x5133b0, false), new(0x5132d0, false), new(0x5131f0, false), new(0x513110, false),
        new(0x513020, null), new(0x50dff0, null), new(0x512f40, false), new(0x512e60, false),
        new(0x512d80, false), new(0x512ca0, false), new(0x512980, null), new(0x5124d0, false),
        new(0x5123f0, false), new(0x512310, false), new(0x512230, false), new(0x511ef0, null),
        new(0x511d80, false), new(0x511ca0, false), new(0x511bc0, false), new(0x511ae0, false),
        new(0x511a00, false), new(0x511920, false), new(0x511840, false), new(0x511760, false),
        new(0x511680, false), new(0x5115a0, false), new(0x5114c0, false), new(0x5113e0, false),
        new(0x511300, false), new(0x511220, false), new(0x511140, false)
    ];

    public static NativeBattleStageCallback Read(int stageId)
    {
        if ((uint)stageId >= Callbacks.Length)
        {
            throw new NotSupportedException("Native stage callback lacks its original initializer binding.");
        }
        return Callbacks[stageId];
    }
}
