using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.Samples.GestureSample;

public class DetectGesture : MonoBehaviour
{
    [SerializeField] private XRHandTrackingEvents handTrackingEvents;
    [SerializeField] private XRHandShape[] handShapes;
    [SerializeField] private float gestureDetectionThreshold = 0.1f;
    [SerializeField] private HandShapeCompletenessCalculator completenessCalculator; 

    private float timeOfLastConditionCheck;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable() {
        if (handTrackingEvents != null)
        {
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
        }
    }
    void OnDisable() {
        if (handTrackingEvents != null)
        {
            handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
        }
    }

    // Update is called once per frame
    void OnJointsUpdated(XRHandJointsUpdatedEventArgs eventArgs)
    {
        if(Time.time - timeOfLastConditionCheck < gestureDetectionThreshold)
            return;

    foreach (var handShape in handShapes)
    {
        completenessCalculator.TryCalculateHandShapeCompletenessScore(eventArgs.hand, 
            handShape, out float completenessScore);

            var detected = 
                handTrackingEvents.handIsTracked && completenessScore >= gestureDetectionThreshold;

            if (detected && completenessScore >= 0.9f) // You can adjust this threshold based on your needs
            {
                Debug.Log($"Hand Gesture Detected: {handShape.name} | Score: {completenessScore}");
            }
        }
        timeOfLastConditionCheck = Time.time;
    }
}
