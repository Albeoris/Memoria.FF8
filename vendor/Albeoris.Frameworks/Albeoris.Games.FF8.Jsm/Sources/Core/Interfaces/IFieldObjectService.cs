using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IFieldService
    {
        Boolean IsSupported { get; }

        EventEngine Engine { get; }

        void FadeOn();
        void FadeOff();
        void FadeIn();
        void FadeOut();

        void PrepareGoTo(FieldId fieldId);
        void GoTo(FieldId fieldId, Int32 walkmeshId);
        void BindArea(Int32 areaId);
    }
}