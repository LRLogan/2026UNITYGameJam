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
    private static readonly Color Ivory = new Color(0.96f, 0.94f, 0.86f);
    private static readonly Color Gold = new Color(0.88f, 0.72f, 0.43f);

    private void Start()
    {
        Time.timeScale = 1;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("Menu Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        var canvasObject = new GameObject("Menu Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1340, 694);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        RectTransform root = (RectTransform)canvasObject.transform;
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
        playButton = Button(home.transform, "PLAY", 55, -374, Play, true);
        Button(home.transform, "CONTROLS", 55, -449, ShowControls);
        Button(home.transform, "EXIT", 55, -524, Exit);
        status = Text(home.transform, "", 16, Ivory, 55, -610, 390, 60);
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
        StartCoroutine(QuitWebPlayer());
#else
        Application.Quit();
#endif
    }

    private System.Collections.IEnumerator QuitWebPlayer()
    {
        // Present the final screen before stopping the Web player; browsers keep the tab open.
        yield return new WaitForEndOfFrame();
        Application.Quit();
    }

    private void OpenDialog(string title, string body, bool showBack = true)
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
            var back = Button(panel, "BACK TO MENU", 35, -520, CloseDialog, true);
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

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void Position(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static TextMeshProUGUI Text(Transform parent, string value, int size, Color color,
        float x, float y, float width, float height, FontStyles style = FontStyles.Normal)
    {
        var label = Rect("Label", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
        Position(label.rectTransform, x, y, width, height);
        label.text = value;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static UnityEngine.UI.Button Button(Transform parent, string title, float x, float y,
        UnityEngine.Events.UnityAction action, bool primary = false)
    {
        var rect = Rect(title, parent, Vector2.zero, Vector2.one);
        Position(rect, x, y, 330, 58);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.white;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        var colors = button.colors;
        colors.normalColor = primary ? Gold : new Color(0.12f, 0.17f, 0.17f, 0.95f);
        colors.highlightedColor = primary ? new Color(1, 0.86f, 0.58f) : new Color(0.27f, 0.34f, 0.32f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.53f, 0.43f, 0.27f);
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        Text(rect, title, 23, primary ? new Color(0.08f, 0.12f, 0.12f) : Ivory, 24, -13, 290, 38, FontStyles.Bold);
        return button;
    }
}
