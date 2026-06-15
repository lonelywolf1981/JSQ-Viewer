namespace JSQViewer.Application.Charting
{
    public enum DynamicsForecastWarningCode
    {
        ReferenceFuncStartTemperatureMismatch,
        ReferenceFuncDurationMismatch
    }

    // Non-blocking quality signal about how well the reference (old) FUNC matches the
    // new FUNC. The forecast is still produced; the UI surfaces these so the operator
    // knows the analogy may be weak. Value carries the measured deviation for display.
    public sealed class DynamicsForecastWarning
    {
        public DynamicsForecastWarning(DynamicsForecastWarningCode code, double value)
        {
            Code = code;
            Value = value;
        }

        public DynamicsForecastWarningCode Code { get; private set; }

        public double Value { get; private set; }
    }
}
