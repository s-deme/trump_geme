#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    [DisallowMultipleComponent]
    public sealed class ProductUiFeedbackEmitter : MonoBehaviour, IMoveHandler,
        ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] private bool submitFeedbackEnabled = true;
        private Selectable? selectable;
        private IProductFeedbackSink? sink;

        public bool SubmitFeedbackEnabled => submitFeedbackEnabled;

        public void Configure(IProductFeedbackSink configuredSink)
        {
            sink = configuredSink ?? throw new ArgumentNullException(nameof(configuredSink));
        }

        public void SetSubmitFeedbackEnabled(bool enabled) =>
            submitFeedbackEnabled = enabled;

        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));
            if (IsInteractable()) Emit(ProductFeedbackKind.Navigation);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));
            if (CanEmitSubmit())
                Emit(ProductFeedbackKind.Submit);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (CanEmitSubmit())
                Emit(ProductFeedbackKind.Submit);
        }

        private bool CanEmitSubmit()
        {
            if (!submitFeedbackEnabled || !IsInteractable()) return false;
            MatchScreen? match = GetComponentInParent<MatchScreen>();
            return match == null || !match.IsPresentationLocked;
        }

        private bool IsInteractable()
        {
            Selectable control = selectable ??= GetComponent<Selectable>() ??
                throw new InvalidOperationException(
                    "Product UI feedback requires a Selectable on the same object.");
            return control.IsActive() && control.IsInteractable();
        }

        private void Emit(ProductFeedbackKind kind) => RequireSink().Play(kind);

        private IProductFeedbackSink RequireSink()
        {
            if (IsAlive(sink)) return sink!;
            sink = null;
            foreach (MonoBehaviour behaviour in GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour != this &&
                    behaviour is IProductFeedbackSink candidate)
                {
                    sink = candidate;
                    return candidate;
                }
            }
            throw new InvalidOperationException(
                "Product UI feedback requires an IProductFeedbackSink on a parent object.");
        }

        private static bool IsAlive(IProductFeedbackSink? candidate)
        {
            if (candidate == null) return false;
            return !(candidate is UnityEngine.Object unityObject) || unityObject != null;
        }
    }
}
