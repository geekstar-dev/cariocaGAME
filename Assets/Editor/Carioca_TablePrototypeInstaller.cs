#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class Carioca_TablePrototypeInstaller
{
    private const string ScriptsFolder = "Assets/Scripts/CariocaRuntime";
    private const string PrefabsFolder = "Assets/Prefabs";
    private const string ScenesFolder = "Assets/Scenes";
    private const string Scene_GameTable = "GameTable";

    [MenuItem("Tools/CARIOCA/Build GameTable Prototype (Deck/Discard/Hand)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Sal de Play Mode antes de construir.");
            return;
        }

        try
        {
            EnsureFolders();
            WriteRuntimeScripts(overwrite: true);
            AssetDatabase.Refresh();

            BuildScene_GameTable();

            Debug.Log("✅ GameTable Prototype listo: mazo + descarte + mano + turnos (click to draw/discard). Abre Scenes/GameTable y Play.");
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ Build GameTable Prototype falló:\n" + ex);
        }
    }

    // ---------------- Folders ----------------
    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Scripts");
        EnsureFolder("Assets/Scripts", "CariocaRuntime");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Scenes");
    }

    private static void EnsureFolder(string parent, string name)
    {
        var path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    // ---------------- Scene Build ----------------
    private static void BuildScene_GameTable()
    {
        var scenePath = $"{ScenesFolder}/{Scene_GameTable}.unity";
        Scene scene;

        if (File.Exists(scenePath))
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        else
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        EnsureEventSystem_InputSystemFriendly();

        // Root
        var root = GameObject.Find("CariocaTableRoot");
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        root = new GameObject("CariocaTableRoot");

        // Canvas
        var canvas = CreateCanvas("Canvas");
        canvas.transform.SetParent(root.transform, false);

        // Top title
        var title = CreateTMPText(canvas.transform, "Title", "MESA (PROTOTIPO)", 52, TextAlignmentOptions.Center);
        AnchorRect(title.rectTransform, new Vector2(0.1f, 0.87f), new Vector2(0.9f, 0.97f));

        var hint = CreateTMPText(canvas.transform, "Hint",
            "Click MAZO o DESCARTE para robar (1 por turno). Click una carta en mano para DESCARTAR.",
            20, TextAlignmentOptions.Center);
        hint.color = new Color(1, 1, 1, 0.85f);
        AnchorRect(hint.rectTransform, new Vector2(0.1f, 0.81f), new Vector2(0.9f, 0.87f));

        // Middle area (Deck + Discard)
        var mid = new GameObject("MidArea", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        mid.transform.SetParent(canvas.transform, false);
        var midRt = mid.GetComponent<RectTransform>();
        AnchorRect(midRt, new Vector2(0.2f, 0.45f), new Vector2(0.8f, 0.78f));

        var hlg = mid.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 40;
        hlg.childAlignment = TextAnchor.MiddleCenter;

        // Piles
        var deckBtn = CreatePileButton(mid.transform, "DeckPile", "MAZO");
        var discardBtn = CreatePileButton(mid.transform, "DiscardPile", "DESCARTE");

        // Hand area
        var handPanel = new GameObject("HandPanel", typeof(RectTransform), typeof(Image));
        handPanel.transform.SetParent(canvas.transform, false);
        var handRt = handPanel.GetComponent<RectTransform>();
        AnchorRect(handRt, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.30f));
        handPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.22f);

        var handLayout = new GameObject("HandLayout", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        handLayout.transform.SetParent(handPanel.transform, false);
        var hlRt = handLayout.GetComponent<RectTransform>();
        AnchorRect(hlRt, new Vector2(0.02f, 0.1f), new Vector2(0.98f, 0.9f));

        var handHLG = handLayout.GetComponent<HorizontalLayoutGroup>();
        handHLG.spacing = 10;
        handHLG.childAlignment = TextAnchor.MiddleCenter;

        var fitter = handLayout.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Card prefab (UI)
        var cardPrefab = BuildCardPrefab();

        // Game Controller
        var controllerGO = new GameObject("GameTableController");
        controllerGO.transform.SetParent(root.transform, false);

        var controllerType = FindTypeOrThrow("CariocaRuntime.GameTableController");
        var controller = controllerGO.AddComponent(controllerType);

        // Assign references (reflection so you don't have to drag-drop)
        SetField(controller, "deckButton", deckBtn);
        SetField(controller, "discardButton", discardBtn);
        SetField(controller, "handLayout", handLayout.transform);
        SetField(controller, "cardPrefab", cardPrefab);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
    }

    private static GameObject BuildCardPrefab()
    {
        var prefabPath = $"{PrefabsFolder}/CardUI.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        var go = new GameObject("CardUI", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120, 170);

        var img = go.GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0.95f);

        var label = CreateTMPText(go.transform, "Label", "A♠", 34, TextAlignmentOptions.Center);
        label.color = Color.black;
        AnchorRect(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        var viewType = FindTypeOrThrow("CariocaRuntime.CardView");
        go.AddComponent(viewType);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------- UI helpers ----------------
    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static Button CreatePileButton(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240, 320);

        var img = go.GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0.22f);

        var t = CreateTMPText(go.transform, "Text", text, 28, TextAlignmentOptions.Center);
        t.color = new Color(1, 1, 1, 0.95f);
        AnchorRect(t.rectTransform, Vector2.zero, Vector2.one);

        return go.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateTMPText(Transform parent, string name, string text, int size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;

        return tmp;
    }

    private static void AnchorRect(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ---------------- EventSystem ----------------
    private static void EnsureEventSystem_InputSystemFriendly()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();

        var inputModuleType = FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        if (inputModuleType != null) esGO.AddComponent(inputModuleType);
        else esGO.AddComponent<StandaloneInputModule>();
    }

    // ---------------- Reflection helpers ----------------
    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    private static Type FindTypeOrThrow(string fullName)
    {
        var t = FindType(fullName);
        if (t == null) throw new Exception($"No encontré el tipo: {fullName} (¿compiló Scripts?)");
        return t;
    }

    private static void SetField(Component target, string fieldName, object value)
    {
        if (target == null) return;
        var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (f == null) throw new Exception($"No encontré el field '{fieldName}' en {target.GetType().Name}");
        f.SetValue(target, value);
    }

    // ---------------- Runtime scripts writer ----------------
    private static void WriteRuntimeScripts(bool overwrite)
    {
        Write("CardModel.cs", overwrite, @"
using System;

namespace CariocaRuntime
{
    public enum Suit { Clubs, Diamonds, Hearts, Spades }
    public enum Rank
    {
        Joker = 0,
        Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
        Jack, Queen, King
    }

    public readonly struct Card
    {
        public readonly Suit? Suit;   // null si Joker
        public readonly Rank Rank;

        public bool IsJoker => Rank == Rank.Joker;

        public Card(Suit? suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public override string ToString() => IsJoker ? ""JOKER"" : $""{Rank} of {Suit}"";
    }
}
");

        Write("ShuffleUtils.cs", overwrite, @"
using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    public static class ShuffleUtils
    {
        // Fisher–Yates Shuffle
        public static void FisherYates<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
");

        Write("Deck.cs", overwrite, @"
using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    public sealed class Deck
    {
        private readonly List<Card> _cards = new();
        private readonly System.Random _rng;

        public int Count => _cards.Count;

        public Deck(int seed = 0)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            // 13 por pinta
            foreach (Suit s in Enum.GetValues(typeof(Suit)))
            {
                _cards.Add(new Card(s, Rank.Ace));
                _cards.Add(new Card(s, Rank.Two));
                _cards.Add(new Card(s, Rank.Three));
                _cards.Add(new Card(s, Rank.Four));
                _cards.Add(new Card(s, Rank.Five));
                _cards.Add(new Card(s, Rank.Six));
                _cards.Add(new Card(s, Rank.Seven));
                _cards.Add(new Card(s, Rank.Eight));
                _cards.Add(new Card(s, Rank.Nine));
                _cards.Add(new Card(s, Rank.Ten));
                _cards.Add(new Card(s, Rank.Jack));
                _cards.Add(new Card(s, Rank.Queen));
                _cards.Add(new Card(s, Rank.King));
            }

            // 2 jokers
            _cards.Add(new Card(null, Rank.Joker));
            _cards.Add(new Card(null, Rank.Joker));
        }

        public void Shuffle()
        {
            ShuffleUtils.FisherYates(_cards, _rng);
        }

        public Card Draw()
        {
            if (_cards.Count == 0) throw new InvalidOperationException(""Deck vacío"");
            var c = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return c;
        }

        public void AddRange(IEnumerable<Card> cards)
        {
            _cards.AddRange(cards);
        }
    }
}
");

        Write("CardView.cs", overwrite, @"
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CariocaRuntime
{
    [RequireComponent(typeof(Button))]
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;

        public Card Card { get; private set; }

        public void Bind(Card card, System.Action<CardView> onClick)
        {
            Card = card;

            if (label == null)
                label = GetComponentInChildren<TextMeshProUGUI>(true);

            label.text = Format(card);

            var btn = GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke(this));
        }

        private static string Format(Card c)
        {
            if (c.IsJoker) return ""JOKER"";

            string r = c.Rank switch
            {
                Rank.Ace => ""A"",
                Rank.Jack => ""J"",
                Rank.Queen => ""Q"",
                Rank.King => ""K"",
                Rank.Ten => ""10"",
                Rank.Nine => ""9"",
                Rank.Eight => ""8"",
                Rank.Seven => ""7"",
                Rank.Six => ""6"",
                Rank.Five => ""5"",
                Rank.Four => ""4"",
                Rank.Three => ""3"",
                Rank.Two => ""2"",
                _ => ((int)c.Rank).ToString()
            };

            string s = c.Suit switch
            {
                Suit.Spades => ""♠"",
                Suit.Hearts => ""♥"",
                Suit.Diamonds => ""♦"",
                Suit.Clubs => ""♣"",
                _ => ""?""
            };

            return r + s;
        }
    }
}
");

        Write("GameTableController.cs", overwrite, @"
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CariocaRuntime
{
    public sealed class GameTableController : MonoBehaviour
    {
        [Header(""UI Refs (auto-set by installer)"")]
        [SerializeField] private Button deckButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Transform handLayout;
        [SerializeField] private GameObject cardPrefab;

        private readonly List<Card> _hand = new();
        private readonly List<Card> _discard = new();
        private Deck _deck;

        private bool _hasDrawnThisTurn;

        private void Start()
        {
            if (deckButton == null || discardButton == null || handLayout == null || cardPrefab == null)
            {
                Debug.LogError(""GameTableController: faltan referencias UI. Vuelve a correr Tools → CARIOCA → Build GameTable Prototype."");
                enabled = false;
                return;
            }

            deckButton.onClick.RemoveAllListeners();
            discardButton.onClick.RemoveAllListeners();
            deckButton.onClick.AddListener(DrawFromDeck);
            discardButton.onClick.AddListener(DrawFromDiscard);

            NewRound();
        }

        private void NewRound()
        {
            _deck = new Deck();
            _deck.Build54With2Jokers();
            _deck.Shuffle();

            _hand.Clear();
            _discard.Clear();
            ClearHandUI();

            // Deal 12
            for (int i = 0; i < 12; i++)
                _hand.Add(_deck.Draw());

            // First discard cannot be Joker
            Card first = _deck.Draw();
            int safety = 0;
            while (first.IsJoker && safety < 200)
            {
                // put joker back into deck bottom and reshuffle lightly
                _discard.Add(first); // temporarily store, but we will recycle all except top later
                first = _deck.Draw();
                safety++;
            }

            // We don't want Joker on top. If we collected jokers in discard temp, move them back & shuffle.
            if (_discard.Count > 0)
            {
                var temp = new List<Card>(_discard);
                _discard.Clear();

                _deck.AddRange(temp);
                _deck.Shuffle();
            }

            _discard.Add(first);
            _hasDrawnThisTurn = false;

            RefreshDiscardLabel();
            RefreshHandUI();
        }

        private void DrawFromDeck()
        {
            if (_hasDrawnThisTurn) return;

            EnsureDeckHasCards();
            _hand.Add(_deck.Draw());
            _hasDrawnThisTurn = true;

            RefreshHandUI();
        }

        private void DrawFromDiscard()
        {
            if (_hasDrawnThisTurn) return;
            if (_discard.Count == 0) return;

            var top = _discard[^1];
            _discard.RemoveAt(_discard.Count - 1);
            _hand.Add(top);

            _hasDrawnThisTurn = true;

            RefreshDiscardLabel();
            RefreshHandUI();
        }

        private void OnCardClicked(CardView view)
        {
            if (!_hasDrawnThisTurn) return; // rule: must draw before discard

            // remove from hand
            int idx = _hand.FindIndex(c => CardsEqual(c, view.Card));
            if (idx >= 0) _hand.RemoveAt(idx);

            // discard it
            _discard.Add(view.Card);
            _hasDrawnThisTurn = false;

            RefreshDiscardLabel();
            RefreshHandUI();

            // win check
            if (_hand.Count == 0)
                Debug.Log(""🏁 Ganaste la ronda (mano vacía). Luego agregamos puntaje."");
        }

        private static bool CardsEqual(Card a, Card b)
        {
            // Struct compare manual (Suit? + Rank)
            return a.Rank == b.Rank && a.Suit == b.Suit;
        }

        private void EnsureDeckHasCards()
        {
            if (_deck.Count > 0) return;

            if (_discard.Count <= 1)
            {
                Debug.LogWarning(""No hay cartas para reciclar."");
                return;
            }

            // Keep top discard, shuffle rest back into deck
            var top = _discard[^1];
            var recycle = _discard.GetRange(0, _discard.Count - 1);

            _discard.Clear();
            _discard.Add(top);

            _deck.AddRange(recycle);
            _deck.Shuffle();

            Debug.Log(""♻️ Mazo vacío: se revuelve descarte y se reutiliza como mazo (dejando 1 arriba)."");
        }

        private void ClearHandUI()
        {
            for (int i = handLayout.childCount - 1; i >= 0; i--)
                Destroy(handLayout.GetChild(i).gameObject);
        }

        private void RefreshHandUI()
        {
            ClearHandUI();

            foreach (var c in _hand)
            {
                var go = Instantiate(cardPrefab, handLayout);
                var view = go.GetComponent<CardView>();
                view.Bind(c, OnCardClicked);
            }
        }

        private void RefreshDiscardLabel()
        {
            // Change text on discard button child TMP (if exists)
            var tmp = discardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp == null) return;

            if (_discard.Count == 0) tmp.text = ""DESCARTE"";
            else
            {
                var top = _discard[^1];
                tmp.text = top.IsJoker ? ""DESCARTE\\n(JOKER)"" : ""DESCARTE\\n(""+top.ToString()+ "")"";
            }
        }
    }
}
");
    }

    private static void Write(string fileName, bool overwrite, string content)
    {
        var path = $"{ScriptsFolder}/{fileName}";
        if (!overwrite && File.Exists(path)) return;
        Directory.CreateDirectory(ScriptsFolder);
        File.WriteAllText(path, content.TrimStart());
    }
}
#endif
