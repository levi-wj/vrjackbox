// UnityHTTPServer.cs (Modified)
// This version now extracts web files to a readable location on the device before starting the server.

using UnityEngine;
using System;
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // Required for Lists
using System.IO; // Required for Path
using System.Net;
using System.Net.Sockets;
using UnityEngine.Networking; // Required for UnityWebRequest

public class UnityHTTPServer : MonoBehaviour
{
    [SerializeField]
    public int port = 8080;
    [SerializeField]
    public bool UseStreamingAssetsPath = true; // This will now control the SOURCE, not the final path.
    [SerializeField]
    public int bufferSize = 16;
    
    public static UnityHTTPServer Instance;

    public MonoBehaviour controller;
    
    private SimpleHTTPServer myServer;
    private string serverRootPath; // The path the server will actually use.

    // A list of all the files your web controller needs.
    // You MUST list every file you want to be accessible here.
    private List<string> filesToExtract = new List<string>
    {
        "Game.html"
    };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // The server will now serve files from the persistent data path.
            serverRootPath = Application.persistentDataPath;

            // Start the extraction and server setup process.
            StartCoroutine(InitializeServer());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // The server setup is now a coroutine to handle the async file extraction.
    private IEnumerator InitializeServer()
    {
        Debug.Log("Starting server initialization and file extraction...");
        yield return ExtractWebFiles();

        // Now that files are extracted, we can start the server.
        if (controller == null)
        {
            Debug.LogError("HTTP Server Controller not assigned in the Inspector!");
            controller = this;
        }
        
        StartServer();
        string serverUrl = GetHttpUrl();
        Debug.Log($"Server started. Files are served from: {serverRootPath}");
        Debug.Log($"Connect at: {serverUrl}");

        // Display the IP address in your game's UI
        TestController testController = controller as TestController;
        if (testController != null)
        {
            testController.DisplayJoinQR(serverUrl);
        }
    }

    private IEnumerator ExtractWebFiles()
    {
        foreach (string fileName in filesToExtract)
        {
            string sourcePath = Path.Combine(Application.streamingAssetsPath, fileName);
            string destinationPath = Path.Combine(serverRootPath, fileName);

            // On Android, UnityWebRequest is needed to access StreamingAssets.
            // For other platforms, we can use it too for consistency.
            using (UnityWebRequest www = UnityWebRequest.Get(sourcePath))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load {fileName} from StreamingAssets: {www.error}");
                }
                else
                {
                    Debug.Log($"Extracting '{fileName}' to '{destinationPath}'");
                    try
                    {
                        File.WriteAllBytes(destinationPath, www.downloadHandler.data);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to write {fileName} to persistent data path: {e.Message}");
                    }
                }
            }
        }
    }

    void Update()
    {
        myServer?.ProcessMainThreadActions();
    }

    public void StartServer()
    {
        // Pass the NEW server root path to the server.
        myServer = new SimpleHTTPServer(serverRootPath, port, controller, bufferSize);
        
        myServer.OnJsonSerialized += (result) => {
            return JsonUtility.ToJson(result);
        };
    }

    public static string GetHttpUrl()
    {
        if (Instance == null || Instance.myServer == null) return "Server not started";
        // The URL now needs to point to the specific HTML file.
        return $"http://{GetLocalIPAddress()}:{Instance.myServer.Port}/Game.html";
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
        return "127.0.0.1";
    }

    void OnApplicationQuit()
    {
        myServer?.Stop();
    }
}