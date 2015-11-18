/*
 *  ARPattern.cs
 *  ARToolKit for Unity
 *
 *  Copyright 2010-2014 ARToolworks, Inc. All rights reserved.
 *
 */

using System;

using UnityEngine;

public sealed class ARPattern
{
#if UNITY_EDITOR
    public readonly Texture2D texture;
    public readonly Matrix4x4 matrix;
    public readonly float width;
	public readonly float height;
#endif

    /// <exception cref="ArgumentException">Invalid argument</exception>
    public ARPattern(int markerID, int patternID, bool multiMarker)
    {
#if UNITY_EDITOR
        float[] matrixRawArray = new float[16];
		float widthRaw;
		float heightRaw;

        int imageSizeX;
        int imageSizeY;
        // Get the pattern local transformation and size.
        if (!PluginFunctions.arwGetMarkerPatternConfig(markerID, patternID, matrixRawArray, out widthRaw, out heightRaw, out imageSizeX, out imageSizeY))
		{
			throw new ArgumentException("Invalid argument", "patternID");
		}
        width = widthRaw*0.001f;
		height = heightRaw*0.001f;

		matrixRawArray[12] *= 0.001f; // Scale the position from ARToolKit units (mm) into Unity units (m).
		matrixRawArray[13] *= 0.001f;
		matrixRawArray[14] *= 0.001f;

		Matrix4x4 matrixRaw = ARUtilityFunctions.MatrixFromFloatArray(matrixRawArray);
		//ARController.Log("arwGetMarkerPatternConfig(" + markerID + ", " + patternID + ", ...) got matrix: [" + Environment.NewLine + matrixRaw.ToString("F3").Trim() + "]");

		// ARToolKit uses right-hand coordinate system where the marker lies in x-y plane with right in direction of +x,
		// up in direction of +y, and forward (towards viewer) in direction of +z.
		// Need to convert to Unity's left-hand coordinate system where marker lies in x-y plane with right in direction of +x,
		// up in direction of +y, and forward (towards viewer) in direction of -z.
		matrix = ARUtilityFunctions.LHMatrixFromRHMatrix(matrixRaw, multiMarker);

		// Handle pattern image.
        if (imageSizeX <= 0 || imageSizeY <= 0)
        {
            return;
        }

        // Allocate a new texture for the pattern image
        texture = new Texture2D(imageSizeX, imageSizeY, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };

        // Get the pattern image data and load it into the texture
        Color[] colors = new Color[imageSizeX * imageSizeY];
        if (!PluginFunctions.arwGetMarkerPatternImage(markerID, patternID, colors))
        {
            return;
        }

        texture.SetPixels(colors);
        texture.Apply();
#endif
    }
}
