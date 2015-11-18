/*
 *  ARTrackedObject.cs
 *  ARToolKit for Unity
 *
 *  Copyright 2014-2014 ARToolworks, Inc. All rights reserved.
 *
 */

using System;

using Daqri.Platform.Utilities;

using SmoothTR;

using UnityEngine;

[RequireComponent(typeof(Transform))]
[ExecuteInEditMode]
public class ARTrackedObject : MonoBehaviour
{
    private AROrigin _origin = null;
    private ARMarker _marker = null;

    private bool visible = false;               // Current visibility from tracking
    private float? timeTrackingLost;            // Time when tracking was last lost
    public float secondsToRemainVisible = 0f;   // How long to remain visible after tracking is lost (to reduce flicker)

    private readonly SmoothDampTRMatrix smoothDampTRMatrix = new SmoothDampTRMatrix();
    /// <summary>
    /// Used to synchronize _marker and visible.
    /// </summary>
    private readonly object markerLock = new object();

    /// <summary>How long to remain visible after tracking is lost (to reduce flicker).</summary>
    public GameObject eventReceiver;
    public GameObject extractionEventReceiver;

    [SerializeField]
    private string _markerTag = string.Empty;

    [SerializeField]
    [Euler]
    private Quaternion additionalRotation = Quaternion.identity;

    private bool applicationIsPlaying;
    private ARController arController;
    private bool lastFrameWasVisible;
    private Transform parent;

    /// <summary>
    /// The tag that will be used to associate this tracked object with an <see cref="ARMarker"/>.
    /// </summary>
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

    private bool IsMultimarker
    {
        get
        {
            lock (markerLock)
            {
                return _marker != null && _marker.MarkerType == MarkerType.Multimarker;
            }
        }
    }

    private bool IsBase
    {
        get
        {
            lock (markerLock)
            {
                return _origin != null && _origin.GetBaseMarker() == _marker;
            }
        }
    }

    public ARMarker GetMarker()
    {
        lock (markerLock)
        {
            if (_marker != null)
            {
                return _marker;
            }

            ARMarker[] ms = FindObjectsOfType<ARMarker>();
            foreach (ARMarker m in ms)
            {
                if (string.Equals(m.Tag, _markerTag, StringComparison.Ordinal))
                {
                    _marker = m;
                    break;
                }
            }

            return _marker;
        }
    }

    /// <summary>
    /// Utility method used to send the <see cref="GameObject.BroadcastMessage(string, object, SendMessageOptions)"/>.
    /// This ensures the method is sent the same way each time and only if the <paramref name="receiver"/> is not null.
    /// Mostly to prevent duplicate code.
    /// </summary>
    /// <param name="receiver">Calls the method named <paramref name="methodName"/> on every MonoBehaviour in this game object or any of its children.</param>
    /// <param name="methodName">Name of the method to call.</param>
    /// <param name="parameter">Optional parameter to pass to the method <c>(can be any value)</c>.</param>
    private static void NotifyReceiver(
        GameObject receiver,
        string methodName,
        object parameter)
    {
        if (receiver == null)
        {
            return;
        }

        receiver.BroadcastMessage(
            methodName,
            parameter,
            SendMessageOptions.DontRequireReceiver);
    }

