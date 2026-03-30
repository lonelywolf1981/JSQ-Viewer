using System;
namespace JSQViewer.Application.Charting.UseCases
{
    public sealed class BuildChartViewUseCase
    {
        private readonly ChartPipelineService _chartPipelineService;

        public BuildChartViewUseCase(ChartPipelineService chartPipelineService)
        {
            _chartPipelineService = chartPipelineService ?? throw new ArgumentNullException(nameof(chartPipelineService));
        }

        public ChartPipelineResult Execute(ChartPipelineRequest request)
        {
            return _chartPipelineService.Execute(request);
        }
    }
}
