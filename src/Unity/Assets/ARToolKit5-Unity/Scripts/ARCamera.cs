/*
 *  ARCamera.cs
 *  ARToolKit for Unity
 *
 *  Copyright 2010-2014 ARToolworks, Inc. All rights reserved.
 *
 */

using System;

using UnityEngine;

/// <summary>
/// A class which links an ARCamera to any available ARMarker via an AROrigin object.
/// 
/// To get a list of foreground Camera objects, do:
///
///     List{Camera} foregroundCameras = new List{Camera}();
///     ARCamera[] arCameras = FindObjectsOfType{ARCamera}(); // (or FindObjectsOfType(typeof(ARCamera)) as ARCamera[])
///     foreach (ARCamera arc in arCameras) {
///         foregroundCameras.Add(arc.gameObject.camera);
///     }
/// </summary>
[RequireComponent(typeof(Transform))]   // A Transform is required to update the position and orientation from tracking
[ExecuteInEditMode]                     // Run in the editor so we can keep the scale at 1
public class ARCamera : MonoBehaviour
{
	private const string LogTag = "ARCamera: ";
    private static readonly Matrix4x4 TsrMatrixRotZNeg180 = Matrix4x4.TRS(
                                    Vector3.zero,
                                    Quaternion.AngleAxis(180.0f, Vector3.back),
                                    Vector3.one);
	
	public enum ViewEye
	{
		Left = 1,
		Right = 2
	}
	
	/*public enum STEREO_DISPLAY_MODE {
		STEREO_DISPLAY_MODE_INACTIVE = 0,           // Stereo display not active.
		STEREO_DISPLAY_MODE_DUAL_OUTPUT,            // Two outputs, one displaying the left view, and one the right view.  Blue-line optional.
		STEREO_DISPLAY_MODE_QUADBUFFERED,           // One output exposing both left and right buffers, with display mode determined by the hardware implementation. Blue-line optional.
		STEREO_DISPLAY_MODE_FRAME_SEQUENTIAL,       // One output, first frame displaying the left view, and the next frame the right view. Blue-line optional.
		STEREO_DISPLAY_MODE_SIDE_BY_SIDE,           // One output. Two normally-proportioned views are drawn in the left and right halves.
		STEREO_DISPLAY_MODE_OVER_UNDER,             // One output. Two normally-proportioned views are drawn in the top and bottom halves.
		STEREO_DISPLAY_MODE_HALF_SIDE_BY_SIDE,      // One output. Two views, scaled to half-width, are drawn in the left and right halves
		STEREO_DISPLAY_MODE_OVER_UNDER_HALF_HEIGHT, // One output. Two views, scaled to half-height, are drawn in the top and bottom halves.
		STEREO_DISPLAY_MODE_ROW_INTERLACED,         // One output. Two views, normally proportioned, are interlaced, with even numbered rows drawn from the first view and odd numbered rows drawn from the second view.
		STEREO_DISPLAY_MODE_COLUMN_INTERLACED,      // One output. Two views, normally proportioned, are interlaced, with even numbered columns drawn from the first view and odd numbered columns drawn from the second view.
		STEREO_DISPLAY_MODE_CHECKERBOARD,           // One output. Two views, normally proportioned, are hatched. On even numbered rows, even numbered columns are drawn from the first view and odd numbered columns drawn from the second view. On odd numbered rows, this is reversed.
		STEREO_DISPLAY_MODE_ANAGLYPH_RED_BLUE,      // One output. Both views are rendered into the same buffer, the left view drawn only in the red channel and the right view only in the blue channel.
		STEREO_DISPLAY_MODE_ANAGLYPH_RED_GREEN,     // One output. Both views are rendered into the same buffer, the left view drawn only in the red channel and the right view only in the green channel.
	}*/
	
	private AROrigin _origin = null;
	protected ARMarker _marker = null;				// Instance of marker that will be used as the origin for the camera pose.
	
