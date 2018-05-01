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
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class XenoIndustryScienceTransfer : MonoBehaviour
    {
        public static readonly String MOD_PATH = "GameData/XenoIndustry/";
        public static readonly String RESOURCE_PATH = "XenoIndustry/Resource/";

        private ApplicationLauncherButton stockToolbarButton = null;

        private bool windowVisible = false;

        private Rect windowRect;

        private Dictionary<string, int> clusterioInventory;

        private float sciencePerSciencePack = 10f;

        private float lastClusterioUpdate = 0f;

        public void Awake()
        {
            DontDestroyOnLoad(this);

            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIApplicationLauncherDestroyed);

            StreamReader reader = new StreamReader(MOD_PATH + "config.json");

            if (reader != null)
            {
                JSONNode modConfig = JSON.Parse(reader.ReadToEnd());

                if (modConfig["sciencePerSciencePack"] != null)
                {
                    Debug.Log(String.Format("ClusterioTest: sciencePerSciencePack is {0}", modConfig["sciencePerSciencePack"]));
                    sciencePerSciencePack = modConfig["sciencePerSciencePack"];
                }
            }

            windowRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 150, 300, 100);

            clusterioInventory = new Dictionary<string, int>();
        }

        /*public void Start()
        {
            Debug.Log("ClusterioTest: Start");
        }*/

        private void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready && stockToolbarButton == null)
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

                if (stockToolbarButton == null) Debug.Log("XenoIndustryScienceTransfer: could not register stock toolbar button!");
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
                windowRect = GUILayout.Window(22349, windowRect, OnClusterioWindowInternal, "Clusterio Science Transfer");
            }
        }

        private void Update()
        {
            if (Time.unscaledTime > lastClusterioUpdate + 10)
            {
                lastClusterioUpdate = Time.unscaledTime;

                // Periodically update Clusterio inventory if not ingame
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                {
                    if (ClusterioConnector.IsConnected())
                    {
                        StartCoroutine(ClusterioUtil.GetClusterioInventory(clusterioInventory));
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
            else if (ResearchAndDevelopment.Instance == null)
            {
                GUILayout.Label("Cannot transfer science in sandbox mode!");
            }
            else
            {
                // Science transfer to Factorio
                //if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.TRACKSTATION || HighLogic.LoadedScene == GameScenes.FLIGHT)
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION || HighLogic.LoadedScene == GameScenes.FLIGHT)
                {
                    /*GUILayout.Space(16);
                    GUILayout.Label("", GUI.skin.horizontalSlider);
                    GUILayout.Space(8);*/

                    int scienceTransferAmount = (int)(ResearchAndDevelopment.Instance.Science - (ResearchAndDevelopment.Instance.Science % sciencePerSciencePack));

                    GUILayout.Label(String.Format("Can use {0} science to transfer {1} science packs to Factorio.", scienceTransferAmount, (scienceTransferAmount / sciencePerSciencePack).ToString()));

                    GUILayout.Space(8);

                    if (GUILayout.Button("Transfer Science to Clusterio") && ResearchAndDevelopment.Instance != null)
                    {
                        Debug.Log("ClusterioTest: transferring science to Factorio");

                        StartCoroutine(TransferScienceToClusterio());
                    }

                    GUILayout.Space(8);

                    if (GUILayout.Button("Refresh Clusterio inventory"))
                    {
                        StartCoroutine(ClusterioUtil.GetClusterioInventory(clusterioInventory));
                    }
                }
            }

            GUILayout.EndVertical();

            // ---
            GUI.DragWindow();
        }

        IEnumerator TransferScienceToClusterio()
        {
            int scienceTransferAmount = (int)(ResearchAndDevelopment.Instance.Science - (ResearchAndDevelopment.Instance.Science % sciencePerSciencePack));
            int sciencePackTransferAmount = (int)(scienceTransferAmount / sciencePerSciencePack);

            ClusterioMessage resultMessage = new ClusterioMessage();

            yield return StartCoroutine(ClusterioUtil.AddItemsToClusterio("space-science-pack", sciencePackTransferAmount, (success) => 
                {
                    if (success)
                    {
                        // Show results as text
                        Debug.Log("TransferScienceToClusterio: science sent successfully");

                        // Only subtract science in KSP if the request successfuly reached the Clusterio master server
                        ResearchAndDevelopment.Instance.AddScience(-scienceTransferAmount, TransactionReasons.ScienceTransmission);
                    }
                }
            ));
        }
    }
}
