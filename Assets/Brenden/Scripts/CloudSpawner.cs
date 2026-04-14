using System.Collections.Generic;
using UnityEngine;

public class CloudSpawner : MonoBehaviour
{
    private class CloudInstance
    {
        public Transform Transform;
        public float Speed;
    }

    public enum SpawnMode
    {
        SphereShell,
        BoxVolume,
        FixedPoints
    }

    [Header("References")]
    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private List<GameObject> cloudPrefabs = new();
    [SerializeField] private Transform despawnPoint;

    [Header("Spawn")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.SphereShell;
    [SerializeField, Min(0.05f)] private float spawnInterval = 2f;
    [SerializeField, Min(0f)] private float spawnIntervalJitter = 0.6f;
    [SerializeField, Min(0)] private int initialSpawnCount = 8;
    [SerializeField, Min(1)] private int maxClouds = 20;
    [SerializeField, Min(0f)] private float initialBackfillDistance = 20f;
    [SerializeField, Min(0f)] private float spawnMinSeparation = 6f;
    [SerializeField, Min(1)] private int spawnPlacementAttempts = 8;

    [Header("Sphere Shell Spawn")]
    [SerializeField] private Vector3 sphereCenterOffset = Vector3.zero;
    [SerializeField, Min(0.1f)] private float sphereRadius = 50f;
    [SerializeField, Min(0f)] private float shellThickness = 4f;

    [Header("Box Volume Spawn")]
    [SerializeField] private Vector3 boxCenterOffset = Vector3.zero;
    [SerializeField] private Vector3 boxSize = new Vector3(100f, 30f, 100f);

    [Header("Fixed Points Spawn")]
    [SerializeField] private List<Transform> spawnPoints = new();
    [SerializeField, Min(0f)] private float pointSpreadRadius = 2f;
    [SerializeField] private bool usePointForwardRotation = true;

    [Header("Cloud Size")]
    [SerializeField, Min(0.01f)] private float minScale = 0.8f;
    [SerializeField, Min(0.01f)] private float maxScale = 2.2f;

    [Header("Vertical Limits")]
    [SerializeField] private bool clampSpawnHeight = true;
    [SerializeField] private float minSpawnY = 20f;
    [SerializeField] private float maxSpawnY = 120f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float speedVariance = 0.7f;
    [SerializeField] private bool useSpawnerForward = true;
    [SerializeField] private Vector3 customMoveDirection = Vector3.forward;

    private readonly List<CloudInstance> _activeClouds = new();
    private float _spawnTimer;
    private int _nextPointIndex;

    private void Start()
    {
        SpawnInitialClouds();
        ResetSpawnTimer();
    }

    private void Update()
    {
        MoveClouds();
        DespawnPassedClouds();
        TrySpawnOverTime();
    }

    private void SpawnInitialClouds()
    {
        for (int i = 0; i < initialSpawnCount && _activeClouds.Count < maxClouds; i++)
        {
            SpawnCloud();
        }
    }

    private void TrySpawnOverTime()
    {
        if (!HasAnyCloudPrefab() || _activeClouds.Count >= maxClouds)
        {
            return;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f)
        {
            return;
        }

        SpawnCloud();
        ResetSpawnTimer();
    }

    private void SpawnCloud()
    {
        GameObject prefabToSpawn = GetRandomCloudPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("CloudSpawner has no cloud prefab assigned in Cloud Prefab or Cloud Prefabs.", this);
            return;
        }

        if (!TryGetSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            return;
        }

        Transform cloud = Instantiate(prefabToSpawn, spawnPosition, spawnRotation).transform;

        float randomScale = Random.Range(Mathf.Min(minScale, maxScale), Mathf.Max(minScale, maxScale));
        cloud.localScale = cloud.localScale * randomScale;

        float cloudSpeed = Mathf.Max(0f, moveSpeed + Random.Range(-speedVariance, speedVariance));
        _activeClouds.Add(new CloudInstance
        {
            Transform = cloud,
            Speed = cloudSpeed
        });
    }

