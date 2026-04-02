using UnityEngine;

public class SigningModeSwitcher : MonoBehaviour
{
    [Header("Enable During Signing")]
    [SerializeField] private GameObject[] signingObjects;

    [Header("Disable During Signing")]
    [SerializeField] private GameObject[] nonSigningObjects;

    public void EnterSigningMode()
    {
        SetObjectsActive(signingObjects, true);
        SetObjectsActive(nonSigningObjects, false);
    }

    public void ExitSigningMode()
    {
        SetObjectsActive(signingObjects, false);
        SetObjectsActive(nonSigningObjects, true);
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }
}
