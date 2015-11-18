// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SmoothDampTRMatrix.cs" company="Daqri LLC">
//   Copyright © 2015 DAQRI. DAQRI is a registered trademark of DAQRI LLC. All Rights Reserved.
// </copyright>
// <summary>
//   Thread safe matrix access for both translational and rotational data.
//   Also allows for applying a "chasing" formula to smoothly lerp to the values if needed.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace SmoothTR
{
    using System;
    using System.Diagnostics;

    using UnityEngine;

    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Thread safe matrix access for both translational and rotational data.
    /// Also allows for applying a "chasing" formula to smoothly lerp to the values if needed.
    /// </summary>
    public sealed class SmoothDampTRMatrix
    {
        /// <summary>
        /// This should be a number between 0 and 1. Will be used to lerp to new rotation to smooth out very small deltas.
        /// </summary>
        private const float RotationNoiseBufferBias = 0.9f;
        private const float RotationSmoothTime = 0.33f;
        private const float PositionSmoothTime = 8f;
        private const float RotationMaxVelocity = 360f;
        private const float PositionMaxVelocity = 1000f;

        private readonly object matrixLock = new object();
        private readonly object enabledLock = new object();
        private readonly Stopwatch positionTimer = new Stopwatch();
        private readonly Stopwatch rotationTimer = new Stopwatch();
        private Quaternion? rotation;
        private Vector3? position;
        private Vector3 positionVelocity = Vector3.zero;
        private Vector3 rotationVelocity;
        private bool enabled;

        /// <summary>
        /// Gets the current values of the matrix in a data source.
        /// <para>
        /// If the <see cref="TRMatrix"/> <see cref="TRMatrix.Position"/> or <see cref="TRMatrix.Rotation"/> are null, they where not applied yet.
        /// </para>
        /// </summary>
        public TRMatrix Value
        {
            get
            {
                lock (matrixLock)
                {
                    try
                    {
                        return new TRMatrix(rotation, position);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets if this should be updating the matrix.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public bool Enabled
        {
            get
            {
                lock (enabledLock)
                {
                    return enabled;
                }
            }

            set
            {
                lock (enabledLock)
                {
                    if (enabled == value)
                    {
                        return;
                    }

                    enabled = value;
                    lock (positionTimer)
                    {
                        lock (rotationTimer)
                        {
                            if (enabled)
                            {
                                positionTimer.Start();
                                rotationTimer.Start();
                            }
                            else
                            {
                                positionTimer.Stop();
                                rotationTimer.Stop();
                            }
                        }
                    }
                }
            }
        }

        private float PositionDeltaTime
        {
            get
            {
                lock (positionTimer)
                {
                    float elapsedSeconds =
                        Convert.ToSingle(positionTimer.ElapsedMilliseconds) * 0.001f;
                    if (Enabled)
                    {
                        positionTimer.Start();
                    }

                    return elapsedSeconds;
                }
            }
        }

        private float RotationDeltaTime
        {
            get
            {
                lock (rotationTimer)
                {
                    float elapsedSeconds =
                        Convert.ToSingle(rotationTimer.ElapsedMilliseconds) * 0.001f;
                    rotationTimer.Reset();
                    if (Enabled)
                    {
                        rotationTimer.Start();
                    }

                    return elapsedSeconds;
                }
            }
        }

        /// <summary>
        /// Resets the position and rotation values as well as the current velocity to the current value.
        /// <para>
        /// This does not stop or start this, use <see cref="Enabled"/> to control that.
        /// </para>
        /// </summary>
        public void Reset()
        {
            lock (matrixLock)
            {
                rotation = null;
                position = null;
                positionVelocity = Vector3.zero;
                rotationVelocity = Vector3.zero;

                lock (positionTimer)
                {
                    positionTimer.Reset();
                    if (Enabled)
                    {
                        positionTimer.Start();
                    }
                }

                lock (rotationTimer)
                {
                    rotationTimer.Reset();
                    if (Enabled)
                    {
                        rotationTimer.Start();
                    }
                }
            }
        }

        /// <summary>
        /// Will instantly set the rotation from the supplied matrix.
        /// </summary>
        /// <param name="matrix">
        /// The source to extract a rotation from.
        /// </param>
        public void SetRotationFromMatrix(Matrix4x4 matrix)
        {
            try
            {
                Quaternion newRotation = RotationFromMatrix(matrix);

                lock (matrixLock)
                {
                    rotation = newRotation;

                    CheckRotation(rotation);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Will instantly set the rotation and position from the supplied matrix.
        /// </summary>
        /// <param name="matrix">
        /// The source to extract a rotation and position from.
        /// </param>
        public void SetRotationAndPositionFromMatrix(Matrix4x4 matrix)
        {
            try
            {
                Quaternion newRotation = RotationFromMatrix(matrix);
                Vector3 newPosition = PositionFromMatrix(matrix);

                lock (matrixLock)
                {
                    rotation = newRotation;
                    position = newPosition;

                    CheckRotation(rotation);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Will move towards the rotation from the supplied matrix at speeds derived from the smooth times and max velocities.
        /// </summary>
        /// <param name="matrix">
        /// The source to extract a rotation from.
        /// </param>
        public void SmoothDampRotationFromMatrix(Matrix4x4 matrix)
        {
            try
            {
                Quaternion newRotation = RotationFromMatrix(matrix);

                lock (matrixLock)
                {
                    rotation = SmoothRotationValue(
                        rotation ?? newRotation, 
                        newRotation, 
                        ref rotationVelocity, 
                        RotationSmoothTime, 
                        RotationMaxVelocity, 
                        RotationDeltaTime);

                    CheckRotation(rotation);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Will move towards the rotation and position from the supplied matrix at speeds derived from the smooth times and max velocities.
        /// </summary>
        /// <param name="matrix">
        /// The source to extract a rotation and position from.
        /// </param>
        public void SmoothDampRotationAndPositionFromMatrix(Matrix4x4 matrix)
        {
            try
            {
                Quaternion newRotation = RotationFromMatrix(matrix);
                Vector3 newPosition = PositionFromMatrix(matrix);

                lock (matrixLock)
                {
                    rotation = SmoothRotationValue(
                        rotation ?? newRotation, 
                        newRotation, 
                        ref rotationVelocity, 
                        RotationSmoothTime, 
                        RotationMaxVelocity, 
                        RotationDeltaTime);

                    position = Vector3.SmoothDamp(
                        position ?? newPosition, 
                        newPosition, 
                        ref positionVelocity, 
                        PositionSmoothTime, 
                        PositionMaxVelocity, 
                        PositionDeltaTime);

                    CheckRotation(rotation);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void CheckRotation(Quaternion? rotationValue)
        {
            if (!rotationValue.HasValue)
            {
                return;
            }

            if (float.IsNaN(rotationValue.Value.x))
            {
                throw new Exception("value x is NaN");
            }

            if (float.IsNaN(rotationValue.Value.y))
            {
                throw new Exception("value y is NaN");
            }

            if (float.IsNaN(rotationValue.Value.z))
            {
                throw new Exception("value z is NaN");
            }

            if (float.IsNaN(rotationValue.Value.w))
            {
                throw new Exception("value w is NaN");
            }
        }

        private static Quaternion SmoothRotationValue(
            Quaternion source, 
            Quaternion target, 
            ref Vector3 velocity, 
            float smoothTime, 
            float maxSpeed, 
            float currentDeltaTime)
        {
            Vector3 sourceEuler = source.eulerAngles;
            Vector3 targetEuler = target.eulerAngles;

            Quaternion newTarget =
                Quaternion.Euler(
                    SmoothDampAngle(
                        sourceEuler.x,
                        targetEuler.x,
                        ref velocity.x,
                        smoothTime,
                        maxSpeed,
                        currentDeltaTime),
                    SmoothDampAngle(
                        sourceEuler.y,
                        targetEuler.y,
                        ref velocity.y,
                        smoothTime,
                        maxSpeed,
                        currentDeltaTime),
                    SmoothDampAngle(
                        sourceEuler.z,
                        targetEuler.z,
                        ref velocity.z,
                        smoothTime,
                        maxSpeed,
                        currentDeltaTime));

            return Quaternion.Lerp(source, newTarget, RotationNoiseBufferBias);
        }

        private static float SmoothDampAngle(
            float current, 
            float target, 
            ref float currentVelocity, 
            float smoothTime, 
            float maxSpeed, 
            float currentDeltaTime)
        {
            return Mathf.SmoothDampAngle(
                current, 
                target, 
                ref currentVelocity, 
                smoothTime, 
                maxSpeed, 
                currentDeltaTime);
        }

        private static Quaternion RotationFromMatrix(Matrix4x4 matrix)
        {
            // if (matrix.GetColumn(2) == Vector4.zero)
            // {
            // throw new Exception("Invalid rotation in matrix.");
            // }
            return Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
        }

        private static Vector3 PositionFromMatrix(Matrix4x4 matrix)
        {
            return matrix.GetColumn(3);
        }
    }
}
