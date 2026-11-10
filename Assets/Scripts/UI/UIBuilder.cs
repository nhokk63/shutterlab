// UIBuilder.cs — ShuttleLab (Unity 6)
// Helper tĩnh dùng chung cho toàn bộ UI: dựng uGUI bằng code (Canvas,
// panel, Text, Button, Slider, Toggle, InputField) vì scene được
// SceneBuilder tạo sau — không phụ thuộc prefab hay Inspector.
// KHÔNG chứa logic game; chỉ là factory + bảng màu + font mặc định.
// Phụ thuộc: UnityEngine.UI, UnityEngine.EventSystems. Không phụ thuộc
// module game nào (Core/Match/Players) để tránh vòng lặp.

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShuttleLab.UI
{
    public static class UIBuilder
    {
        // --- Bảng màu (tối, điểm xanh lá sân cầu) --------------------------
        public static readonly Color PanelDark = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color PanelSolid = new Color(0.05f, 0.09f, 0.07f, 0.95f);
        public static readonly Color ButtonNormal = new Color(0.10f, 0.16f, 0.12f, 0.95f);
        public static readonly Color ButtonSelected = new Color(0.16f, 0.55f, 0.30f, 0.95f);
        public static readonly Color Accent = new Color(0.20f, 0.75f, 0.38f);
        public static readonly Color AccentText = new Color(0.55f, 1f, 0.65f);
        public static readonly Color TextWhite = Color.white;
        public static readonly Color TextDim = new Color(0.75f, 0.78f, 0.75f);
        public static readonly Color Danger = new Color(0.75f, 0.25f, 0.22f);
        public static readonly Color BarGood = new Color(0.25f, 0.80f, 0.40f);
        public static readonly Color BarLow = new Color(0.95f, 0.75f, 0.20f);
        public static readonly Color BarEmpty = new Color(0.90f, 0.25f, 0.20f);
        public static readonly Color SliderTrack = new Color(0.20f, 0.24f, 0.21f, 0.95f);

        private static Font _defaultFont;

        /// <summary>Font mặc định của Unity (LegacyRuntime) — có sẵn mọi build.</summary>
        public static Font DefaultFont
        {
            get
            {
                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_defaultFont == null)
                        _defaultFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "sans-serif" }, 16);
                }
                return _defaultFont;
            }
        }

        /// <summary>Đảm bảo scene có EventSystem (bắt buộc để bấm được UI).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        /// <summary>Canvas ScreenSpaceOverlay, scaler 1280x720 (landscape).</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 10)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static RectTransform Place(RectTransform rt,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        /// <summary>Panel chữ nhật có nền màu (Image).</summary>
        public static RectTransform CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return Place(go.GetComponent<RectTransform>(),
                anchorMin, anchorMax, anchoredPosition, sizeDelta);
        }

        /// <summary>Text uGUI (dùng font mặc định).</summary>
        public static Text CreateText(Transform parent, string name, string text,
            int fontSize, Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.raycastTarget = false;
            Place(go.GetComponent<RectTransform>(),
                anchorMin, anchorMax, anchoredPosition, sizeDelta);
            return t;
        }

        /// <summary>Nút có nhãn; trả về Button (Text con đã gắn sẵn).</summary>
        public static Button CreateButton(Transform parent, string name, string label,
            int fontSize, UnityAction onClick,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonNormal;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = new Color(0.18f, 0.30f, 0.22f, 0.95f);
            colors.pressedColor = ButtonSelected;
            colors.selectedColor = ButtonSelected;
            colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.55f);
            button.colors = colors;
            Place(go.GetComponent<RectTransform>(),
                anchorMin, anchorMax, anchoredPosition, sizeDelta);

            var labelText = CreateText(go.transform, "Label", label, fontSize,
                TextWhite, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelText.raycastTarget = false;

            // Tiếng click cho mọi nút (AudioManager tồn tại thì phát,
            // không thì thôi — nút vẫn bấm được bình thường).
            button.onClick.AddListener(() =>
            {
                if (ShuttleLab.Systems.AudioManager.HasInstance)
                    ShuttleLab.Systems.AudioManager.Instance.PlayClick();
            });
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>Đổi màu nền của nút (dùng highlight lựa chọn đang chọn).</summary>
        public static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? ButtonSelected : ButtonNormal;
        }

        /// <summary>
        /// Slider ngang: Background + Fill (Filled) + Handle. Trả về Slider.
        /// Cấu trúc con đúng chuẩn Unity để Slider tự tìm fillRect/handleRect.
        /// </summary>
        public static Slider CreateSlider(Transform parent, string name,
            float min, float max, float value,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            Place(go.GetComponent<RectTransform>(),
                anchorMin, anchorMax, anchoredPosition, sizeDelta);

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = SliderTrack;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.anchoredPosition = Vector2.zero;
            fillAreaRt.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = Accent;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = Vector2.zero;
            slider.fillRect = fillRt;

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.anchoredPosition = Vector2.zero;
            handleAreaRt.sizeDelta = new Vector2(-20f, 0f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0.5f, 0.5f);
            handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.anchoredPosition = Vector2.zero;
            handleRt.sizeDelta = new Vector2(20f, 28f);
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImage;

            return slider;
        }

        /// <summary>Toggle có nhãn bên phải. Trả về Toggle.</summary>
        public static Toggle CreateToggle(Transform parent, string name, string label,
            int fontSize, bool isOn,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bgImage = go.AddComponent<Image>();
            bgImage.color = SliderTrack;
            var toggle = go.AddComponent<Toggle>();
            toggle.isOn = isOn;
            toggle.targetGraphic = bgImage;
            Place(go.GetComponent<RectTransform>(),
                anchorMin, anchorMax, anchoredPosition, sizeDelta);

            var check = new GameObject("Checkmark");
            check.transform.SetParent(go.transform, false);
            var checkImage = check.AddComponent<Image>();
            checkImage.color = Accent;
            var checkRt = check.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0.15f, 0.15f);
            checkRt.anchorMax = new Vector2(0.85f, 0.85f);
            checkRt.anchoredPosition = Vector2.zero;
            checkRt.sizeDelta = Vector2.zero;
            toggle.graphic = checkImage;

            // Ô vuông: toggle là hình vuông 32x32, nhãn nằm bên phải ngoài ô.
            var labelText = CreateText(go.transform, "Label", label, fontSize,
                TextWhite, TextAnchor.MiddleLeft,
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(220f, 0f), new Vector2(440f, 0f));
            labelText.raycastTarget = false;
            return toggle;
        }

        /// <summary>
        /// InputField một dòng: nền + Text con + Placeholder con.
        /// Trả về InputField (textComponent đã gán).
        /// </summary>
        public static InputField CreateInputField(Transform parent, string name,
            string placeholder, string initialText, int characterLimit,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bgImage = go.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);
            var input = go.AddComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;
            Place(go.GetComponent<RectTransform>(),
                anchorMin, anchorMax, anchoredPosition, sizeDelta);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = 24;
            text.color = TextWhite;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = new Vector2(-20f, 0f);
            input.textComponent = text;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var ph = phGo.AddComponent<Text>();
            ph.font = DefaultFont;
            ph.fontSize = 24;
            ph.color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            ph.text = placeholder;
            ph.alignment = TextAnchor.MiddleLeft;
            ph.raycastTarget = false;
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.anchoredPosition = Vector2.zero;
            phRt.sizeDelta = new Vector2(-20f, 0f);
            input.placeholder = ph;

            input.text = initialText;
            return input;
        }
    }
}
