using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using KSP;

namespace XenoIndustry
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class XenoIndustryCargo : MonoBehaviour
    {
        public static bool windowVisible = false;
        public static Rect windowRect;
        public static ModuleXenoIndustryCargo windowActivePart;
        public static string windowResponse;

        private Dictionary<string, int> clusterioInventory = new Dictionary<string, int>();
        private Dictionary<string, int> cargoSelected = new Dictionary<string, int>();

        public void Awake()
        {
            DontDestroyOnLoad(this);

            windowRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 150, 300, 100);
        }

        public void OnGUI()
        {
            if (windowVisible)
            {
                windowRect = GUILayout.Window(22350, windowRect, OnCargoWindowInternal, "XenoIndustry Cargo Interface");
            }
        }

        private void OnCargoWindowInternal(int id)
        {
            GUILayout.BeginVertical();

            string bodyName;

            if (FlightGlobals.ActiveVessel != null)
            {
                bodyName = FlightGlobals.ActiveVessel.mainBody.bodyName;
            }
            else
            {
                bodyName = "Kerbin";
            }

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
            else if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.situation != Vessel.Situations.LANDED && FlightGlobals.ActiveVessel.situation != Vessel.Situations.PRELAUNCH && FlightGlobals.ActiveVessel.situation != Vessel.Situations.SPLASHED)
            {
                GUILayout.Label("Cannot transfer cargo until vessel is landed");
            }
            else
            {
                // Clusterio inventory handling
                if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.TRACKSTATION || HighLogic.LoadedScene == GameScenes.FLIGHT)
                {
                    GUILayout.Label("Available inventory:");

                    if (clusterioInventory.Count > 0)
                    {
                        int spaceRemaining = (int)(windowActivePart.cargoResource.maxAmount - windowActivePart.cargoResource.amount);
                        int netTransfer = 0;

                        // Item names
                        GUILayout.BeginHorizontal();

                        GUILayout.BeginVertical();
                        GUILayout.Label("Resource");
                        GUILayout.Space(8);

                        foreach (KeyValuePair<string, int> kvPair in clusterioInventory)
                        {
                            GUILayout.Label(kvPair.Key + ":");
                        }
                        GUILayout.EndVertical();

                        // Loaded cargo
                        GUILayout.BeginVertical();
                        GUILayout.Label("Loaded");
                        GUILayout.Space(8);

                        foreach (KeyValuePair<string, int> kvPair in clusterioInventory)
                        {
                            if (windowActivePart.carriedCargo.ContainsKey(kvPair.Key))
                            {
                                GUILayout.Label(windowActivePart.carriedCargo[kvPair.Key].ToString());
                            }
                            else
                            {
                                GUILayout.Label("0");
                            }
                        }

                        GUILayout.EndVertical();

                        // Item amount to transfer
                        GUILayout.BeginVertical();
                        GUILayout.Label("Transfer");
                        GUILayout.Space(8);

                        foreach (KeyValuePair<string, int> kvPair in clusterioInventory)
                        {
                            if (!cargoSelected.ContainsKey(kvPair.Key))
                            {
                                cargoSelected[kvPair.Key] = 0;
                            }

                            int result;

                            int.TryParse(GUILayout.TextField(cargoSelected[kvPair.Key].ToString()), out result);

                            cargoSelected[kvPair.Key] = result;
                            spaceRemaining -= result;
                            netTransfer += result;
                        }
                        GUILayout.EndVertical();

                        // Items stocks
                        GUILayout.BeginVertical();
                        GUILayout.Label("Available");
                        GUILayout.Space(8);

                        foreach (KeyValuePair<string, int> kvPair in clusterioInventory)
                        {
                            GUILayout.Label(kvPair.Value.ToString());
                        }

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(8);

                        // Remaining space
                        GUILayout.BeginHorizontal();

                        GUILayout.Label("Space remaining:");
                        GUILayout.Label(spaceRemaining.ToString() + " / " + ((int)windowActivePart.cargoResource.maxAmount).ToString());
                    
                        GUILayout.EndHorizontal();

                        // Net amount of items transferred to part
                        GUILayout.BeginHorizontal();

                        GUILayout.Label("Net items transferred:");
                        GUILayout.Label(netTransfer.ToString());

                        GUILayout.EndHorizontal();

                        GUILayout.Space(8);

                        GUILayout.Label(windowResponse);

                        if (GUILayout.Button("Transfer cargo"))
                        {
                            // Check if we aren't trying to load more than the module can actually hold
                            if (spaceRemaining < 0)
                            {
                                windowResponse = "Cannot load more cargo than the module can hold";
                            }
                            else
                            {
                                bool success = true;

                                // Check if we aren't trying to more cargo than the the server actually has
                                foreach (KeyValuePair<string, int> kvPair in cargoSelected)
                                {
                                    if (clusterioInventory.ContainsKey(kvPair.Key))
                                    {
                                        if (kvPair.Value > clusterioInventory[kvPair.Key])
                                        {
                                            windowResponse = String.Format("Cannot load {0} of item {1}, only {2} is available", kvPair.Value, kvPair.Key, clusterioInventory[kvPair.Key]);
                                            success = false;
                                            break;
                                        }
                                    }
                                }

                                // Check if we aren't trying to unload more cargo than the module has
                                foreach (KeyValuePair<string, int> kvPair in cargoSelected)
                                {
                                    if (kvPair.Value < 0)
                                    {
                                        if (!windowActivePart.carriedCargo.ContainsKey(kvPair.Key))
                                        {
                                            windowResponse = String.Format("Cannot unload {0} item {1}, none currently loaded", kvPair.Value, kvPair.Key);
                                            success = false;
                                            break;
                                        }

                                        if (-kvPair.Value > windowActivePart.carriedCargo[kvPair.Key])
                                        {
                                            windowResponse = String.Format("Cannot unload {0} of item {1}, only {2} currently loaded", kvPair.Value, kvPair.Key, windowActivePart.carriedCargo[kvPair.Key]);
                                            success = false;
                                            break;
                                        }
                                    }
                                }

                                if (success)
                                {
                                    windowResponse = "Cargo transfer successful";

                                    foreach (KeyValuePair<string, int> kvPair in cargoSelected)
                                    {
                                        // Sanity check
                                        if (kvPair.Value == 0)
                                        {
                                            continue;
                                        }

                                        Action<bool> cargoCallback = delegate (bool requestSuccessful) 
                                        {
                                            if (requestSuccessful)
                                            {
                                                if (windowActivePart.carriedCargo.ContainsKey(kvPair.Key))
                                                {
                                                    windowActivePart.carriedCargo[kvPair.Key] += kvPair.Value;
                                                }
                                                else
                                                {
                                                    windowActivePart.carriedCargo[kvPair.Key] = kvPair.Value;
                                                }
                                            }
                                        };

                                        if (kvPair.Value > 0)
                                        {
                                            StartCoroutine(XenoIndustrySignpost.RemoveItemsFromClusterio(bodyName, clusterioInventory, kvPair.Key, kvPair.Value, cargoCallback));
                                        }
                                        else
                                        {
                                            StartCoroutine(XenoIndustrySignpost.AddItemsToClusterio(bodyName, kvPair.Key, -kvPair.Value, cargoCallback));
                                        }
                                    }

                                    windowActivePart.cargoResource.amount += netTransfer;

                                    cargoSelected.Clear();
                                }
                            }
                        }
                    }
                    else
                    {
                        GUILayout.Label("No cargo available for transfer.");
                    }

                    GUILayout.Space(16);

                    if (GUILayout.Button("Refresh Clusterio inventory"))
                    {
                        StartCoroutine(XenoIndustrySignpost.GetClusterioInventory(bodyName, clusterioInventory));
                    }
                }
            }

            GUILayout.EndVertical();

            // ---
            GUI.DragWindow();
        }
    }
}
