using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Reflection;

using SimpleJSON;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Networking;

using KSP;
using KSP.UI.Screens;

namespace XenoIndustry
{
    public class XenoIndustryLaunchCostsPartRule
    {
        public string partName;
        public string moduleName;

        public string containsResource;

        public bool crewedOnly = false;
        public bool nonCrewedOnly = false;

        public Dictionary<string, int> itemCosts = new Dictionary<string, int>();
    }

    public class XenoIndustryLaunchCostsResourceRule
    {
        public string resourceName = null;

        public Dictionary<string, int> itemCosts = new Dictionary<string, int>();
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class XenoIndustryLaunchCosts : MonoBehaviour
    {
        public static readonly String MOD_PATH = "GameData/XenoIndustry/";
        public static readonly String RESOURCE_PATH = "XenoIndustry/Resource/";

        private ApplicationLauncherButton toolbarButton = null;

        private bool windowVisible = false;
        private Rect windowRect;

        private Dictionary<string, int> clusterioInventory;
        private Dictionary<string, int> latestLaunchCosts;

        private float lastConnectionUpdate = 0f;

        private List<XenoIndustryLaunchCostsPartRule> partCostRules;
        private List<XenoIndustryLaunchCostsResourceRule> resourceCostRules;

        public void Awake()
        {
            DontDestroyOnLoad(this);

            GameEvents.onGameSceneSwitchRequested.Add(OnGameSceneSwitchRequested);
            GameEvents.onGUILaunchScreenSpawn.Add(OnGUILaunchScreenSpawn);
            GameEvents.onLevelWasLoadedGUIReady.Add(OnLevelWasLoadedGUIReady);
            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            //GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIApplicationLauncherDestroyed);
            GameEvents.onVesselRecovered.Add(OnVesselRecovered);

            partCostRules = new List<XenoIndustryLaunchCostsPartRule>();
            resourceCostRules = new List<XenoIndustryLaunchCostsResourceRule>();

            JSONNode[] launchCostRuleNodes = JSONUtil.ReadJSONFile(MOD_PATH + "launchCosts.json");

            if (launchCostRuleNodes != null)
            {
                foreach (JSONNode ruleNode in launchCostRuleNodes)
                {
                    if (ruleNode["type"] == null)
                    {
                        Debug.Log("XenoIndustryLaunchCosts: cost rule is lacking type, skipping");
                        continue;
                    }

                    if (ruleNode["type"] == "part")
                    {
                        XenoIndustryLaunchCostsPartRule rule = new XenoIndustryLaunchCostsPartRule();

                        Debug.Log("XenoIndustryLaunchCosts: adding new part rule");

                        if (ruleNode["name"] != null)
                        {
                            Debug.Log(String.Format("XenoIndustryLaunchCosts: adding name {0} to rule", ruleNode["name"]));
                            rule.partName = ruleNode["name"];
                        }

                        if (ruleNode["moduleName"] != null)
                        {
                            Debug.Log(String.Format("XenoIndustryLaunchCosts: adding moduleName {0} to rule", ruleNode["moduleName"]));
                            rule.moduleName = ruleNode["moduleName"];
                        }

                        if (ruleNode["containsResource"] != null)
                        {
                            Debug.Log(String.Format("XenoIndustryLaunchCosts: adding containsResource {0} to rule", ruleNode["containsResource"]));
                            rule.containsResource = ruleNode["containsResource"];
                        }

                        if (ruleNode["crewedOnly"] != null)
                        {
                            Debug.Log(String.Format("XenoIndustryLaunchCosts: adding crewedOnly {0} to rule", ruleNode["crewedOnly"]));
                            rule.crewedOnly = ruleNode["crewedOnly"].AsBool;
                        }

                        if (ruleNode["nonCrewedOnly"] != null)
                        {
                            Debug.Log(String.Format("XenoIndustryLaunchCosts: adding nonCrewedOnly {0} to rule", ruleNode["nonCrewedOnly"]));
                            rule.nonCrewedOnly = ruleNode["nonCrewedOnly"].AsBool;
                        }

                        if (ruleNode["itemCosts"] != null)
                        {
                            if (!ruleNode["itemCosts"].IsObject)
                            {
                                Debug.Log("XenoIndustryLaunchCosts: rule itemCosts are not valid, must be object");
                            }
                            else
                            {
                                foreach (KeyValuePair<string, JSONNode> itemNode in ruleNode["itemCosts"].AsObject)
                                {
                                    string name = itemNode.Key;
                                    int count = itemNode.Value.AsInt;

                                    rule.itemCosts[name] = count;

                                    Debug.Log(String.Format("XenoIndustryLaunchCosts: adding item cost {0} of item {1} to rule", count, name));
                                }
                            }
                        }

                        partCostRules.Add(rule);
                    }
                    else if (ruleNode["type"] == "resource")
                    {
                        XenoIndustryLaunchCostsResourceRule rule = new XenoIndustryLaunchCostsResourceRule();

                        Debug.Log("XenoIndustryLaunchCosts: adding new resource rule");

                        if (ruleNode["name"] != null)
                        {
                            Debug.Log(String.Format("XenoIndustryLaunchCosts: adding name {0} to rule", ruleNode["name"]));
                            rule.resourceName = ruleNode["name"];
                        }

                        if (ruleNode["itemCosts"] != null)
                        {
                            if (!ruleNode["itemCosts"].IsObject)
                            {
                                Debug.Log("XenoIndustryLaunchCosts: rule itemCosts are not valid, must be object");
                            }
                            else
                            {
                                foreach (KeyValuePair<string, JSONNode> itemNode in ruleNode["itemCosts"].AsObject)
                                {
                                    string name = itemNode.Key;
                                    int count = itemNode.Value.AsInt;

                                    rule.itemCosts[name] = count;

                                    Debug.Log(String.Format("XenoIndustryLaunchCosts: adding item cost {0} of item {1} to rule", count, name));
                                }
                            }
                        }

                        resourceCostRules.Add(rule);
                    }
                    else
                    {
                        Debug.Log(String.Format("XenoIndustryLaunchCosts: cost rule has invalid type {0}, skipping", ruleNode["type"]));
                    }
                }
            }

            windowRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 150, 300, 100);

            clusterioInventory = new Dictionary<string, int>();
            latestLaunchCosts = new Dictionary<string, int>();
        }

