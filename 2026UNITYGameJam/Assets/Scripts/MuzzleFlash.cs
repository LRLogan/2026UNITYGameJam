
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MuzzleFlash : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 24f;

    [Header("Visibility")]
    [SerializeField] private bool faceCamera;
    [SerializeField] private Camera viewingCamera;

    private SpriteRenderer spriteRenderer;
    private Coroutine animationRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
    }

    public void Play()
    {
        if (!isActiveAndEnabled || frames == null || frames.Length == 0)
            return;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        FaceViewingCamera();

        // Restart if another shot occurs during the animation.
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(AnimateFlash());
    }

    public void SetCameraFacing(bool value)
    {
        faceCamera = value;
    }

    private void LateUpdate()
    {
        // Run after the weapon's animated hand has moved. Keep the muzzle position intact.
        if (spriteRenderer != null && spriteRenderer.enabled) FaceViewingCamera();
    }

    private void FaceViewingCamera()
    {
        if (!faceCamera) return;
        if (viewingCamera == null || !viewingCamera.isActiveAndEnabled) viewingCamera = Camera.main;
        if (viewingCamera == null) return;

        Vector3 forward = viewingCamera.orthographic ? viewingCamera.transform.forward
            : transform.position - viewingCamera.transform.position;
        if (forward.sqrMagnitude < 0.000001f) return;
        // Sprite front faces local -Z; point it toward the camera, including its elevation.
        transform.rotation = Quaternion.LookRotation(forward, viewingCamera.transform.up);
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
