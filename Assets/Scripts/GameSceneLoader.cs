using UnityEngine;

public class GameSceneLoader : MonoBehaviour
{
    //separate script to load save file when main scene is loaded
    private SaveSystem saveSystem = new SaveSystem();
    void Start()
    {
        saveSystem.Load();
    }
}