        /*public void Start()
        {
            Debug.Log("ClusterioTest: Start");
        }*/

        private void OnGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> sceneSwitch)
        {
            if (H﻿ighLogic.CurrentGame.Parameters.CustomParams<XenoIndustryCoreGameParameters>().enabled)
            {
                if (toolbarButton != null)
                {
                    toolbarButton.SetFalse();
                }

                // If the player has reverted to VAB or SPH, refund the spent items
                if (sceneSwitch.from == GameScenes.FLIGHT && sceneSwitch.to == GameScenes.EDITOR)
                {
                    string bodyName = "Kerbin"; // Only launches from Kerbin are possible at this point

                    foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
                    {
                        StartCoroutine(XenoIndustrySignpost.AddItemsToClusterio(bodyName, kvPair.Key, kvPair.Value));
                    }
                }
            }
        }

        private void OnGUILaunchScreenSpawn(GameEvents.VesselSpawnInfo e)
        {
            //onGUILaunchScreenSpawn is called before VesselSpawnDialog's Start() happens, but we want to wait after it's done so that we can replace
            if (H﻿ighLogic.CurrentGame.Parameters.CustomParams<XenoIndustryCoreGameParameters>().enabled)
            {
                StartCoroutine(ReplaceLaunchButtonFunction());
            }
        }

        IEnumerator ReplaceLaunchButtonFunction()
        {
            yield return new WaitForSeconds(.1f);

            if (VesselSpawnDialog.Instance != null)
            {
                // A bit of reflection because buttonLaunch in VesselSpawnDialog is private :/
                FieldInfo buttonFieldInfo = typeof(VesselSpawnDialog).GetField("buttonLaunch", BindingFlags.NonPublic | BindingFlags.Instance);

                if (buttonFieldInfo == null)
                {
                    yield break;
                }

                Button button = buttonFieldInfo.GetValue(VesselSpawnDialog.Instance) as Button;

                if (button == null)
                {
                    yield break;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { StartCoroutine(ClusterioPreflightResourceCheck()); }); //originalAction: VesselSpawnDialog.Instance.ButtonLaunch
            }
        }

