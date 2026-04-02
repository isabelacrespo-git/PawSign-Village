using UnityEngine;

public class AnchorGate : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    public bool IsPlayerOnAnchor { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            IsPlayerOnAnchor = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            IsPlayerOnAnchor = false;
        }
    }
}
