namespace JSQViewer.Application.Charting
{
    public sealed class DynamicsForecastRoleSelection
    {
        public DynamicsForecastRoleSelection(string oldFuncCode, string targetCode, string newFuncCode)
        {
            OldFuncCode = oldFuncCode ?? string.Empty;
            TargetCode = targetCode ?? string.Empty;
            NewFuncCode = newFuncCode ?? string.Empty;
        }

        public string OldFuncCode { get; private set; }

        public string TargetCode { get; private set; }

        public string NewFuncCode { get; private set; }

        public bool HasAllRoles
        {
            get
            {
                return OldFuncCode.Length > 0
                    && TargetCode.Length > 0
                    && NewFuncCode.Length > 0;
            }
        }
    }
}
