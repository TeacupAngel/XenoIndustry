using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using KSP;

namespace XenoIndustry
{
    public class ModuleXenoIndustryCargo : PartModule
    {
        //[KSPField(guiActive = true, guiActiveEditor = false, guiName = "Carried cargo")]
        //public string carriedCargoDisplay = "none";

        public PartResource cargoResource;

        public Dictionary<string, int> carriedCargo = new Dictionary<string, int>();

        [KSPField(isPersistant = true)]
        private bool initialised = false;

        [KSPEvent(guiName = "Transfer Cargo", guiActive = true, guiActiveEditor = false, guiActiveUnfocused = false, guiActiveUncommand = false)]
        public void TransferCargo()
        {
            if (!XenoIndustryCargo.windowVisible || XenoIndustryCargo.windowActivePart != this)
            {
                XenoIndustryCargo.windowVisible = true;
                XenoIndustryCargo.windowActivePart = this;
                XenoIndustryCargo.windowResponse = "";
                Debug.Log("ModuleXenoIndustryCargo: Setting XenoIndustryCargo to this partModule");
            }
            else
            {
                XenoIndustryCargo.windowVisible = false;
                Debug.Log("ModuleXenoIndustryCargo: Turning off XenoIndustryCargo window");
            }
        }

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight || part == null || part.State == PartStates.DEAD || vessel == null)
            {
                return;
            }

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName == "FactorioCargo")
                {
                    cargoResource = resource;

                    if (!initialised)
                    {
                        cargoResource.amount = 0;
                    }
                }
            }

            initialised = true;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (!HighLogic.LoadedSceneIsFlight || part == null || part.State == PartStates.DEAD || vessel == null)
            {
                return;
            }

            if (carriedCargo.Count > 0)
            {
                ConfigNode saveNode = node.AddNode("CarriedCargo");

                foreach (KeyValuePair<string, int> kvPair in carriedCargo)
                {
                    saveNode.AddValue(kvPair.Key, kvPair.Value);
                    Debug.Log("ModuleXenoIndustryCargo: saving item " + kvPair.Key + ", count " + kvPair.Value);
                }

                Debug.Log("ModuleXenoIndustryCargo: carried cargo saved");
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsFlight || part == null || part.State == PartStates.DEAD || vessel == null)
            {
                return;
            }

            ConfigNode cargoNode = node.GetNode("CarriedCargo");

            if (cargoNode != null)
            {
                foreach (ConfigNode.Value value in cargoNode.values)
                {
                    int result;

                    if (int.TryParse(value.value, out result))
                    {
                        carriedCargo[value.name] = result;
                        Debug.Log("ModuleXenoIndustryCargo: loading item " + value.name + ", count " + result);
                    }
                    else
                    {
                        Debug.Log("ModuleXenoIndustryCargo: cannot read value of item " + value.name);
                    }
                }

                Debug.Log("ModuleXenoIndustryCargo: carried cargo loaded");
            }
        }
    }
}