        private void OnLevelWasLoadedGUIReady(GameScenes gameScene)
        {
            Debug.Log("ClusterioTest: onLevelWasLoadedGUIReady");

            if (gameScene == GameScenes.EDITOR && H﻿ighLogic.CurrentGame.Parameters.CustomParams<XenoIndustryCoreGameParameters>().enabled)
            {
                EditorLogic.fetch.launchBtn.onClick.RemoveAllListeners();
                EditorLogic.fetch.launchBtn.onClick.AddListener(() => { StartCoroutine(ClusterioPreflightResourceCheck()); }); // originalAction: EditorLogic.fetch.launchVessel

                // Has to be done here, otherwise the button appears twice in some scenes
                if (ApplicationLauncher.Ready && toolbarButton == null)
                {
                    toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                        OnToolbarButtonOn,
                        OnToolbarButtonOff,
                        null,
                        null,
                        null,
                        null,
                        ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                        (Texture)GameDatabase.Instance.GetTexture(RESOURCE_PATH + "icon_clusterio", false));

                    if (toolbarButton == null) Debug.Log("XenoIndustryLaunchCosts: could not register stock toolbar button!");
                }
            }
        }

        private void OnEditorShipModified(ShipConstruct shipConstruct)
        {
            if (H﻿ighLogic.CurrentGame.Parameters.CustomParams<XenoIndustryCoreGameParameters>().enabled)
            {
                CalculateLaunchCosts(ref latestLaunchCosts);
            }
        }

        private void OnVesselRecovered(ProtoVessel recoveredVessel, bool quick)
        {
            if (H﻿ighLogic.CurrentGame.Parameters.CustomParams<XenoIndustryCoreGameParameters>().enabled)
            {
                Dictionary<string, int> recoveredResources = new Dictionary<string, int>();

                CalculateLaunchCosts(ref recoveredResources, recoveredVessel);

                if (recoveredResources.Count > 0)
                {
                    string message = "Following resources have been recovered: \n";

                    string bodyName = "Kerbin"; // Recovery is only possible on Kerbin for now

                    foreach (KeyValuePair<string, int> kvPair in recoveredResources)
                    {
                        message += String.Format("\n {0} {1}", kvPair.Value, kvPair.Key);

                        StartCoroutine(XenoIndustrySignpost.AddItemsToClusterio(bodyName, kvPair.Key, kvPair.Value));
                    }

                    message += "\n";

                    if (!quick)
                    {
                        MultiOptionDialog dialog = new MultiOptionDialog("ClusterioResourceRecovery", message, "Recovery successful", UISkinManager.GetSkin("KSP window 7"),
                            new DialogGUIBase[]
                            {
                        new DialogGUIButton("Continue", null)
                            });

                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dialog, false, null);
                    }
                }
            }
        }

        IEnumerator ClusterioPreflightResourceCheck()
        {
            Debug.Log("XenoIndustryLaunchCosts: PreflightResourceCheck");

            string bodyName = "Kerbin"; // Launching only works on Kerbin for now

            yield return StartCoroutine(XenoIndustrySignpost.GetClusterioInventory(bodyName, clusterioInventory));

            CalculateLaunchCosts(ref latestLaunchCosts);

            bool pass = true;

            foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
            {
                if (!clusterioInventory.ContainsKey(kvPair.Key) || clusterioInventory[kvPair.Key] < kvPair.Value)
                {
                    pass = false;
                    break;
                }
            }

            if (!pass)
            { // Insufficient resources to launch
                string message = "You do not have enough resources to launch this vessel! This vessel needs: \n";

                foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
                {
                    message += String.Format("\n {0} {1} (you have {2})", kvPair.Value, kvPair.Key, (clusterioInventory.ContainsKey(kvPair.Key)) ? clusterioInventory[kvPair.Key] : 0);
                }

                message += "\n";

                MultiOptionDialog dialog = new MultiOptionDialog("InsufficientClusterioResources", message, "Insufficient resources!", UISkinManager.GetSkin("KSP window 7"),
                    new DialogGUIBase[]
                    {
                        new DialogGUIButton("Unable to Launch", null)
                    });

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dialog, false, null);
            }
            else
            { // Launch can proceed
                string message = "Launching this vessel will require: \n";

                foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
                {
                    message += String.Format("\n {0} {1} (you have {2})", kvPair.Value, kvPair.Key, (clusterioInventory.ContainsKey(kvPair.Key)) ? clusterioInventory[kvPair.Key] : 0);
                }

                message += "\n";

                MultiOptionDialog dialog = new MultiOptionDialog("ClusterioLaunchConfirmation", message, "Launch Possible", UISkinManager.GetSkin("KSP window 7"),
                    new DialogGUIBase[]
                    {
                        new DialogGUIButton("Launch", new Callback(ProceedToLaunch)),
                        new DialogGUIButton("Cancel", null)
                    });

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dialog, false, null);
            }
        }

        private void CalculateLaunchCosts(ref Dictionary<string, int> launchCosts, ProtoVessel protoVessel = null)
        {
            launchCosts.Clear();

            // ProtoVessel (for recovery)
            if (protoVessel != null)
            {
                foreach (ProtoPartSnapshot part in protoVessel.protoPartSnapshots)
                {
                    AvailablePart partInfo = part.partInfo;

                    float partCost = partInfo.cost + part.moduleCosts;

                    foreach (ProtoPartResourceSnapshot resource in part.resources)
                    {
                        PartResourceDefinition resourceInfo = resource.definition;

                        partCost -= (float)(resourceInfo.unitCost * resource.maxAmount);

                        ApplyResourceCostRules(resource.resourceName, (float)resource.amount, ref launchCosts);
                    }

                    ApplyPartCostRules(partInfo, partCost, ref launchCosts);
                }
            }
            // ShipConstruct (for Editor)
            else if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                ShipConstruct ship = EditorLogic.fetch.ship;

                if (ship == null)
                {
                    return;
                }

                foreach (Part part in ship.parts)
                {
                    AvailablePart partInfo = part.partInfo;

                    float partCost = partInfo.cost + part.GetModuleCosts(partInfo.cost, ModifierStagingSituation.CURRENT);

                    foreach (PartResource resource in part.Resources)
                    {
                        PartResourceDefinition resourceInfo = resource.info;

                        partCost -= (float)(resourceInfo.unitCost * resource.maxAmount);

                        ApplyResourceCostRules(resource.resourceName, (float)resource.amount, ref launchCosts);
                    }

                    ApplyPartCostRules(partInfo, partCost, ref launchCosts);
                }
            }
            // ConfigNode (for launchpad and runway)
            else
            {
                if (VesselSpawnDialog.Instance != null)
                {
                    // More reflection because not only is the vessel ConfigNode private, it's also hidden in an internal class in VesselSpawnDialog
                    FieldInfo selectedDataFieldInfo = typeof(VesselSpawnDialog).GetField("selectedDataItem", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (selectedDataFieldInfo == null)
                    {
                        Debug.Log("XenoIndustryLaunchCosts: selectedDataFieldInfo is null");
                        return;
                    }

                    object selectedDataValue = selectedDataFieldInfo.GetValue(VesselSpawnDialog.Instance);

                    if (selectedDataValue == null)
                    {
                        Debug.Log("XenoIndustryLaunchCosts: selectedDataValue is null");
                        return;
                    }

                    // ---
                    FieldInfo configNodeFieldInfo = selectedDataValue.GetType().GetField("_configNode", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (configNodeFieldInfo == null)
                    {
                        Debug.Log("XenoIndustryLaunchCosts: configNodeFieldInfo is null");
                        return;
                    }

                    ConfigNode configNodeValue = configNodeFieldInfo.GetValue(selectedDataValue) as ConfigNode;

                    if (configNodeValue == null)
                    {
                        Debug.Log("XenoIndustryLaunchCosts: configNodeValue is null");
                        return;
                    }

                    foreach (ConfigNode partNode in configNodeValue.GetNodes("PART"))
                    {
                        string partName = partNode.GetValue("part");
                        partName = partName.Substring(0, partName.IndexOf("_"));

                        AvailablePart partInfo = PartLoader.getPartInfoByName(partName);
                        Part part = partInfo.partPrefab;

                        float partCost = partInfo.cost + part.GetModuleCosts(partInfo.cost, ModifierStagingSituation.CURRENT) + float.Parse(partNode.GetValue("modCost"));

                        foreach (PartResource resource in part.Resources)
                        {
                            PartResourceDefinition resourceInfo = resource.info;

                            partCost -= (float)(resourceInfo.unitCost * resource.maxAmount);

                            ApplyResourceCostRules(resource.resourceName, (float)resource.amount, ref launchCosts);
                        }

                        ApplyPartCostRules(partInfo, partCost, ref launchCosts);
                    }
                }
            }
        }

        private void ApplyPartCostRules(AvailablePart part, float cost, ref Dictionary<string, int> launchCosts)
        {
            XenoIndustryLaunchCostsPartRule finalRule = null;

            foreach (XenoIndustryLaunchCostsPartRule rule in partCostRules)
            {
                if (rule.partName != null && part.name != rule.partName)
                {
                    continue;
                }

                if (rule.moduleName != null)
                {
                    bool hasModule = false;

                    foreach (PartModule module in part.partPrefab.Modules)
                    {
                        if (module.ClassName == rule.moduleName)
                        {
                            hasModule = true;
                            break;
                        }
                    }

                    if (!hasModule)
                    {
                        continue;
                    }
                }

                if (rule.containsResource != null)
                {
                    bool hasResource = false;

                    foreach (PartResource resource in part.partPrefab.Resources)
                    {
                        if (resource.resourceName == rule.containsResource)
                        {
                            hasResource = true;
                            break;
                        }
                    }

                    if (!hasResource)
                    {
                        continue;
                    }
                }

                if (part.partPrefab.CrewCapacity > 0 && rule.nonCrewedOnly)
                {
                    continue;
                }
                else if (part.partPrefab.CrewCapacity == 0 && rule.crewedOnly)
                {
                    continue;
                }

                finalRule = rule;
            }

            if (finalRule == null)
            {
                Debug.Log("XenoIndustryLaunchCosts: no rule has passed, part cannot be counted into final cost");
                return;
            }

            foreach (KeyValuePair<string, int> kvPair in finalRule.itemCosts)
            {
                string itemName = kvPair.Key;
                int itemCost = kvPair.Value;

                if (launchCosts.ContainsKey(itemName))
                {
                    launchCosts[itemName] += (int)Math.Ceiling(cost / (float)itemCost);
                }
                else
                {
                    launchCosts[itemName] = (int)Math.Ceiling(cost / (float)itemCost);
                }
            }
        }

        private void ApplyResourceCostRules(string resourceName, float amount, ref Dictionary<string, int> launchCosts)
        {
            XenoIndustryLaunchCostsResourceRule finalRule = null;

            foreach (XenoIndustryLaunchCostsResourceRule rule in resourceCostRules)
            {
                if (resourceName != null && resourceName != rule.resourceName)
                {
                    continue;
                }

                finalRule = rule;
            }

            if (finalRule == null)
            {
                Debug.Log("XenoIndustryLaunchCosts: no rule has passed, resource cannot be counted in vessel cost");
                return;
            }

            foreach (KeyValuePair<string, int> kvPair in finalRule.itemCosts)
            {
                string itemName = kvPair.Key;
                int itemCost = kvPair.Value;

                int finalCost = (int)Math.Ceiling(amount / (float)itemCost);

                // This check is needed because fuel cost can end up being 0, and we don't want to add zeros to the cost
                if (finalCost > 0)
                {
                    if (launchCosts.ContainsKey(itemName))
                    {
                        launchCosts[itemName] += finalCost;
                    }
                    else
                    {
                        launchCosts[itemName] = finalCost;
                    }
                }
            }
        }

        private void ProceedToLaunch()
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                EditorLogic.fetch.launchVessel();
                DeductLaunchPrice();
            }
            else
            {
                if (VesselSpawnDialog.Instance != null)
                {
                    VesselSpawnDialog.Instance.ButtonLaunch();
                    DeductLaunchPrice();
                }
            }
        }

        private void DeductLaunchPrice()
        {
            string bodyName = "Kerbin"; // Only launches from Kerbin are possible at this point

            foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
            {
                StartCoroutine(XenoIndustrySignpost.RemoveItemsFromClusterio(bodyName, clusterioInventory, kvPair.Key, kvPair.Value));
            }
        }

        private void OnGUIApplicationLauncherDestroyed()
        {
            if (toolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
                toolbarButton = null;
            }
        }

        void OnToolbarButtonOn()
        {
            windowVisible = true;
        }

        void OnToolbarButtonOff()
        {
            windowVisible = false;
        }

        public void OnGUI()
        {
            if (windowVisible)
            {
                windowRect = GUILayout.Window(22348, windowRect, OnWindowInternal, "XenoIndustry Vessel Costs");
            }
        }

        private void Update()
        {
            if (Time.unscaledTime > lastConnectionUpdate + 10)
            {
                lastConnectionUpdate = Time.unscaledTime;

                // Only update in the editor, since that's the only place where we need to actively track the inventory.
                if (HighLogic.LoadedScene == GameScenes.EDITOR)
                {
                    string bodyName;

                    if (FlightGlobals.ActiveVessel != null)
                    {
                        bodyName = FlightGlobals.ActiveVessel.mainBody.bodyName;
                    }
                    else
                    {
                        bodyName = "Kerbin";
                    }

                    if (XenoIndustrySignpost.IsConnected(bodyName))
                    {
                        StartCoroutine(XenoIndustrySignpost.GetClusterioInventory(bodyName, clusterioInventory));
                    }
                }
            }
        }

        private void OnWindowInternal(int id)
        {
            GUILayout.BeginVertical();

            string bodyName = "Kerbin"; // Since this is for launching, only Kerbin is possible for now

            if (!XenoIndustrySignpost.BodyHasServer(bodyName))
            {
                GUILayout.Label("This celestial body has no associated master server.");
            }
            else if (!XenoIndustrySignpost.IsConnected(bodyName))
            {
                GUILayout.Label("Cannot connect to Clusterio master server!");

                GUILayout.Label("Error: " + XenoIndustrySignpost.GetConnectionError(bodyName));

                if (GUILayout.Button("Refresh connection"))
                {
                    XenoIndustrySignpost.RefreshConnection(bodyName);
                }
            }
            else
            {
                // Show ship costs in editor
                if (HighLogic.LoadedScene == GameScenes.EDITOR)
                {
                    //GUILayout.Space(16);
                    //GUILayout.Label("", GUI.skin.horizontalSlider);
                    //GUILayout.Space(8);

                    if (latestLaunchCosts.Count > 0)
                    {
                        // Item names
                        GUILayout.BeginHorizontal();

                        GUILayout.BeginVertical();
                        GUILayout.Label("Resource");
                        GUILayout.Space(8);

                        foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
                        {
                            GUILayout.Label(kvPair.Key + ":");
                        }
                        GUILayout.EndVertical();

                        // Item costs
                        GUILayout.BeginVertical();
                        GUILayout.Label("Required");
                        GUILayout.Space(8);

                        foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
                        {
                            GUILayout.Label(kvPair.Value.ToString());
                        }
                        GUILayout.EndVertical();

                        // Items stocks
                        GUILayout.BeginVertical();
                        GUILayout.Label("Available");
                        GUILayout.Space(8);

                        foreach (KeyValuePair<string, int> kvPair in latestLaunchCosts)
                        {
                            GUILayout.Label((clusterioInventory.ContainsKey(kvPair.Key)) ? clusterioInventory[kvPair.Key].ToString() : 0.ToString());
                        }

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(8);

                        if (GUILayout.Button("Refresh Clusterio inventory"))
                        {
                            StartCoroutine(XenoIndustrySignpost.GetClusterioInventory(bodyName, clusterioInventory));
                        }
                    }
                    else
                    {
                        GUILayout.Label("None");
                    }
                }
            }

            GUILayout.EndVertical();

            // ---
            GUI.DragWindow();
        }
    }
}
