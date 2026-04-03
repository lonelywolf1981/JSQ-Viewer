using System;
using System.Collections.Generic;
using JSQViewer.Application.Exporting;
using JSQViewer.Core;
using JSQViewer.Settings;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class ExportSettingsPresenter
    {
        public ExportTemplateRequest BuildRequest(
            ProtocolTemplateMode templateMode,
            string loadedFolder,
            TestData data,
            IReadOnlyList<string> selectedChannels,
            bool includeExtra,
            string refrigerant,
            ViewerSettingsModel viewerSettings,
            bool overlayMode,
            double rangeStartOa,
            double rangeEndOa)
        {
            long? rangeStartMs = null;
            long? rangeEndMs = null;
            if (!overlayMode && !double.IsNaN(rangeStartOa) && !double.IsNaN(rangeEndOa))
            {
                rangeStartMs = ToUnixMilliseconds(rangeStartOa);
                rangeEndMs = ToUnixMilliseconds(rangeEndOa);
            }

            return new ExportTemplateRequest
            {
                TemplateMode = templateMode,
                LoadedFolder = loadedFolder,
                Data = data,
                SelectedChannels = selectedChannels == null
                    ? (IReadOnlyList<string>)new string[0]
                    : new List<string>(selectedChannels),
                IncludeExtra = includeExtra,
                Refrigerant = string.IsNullOrWhiteSpace(refrigerant) ? "R290" : refrigerant,
                ViewerSettings = viewerSettings ?? ViewerSettingsModel.CreateDefault(),
                RangeStartMs = rangeStartMs,
                RangeEndMs = rangeEndMs
            };
        }

        private static long ToUnixMilliseconds(double oaValue)
        {
            DateTime dateTime = DateTime.FromOADate(oaValue);
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(dateTime.ToUniversalTime() - epoch).TotalMilliseconds;
        }
    }
}
