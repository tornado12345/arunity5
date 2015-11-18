/*
 *  ARUtilityFunctions.cs
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

public static class ARUtilityFunctions
{

	/// <summary>
	/// Returns the named camera or null if not found.
	/// </summary>
	/// <param name="name">Camera name to search for.</param>
	/// <returns>The named <see cref="Camera"/> or null if not found.</returns>
	public static Camera FindCameraByName(string name)
	{
	    foreach (Camera c in Camera.allCameras)
	    {
	        if (c.gameObject.name == name) return c;
	    }

	    return null;
	}


	/// <summary>
	/// Creates a Unity matrix from an array of floats.
	/// </summary>
	/// <param name="values">Array of 16 floats to populate the matrix.</param>
	/// <returns>A new <see cref="Matrix4x4"/> with the given values.</returns>
	public static Matrix4x4 MatrixFromFloatArray(float[] values)
	{
	    if (values == null || values.Length < 16) throw new ArgumentException("Expected 16 elements in values array", "values");

	    Matrix4x4 mat = new Matrix4x4();
	    for (int i = 0; i < 16; i++) mat[i] = values[i];
	    return mat;
	}

#if false
	// Posted on: http://answers.unity3d.com/questions/11363/converting-matrix4x4-to-quaternion-vector3.html
	public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
	{
	    // Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
	    Quaternion q = new Quaternion();
	    q.w = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2;
	    q.x = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2;
	    q.y = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2;
	    q.z = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2;
	    q.x *= Mathf.Sign(q.x * (m[2, 1] - m[1, 2]));
	    q.y *= Mathf.Sign(q.y * (m[0, 2] - m[2, 0]));
	    q.z *= Mathf.Sign(q.z * (m[1, 0] - m[0, 1]));
	    return q;
	}
#else
	public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
	{
		// Trap the case where the matrix passed in has an invalid rotation submatrix.
		if (m.GetColumn(2) == Vector4.zero) {
			ARController.Log("QuaternionFromMatrix got zero matrix.");
			return Quaternion.identity;
		}
		return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
	}
#endif

	public static Vector3 PositionFromMatrix(Matrix4x4 m)
	{
	    return m.GetColumn(3);
	}

	public static Vector3 ScaleFromMatrix(Matrix4x4 m)
	{
		return new Vector3 (m.GetColumn (0).magnitude, m.GetColumn (1).magnitude, m.GetColumn (2).magnitude);
	}

    // Convert from right-hand coordinate system with <normal vector> in direction of +x,
    // <orthogonal vector> in direction of +y, and <approach vector> in direction of +z,
    // to Unity's left-hand coordinate system with <normal vector> in direction of +x,
    // <orthogonal vector> in direction of +y, and <approach vector> in direction of +z.
    // This is equivalent to negating row 2, and then negating column 2.
    public static Matrix4x4 LHMatrixFromRHMatrix(Matrix4x4 rhm, bool useNew)
	{
        if (useNew)
        {
            return new Matrix4x4
            {
                m00 = rhm.m00,
                m01 = rhm.m01,
                m02 = rhm.m02,
                m03 = rhm.m03,
                m10 = rhm.m10,
                m11 = rhm.m11,
                m12 = rhm.m12,
                m13 = rhm.m13,
                m20 = -rhm.m20,
                m21 = -rhm.m21,
                m22 = -rhm.m22,
                m23 = -rhm.m23,
                m30 = rhm.m30,
                m31 = rhm.m31,
                m32 = -rhm.m32,
                m33 = rhm.m33
            };
        }

        return new Matrix4x4
        {
            m00 = rhm.m00,
            m01 = rhm.m01,
            m02 = -rhm.m02,
            m03 = rhm.m03,
            m10 = rhm.m10,
            m11 = rhm.m11,
            m12 = -rhm.m12,
            m13 = rhm.m13,
            m20 = -rhm.m20,
            m21 = -rhm.m21,
            m22 = rhm.m22,
            m23 = -rhm.m23,
            m30 = rhm.m30,
            m31 = rhm.m31,
            m32 = -rhm.m32,
            m33 = rhm.m33
        };
    }
}
