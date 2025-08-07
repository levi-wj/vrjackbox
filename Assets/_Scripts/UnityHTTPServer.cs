using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;

public class UnityHTTPServer : MonoBehaviour
{
    [SerializeField]
    public int port = 1235; // Default port
    [SerializeField]
    public string SaveFolder;
    [SerializeField]
    public bool UseStreamingAssetsPath = true;
    [SerializeField]
    public int bufferSize = 16;
    
    public static UnityHTTPServer Instance;

    public MonoBehaviour controller;
    
    private SimpleHTTPServer myServer;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Init()
    {
        // Ensure you have a controller assigned in the inspector.
        if (controller == null)
        {
            Debug.LogError("HTTP Server Controller not assigned in the Inspector!");
            controller = this; // Fallback to self, but show an error.
        }
        
        StartServer();
        Debug.Log($"Started server at: {GetHttpUrl()}");
    }

    // This Update method runs on the main thread and safely executes queued actions
    void Update()
    {
        myServer?.ProcessMainThreadActions();
    }

    public void StartServer()
    {
        myServer = new SimpleHTTPServer(GetSaveFolderPath, port, controller, bufferSize);
        
        // This delegate is used to serialize the return value of your methods into JSON.
        myServer.OnJsonSerialized += (result) => {
            // Using Unity's built-in JsonUtility.
            return JsonUtility.ToJson(result);
        };
    }

    private string GetSaveFolderPath
    {
        get
        {
            if (UseStreamingAssetsPath)
            {
                return Application.streamingAssetsPath;
            }
            return SaveFolder;
        }
    }

    public static string GetHttpUrl()
    {
        if (Instance == null || Instance.myServer == null) return "Server not started";
        return $"http://{GetLocalIPAddress()}:{Instance.myServer.Port}/";
    }

    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1"; // Fallback
    }

    void OnApplicationQuit()
    {
        myServer?.Stop();
    }
}