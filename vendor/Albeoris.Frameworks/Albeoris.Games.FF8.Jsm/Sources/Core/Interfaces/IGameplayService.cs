using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IGameplayService
    {
        Boolean IsSupported { get; }

        Boolean IsUserControlEnabled { get; set; }
        Boolean IsRandomBattlesEnabled { get; set; }

        void ResetAllData();
    }
}