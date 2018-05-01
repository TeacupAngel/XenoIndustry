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

    public static class ClusterioConnector
    {
        public static string masterIP { get; private set; }
        public static string masterPort { get; private set; }
        public static string masterAuthToken { get; private set; }

        private static ClusterioConnectionStatus connectionStatus;
        private static string connectionError;

        private static MonoBehaviour mbLink;

        public static void ConntectToMonoBehaviour(MonoBehaviour monobehaviour)
        {
            mbLink = monobehaviour;

            Debug.Log("ClusterioConnector: connected to Unity MonoBehaviour");
        }

        public static void ConntectToMaster(string IP, string port, string authToken = null)
        {
            Debug.Log(String.Format("ClusterioConnector: connecting to master at address {0}:{1}", IP, port));

            masterIP = IP;
            masterPort = port;
            masterAuthToken = authToken;

            mbLink.StartCoroutine(RefreshConnection());
        }

        public static IEnumerator RefreshConnection()
        {
            if (connectionStatus == ClusterioConnectionStatus.CONNECTING || connectionStatus == ClusterioConnectionStatus.REFRESHING)
            {
                yield break;
            }

            if (mbLink == null)
            {
                Debug.Log("ClusterioConnector: not connected to Unity MonoBehaviour! Cannot check connection to Clusterio server");
                yield break;
            }

            if (connectionStatus == ClusterioConnectionStatus.CONNECTION_SUCCESS)
            {
                connectionStatus = ClusterioConnectionStatus.REFRESHING;
            }
            else
            {
                connectionStatus = ClusterioConnectionStatus.CONNECTING;
            }

            WWW www = new WWW(masterIP + ":" + masterPort);

            yield return www;

            if (www.error != null)
            {
                connectionStatus = ClusterioConnectionStatus.CONNECTION_ERROR;
                connectionError = www.error;

                Debug.Log("ClusterioConnector: cannot connect to master server! Error: " + www.error);
            }
            else
            {
                connectionStatus = ClusterioConnectionStatus.CONNECTION_SUCCESS;
            }
        }

        private static bool CanSendRequest(ClusterioMessage resultMessage)
        {
            if (masterIP == null || masterPort == null)
            {
                resultMessage.result = ClusterioMessageResult.ERROR;
                resultMessage.text = "Master server connection not set";

                Debug.Log("ClusterioConnector: cannot send requests when master IP or port aren't set!");
                return false;
            }

            if (!IsConnected())
            {
                resultMessage.result = ClusterioMessageResult.ERROR;
                resultMessage.text = "No master server connection";

                Debug.Log("ClusterioConnector: cannot send requests, not connected to master server");
                return false;
            }

            return true;
        }

        private static void ReturnRequestMessage(UnityWebRequest webRequest, ClusterioMessage resultMessage)
        {
            if (webRequest.isHttpError || webRequest.responseCode == 0) // Response code is 0 when server cannot be reached
            {
                resultMessage.result = ClusterioMessageResult.ERROR;
                resultMessage.text = webRequest.error;

                Debug.Log("ClusterioConnector: request failed! Error: " + webRequest.error);

                mbLink.StartCoroutine(RefreshConnection());
            }
            else
            {
                resultMessage.result = ClusterioMessageResult.SUCCESS;
                resultMessage.text = webRequest.downloadHandler.text;
            }
        }

        public static IEnumerator SendPostRequest(string apiCall, ClusterioMessage resultMessage, Dictionary<string, string> sendValues = null)
        {
            if (!CanSendRequest(resultMessage))
            {
                yield break;
            }

            UnityWebRequest webRequest = UnityWebRequest.Post(masterIP + ":" + masterPort + "/" + apiCall, sendValues);

            if (masterAuthToken != null)
            {
                webRequest.SetRequestHeader("x-access-token", masterAuthToken);
            }
            
            yield return webRequest.Send();

            ReturnRequestMessage(webRequest, resultMessage);
        }

        public static IEnumerator SendGetRequest(string apiCall, ClusterioMessage resultMessage)
        {
            if (!CanSendRequest(resultMessage))
            {
                yield break;
            }

            UnityWebRequest webRequest = UnityWebRequest.Get(masterIP + ":" + masterPort + "/" + apiCall);

            if (masterAuthToken != null)
            {
                webRequest.SetRequestHeader("x-access-token", masterAuthToken);
            }

            yield return webRequest.Send();

            ReturnRequestMessage(webRequest, resultMessage);
        }

        public static bool IsConnected()
        {
            return connectionStatus == ClusterioConnectionStatus.CONNECTION_SUCCESS || connectionStatus == ClusterioConnectionStatus.REFRESHING;
        }

        public static string GetConnectionError()
        {
            return (connectionStatus == ClusterioConnectionStatus.CONNECTION_ERROR) ? connectionError : null;
        }
    }
}
