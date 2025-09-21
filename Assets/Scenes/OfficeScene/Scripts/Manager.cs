using UnityEngine;

/// <summary>
/// Centralised manager responsible for handling background audio playback
/// for the Office scene. The component safely initialises the underlying
/// <see cref="AudioSource"/> so the scene does not fail to compile when the
/// source is referenced from events or other scripts.
/// </summary>
[DisallowMultipleComponent]
public class Manager : MonoBehaviour
{
    [Header("Background Audio")]
    [Tooltip("Audio source used to play the scene's background music.")]
    [SerializeField]
    private AudioSource bgAudioSource;

    [Tooltip("Clip that will be played as the background music.")]
    [SerializeField]
    private AudioClip bgAudioClip;

    [Tooltip("Initial volume assigned to the background audio source.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float initialVolume = 1f;

    [Tooltip("Automatically start playing the clip on Start.")]
    [SerializeField]
    private bool playOnStart = true;

    /// <summary>
    /// Public accessor for the background audio source, allowing other
    /// components to interact with the configured source if needed.
    /// </summary>
    public AudioSource BackgroundAudioSource => bgAudioSource;

    private void Awake()
    {
        // Attempt to fall back to an AudioSource on the same GameObject when
        // one has not been explicitly assigned in the inspector.
        if (bgAudioSource == null)
        {
            bgAudioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        ApplyInitialVolume();

        if (playOnStart)
        {
            PlayBackgroundLoop();
        }
    }

    /// <summary>
    /// Plays the configured background clip using the managed audio source.
    /// </summary>
    public void PlayBackgroundLoop()
    {
        if (!EnsureAudioSourceAvailable())
        {
            return;
        }

        if (bgAudioClip != null)
        {
            bgAudioSource.clip = bgAudioClip;
        }

        if (bgAudioSource.clip == null)
        {
            Debug.LogWarning("[Manager] Unable to play background audio because no clip is assigned.");
            return;
        }

        bgAudioSource.loop = true;
        if (!bgAudioSource.isPlaying)
        {
            bgAudioSource.Play();
        }
    }

    /// <summary>
    /// Stops the currently playing background clip.
    /// </summary>
    public void StopBackgroundLoop()
    {
        if (bgAudioSource != null && bgAudioSource.isPlaying)
        {
            bgAudioSource.Stop();
        }
    }

    /// <summary>
    /// Updates the playback volume of the background music.
    /// </summary>
    /// <param name="volume">Target volume value between 0 and 1.</param>
    public void SetVolume(float volume)
    {
        if (!EnsureAudioSourceAvailable())
        {
            return;
        }

        bgAudioSource.volume = Mathf.Clamp01(volume);
    }

    private void ApplyInitialVolume()
    {
        if (bgAudioSource != null)
        {
            bgAudioSource.volume = Mathf.Clamp01(initialVolume);
        }
    }

    private bool EnsureAudioSourceAvailable()
    {
        if (bgAudioSource != null)
        {
            return true;
        }

        Debug.LogWarning("[Manager] No AudioSource assigned for background playback.");
        return false;
    }
}
