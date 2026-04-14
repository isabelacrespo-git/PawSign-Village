using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameMenu : MonoBehaviour
{
    [Header("UI Pages")]
    public GameObject mainMenu;
    public GameObject options;

    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button startMenuButton;
    public Button optionButton;
    public Button quitButton;
    public Button backButton;

    // Start is called before the first frame update
    void Start()
    {
        EnableMainMenu();

        //Hook events
        if (startButton) {
            startButton.gameObject.SetActive(true);
            startButton.onClick.AddListener(StartGame);
        }
        if (startMenuButton) {
            startMenuButton.gameObject.SetActive(true);
            startMenuButton.onClick.AddListener(StartMenu);
        }
        optionButton.onClick.AddListener(EnableOption);
        quitButton.onClick.AddListener(QuitGame);
        backButton.onClick.AddListener(EnableMainMenu);
    }

    public void QuitGame()
    {
        Application.Quit();
        // This line tells the Unity Editor to stop playing
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void StartGame()
    {
        HideAll();
        SceneTransitionManager.singleton.GoToSceneAsync(1);
    }

    public void StartMenu()
    {
        HideAll();
        Time.timeScale = 1f;
        SceneTransitionManager.singleton.GoToSceneAsync(0);
    }

    public void HideAll()
    {
        mainMenu.SetActive(false);
        options.SetActive(false);
    }

    public void EnableMainMenu()
    {
        mainMenu.SetActive(true);
        options.SetActive(false);
    }
    public void EnableOption()
    {
        mainMenu.SetActive(false);
        options.SetActive(true);
    }
    public void DisableMainMenu()
    {
        mainMenu.SetActive(false);
        options.SetActive(false);
    }
}
