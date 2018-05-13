using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

using UnityEngine;
using UnityEngine.Networking;

namespace XenoIndustry
{
    public enum ClusterioMessageResult
    {
        PENDING,
        SUCCESS,
        ERROR
    }

    public class ClusterioMessage
    {
        public ClusterioMessageResult result = ClusterioMessageResult.PENDING;
        public string text = "";
    }

    public enum ClusterioConnectionStatus
    {
        NO_CONNECTION, // Disconnected
        CONNECTING, // Attempting to connect
        REFRESHING, // Refreshing a successful connection
        CONNECTION_SUCCESS, // Successfully connected to master server
        CONNECTION_ERROR // An error happened in the connection process
    }

    public class ClusterioConnection
    {
        public string masterIP;
        public string masterPort;
        public string masterAuthToken = null;

        public ClusterioConnectionStatus connectionStatus = ClusterioConnectionStatus.NO_CONNECTION;
        public string connectionError;

        public ClusterioConnection(string masterIP, string masterPort, string masterAuthToken = null)
        {
            this.masterIP = masterIP;
            this.masterPort = masterPort;

            if (masterAuthToken != null)
            {
                this.masterAuthToken = masterAuthToken;
            }
        }
    }

    public static class ClusterioConnector
    {
        private static Dictionary<string, ClusterioConnection> connections = new Dictionary<string, ClusterioConnection>();

        public static void ConntectToMaster(string IP, string port, string authToken = null)
        {
            Debug.Log(String.Format("ClusterioConnector: connecting to master at address {0}:{1}", IP, port));

            string masterAddress = IP + ":" + port;

            ClusterioConnection connection = new ClusterioConnection(IP, port, authToken);

            connections[masterAddress] = connection;

            XenoIndustryCore.instance.StartCoroutine(RefreshConnection(connection));
        }

        public static void RefreshConnectionFromAddress(string masterIP, string masterPort)
        {
            string masterAddress = masterIP + ":" + masterPort;

            if (!connections.ContainsKey(masterAddress))
            {
                return;
            }

            ClusterioConnection connection = connections[masterAddress];

            XenoIndustryCore.instance.StartCoroutine(RefreshConnection(connection));
        }

        public static IEnumerator RefreshConnection(ClusterioConnection connection)
        {
            if (connection.connectionStatus == ClusterioConnectionStatus.CONNECTING || connection.connectionStatus == ClusterioConnectionStatus.REFRESHING)
            {
                yield break;
            }

            if (XenoIndustryCore.instance == null)
            {
                Debug.Log("ClusterioConnector: not connected to Unity MonoBehaviour! Cannot check connection to Clusterio server");
                yield break;
            }

            if (connection.connectionStatus == ClusterioConnectionStatus.CONNECTION_SUCCESS)
            {
                connection.connectionStatus = ClusterioConnectionStatus.REFRESHING;
            }
            else
            {
                connection.connectionStatus = ClusterioConnectionStatus.CONNECTING;
            }

            WWW www = new WWW(connection.masterIP + ":" + connection.masterPort);

            yield return www;

            if (www.error != null)
            {
                connection.connectionStatus = ClusterioConnectionStatus.CONNECTION_ERROR;
                connection.connectionError = www.error;

                Debug.Log("ClusterioConnector: cannot connect to master server! Error: " + www.error);
            }
            else
            {
                connection.connectionStatus = ClusterioConnectionStatus.CONNECTION_SUCCESS;
            }
        }

        private static ClusterioConnection GetRequestClusterioConnection(string masterIP, string masterPort, ClusterioMessage resultMessage)
        {
            string masterAddress = masterIP + ":" + masterPort;

            if (!connections.ContainsKey(masterAddress))
            {
                resultMessage.result = ClusterioMessageResult.ERROR;
                resultMessage.text = "Master server connection not set";

                Debug.Log("ClusterioConnector: cannot send requests when master IP or port aren't set!");
                return null;
            }

            if (!IsConnected(masterIP, masterPort))
            {
                resultMessage.result = ClusterioMessageResult.ERROR;
                resultMessage.text = "No master server connection";

                Debug.Log("ClusterioConnector: cannot send requests, not connected to master server");
                return null;
            }

            ClusterioConnection connection = connections[masterAddress];

            return connection;
        }

        private static void ReturnRequestMessage(ClusterioConnection connection, UnityWebRequest webRequest, ClusterioMessage resultMessage)
        {
            if (webRequest.isHttpError || webRequest.responseCode == 0) // Response code is 0 when server cannot be reached
            {
                resultMessage.result = ClusterioMessageResult.ERROR;
                resultMessage.text = webRequest.error;

                Debug.Log("ClusterioConnector: request failed! Error: " + webRequest.error);

                XenoIndustryCore.instance.StartCoroutine(RefreshConnection(connection));
            }
            else
            {
                resultMessage.result = ClusterioMessageResult.SUCCESS;
                resultMessage.text = webRequest.downloadHandler.text;
            }
        }

        public static IEnumerator SendPostRequest(string masterIP, string masterPort, string apiCall, ClusterioMessage resultMessage, Dictionary<string, string> sendValues = null)
        {
            ClusterioConnection connection = GetRequestClusterioConnection(masterIP, masterPort, resultMessage);

            if (connection == null)
            {
                yield break;
            }

            UnityWebRequest webRequest = UnityWebRequest.Post(masterIP + ":" + masterPort + "/" + apiCall, sendValues);

            if (connection.masterAuthToken != null)
            {
                webRequest.SetRequestHeader("x-access-token", connection.masterAuthToken);
            }
            
            yield return webRequest.Send();

            ReturnRequestMessage(connection, webRequest, resultMessage);
        }

        public static IEnumerator SendGetRequest(string masterIP, string masterPort, string apiCall, ClusterioMessage resultMessage)
        {
            ClusterioConnection connection = GetRequestClusterioConnection(masterIP, masterPort, resultMessage);

            if (connection == null)
            {
                yield break;
            }

            UnityWebRequest webRequest = UnityWebRequest.Get(masterIP + ":" + masterPort + "/" + apiCall);

            if (connection.masterAuthToken != null)
            {
                webRequest.SetRequestHeader("x-access-token", connection.masterAuthToken);
            }

            yield return webRequest.Send();

            ReturnRequestMessage(connection, webRequest, resultMessage);
        }

        public static bool IsConnected(string masterIP, string masterPort)
        {
            string masterAddress = masterIP + ":" + masterPort;

            if (!connections.ContainsKey(masterAddress))
            {
                return false;
            }

            ClusterioConnection connection = connections[masterAddress];

            return connection.connectionStatus == ClusterioConnectionStatus.CONNECTION_SUCCESS || connection.connectionStatus == ClusterioConnectionStatus.REFRESHING;
        }

        public static string GetConnectionError(string masterIP, string masterPort)
        {
            string masterAddress = masterIP + ":" + masterPort;

            if (!connections.ContainsKey(masterAddress))
            {
                return null;
            }

            ClusterioConnection connection = connections[masterAddress];

            return (connection.connectionStatus == ClusterioConnectionStatus.CONNECTION_ERROR) ? connection.connectionError : null;
        }
    }
}
