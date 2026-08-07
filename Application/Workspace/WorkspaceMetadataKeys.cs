namespace JSQViewer.Application.Workspace
{
    /// <summary>
    /// Names of <see cref="JSQViewer.Core.TestData.Meta"/> keys that describe a single recording
    /// (equipment model, compressor, experiment type, climate mode). These are source-specific:
    /// they must not survive a merge of several recordings into one workspace, because a merged
    /// <c>Meta</c> is workspace-global and cannot be attributed to any one of the merged sources.
    /// </summary>
    public static class WorkspaceMetadataKeys
    {
        public const string EquipmentModel = "Модель оборудования";
        public const string Compressor = "Компрессор";
        public const string ExperimentType = "Тип испытания";
        public const string ClimateMode = "Климатический режим";
    }
}
