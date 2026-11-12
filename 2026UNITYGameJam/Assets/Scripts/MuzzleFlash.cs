
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MuzzleFlash : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 24f;

    private SpriteRenderer spriteRenderer;
    private Coroutine animationRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0)
            return;

        // Restart if another shot occurs during the animation.
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(AnimateFlash());
    }

    private IEnumerator AnimateFlash()
    {
        spriteRenderer.enabled = true;

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);

        foreach (Sprite frame in frames)
        {
            spriteRenderer.sprite = frame;
            yield return new WaitForSeconds(frameDuration);
        }

        spriteRenderer.enabled = false;
        animationRoutine = null;
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }
}
