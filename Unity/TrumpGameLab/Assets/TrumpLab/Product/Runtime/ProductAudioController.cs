#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrumpLab.Product
{
    public enum ProductFeedbackKind
    {
        Navigation = 0,
        Submit = 1,
        Reject = 2,
        CardPlay = 3,
        Draw = 4,
        WildSuit = 5,
        CpuTurn = 6,
        Win = 7,
        Lose = 8,
        Error = 9
    }

    public interface IProductFeedbackSink
    {
        void Play(ProductFeedbackKind kind);
    }

    public interface IProductAudioFeedback : IProductFeedbackSink
    {
        void ApplySettings(ProductSettings settings);
    }

    [Serializable]
    public sealed class ProductAudioClipBinding
    {
        [SerializeField] private ProductFeedbackKind kind;
        [SerializeField] private AudioClip? clip;

        public ProductFeedbackKind Kind => kind;
        public AudioClip? Clip => clip;

        public ProductAudioClipBinding(ProductFeedbackKind kind, AudioClip clip)
        {
            this.kind = kind;
            this.clip = clip ?? throw new ArgumentNullException(nameof(clip));
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProductAudioController : MonoBehaviour, IProductAudioFeedback
    {
        [SerializeField] private AudioSource? musicSource;
        [SerializeField] private AudioSource? sfxSource;
        [SerializeField] private AudioClip? musicLoop;
        [SerializeField] private ProductAudioClipBinding[] cueClips =
            Array.Empty<ProductAudioClipBinding>();

        private readonly Dictionary<ProductFeedbackKind, AudioClip> clips =
            new Dictionary<ProductFeedbackKind, AudioClip>();
        private bool initialized;
        private int musicVolumePercent;
        private int sfxVolumePercent;

        public event System.Action<ProductFeedbackKind>? CuePlayed;

        public bool IsInitialized => initialized;
        public ProductFeedbackKind? LastCue { get; private set; }
        public AudioSource MusicSource => musicSource ?? throw Missing(nameof(musicSource));
        public AudioSource SfxSource => sfxSource ?? throw Missing(nameof(sfxSource));
        public AudioClip MusicLoop => musicLoop ?? throw Missing(nameof(musicLoop));
        public int MusicVolumePercent => musicVolumePercent;
        public int SfxVolumePercent => sfxVolumePercent;
        public float MusicVolume => musicSource == null ? 0f : musicSource.volume;
        public float SfxVolume => sfxSource == null ? 0f : sfxSource.volume;

        public void Configure(AudioSource configuredMusicSource,
            AudioSource configuredSfxSource, AudioClip configuredMusicLoop,
            ProductAudioClipBinding[] configuredCues)
        {
            musicSource = configuredMusicSource ??
                throw new ArgumentNullException(nameof(configuredMusicSource));
            sfxSource = configuredSfxSource ??
                throw new ArgumentNullException(nameof(configuredSfxSource));
            musicLoop = configuredMusicLoop ??
                throw new ArgumentNullException(nameof(configuredMusicLoop));
            cueClips = configuredCues == null
                ? throw new ArgumentNullException(nameof(configuredCues))
                : (ProductAudioClipBinding[])configuredCues.Clone();
            initialized = false;
            Initialize();
        }

        private void Awake()
        {
            // Runtime-created controllers are allowed to receive Configure after AddComponent.
            // Serialized scene controllers still fail fast during startup when partly configured.
            if (musicSource != null || sfxSource != null || musicLoop != null ||
                (cueClips != null && cueClips.Length != 0))
                Initialize();
        }

        public void Initialize()
        {
            if (initialized) return;
            ValidateAndIndexClips();
            ConfigureSources();
            musicVolumePercent = 0;
            sfxVolumePercent = 0;
            MusicSource.volume = 0f;
            SfxSource.volume = 0f;
            initialized = true;
        }

        public void ApplySettings(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Initialize();

            musicVolumePercent = settings.MusicVolume;
            sfxVolumePercent = settings.SfxVolume;
            MusicSource.volume = Normalize(settings.MusicVolume);
            SfxSource.volume = Normalize(settings.SfxVolume);

            if (settings.MusicVolume == 0)
                MusicSource.Stop();
            else if (CanProduceAudio(MusicSource) && !MusicSource.isPlaying)
                MusicSource.Play();

            if (settings.SfxVolume == 0) SfxSource.Stop();
        }

        public void Play(ProductFeedbackKind kind)
        {
            RequireKind(kind);
            Initialize();
            if (!clips.TryGetValue(kind, out AudioClip? clip) || clip == null)
                throw new InvalidOperationException(
                    "Product audio cue is not configured: " + kind);

            // Batch and EditMode tests observe this logical dispatch without opening an
            // audio device. A zero SFX category remains completely silent.
            if (sfxVolumePercent > 0 && CanProduceAudio(SfxSource))
                SfxSource.PlayOneShot(clip);
            LastCue = kind;
            CuePlayed?.Invoke(kind);
        }

        private void ValidateAndIndexClips()
        {
            if (musicSource == null) throw Missing(nameof(musicSource));
            if (sfxSource == null) throw Missing(nameof(sfxSource));
            if (musicSource == sfxSource)
                throw new InvalidOperationException(
                    "Music and SFX require separate AudioSource components.");
            if (musicLoop == null) throw Missing(nameof(musicLoop));

            Array values = Enum.GetValues(typeof(ProductFeedbackKind));
            if (cueClips == null || cueClips.Length != values.Length)
                throw new InvalidOperationException(
                    "Product audio requires exactly one clip for every feedback kind.");

            clips.Clear();
            foreach (ProductAudioClipBinding? binding in cueClips)
            {
                if (binding == null)
                    throw new InvalidOperationException(
                        "Product audio cue bindings cannot contain null.");
                RequireKind(binding.Kind);
                AudioClip clip = binding.Clip ?? throw new InvalidOperationException(
                    "Product audio cue has no clip: " + binding.Kind);
                if (!clips.TryAdd(binding.Kind, clip))
                    throw new InvalidOperationException(
                        "Product audio cue is configured more than once: " + binding.Kind);
            }
            foreach (ProductFeedbackKind kind in values)
            {
                if (!clips.ContainsKey(kind))
                    throw new InvalidOperationException(
                        "Product audio cue is missing: " + kind);
            }
        }

        private void ConfigureSources()
        {
            AudioSource music = MusicSource;
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.dopplerLevel = 0f;
            music.mute = false;
            music.clip = MusicLoop;

            AudioSource sfx = SfxSource;
            sfx.playOnAwake = false;
            sfx.loop = false;
            sfx.spatialBlend = 0f;
            sfx.dopplerLevel = 0f;
            sfx.mute = false;
            sfx.clip = null;
        }

        private static bool CanProduceAudio(AudioSource source) =>
            Application.isPlaying && !Application.isBatchMode &&
            source.isActiveAndEnabled;

        private static float Normalize(int percent) => percent / 100f;

        private static void RequireKind(ProductFeedbackKind kind)
        {
            if (!Enum.IsDefined(typeof(ProductFeedbackKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    "Unknown product feedback kind.");
        }

        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException(
                "Product audio controller is not configured: " + name);
    }
}
