namespace JSQViewer.Presentation.WinForms.ViewModels
{
    public sealed class DynamicsForecastRoleItemViewModel
    {
        public DynamicsForecastRoleItemViewModel(string code, string label, double durationHours)
        {
            Code = code ?? string.Empty;
            Label = label ?? string.Empty;
            DurationHours = durationHours;
        }

        public string Code { get; private set; }

        public string Label { get; private set; }

        public double DurationHours { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }
}
