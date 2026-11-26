using UnityEngine;
using UnityEngine.UI;

namespace Komikan
{
    /// <summary>
    /// Builds a minimal uGUI overlay entirely in code (no prefabs, no
    /// TextMeshPro): a Canvas with labels for whose turn it is and the Trewas
    /// eaten count ("x/6"), a result banner shown when the game ends, and a
    /// "Reiniciar" button wired to <see cref="GameManager.Restart"/>. Uses
    /// <see cref="Text"/> to avoid extra package dependencies.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        private GameManager _game;
        private Font _font;

        private Text _turnLabel;
        private Text _eatenLabel;
        private Text _bannerLabel;
        private Button _restartButton;

        public void Configure(GameManager game)
        {
            _game = game;
            BuildUI();

            _game.OnStateChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.OnStateChanged -= Refresh;
            }
        }

        private void BuildUI()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                // Fallback name used by older Unity versions.
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            var canvasGO = new GameObject("KomikanCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGO.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            _turnLabel = CreateLabel(canvasGO.transform, "TurnLabel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f),
                new Vector2(500f, 40f), TextAnchor.UpperLeft, 28);

            _eatenLabel = CreateLabel(canvasGO.transform, "EatenLabel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -60f),
                new Vector2(500f, 40f), TextAnchor.UpperLeft, 28);

            _bannerLabel = CreateLabel(canvasGO.transform, "BannerLabel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
                new Vector2(700f, 90f), TextAnchor.MiddleCenter, 48);
            _bannerLabel.gameObject.SetActive(false);

            _restartButton = CreateButton(canvasGO.transform, "RestartButton", "Reiniciar",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                new Vector2(220f, 60f));
            _restartButton.onClick.AddListener(() => _game.Restart());
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private Text CreateLabel(
            Transform parent, string objName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
            Vector2 size, TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = string.Empty;
            return text;
        }

        private Button CreateButton(
            Transform parent, string objName, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.20f, 0.45f, 0.85f, 0.95f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            return button;
        }

        private void Refresh()
        {
            if (_game == null)
            {
                return;
            }

            _eatenLabel.text = $"Trewas comidas: {_game.TrewasEaten}/{GameManager.TrewasToWin}";

            switch (_game.Status)
            {
                case GameStatus.Playing:
                    _turnLabel.text = _game.CurrentTurn == Turn.Pangui
                        ? "Turno: Pangui (tu)"
                        : "Turno: Trewas (IA)";
                    _bannerLabel.gameObject.SetActive(false);
                    break;

                case GameStatus.PanguiWins:
                    _turnLabel.text = "Fin de la partida";
                    _bannerLabel.text = "\u00a1Pangui gana!";
                    _bannerLabel.color = SpriteFactory.PanguiColor;
                    _bannerLabel.gameObject.SetActive(true);
                    break;

                case GameStatus.TrewasWin:
                    _turnLabel.text = "Fin de la partida";
                    _bannerLabel.text = "\u00a1Las Trewas ganan!";
                    _bannerLabel.color = SpriteFactory.TrewaColor;
                    _bannerLabel.gameObject.SetActive(true);
                    break;
            }
        }
    }
}
