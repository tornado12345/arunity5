using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

public class ARMarkerSet : MonoBehaviour {
	public ARController arControl;
	public bool forceLoad = false;   //force load of markers, useful for debugging w/o whole application
	private bool loading = false;
	private ARMarker[] allMarkers;
	private Action callback;

	public static ARMarkerSet Instance()
	{
		GameObject arToolKit = GameObject.Find("ARToolKit");
		if (arToolKit == null) {
			Debug.Log ("ARMarkerSet: No ARToolkit object found");
			return null;
		}
			
		ARMarkerSet script = arToolKit.GetComponent<ARMarkerSet>();
		if (script == null) {
			Debug.LogError ("ARMarkerSet: No ARMarkerSet found");
			return null;
		}

		return script;
	}

	private void Awake () 
	{
		if (arControl == null)
	    {
	    	arControl = FindObjectOfType<ARController>();
	    }
	}

	public void Start()
	{
		// force load of markers, useful for debugging without whole application
		if (forceLoad == true) {
			LoadMarkers (GameObject.Find ("ARToolKit"), false);
		}
	}

	public bool IsLoading()
	{
		return loading;
	}

	public bool UnloadMarkers(ARMarker[] markers)
	{
		for (int i = 0; i < markers.Length; ++i)
		{
			markers[i].Unload();
		}

		return true;
	}

	public bool UnloadMarkers(GameObject parentObject)
	{
		if (parentObject != null) 
		{
			return UnloadMarkers (parentObject.GetComponentsInChildren<ARMarker> ());
		} 

		return false;
	}

	public bool LoadMarkers(ARMarker[] markers, bool useThread, Action completeCallback = null)
	{
		if (IsLoading ())
			return false;

		loading = true;
		allMarkers = markers;
		callback = completeCallback;
		StartCoroutine(_BeginMarkerLoadSeq(useThread));

		return true;
	}

	//load all markers under this object
	public bool LoadMarkers(GameObject parentObject, bool useThread,Action completeCallback = null)
	{
		if (IsLoading ())
			return false;

		if (parentObject == null)
			return false;

		allMarkers = parentObject.GetComponentsInChildren<ARMarker>();

		return LoadMarkers (allMarkers,useThread,completeCallback);
	}

	IEnumerator _BeginMarkerLoadSeq(bool useThread)
	{
		if (arControl!=null) arControl.AllowUpdateAR = false;// switch off updates to native side

		List<ARMarker> markersToLoad = new List<ARMarker>(allMarkers);

		for (int i = 0, count = allMarkers.Length; i < count; ++i)
		{
			allMarkers[i].LoadMe( useThread, marker => { markersToLoad.Remove(marker); }); 
		}

		while (markersToLoad.Count > 0)
		{
			yield return null;
		}

		loading = false;

		if (arControl!=null)
			arControl.AllowUpdateAR = true;

		if (callback!=null)
			callback ();
	}
}