	[NonSerialized]
	protected Vector3 arPosition = Vector3.zero;	// Current 3D position from tracking
	[NonSerialized]
	protected Quaternion arRotation = Quaternion.identity; // Current 3D rotation from tracking
	[NonSerialized]
	protected bool arVisible = false;				// Current visibility from tracking
	[NonSerialized]
	protected float timeLastUpdate = 0;				// Time when tracking was last updated.
	[NonSerialized]
	protected float timeTrackingLost = 0;			// Time when tracking was last lost.
	
	public GameObject eventReceiver;
	
    private readonly object updateQueuedLock = new object();
    private bool updateQueued;

	// Stereo settings.
	public bool Stereo = false;
	public ViewEye StereoEye = ViewEye.Left;
	
	// Optical settings.
	public bool Optical = false;
	private bool opticalSetupOK = false;
	public int OpticalParamsFilenameIndex = 0;
	public string OpticalParamsFilename = "";
	public byte[] OpticalParamsFileContents = new byte[0]; // Set by the Editor.
	public float OpticalEyeLateralOffsetRight = 0.0f;
    [SerializeField]
    private bool multiMarker;
	private Matrix4x4 opticalViewMatrix; // This transform expresses the position and orientation of the physical camera in eye coordinates.
    private bool applicationIsPlaying;
    private ARController arController;

    private void OnEnable()
    {
        applicationIsPlaying = Application.isPlaying;
    }

    void Start()
	{
        // Saving off the ARController instance that this registered to for quick access
        // later as well as ensuring that when this un-registers, it does so on the same
        // one that it was registered to.
        if ((arController = ARController.Instance()) != null)
        {
            arController.RegisterUpdateEvent(new ARController.UpdateEventObject(LateUpdateMe, 2));
		}
	}

    void OnDestroy()
    {
        if (arController != null)
        {
            arController.UnregisterUpdateEvent(LateUpdateMe);
        }

        arController = null;
    }

    public bool SetupCamera(float nearClipPlane, float farClipPlane, Matrix4x4 projectionMatrix, ref bool opticalOut)
	{
		Camera c = this.gameObject.GetComponent<Camera>();
		
		// A perspective projection matrix from the tracker
		c.orthographic = false;
		
		// Shouldn't really need to set these, because they are part of the custom 
		// projection matrix, but it seems that in the editor, the preview camera view 
		// isn't using the custom projection matrix.
		c.nearClipPlane = nearClipPlane;
		c.farClipPlane = farClipPlane;
		
		if (Optical) {
			float fovy ;
			float aspect;
			float[] m = new float[16];
			float[] p = new float[16];
			opticalSetupOK = PluginFunctions.arwLoadOpticalParams(null, OpticalParamsFileContents, OpticalParamsFileContents.Length, out fovy, out aspect, m, p);
			if (!opticalSetupOK) {
				ARController.Log(string.Format("{0}Error loading optical parameters.", LogTag));
				return false;
			}
			m[12] *= 0.001f;
			m[13] *= 0.001f;
			m[14] *= 0.001f;
			ARController.Log(string.Format("{0}Optical parameters: fovy={1}, aspect={2}, camera position (m)={{{3}, {4}, {5}}}", LogTag, fovy, aspect, m[12].ToString("F3"), m[13].ToString("F3"), m[14].ToString("F3")));
			
			c.projectionMatrix = ARUtilityFunctions.MatrixFromFloatArray(p);
			
			opticalViewMatrix = ARUtilityFunctions.MatrixFromFloatArray(m);
			if (Math.Abs(OpticalEyeLateralOffsetRight) > float.Epsilon) opticalViewMatrix = Matrix4x4.TRS(new Vector3(-OpticalEyeLateralOffsetRight, 0.0f, 0.0f), Quaternion.identity, Vector3.one) * opticalViewMatrix; 
			// Convert to left-hand matrix.
			opticalViewMatrix = ARUtilityFunctions.LHMatrixFromRHMatrix(opticalViewMatrix, multiMarker);
			
			opticalOut = true;
		} else {
			c.projectionMatrix = projectionMatrix;
		}
		
		// Don't clear anything or else we interfere with other foreground cameras
		c.clearFlags = CameraClearFlags.Nothing;
		
		// Renders after the clear and background cameras
		c.depth = 2;
		
		c.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
		c.transform.rotation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
		c.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
		
		return true;
	}
	
