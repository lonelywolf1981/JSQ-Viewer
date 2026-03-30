using System.Collections.Generic;

namespace JSQViewer.Application.Abstractions
{
    public sealed class UiStateModel
    {
        public string folder { get; set; }
        public bool? auto_step { get; set; }
        public string target_points { get; set; }
        public int? manual_step { get; set; }
        public bool? compare_overlay { get; set; }
        public string sort_mode { get; set; }
        public bool? selected_only { get; set; }
        public string channel_filter { get; set; }
        public bool? include_extra { get; set; }
        public string refrigerant { get; set; }
        public int? splitter_distance { get; set; }
        public List<string> checked_channels { get; set; }
    }
}
