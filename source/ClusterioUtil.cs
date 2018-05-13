using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

using UnityEngine;

using SimpleJSON;

namespace XenoIndustry
{
    public static class ClusterioUtil
    {
        /*public static IEnumerator GetClusterioInventory(Dictionary<string, int> clusterioInventory)
        {
            string apiRequest = "/api/inventory";

            ClusterioMessage resultMessage = new ClusterioMessage();

            yield return ClusterioConnector.SendGetRequest(apiRequest, resultMessage);

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

        public static IEnumerator AddItemsToClusterio(string itemName, int count, Action<bool> onFinished = null)
        {
            Dictionary<string, string> sendValues = new Dictionary<string, string>();

            sendValues["name"] = itemName;
            sendValues["count"] = count.ToString();

            Debug.Log(String.Format("ClusterioUtil: adding {0} of {1} to item repository", count, itemName));

            string apiCommand = "/api/place";

            ClusterioMessage resultMessage = new ClusterioMessage();

            yield return ClusterioConnector.SendPostRequest(apiCommand, resultMessage, sendValues);

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

        public static IEnumerator RemoveItemsFromClusterio(Dictionary<string, int> clusterioInventory, string itemName, int count, Action<bool> onFinished = null)
        {
            // First, we need to see what items are stored in the Clusterio server, and wait for that request to be finished
            yield return XenoIndustryCore.instance.StartCoroutine(ClusterioUtil.GetClusterioInventory(clusterioInventory));

            // Don't do a remove request if there's not enough items
            if (clusterioInventory[itemName] < count)
            {
                yield break;
            }

            Dictionary<string, string> sendValues = new Dictionary<string, string>();

            sendValues["name"] = itemName;
            sendValues["count"] = count.ToString();

            Debug.Log(String.Format("ClusterioUtil: removing {0} of {1} from item repository", count, itemName));

            string apiCommand = "/api/remove";

            ClusterioMessage resultMessage = new ClusterioMessage();

            yield return ClusterioConnector.SendPostRequest(apiCommand, resultMessage, sendValues);

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
        }*/
    }
}
