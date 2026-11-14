using static UIHelpers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public sealed class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Texture2D background;
    [SerializeField] private Material blurPanelMaterial;
    [SerializeField] private string gameplayScene = "Level-1";
    private GameObject home;
    private GameObject dialog;
    private UnityEngine.UI.Button playButton;
    private TextMeshProUGUI status;
    private bool loading;
    private bool exiting;

    private void Start()
    {
        Time.timeScale = 1;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem("Menu Event System", transform);
        RectTransform root = CreateOverlayCanvas("Menu Canvas", transform, new Vector2(1340, 694));
        var image = Rect("Scenery", root, Vector2.zero, Vector2.one).gameObject.AddComponent<UnityEngine.UI.RawImage>();
        image.texture = background;
        image.raycastTarget = false;
        var blur = Rect("Diagonal Frosted Panel", root, Vector2.zero, Vector2.one).gameObject.AddComponent<UnityEngine.UI.RawImage>();
        blur.texture = background;
        blur.material = blurPanelMaterial;
        blur.raycastTarget = false;
        home = Rect("Home", root, new Vector2(0, 0), new Vector2(0.38f, 1)).gameObject;
        Text(home.transform, "A LITTLE LUCK. A PERFECT SHOT.", 15, Gold, 55, -66, 390, 30);
        Text(home.transform, "JACKPOT\nSHOT", 70, Ivory, 52, -115, 400, 180, FontStyles.Bold);
        Text(home.transform, "Find the swords. Make every shot count.", 18, Ivory, 55, -297, 390, 38);
        playButton = Button(home.transform, "PLAY", 55, -345, Play, true);
        Button(home.transform, "CONTROLS", 55, -415, ShowControls);
        Button(home.transform, "CREDITS", 55, -485, ShowCredits);
        Button(home.transform, "EXIT", 55, -555, Exit);
        status = Text(home.transform, "", 16, Ivory, 55, -625, 390, 60);
        EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    private void Update()
    {
        if (!exiting && dialog != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseDialog();
    }

    private void Play()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
        { status.text = "Level-1 is missing from the build scene list."; return; }
        loading = true;
        playButton.interactable = false;
        status.text = "Loading your run…";
        Time.timeScale = 1;
        AudioListener.pause = false;
        SceneManager.LoadSceneAsync(gameplayScene);
    }

    private void ShowControls()
    {
        OpenDialog("CONTROLS", "MOVE                         W A S D / Arrow keys\nLOOK                         Mouse\nSPRINT                       Left Shift\nJUMP / WALL JUMP             Space\nWALL CLIMB / DESCEND         Z / X\nSHOOT                        Left mouse button\nRELOAD                       R\nSPIN REVOLVER                T\nCOLLECT SWORD                E\n\nCollect each marked sword, then return to the green\nextraction beacon. Use your lucky shot wisely.");
    }

    private void ShowCredits()
    {
        OpenDialog("CREDITS", "Made by: Logan Larrondo\n\n" +
            "Assets used from the Unity Asset Store:\n" +
            "Human Soldier Animations FREE by Kevin Iglesias\n" +
            "Weapons FREE - Low Poly 3D Models Pack by ithappy\n" +
            "Zap VFX - URP by Vefects\n" +
            "Sprite muzzle flashes by bestgamekits\n" +
            "Low Poly Medieval Starter Pack by Plug & Play\n" +
            "DOTween (HOTween v2) by Demigiant\n" +
            "Low Poly Tree Pack by BROKEN VECTOR\n\n" +
            "Thanks for playing!", backText: "BACK");
    }

    private void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Screen.fullScreen = false;
        exiting = true;
        OpenDialog("THANKS FOR PLAYING", "Your session has ended.\n\nYou can safely close this browser tab.\nRefresh the page to play again.", false);
        StartCoroutine(QuitAfterFrame());
#else
        Application.Quit();
#endif
    }

    private void OpenDialog(string title, string body, bool showBack = true, string backText = "BACK TO MENU")
    {
        home.SetActive(false);
        dialog = Rect("Menu Dialog", home.transform.parent, Vector2.zero, Vector2.one).gameObject;
        var shade = dialog.AddComponent<UnityEngine.UI.Image>();
        shade.color = new Color(0.025f, 0.045f, 0.05f, 0.94f);
        RectTransform panel = Rect("Content", dialog.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.sizeDelta = new Vector2(760, 610);
        Text(panel, title, 42, Gold, 35, -28, 690, 65, FontStyles.Bold);
        Text(panel, body, 22, Ivory, 35, -115, 690, 365);
        if (showBack)
        {
            var back = Button(panel, backText, 35, -520, CloseDialog, true);
            EventSystem.current.SetSelectedGameObject(back.gameObject);
        }
        else EventSystem.current.SetSelectedGameObject(null);
    }

    private void CloseDialog()
    {
        if (dialog == null) return;
        dialog.SetActive(false);
        Destroy(dialog);
        dialog = null;
        home.SetActive(true);
        EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

}
