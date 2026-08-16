#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public enum ProductPresentationPalette
    {
        Navigation,
        Positive,
        Negative,
        Card,
        Draw,
        Wild,
        Cpu,
        Win,
        Lose,
        Error
    }

    public sealed class ProductFeedbackPresentation
    {
        public ProductFeedbackKind Kind { get; }
        public string Key { get; }
        public string Symbol { get; }
        public string EnglishFallback { get; }
        public string DisplayText { get; }
        public ProductPresentationPalette Palette { get; }

        public ProductFeedbackPresentation(ProductFeedbackKind kind, string key, string symbol,
            string englishFallback, ProductPresentationPalette palette)
        {
            Kind = kind;
            Key = Required(key, nameof(key));
            Symbol = Required(symbol, nameof(symbol));
            EnglishFallback = Required(englishFallback, nameof(englishFallback));
            DisplayText = Symbol + "  " + EnglishFallback;
            Palette = palette;
        }

        private static string Required(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Presentation text cannot be empty.", parameterName)
                : value;
    }

    public static class ProductPresentationCatalog
    {
        private static readonly ProductFeedbackPresentation[] Entries =
        {
            new ProductFeedbackPresentation(ProductFeedbackKind.Navigation,
                "feedback.navigation", "◇", "Focus moved", ProductPresentationPalette.Navigation),
            new ProductFeedbackPresentation(ProductFeedbackKind.Submit,
                "feedback.submit", "✓", "Confirmed", ProductPresentationPalette.Positive),
            new ProductFeedbackPresentation(ProductFeedbackKind.Reject,
                "feedback.reject", "✕", "Not available", ProductPresentationPalette.Negative),
            new ProductFeedbackPresentation(ProductFeedbackKind.CardPlay,
                "feedback.card_play", "▣", "Card played", ProductPresentationPalette.Card),
            new ProductFeedbackPresentation(ProductFeedbackKind.Draw,
                "feedback.draw", "+", "Card drawn", ProductPresentationPalette.Draw),
            new ProductFeedbackPresentation(ProductFeedbackKind.WildSuit,
                "feedback.wild_suit", "★", "Suit confirmed", ProductPresentationPalette.Wild),
            new ProductFeedbackPresentation(ProductFeedbackKind.CpuTurn,
                "feedback.cpu_turn", "…", "CPU turn", ProductPresentationPalette.Cpu),
            new ProductFeedbackPresentation(ProductFeedbackKind.Win,
                "feedback.win", "★", "You win", ProductPresentationPalette.Win),
            new ProductFeedbackPresentation(ProductFeedbackKind.Lose,
                "feedback.lose", "◆", "CPU wins", ProductPresentationPalette.Lose),
            new ProductFeedbackPresentation(ProductFeedbackKind.Error,
                "feedback.error", "!", "Error", ProductPresentationPalette.Error)
        };

        private static readonly IReadOnlyList<ProductFeedbackPresentation> ReadOnlyEntries =
            Array.AsReadOnly(Entries);

        public static IReadOnlyList<ProductFeedbackPresentation> All => ReadOnlyEntries;

        public static ProductFeedbackPresentation Get(ProductFeedbackKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Entries.Length || Entries[index].Kind != kind)
                throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    "Unknown product feedback kind.");
            return Entries[index];
        }
    }

    public readonly struct ProductPresentationPolicy
    {
        public const float ReducedCueHoldSeconds = 0.55f;
        public const float NormalCueEnterSeconds = 0.10f;
        public const float NormalCueHoldSeconds = 0.65f;
        public const float NormalCueExitSeconds = 0.15f;
        public const float NormalTransitionSeconds = 0.18f;
        public const float FastCueEnterSeconds = 0.03f;
        public const float FastCueHoldSeconds = 0.22f;
        public const float FastCueExitSeconds = 0.05f;
        public const float FastTransitionSeconds = 0.06f;

        private ProductPresentationPolicy(ProductPresentationSpeed speed, bool reducedMotion,
            bool motionEnabled, float cueEnterSeconds, float cueHoldSeconds,
            float cueExitSeconds, float transitionSeconds)
        {
            Speed = speed;
            ReducedMotion = reducedMotion;
            MotionEnabled = motionEnabled;
            CueEnterSeconds = cueEnterSeconds;
            CueHoldSeconds = cueHoldSeconds;
            CueExitSeconds = cueExitSeconds;
            TransitionSeconds = transitionSeconds;
        }

        public ProductPresentationSpeed Speed { get; }
        public bool ReducedMotion { get; }
        public bool MotionEnabled { get; }
        public float CueEnterSeconds { get; }
        public float CueHoldSeconds { get; }
        public float CueExitSeconds { get; }
        public float TransitionSeconds { get; }

        public static ProductPresentationPolicy From(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return From(settings.PresentationSpeed, settings.ReducedMotion);
        }

        public static ProductPresentationPolicy From(ProductPresentationSpeed speed,
            bool reducedMotion)
        {
            bool motionEnabled = speed != ProductPresentationSpeed.Reduced && !reducedMotion;
            switch (speed)
            {
                case ProductPresentationSpeed.Reduced:
                    return new ProductPresentationPolicy(speed, reducedMotion,
                        motionEnabled: false, cueEnterSeconds: 0f,
                        cueHoldSeconds: ReducedCueHoldSeconds, cueExitSeconds: 0f,
                        transitionSeconds: 0f);
                case ProductPresentationSpeed.Normal:
                    return new ProductPresentationPolicy(speed, reducedMotion, motionEnabled,
                        motionEnabled ? NormalCueEnterSeconds : 0f,
                        NormalCueHoldSeconds,
                        motionEnabled ? NormalCueExitSeconds : 0f,
                        motionEnabled ? NormalTransitionSeconds : 0f);
                case ProductPresentationSpeed.Fast:
                    return new ProductPresentationPolicy(speed, reducedMotion, motionEnabled,
                        motionEnabled ? FastCueEnterSeconds : 0f,
                        FastCueHoldSeconds,
                        motionEnabled ? FastCueExitSeconds : 0f,
                        motionEnabled ? FastTransitionSeconds : 0f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(speed), speed,
                        "Unknown product presentation speed.");
            }
        }
    }

    public sealed class ProductPresentationController : MonoBehaviour, IProductFeedbackSink,
        IProductSettingsApplier
    {
        private const float CueStartScale = 0.92f;

        [SerializeField] private CanvasGroup? banner;
        [SerializeField] private Image? bannerImage;
        [SerializeField] private Text? bannerText;
        [SerializeField] private CanvasGroup? transition;
        [SerializeField] private MonoBehaviour? audioFeedbackBehaviour;

        private IProductAudioFeedback? configuredAudio;
        private Coroutine? cueCoroutine;
        private Coroutine? transitionCoroutine;
        private int cueGeneration;
        private int transitionGeneration;
        private bool transitionActive;
        private ProductPresentationPolicy policy = ProductPresentationPolicy.From(
            ProductPresentationSpeed.Normal, reducedMotion: false);

        public CanvasGroup Banner => banner ?? throw Missing(nameof(banner));
        public Image BannerImage => bannerImage ?? throw Missing(nameof(bannerImage));
        public Text BannerText => bannerText ?? throw Missing(nameof(bannerText));
        public CanvasGroup Transition => transition ?? throw Missing(nameof(transition));
        public ProductPresentationPolicy Policy => policy;
        public ProductFeedbackKind? LastKind { get; private set; }
        public string? LastKey { get; private set; }
        public bool IsTransitioning => transitionActive;

        public event System.Action<ProductFeedbackKind>? CuePresented;
        public event System.Action? TransitionCompleted;

        public void Configure(CanvasGroup configuredBanner, Image configuredBannerImage,
            Text configuredBannerText, CanvasGroup configuredTransition,
            IProductAudioFeedback audio)
        {
            banner = configuredBanner ?? throw new ArgumentNullException(nameof(configuredBanner));
            bannerImage = configuredBannerImage ??
                throw new ArgumentNullException(nameof(configuredBannerImage));
            bannerText = configuredBannerText ??
                throw new ArgumentNullException(nameof(configuredBannerText));
            transition = configuredTransition ??
                throw new ArgumentNullException(nameof(configuredTransition));
            configuredAudio = audio ?? throw new ArgumentNullException(nameof(audio));
            audioFeedbackBehaviour = audio as MonoBehaviour;
            InitializeVisuals();
        }

        public void Apply(ProductSettings settings) => ApplySettings(settings);

        public void ApplySettings(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            policy = ProductPresentationPolicy.From(settings);
            AudioFeedback.ApplySettings(settings);
            if (!policy.MotionEnabled && banner != null)
                banner.transform.localScale = Vector3.one;
            if (transitionActive && policy.TransitionSeconds <= 0f)
                FinishTransition(notify: true);
        }

        public void Play(ProductFeedbackKind kind)
        {
            ValidateConfiguration();
            ProductFeedbackPresentation presentation = ProductPresentationCatalog.Get(kind);
            CancelCue(clearVisual: false);

            LastKind = kind;
            LastKey = presentation.Key;
            BannerText.text = presentation.DisplayText;
            BannerImage.color = ColorFor(presentation.Palette);
            Banner.alpha = 1f;
            Banner.interactable = false;
            Banner.blocksRaycasts = false;
            Banner.transform.localScale = policy.MotionEnabled
                ? Vector3.one * CueStartScale
                : Vector3.one;

            AudioFeedback.Play(kind);
            CuePresented?.Invoke(kind);

            if (!Application.isPlaying || !isActiveAndEnabled) return;
            int generation = ++cueGeneration;
            cueCoroutine = StartCoroutine(RunCue(generation));
        }

        public void BeginScreenTransition()
        {
            ValidateConfiguration();
            CancelTransition(clearVisual: true, notify: false);
            transitionActive = true;
            Transition.interactable = false;
            Transition.blocksRaycasts = false;

            if (!Application.isPlaying || !isActiveAndEnabled ||
                policy.TransitionSeconds <= 0f)
            {
                FinishTransition(notify: true);
                return;
            }

            Transition.alpha = 1f;
            int generation = ++transitionGeneration;
            transitionCoroutine = StartCoroutine(RunTransition(generation));
        }

        public void Cancel()
        {
            CancelCue(clearVisual: true);
            CancelTransition(clearVisual: true, notify: false);
        }

        private IProductAudioFeedback AudioFeedback
        {
            get
            {
                if (configuredAudio != null) return configuredAudio;
                if (audioFeedbackBehaviour is IProductAudioFeedback persistedAudio)
                {
                    configuredAudio = persistedAudio;
                    return persistedAudio;
                }
                throw Missing(nameof(audioFeedbackBehaviour));
            }
        }

        private void Awake()
        {
            if (audioFeedbackBehaviour is IProductAudioFeedback persistedAudio)
                configuredAudio = persistedAudio;
            if (IsConfigured) InitializeVisuals();
        }

        private void Start() => ValidateConfiguration();

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            cueGeneration++;
            transitionGeneration++;
            CuePresented = null;
            TransitionCompleted = null;
        }

        private IEnumerator RunCue(int generation)
        {
            float elapsed = 0f;
            while (generation == cueGeneration)
            {
                float duration = policy.CueEnterSeconds;
                if (!policy.MotionEnabled || duration <= 0f || elapsed >= duration) break;
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                Banner.transform.localScale = Vector3.one *
                    Mathf.Lerp(CueStartScale, 1f, progress);
                yield return null;
            }
            if (generation != cueGeneration) yield break;
            Banner.transform.localScale = Vector3.one;

            elapsed = 0f;
            while (generation == cueGeneration && elapsed < policy.CueHoldSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (generation != cueGeneration) yield break;

            elapsed = 0f;
            while (generation == cueGeneration)
            {
                float duration = policy.CueExitSeconds;
                if (!policy.MotionEnabled || duration <= 0f || elapsed >= duration) break;
                elapsed += Time.unscaledDeltaTime;
                Banner.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            if (generation != cueGeneration) yield break;
            Banner.alpha = 0f;
            Banner.transform.localScale = Vector3.one;
            cueCoroutine = null;
        }

        private IEnumerator RunTransition(int generation)
        {
            float elapsed = 0f;
            while (generation == transitionGeneration)
            {
                float duration = policy.TransitionSeconds;
                if (!policy.MotionEnabled || duration <= 0f || elapsed >= duration) break;
                elapsed += Time.unscaledDeltaTime;
                Transition.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            if (generation == transitionGeneration)
            {
                // Do not ask Unity to stop the coroutine that is currently completing.
                transitionCoroutine = null;
                FinishTransition(notify: true);
            }
        }

        private void FinishTransition(bool notify)
        {
            if (!transitionActive && transitionCoroutine == null)
            {
                if (transition != null)
                {
                    transition.alpha = 0f;
                    transition.interactable = false;
                    transition.blocksRaycasts = false;
                }
                return;
            }

            transitionGeneration++;
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
            transitionActive = false;
            Transition.alpha = 0f;
            Transition.interactable = false;
            Transition.blocksRaycasts = false;
            if (notify) TransitionCompleted?.Invoke();
        }

        private void CancelCue(bool clearVisual)
        {
            cueGeneration++;
            if (cueCoroutine != null)
            {
                StopCoroutine(cueCoroutine);
                cueCoroutine = null;
            }
            if (!clearVisual || banner == null) return;
            banner.alpha = 0f;
            banner.interactable = false;
            banner.blocksRaycasts = false;
            banner.transform.localScale = Vector3.one;
        }

        private void CancelTransition(bool clearVisual, bool notify)
        {
            bool wasActive = transitionActive || transitionCoroutine != null;
            transitionGeneration++;
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
            transitionActive = false;
            if (clearVisual && transition != null)
            {
                transition.alpha = 0f;
                transition.interactable = false;
                transition.blocksRaycasts = false;
            }
            if (wasActive && notify) TransitionCompleted?.Invoke();
        }

        private void InitializeVisuals()
        {
            if (banner != null)
            {
                banner.alpha = 0f;
                banner.interactable = false;
                banner.blocksRaycasts = false;
                banner.transform.localScale = Vector3.one;
            }
            if (bannerImage != null) bannerImage.raycastTarget = false;
            if (bannerText != null) bannerText.raycastTarget = false;
            if (transition != null)
            {
                transition.alpha = 0f;
                transition.interactable = false;
                transition.blocksRaycasts = false;
            }
        }

        private bool IsConfigured => banner != null && bannerImage != null && bannerText != null &&
            transition != null && (configuredAudio != null ||
                audioFeedbackBehaviour is IProductAudioFeedback);

        private void ValidateConfiguration()
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "Product presentation controller is not configured.");
        }

        private static Color ColorFor(ProductPresentationPalette palette)
        {
            switch (palette)
            {
                case ProductPresentationPalette.Navigation:
                    return new Color(0.10f, 0.40f, 0.58f, 0.97f);
                case ProductPresentationPalette.Positive:
                    return new Color(0.08f, 0.45f, 0.25f, 0.97f);
                case ProductPresentationPalette.Negative:
                    return new Color(0.62f, 0.23f, 0.08f, 0.97f);
                case ProductPresentationPalette.Card:
                    return new Color(0.08f, 0.42f, 0.38f, 0.97f);
                case ProductPresentationPalette.Draw:
                    return new Color(0.12f, 0.32f, 0.58f, 0.97f);
                case ProductPresentationPalette.Wild:
                    return new Color(0.47f, 0.20f, 0.58f, 0.97f);
                case ProductPresentationPalette.Cpu:
                    return new Color(0.48f, 0.34f, 0.08f, 0.97f);
                case ProductPresentationPalette.Win:
                    return new Color(0.18f, 0.48f, 0.20f, 0.97f);
                case ProductPresentationPalette.Lose:
                    return new Color(0.48f, 0.16f, 0.18f, 0.97f);
                case ProductPresentationPalette.Error:
                    return new Color(0.58f, 0.08f, 0.10f, 0.97f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(palette), palette,
                        "Unknown presentation palette.");
            }
        }

        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException("Product presentation control is not configured: " + name);
    }
}
