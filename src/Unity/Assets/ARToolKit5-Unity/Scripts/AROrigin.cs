/*
 *  AROrigin.cs
 *  ARToolKit for Unity
 *
 *  Copyright 2014-2014 ARToolworks, Inc. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Transform))]
[ExecuteInEditMode]
public class AROrigin : MonoBehaviour
{
	private const string LogTag = "AROrigin: ";

	public enum FindMode {
		AutoAll,
		AutoByTags,
		Manual
	}

    [SerializeField]
    private List<string> findMarkerTags = new List<string>();

    [SerializeField]
    private FindMode _findMarkerMode = FindMode.AutoAll;

    private ARMarker baseMarker = null;
    private readonly List<ARMarker> markersEligibleForBaseMarker = new List<ARMarker>();

	public FindMode findMarkerMode
	{
		get
		{
			return _findMarkerMode;
		}
		
		set
		{
			if (_findMarkerMode != value) {
				_findMarkerMode = value;
				FindMarkers();
			}
		}
	}

    /// <summary>
    /// Used as a flag read by <see cref="ARTrackedObject"/>s to notify them to stop applying transformation changes.
    /// </summary>
    public bool Paused { get; set; }

	public void AddMarker(ARMarker marker, bool atHeadOfList = false)
	{
		if (!atHeadOfList) {
			markersEligibleForBaseMarker.Add(marker);
		} else {
			markersEligibleForBaseMarker.Insert(0, marker);
		}
	}

	public bool RemoveMarker(ARMarker marker)
	{
		if (baseMarker == marker) baseMarker = null;
		return markersEligibleForBaseMarker.Remove(marker);
	}
	
	public void RemoveAllMarkers()
	{
		baseMarker = null;
		markersEligibleForBaseMarker.Clear();
	}

	public void FindMarkers()
	{
		RemoveAllMarkers();
		if (findMarkerMode != FindMode.Manual) {
			ARMarker[] ms = FindObjectsOfType<ARMarker>(); // Does not find inactive objects.
			foreach (ARMarker m in ms) {
				if (findMarkerMode == FindMode.AutoAll || (findMarkerMode == FindMode.AutoByTags && findMarkerTags.Contains(m.Tag))) {
					markersEligibleForBaseMarker.Add(m);
				}
			}
			ARController.Log(LogTag + "Found " + markersEligibleForBaseMarker.Count + " markers eligible to become base marker.");
		}
	}

	void Start()
	{
		FindMarkers();
	}

	// Get the marker, if any, currently acting as the base.
	public ARMarker GetBaseMarker()
	{
		if (baseMarker != null) {
			if (baseMarker.Visible) return baseMarker;
			else baseMarker = null;
		}
		
		foreach (ARMarker m in markersEligibleForBaseMarker) {
			if (m.Visible) {
				baseMarker = m;
                ARController.Log(string.Format("Marker {0} became base marker.", m.UID));
                break;
			}
		}
		
		return baseMarker;
	}
	
	void OnDestroy()
	{
		RemoveAllMarkers();
	}
}
