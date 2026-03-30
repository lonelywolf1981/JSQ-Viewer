using System;
using JSQViewer.Core;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Application.Charting.UseCases
{
    public sealed class BuildWorkspaceSummaryUseCase
    {
        private readonly DataSummaryService _dataSummaryService;

        public BuildWorkspaceSummaryUseCase(DataSummaryService dataSummaryService)
        {
            _dataSummaryService = dataSummaryService ?? throw new ArgumentNullException(nameof(dataSummaryService));
        }

        public WorkspaceSummaryViewModel Execute(TestData data)
        {
            DataSummary summary = _dataSummaryService.BuildSummary(data);
            return new WorkspaceSummaryViewModel
            {
                PointCount = summary.Points,
                Start = summary.Start,
                End = summary.End
            };
        }
    }
}
