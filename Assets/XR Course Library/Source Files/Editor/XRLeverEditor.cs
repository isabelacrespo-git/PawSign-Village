using UnityEditor;


[CustomEditor(typeof(XRLever))]
public class XRLeverEditor : UnityEditor.XR.Interaction.Toolkit.Interactables.XRBaseInteractableEditor
{
    private SerializedProperty handle = null;
    private SerializedProperty defaultValue = null;

    private SerializedProperty onLeverActivate = null;
    private SerializedProperty onLeverDeactivate = null;

    protected override void OnEnable()
    {
        base.OnEnable();

        handle = serializedObject.FindProperty("handle");
        defaultValue = serializedObject.FindProperty("defaultValue");

        onLeverActivate = serializedObject.FindProperty("OnLeverActivate");
        onLeverDeactivate = serializedObject.FindProperty("OnLeverDeactivate");
    }

    protected override void DrawCoreConfiguration()
    {
        base.DrawCoreConfiguration();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lever Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(handle);
        EditorGUILayout.PropertyField(defaultValue);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lever Event", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(onLeverActivate);
        EditorGUILayout.PropertyField(onLeverDeactivate);
    }
}
