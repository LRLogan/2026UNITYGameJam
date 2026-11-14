using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Visual-only subscriber. Damage and target selection remain in JackpotEffects.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Revolver))]
public sealed class JackpotShotVisuals : MonoBehaviour
{
    private Revolver weapon;
    private Material beamMaterial;
    private Material particleMaterial;
    private readonly List<GameObject> active = new List<GameObject>();
    // Keep cosmetic randomness independent of the gameplay effect roll.
    private readonly System.Random random = new System.Random();

    private void OnEnable()
    {
        weapon = GetComponent<Revolver>();
        weapon.JackpotEffectResolved += Play;
    }

    private void Play(JackpotEffect effect, IReadOnlyList<Vector3> points)
    {
        if (points == null || points.Count == 0) return;
        if (beamMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("JackpotGlow");
            if (shader == null) { Debug.LogError("Missing JackpotGlow shader.", this); return; }
            beamMaterial = new Material(shader);
            particleMaterial = new Material(shader);
            particleMaterial.SetFloat("_Round", 1);
        }
        // Bound concurrent particle/renderer counts even during rapid firing.
        if (active.Count >= 12) { Destroy(active[0]); active.RemoveAt(0); }
        var root = new GameObject("Jackpot " + effect);
        active.Add(root);
        root.transform.position = points[0];
        if (effect == JackpotEffect.Explosion) StartCoroutine(Explosion(root, points[0], weapon.ExplosionRadius));
        else
        {
            var path = new Vector3[points.Count + 1];
            path[0] = weapon.MuzzlePosition;
            for (int i = 0; i < points.Count; i++) path[i + 1] = points[i];
            StartCoroutine(Lightning(root, path));
        }
    }

    private LineRenderer Line(GameObject root, string name, float width, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = beamMaterial;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private IEnumerator Lightning(GameObject root, Vector3[] path)
    {
        var lines = new LineRenderer[path.Length - 1];
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = Line(root, "Chain Arc", 0.13f, new Color(0.35f, 0.75f, 1f));
            if (weapon.LightningImpactPrefab != null)
            {
                GameObject zap = Instantiate(weapon.LightningImpactPrefab, path[i + 1], Quaternion.identity, root.transform);
                // No real-time lights/shadows required for the bright unlit particle effect.
                foreach (Light light in zap.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (ParticleSystem system in zap.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    main.loop = false;
                    main.maxParticles = Mathf.Min(main.maxParticles, 128);
                    main.useUnscaledTime = false;
                    var lights = system.lights;
                    lights.enabled = false;
                }
                foreach (ParticleSystem system in zap.GetComponentsInChildren<ParticleSystem>()) system.Play(false);
            }
        }
        float elapsed = 0;
        float nextJitter = 0;
        while (root != null && elapsed < 0.35f)
        {
            if (elapsed >= nextJitter)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    Vector3 from = path[i], to = path[i + 1];
                    Vector3 axis = (to - from).normalized;
                    Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 up = Vector3.Cross(axis, side);
                    lines[i].positionCount = 13;
                    for (int j = 0; j < 13; j++)
                    {
                        float t = j / 12f;
                        float jitter = Mathf.Sin(t * Mathf.PI) * Mathf.Min(0.35f, Vector3.Distance(from, to) * 0.1f);
                        lines[i].SetPosition(j, Vector3.Lerp(from, to, t) + jitter *
                            (side * (float)(random.NextDouble() * 2 - 1) + up * (float)(random.NextDouble() * 2 - 1)));
                    }
                }
                nextJitter = elapsed + 0.045f;
            }
            foreach (LineRenderer line in lines)
                line.startColor = line.endColor = new Color(0.35f, 0.75f, 1f, 1f - elapsed / 0.35f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        if (root != null)
        {
            foreach (LineRenderer line in lines) line.enabled = false;
            yield return new WaitForSeconds(2f);
        }
        Finish(root);
    }

    private IEnumerator Explosion(GameObject root, Vector3 origin, float radius)
    {
        var particles = root.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius, radius * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 0.28f, 0.03f), new Color(1, 0.85f, 0.3f));
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, 0.2f, 0.02f), 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
        particles.Emit(32);
        var ring = Line(root, "Blast Radius", 0.18f, new Color(1, 0.55f, 0.1f));
        ring.loop = true;
        ring.positionCount = 48;
        float elapsed = 0;
        while (root != null && elapsed < 0.65f)
        {
            float t = elapsed / 0.65f;
            float r = radius * (1 - (1 - t) * (1 - t));
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2 / 48;
                ring.SetPosition(i, origin + new Vector3(Mathf.Cos(angle) * r, 0.05f, Mathf.Sin(angle) * r));
            }
            ring.startColor = ring.endColor = new Color(1, 0.55f, 0.1f, 1 - t);
            yield return null;
            elapsed += Time.deltaTime;
        }
        Finish(root);
    }

    private void Finish(GameObject root)
    {
        active.Remove(root);
        if (root != null) Destroy(root);
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.JackpotEffectResolved -= Play;
        StopAllCoroutines();
        foreach (GameObject root in active) if (root != null) Destroy(root);
        active.Clear();
    }

    private void OnDestroy()
    {
        if (beamMaterial != null) Destroy(beamMaterial);
        if (particleMaterial != null) Destroy(particleMaterial);
    }
}
