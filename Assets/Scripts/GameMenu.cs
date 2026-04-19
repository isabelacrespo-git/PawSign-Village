using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameMenu : MonoBehaviour
{
    [Header("UI Pages")]
    public GameObject mainMenu;
    public GameObject options;
    public GameObject controls;

    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button startMenuButton;
    public Button optionButton;
    public Button controlsButton;
    public Button quitButton;
    public Button backButton1;
    public Button backButton2;

    // Start is called before the first frame update
    private SaveSystem saveSystem = new SaveSystem();
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
            //saves player position when clicked
            startMenuButton.onClick.AddListener(() => saveSystem.Save());
        }
        optionButton.onClick.AddListener(EnableOption);
        controlsButton.onClick.AddListener(EnableControl);
        quitButton.onClick.AddListener(QuitGame);
        backButton1.onClick.AddListener(EnableMainMenu);
        backButton2.onClick.AddListener(EnableMainMenu);
    }

    public void QuitGame()
    {
        //saves player position before game closes
        saveSystem.Save();
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
        controls.SetActive(false);
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

    public void EnableControl() 
    {
        mainMenu.SetActive(false);
        controls.SetActive(true);
    }
}
