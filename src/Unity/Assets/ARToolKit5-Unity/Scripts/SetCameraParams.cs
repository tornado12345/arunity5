using System.Collections;

using UnityEngine;

public class SetCameraParams : MonoBehaviour
{
	public ARController arControl;
	private const string macCamName = "FaceTime HD Camera";
	private const string macDualBootCamName = "FaceTime HD Camera (Built-in)";
	public bool isFront = true;
	public bool isHighRes = true;

	private ScreenOrientation currentOrientation = ScreenOrientation.Unknown;

	private static SetCameraParams sInstance = null;

	public static SetCameraParams Instance()
	{
		return sInstance;
	}

	public ScreenOrientation Orientation()
	{
		return currentOrientation;
	}

	void Awake()
	{
		sInstance = this;
	}

	void OnEnable()
	{
		if ((arControl = GetComponent<ARController>()) == null)
		{
			Debug.LogError("ERROR! Can't find the ARController script");
			this.enabled = false;
		}
	}

	void Update () 
	{
		//there is a bug on android. on android ScreenOrientation never changes. Thats why we using DeviceOrientation to figure out ScreenOrientation
		DeviceOrientation orientation = Input.deviceOrientation;

		bool orientationChanged = false;

		if (orientation == DeviceOrientation.Portrait && currentOrientation != ScreenOrientation.Portrait && Screen.autorotateToPortrait)
		{
			orientationChanged = true;
			currentOrientation = ScreenOrientation.Portrait;
		} 
		else if (orientation == DeviceOrientation.PortraitUpsideDown && currentOrientation != ScreenOrientation.PortraitUpsideDown && Screen.autorotateToPortraitUpsideDown)
		{
			orientationChanged = true;
			currentOrientation = ScreenOrientation.PortraitUpsideDown;
		} 
		else if (orientation == DeviceOrientation.LandscapeLeft && currentOrientation != ScreenOrientation.LandscapeLeft && Screen.autorotateToLandscapeLeft)
		{
			orientationChanged = true;
			currentOrientation = ScreenOrientation.LandscapeLeft;
		} 
		else if (orientation == DeviceOrientation.LandscapeRight && currentOrientation != ScreenOrientation.LandscapeRight && Screen.autorotateToLandscapeRight)
		{
			orientationChanged = true;
			currentOrientation = ScreenOrientation.LandscapeRight;
		} 

		if (currentOrientation == ScreenOrientation.Unknown) {
			currentOrientation = Screen.orientation;
		}

        if(orientationChanged && arControl.ARRunning)
		{
			if(arControl._eventReceiver != null) arControl._eventReceiver.BroadcastMessage("OnOrientationChanged", this, SendMessageOptions.DontRequireReceiver);

		    if(arControl.ARRunning){
				StartCoroutine(SetCam());
			}
		}
	}

	public void SetCamera()
	{
		SetCamera (isFront, isHighRes);
	}

	public void SetCamera(bool isFront, bool isHighRes)
	{
		this.isFront = isFront;
		this.isHighRes = isHighRes;
		StartCoroutine(SetCam());
	}

	public void SwapCamera()
	{
		isFront = !isFront;
		SetCamera (isFront,isHighRes);

		if(arControl._eventReceiver != null) arControl._eventReceiver.BroadcastMessage("OnCameraSwaped", this, SendMessageOptions.DontRequireReceiver);
	}

    public void StopCamera()
    {
		arControl.StopAR();

		if(arControl._eventReceiver != null) arControl._eventReceiver.BroadcastMessage("OnCameraStopped", null, SendMessageOptions.DontRequireReceiver);
	}

	public bool IsLowEndDevice()
	{
#if UNITY_IOS && !UNITY_EDITOR
		switch (UnityEngine.iOS.Device.generation) 
		{
		case UnityEngine.iOS.DeviceGeneration.iPad1Gen:
		case UnityEngine.iOS.DeviceGeneration.iPad2Gen:
		case UnityEngine.iOS.DeviceGeneration.iPad3Gen:
		case UnityEngine.iOS.DeviceGeneration.iPadMini1Gen:
		case UnityEngine.iOS.DeviceGeneration.iPodTouch1Gen:
		case UnityEngine.iOS.DeviceGeneration.iPodTouch2Gen:
		case UnityEngine.iOS.DeviceGeneration.iPodTouch3Gen:
		case UnityEngine.iOS.DeviceGeneration.iPodTouch4Gen:
		case UnityEngine.iOS.DeviceGeneration.iPodTouch5Gen:
		case UnityEngine.iOS.DeviceGeneration.iPhone:
		case UnityEngine.iOS.DeviceGeneration.iPhone3G:
		case UnityEngine.iOS.DeviceGeneration.iPhone3GS:
		case UnityEngine.iOS.DeviceGeneration.iPhone4:
		case UnityEngine.iOS.DeviceGeneration.iPhone4S:
			return true;
		}
#endif

		return false;
	}
	
