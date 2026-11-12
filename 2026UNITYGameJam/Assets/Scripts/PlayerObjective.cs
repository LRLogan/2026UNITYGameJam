using TMPro;
using UnityEngine;

public class PlayerObjective : MonoBehaviour
{
    private TextMeshPro prompt;
    private PlayerCore viewer;
    private LevelManager extractionLevel;

    internal void SetPrompt(TextMeshPro label, PlayerCore player)
    {
        prompt = label;
        viewer = player;
        ShowPrompt(false);
    }

    internal void ShowPrompt(bool visible)
    {
        if (prompt != null && prompt.gameObject.activeSelf != visible)
            prompt.gameObject.SetActive(visible);
    }

    internal void EnableExtraction(LevelManager level)
    {
        extractionLevel = level;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (extractionLevel != null)
            extractionLevel.TryExtract(other.GetComponentInParent<PlayerCore>());
    }

    private void LateUpdate()
    {
        if (prompt != null && prompt.gameObject.activeSelf && viewer != null)
            prompt.transform.rotation = viewer.AimTransform.rotation;
    }
}