    private void OnEnable()
    {
        try
        {
            applicationIsPlaying = Application.isPlaying;
            if (!applicationIsPlaying)
            {
                return;
            }

            if ((_origin = GetComponentInParent<AROrigin>()) == null)
            {
                throw new NullReferenceException("_origin");
            }

            if ((arController = ARController.Instance()) != null)
            {
                arController.RegisterUpdateEvent(
                    new ARController.UpdateEventObject(ProcessARUpdate, 2));
            }

            if ((parent = transform.parent) == null)
            {
                throw new NullReferenceException("transform.parent");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        lock (markerLock)
        {
            _marker = null;
        }

        if (arController != null)
        {
            arController.UnregisterUpdateEvent(ProcessARUpdate);
            arController = null;
        }

        smoothDampTRMatrix.Enabled = false;
        smoothDampTRMatrix.Reset();

        lastFrameWasVisible = false;
        timeTrackingLost = null;
    }

    private void LateUpdate()
    {
        if (_origin != null && _origin.Paused)
        {
            return;
        }

        bool markerIsVisible;
        lock (markerLock)
        {
            if (_marker == null)
            {
                GetMarker();
                if (_marker == null)
                {
                    return;
                }

                if (!IsMultimarker)
                {
                    if (applicationIsPlaying)
                    {
                        // In Player, set initial visibility to not visible.
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            transform.GetChild(i).gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        // In Editor, set initial visibility to visible.
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            transform.GetChild(i).gameObject.SetActive(true);
                        }
                    }
                }

                smoothDampTRMatrix.Reset();
                smoothDampTRMatrix.Enabled = true;

                return;
            }

            if (!_marker.TransformationMatrix.HasValue)
            {
                return;
            }

            markerIsVisible = _marker.Visible;
        }

        if (IsMultimarker)
        {
            ProcessVisibleMultimarker(smoothDampTRMatrix.Value);
        }

        if (markerIsVisible)
        {
            ProcessVisible();
        }
        else
        {
            ProcessNonVisible();
        }
    }

    private void ProcessARUpdate()
    {
        lock (markerLock)
        {
            if (_marker == null)
            {
                visible = false;
                smoothDampTRMatrix.Enabled = false;
                smoothDampTRMatrix.Reset();
                return;
            }

            visible = _marker.Visible;
            if (!visible || !_marker.TransformationMatrix.HasValue)
            {
                return;
            }

            UpdateMatrix(_marker.TransformationMatrix.Value);
        }
    }

    private void ProcessNonVisible()
    {
        float timeNow = Time.realtimeSinceStartup;
        if (lastFrameWasVisible)
        {
            lastFrameWasVisible = false;
            timeTrackingLost = timeNow;
        }

        if (!timeTrackingLost.HasValue || timeNow - timeTrackingLost.Value < secondsToRemainVisible)
        {
            return;
        }

        timeTrackingLost = null;

        NotifyReceiver(eventReceiver, "OnMarkerLost", _marker);
        NotifyReceiver(extractionEventReceiver, "OnMarkerLostStopExtraction", gameObject);

        if (IsMultimarker)
        {
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void ProcessVisible()
    {
        timeTrackingLost = null;

        if (!lastFrameWasVisible)
        {
            lastFrameWasVisible = true;
            NotifyReceiver(eventReceiver, "OnMarkerFound", _marker);
            NotifyReceiver(extractionEventReceiver, "OnMarkerFoundSetUpExtraction", gameObject);
        }

        if (!IsMultimarker)
        {
            ProcessVisibleOriginal();
        }

        NotifyReceiver(eventReceiver, "OnMarkerTracked", _marker);
        NotifyReceiver(extractionEventReceiver, "OnMarkerTrackedForExtraction", gameObject);
    }

    private void ProcessVisibleOriginal()
    {
        if (IsBase)
        {
            transform.rotation = _origin.transform.rotation;
            transform.position = _origin.transform.position;
        }
        else
        {
            Matrix4x4? baseTransformationMatrix;
            Matrix4x4? markerTransformationMatrix;
            if (!(baseTransformationMatrix = _origin.GetBaseMarker().TransformationMatrix).HasValue ||
                !(markerTransformationMatrix = _marker.TransformationMatrix).HasValue)
            {
                return;
            }

            Matrix4x4 pose = _origin.transform.localToWorldMatrix *
                             baseTransformationMatrix.Value.inverse *
                             markerTransformationMatrix.Value;
            transform.position = ARUtilityFunctions.PositionFromMatrix(pose);
            transform.rotation = ARUtilityFunctions.QuaternionFromMatrix(pose);
        }
    }

    private void ProcessVisibleMultimarker(TRMatrix updatedTarget)
    {
        transform.localScale = Vector3.one;

        if (updatedTarget == null)
        {
            return;
        }

        if (updatedTarget.Rotation.HasValue)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                Quaternion.Slerp(
                    updatedTarget.Rotation.Value,
                    Quaternion.Inverse(parent.rotation) * updatedTarget.Rotation.Value,
                    1f) * additionalRotation,
                1f);
        }

        if (updatedTarget.Position.HasValue)
        {
            transform.localPosition =
                parent.InverseTransformPoint(updatedTarget.Position.Value);
        }
    }

    private void UpdateMatrix(Matrix4x4 transformationMatrix)
    {
        smoothDampTRMatrix.SmoothDampRotationFromMatrix(transformationMatrix);
    }
}
