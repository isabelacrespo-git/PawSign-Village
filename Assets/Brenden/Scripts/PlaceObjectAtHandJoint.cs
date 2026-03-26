using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class PlaceObjectAtHandJoint : MonoBehaviour
{
    [SerializeField] private GameObject placementPrefab;
    [SerializeField] private Handedness handedness = Handedness.Right;
    [SerializeField] private XRHandJointID jointId = XRHandJointID.IndexTip;
    [SerializeField] private Transform placementParent;

    private XRHandSubsystem handSubsystem;
    private GameObject placementInstance;

    private void OnEnable() => StartCoroutine(WaitForHandSubsystem());

    IEnumerator WaitForHandSubsystem()
    {
        List<XRHandSubsystem> subsystems = new();
        while (handSubsystem == null)
        {
            subsystems.Clear();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var sub in subsystems)
            {
                if (sub != null && sub.running)
                {
                    handSubsystem = sub;
                    break;
                }
            }
            yield return null;
        }
    }
    
    void Update()
    {
        if (handSubsystem == null) return;
        if (!handSubsystem.running)
        {
            handSubsystem = null;
            return;
        }
        
        XRHand hand = (handedness == Handedness.Left) ? handSubsystem.leftHand : handSubsystem.rightHand;
        if (!hand.isTracked) return;

        XRHandJoint joint = hand.GetJoint(jointId);
        if (!joint.TryGetPose(out Pose pose)) return;

        if (placementInstance == null)
        {
            if (placementPrefab == null)
            {
                Debug.LogError("PlaceObjectAtHandJoint needs a placement prefab assigned. Disabling component so it won't spam Instantiate.", this);
                enabled = false;
                return;
            }

            placementInstance = Instantiate(placementPrefab, placementParent);
        }

        placementInstance.transform.position = pose.position;
        placementInstance.transform.rotation = pose.rotation;
    }
}
