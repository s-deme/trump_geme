#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductAudioControllerTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        [Test]
        public void InitializeConfiguresSeparateTwoDimensionalCategorySources()
        {
            AudioRig rig = CreateRig();

            AssertAll(() =>
            {
                Assert.That(rig.Controller.IsInitialized, Is.True);
                Assert.That(rig.Music.playOnAwake, Is.False);
                Assert.That(rig.Music.loop, Is.True);
                Assert.That(rig.Music.spatialBlend, Is.Zero);
                Assert.That(rig.Music.dopplerLevel, Is.Zero);
                Assert.That(rig.Music.clip, Is.SameAs(rig.MusicLoop));
                Assert.That(rig.Sfx.playOnAwake, Is.False);
                Assert.That(rig.Sfx.loop, Is.False);
                Assert.That(rig.Sfx.spatialBlend, Is.Zero);
                Assert.That(rig.Sfx.dopplerLevel, Is.Zero);
                Assert.That(rig.Sfx.clip, Is.Null);
                Assert.That(rig.Controller.MusicVolume, Is.Zero);
                Assert.That(rig.Controller.SfxVolume, Is.Zero);
            });

            rig.Controller.Initialize();
            Assert.That(rig.Controller.IsInitialized, Is.True);
        }

        [Test]
        public void ApplySettingsSynchronouslyUpdatesCategoryVolumesAndZeroMutes()
        {
            AudioRig rig = CreateRig();
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");

            rig.Controller.ApplySettings(defaults);

            AssertAll(() =>
            {
                Assert.That(rig.Controller.MusicVolumePercent, Is.EqualTo(60));
                Assert.That(rig.Controller.SfxVolumePercent, Is.EqualTo(80));
                Assert.That(rig.Controller.MusicVolume, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(rig.Controller.SfxVolume, Is.EqualTo(0.8f).Within(0.0001f));
            });

            rig.Controller.ApplySettings(defaults.WithAudio(0, 0, 0));

            AssertAll(() =>
            {
                Assert.That(rig.Controller.MusicVolumePercent, Is.Zero);
                Assert.That(rig.Controller.SfxVolumePercent, Is.Zero);
                Assert.That(rig.Music.volume, Is.Zero);
                Assert.That(rig.Sfx.volume, Is.Zero);
                Assert.That(rig.Music.isPlaying, Is.False);
                Assert.That(rig.Sfx.isPlaying, Is.False);
            });

            rig.Controller.ApplySettings(defaults.WithAudio(100, 100, 100));
            AssertAll(() =>
            {
                Assert.That(rig.Controller.MusicVolume, Is.EqualTo(1f));
                Assert.That(rig.Controller.SfxVolume, Is.EqualTo(1f));
            });
        }

        [Test]
        public void PlayDispatchesEveryConfiguredCueThroughStableTestSeams()
        {
            AudioRig rig = CreateRig();
            rig.Controller.ApplySettings(ProductSettings.CreateDefaults("en-US"));
            var observed = new List<ProductFeedbackKind>();
            rig.Controller.CuePlayed += observed.Add;
            ProductFeedbackKind[] expected = Enum.GetValues(typeof(ProductFeedbackKind))
                .Cast<ProductFeedbackKind>().ToArray();

            foreach (ProductFeedbackKind kind in expected) rig.Controller.Play(kind);

            AssertAll(() =>
            {
                Assert.That(observed, Is.EqualTo(expected));
                Assert.That(rig.Controller.LastCue, Is.EqualTo(ProductFeedbackKind.Error));
            });
        }

        [Test]
        public void ConfigureRejectsMissingDuplicateAndSharedSourceContracts()
        {
            ProductAudioClipBinding[] valid = CreateCueBindings();
            AudioClip musicLoop = Clip("music");
            GameObject root = CreateGameObject("InvalidAudioRig");
            root.SetActive(false);
            AudioSource music = root.AddComponent<AudioSource>();
            AudioSource sfx = root.AddComponent<AudioSource>();
            ProductAudioController controller = root.AddComponent<ProductAudioController>();
            ProductAudioClipBinding[] missing = valid.Take(valid.Length - 1).ToArray();
            ProductAudioClipBinding[] duplicate = (ProductAudioClipBinding[])valid.Clone();
            duplicate[duplicate.Length - 1] = new ProductAudioClipBinding(
                ProductFeedbackKind.Navigation, Clip("duplicate"));

            AssertAll(() =>
            {
                Assert.That(() => controller.Configure(music, sfx, musicLoop, missing),
                    Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => controller.Configure(music, sfx, musicLoop, duplicate),
                    Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => controller.Configure(music, music, musicLoop, valid),
                    Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => controller.Play((ProductFeedbackKind)999),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void AudioConfigurationChangeRestoresSourcesWithoutLosingSettings()
        {
            AudioRig rig = CreateRig();
            ProductSettings settings = ProductSettings.CreateDefaults("en-US")
                .WithAudio(80, 35, 65);
            rig.Controller.ApplySettings(settings);
            rig.Music.volume = 0f;
            rig.Sfx.volume = 0f;
            rig.Music.loop = false;
            rig.Music.clip = null;
            rig.Sfx.spatialBlend = 1f;

            rig.Controller.RefreshAfterAudioConfigurationChange(deviceWasChanged: true);

            AssertAll(() =>
            {
                Assert.That(rig.Controller.AudioConfigurationRefreshCount, Is.EqualTo(1));
                Assert.That(rig.Music.volume, Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(rig.Sfx.volume, Is.EqualTo(0.65f).Within(0.001f));
                Assert.That(rig.Music.loop, Is.True);
                Assert.That(rig.Music.clip, Is.SameAs(rig.Controller.MusicLoop));
                Assert.That(rig.Sfx.spatialBlend, Is.Zero);
                Assert.That(rig.Controller.MusicVolumePercent, Is.EqualTo(35));
                Assert.That(rig.Controller.SfxVolumePercent, Is.EqualTo(65));
            });
        }

        [Test]
        public void UiEmitterResolvesParentSinkAndIgnoresDisabledControls()
        {
            AudioRig rig = CreateRig();
            rig.Root.SetActive(true);
            var eventSystemObject = CreateGameObject("EventSystem");
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var controlObject = new GameObject("Control", typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(ProductUiFeedbackEmitter));
            created.Add(controlObject);
            controlObject.transform.SetParent(rig.Root.transform, false);
            Button button = controlObject.GetComponent<Button>();
            ProductUiFeedbackEmitter emitter =
                controlObject.GetComponent<ProductUiFeedbackEmitter>();
            var observed = new List<ProductFeedbackKind>();
            rig.Controller.CuePlayed += observed.Add;

            emitter.OnMove(new AxisEventData(eventSystem));
            emitter.OnSubmit(new BaseEventData(eventSystem));
            emitter.OnPointerClick(new PointerEventData(eventSystem)
                { button = PointerEventData.InputButton.Right });
            emitter.OnPointerClick(new PointerEventData(eventSystem)
                { button = PointerEventData.InputButton.Left });
            button.interactable = false;
            emitter.OnSubmit(new BaseEventData(eventSystem));
            button.interactable = true;
            emitter.SetSubmitFeedbackEnabled(false);
            emitter.OnMove(new AxisEventData(eventSystem));
            emitter.OnSubmit(new BaseEventData(eventSystem));
            emitter.OnPointerClick(new PointerEventData(eventSystem)
                { button = PointerEventData.InputButton.Left });

            Assert.That(observed, Is.EqualTo(new[]
            {
                ProductFeedbackKind.Navigation,
                ProductFeedbackKind.Submit,
                ProductFeedbackKind.Submit,
                ProductFeedbackKind.Navigation
            }));
        }

        private AudioRig CreateRig()
        {
            GameObject root = CreateGameObject("ProductAudio");
            root.SetActive(false);
            AudioSource music = root.AddComponent<AudioSource>();
            AudioSource sfx = root.AddComponent<AudioSource>();
            ProductAudioController controller = root.AddComponent<ProductAudioController>();
            AudioClip musicLoop = Clip("music_loop");
            controller.Configure(music, sfx, musicLoop, CreateCueBindings());
            return new AudioRig(root, controller, music, sfx, musicLoop);
        }

        private ProductAudioClipBinding[] CreateCueBindings()
        {
            return Enum.GetValues(typeof(ProductFeedbackKind))
                .Cast<ProductFeedbackKind>()
                .Select(kind => new ProductAudioClipBinding(kind,
                    Clip("cue_" + kind.ToString().ToLowerInvariant())))
                .ToArray();
        }

        private AudioClip Clip(string name)
        {
            AudioClip clip = AudioClip.Create(name, lengthSamples: 64, channels: 1,
                frequency: 44100, stream: false);
            created.Add(clip);
            return clip;
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            created.Add(gameObject);
            return gameObject;
        }

        private static void AssertAll(System.Action assertions) => assertions();

        private sealed class AudioRig
        {
            public GameObject Root { get; }
            public ProductAudioController Controller { get; }
            public AudioSource Music { get; }
            public AudioSource Sfx { get; }
            public AudioClip MusicLoop { get; }

            public AudioRig(GameObject root, ProductAudioController controller,
                AudioSource music, AudioSource sfx, AudioClip musicLoop)
            {
                Root = root;
                Controller = controller;
                Music = music;
                Sfx = sfx;
                MusicLoop = musicLoop;
            }
        }
    }
}
