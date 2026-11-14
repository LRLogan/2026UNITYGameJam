using static UIHelpers;
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
    private int selectedLuckyNumber;
    private readonly UnityEngine.UI.Button[] luckyButtons = new UnityEngine.UI.Button[6];
    private TextMeshProUGUI luckyStatus;

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
        EnsureEventSystem("Pause Event System", transform);
        overlay = CreateOverlayCanvas("Pause Overlay", transform, new Vector2(1340, 694),
            sortingOrder: 1000).gameObject;
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
            Label(choices.transform, "Welcome to Jackpot Shot! Your fast and with some luck your revolver can pack a serious punch, so get in there have some fun, grab the swords of coolness, and get out of there!", 25, 175, 165);
            luckyStatus = Text(choices.transform, "CHOOSE YOUR LUCKY NUMBER", 19, Gold, 70, -340, 740, 35);
            for (int i = 0; i < luckyButtons.Length; i++)
            {
                int number = i + 1;
                var button = Button(choices.transform, number.ToString(), 380, () => SelectLuckyNumber(number));
                Position((RectTransform)button.transform, 70 + i * 124, -380, 110, 56);
                button.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
                luckyButtons[i] = button;
            }
            resumeButton = Button(choices.transform, "CONTINUE TO LEVEL", 455, BeginChosenRound);
            resumeButton.interactable = false;
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
        Label(controls.transform, UIHelpers.ControlsText, 23, 155, 350);
        Button(controls.transform, "BACK", 550, () => ShowControls(false));
        controls.SetActive(false);
        EventSystem.current.SetSelectedGameObject(showingWelcome ? luckyButtons[0].gameObject : resumeButton.gameObject);
    }

    private void SelectLuckyNumber(int number)
    {
        selectedLuckyNumber = number;
        luckyStatus.text = $"YOUR LUCKY SHOT NUMBER: {number}";
        for (int i = 0; i < luckyButtons.Length; i++)
        {
            var colors = luckyButtons[i].colors;
            colors.normalColor = i + 1 == number ? Gold : new Color(0.12f, 0.18f, 0.18f, 0.95f);
            luckyButtons[i].colors = colors;
            luckyButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = i + 1 == number
                ? new Color(0.08f, 0.12f, 0.12f) : Ivory;
        }
        resumeButton.interactable = true;
    }

    private void BeginChosenRound()
    {
        if (selectedLuckyNumber < 1 || selectedLuckyNumber > 6 || game == null) return;
        game.StartRound(selectedLuckyNumber);
        if (!game.IsRoundActive)
        {
            luckyStatus.text = "Unable to start: check the GameManager's Revolver reference.";
            return;
        }
        SetPaused(false);
    }

    private void ShowControls(bool show)
    {
        choices.SetActive(!show);
        controls.SetActive(show);
        EventSystem.current.SetSelectedGameObject(show
            ? controls.GetComponentInChildren<UnityEngine.UI.Button>().gameObject
            : showingWelcome && selectedLuckyNumber == 0 ? luckyButtons[0].gameObject : resumeButton.gameObject);
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

    internal void DismissForResults()
    {
        if (paused) SetPaused(false);
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

}
