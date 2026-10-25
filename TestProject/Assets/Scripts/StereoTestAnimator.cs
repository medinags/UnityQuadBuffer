using UnityEngine;

/// <summary>
/// Test animation for checking stereo depth: rotates the object continuously and, at a random
/// interval between <see cref="minColorInterval"/> and <see cref="maxColorInterval"/> seconds,
/// fades it to a new random color.
/// </summary>
/// <remarks>
/// Uses a MaterialPropertyBlock, so objects sharing a material don't create material copies.
/// Sets both _Color (Built-in) and _BaseColor (URP/HDRP Lit).
/// </remarks>
[RequireComponent(typeof(Renderer))]
public class StereoTestAnimator : MonoBehaviour
{
    static readonly int s_color = Shader.PropertyToID("_Color");
    static readonly int s_baseColor = Shader.PropertyToID("_BaseColor");

    [Header("Rotation")]
    [Tooltip("Degrees per second. Each object gets a random speed in this range.")]
    public Vector2 rotationSpeedRange = new Vector2(20f, 60f);

    [Header("Color")]
    [Tooltip("Shortest time between color changes, in seconds.")]
    public float minColorInterval = 10f;
    [Tooltip("Longest time between color changes, in seconds.")]
    public float maxColorInterval = 20f;
    [Tooltip("How long the fade to the new color takes, in seconds.")]
    public float fadeDuration = 1f;

    Renderer m_renderer;
    MaterialPropertyBlock m_block;
    Vector3 m_axis;
    float m_speed;
    Color m_from, m_to, m_current;
    float m_fadeStart = float.NegativeInfinity;
    float m_nextChange;

    void Awake()
    {
        m_renderer = GetComponent<Renderer>();
        m_block = new MaterialPropertyBlock();

        // A random axis and speed per object so they don't all move together.
        m_axis = Random.onUnitSphere;
        m_speed = Random.Range(rotationSpeedRange.x, rotationSpeedRange.y);

        m_current = m_to = RandomColor();
        ApplyColor(m_current);
        ScheduleNextChange();
    }

    void Update()
    {
        transform.Rotate(m_axis, m_speed * Time.deltaTime, Space.World);

        if (Time.time >= m_nextChange)
        {
            m_from = m_current;
            m_to = RandomColor();
            m_fadeStart = Time.time;
            ScheduleNextChange();
        }

        float t = fadeDuration > 0f ? (Time.time - m_fadeStart) / fadeDuration : 1f;
        if (t <= 1f)
        {
            m_current = Color.Lerp(m_from, m_to, t);
            ApplyColor(m_current);
        }
    }

    void ScheduleNextChange()
    {
        m_nextChange = Time.time + Random.Range(minColorInterval, Mathf.Max(minColorInterval, maxColorInterval));
    }

    void ApplyColor(Color color)
    {
        m_renderer.GetPropertyBlock(m_block);
        m_block.SetColor(s_color, color);
        m_block.SetColor(s_baseColor, color);
        m_renderer.SetPropertyBlock(m_block);
    }

    // Bright, saturated colors stay easy to tell apart through shutter glasses.
    static Color RandomColor() => Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.7f, 1f);
}
