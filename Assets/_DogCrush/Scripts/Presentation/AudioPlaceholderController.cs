using DogCrush.Board;
using DogCrush.Core;
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
        public AudioClip specialClip;
        public AudioClip cascadeClip;
        public AudioClip timerWarningClip;
        public AudioClip gameOverClip;
        private AudioClip victoryClip;
        private AudioClip musicClip;
        private BoardTheme musicTheme;
        private bool musicReady;
        private const string MusicVolumePreference = "DogCrush_MusicVolume";

        public float SfxVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 0.18f;

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
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePreference, 0.18f));
            ApplyVolume();
            CreateFallbackClips();
        }

        public void PlayWorldTheme(BoardTheme theme)
        {
            if (musicSource == null) return;
            if (!musicReady || musicClip == null || musicTheme != theme)
            {
                musicClip = CreateThemeMusic(theme);
                musicTheme = theme;
                musicReady = true;
            }
            musicSource.clip = musicClip;
            musicSource.volume = MusicVolume;
            if (MusicVolume > 0.001f && !musicSource.isPlaying) musicSource.Play();
            else if (MusicVolume <= 0.001f && musicSource.isPlaying) musicSource.Stop();
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            if (musicSource != null)
            {
                musicSource.volume = MusicVolume;
                if (MusicVolume <= 0.001f) musicSource.Stop();
                else if (musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
            }
            PlayerPrefs.SetFloat(MusicVolumePreference, MusicVolume);
            PlayerPrefs.Save();
        }

        public void PlaySelectSound(int chainLength = 1)
        {
            PlayClip(selectClip, 0.9f + Mathf.Clamp(chainLength - 1, 0, 7) * 0.055f, 0.42f);
        }

        public void PlayMatchSound(int chainLength = 3)
        {
            int bonus = Mathf.Clamp(chainLength - 3, 0, 6);
            PlayClip(matchClip, 0.96f + bonus * 0.045f, 0.72f + bonus * 0.025f);
        }

        public void PlayComboSound()
        {
            PlayClip(comboClip, 1f, 0.82f);
        }

        public void PlaySpecialSound(bool mega = false)
        {
            PlayClip(specialClip, mega ? 0.78f : 1f, mega ? 0.96f : 0.78f);
        }

        public void PlaySpecialComboSound(SpecialComboKind comboKind)
        {
            int tier = comboKind == SpecialComboKind.BoardNova ? 4 :
                comboKind == SpecialComboKind.ColorSweep || comboKind == SpecialComboKind.DoubleArea ? 3 :
                comboKind == SpecialComboKind.WideRow || comboKind == SpecialComboKind.WideColumn ? 2 : 1;
            PlayClip(specialClip, Mathf.Lerp(1.08f, 0.76f, tier / 4f), 0.72f + tier * 0.065f);
            PlayClip(comboClip, 0.92f + tier * 0.08f, 0.34f + tier * 0.06f);
        }

        public void PlayVictorySound()
        {
            PlayClip(victoryClip, 1f, 0.88f);
        }

        public void PlayCascadeSound(int depth)
        {
            int step = Mathf.Clamp(depth, 1, 8);
            float risingPitch = 0.88f + step * 0.085f;
            PlayClip(cascadeClip, risingPitch, 0.44f + step * 0.025f);
        }

        /// <summary>
        /// A playful high overtone layered over the rising cascade pluck.
        /// It creates a light bark-like melodic signature without loading an
        /// external audio asset.
        /// </summary>
        public void PlayCascadeBark(int depth)
        {
            if (depth < 2) return;
            int step = Mathf.Clamp(depth, 2, 8);
            PlayClip(selectClip, 0.74f + step * 0.115f, 0.20f + step * 0.018f);
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

        public float CycleMusicVolume()
        {
            if (MusicVolume > 0.12f) SetMusicVolume(0f);
            else if (MusicVolume <= 0.001f) SetMusicVolume(0.18f);
            else SetMusicVolume(0.18f);
            return MusicVolume;
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
            if (musicSource != null) musicSource.volume = MusicVolume;
        }

        private static AudioClip CreateThemeMusic(BoardTheme theme)
        {
            const int sampleRate = 11025;
            const float duration = 8f;
            int count = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[count];
            float[] roots = { 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f };
            int themeIndex = Mathf.Clamp((int)theme, 0, 9);
            float root = roots[themeIndex % roots.Length] * (themeIndex >= 6 ? .5f : 1f);
            float[] steps = { 0f, 3f, 5f, 7f, 10f, 7f, 5f, 3f };
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                int step = Mathf.FloorToInt(time * 2f) % steps.Length;
                float note = root * Mathf.Pow(2f, steps[step] / 12f);
                phase += 2f * Mathf.PI * note / sampleRate;
                float pulse = Mathf.Sin(2f * Mathf.PI * time * .25f) * .5f + .5f;
                float envelope = Mathf.Min(1f, time * 4f) * Mathf.Min(1f, (duration - time) * 4f);
                float pad = Mathf.Sin(phase * .5f) * .06f + Mathf.Sin(phase) * .035f;
                samples[i] = (pad + Mathf.Sin(2f * Mathf.PI * root * time) * .022f * pulse) * envelope;
            }
            return CreateRuntimeClip("WorldTheme_RT_" + theme, samples, sampleRate);
        }

        private void PlayClip(AudioClip clip, float pitch, float volumeScale)
        {
            if (clip == null || sfxSource == null || SfxVolume <= 0.001f)
            {
                return;
            }

            sfxSource.volume = SfxVolume;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volumeScale);
        }

        private void CreateFallbackClips()
        {
            if (selectClip == null)
                selectClip = CreateTone("SelectTone_RT", 620f, 0.055f, 0.20f, 180f);
            if (matchClip == null)
                matchClip = CreateJuicyPop("MatchPop_RT");
            if (comboClip == null)
                comboClip = CreateSparkle("ComboSparkle_RT");
            if (specialClip == null)
                specialClip = CreateSpecialBoom("SpecialBoom_RT");
            if (cascadeClip == null)
                cascadeClip = CreatePluck("CascadePluck_RT");
            if (timerWarningClip == null)
                timerWarningClip = CreateTone("WarningTone_RT", 760f, 0.16f, 0.19f, -120f);
            if (gameOverClip == null)
                gameOverClip = CreateTone("GameOverTone_RT", 420f, 0.34f, 0.23f, -220f);
            if (victoryClip == null)
                victoryClip = CreateVictoryFanfare("VictoryFanfare_RT");

        }

        private static AudioClip CreateVictoryFanfare(string name)
        {
            const int sampleRate = 22050;
            const float duration = 0.72f;
            int count = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[count];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                float value = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float local = time - n * 0.105f;
                    if (local < 0f) continue;
                    float envelope = Mathf.Clamp01(local / 0.012f) * Mathf.Exp(-local * 5.2f);
                    value += (Mathf.Sin(2f * Mathf.PI * notes[n] * local) * 0.16f +
                              Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * local) * 0.035f) * envelope;
                }
                samples[i] = Mathf.Clamp(value, -0.8f, 0.8f);
            }
            return CreateRuntimeClip(name, samples, sampleRate);
        }

        private static AudioClip CreateJuicyPop(string name)
        {
            const int sampleRate = 22050;
            const float duration = 0.19f;
            int count = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[count];
            float phase = 0f;
            uint noise = 0x5f3759dfu;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(210f, 610f, 1f - Mathf.Pow(1f - t, 2.4f));
                phase += 2f * Mathf.PI * frequency / sampleRate;
                float envelope = Mathf.Clamp01(t / 0.025f) * Mathf.Pow(1f - t, 2.1f);
                noise = noise * 1664525u + 1013904223u;
                float transient = (((noise >> 9) & 0x7fffu) / 16384f - 1f) * Mathf.Clamp01(1f - t / 0.055f);
                float body = Mathf.Sin(phase) + Mathf.Sin(phase * 2.01f) * 0.22f;
                samples[i] = (body * 0.34f + transient * 0.16f) * envelope;
            }

            return CreateRuntimeClip(name, samples, sampleRate);
        }

        private static AudioClip CreateSparkle(string name)
        {
            const int sampleRate = 22050;
            const float duration = 0.28f;
            int count = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[count];
            float[] notes = { 880f, 1174.66f, 1567.98f };

            for (int i = 0; i < count; i++)
            {
                float time = i / (float)sampleRate;
                float value = 0f;
                for (int note = 0; note < notes.Length; note++)
                {
                    float local = time - note * 0.045f;
                    if (local < 0f) continue;
                    float envelope = Mathf.Exp(-local * 15f) * Mathf.Clamp01(local / 0.006f);
                    value += Mathf.Sin(2f * Mathf.PI * notes[note] * local) * envelope * 0.16f;
                }
                samples[i] = value;
            }

            return CreateRuntimeClip(name, samples, sampleRate);
        }

        private static AudioClip CreateSpecialBoom(string name)
        {
            const int sampleRate = 22050;
            const float duration = 0.48f;
            int count = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[count];
            float phase = 0f;
            uint noise = 0x1234abcdu;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(185f, 48f, Mathf.Sqrt(t));
                phase += 2f * Mathf.PI * frequency / sampleRate;
                noise = noise * 1103515245u + 12345u;
                float burst = (((noise >> 8) & 0xffffu) / 32768f - 1f) * Mathf.Exp(-t * 18f);
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * 5.2f);
                float shimmer = Mathf.Sin(2f * Mathf.PI * 1320f * t * duration) * Mathf.Exp(-t * 9f);
                samples[i] = body * 0.42f + burst * 0.19f + shimmer * 0.07f;
            }

            return CreateRuntimeClip(name, samples, sampleRate);
        }

        private static AudioClip CreatePluck(string name)
        {
            const int sampleRate = 22050;
            const float duration = 0.12f;
            int count = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float envelope = Mathf.Clamp01(t / 0.012f) * Mathf.Pow(1f - t, 3.2f);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * 720f * i / sampleRate) * 0.24f +
                              Mathf.Sin(2f * Mathf.PI * 1080f * i / sampleRate) * 0.10f) * envelope;
            }
            return CreateRuntimeClip(name, samples, sampleRate);
        }

        private static AudioClip CreateRuntimeClip(string name, float[] samples, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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
