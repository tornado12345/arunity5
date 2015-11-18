// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TRMatrix.cs" company="Daqri LLC">
//   Copyright © 2015 DAQRI. DAQRI is a registered trademark of DAQRI LLC. All Rights Reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace SmoothTR
{
    using System;

    using UnityEngine;

    /// <summary>
    /// Data from the <see cref="SmoothDampTRMatrix"/> to provide a quick way of polling the values without additional locks.
    /// </summary>
    public sealed class TRMatrix
    {
        /// <summary>
        /// If null, their is no data provided yet for this field.
        /// </summary>
        public readonly Quaternion? Rotation;

        /// <summary>
        /// If null, their is no data provided yet for this field.
        /// </summary>
        public readonly Vector3? Position;

        /// <summary>
        /// Initializes a new instance of the <see cref="TRMatrix"/> class.
        /// </summary>
        /// <param name="rotation">
        /// Optional rotational amount.
        /// </param>
        /// <param name="position">
        /// Optional translational amount.
        /// </param>
        /// <exception cref="Exception">
        /// <para>
        /// value x is NaN
        /// </para>
        /// <para>
        /// value y is NaN
        /// </para>
        /// <para>
        /// value z is NaN
        /// </para>
        /// <para>
        /// value w is NaN
        /// </para>
        /// </exception>
        public TRMatrix(Quaternion? rotation, Vector3? position)
        {
            if (rotation.HasValue)
            {
                if (float.IsNaN(rotation.Value.x))
                {
                    throw new Exception("value x is NaN");
                }

                if (float.IsNaN(rotation.Value.y))
                {
                    throw new Exception("value y is NaN");
                }

                if (float.IsNaN(rotation.Value.z))
                {
                    throw new Exception("value z is NaN");
                }

                if (float.IsNaN(rotation.Value.w))
                {
                    throw new Exception("value w is NaN");
                }
            }

            Rotation = rotation;
            Position = position;
        }
    }
}