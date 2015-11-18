/*
 *  ARTrackedCamera.cs
 *  ARToolKit for Unity
 *
 *  Copyright 2010-2014 ARToolworks, Inc. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// A class which directly associates an ARMarker with a Unity Camera object.
/// 
/// To get a list of foreground Camera objects, do:
///
///     List<Camera> foregroundCameras = new List<Camera>();
///     ARCamera[] arCameras = FindObjectsOfType<ARCamera>(); // (or FindObjectsOfType(typeof(ARCamera)) as ARCamera[])
///     foreach (ARCamera arc in arCameras) {
///         foregroundCameras.Add(arc.gameObject.camera);
///     }
/// </summary>
/// 
[RequireComponent(typeof(Transform))]   // A Transform is required to update the position and orientation from tracking
[ExecuteInEditMode]                     // Run in the editor so we can keep the scale at 1
public class ARTrackedCamera : ARCamera
{
	private const string LogTag = "ARTrackedCamera: ";

	public float secondsToRemainVisible = 0.0f;		// How long to remain visible after tracking is lost (to reduce flicker)

	[NonSerialized]
	protected int cullingMask = -1;					// Correct culling mask for content (set to 0 when not visible)

	private bool lastArVisible = false;
	
	// Private fields with accessors.
	[SerializeField]
	private string _markerTag = "";					// Unique tag for the marker to get tracking from
	
	public string MarkerTag
	{
		get
		{
			return _markerTag;
		}
		
		set
		{
			_markerTag = value;
			_marker = null;
		}
	}
	
	// Return the marker associated with this component.
	// Uses cached value if available, otherwise performs a find operation.
	public override ARMarker GetMarker()
	{
		if (_marker == null) {
			// Locate the marker identified by the tag
			ARMarker[] ms = FindObjectsOfType<ARMarker>();
			foreach (ARMarker m in ms) {
				if (m.Tag == _markerTag) {
					_marker = m;
					break;
				}
			}
		}
		return _marker;
	}

	public virtual void Start()
	{
		// Store the camera's initial culling mask. When the marker is tracked, this mask will be used
		// so that the virtual objects are rendered. When tracking is lost, 0 will be used, so that no 
		// objects are displayed.
		if (cullingMask == -1) {
			cullingMask = this.gameObject.GetComponent<Camera>().cullingMask;
		}
	}

	protected override void ApplyTracking()
	{
		if (arVisible || (timeLastUpdate - timeTrackingLost < secondsToRemainVisible)) {
			if (arVisible != lastArVisible) {
				this.gameObject.GetComponent<Camera>().cullingMask = cullingMask;
				if (eventReceiver != null) eventReceiver.BroadcastMessage("OnMarkerFound", GetMarker(), SendMessageOptions.DontRequireReceiver);
			}
			transform.localPosition = arPosition; // TODO: Change to transform.position = PositionFromMatrix(origin.transform.localToWorldMatrix * pose) etc;
			transform.localRotation = arRotation;
			if (eventReceiver != null) eventReceiver.BroadcastMessage("OnMarkerTracked", GetMarker(), SendMessageOptions.DontRequireReceiver);
		} else {
			if (arVisible != lastArVisible) {
				this.gameObject.GetComponent<Camera>().cullingMask = 0;
				if (eventReceiver != null) eventReceiver.BroadcastMessage("OnMarkerLost", GetMarker(), SendMessageOptions.DontRequireReceiver);
			}
		}
		lastArVisible = arVisible;
	}

}
