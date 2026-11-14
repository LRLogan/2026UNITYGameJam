using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class UIHelpers
{
    /// <summary>Creates a reusable overlay canvas without prescribing its contents.</summary>
    public static RectTransform CreateOverlayCanvas(string name, Transform parent, Vector2 resolution,
        UnityEngine.UI.CanvasScaler.ScreenMatchMode matchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand,
        int sortingOrder = 0, float matchWidthOrHeight = 0.5f)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        root.transform.SetParent(parent, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = resolution;
        scaler.screenMatchMode = matchMode;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
        return (RectTransform)root.transform;
    }

    public static EventSystem EnsureEventSystem(string name, Transform parent)
    {
        EventSystem existing = EventSystem.current != null ? EventSystem.current
            : Object.FindFirstObjectByType<EventSystem>();
        if (existing != null) return existing;
        var root = new GameObject(name, typeof(EventSystem), typeof(InputSystemUIInputModule));
        root.transform.SetParent(parent, false);
        root.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        return root.GetComponent<EventSystem>();
    }

    /// <summary>Allow the final UI frame to render before stopping the player.</summary>
    public static System.Collections.IEnumerator QuitAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        Application.Quit();
    }

    public static string ControlsText => "Move  ·  WASD / Arrow keys\nLook  ·  Mouse\nSprint  ·  Left Shift\nJump / Wall jump  ·  Space" +
        "\nWall climb / Descend  ·  Z / X\nShoot  ·  Left mouse button\nReload  ·  R\nSpin revolver  ·  T\nCollect sword  ·  E" +
        "\n\nYou can begin wall running by running twards a wall and jumping twards it at an acute angle.";

    public static readonly Color Ivory = new Color(0.96f, 0.94f, 0.86f);
    public static readonly Color Gold = new Color(0.88f, 0.72f, 0.43f);

    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static void Position(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public static TextMeshProUGUI Text(Transform parent, string value, int size, Color color,
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

    public static UnityEngine.UI.Button Button(Transform parent, string title, float x, float y,
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
    public static void Position(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0.08f, 1);
        rect.anchorMax = new Vector2(0.92f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -top);
        rect.sizeDelta = new Vector2(0, height);
    }

    public static void Label(Transform parent, string text, int size, float top, float height, bool gold = false)
    {
        var label = Rect("Label", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
        Position(label.rectTransform, top, height);
        label.text = text;
        label.fontSize = size;
        label.color = gold ? Gold : Ivory;
        label.raycastTarget = false;
    }

    public static UnityEngine.UI.Button Button(Transform parent, string text, float top, UnityEngine.Events.UnityAction action)
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
