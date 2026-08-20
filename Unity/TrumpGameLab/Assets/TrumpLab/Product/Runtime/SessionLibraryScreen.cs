#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class SessionLibraryScreen : ProductScreen
    {
        [SerializeField] private Dropdown? slotDropdown;
        [SerializeField] private Text? detailLabel;
        [SerializeField] private Button? resumeButton;
        [SerializeField] private Button? replayButton;
        [SerializeField] private Button? deleteButton;
        [SerializeField] private Button? backButton;

        private IReadOnlyList<SessionSlotInfo> slots = Array.Empty<SessionSlotInfo>();
        private bool deleteArmed;
        private IProductText text = ProductTextCatalog.English;

        public override ScreenId Id => ScreenId.SessionLibrary;
        public string? SelectedSlotId => slots.Count == 0 ? null : slots[SlotDropdown.value].Id;
        public Dropdown SlotDropdown => slotDropdown ?? throw Missing(nameof(slotDropdown));
        public Text DetailLabel => detailLabel ?? throw Missing(nameof(detailLabel));

        public event System.Action<string>? ResumeRequested;
        public event System.Action<string>? ReplayRequested;
        public event System.Action<string>? DeleteRequested;
        public event System.Action? BackRequested;

        public void Configure(Dropdown dropdown, Text detail, Button resume, Button replay,
            Button delete, Button back)
        {
            slotDropdown = dropdown;
            detailLabel = detail;
            resumeButton = resume;
            replayButton = replay;
            deleteButton = delete;
            backButton = back;
            RefreshButtonText();
        }

        public void SetText(IProductText configuredText)
        {
            text = configuredText ?? throw new ArgumentNullException(nameof(configuredText));
            RefreshOptions();
            RefreshSelection();
            RefreshButtonText();
            if (deleteArmed)
            {
                SetDeleteButtonText("library.confirm_delete");
                DetailLabel.text = text.Get("library.confirm_delete_instruction");
            }
        }

        public void SetSlots(IReadOnlyList<SessionSlotInfo> available)
        {
            slots = available?.ToArray() ?? throw new ArgumentNullException(nameof(available));
            RefreshOptions();
            ResetDeleteConfirmation();
            RefreshSelection();
        }

        private void Awake()
        {
            if (slotDropdown == null || detailLabel == null || resumeButton == null ||
                replayButton == null || deleteButton == null || backButton == null)
                throw new InvalidOperationException("Session library controls are not configured.");
            slotDropdown.onValueChanged.AddListener(HandleSelectionChanged);
            resumeButton.onClick.AddListener(HandleResume);
            replayButton.onClick.AddListener(HandleReplay);
            deleteButton.onClick.AddListener(HandleDelete);
            backButton.onClick.AddListener(HandleBack);
        }

        private void OnDestroy()
        {
            if (slotDropdown != null) slotDropdown.onValueChanged.RemoveListener(HandleSelectionChanged);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(HandleResume);
            if (replayButton != null) replayButton.onClick.RemoveListener(HandleReplay);
            if (deleteButton != null) deleteButton.onClick.RemoveListener(HandleDelete);
            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
        }

        private void HandleSelectionChanged(int _) { ResetDeleteConfirmation(); RefreshSelection(); }
        private void HandleResume() { if (SelectedSlotId is string id) ResumeRequested?.Invoke(id); }
        private void HandleReplay() { if (SelectedSlotId is string id) ReplayRequested?.Invoke(id); }
        private void HandleBack() { ResetDeleteConfirmation(); BackRequested?.Invoke(); }

        private void HandleDelete()
        {
            if (!(SelectedSlotId is string id)) return;
            if (!deleteArmed)
            {
                deleteArmed = true;
                SetDeleteButtonText("library.confirm_delete");
                DetailLabel.text = text.Get("library.confirm_delete_instruction");
                return;
            }
            ResetDeleteConfirmation();
            DeleteRequested?.Invoke(id);
        }

        private void RefreshSelection()
        {
            bool available = slots.Count > 0;
            if (resumeButton != null) resumeButton.interactable = available;
            if (replayButton != null) replayButton.interactable = available;
            if (deleteButton != null) deleteButton.interactable = available;
            DetailLabel.text = available
                ? text.Get("library.instruction")
                : text.Get("library.empty");
        }

        private void ResetDeleteConfirmation()
        {
            deleteArmed = false;
            SetDeleteButtonText("library.delete");
        }

        private void SetDeleteButtonText(string key)
        {
            Text? label = deleteButton == null ? null : deleteButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text.Get(key);
        }

        private void RefreshOptions()
        {
            if (slotDropdown == null) return;
            int selected = slots.Count == 0 ? 0 : Mathf.Clamp(slotDropdown.value, 0,
                slots.Count - 1);
            SlotDropdown.ClearOptions();
            SlotDropdown.AddOptions(slots.Count == 0
                ? new List<string> { text.Get("library.empty_option") }
                : slots.Select(slot => text.Get("library.slot_option", slot.SavedAtUtc,
                    slot.Id.Substring(0, 8))).ToList());
            SlotDropdown.SetValueWithoutNotify(selected);
            SlotDropdown.RefreshShownValue();
            SlotDropdown.interactable = slots.Count > 0;
        }

        private void RefreshButtonText()
        {
            SetButtonText(resumeButton, "library.resume");
            SetButtonText(replayButton, "library.replay");
            SetButtonText(deleteButton, deleteArmed
                ? "library.confirm_delete"
                : "library.delete");
            SetButtonText(backButton, "common.back");
        }

        private void SetButtonText(Button? button, string key)
        {
            Text? label = button?.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text.Get(key);
        }

        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException("Session library control is not configured: " + name);
    }
}
