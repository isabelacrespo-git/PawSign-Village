using UnityEngine;

public class DayandNightCycle : MonoBehaviour
{
    [Header("Material Setup")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private string textureProperty = "_MainTex";

    [Header("Cycle")]
    [SerializeField] private Vector2 dayOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 nightOffset = new Vector2(1f, 0f);
    [SerializeField, Min(0.1f)] private float cycleDurationSeconds = 30f;
    [SerializeField, Range(0f, 1f)] private float timeOfDayOffset = 0f;
    [SerializeField] private bool syncTextureWithSun = true;
    [SerializeField] private bool pingPong = true;
    [SerializeField] private bool startAtRandomPoint = true;

    [Header("Lighting")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private Color dayLightColor = Color.white;
    [SerializeField] private Color nightLightColor = new Color(0.2f, 0.25f, 0.35f);
    [SerializeField, Min(0f)] private float dayLightIntensity = 1.1f;
    [SerializeField, Min(0f)] private float nightLightIntensity = 0.05f;

    [Header("Sun And Moon")]
    [SerializeField] private Transform sunTransform;
    [SerializeField] private Transform moonTransform;
    [SerializeField] private Transform skyCenter;
    [SerializeField] private bool followSkydomeTransform = true;
    [SerializeField] private bool useSkydomeLocalAxes = true;
    [SerializeField] private bool useSkydomeBoundsForOrbit = true;
    [SerializeField, Min(0.1f)] private float skydomeRadiusMultiplier = 0.45f;
    [SerializeField, Min(0.1f)] private float celestialRadius = 200f;
    [SerializeField] private float orbitHeightOffset = 100f;
    [SerializeField] private Vector3 orbitAxis = Vector3.forward;
    [SerializeField] private Vector3 eastDirection = Vector3.right;
    [SerializeField] private bool faceCelestialsToSkyCenter = false;
    [SerializeField] private Transform celestialLookTarget;
    [SerializeField] private bool flipCelestialFacing = true;
    [SerializeField] private bool hideCelestialsBelowHorizon = true;
    [SerializeField, Range(-0.25f, 0.25f)] private float horizonPadding = 0f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawOrbitGizmo = true;
    [SerializeField, Range(8, 128)] private int orbitGizmoSegments = 64;
    [SerializeField, Min(0.05f)] private float orbitMarkerSize = 5f;
    [SerializeField] private Color orbitGizmoColor = new Color(1f, 0.8f, 0.2f, 0.7f);
    [SerializeField] private Color sunGizmoColor = new Color(1f, 0.5f, 0.1f, 1f);
    [SerializeField] private Color moonGizmoColor = new Color(0.65f, 0.75f, 1f, 1f);

    private Material _runtimeMaterial;
    private Renderer[] _sunRenderers;
    private Renderer[] _moonRenderers;
    private float _elapsed;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer != null)
        {
            _runtimeMaterial = targetRenderer.material;
        }

        if (sunTransform != null)
        {
            _sunRenderers = sunTransform.GetComponentsInChildren<Renderer>(true);
        }

        if (moonTransform != null)
        {
            _moonRenderers = moonTransform.GetComponentsInChildren<Renderer>(true);
        }

        if (startAtRandomPoint)
        {
            _elapsed = Random.Range(0f, cycleDurationSeconds);
        }
    }

    private void Update()
    {
        if (_runtimeMaterial == null && directionalLight == null && sunTransform == null && moonTransform == null)
        {
            return;
        }

        _elapsed += Time.deltaTime;

        float cycleProgress = Mathf.Repeat((_elapsed / cycleDurationSeconds) + timeOfDayOffset, 1f);

        Vector3 sunDirection = GetSunDirection(cycleProgress);
        Vector3 upDirection = GetUpDirection();
        float daylightAmount = GetDaylightAmount(sunDirection, upDirection);

        float textureProgress;
        if (syncTextureWithSun)
        {
            // Keep texture day/night in phase with the sun's height.
            textureProgress = 1f - daylightAmount;
        }
        else
        {
            textureProgress = pingPong ? Mathf.PingPong(cycleProgress * 2f, 1f) : Mathf.Repeat(cycleProgress, 1f);
        }

        if (_runtimeMaterial != null)
        {
            Vector2 currentOffset = Vector2.Lerp(dayOffset, nightOffset, textureProgress);
            _runtimeMaterial.SetTextureOffset(textureProperty, currentOffset);
        }

        UpdateCelestials(sunDirection, upDirection);
        UpdateDirectionalLight(sunDirection, upDirection, daylightAmount);
    }

    private Vector3 GetSunDirection(float cycleProgress)
    {
        GetOrbitAxes(out Vector3 axis, out Vector3 sunriseDirection);
        float angle = cycleProgress * 360f;

        return Quaternion.AngleAxis(angle, axis) * sunriseDirection;
    }

    private void GetOrbitAxes(out Vector3 axis, out Vector3 sunriseDirection)
    {
        Transform basis = GetOrbitBasisTransform();
        Vector3 fallbackUp = GetUpDirection();

        axis = orbitAxis.sqrMagnitude > 0.0001f ? orbitAxis.normalized : Vector3.forward;
        sunriseDirection = eastDirection.sqrMagnitude > 0.0001f ? eastDirection.normalized : Vector3.right;

        if (basis != null && useSkydomeLocalAxes)
        {
            axis = basis.TransformDirection(axis).normalized;
            sunriseDirection = basis.TransformDirection(sunriseDirection).normalized;
        }

        sunriseDirection = Vector3.ProjectOnPlane(sunriseDirection, axis);
        if (sunriseDirection.sqrMagnitude <= 0.0001f)
        {
            sunriseDirection = Vector3.Cross(axis, fallbackUp);
            if (sunriseDirection.sqrMagnitude <= 0.0001f)
            {
                sunriseDirection = Vector3.right;
            }
        }

        sunriseDirection.Normalize();
    }