	// Return the origin associated with this component.
	// Uses cached value if available, otherwise performs a find operation.
	public virtual AROrigin GetOrigin()
	{
		if (_origin == null) {
			// Locate the origin in parent.
			_origin = this.gameObject.GetComponentInParent<AROrigin>();
		}
		return _origin;
	}
	
	// Get the marker, if any, currently acting as the base.
	public virtual ARMarker GetMarker()
	{
		AROrigin origin = GetOrigin();
		if (origin == null) return null;
		return (origin.GetBaseMarker());
	}
	
	// Updates arVisible, arPosition, arRotation based on linked marker state.
	private void UpdateTracking()
	{
		// Note the current time
		timeLastUpdate = Time.realtimeSinceStartup;
			
		// First, ensure we have a base marker. If none, then no markers are currently in view.
		ARMarker marker = GetMarker();
		if (marker == null) {
			if (arVisible) {
				// Marker was visible but now is hidden.
				timeTrackingLost = timeLastUpdate;
				arVisible = false;
			}
		} else {
			
			if (marker.Visible) {
                if (marker.TransformationMatrix.HasValue) {
				Matrix4x4 pose;
				if (Optical && opticalSetupOK) {
                        pose =
                            (opticalViewMatrix * marker.TransformationMatrix.Value)
                                .inverse;
				} else {
					ScreenOrientation orientation = SetCameraParams.Instance ().Orientation ();
					if ( orientation == ScreenOrientation.PortraitUpsideDown || orientation == ScreenOrientation.LandscapeRight) 
					{
                            pose = (TsrMatrixRotZNeg180 * marker.TransformationMatrix.Value).inverse;
					}
					else 
					{
                            pose = marker.TransformationMatrix.Value.inverse;
					}
				}

				arPosition = ARUtilityFunctions.PositionFromMatrix(pose);
				// Camera orientation: In ARToolKit, zero rotation of the camera corresponds to looking vertically down on a marker
				// lying flat on the ground. In Unity however, if we still treat markers as being flat on the ground, we clash with Unity's
				// camera "rotation", because an unrotated Unity camera is looking horizontally.
				// So we choose to treat an unrotated marker as standing vertically, and apply a transform to the scene to
				// to get it to lie flat on the ground.
				arRotation = ARUtilityFunctions.QuaternionFromMatrix(pose);
				
				if (!arVisible) {
					// Marker was hidden but now is visible.
					arVisible = true;
				}
                }
			} else {
				if (arVisible) {
					// Marker was visible but now is hidden.
					timeTrackingLost = timeLastUpdate;
					arVisible = false;
				}
			}
		}
	}
	
	protected virtual void ApplyTracking()
	{
		if (arVisible) {
			transform.localPosition = arPosition; // TODO: Change to transform.position = PositionFromMatrix(origin.transform.localToWorldMatrix * pose) etc;
			transform.localRotation = arRotation;
		}
	}
	
	// Use LateUpdate to be sure the ARMarker has updated before we try and use the transformation.
    private void LateUpdateMe()
    {
        lock (updateQueuedLock)
	{
            if (updateQueued)
            {
                // Make sure only one happens per frame.
                return;
            }
        }

        if (arController == null)
        {
            return;
        }

        arController.QueueOnMainThread(DoUpdate);
    }

    private void DoUpdate()
    {
        lock (updateQueuedLock)
        {
            updateQueued = false;
        }

        // Update tracking if we are running in Player.
        if (!applicationIsPlaying)
        {
            return;
        }

		// Local scale is always 1 for now
		transform.localScale = Vector3.one;
		
		UpdateTracking();
		ApplyTracking();
	}
}

