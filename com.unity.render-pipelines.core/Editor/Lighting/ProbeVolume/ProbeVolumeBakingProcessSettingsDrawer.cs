using UnityEditor;
using UnityEngine;

namespace UnityEngine.Experimental.Rendering
{
    [CustomPropertyDrawer(typeof(ProbeVolumeBakingProcessSettings))]
    class ProbeVolumeBakingProcessSettingsDrawer : PropertyDrawer
    {
        static class Styles
        {
            public static readonly GUIContent enableDilation = new GUIContent("Enable Dilation", "Whether to enable dilation after the baking. Dilation will dilate valid probes data into invalid probes.");
            public static readonly GUIContent dilationDistance = new GUIContent("Dilation Distance", "The distance used to pick neighbouring probes to dilate into the invalid probe.");
            public static readonly GUIContent dilationValidity = new GUIContent("Dilation Validity Threshold", "The validity threshold used to identify invalid probes.");
            public static readonly GUIContent dilationIterationCount = new GUIContent("Dilation Iteration Count", "The number of times the dilation process takes place.");
            public static readonly GUIContent dilationSquaredDistanceWeighting = new GUIContent("Squared Distance Weighting", "Whether to weight neighbouring probe contribution using squared distance rather than linear distance.");
            public static readonly GUIContent useVirtualOffset = EditorGUIUtility.TrTextContent("Use Virtual Offset", "Push invalid probes out of geometry. Please note, this feature is currently a proof of concept, it is fairly slow and not optimal in quality.");
            public static readonly GUIContent virtualOffsetSearchMultiplier = EditorGUIUtility.TrTextContent("Search multiplier", "A multiplier to be applied on the distance between two probes to derive the search distance out of geometry.");
            public static readonly GUIContent virtualOffsetBiasOutGeometry = EditorGUIUtility.TrTextContent("Bias out geometry", "Determines how much a probe is pushed out of the geometry on top of the distance to closest hit.");

            public static readonly GUIContent autoInvalidate = EditorGUIUtility.TrTextContent("Auto Invalidate", "TODO_FCC ADD ME.");
            public static readonly GUIContent checkRange = EditorGUIUtility.TrTextContent("Invalidate range", "TODO_FCC ADD ME.");

            public static readonly string dilationSettingsTitle = "Dilation Settings";
            public static readonly string advancedTitle = "Advanced";
            public static readonly string virtualOffsetSettingsTitle = "Virtual Offset Settings";
            public static readonly string invalidateSettingsTitle = "Additional Invalidate";
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var dilationSettings = property.FindPropertyRelative("dilationSettings");
            var virtualOffsetSettings = property.FindPropertyRelative("virtualOffsetSettings");
            var invalidationSettings = property.FindPropertyRelative("invalidationSettings");

            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            property.serializedObject.Update();

            DrawDilationSettings(dilationSettings);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            DrawVirtualOffsetSettings(virtualOffsetSettings);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            DrawInvalidateSettings(invalidationSettings);

            EditorGUI.EndProperty();

            property.serializedObject.ApplyModifiedProperties();
        }

        void DrawDilationSettings(SerializedProperty dilationSettings)
        {
            var enableDilation = dilationSettings.FindPropertyRelative("enableDilation");
            var maxDilationSampleDistance = dilationSettings.FindPropertyRelative("dilationDistance");
            var dilationValidityThreshold = dilationSettings.FindPropertyRelative("dilationValidityThreshold");
            float dilationValidityThresholdInverted = 1f - dilationValidityThreshold.floatValue;
            var dilationIterations = dilationSettings.FindPropertyRelative("dilationIterations");
            var dilationInvSquaredWeight = dilationSettings.FindPropertyRelative("squaredDistWeighting");

            EditorGUILayout.LabelField(Styles.dilationSettingsTitle, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            enableDilation.boolValue = EditorGUILayout.Toggle(Styles.enableDilation, enableDilation.boolValue);
            EditorGUI.BeginDisabledGroup(!enableDilation.boolValue);
            maxDilationSampleDistance.floatValue = Mathf.Max(EditorGUILayout.FloatField(Styles.dilationDistance, maxDilationSampleDistance.floatValue), 0);
            dilationValidityThresholdInverted = EditorGUILayout.Slider(Styles.dilationValidity, dilationValidityThresholdInverted, 0f, 0.95f);
            dilationValidityThreshold.floatValue = Mathf.Max(0.05f, 1.0f - dilationValidityThresholdInverted);
            dilationIterations.intValue = EditorGUILayout.IntSlider(Styles.dilationIterationCount, dilationIterations.intValue, 1, 5);
            dilationInvSquaredWeight.boolValue = EditorGUILayout.Toggle(Styles.dilationSquaredDistanceWeighting, dilationInvSquaredWeight.boolValue);
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();

            // if (GUILayout.Button(EditorGUIUtility.TrTextContent("Refresh dilation"), EditorStyles.miniButton))
            // {
            //     ProbeGIBaking.RevertDilation();
            //     ProbeGIBaking.PerformDilation();
            // }
        }

        void DrawVirtualOffsetSettings(SerializedProperty virtualOffsetSettings)
        {
            var enableVirtualOffset = virtualOffsetSettings.FindPropertyRelative("useVirtualOffset");
            var virtualOffsetGeometrySearchMultiplier = virtualOffsetSettings.FindPropertyRelative("searchMultiplier");
            var virtualOffsetBiasOutOfGeometry = virtualOffsetSettings.FindPropertyRelative("outOfGeoOffset");

            EditorGUILayout.LabelField(Styles.virtualOffsetSettingsTitle, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            enableVirtualOffset.boolValue = EditorGUILayout.Toggle(Styles.useVirtualOffset, enableVirtualOffset.boolValue);
            EditorGUI.BeginDisabledGroup(!enableVirtualOffset.boolValue);
            virtualOffsetGeometrySearchMultiplier.floatValue = Mathf.Clamp01(EditorGUILayout.FloatField(Styles.virtualOffsetSearchMultiplier, virtualOffsetGeometrySearchMultiplier.floatValue));
            virtualOffsetBiasOutOfGeometry.floatValue = EditorGUILayout.FloatField(Styles.virtualOffsetBiasOutGeometry, virtualOffsetBiasOutOfGeometry.floatValue);
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();

        }

        void DrawInvalidateSettings(SerializedProperty invalidateSettings)
        {
            var enableExtraInvalidation = invalidateSettings.FindPropertyRelative("enableExtraInvalidation");
            var checkRange = invalidateSettings.FindPropertyRelative("checkRange");
            EditorGUILayout.LabelField(Styles.invalidateSettingsTitle, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            enableExtraInvalidation.boolValue = EditorGUILayout.Toggle(Styles.autoInvalidate, enableExtraInvalidation.boolValue);
            checkRange.floatValue = EditorGUILayout.FloatField(Styles.checkRange, checkRange.floatValue);
            EditorGUI.indentLevel--;
        }

    }
}
