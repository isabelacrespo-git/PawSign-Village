using UnityEditor;


[CustomEditor(typeof(XRButton))]
public class XRButtonEditor : UnityEditor.XR.Interaction.Toolkit.Interactables.XRBaseInteractableEditor
{
    private SerializedProperty buttonTransform = null;
    private SerializedProperty pressDistance = null;

    private SerializedProperty onPress = null;
    private SerializedProperty onRelease = null;

    protected override void OnEnable()
    {
        base.OnEnable();

        buttonTransform = serializedObject.FindProperty("buttonTransform");
        pressDistance = serializedObject.FindProperty("pressDistance");

        onPress = serializedObject.FindProperty("OnPress");
        onRelease = serializedObject.FindProperty("OnRelease");
    }

    protected override void DrawCoreConfiguration()
    {
        base.DrawCoreConfiguration();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Button Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(buttonTransform);
        EditorGUILayout.PropertyField(pressDistance);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Button Events", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(onPress);
        EditorGUILayout.PropertyField(onRelease);
    }
}
