using Core.Window;
using UnityEngine;
using UnityEngine.UI;

namespace TScript
{
    public class TestPopup : PopupBase
    {
        [Header("UI (Optional)")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleText;

        public override void Setup(object data)
        {
            base.Setup(data);

            if (titleText != null)
                titleText.text = data != null ? $"TestPopup: {data}" : "TestPopup";
        }

        protected override void Awake()
        {
            base.Awake();

            // If prefab doesn't wire these, try auto-find one time.
            if (closeButton == null)
                closeButton = GetComponentInChildren<Button>(includeInactive: true);

            if (titleText == null)
                titleText = GetComponentInChildren<Text>(includeInactive: true);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnCloseClicked()
        {
            Close();
        }
    }
}
