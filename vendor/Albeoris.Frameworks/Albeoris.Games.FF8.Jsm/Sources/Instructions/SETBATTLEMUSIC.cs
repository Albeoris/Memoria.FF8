using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SETBATTLEMUSIC : JsmInstruction
    {
        private MusicId _musicId;

        public SETBATTLEMUSIC(MusicId musicId)
        {
            _musicId = musicId;
        }

        public SETBATTLEMUSIC(Int32 parameter, IExpressionStack stack)
            : this(
                musicId: (MusicId)((Jsm.Expression.PSHN_L)stack.Pop()).Int32())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SETBATTLEMUSIC)}({nameof(_musicId)}: {_musicId})";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .CommentLine(MusicName.Get(_musicId))
                .StaticType(nameof(IMusicService))
                .Method(nameof(IMusicService.ChangeBattleMusic))
                .Enum(_musicId)
                .Comment(nameof(SETBATTLEMUSIC));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Music[services].ChangeBattleMusic(_musicId);
            return DummyAwaitable.Instance;
        }
    }
}