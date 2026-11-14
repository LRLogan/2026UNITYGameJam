using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

internal sealed class LevelHud
{
    private readonly Transform canvas;
    private readonly TextMeshProUGUI instructions;
    private TextMeshProUGUI loadError;

    internal LevelHud(Transform parent)
    {
        var root = new GameObject("Level HUD", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        root.transform.SetParent(parent, false);
        canvas = root.transform;
        var screen = root.GetComponent<Canvas>();
        screen.renderMode = RenderMode.ScreenSpaceOverlay;
        screen.sortingOrder = 10;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        instructions = CreateLabel(canvas, "Objective Status", new Vector2(0.5f, 1f),
            new Vector2(0f, -48f), new Vector2(650f, 52f), 26f);
    }

    internal void SetInstructions(string message)
    {
        instructions.text = message;
    }

    internal void ShowResult(RoundResult result, Action replay, Action returnToMenu)
    {
        instructions.gameObject.SetActive(false);
        EventSystem events = UIHelpers.EnsureEventSystem("Results Event System", canvas);

        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform panel = CreateRect(canvas, "Level Results", center, Vector2.zero, new Vector2(520f, 340f));
        panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(17, 21, 30, 250);

        TextMeshProUGUI stats = CreateLabel(panel, "Results", center, new Vector2(0f, 52f),
            new Vector2(470f, 200f), 32f);
        TimeSpan time = TimeSpan.FromSeconds(result.ElapsedSeconds);
        stats.text = $"LEVEL COMPLETE\nScore: {result.Score}\nTime: {(int)time.TotalMinutes:00}:{time.Seconds:00}\nHealth: {result.Health} / {result.MaxHealth}";

        UnityEngine.UI.Button replayButton = CreateButton(panel, "Replay Level", -118f, replay);
        CreateButton(panel, "Main Menu", 118f, returnToMenu);
        loadError = CreateLabel(panel, "Load Status", center, new Vector2(0f, -125f),
            new Vector2(470f, 58f), 19f);
        events.SetSelectedGameObject(replayButton.gameObject);
    }

    internal void SetLoadError(string message)
    {
        if (loadError != null) loadError.text = message;
    }

    private static UnityEngine.UI.Button CreateButton(Transform parent, string text, float x, Action action)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform rect = CreateRect(parent, text, center, new Vector2(x, -65f), new Vector2(214f, 48f));
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color32(56, 67, 87, 255);
        colors.highlightedColor = new Color32(80, 95, 121, 255);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(() => action());
        CreateLabel(rect, "Label", center, Vector2.zero, rect.sizeDelta, 24f).text = text;
        return button;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 anchor,
        Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = CreateRect(parent, name, anchor, position, size);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchor,
        Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
