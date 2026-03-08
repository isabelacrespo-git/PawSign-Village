using UnityEngine;

public class TestPoseOverride : MonoBehaviour
{
    [SerializeField] private HandPoseOverride handPoseOverride;
    [SerializeField] private string poseName = "RPose";

    [ContextMenu("Activate Pose")]
    public void TestActivate()
    {
        handPoseOverride?.ActivatePose(poseName);
    }

    [ContextMenu("Deactivate Pose")]
    public void TestDeactivate()
    {
        handPoseOverride?.DeactivatePose();
    }
}
