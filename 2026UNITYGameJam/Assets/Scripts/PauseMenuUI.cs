using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>Owns pause state, input suppression and a snapshot-backed menu.</summary>
public sealed class PauseMenuUI : MonoBehaviour
{
    private bool paused;
    private bool exiting;
    private bool showingWelcome;
    private float previousTimeScale;
    private bool previousAudioPause;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private InputActionMap gameplay;
    private GameObject overlay;
    private GameObject choices;
    private GameObject controls;
    private UnityEngine.UI.Button resumeButton;
    private Texture2D snapshot;
    private Material blurMaterial;
    private Coroutine capture;
    private GameManager game;
    private PlayerInput playerInput;

    private void Start()
    {
        game = FindFirstObjectByType<GameManager>();
        PlayerCore player = FindFirstObjectByType<PlayerCore>();
        if (player != null) playerInput = player.GetComponent<PlayerInput>();
        if (game != null && playerInput != null && Time.timeScale > 0)
        {
            showingWelcome = true;
            SetPaused(true);
        }
    }

    private void Update()
    {
        if (exiting) return;
        bool toggle = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        toggle |= Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        if (!toggle) return;
        if (paused)
        {
            if (controls != null && controls.activeSelf) ShowControls(false);
            else if (!showingWelcome) SetPaused(false);
        }
        // Do not take ownership of the results screen's pause state.
        else if (Time.timeScale > 0 && game != null && game.IsRoundActive) SetPaused(true);
    }

