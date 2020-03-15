using UnityEngine;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// this file contains code relating to allowing triggers to be called remotely
/// </summary>
namespace KeyMaster
{
    [System.Serializable]
    public class KeyMasterRemoteConfig
    {
        public int port;
        public string ipAddress;
    }

    public enum Topic
    {
        None            = 0,
        RemoteCommand   = 1
    }

    [System.Serializable]
    public class RemoteKeymasterCommand
    {
        public ControlPage page;
        public string label;
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }



    [System.Serializable]
    public class NetworkMessage
    {
        public Topic topic;
        public string payload;

        public T Deseralize<T>() where T : RemoteKeymasterCommand
        {
            return JsonUtility.FromJson<T>(payload);
        }

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

    static public partial class Utilities
    {
        static public void WriteRemoteConfig(string contents, bool allowRemote)
        {
            var fullFilePath = Path.Combine(Application.streamingAssetsPath, "keymasterRemoteConfig.json");
            if (!Directory.Exists(Application.streamingAssetsPath))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath);
            }

            if (!allowRemote && File.Exists(fullFilePath))
            {
                File.Delete(fullFilePath);
                return;
            }

            using (StreamWriter sw = new StreamWriter(fullFilePath))
            {
                sw.Write(contents);
            }
        }

        static public KeyMasterRemoteConfig LoadRemoteConfig()
        {
            var fullFilePath = Path.Combine(Application.streamingAssetsPath, "keymasterRemoteConfig.json");
            if (!Directory.Exists(Application.streamingAssetsPath) || !File.Exists(fullFilePath)) return null;

            var contents = string.Empty;
            using (StreamReader sr = new StreamReader(fullFilePath))
            {
                contents = sr.ReadToEnd();
            }

            if (string.IsNullOrEmpty(contents)) return null;

            return JsonUtility.FromJson<KeyMasterRemoteConfig>(contents);
        }

        static public void StartKeymasterRemote(int inPort)
        {
            port = inPort;
            isListening = true;
            receiveThread = new Thread(
                new ThreadStart(Listen));
            receiveThread.IsBackground = true;
            receiveThread.Name = "NetworkUDPService";
            receiveThread.Start();
            Debug.LogFormat("#KEYMASTER# now listening for remote commands on port {0}", port);
        }

        static Thread receiveThread;
        static UdpClient listenClient;
        static int port;
        static public bool isListening;

        static void Listen()
        {
            //if (listenClient != null) return;
            listenClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            listenClient.Client.ReceiveTimeout = 500;

            while (isListening)
            {
                try
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);
                    byte[] data = listenClient.Receive(ref anyIP);
                    string text = Encoding.UTF8.GetString(data);
                    ParseMessage(text);
                }

                catch (System.Exception err)
                {
                    // this sends logspam on listen timeout (500 ms)
                    //Utilities.KeyMaster.EnqueueRemoteCommand(() => Debug.LogFormat("#KEYMASTER# timeout"));
                }
                Thread.Sleep(50);
            }
            listenClient.Close();
        }

        static void ParseMessage(string msg)
        {
            //Debug.LogFormat("#appBridge# received msg {0}", msg);
            NetworkMessage parsedMessage = null;
            try
            {
                parsedMessage = JsonUtility.FromJson<NetworkMessage>(msg);
                // topics can be used to sort various message receipts
                // in order to parse them to known data types
                // in this isolated example project it may be less than useful,
                // but this was extracted from a larger project which made heavy use of the topic signature
                // to identify various data types
                switch (parsedMessage.topic)
                {
                    case Topic.None:
                    default:
                        Utilities.KeyMaster.EnqueueAction(() => Debug.LogFormat("#KEYMASTER# invalid topic received for net message: {0}", parsedMessage));
                        break;
                    case Topic.RemoteCommand:
                        ParseCommand(parsedMessage.payload);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogFormat("#KEYMASTER# remote message parser couldn't parse data: {0}", msg);
                Debug.LogFormat("Exception {0}", e);
            }
        }

        static void ParseCommand(string payload)
        {
            RemoteKeymasterCommand cmd = null;
            try
            {
                cmd = JsonUtility.FromJson<RemoteKeymasterCommand>(payload);
                Utilities.KeyMaster.HandleRemoteCommand(cmd);
                //Utilities.KeyMaster.EnqueueAction(() => Debug.LogFormat("#KEYMASTER# parsed command: {0}", cmd));
            }
            catch (System.Exception e)
            {
                Utilities.KeyMaster.EnqueueAction(() => Debug.LogFormat("#KEYMASTER# failed to parse payload {0}", payload));
                Utilities.KeyMaster.EnqueueAction(() => Debug.LogFormat("#KEYMASTER# exception: {0}", e));
            }
        }
    }
}
