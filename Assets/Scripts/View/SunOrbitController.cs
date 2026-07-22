using UnityEngine;



#if UNITY_EDITOR

using UnityEditor;

#endif



/// <summary>

/// Positions a 2D directional-light substitute around a moving centre,

/// normally the main camera.

///

/// The light's X/Y position determines how sprite normal maps are illuminated.

/// This controller does not change sorting, rendering layers, tilemaps or floor logic.

/// </summary>

[ExecuteAlways]

[DisallowMultipleComponent]

public sealed class SunOrbitController : MonoBehaviour

{

    [Header("Orbit Centre")]



    [Tooltip("Usually the main camera. If empty, the script tries to find Camera.main.")]

    [SerializeField] private Transform orbitCentre;



    [Tooltip("Additional world-space offset from the selected orbit centre.")]

    [SerializeField] private Vector2 centreOffset;



    [Header("Orbit")]



    [Tooltip("Distance between the light and its orbit centre.")]

    [Min(0.01f)]

    [SerializeField] private float orbitRadius = 8f;



    [Tooltip("Position of the light around the centre, measured in degrees.")]

    [Range(0f, 360f)]

    [SerializeField] private float orbitAngle = 45f;



    [Tooltip("Preserves the light's current Z position.")]

    [SerializeField] private bool preserveCurrentZ = true;



    [Tooltip("Z position used when Preserve Current Z is disabled.")]

    [SerializeField] private float fixedZ;



    [Header("Runtime Day Cycle")]



    [Tooltip("Automatically changes the orbit angle while the game is running.")]

    [SerializeField] private bool autoOrbit;



    [Tooltip("Degrees travelled per second when Auto Orbit is enabled.")]

    [SerializeField] private float degreesPerSecond = 2f;



    [Tooltip("Use unscaled time so pausing gameplay does not pause the sun.")]

    [SerializeField] private bool useUnscaledTime;



    [Header("Editor Preview")]



    [Tooltip("Update the light in Edit Mode when values or the camera position change.")]

    [SerializeField] private bool previewInEditMode = true;



    [Tooltip("Draw the orbit circle and light direction in the Scene view.")]

    [SerializeField] private bool drawGizmos = true;



    /// <summary>

    /// Direction travelling from the light toward the orbit centre.

    /// This is useful as the incoming light direction.

    /// </summary>

    public Vector2 LightDirection

    {

        get

        {

            Vector2 centre = GetCentrePosition();

            Vector2 light = transform.position;

            Vector2 direction = centre - light;



            return direction.sqrMagnitude > 0.0001f

            ? direction.normalized

            : Vector2.down;

        }

    }



    /// <summary>

    /// Direction in which a projected shadow should extend.

    /// This points away from the light.

    /// </summary>

    public Vector2 ShadowDirection => -LightDirection;



    public float OrbitAngle

    {

        get => orbitAngle;

        set

        {

            orbitAngle = NormalizeAngle(value);

            ApplyOrbitPosition();

        }

    }



    public float OrbitRadius

    {

        get => orbitRadius;

        set

        {

            orbitRadius = Mathf.Max(0.01f, value);

            ApplyOrbitPosition();

        }

    }



    private void Reset()

    {

        TryAssignMainCamera();

        ApplyOrbitPosition();

    }



    private void OnEnable()

    {

        TryAssignMainCamera();

        ApplyOrbitPosition();

    }



    private void OnValidate()

    {

        orbitRadius = Mathf.Max(0.01f, orbitRadius);

        orbitAngle = NormalizeAngle(orbitAngle);



        if (!Application.isPlaying && previewInEditMode)

            ApplyOrbitPosition();

    }



    private void Update()

    {

        if (Application.isPlaying)

        {

            if (autoOrbit)

            {

                float deltaTime = useUnscaledTime

                ? Time.unscaledDeltaTime

                : Time.deltaTime;



                orbitAngle = NormalizeAngle(

                orbitAngle + degreesPerSecond * deltaTime);

            }



            // Always update at runtime so the light follows a moving camera.

            ApplyOrbitPosition();

        }

        else if (previewInEditMode)

        {

            ApplyOrbitPosition();

        }

    }



    [ContextMenu("Assign Main Camera")]

    private void AssignMainCamera()

    {

        TryAssignMainCamera();

        ApplyOrbitPosition();

    }



    [ContextMenu("Move Light To Orbit Position")]

    private void MoveLightToOrbitPosition()

    {

        ApplyOrbitPosition();

    }



    private void TryAssignMainCamera()

    {

        if (orbitCentre != null)

            return;



        Camera mainCamera = Camera.main;



        if (mainCamera != null)

            orbitCentre = mainCamera.transform;

    }



    private void ApplyOrbitPosition()

    {

        if (orbitCentre == null)

            return;



        float radians = orbitAngle * Mathf.Deg2Rad;



        Vector2 orbitDirection = new(

        Mathf.Cos(radians),

        Mathf.Sin(radians));



        Vector2 centre = GetCentrePosition();

        Vector2 targetPosition = centre + orbitDirection * orbitRadius;



        float targetZ = preserveCurrentZ

        ? transform.position.z

        : fixedZ;



        transform.position = new Vector3(

        targetPosition.x,

        targetPosition.y,

        targetZ);

    }



    private Vector2 GetCentrePosition()

    {

        if (orbitCentre == null)

            return centreOffset;



        return (Vector2)orbitCentre.position + centreOffset;

    }



    private static float NormalizeAngle(float angle)

    {

        angle %= 360f;



        if (angle < 0f)

            angle += 360f;



        return angle;

    }



    private void OnDrawGizmosSelected()

    {

        if (!drawGizmos || orbitCentre == null)

            return;



        Vector2 centre = GetCentrePosition();



        Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.8f);

        Gizmos.DrawLine(centre, transform.position);

        Gizmos.DrawWireSphere(centre, 0.15f);



#if UNITY_EDITOR

        Handles.color = new Color(1f, 0.75f, 0.15f, 0.35f);

        Handles.DrawWireDisc(centre, Vector3.forward, orbitRadius);

#endif



        Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.9f);



        Vector2 shadowEnd =

        (Vector2)transform.position + ShadowDirection * 1.5f;



        Gizmos.DrawLine(transform.position, shadowEnd);

    }

}