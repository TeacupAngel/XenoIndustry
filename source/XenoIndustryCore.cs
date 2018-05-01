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
//using KSP.IO;
using KSP.UI.Screens;

namespace XenoIndustry
{
    //[KSPAddon(KSPAddon.Startup.Instantly, true)]
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    //[KSPScenario(ScenarioCreationOptions.AddToNewCareerGames | ScenarioCreationOptions.AddToExistingCareerGames, GameScenes.SPACECENTER)]
    public class XenoIndustryCore : MonoBehaviour
    {
        public static readonly String MOD_PATH = "GameData/XenoIndustry/";
        public static readonly String RESOURCE_PATH = "XenoIndustry/Resource/";

        public static XenoIndustryCore instance;

        private ApplicationLauncherButton stockToolbarButton = null;

        private bool windowVisible = false;

        private Rect windowRect;

        private Dictionary<string, int> clusterioInventory;

        private float lastConnectionUpdate = 0f;

        private bool debug = false;

        // TODO
        //
        // - XenoIndustryLaunchCosts
        // --- loading from config files using a rule system
        // - ClusterioConnector
        // --- ability to connect to multiple masters and hold multiple connections open
        // --- only acts on external commands, no "smart" behaviour of its own
        // 
        // - a single class
        //
        // ADD
        // - XenoIndustryCargo - dedicated cargo tanks (separate for solids and liquids) than can be loaded and unloaded using an interface when on the launchpad
        // - XenoIndustrySignpost 
        // --- class handling the Clusterio connections based on planets (planetName => urlAddress dictionary)
        // --- loading from signpost.json file (later from a signpost server?)
        // --- other classes should route their requests through the XenoIndustrySignpost one, which then in turn sends the proper url address to ClusterioConnector
        // --- should even decide which connections to keep open and how long
        //

        public void Awake()
        {
            instance = this;

            DontDestroyOnLoad(this);

            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIApplicationLauncherDestroyed);

            StreamReader reader = new StreamReader(MOD_PATH + "config.json");

            string masterIP = "localhost";
            string masterPort = "8080";
            string masterAuthToken = null;

            if (reader != null)
            {
                JSONNode modConfig = JSON.Parse(reader.ReadToEnd());

                if (modConfig["masterIP"] != null)
                {
                    Debug.Log(String.Format("XenoIndustryCore: masterIP is {0}", modConfig["masterIP"]));
                    masterIP = modConfig["masterIP"];

                    if (masterIP.ToLowerInvariant() != "localhost")
                    {
                        masterIP = "http://" + masterIP;
                    }
                }

                if (modConfig["masterPort"] != null)
                {
                    Debug.Log(String.Format("XenoIndustryCore: masterPort is {0}", modConfig["masterPort"]));
                    masterPort = modConfig["masterPort"];
                }

                if (modConfig["masterAuthToken"] != null)
                {
                    Debug.Log(String.Format("XenoIndustryCore: masterAuthToken is {0}", modConfig["masterAuthToken"]));
                    masterAuthToken = modConfig["masterAuthToken"];
                }

                if (modConfig["debug"] != null)
                {
                    Debug.Log(String.Format("ClusterioTest: sciencePerSciencePack is {0}", modConfig["sciencePerSciencePack"]));
                    debug = modConfig["debug"];
                }
            }

            ClusterioConnector.ConntectToMonoBehaviour(this);
            ClusterioConnector.ConntectToMaster(masterIP, masterPort, masterAuthToken);

            windowRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 150, 300, 100);

            clusterioInventory = new Dictionary<string, int>();
        }

        /*public void Start()
        {
            Debug.Log("ClusterioTest: Start");
        }*/

        private void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready && stockToolbarButton == null && debug)
            {
                stockToolbarButton = ApplicationLauncher.Instance.AddModApplication(
                    OnToolbarClusterioButtonOn,
                    OnToolbarClusterioButtonOff,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                    (Texture)GameDatabase.Instance.GetTexture(RESOURCE_PATH + "icon_clusterio", false));

                if (stockToolbarButton == null) Debug.Log("XenoIndustryCore: could not register stock toolbar button!");
            }
        }

        private void OnGUIApplicationLauncherDestroyed()
        {
            if (stockToolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarButton);
                stockToolbarButton = null;
            }
        }

        void OnToolbarClusterioButtonOn()
        {
            windowVisible = true;
        }

        void OnToolbarClusterioButtonOff()
        {
            windowVisible = false;
        }

        public void OnGUI()
        {
            if (windowVisible)
            {
                windowRect = GUILayout.Window(22347, windowRect, OnClusterioWindowInternal, "Clusterio Debug Interface");
            }
        }

        private void Update()
        {
            if (Time.unscaledTime > lastConnectionUpdate + 10)
            {
                lastConnectionUpdate = Time.unscaledTime;

                // Periodically update Clusterio inventory if not ingame
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                {
                    if (ClusterioConnector.IsConnected())
                    {
                        StartCoroutine(ClusterioUtil.GetClusterioInventory(clusterioInventory));
                    }
                    else
                    {
                        StartCoroutine(ClusterioConnector.RefreshConnection());
                    }
                }
            }
        }

        private void OnClusterioWindowInternal(int id)
        {
            GUILayout.BeginVertical();

            if (!ClusterioConnector.IsConnected())
            {
                GUILayout.Label("Cannot connect to Clusterio master server!");

                GUILayout.Label("Error: " + ClusterioConnector.GetConnectionError());

                if (GUILayout.Button("Refresh connection"))
                {
                    StartCoroutine(ClusterioConnector.RefreshConnection());
                }
            }
            else
            {
                // Clusterio inventory handling
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.TRACKSTATION || HighLogic.LoadedScene == GameScenes.FLIGHT)
                {
                    GUILayout.Label("Clusterio inventory:");

                    if (clusterioInventory.Count > 0)
                    {
                        GUILayout.BeginHorizontal();

                        GUILayout.BeginVertical();
                        foreach (KeyValuePair<string, int> kvPair in clusterioInventory)
                        {
                            GUILayout.Label(kvPair.Key + ":");
                        }
                        GUILayout.EndVertical();

                        GUILayout.BeginVertical();
                        foreach (KeyValuePair<string, int> kvPair in clusterioInventory)
                        {
                            GUILayout.Label(kvPair.Value.ToString());
                        }
                        GUILayout.EndVertical();

                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.Label("Clusterio inventory is empty.");
                    }

                    GUILayout.Space(8);

                    if (GUILayout.Button("Refresh Clusterio inventory"))
                    {
                        StartCoroutine(ClusterioUtil.GetClusterioInventory(clusterioInventory));
                    }
                }

                if (debug)
                {
                    if (GUILayout.Button("Add 1000 rocket component items"))
                    {
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("low-density-structure", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("rocket-control-unit", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("solar-panel", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("uranium-238", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("accumulator", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("electric-mining-drill", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("electric-furnace", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("radar", 1000));
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("processing-unit", 1000));
                    }

                    if (GUILayout.Button("Add 1000 rocket fuel"))
                    {
                        StartCoroutine(ClusterioUtil.AddItemsToClusterio("rocket-fuel", 1000));
                    }
                }

            }

            GUILayout.EndVertical();

            // ---
            GUI.DragWindow();
        }
    }
}
