using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface ISalaryService
    {
        Boolean IsSupported { get; }

        Boolean IsSalaryEnabled { get; set; }
        Boolean IsSalaryAlertEnabled { get; set; }
    }
}