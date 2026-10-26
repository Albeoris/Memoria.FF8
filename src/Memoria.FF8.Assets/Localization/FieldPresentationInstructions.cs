using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Commands whose complete operand packet only controls audiovisual presentation in the text comparison.</summary>
internal static class FieldPresentationInstructions
{
    public static bool CanIgnore(Jsm.Opcode opcode, IReadOnlyList<int?> operands)
    {
        int count = OperandCount(opcode);
        if (count < 0 || operands.Count != count)
        {
            return false;
        }
        return opcode is not (Jsm.Opcode.DISPBAR or Jsm.Opcode.BROKEN or Jsm.Opcode.SETBAR) || operands[0] is 0 or 1;
    }

    // Operand counts follow the corresponding EventEngine command implementations.
    // Status queries, general waits, movement, input, messages and script calls remain significant.
    public static int OperandCount(Jsm.Opcode opcode) => opcode switch
    {
        Jsm.Opcode.MUSICCHANGE or Jsm.Opcode.MUSICREPLAY or Jsm.Opcode.MUSICVOLSYNC or Jsm.Opcode.INITSOUND => 0,
        Jsm.Opcode.MUSICLOAD or Jsm.Opcode.MUSICSKIP or Jsm.Opcode.DUALMUSIC or Jsm.Opcode.MUSICSTOP => 1,
        Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.CHOICEMUSIC or Jsm.Opcode.MUSICVOL => 2,
        Jsm.Opcode.MUSICVOLTRANS => 3,
        Jsm.Opcode.MUSICVOLFADE => 4,
        Jsm.Opcode.EFFECTLOAD or Jsm.Opcode.SESTOP or Jsm.Opcode.ALLSEVOL or Jsm.Opcode.ALLSEPOS => 1,
        Jsm.Opcode.SEVOL or Jsm.Opcode.SEPOS or Jsm.Opcode.ALLSEVOLTRANS or Jsm.Opcode.ALLSEPOSTRANS => 2,
        Jsm.Opcode.EFFECTPLAY2 or Jsm.Opcode.SEVOLTRANS or Jsm.Opcode.SEPOSTRANS => 3,
        Jsm.Opcode.EFFECTPLAY => 4,
        Jsm.Opcode.DCOLADD or Jsm.Opcode.DCOLSUB => 3,
        Jsm.Opcode.TCOLADD or Jsm.Opcode.TCOLSUB => 4,
        Jsm.Opcode.FCOLADD or Jsm.Opcode.FCOLSUB => 7,
        Jsm.Opcode.FADEIN or Jsm.Opcode.FADEOUT or Jsm.Opcode.FADEBLACK or Jsm.Opcode.FADENONE or Jsm.Opcode.FADESYNC or Jsm.Opcode.COLSYNC or Jsm.Opcode.MAPFADEOFF or Jsm.Opcode.MAPFADEON => 0,
        Jsm.Opcode.SETCAMERA => 1,
        Jsm.Opcode.SHAKE => 4,
        Jsm.Opcode.SHAKEOFF => 0,
        Jsm.Opcode.DISPBAR => 7,
        Jsm.Opcode.BROKEN => 8,
        Jsm.Opcode.SETBAR => 2,
        _ => -1
    };
}
