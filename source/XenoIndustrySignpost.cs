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
    public class XenoIndustrySignpostBodyAddress
    {
        public string masterIP;
        public string masterPort;
        public string masterAuthToken;

        public XenoIndustrySignpostBodyAddress(string masterIP, string masterPort, string masterAuthKey = null)
        {
            this.masterIP = masterIP;
            this.masterPort = masterPort;

            if (masterAuthKey != null)
            {
                this.masterAuthToken = masterAuthKey;
            }
        }
    }

    public static class XenoIndustrySignpost
    {
        public static readonly String MOD_PATH = "GameData/XenoIndustry/";

        public static Dictionary<string, XenoIndustrySignpostBodyAddress> bodyAddresses = new Dictionary<string, XenoIndustrySignpostBodyAddress>();

        public static void LoadSignpost()
        {
            StreamReader reader = new StreamReader(MOD_PATH + "signpost.json");

            if (reader != null)
            {
                JSONNode signpostNode = JSON.Parse(reader.ReadToEnd());

                foreach (KeyValuePair<string, JSONNode> node in signpostNode.AsObject)
                {
                    Debug.Log("XenoIndustrySignpost: loading celestial body " + node.Key);

                    JSONNode bodyNode = node.Value;

                    if (bodyNode["masterIP"] != null && bodyNode["masterPort"] != null)
                    {
                        bodyAddresses[node.Key] = new XenoIndustrySignpostBodyAddress(bodyNode["masterIP"], bodyNode["masterPort"], (bodyNode["masterAuthToken"] != null) ? bodyNode["masterAuthToken"] : null);
                        Debug.Log("XenoIndustrySignpost: adding body, address " + bodyNode["masterIP"] + ":" + bodyNode["masterPort"] + ((bodyNode["masterAuthToken"] != null) ? (", auth token " + bodyNode["masterAuthToken"]) : ", no auth token"));
                    }
                    else
                    {
                        Debug.Log("XenoIndustrySignpost: cannot load celestial body " + node.Key + ", lacks full address");
                    }
                }
            }
        }

        public static void WriteOutCelestialBodies()
        {
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                Debug.Log("XenoIndustrySignpost: celestial body " + body.bodyName);
            }
        }

        public static bool BodyHasServer(string bodyName)
        {
            return bodyAddresses.ContainsKey(bodyName);
        }

        public static bool IsConnected(string bodyName)
        {
            if (!BodyHasServer(bodyName))
            {
                return false;
            }

            XenoIndustrySignpostBodyAddress address = bodyAddresses[bodyName];

            return ClusterioConnector.IsConnected(address.masterIP, address.masterPort);
        }

        public static string GetConnectionError(string bodyName)
        {
            if (!BodyHasServer(bodyName))
            {
                return null;
            }

            XenoIndustrySignpostBodyAddress address = bodyAddresses[bodyName];

            return ClusterioConnector.GetConnectionError(address.masterIP, address.masterPort);
        }

        public static void RefreshConnection(string bodyName)
        {
            if (!BodyHasServer(bodyName))
            {
                return;
            }

            XenoIndustrySignpostBodyAddress address = bodyAddresses[bodyName];

            if (!ClusterioConnector.IsConnected(address.masterIP, address.masterPort))
            {
                ClusterioConnector.ConntectToMaster(address.masterIP, address.masterPort, address.masterAuthToken);
            }
            else
            {
                ClusterioConnector.RefreshConnectionFromAddress(address.masterIP, address.masterPort);
            }
        }

        public static IEnumerator GetClusterioInventory(string bodyName, Dictionary<string, int> clusterioInventory)
        {
            if (!BodyHasServer(bodyName))
            {
                Debug.Log(String.Format("XenoIndustrySignpost: Sending a GetClusterioInventory request to a celestial {0} which has no assigned server!", bodyName));
                yield break;
            }

            XenoIndustrySignpostBodyAddress address = bodyAddresses[bodyName];

            // Connect to the server if we aren't connected yet
            if (!ClusterioConnector.IsConnected(address.masterIP, address.masterPort))
            {
                ClusterioConnector.ConntectToMaster(address.masterIP, address.masterPort, address.masterAuthToken);
            }

            string apiRequest = "/api/inventory";

            ClusterioMessage resultMessage = new ClusterioMessage();

            yield return ClusterioConnector.SendGetRequest(address.masterIP, address.masterPort, apiRequest, resultMessage);

            if (resultMessage.result == ClusterioMessageResult.SUCCESS)
            {
                // Inventory request successful, refresh the inventory
                clusterioInventory.Clear();

                JSONNode rootNode = JSON.Parse(resultMessage.text);

                foreach (JSONNode childNode in rootNode.Children)
                {
                    if (childNode["name"] == null)
                    {
                        Debug.Log("GetClusterioInventory: an item is missing its name!");
                        continue;
                    }

                    if (childNode["count"] == null)
                    {
                        Debug.Log(String.Format("GetClusterioInventory: item {0} is missing its count number!", childNode["name"]));
                        continue;
                    }

                    clusterioInventory[childNode["name"]] = childNode["count"].AsInt;
                }
            }
        }

        public static IEnumerator AddItemsToClusterio(string bodyName, string itemName, int count, Action<bool> onFinished = null)
        {
            if (!BodyHasServer(bodyName))
            {
                Debug.Log(String.Format("XenoIndustrySignpost: Sending a AddItemsToClusterio request to a celestial {0} which has no assigned server!", bodyName));
                yield break;
            }

            XenoIndustrySignpostBodyAddress address = bodyAddresses[bodyName];

            // Connect to the server if we aren't connected yet
            if (!ClusterioConnector.IsConnected(address.masterIP, address.masterPort))
            {
                ClusterioConnector.ConntectToMaster(address.masterIP, address.masterPort, address.masterAuthToken);
            }

            Dictionary<string, string> sendValues = new Dictionary<string, string>();

            sendValues["name"] = itemName;
            sendValues["count"] = count.ToString();

            Debug.Log(String.Format("ClusterioUtil: adding {0} of {1} to item repository", count, itemName));

            string apiCommand = "/api/place";

            ClusterioMessage resultMessage = new ClusterioMessage();

            yield return ClusterioConnector.SendPostRequest(address.masterIP, address.masterPort, apiCommand, resultMessage, sendValues);

            if (resultMessage.result == ClusterioMessageResult.SUCCESS)
            {
                if (onFinished != null)
                {
                    onFinished(true);
                }
            }
            else
            {
                if (onFinished != null)
                {
                    onFinished(false);
                }
            }
        }

        public static IEnumerator RemoveItemsFromClusterio(string bodyName, Dictionary<string, int> clusterioInventory, string itemName, int count, Action<bool> onFinished = null)
        {
            if (!BodyHasServer(bodyName))
            {
                Debug.Log(String.Format("XenoIndustrySignpost: Sending a RemoveItemsFromClusterio request to a celestial {0} which has no assigned server!", bodyName));
                yield break;
            }

            XenoIndustrySignpostBodyAddress address = bodyAddresses[bodyName];

            // Connect to the server if we aren't connected yet
            if (!ClusterioConnector.IsConnected(address.masterIP, address.masterPort))
            {
                ClusterioConnector.ConntectToMaster(address.masterIP, address.masterPort, address.masterAuthToken);
            }

            // First, we need to see what items are stored in the Clusterio server, and wait for that request to be finished
            yield return XenoIndustryCore.instance.StartCoroutine(XenoIndustrySignpost.GetClusterioInventory(bodyName, clusterioInventory));

            // Don't do a remove request if there's not enough items, or if the item isn't there at all
            if (!clusterioInventory.ContainsKey(itemName) || clusterioInventory[itemName] < count)
            {
                yield break;
            }

            Dictionary<string, string> sendValues = new Dictionary<string, string>();

            sendValues["name"] = itemName;
            sendValues["count"] = count.ToString();

            Debug.Log(String.Format("ClusterioUtil: removing {0} of {1} from item repository", count, itemName));

            string apiCommand = "/api/remove";

            ClusterioMessage resultMessage = new ClusterioMessage();

            yield return ClusterioConnector.SendPostRequest(address.masterIP, address.masterPort, apiCommand, resultMessage, sendValues);

            if (resultMessage.result == ClusterioMessageResult.SUCCESS)
            {
                if (onFinished != null)
                {
                    onFinished(true);
                }
            }
            else
            {
                if (onFinished != null)
                {
                    onFinished(false);
                }
            }
        }
    }
}
