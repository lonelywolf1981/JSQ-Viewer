using System.Collections.Generic;

namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class SourceWindowRefreshPlan
    {
        public SourceWindowRefreshPlan(bool canRefreshInPlace, IReadOnlyList<SourceChannelWindowViewModel> windows)
        {
            CanRefreshInPlace = canRefreshInPlace;
            Windows = windows ?? new SourceChannelWindowViewModel[0];
        }

        public bool CanRefreshInPlace { get; private set; }

        public IReadOnlyList<SourceChannelWindowViewModel> Windows { get; private set; }
    }
}
