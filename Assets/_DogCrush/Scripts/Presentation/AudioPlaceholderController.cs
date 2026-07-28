using UnityEngine;

namespace DogCrush.Presentation
{
    public class AudioPlaceholderController : MonoBehaviour
    {
        private const string SfxVolumePreference = "DogCrush_SfxVolume";

        public AudioSource sfxSource;
        public AudioSource musicSource;

        [Header("Audio Clips (Optional)")]
        public AudioClip selectClip;
        public AudioClip matchClip;
        public AudioClip comboClip;
        public AudioClip timerWarningClip;
        public AudioClip gameOverClip;

        public float SfxVolume { get; private set; } = 1f;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }
            sfxSource.spatialBlend = 0f;
            sfxSource.ignoreListenerPause = true;

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }

            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePreference, 1f));
            ApplyVolume();
            CreateFallbackClips();
        }

        public void PlaySelectSound(int chainLength = 1)
        {
            PlayClip(selectClip, 0.9f + Mathf.Clamp(chainLength - 1, 0, 7) * 0.055f, 0.42f);
        }

        public void PlayMatchSound(int chainLength = 3)
        {
            PlayClip(matchClip, 0.92f + Mathf.Clamp(chainLength - 3, 0, 6) * 0.025f, 0.72f);
        }

        public void PlayComboSound()
        {
            PlayClip(comboClip, 1f, 0.82f);
        }

        public void PlayTimerWarningSound()
        {
            PlayClip(timerWarningClip, 1f, 0.55f);
        }

        public void PlayGameOverSound()
        {
            PlayClip(gameOverClip, 1f, 0.7f);
        }

        public void PlayUISound()
        {
            PlayClip(selectClip, 1.08f, 0.38f);
        }

        public float CycleSfxVolume()
        {
            if (SfxVolume > 0.8f)
            {
                SetSfxVolume(0.6f);
            }
            else if (SfxVolume > 0.05f)
            {
                SetSfxVolume(0f);
            }
            else
            {
                SetSfxVolume(1f);
                PlayUISound();
            }

            return SfxVolume;
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            ApplyVolume();
            PlayerPrefs.SetFloat(SfxVolumePreference, SfxVolume);
            PlayerPrefs.Save();
        }

        private void ApplyVolume()
        {
            if (sfxSource != null)
            {
                sfxSource.volume = SfxVolume;
            }
        }

        private void PlayClip(AudioClip clip, float pitch, float volumeScale)
        {
            if (clip == null || sfxSource == null || SfxVolume <= 0.001f)
            {
                return;
            }

            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volumeScale);
        }

        private void CreateFallbackClips()
        {
            if (selectClip == null)
                selectClip = CreateTone("SelectTone_RT", 620f, 0.055f, 0.20f, 180f);
            if (matchClip == null)
                matchClip = CreateTone("MatchTone_RT", 330f, 0.18f, 0.28f, 520f);
            if (comboClip == null)
                comboClip = CreateTone("ComboTone_RT", 520f, 0.24f, 0.27f, 620f);
            if (timerWarningClip == null)
                timerWarningClip = CreateTone("WarningTone_RT", 760f, 0.16f, 0.19f, -120f);
            if (gameOverClip == null)
                gameOverClip = CreateTone("GameOverTone_RT", 420f, 0.34f, 0.23f, -220f);
        }

        private static AudioClip CreateTone(
            string name,
            float startFrequency,
            float duration,
            float amplitude,
            float frequencySweep)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float progress = i / (float)sampleCount;
                float frequency = startFrequency + frequencySweep * progress;
                phase += 2f * Mathf.PI * frequency / sampleRate;

                float attack = Mathf.Clamp01(progress / 0.08f);
                float release = Mathf.Clamp01((1f - progress) / 0.28f);
                float envelope = attack * release;
                float fundamental = Mathf.Sin(phase);
                float softHarmonic = Mathf.Sin(phase * 2f) * 0.16f;
                samples[i] = (fundamental + softHarmonic) * amplitude * envelope;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