    private static float GetDaylightAmount(Vector3 sunDirection, Vector3 upDirection)
    {
        float sunHeight = Vector3.Dot(sunDirection, upDirection);
        return Mathf.Clamp01((sunHeight + 0.1f) / 0.7f);
    }

    private void UpdateCelestials(Vector3 sunDirection, Vector3 upDirection)
    {
        Vector3 center = GetOrbitCenter();
        float orbitRadius = GetOrbitRadius();

        Vector3 moonDirection = -sunDirection;
        bool sunAboveHorizon = Vector3.Dot(sunDirection, upDirection) > horizonPadding;
        bool moonAboveHorizon = Vector3.Dot(moonDirection, upDirection) > horizonPadding;

        if (sunTransform != null)
        {
            sunTransform.position = center + (sunDirection * orbitRadius);
            if (faceCelestialsToSkyCenter)
            {
                sunTransform.LookAt(GetCelestialLookPoint(center), upDirection);
                if (flipCelestialFacing)
                {
                    sunTransform.Rotate(0f, 180f, 0f, Space.Self);
                }
            }

            SetRenderersVisible(_sunRenderers, !hideCelestialsBelowHorizon || sunAboveHorizon);
        }

        if (moonTransform != null)
        {
            moonTransform.position = center + (moonDirection * orbitRadius);
            if (faceCelestialsToSkyCenter)
            {
                moonTransform.LookAt(GetCelestialLookPoint(center), upDirection);
                if (flipCelestialFacing)
                {
                    moonTransform.Rotate(0f, 180f, 0f, Space.Self);
                }
            }

            SetRenderersVisible(_moonRenderers, !hideCelestialsBelowHorizon || moonAboveHorizon);
        }

        if (directionalLight != null)
        {
            directionalLight.transform.rotation = Quaternion.LookRotation(-sunDirection, upDirection);
        }
    }

    private void UpdateDirectionalLight(Vector3 sunDirection, Vector3 upDirection, float daylightAmount)
    {
        if (directionalLight == null)
        {
            return;
        }

        directionalLight.transform.rotation = Quaternion.LookRotation(-sunDirection, upDirection);

        directionalLight.color = Color.Lerp(nightLightColor, dayLightColor, daylightAmount);
        directionalLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, daylightAmount);
    }

    private static void SetRenderersVisible(Renderer[] renderers, bool isVisible)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = isVisible;
            }
        }
    }

    private Vector3 GetOrbitCenter()
    {
        Transform basis = GetOrbitBasisTransform();
        Vector3 basePosition = basis != null ? basis.position : transform.position;

        if (useSkydomeBoundsForOrbit && targetRenderer != null)
        {
            basePosition = targetRenderer.bounds.center;
        }

        return basePosition + (GetUpDirection() * orbitHeightOffset);
    }

    private float GetOrbitRadius()
    {
        if (useSkydomeBoundsForOrbit && targetRenderer != null)
        {
            float autoRadius = targetRenderer.bounds.extents.magnitude * skydomeRadiusMultiplier;
            return Mathf.Max(0.1f, autoRadius);
        }

        return celestialRadius;
    }

    private Vector3 GetCelestialLookPoint(Vector3 fallbackCenter)
    {
        if (celestialLookTarget != null)
        {
            return celestialLookTarget.position;
        }

        return fallbackCenter;
    }

    private Vector3 GetUpDirection()
    {
        Transform basis = GetOrbitBasisTransform();
        if (basis != null && useSkydomeLocalAxes)
        {
            return basis.up;
        }

        return Vector3.up;
    }

    private Transform GetOrbitBasisTransform()
    {
        if (followSkydomeTransform && targetRenderer != null)
        {
            return targetRenderer.transform;
        }

        if (skyCenter != null)
        {
            return skyCenter;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOrbitGizmo)
        {
            return;
        }

        float radius = GetOrbitRadius();
        if (radius <= 0f)
        {
            return;
        }

        Vector3 center = GetOrbitCenter();
        GetOrbitAxes(out Vector3 axis, out Vector3 sunriseDirection);

        int segments = Mathf.Max(8, orbitGizmoSegments);
        Vector3 prevPoint = center + (sunriseDirection * radius);

        Gizmos.color = orbitGizmoColor;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 nextDir = Quaternion.AngleAxis(t * 360f, axis) * sunriseDirection;
            Vector3 nextPoint = center + (nextDir * radius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        float markerSize = Mathf.Max(0.05f, orbitMarkerSize);
        float previewProgress = Application.isPlaying
            ? Mathf.Repeat((_elapsed / Mathf.Max(0.1f, cycleDurationSeconds)) + timeOfDayOffset, 1f)
            : Mathf.Repeat(timeOfDayOffset, 1f);

        Vector3 sunDir = GetSunDirection(previewProgress);
        Vector3 moonDir = -sunDir;

        Gizmos.color = sunGizmoColor;
        Gizmos.DrawSphere(center + (sunDir * radius), markerSize);

        Gizmos.color = moonGizmoColor;
        Gizmos.DrawSphere(center + (moonDir * radius), markerSize);
    }

    private void OnValidate()
    {
        if (cycleDurationSeconds < 0.1f)
        {
            cycleDurationSeconds = 0.1f;
        }

        if (celestialRadius < 0.1f)
        {
            celestialRadius = 0.1f;
        }

        if (skydomeRadiusMultiplier < 0.1f)
        {
            skydomeRadiusMultiplier = 0.1f;
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
    }
}