	private IEnumerator SetCam()
	{
		StopCamera ();

		while (currentOrientation == ScreenOrientation.Unknown)
        {
            yield return new WaitForSeconds(0.5f);
        }

        arControl.SetContentForScreenOrientation(isFront);

        // not supported yet in artoolkit for unity5
        arControl.UseNativeGLTexturingIfAvailable = false;

        arControl.videoCParamName0 = "camera_para";


#if UNITY_IOS && !UNITY_EDITOR
		string preset = isHighRes ? "720p" : "480p";

		if(UnityEngine.iOS.Device.generation == UnityEngine.iOS.DeviceGeneration.iPad3Gen || UnityEngine.iOS.Device.generation == UnityEngine.iOS.DeviceGeneration.iPad2Gen
           || UnityEngine.iOS.Device.generation == UnityEngine.iOS.DeviceGeneration.iPhone4S)
		{
            if (isFront)
			{
				preset = "480p";
			}
		}

        arControl.videoConfigurationiOS0 = 
            string.Format(
                "-device=iPhone {0}-preset={1}",
                isFront ? "-position=front " : string.Empty,
                preset);
       
#elif UNITY_ANDROID && !UNITY_EDITOR
        // todo : front cam setting. currently no exposed front cam property in android video config
        arControl.videoConfigurationAndroid0 = isFront == false ? "-device=Android" : "-device=Android";

        yield return new WaitForSeconds(0.1f);

        using (AndroidJavaClass cls_UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            using (AndroidJavaObject obj_Activity = cls_UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
				obj_Activity.Call("SetCamera", isFront ? "front" : "rear", isHighRes ? "high" : "medium");
			}
		}
#elif UNITY_EDITOR_OSX
        foreach (WebCamDevice webCamDevice in WebCamTexture.devices)
        {
            switch (webCamDevice.name)
            {
                case macCamName:
                case macDualBootCamName:
                    arControl.videoCParamName0 = "camera_para";
                    arControl.videoConfigurationMacOSX0 = "-width=960 -height=720";
                    arControl.ContentRotate90 = false;
                    arControl.ContentFlipH = true;
                    arControl.ContentFlipV = false;
                    break;
            }
        }
#elif UNITY_EDITOR_WIN
        const string ConfigFormat =
            "-device={0} <?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<dsvl_input><camera show_format_dialog=\"{1}\"" +
            " friendly_name=\"{2}\" frame_width=\"{3}\" frame_height=\"{4}\"" +
            " frame_rate=\"{5}\"><pixel_format><{6} flip_h=\"{7}\"" +
            " flip_v=\"{8}\"/></pixel_format></camera></dsvl_input>";
        const string WinDevice = "WinDSVL";
        const string ShowFormatDialog = "false";
        const string FriendlyName = "Microsoft LifeCam Front";
        const string LogitechFriendlyName = "Logitech HD Pro Webcam C920";
        const string Width = "1280";
        const string Height = "720";
        const string FrameRate = "15.0";
        const string PixelFormat = "RGB32";
        const string FlipH = "false";
        const string FlipV = "true";

	    string windowsConfig = string.Format(
	        ConfigFormat,
	        WinDevice,
	        ShowFormatDialog,
	        FriendlyName,
	        Width,
	        Height,
	        FrameRate,
	        PixelFormat,
	        FlipH,
	        FlipV);

        foreach (WebCamDevice webCamDevice in WebCamTexture.devices)
        {
            switch (webCamDevice.name)
            {
                case FriendlyName:
                case LogitechFriendlyName:
                case macCamName:
                case macDualBootCamName:
                    arControl.videoCParamName0 = "camera_para";
                    arControl.videoConfigurationMacOSX0 = "-width=960 -height=720";
                    arControl.videoConfigurationWindows0 = windowsConfig;
                    arControl.videoConfigurationWindows1 = windowsConfig;
                    arControl.ContentRotate90 = false;
                    arControl.ContentFlipH = true;
                    arControl.ContentFlipV = false;
                    break;
            }
        }
#endif

        arControl.StartAR();
    }

    /*
    void OnGUI () {
		if(GUI.Button(new Rect(20,40,120,20), "Flip Camera")) {
            SwapCamera();
        }
    }
    */
}
