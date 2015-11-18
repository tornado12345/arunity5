/*
 *  ARMarker.cs
 *  ARToolKit for Unity
 *
 *  Copyright 2010-2014 ARToolworks, Inc. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;
using System.Threading;

public enum MarkerType
{
    Square,      		// A standard ARToolKit template (pattern) marker
    SquareBarcode,      // A standard ARToolKit matrix (barcode) marker.
    Multimarker,        // Multiple markers treated as a single target
	NFT
}

public enum ARWMarkerOption : int {
        ARW_MARKER_OPTION_FILTERED = 1,
        ARW_MARKER_OPTION_FILTER_SAMPLE_RATE = 2,
        ARW_MARKER_OPTION_FILTER_CUTOFF_FREQ = 3,
        ARW_MARKER_OPTION_SQUARE_USE_CONT_POSE_ESTIMATION = 4,
		ARW_MARKER_OPTION_SQUARE_CONFIDENCE = 5,
		ARW_MARKER_OPTION_SQUARE_CONFIDENCE_CUTOFF = 6,
		ARW_MARKER_OPTION_NFT_SCALE = 7
}

/// <summary>
/// ARMarker objects represent an ARToolKit marker, even when ARToolKit is not initialized.
/// <para>
/// To find markers from elsewhere in the Unity environment:
/// <c>
/// ARMarker[] markers = FindObjectsOfType{ARMarker}() (or FindObjectsOfType(typeof(ARMarker)) as ARMarker[]);
/// </c>
/// </para>
/// </summary>
[ExecuteInEditMode]
public class ARMarker : MonoBehaviour
{
    public static readonly Dictionary<MarkerType, string> MarkerTypeNames = new Dictionary<MarkerType, string>
    {
		{MarkerType.Square, "Single AR pattern"},
		{MarkerType.SquareBarcode, "Single AR barcode"},
    	{MarkerType.Multimarker, "Multimarker AR configuration"},
		{MarkerType.NFT, "NFT dataset"}
    };

    private const string LogTag = "ARMarker: ";
    
    // Quaternion to rotate from ART to Unity
    //public static Quaternion RotationCorrection = Quaternion.AngleAxis(90.0f, new Vector3(1.0f, 0.0f, 0.0f));

    // Value used when no underlying ARToolKit marker is assigned
    public const int NO_ID = -1;

    private static readonly Matrix4x4 MatrixTsr90Inverse = Matrix4x4.TRS(
        Vector3.zero,
        Quaternion.AngleAxis(90f, Vector3.forward),
        Vector3.one).inverse;
    private static readonly Matrix4x4 MatrixTsr270Inverse = Matrix4x4.TRS(
        Vector3.zero,
        Quaternion.AngleAxis(90f, Vector3.back),
        Vector3.one).inverse;
    private readonly object visibleLock = new object();
    private readonly object transformMatrixLock = new object();

    [NonSerialized]       // UID is not serialized because its value is only meaningful during a specific run.
    public int UID = NO_ID;      // Current Unique Identifier (UID) assigned to this marker.

    // Public members get serialized
    public MarkerType MarkerType = MarkerType.Square;
    public string Tag = string.Empty;

    // If the marker is single, then it has a filename and a width
	public int PatternFilenameIndex = 0;
    public string PatternFilename = string.Empty;
    public string PatternContents = string.Empty; // Set by the editor.
    public float PatternWidth = 0.08f;
	
	// Barcode markers have a user-selected ID.
	public int BarcodeID = 0;
	
    // If the marker is multi, it just has a config filename
    public string MultiConfigFile = string.Empty;
	
	// NFT markers have a dataset pathname (less the extension).
	// Also, we need a list of the file extensions that make up an NFT dataset.
	public string NFTDataName = string.Empty;
	#if !UNITY_METRO
	private readonly string[] NFTDataExts = {"iset", "fset", "fset2"};
	#endif
	[NonSerialized]
	public float NFTWidth; // Once marker is loaded, this holds the width of the marker in Unity units.
	[NonSerialized]
	public float NFTHeight; // Once marker is loaded, this holds the height of the marker in Unity units.

    // Private fields with accessors.
	// Marker configuration options.
	[SerializeField]
	private bool currentUseContPoseEstimation = false;						// Single marker only; whether continuous pose estimation should be used.
	[SerializeField]
	private bool currentFiltered = false;
	[SerializeField]
	private float currentFilterSampleRate = 30.0f;
	[SerializeField]
	private float currentFilterCutoffFreq = 15.0f;
	[SerializeField]
	private float currentNFTScale = 1.0f;									// NFT marker only; scale factor applied to marker size.

    // Realtime tracking information
    private bool visible = false;                                           // Marker is visible or not
    /// <summary>Full transformation matrix as a Unity matrix.<c>Null if not been set.</c></summary>
    private Matrix4x4? transformationMatrix;
    //    private Quaternion rotation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);   // Rotation corrected for Unity
    //    private Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);               // Position corrected for Unity

    public string markerDirectory = "EasyAnimatorDataSet";

    private ARController arController;
    private SetCameraParams setCameraParams;
    private IEnumerator loadEnumerator = null;
    private bool applicationIsPlaying;
    private bool isAndroid;
    private bool isEditor;

    // Initialization.
    // When Awake() is called, the object is already instantiated and de-serialized.
    // This is the right place to connect to other objects.
    // However, objects are awoken in random order, so do not assume that any
    // other object is ready to be communicated with -- it might need to be awoken first.
    void Awake()
    {
        applicationIsPlaying = Application.isPlaying;
        isAndroid = Application.platform == RuntimePlatform.Android;
        isEditor = Application.isEditor;
        UID = NO_ID;
    }
	
	public void OnEnable()
	{
        applicationIsPlaying = Application.isPlaying;
    }
	
	public void OnDisable()
	{
		Unload();
	}

	#if !UNITY_METRO
	private bool unpackStreamingAssetToCacheDir(string basename)
	{
		if (!File.Exists(System.IO.Path.Combine(Application.temporaryCachePath, basename))) {
			string file = System.IO.Path.Combine(Application.streamingAssetsPath + "/" + markerDirectory, basename); // E.g. "jar:file://" + Application.dataPath + "!/assets/" + basename;
			WWW unpackerWWW = new WWW(file);
			while (!unpackerWWW.isDone) { } // This will block in the webplayer. TODO: switch to co-routine.
			if (!string.IsNullOrEmpty(unpackerWWW.error)) {
				ARController.Log(LogTag + "Error unpacking '" + file + "'");
				return (false);
			}
			File.WriteAllBytes(System.IO.Path.Combine(Application.temporaryCachePath, basename), unpackerWWW.bytes); // 64MB limit on File.WriteAllBytes.
		}
		return (true);
	}
	#endif

	public void Load()
	{

	}

    public void LoadMe(bool useThread, Action<ARMarker> finishedLoadingAction)
    {
        if ((arController = ARController.Instance()) == null)
        {
            Debug.LogError("Failed to find the ARController when loading.");
            return;
        }

        if ((setCameraParams = SetCameraParams.Instance()) == null)
        {
            Debug.LogError("Failed to find the SetCameraParams when loading.");
            return;
        }

        if (loadEnumerator == null && gameObject.activeSelf)
        {
            StartLoad(useThread, finishedLoadingAction);
        }

        arController.RegisterUpdateEvent(new ARController.UpdateEventObject(UpdateMe, 1));
    }

    private void StartLoad(bool useThread, Action<ARMarker> finishedLoadingAction)
	{
		if (loadEnumerator != null)
		{
			StopCoroutine(loadEnumerator);
			loadEnumerator = null;
		}

		loadEnumerator = _Load(useThread, finishedLoadingAction);
		StartCoroutine(loadEnumerator);
	}

    private sealed class ARMarkerLoadingArgs
    {
        public readonly string config;
        public readonly Action finishedCallback;

        public ARMarkerLoadingArgs(string config, Action finishedCallback)
        {
            this.config = config;
            this.finishedCallback = finishedCallback;
        }
    }

    private void ArwAddMarker(string config, bool useThread, Action doneAddingAction) {
		ARMarkerLoadingArgs args = new ARMarkerLoadingArgs(config, doneAddingAction);

		if (useThread)
		{
			ThreadPool.QueueUserWorkItem(RunGetArwAddMarker,args);
		}
		else
		{
			RunGetArwAddMarker (args);
		}
	}
	
	private void RunGetArwAddMarker(object stateInfo)
	{
		ARMarkerLoadingArgs args = stateInfo as ARMarkerLoadingArgs;
		try {
            if (args == null) {
                throw new NullReferenceException("args");
            }

			int uid = PluginFunctions.arwAddMarker(args.config);
			UID = uid;
		} catch (Exception exception) {
			Debug.LogException(exception);
		}
		
        if (args != null && args.finishedCallback != null) {
            args.finishedCallback();
        }
	}

    private bool ShouldBeLoading()
    {
        if (!applicationIsPlaying)
        {
            return false;
        }

        if (arController == null)
        {
            Debug.LogWarning("Lost the ARController when loading.");
            return false;
        }

        if (setCameraParams == null)
        {
            Debug.LogWarning("Lost the SetCameraParams when loading.");
            return false;
        }

        if (UID == NO_ID)
        {
            return PluginFunctions.inited;
        }

        Debug.LogWarning("Marker already loaded.");
        return false;
    }

    // Load the underlying ARToolKit marker structure(s) and set the UID.
    IEnumerator _Load(bool useThread, Action<ARMarker> finishedLoadingAction) 
    {
		Action done = () => {
			loadEnumerator = null;
			if(finishedLoadingAction != null){
				finishedLoadingAction(this);
			}
		};

        if (!ShouldBeLoading())
        {
            done();
            yield break;
        }

        // Work out the configuration string to pass to ARToolKit.
        string dir = string.Format("{0}/{1}", Application.streamingAssetsPath, markerDirectory);
        string cfg = string.Empty;

		switch (MarkerType) {

			case MarkerType.Square:
				// Multiply width by 1000 to convert from metres to ARToolKit's millimeters.
				cfg = string.Format("single_buffer;{0};buffer={1}", PatternWidth*1000.0f, PatternContents);
				break;
			
			case MarkerType.SquareBarcode:
				// Multiply width by 1000 to convert from metres to ARToolKit's millimeters.
				cfg = string.Format("single_barcode;{0};{1}", BarcodeID, PatternWidth*1000.0f);
				break;
			
            case MarkerType.Multimarker:
				#if !UNITY_METRO
				if (dir.Contains("://")) 
			{
					// On Android, we need to unpack the StreamingAssets from the .jar file in which
					// they're archived into the native file system.
					dir = Application.temporaryCachePath;
					if (!unpackStreamingAssetToCacheDir(MultiConfigFile))
					{
						dir = string.Empty;
					} 
					else
					{
						//also unpack the .pat files
						string mrkFileName =  System.IO.Path.Combine(Application.temporaryCachePath,MultiConfigFile);
						string [] lines = File.ReadAllLines(mrkFileName);
						foreach( string line in lines )
						{
							if(line.Contains(".pat"))
							{
								if (!unpackStreamingAssetToCacheDir(line)) {
								    dir = string.Empty;
								    break;
								}
							}
						}
						
				    }
				}
				#endif
				
				if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(MultiConfigFile)) {
					cfg = string.Format("multi;{0}", System.IO.Path.Combine(dir, MultiConfigFile));
				}
                break;

			
			case MarkerType.NFT:
				#if !UNITY_METRO
				if (dir.Contains("://")) 
				{
					// On Android, we need to unpack the StreamingAssets from the .jar file in which
					// they're archived into the native file system.
					dir = Application.temporaryCachePath;
					foreach (string ext in NFTDataExts) {
						string basename = string.Format("{0}.{1}", NFTDataName, ext);
						if (!unpackStreamingAssetToCacheDir(basename)) {
							dir = string.Empty;
							break;
						}
					}
				}
				#endif
			
				if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(NFTDataName)) {
					cfg = string.Format("nft;{0}", System.IO.Path.Combine(dir, NFTDataName));
				}
				break;

            default:
                Debug.LogWarningFormat("Unknown marker type \"{0}\"", MarkerType);
                break;

        }

		// If a valid config. could be assembled, get ARToolKit to process it, and assign the resulting ARMarker UID.
		if (string.IsNullOrEmpty(cfg)) {
			done();
			yield break;
		}

		bool addingMarker = true;
		ArwAddMarker(cfg, useThread, () => addingMarker = false);
		
		while (addingMarker) {
			yield return null;//new WaitForEndOfFrame();
		}

		if (UID == NO_ID) {
			ARController.Log(string.Format("{0}Error loading marker.", LogTag));
			done();
			yield break;
		}

		// Marker loaded. Do any additional configuration.
		//ARController.Log("Added marker with cfg='" + cfg + "'");
				
		if (MarkerType == MarkerType.Square || MarkerType == MarkerType.SquareBarcode) UseContPoseEstimation = currentUseContPoseEstimation;
		Filtered = currentFiltered;
		FilterSampleRate = currentFilterSampleRate;
		FilterCutoffFreq = currentFilterCutoffFreq;
		if (MarkerType == MarkerType.NFT) NFTScale = currentNFTScale;

		// Retrieve any required information from the configured ARToolKit ARMarker.
		if (MarkerType == MarkerType.NFT) {

		int imageSizeX, imageSizeY;
		PluginFunctions.arwGetMarkerPatternConfig(UID, 0, null, out NFTWidth, out NFTHeight, out imageSizeX, out imageSizeY);
		NFTWidth *= 0.001f;
		NFTHeight *= 0.001f;
		//ARController.Log("Got NFTWidth=" + NFTWidth + ", NFTHeight=" + NFTHeight + ".");
				
		} else {

       			// Create array of patterns. A single marker will have array length 1.
				int numPatterns = PluginFunctions.arwGetMarkerPatternCount(UID);
       			//ARController.Log("Marker with UID=" + UID + " has " + numPatterns + " patterns.");
        		if (numPatterns > 0) {
					Patterns = new ARPattern[numPatterns];
			       	for (int i = 0; i < numPatterns; i++) {
            			Patterns[i] = new ARPattern(UID, i, MarkerType == MarkerType.Multimarker);
        			}
				}

		}
		done ();
    }

	// We use Update() here, but be aware that unless ARController has been configured to
	// execute first (Unity Editor->Edit->Project Settings->Script Execution Order) then
	// state produced by this update may lag by one frame.
    void UpdateMe()
    {
        if (UID == NO_ID || !PluginFunctions.inited || arController == null || setCameraParams == null)
        {
            Visible = false;
            return;
        }

        if (!applicationIsPlaying)
        {
            return;
        }

		// Query visibility if we are running in the Player.
        float[] matrixRawArray = new float[16];
        Visible = PluginFunctions.arwQueryMarkerTransformation(UID, matrixRawArray);

		if (Visible) {
            if (MarkerType != MarkerType.Multimarker)
            {
                // Scale the position from ARToolKit units (mm) into Unity units (m).
                matrixRawArray[12] *= 0.001f;
                matrixRawArray[13] *= 0.001f;
                matrixRawArray[14] *= 0.001f;
            }

            Matrix4x4 matrixRaw = ARUtilityFunctions.MatrixFromFloatArray(matrixRawArray);

            // ARToolKit uses right-hand coordinate system where the marker lies in x-y plane with right in direction of +x,
            // up in direction of +y, and forward (towards viewer) in direction of +z.
            // Need to convert to Unity's left-hand coordinate system where marker lies in x-y plane with right in direction of +x,
            // up in direction of +y, and forward (towards viewer) in direction of -z.
            Matrix4x4 worldToModelMatrix =
                ARUtilityFunctions.LHMatrixFromRHMatrix(
                    matrixRaw,
                    MarkerType == MarkerType.Multimarker);

            if (MarkerType == MarkerType.Multimarker && arController.ContentRotate90)
            {
                if (!setCameraParams.isFront || (isAndroid && !isEditor))
                {
                    worldToModelMatrix = MatrixTsr90Inverse * worldToModelMatrix;
                }
                else
                {
                    worldToModelMatrix = MatrixTsr270Inverse * worldToModelMatrix;
                }
            }

            TransformationMatrix = worldToModelMatrix;
        }
    }
	
	// Unload any underlying ARToolKit structures, and clear the UID.
	public void Unload()
	{
		if (loadEnumerator != null)
		{
			StopCoroutine(loadEnumerator);
			loadEnumerator = null;
		}

		Visible = false;

		if (UID == NO_ID) {
			return;
		}
		
		if (PluginFunctions.inited) {
			// Remove any currently loaded ARToolKit marker.
        	PluginFunctions.arwRemoveMarker(UID);
		}

		UID = NO_ID;

		Patterns = null; // Delete the patterns too.

		if (arController != null) {
            arController.UnregisterUpdateEvent(UpdateMe);
            arController = null;
        }

        setCameraParams = null;
    }

    /// <summary>
    /// Will contain the world-to-model matrix.
    /// <c>
    /// Null if not been set.
    /// </c>
    /// </summary>
    public Matrix4x4? TransformationMatrix
    {
        get
        {
            lock (transformMatrixLock)
            {
                return transformationMatrix;
            }
        }

        private set
        {
            lock (transformMatrixLock)
            {
                transformationMatrix = value;
            }
        }
    }

    /// <summary>
    /// Is the marker is visible or not.
    /// </summary>
    public bool Visible
    {
        get
        {
            lock (visibleLock)
            {
                return visible;
            }
        }

        private set
        {
            lock (visibleLock)
            {
                visible = value;
            }
        }
    }

    // Single markers have a single pattern, multi markers have one or more, NFT have none.
    public ARPattern[] Patterns { get; private set; }

    public bool Filtered
    {
        get
        {
            return currentFiltered; // Serialized.
        }

        set
        {
			currentFiltered = value;
			if (UID != NO_ID) {
				PluginFunctions.arwSetMarkerOptionBool(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_FILTERED, value);
			}
        }
    }

    public float FilterSampleRate
    {
        get
        {
            return currentFilterSampleRate; // Serialized.
        }

        set
        {
			currentFilterSampleRate = value;
			if (UID != NO_ID) {
				PluginFunctions.arwSetMarkerOptionFloat(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_FILTER_SAMPLE_RATE, value);
			}
        }
    }

	public float FilterCutoffFreq
    {
        get
        {
            return currentFilterCutoffFreq; // Serialized.
        }

        set
        {
			currentFilterCutoffFreq = value;
			if (UID != NO_ID) {
				PluginFunctions.arwSetMarkerOptionFloat(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_FILTER_CUTOFF_FREQ, value);
			}
        }
    }

	public bool UseContPoseEstimation
    {
        get
        {
            return currentUseContPoseEstimation; // Serialized.
        }

        set
        {
			currentUseContPoseEstimation = value;
			if (UID != NO_ID && (MarkerType == MarkerType.Square || MarkerType == MarkerType.SquareBarcode)) {
				PluginFunctions.arwSetMarkerOptionBool(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_SQUARE_USE_CONT_POSE_ESTIMATION, value);
			}
        }
    }

	public float NFTScale
	{
		get
		{
            return currentNFTScale; // Serialized.
		}
		
		set
		{
			currentNFTScale = value;
			if (UID != NO_ID && (MarkerType == MarkerType.NFT)) {
				PluginFunctions.arwSetMarkerOptionFloat(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_NFT_SCALE, value);
			}
		}
	}
}