    private void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (value)
        {
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            gameplay = playerInput != null && playerInput.currentActionMap != null
                && playerInput.currentActionMap.enabled ? playerInput.currentActionMap : null;
            gameplay?.Disable();
            Time.timeScale = 0;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            capture = StartCoroutine(CaptureAndShow());
        }
        else
        {
            if (capture != null) StopCoroutine(capture);
            capture = null;
            if (overlay != null) overlay.SetActive(false);
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            gameplay?.Enable();
            gameplay = null;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            ReleaseVisuals();
            showingWelcome = false;
        }
    }

    private IEnumerator CaptureAndShow()
    {
        // End-of-frame still runs at timeScale zero. Capture before creating the menu.
        yield return new WaitForEndOfFrame();
        snapshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (snapshot != null) snapshot.wrapMode = TextureWrapMode.Clamp;
        BuildMenu();
        capture = null;
    }

    private void BuildMenu()
    {
        if (EventSystem.current == null)
        {
            var events = new GameObject("Pause Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        overlay = new GameObject("Pause Overlay", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1340, 694);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        // Invisible full-screen hit target prevents clicking through to gameplay HUD.
        var blocker = Rect("Input Blocker", overlay.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<UnityEngine.UI.Image>();
        blocker.color = Color.clear;
        var left = Rect(showingWelcome ? "Full Screen Welcome" : "Left Half", overlay.transform,
            Vector2.zero, new Vector2(showingWelcome ? 1f : 0.5f, 1));
        var image = left.gameObject.AddComponent<UnityEngine.UI.RawImage>();
        image.texture = snapshot;
        image.uvRect = new Rect(0, 0, showingWelcome ? 1f : 0.5f, 1);
        Shader shader = Resources.Load<Shader>("PauseMenuBlur");
        if (shader != null)
        {
            blurMaterial = new Material(shader);
            image.material = blurMaterial;
        }
        else image.color = new Color(0.15f, 0.2f, 0.2f, 0.98f);

        Transform content = left;
        if (showingWelcome)
        {
            var centered = Rect("Welcome Content", left, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            centered.sizeDelta = new Vector2(880, 650);
            content = centered;
        }
        choices = Rect("Pause Actions", content, Vector2.zero, Vector2.one).gameObject;
        if (showingWelcome)
        {
            Label(choices.transform, "YOUR MISSION", 18, 42, 30, true);
            Label(choices.transform, "JACKPOT SHOT", 48, 92, 75);
            Label(choices.transform, "Welcome to Jackpot Shot! Your fast and with some luck your revolver can pack a serious punch, so get in there have some fun, grab the swords of coolness, and get out of there!", 29, 195, 245);
            resumeButton = Button(choices.transform, "CONTINUE TO LEVEL", 455, () => SetPaused(false));
            Button(choices.transform, "VIEW CONTROLS", 535, () => ShowControls(true));
        }
        else
        {
        Label(choices.transform, "JACKPOT SHOT", 18, 62, 25, true);
        Label(choices.transform, "PAUSED", 54, 118, 85);
        resumeButton = Button(choices.transform, "RESUME", 270, () => SetPaused(false));
        Button(choices.transform, "CONTROLS", 350, () => ShowControls(true));
        Button(choices.transform, "QUIT GAME", 430, Quit);
        Label(choices.transform, "ESC / START TO RESUME", 16, 555, 35, true);
        }
        controls = Rect("Controls", content, Vector2.zero, Vector2.one).gameObject;
        Label(controls.transform, "CONTROLS", 38, 60, 60);
        Label(controls.transform, "Move  ·  WASD / Arrow keys\nLook  ·  Mouse\nSprint  ·  Left Shift\nJump / Wall jump  ·  Space\nWall climb / Descend  ·  Z / X\nShoot  ·  Left mouse button\nReload  ·  R\nSpin revolver  ·  T\nCollect sword  ·  E", 23, 155, 350);
        Button(controls.transform, "BACK", 550, () => ShowControls(false));
        controls.SetActive(false);
        EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private void ShowControls(bool show)
    {
        choices.SetActive(!show);
        controls.SetActive(show);
        EventSystem.current.SetSelectedGameObject(show
            ? controls.GetComponentInChildren<UnityEngine.UI.Button>().gameObject : resumeButton.gameObject);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        exiting = true;
        choices.SetActive(false);
        controls.SetActive(false);
        Label(choices.transform.parent, "THANKS FOR PLAYING", 32, 140, 90);
        Label(choices.transform.parent, "You can close this browser tab.\nRefresh the page to play again.", 22, 255, 120);
        EventSystem.current.SetSelectedGameObject(null);
        Screen.fullScreen = false;
        StartCoroutine(QuitAfterFrame());
#else
        Application.Quit();
#endif
    }

    private IEnumerator QuitAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        Application.Quit();
    }

    private void OnDisable()
    {
        if (paused) SetPaused(false);
    }

    private void ReleaseVisuals()
    {
        if (overlay != null) Destroy(overlay);
        if (snapshot != null) Destroy(snapshot);
        if (blurMaterial != null) Destroy(blurMaterial);
        overlay = null;
        snapshot = null;
        blurMaterial = null;
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

    private static void Position(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0.08f, 1);
        rect.anchorMax = new Vector2(0.92f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -top);
        rect.sizeDelta = new Vector2(0, height);
    }

    private static void Label(Transform parent, string text, int size, float top, float height, bool gold = false)
    {
        var label = Rect("Label", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
        Position(label.rectTransform, top, height);
        label.text = text;
        label.fontSize = size;
        label.color = gold ? new Color(0.88f, 0.72f, 0.43f) : new Color(0.96f, 0.94f, 0.86f);
        label.raycastTarget = false;
    }

    private static UnityEngine.UI.Button Button(Transform parent, string text, float top, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(text, parent, Vector2.zero, Vector2.one);
        Position(rect, top, 60);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.18f, 0.18f, 0.95f);
        colors.highlightedColor = colors.selectedColor = new Color(0.38f, 0.35f, 0.24f);
        colors.pressedColor = new Color(0.24f, 0.22f, 0.16f);
        button.colors = colors;
        button.onClick.AddListener(action);
        Label(rect, text, 23, 14, 40);
        return button;
    }
}
