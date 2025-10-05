using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using QRCoder;
using System;

public class LobbyHTTPController : MonoBehaviour, IHTTPController
{
    public void Startup(string serverUrl)
    {
        DisplayJoinQR(serverUrl + "/Join.html");
    }
    [SerializeField]
    private GameObject cube;
    [SerializeField]
    private GhostManager gm;
    [SerializeField]
    private RawImage qrimg;

    public void DisplayJoinQR(string url)
    {
        // Hide the image until the texture is ready
        qrimg.enabled = false;

        if (string.IsNullOrEmpty(url)) {
            Debug.LogError("URL for QR Code is empty!");
            return;
        }

        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        
        var qrCodeMatrix = qrCodeData.ModuleMatrix;
        int size = qrCodeMatrix.Count;

        // Create a new Texture2D to draw the QR code on
        Texture2D qrTexture = new Texture2D(size, size);
        qrTexture.filterMode = FilterMode.Point; // Use Point filter for sharp pixels (no smoothing)

        // Define colors for the QR code
        Color32 black = new Color32(0, 0, 0, 255);
        Color32 white = new Color32(255, 255, 255, 255);

        // Loop through the QR code data and set the texture pixels
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                // The matrix gives us 'true' for a black module and 'false' for a white one.
                // We need to flip the 'y' coordinate because Texture2D coordinates start
                // from the bottom-left, while the QR matrix starts from the top-left.
                qrTexture.SetPixel(x, size - 1 - y, qrCodeMatrix[y][x] ? black : white);
            }
        }
        
        // Apply all the pixel changes to the texture
        qrTexture.Apply();

        qrimg.texture = qrTexture;
        qrimg.enabled = true;
    }

    public void SpawnCube()
    {
        Instantiate(cube);
    }

    public ReturnResult IsRoundStarted()
    {
        string res = GameManager.Instance.IsRoundInProgress.ToString();
        
        return new ReturnResult
        {
            code = 200,
            msg = res
        };
    }

    public ReturnResult AddPlayer(string nickname)
    {
        Debug.Log("Add player called. nickname:" + nickname);
        gm.AddGhost(nickname);   
        return new ReturnResult
        {
            code = 200,
            msg = "Success"
        };
    }

    //Mark as Serializable to make Unity's JsonUtility works.
    [System.Serializable]
    public class ReturnResult
    {
        public string msg;
        public int code;
    }

}