// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SmoothDampTRMatrixDrawer.cs" company="Daqri LLC">
//   Copyright © 2015 DAQRI. DAQRI is a registered trademark of DAQRI LLC. All Rights Reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace SmoothTR.Editor
{
    using UnityEditor;

    using UnityEngine;

    /// <summary>
    /// The GUI drawer for the <see cref="SmoothDampTRMatrix"/>
    /// </summary>
    [CustomPropertyDrawer(typeof(SmoothDampTRMatrix), false)]
    internal sealed class SmoothDampTRMatrixDrawer : PropertyDrawer
    {
        /// <summary>
        /// <para>
        /// Override this method to make your own GUI for the property.
        /// </para>
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property GUI.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">The label of this property.</param>
        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            float lineHeight = base.GetPropertyHeight(property, label);

            position.height = lineHeight;
            while (property.NextVisible(true))
            {
                EditorGUI.PropertyField(position, property);
                position.y += position.height;
            }
        }

        /// <summary>
        /// <para>
        /// Override this method to specify how tall the GUI for this field is in pixels.
        /// </para>
        /// </summary>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param><param name="label">The label of this property.</param>
        /// <returns>
        /// <para>
        /// The height in pixels.
        /// </para>
        /// </returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float fieldCount = 0f;
            while (property.NextVisible(true))
            {
                ++fieldCount;
            }

            return base.GetPropertyHeight(property, label) * Mathf.Max(fieldCount, 1f);
        }
    }
}