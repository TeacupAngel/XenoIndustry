using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using KSP;

namespace XenoIndustry
{
    class XenoIndustryCoreGameParameters : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "XenoIndustry Core"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.SCIENCE | GameParameters.GameMode.CAREER; } }
        public override string Section { get { return "XenoIndustry"; } }
        public override string DisplaySection { get { return "XenoIndustry"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }

        [GameParameters.CustomParameterUI("Enable XenoIndustry", toolTip = "Governs whether XenoIndustry (and thus the connection to Clusterio) is enabled")]
        public bool enabled = false;
    }
}
