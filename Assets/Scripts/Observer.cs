using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Observer : MonoBehaviour {
    [SerializeField]
    private CustomRenderTexture differenceRenderTexture;

    private void Start() {
        differenceRenderTexture.Initialize();
    }

    public void SaveDifferenceImage(string objectName, string stateName) {
        Debug.Log("Not working properly. Figure it out next update"); //TODO
        // // Swap render textures for this one operation.
        // RenderTexture previousRenderTexture = RenderTexture.active;
        // RenderTexture.active = differenceRenderTexture;
        // // Update the actual texture
        // differenceRenderTexture.Update();
        // // Save it to a 2D texture
        // Texture2D pixels = new(differenceRenderTexture.width, differenceRenderTexture.height);
        // Rect readRegion = new(0, 0, differenceRenderTexture.width, differenceRenderTexture.height);
        // pixels.ReadPixels(readRegion, 0, 0, false);

        // // Save the 2D texture to file
        // byte[] bytes = ImageConversion.EncodeToPNG(pixels);
        // print(bytes.Length);
        // string fileName = "./" + objectName + "-" + stateName + ".png";
        // print(fileName);
        // System.IO.File.WriteAllBytes(fileName, bytes);
        // print("lol");

        // // Swap back to the old render texture
        // RenderTexture.active = previousRenderTexture;
    }

    private void OnDestroy() {
        differenceRenderTexture.Release();
    }
}