    private void ResetSpawnTimer()
    {
        float minInterval = Mathf.Max(0.05f, spawnInterval - spawnIntervalJitter);
        float maxInterval = Mathf.Max(minInterval, spawnInterval + spawnIntervalJitter);
        _spawnTimer = Random.Range(minInterval, maxInterval);
    }

    private GameObject GetRandomCloudPrefab()
    {
        if (cloudPrefabs != null && cloudPrefabs.Count > 0)
        {
            int validCount = 0;
            for (int i = 0; i < cloudPrefabs.Count; i++)
            {
                if (cloudPrefabs[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                int randomValidIndex = Random.Range(0, validCount);
                int currentValidIndex = 0;

                for (int i = 0; i < cloudPrefabs.Count; i++)
                {
                    GameObject candidate = cloudPrefabs[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (currentValidIndex == randomValidIndex)
                    {
                        return candidate;
                    }

                    currentValidIndex++;
                }
            }
        }

        return cloudPrefab;
    }

    private bool HasAnyCloudPrefab()
    {
        if (cloudPrefab != null)
        {
            return true;
        }

        if (cloudPrefabs == null || cloudPrefabs.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < cloudPrefabs.Count; i++)
        {
            if (cloudPrefabs[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        for (int attempt = 0; attempt < Mathf.Max(1, spawnPlacementAttempts); attempt++)
        {
            bool gotPose;
            switch (spawnMode)
            {
                case SpawnMode.FixedPoints:
                    gotPose = TryGetFixedPointPose(out position, out rotation);
                    break;

                case SpawnMode.BoxVolume:
                    gotPose = TryGetBoxVolumePose(out position, out rotation);
                    break;

                case SpawnMode.SphereShell:
                default:
                    gotPose = TryGetSphereShellPose(out position, out rotation);
                    break;
            }

            if (!gotPose)
            {
                continue;
            }

            if (spawnMinSeparation <= 0f || IsPositionFarEnough(position, spawnMinSeparation))
            {
                return true;
            }
        }

        position = default;
        rotation = default;
        return false;
    }

    private bool TryGetSphereShellPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 center = transform.position + sphereCenterOffset;
        Vector3 randomDir = Random.onUnitSphere;
        float extraDistance = shellThickness > 0f ? Random.Range(0f, shellThickness) : 0f;
        float radius = sphereRadius + extraDistance;

        position = center + randomDir * radius;
        position -= GetMoveDirection() * Random.Range(0f, initialBackfillDistance);
        position = ApplyHeightClamp(position);
        rotation = Quaternion.LookRotation(GetMoveDirection(), Vector3.up);
        return true;
    }

    private bool TryGetBoxVolumePose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 center = transform.position + boxCenterOffset;
        Vector3 halfSize = new Vector3(
            Mathf.Max(0.01f, boxSize.x) * 0.5f,
            Mathf.Max(0.01f, boxSize.y) * 0.5f,
            Mathf.Max(0.01f, boxSize.z) * 0.5f
        );

        position = new Vector3(
            Random.Range(center.x - halfSize.x, center.x + halfSize.x),
            Random.Range(center.y - halfSize.y, center.y + halfSize.y),
            Random.Range(center.z - halfSize.z, center.z + halfSize.z)
        );

        position -= GetMoveDirection() * Random.Range(0f, initialBackfillDistance);
        position = ApplyHeightClamp(position);
        rotation = Quaternion.LookRotation(GetMoveDirection(), Vector3.up);
        return true;
    }

    private bool TryGetFixedPointPose(out Vector3 position, out Quaternion rotation)
    {
        if (spawnPoints.Count == 0)
        {
            position = default;
            rotation = default;
            Debug.LogWarning("CloudSpawner is set to FixedPoints mode but no spawn points are assigned.", this);
            return false;
        }

        // Cycle through points so clouds stay distributed, then add random spread around each point.
        Transform point = spawnPoints[_nextPointIndex % spawnPoints.Count];
        _nextPointIndex++;

        Vector3 basePosition = point != null ? point.position : transform.position;
        Vector2 spread = Random.insideUnitCircle * pointSpreadRadius;
        position = basePosition + new Vector3(spread.x, 0f, spread.y);
        position -= GetMoveDirection() * Random.Range(0f, initialBackfillDistance);
        position = ApplyHeightClamp(position);

        if (usePointForwardRotation && point != null)
        {
            rotation = point.rotation;
        }
        else
        {
            rotation = Quaternion.LookRotation(GetMoveDirection(), Vector3.up);
        }

        return true;
    }

    private void MoveClouds()
    {
        if (_activeClouds.Count == 0 || moveSpeed <= 0f)
        {
            return;
        }

        Vector3 moveDirection = GetMoveDirection();

        for (int i = _activeClouds.Count - 1; i >= 0; i--)
        {
            CloudInstance cloudInstance = _activeClouds[i];
            Transform cloud = cloudInstance.Transform;
            if (cloud == null)
            {
                _activeClouds.RemoveAt(i);
                continue;
            }

            float distance = cloudInstance.Speed * Time.deltaTime;
            cloud.position += moveDirection * distance;
        }
    }

    private void DespawnPassedClouds()
    {
        if (_activeClouds.Count == 0 || despawnPoint == null)
        {
            return;
        }

        Vector3 moveDirection = GetMoveDirection();
        Vector3 planePoint = despawnPoint.position;

        for (int i = _activeClouds.Count - 1; i >= 0; i--)
        {
            Transform cloud = _activeClouds[i].Transform;
            if (cloud == null)
            {
                _activeClouds.RemoveAt(i);
                continue;
            }

            float signedDistanceAlongDirection = Vector3.Dot(cloud.position - planePoint, moveDirection);
            if (signedDistanceAlongDirection >= 0f)
            {
                Destroy(cloud.gameObject);
                _activeClouds.RemoveAt(i);
            }
        }
    }

    private bool IsPositionFarEnough(Vector3 candidatePosition, float minimumDistance)
    {
        float minSqrDistance = minimumDistance * minimumDistance;

        for (int i = 0; i < _activeClouds.Count; i++)
        {
            Transform active = _activeClouds[i].Transform;
            if (active == null)
            {
                continue;
            }

            if ((active.position - candidatePosition).sqrMagnitude < minSqrDistance)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 ApplyHeightClamp(Vector3 position)
    {
        if (!clampSpawnHeight)
        {
            return position;
        }

        float minY = Mathf.Min(minSpawnY, maxSpawnY);
        float maxY = Mathf.Max(minSpawnY, maxSpawnY);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    private Vector3 GetMoveDirection()
    {
        Vector3 direction = useSpawnerForward ? transform.forward : customMoveDirection;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        return direction.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnMode == SpawnMode.SphereShell)
        {
            Vector3 center = transform.position + sphereCenterOffset;
            Gizmos.color = new Color(0.45f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(center, sphereRadius);

            if (shellThickness > 0f)
            {
                Gizmos.color = new Color(0.25f, 0.5f, 1f, 0.25f);
                Gizmos.DrawWireSphere(center, sphereRadius + shellThickness);
            }
        }

        if (spawnMode == SpawnMode.BoxVolume)
        {
            Vector3 center = transform.position + boxCenterOffset;
            Gizmos.color = new Color(0.2f, 1f, 0.7f, 0.35f);
            Gizmos.DrawWireCube(center, boxSize);
        }

        if (spawnMode == SpawnMode.FixedPoints)
        {
            Gizmos.color = Color.cyan;
            foreach (Transform point in spawnPoints)
            {
                if (point == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(point.position, Mathf.Max(0.25f, pointSpreadRadius));
            }
        }

        if (despawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(despawnPoint.position, 0.3f);
            Gizmos.DrawRay(despawnPoint.position, GetMoveDirection() * 3f);
        }
    }
}
