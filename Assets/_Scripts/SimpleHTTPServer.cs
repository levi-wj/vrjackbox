// SimpleHTTPServer.cs (Modified)
// I've added a thread-safe queue and changed how methods are invoked.

using UnityEngine;
using System;
using System.Collections.Concurrent; // Required for the thread-safe queue
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

public class SimpleHTTPServer
{
    // A thread-safe queue to hold actions that need to run on the main thread.
    private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
    
    private readonly string[] _indexFiles = { "index.html", "index.htm" };
    private readonly IDictionary<string, string> _mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
    {
        #region extension to MIME type list
        { ".css", "text/css" },
        { ".html", "text/html" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".svg", "image/svg+xml" },
        { ".txt", "text/plain" },
        // Add other MIME types as needed
        #endregion
    };

    private Thread _serverThread;
    private readonly string _rootDirectory;
    private HttpListener _listener;
    private readonly int _port;
    private readonly int _bufferSize;
    private readonly object _methodController;
    
    public Func<object, string> OnJsonSerialized;
    public int Port => _port;

    public SimpleHTTPServer(string path, int port, object controller, int buffer)
    {
        _rootDirectory = path;
        _port = port;
        _methodController = controller;
        _bufferSize = buffer * 1024; // Convert KB to Bytes
        Initialize();
    }

    private void Initialize()
    {
        _serverThread = new Thread(Listen);
        _serverThread.IsBackground = true;
        _serverThread.Start();
    }
    
    public void Stop()
    {
        // Use Abort with caution, but it's often necessary on application quit.
        if (_serverThread != null && _serverThread.IsAlive) _serverThread.Abort();
        if (_listener != null && _listener.IsListening) _listener.Stop();
    }

    // This method is called from the main thread via UnityHTTPServer.Update()
    public void ProcessMainThreadActions()
    {
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    private void Listen()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{_port}/");
        _listener.Start();
        while (true)
        {
            try
            {
                HttpListenerContext context = _listener.GetContext();
                // Queue the request processing to be handled by a new thread from the pool
                ThreadPool.QueueUserWorkItem(o => ProcessRequest(context));
            }
            catch (ThreadAbortException)
            {
                // This is expected when we call Stop()
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Listener loop error: {ex.Message}");
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        string urlPath = context.Request.Url.AbsolutePath;
        string filename = urlPath.Substring(1);

        // Check if the URL is an API call (e.g., /MyMethodName)
        var method = TryParseToController(context.Request.Url);
        if (method != null)
        {
            // This is an API call, handle it and return.
            HandleApiRequest(context, method);
            return;
        }

        // If not an API call, treat it as a file request.
        if (string.IsNullOrEmpty(filename))
        {
            foreach (string indexFile in _indexFiles)
            {
                if (File.Exists(Path.Combine(_rootDirectory, indexFile)))
                {
                    filename = indexFile;
                    break;
                }
            }
        }

        string filePath = Path.Combine(_rootDirectory, filename);
        if (File.Exists(filePath))
        {
            ServeFile(context, filePath);
        }
        else
        {
            SendResponse(context, "<h1>404 - Not Found</h1>", "text/html", HttpStatusCode.NotFound);
        }
    }
    
    private void HandleApiRequest(HttpListenerContext context, MethodInfo method)
    {
        // This part runs on the background thread.
        var namedParameters = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(context.Request.Url.Query))
        {
            var query = context.Request.Url.Query.Replace("?", "").Split('&');
            foreach (var item in query)
            {
                var t = item.Split('=');
                if (t.Length == 2)
                {
                    // URL Decode the parameter value
                    namedParameters.Add(t[0], Uri.UnescapeDataString(t[1]));
                }
            }
        }
        
        // **THE CRITICAL CHANGE**
        // We don't invoke the method here. We queue it to run on the main thread.
        _mainThreadActions.Enqueue(() =>
        {
            // This code will be executed safely on the main thread.
            try
            {
                object result = method.InvokeWithNamedParameters(_methodController, namedParameters);
                
                // Handle the response after the method is invoked.
                string jsonResponse = "{}"; // Default empty JSON
                if (result != null && OnJsonSerialized != null)
                {
                    jsonResponse = OnJsonSerialized.Invoke(result);
                }
                SendResponse(context, jsonResponse, "application/json", HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Debug.LogError($"API Method Invocation Error: {ex.InnerException?.Message ?? ex.Message}");
                SendResponse(context, "{\"error\":\"Internal Server Error\"}", "application/json", HttpStatusCode.InternalServerError);
            }
        });
    }

    private void ServeFile(HttpListenerContext context, string filePath)
    {
        try
        {
            using (Stream input = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                string mimeType = "application/octet-stream";
                _mimeTypeMappings.TryGetValue(Path.GetExtension(filePath), out mimeType);
                
                context.Response.ContentType = mimeType;
                context.Response.ContentLength64 = input.Length;
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                byte[] buffer = new byte[_bufferSize];
                int nbytes;
                while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    context.Response.OutputStream.Write(buffer, 0, nbytes);
                }
            }
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"File serving error: {ex.Message}");
            SendResponse(context, "{\"error\":\"Could not serve file\"}", "application/json", HttpStatusCode.InternalServerError);
        }
    }
    
    private void SendResponse(HttpListenerContext context, string content, string contentType, HttpStatusCode code)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.StatusCode = (int)code;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending response: {ex.Message}");
        }
    }

    private MethodInfo TryParseToController(Uri uri)
    {
        if (uri.Segments.Length < 2) return null;
        
        string methodName = uri.Segments[1].Replace("/", "");
        try
        {
            return _methodController.GetType().GetMethod(methodName);
        }
        catch
        {
            return null;
        }
    }
}

// This extension class allows calling methods using named parameters from a dictionary.
public static class ReflectionExtensions
{
    public static object InvokeWithNamedParameters(this MethodBase self, object obj, IDictionary<string, object> namedParameters)
    {
        return self.Invoke(obj, MapParameters(self, namedParameters));
    }

    private static object[] MapParameters(MethodBase method, IDictionary<string, object> namedParameters)
    {
        var paramInfos = method.GetParameters();
        var parameters = new object[paramInfos.Length];

        for (int i = 0; i < paramInfos.Length; i++)
        {
            var paramInfo = paramInfos[i];
            if (namedParameters.TryGetValue(paramInfo.Name, out object value))
            {
                parameters[i] = Convert.ChangeType(value, paramInfo.ParameterType);
            }
            else
            {
                parameters[i] = paramInfo.HasDefaultValue ? paramInfo.DefaultValue : Type.Missing;
            }
        }
        return parameters;
    }
}