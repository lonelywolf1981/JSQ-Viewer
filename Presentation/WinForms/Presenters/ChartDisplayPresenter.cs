namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class ChartDisplayPresenter
    {
        public bool IsChartRequested { get; private set; }

        public void RequestOpen()
        {
            IsChartRequested = true;
        }

        public void Close()
        {
            IsChartRequested = false;
        }

        public bool ShouldRenderAfterSelectionChange()
        {
            return IsChartRequested;
        }

        public bool ShouldRenderAfterWorkspaceReload()
        {
            return IsChartRequested;
        }

        public bool ShouldOpenHostForCurrentRedraw()
        {
            return IsChartRequested;
        }
    }
}
