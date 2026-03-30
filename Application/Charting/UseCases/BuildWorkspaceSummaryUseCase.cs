using System;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting.UseCases
{
    public sealed class BuildWorkspaceSummaryUseCase
    {
        private readonly DataSummaryService _dataSummaryService;

        public BuildWorkspaceSummaryUseCase(DataSummaryService dataSummaryService)
        {
            _dataSummaryService = dataSummaryService ?? throw new ArgumentNullException(nameof(dataSummaryService));
        }

        public DataSummary Execute(TestData data)
        {
            return _dataSummaryService.BuildSummary(data);
        }
    }
}
