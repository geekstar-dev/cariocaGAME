#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class Carioca_BancaInstaller
{
    private const string ScriptsFolder = "Assets/Scripts/CariocaRuntime";
    private const string PrefabsFolder = "Assets/Prefabs";
    private const string ScenesFolder = "Assets/Scenes";
    private const string Scene_GameTable = "GameTable";

    [MenuItem("Tools/CARIOCA/Upgrade GameTable (Bank/Drop/Add/Sort)")]
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

            UpgradeScene_GameTable();

            Debug.Log("✅ Upgrade listo: banca + seleccionar cartas + BAJAR + AGREGAR + ORDENAR.");
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ Upgrade falló:\n" + ex);
        }
    }

    // ---------------- Folders ----------------
    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Scripts");
        EnsureFolder("Assets/Scripts", "CariocaRuntime");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Editor");
    }

    private static void EnsureFolder(string parent, string name)
    {
        var path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    // ---------------- Scene Upgrade ----------------
    private static void UpgradeScene_GameTable()
    {
        var scenePath = $"{ScenesFolder}/{Scene_GameTable}.unity";
        if (!File.Exists(scenePath))
            throw new Exception($"No existe {scenePath}. Primero crea la mesa con el instalador anterior.");

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        EnsureEventSystem_InputSystemFriendly();

        var root = GameObject.Find("CariocaTableRoot");
        if (root == null) throw new Exception("No encontré CariocaTableRoot en la escena.");

        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) throw new Exception("No encontré Canvas en la escena.");

        // Find existing controller and card prefab reference (from previous installer)
        var controllerGO = GameObject.Find("GameTableController");
        if (controllerGO == null) throw new Exception("No encontré GameTableController en la escena.");

        // Create/replace Bank UI
        var oldBank = GameObject.Find("BankPanel");
        if (oldBank != null) UnityEngine.Object.DestroyImmediate(oldBank);

        var bankPanel = new GameObject("BankPanel", typeof(RectTransform), typeof(Image));
        bankPanel.transform.SetParent(canvas.transform, false);
        var bankRt = bankPanel.GetComponent<RectTransform>();
        AnchorRect(bankRt, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.80f));
        bankPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);

        var bankTitle = CreateTMPText(bankPanel.transform, "BankTitle", "BANCA", 24, TextAlignmentOptions.Left);
        bankTitle.color = new Color(1,1,1,0.9f);
        AnchorRect(bankTitle.rectTransform, new Vector2(0.02f, 0.78f), new Vector2(0.4f, 0.98f));

        var bankLayoutGO = new GameObject("BankLayout", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bankLayoutGO.transform.SetParent(bankPanel.transform, false);
        var bankLayoutRt = bankLayoutGO.GetComponent<RectTransform>();
        AnchorRect(bankLayoutRt, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.76f));
        var bankHLG = bankLayoutGO.GetComponent<HorizontalLayoutGroup>();
        bankHLG.spacing = 14;
        bankHLG.childAlignment = TextAnchor.MiddleLeft;

        // Controls panel (buttons)
        var oldControls = GameObject.Find("ControlsPanel");
        if (oldControls != null) UnityEngine.Object.DestroyImmediate(oldControls);

        var controls = new GameObject("ControlsPanel", typeof(RectTransform), typeof(Image));
        controls.transform.SetParent(canvas.transform, false);
        var cRt = controls.GetComponent<RectTransform>();
        AnchorRect(cRt, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.44f));
        controls.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);

        var btnRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(controls.transform, false);
        var brRt = btnRow.GetComponent<RectTransform>();
        AnchorRect(brRt, new Vector2(0.02f, 0.1f), new Vector2(0.98f, 0.9f));
        var row = btnRow.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 12;
        row.childAlignment = TextAnchor.MiddleCenter;

        var dropBtn = CreateUIButton(btnRow.transform, "DropButton", "BAJAR", 28);
        var addBtn  = CreateUIButton(btnRow.transform, "AddButton", "AGREGAR A BANCA", 24);
        var sortBtn = CreateUIButton(btnRow.transform, "SortButton", "ORDENAR", 28);

        // Wire into controller via reflection (new controller type)
        var newType = FindTypeOrThrow("CariocaRuntime.GameTableControllerV2");

        // Remove old controller if present
        var oldController = controllerGO.GetComponent(FindType("CariocaRuntime.GameTableController"));
        if (oldController != null) UnityEngine.Object.DestroyImmediate(oldController);

        var v2 = controllerGO.GetComponent(newType);
        if (v2 == null) v2 = controllerGO.AddComponent(newType);

        // find old fields (existing references already serialized on GO by previous script)
        // We keep deck/discard/handLayout/cardPrefab from old controller through existing serialized fields in scene
        // But since we changed script type, we reassign by finding objects.
        var deckButton = GameObject.Find("DeckPile")?.GetComponent<Button>();
        var discardButton = GameObject.Find("DiscardPile")?.GetComponent<Button>();
        var handLayout = GameObject.Find("HandLayout")?.transform;

        if (deckButton == null || discardButton == null || handLayout == null)
            throw new Exception("No pude encontrar DeckPile / DiscardPile / HandLayout. ¿Se construyó la mesa base?");

        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/CardUI.prefab");
        if (cardPrefab == null) throw new Exception("No encontré Prefabs/CardUI.prefab");

        SetField((Component)v2, "deckButton", deckButton);
        SetField((Component)v2, "discardButton", discardButton);
        SetField((Component)v2, "handLayout", handLayout);
        SetField((Component)v2, "cardPrefab", cardPrefab);

        SetField((Component)v2, "bankLayout", bankLayoutGO.transform);
        SetField((Component)v2, "dropButton", dropBtn);
        SetField((Component)v2, "addButton", addBtn);
        SetField((Component)v2, "sortButton", sortBtn);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
    }

    // ---------------- UI helpers ----------------
    private static Button CreateUIButton(Transform parent, string name, string text, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320, 80);

        var img = go.GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0.92f);

        var tmp = CreateTMPText(go.transform, "Text", text, fontSize, TextAlignmentOptions.Center);
        tmp.color = Color.black;
        AnchorRect(tmp.rectTransform, Vector2.zero, Vector2.one);

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

    // ---------------- Reflection ----------------
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
        if (t == null) throw new Exception($"No encontré el tipo: {fullName}");
        return t;
    }

    private static void SetField(Component target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (f == null) throw new Exception($"No encontré el field '{fieldName}' en {target.GetType().Name}");
        f.SetValue(target, value);
    }

    // ---------------- Script writer ----------------
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

            _cards.Add(new Card(null, Rank.Joker));
            _cards.Add(new Card(null, Rank.Joker));
        }

        public void Shuffle() => ShuffleUtils.FisherYates(_cards, _rng);

        public Card Draw()
        {
            if (_cards.Count == 0) throw new InvalidOperationException(""Deck vacío"");
            var c = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return c;
        }

        public void AddRange(IEnumerable<Card> cards) => _cards.AddRange(cards);
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
        [SerializeField] private Image background;

        public Card Card { get; private set; }
        public bool Selected { get; private set; }

        public void Bind(Card card, System.Action<CardView> onClick, bool selected)
        {
            Card = card;
            Selected = selected;

            if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
            if (background == null) background = GetComponent<Image>();

            label.text = Format(card);
            ApplySelectedStyle();

            var btn = GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void SetSelected(bool value)
        {
            Selected = value;
            ApplySelectedStyle();
        }

        private void ApplySelectedStyle()
        {
            if (background == null) return;
            background.color = Selected ? new Color(1f, 0.95f, 0.65f, 0.98f) : new Color(1f, 1f, 1f, 0.95f);
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

        Write("BankModels.cs", overwrite, @"
using System.Collections.Generic;

namespace CariocaRuntime
{
    public enum BankGroupType { Trio, Run }

    public sealed class BankGroup
    {
        public BankGroupType Type;
        public readonly List<Card> Cards = new();

        public BankGroup(BankGroupType type, IEnumerable<Card> cards)
        {
            Type = type;
            Cards.AddRange(cards);
        }

        public int JokerCount()
        {
            int n = 0;
            for (int i = 0; i < Cards.Count; i++)
                if (Cards[i].IsJoker) n++;
            return n;
        }
    }
}
");

        Write("RulesValidator.cs", overwrite, @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace CariocaRuntime
{
    public static class RulesValidator
    {
        public static bool TryAsTrio(List<Card> selected, out string reason)
        {
            reason = """";
            if (selected.Count != 3)
            {
                reason = ""Un trío son 3 cartas."";
                return false;
            }

            int jokers = selected.Count(c => c.IsJoker);
            if (jokers > 1)
            {
                reason = ""Máximo 1 Joker por grupo."";
                return false;
            }

            var nonJ = selected.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0)
            {
                reason = ""Un trío no puede ser solo Jokers."";
                return false;
            }

            var rank = nonJ[0].Rank;
            if (nonJ.Any(c => c.Rank != rank))
            {
                reason = ""En trío, todas las cartas deben ser del mismo número (Rank).""; 
                return false;
            }

            // Different suits among non-jokers
            var suits = nonJ.Select(c => c.Suit.Value).ToList();
            if (suits.Distinct().Count() != suits.Count)
            {
                reason = ""En trío, las pintas deben ser distintas (sin repetir).""; 
                return false;
            }

            return true;
        }

        // Run: same suit, 4+ cards, consecutive, 0-1 joker that can fill exactly one gap.
        // Ace can be low (A-2-3-4) OR high (J-Q-K-A). Not wrap (Q-K-A-2) unless you decide later.
        public static bool TryAsRun(List<Card> selected, out string reason, out List<int> resolvedRanks)
        {
            reason = """";
            resolvedRanks = null;

            if (selected.Count < 4)
            {
                reason = ""Una escala es de 4 o más cartas."";
                return false;
            }

            int jokers = selected.Count(c => c.IsJoker);
            if (jokers > 1)
            {
                reason = ""Máximo 1 Joker por grupo."";
                return false;
            }

            var nonJ = selected.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0)
            {
                reason = ""Una escala no puede ser solo Jokers."";
                return false;
            }

            // Same suit for non-jokers
            var suit = nonJ[0].Suit.Value;
            if (nonJ.Any(c => c.Suit.Value != suit))
            {
                reason = ""En escala, todas las cartas deben ser de la misma pinta."";
                return false;
            }

            // Convert ranks to int (Ace=1..King=13)
            var ranks = nonJ.Select(c => (int)c.Rank).ToList();
            if (ranks.Distinct().Count() != ranks.Count)
            {
                reason = ""En escala, no puedes repetir el mismo número."";
                return false;
            }

            // We allow two modes: Ace low (1) or Ace high (treat Ace as 14 if needed)
            // Try best fit for sequence length with <= 1 gap filled by joker.
            if (TryResolveSequence(ranks, selected.Count, jokers, aceHigh:false, out resolvedRanks))
                return true;

            if (TryResolveSequence(ranks, selected.Count, jokers, aceHigh:true, out resolvedRanks))
                return true;

            reason = jokers == 1
                ? ""La escala no es correlativa (ni con 1 Joker rellenando un solo hueco)."" 
                : ""La escala no es correlativa."";
            return false;
        }

        private static bool TryResolveSequence(List<int> nonJRanks, int totalCount, int jokers, bool aceHigh, out List<int> resolved)
        {
            resolved = null;

            // if aceHigh: map Ace(1) -> 14 for computation
            var r = nonJRanks.Select(v => (aceHigh && v == 1) ? 14 : v).OrderBy(x => x).ToList();

            // Determine if can form consecutive sequence length = totalCount with <=1 missing
            // Approach: pick start so that all non-joker ranks fit in [start, start+len-1]
            int len = totalCount;
            int min = r.Min();
            int max = r.Max();

            // Possible start range so interval covers [min..max]
            int startMin = max - (len - 1);
            int startMax = min;

            for (int start = startMin; start <= startMax; start++)
            {
                var seq = Enumerable.Range(start, len).ToHashSet();
                int missing = seq.Count(x => !r.Contains(x));
                if (missing == jokers)
                {
                    // Build resolved list (sorted)
                    var resolvedSeq = Enumerable.Range(start, len).ToList();

                    // Validate: if aceHigh, avoid sequences like 10-11-12-13-14 is ok (J-Q-K-A),
                    // but disallow 14-15... (not possible anyway)
                    // Also disallow sequences with values >14
                    if (resolvedSeq.Any(v => v < 1 || v > 14)) continue;

                    // If aceHigh used, ensure 14 only appears at end (typical J-Q-K-A)
                    if (aceHigh && resolvedSeq.Contains(14) && resolvedSeq.Last() != 14) continue;

                    resolved = resolvedSeq;
                    return true;
                }
            }

            return false;
        }

        public static bool TryAddToTrio(BankGroup trio, Card card, out string reason)
        {
            reason = """";
            if (trio.Type != BankGroupType.Trio) { reason = ""No es trío.""; return false; }

            int jokerCount = trio.Cards.Count(c => c.IsJoker);
            if (card.IsJoker && jokerCount >= 1) { reason = ""Ese trío ya tiene Joker.""; return false; }

            var nonJ = trio.Cards.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0) { reason = ""Trío inválido.""; return false; }

            var rank = nonJ[0].Rank;

            if (!card.IsJoker)
            {
                if (card.Rank != rank) { reason = ""Ese trío es de otro número.""; return false; }

                // suit must not repeat among non-jokers
                var suits = nonJ.Select(c => c.Suit.Value).ToHashSet();
                if (suits.Contains(card.Suit.Value)) { reason = ""Ya existe esa pinta en el trío.""; return false; }
            }
            else
            {
                // Joker ok if none already
            }

            return true;
        }

        public static bool TryAddToRun(BankGroup run, Card card, out string reason)
        {
            reason = """";
            if (run.Type != BankGroupType.Run) { reason = ""No es escala.""; return false; }

            int jokerCount = run.Cards.Count(c => c.IsJoker);
            if (card.IsJoker && jokerCount >= 1) { reason = ""Esa escala ya tiene Joker.""; return false; }

            // Determine suit from non-jokers in run
            var nonJ = run.Cards.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0) { reason = ""Escala inválida.""; return false; }
            var suit = nonJ[0].Suit.Value;

            if (!card.IsJoker && card.Suit.Value != suit)
            {
                reason = ""La carta no es de la misma pinta."";
                return false;
            }

            // For simplicity (MVP): allow adding only at ends by rank (or using joker to extend one step).
            // We'll compute current resolved best sequence, then see if card can be start-1 or end+1.
            string rReason;
            List<int> resolved;
            if (!TryAsRun(run.Cards.ToList(), out rReason, out resolved))
            {
                reason = ""Escala actual no es válida: "" + rReason;
                return false;
            }

            int start = resolved.First();
            int end = resolved.Last();

            if (card.IsJoker)
            {
                // Joker extends one side only (start-1 or end+1), if within 1..14
                if (start > 1 || end < 14) return true;
                reason = ""No hay espacio para extender con Joker."";
                return false;
            }

            int v = (int)card.Rank;
            if (v == 1)
            {
                // Ace can be 1 or 14 depending on sequence
                // If end is 13, allow Ace as 14 (J-Q-K-A)
                if (end == 13) v = 14;
            }

            if (v == start - 1 || v == end + 1)
                return true;

            reason = ""Solo puedes agregar al inicio o al final de la escala."";
            return false;
        }
    }
}
");

        Write("GameTableControllerV2.cs", overwrite, @"
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CariocaRuntime
{
    public sealed class GameTableControllerV2 : MonoBehaviour
    {
        [Header(""UI (auto) — piles/hand"")]
        [SerializeField] private Button deckButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Transform handLayout;
        [SerializeField] private GameObject cardPrefab;

        [Header(""UI (auto) — bank/actions"")]
        [SerializeField] private Transform bankLayout;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button addButton;
        [SerializeField] private Button sortButton;

        private readonly List<Card> _hand = new();
        private readonly List<Card> _discard = new();
        private readonly List<BankGroup> _bank = new();

        private readonly HashSet<int> _selectedHandIndices = new();

        private Deck _deck;
        private bool _hasDrawnThisTurn;

        private void Start()
        {
            if (!AllRefsOk())
            {
                Debug.LogError(""GameTableControllerV2: referencias UI incompletas. Recorre el menú Tools → CARIOCA → Upgrade GameTable."");
                enabled = false;
                return;
            }

            deckButton.onClick.RemoveAllListeners();
            discardButton.onClick.RemoveAllListeners();
            deckButton.onClick.AddListener(DrawFromDeck);
            discardButton.onClick.AddListener(DrawFromDiscard);

            dropButton.onClick.RemoveAllListeners();
            addButton.onClick.RemoveAllListeners();
            sortButton.onClick.RemoveAllListeners();

            dropButton.onClick.AddListener(OnDropPressed);
            addButton.onClick.AddListener(OnAddPressed);
            sortButton.onClick.AddListener(() => { SortHand(); RefreshHandUI(); });

            NewRound();
        }

        private bool AllRefsOk()
        {
            return deckButton && discardButton && handLayout && cardPrefab && bankLayout && dropButton && addButton && sortButton;
        }

        private void NewRound()
        {
            _deck = new Deck();
            _deck.Build54With2Jokers();
            _deck.Shuffle();

            _hand.Clear();
            _discard.Clear();
            _bank.Clear();
            _selectedHandIndices.Clear();

            ClearChildren(handLayout);
            ClearChildren(bankLayout);

            // Deal 12
            for (int i = 0; i < 12; i++)
                _hand.Add(_deck.Draw());

            // First discard cannot be Joker
            Card first = _deck.Draw();
            int safety = 0;
            var temp = new List<Card>();
            while (first.IsJoker && safety < 200)
            {
                temp.Add(first);
                first = _deck.Draw();
                safety++;
            }
            if (temp.Count > 0)
            {
                _deck.AddRange(temp);
                _deck.Shuffle();
            }

            _discard.Add(first);
            _hasDrawnThisTurn = false;

            RefreshDiscardLabel();
            RefreshBankUI();
            RefreshHandUI();
        }

        // ---------- Turn actions ----------
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

        private void OnCardClicked(int index, CardView view)
        {
            // click toggles selection always
            if (_selectedHandIndices.Contains(index)) _selectedHandIndices.Remove(index);
            else _selectedHandIndices.Add(index);

            view.SetSelected(_selectedHandIndices.Contains(index));
        }

        private void OnDropPressed()
        {
            if (_selectedHandIndices.Count == 0) return;

            var selectedCards = _selectedHandIndices.Select(i => _hand[i]).ToList();

            // Validate as Trio or Run
            string reason;
            if (RulesValidator.TryAsTrio(selectedCards, out reason))
            {
                _bank.Add(new BankGroup(BankGroupType.Trio, selectedCards));
                RemoveSelectedFromHand();
                Debug.Log(""✅ Bajaste un TRÍO."");
            }
            else
            {
                List<int> resolved;
                if (RulesValidator.TryAsRun(selectedCards, out reason, out resolved))
                {
                    _bank.Add(new BankGroup(BankGroupType.Run, selectedCards));
                    RemoveSelectedFromHand();
                    Debug.Log(""✅ Bajaste una ESCALA."");
                }
                else
                {
                    Debug.LogWarning(""❌ No válido para bajar: "" + reason);
                    return;
                }
            }

            _selectedHandIndices.Clear();
            RefreshBankUI();
            RefreshHandUI();
        }

        private void OnAddPressed()
        {
            if (_selectedHandIndices.Count != 1)
            {
                Debug.LogWarning(""Selecciona EXACTAMENTE 1 carta para agregar a banca."");
                return;
            }
            if (_bank.Count == 0)
            {
                Debug.LogWarning(""No hay banca aún."");
                return;
            }

            int idx = _selectedHandIndices.First();
            var card = _hand[idx];

            // Try add to first compatible group (simple MVP)
            for (int g = 0; g < _bank.Count; g++)
            {
                var group = _bank[g];
                string reason;

                bool ok = group.Type == BankGroupType.Trio
                    ? RulesValidator.TryAddToTrio(group, card, out reason)
                    : RulesValidator.TryAddToRun(group, card, out reason);

                if (ok)
                {
                    group.Cards.Add(card);
                    _hand.RemoveAt(idx);
                    _selectedHandIndices.Clear();
                    Debug.Log($""✅ Agregaste carta a {(group.Type==BankGroupType.Trio?""TRÍO"":""ESCALA"")}."");
                    RefreshBankUI();
                    RefreshHandUI();
                    return;
                }
            }

            Debug.LogWarning(""❌ No pude agregar esa carta a ninguna banca (por reglas). "");
        }

        private void RemoveSelectedFromHand()
        {
            // remove from highest index first
            var sorted = _selectedHandIndices.OrderByDescending(x => x).ToList();
            foreach (var i in sorted) _hand.RemoveAt(i);
        }

        private void SortHand()
        {
            // Simple sort: Joker last, then by suit then rank
            _hand.Sort((a,b) =>
            {
                if (a.IsJoker && !b.IsJoker) return 1;
                if (!a.IsJoker && b.IsJoker) return -1;
                if (a.IsJoker && b.IsJoker) return 0;

                int suit = a.Suit.Value.CompareTo(b.Suit.Value);
                if (suit != 0) return suit;

                return ((int)a.Rank).CompareTo((int)b.Rank);
            });
            _selectedHandIndices.Clear();
        }

        // ---------- Deck recycle ----------
        private void EnsureDeckHasCards()
        {
            if (_deck.Count > 0) return;
            if (_discard.Count <= 1) return;

            var top = _discard[^1];
            var recycle = _discard.GetRange(0, _discard.Count - 1);

            _discard.Clear();
            _discard.Add(top);

            _deck.AddRange(recycle);
            _deck.Shuffle();

            Debug.Log(""♻️ Mazo vacío: se revuelve descarte y se reutiliza como mazo (dejando 1 arriba)."");
        }

        // ---------- UI Refresh ----------
        private void RefreshHandUI()
        {
            ClearChildren(handLayout);

            for (int i = 0; i < _hand.Count; i++)
            {
                var c = _hand[i];
                var go = Instantiate(cardPrefab, handLayout);
                var view = go.GetComponent<CardView>();
                bool selected = _selectedHandIndices.Contains(i);
                view.Bind(c, (v) => OnCardClicked(i, v), selected);
            }
        }

        private void RefreshBankUI()
        {
            ClearChildren(bankLayout);

            for (int g = 0; g < _bank.Count; g++)
            {
                var group = _bank[g];

                var panel = new GameObject($""Group_{g}"", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                panel.transform.SetParent(bankLayout, false);

                var img = panel.GetComponent<Image>();
                img.color = new Color(1, 1, 1, 0.10f);

                var vlg = panel.GetComponent<VerticalLayoutGroup>();
                vlg.spacing = 6;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.padding = new RectOffset(10,10,10,10);

                var title = panel.AddComponentInChildrenTMP($""{(group.Type==BankGroupType.Trio?""TRÍO"":""ESCALA"")} (J:{group.JokerCount()})"", 18);

                var row = new GameObject(""Cards"", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(panel.transform, false);
                var h = row.GetComponent<HorizontalLayoutGroup>();
                h.spacing = 8;
                h.childAlignment = TextAnchor.MiddleCenter;

                foreach (var c in group.Cards)
                {
                    var mini = Instantiate(cardPrefab, row.transform);
                    var rt = mini.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(80, 110);

                    var view = mini.GetComponent<CardView>();
                    view.Bind(c, null, false); // no click in bank MVP
                }
            }
        }

        private void RefreshDiscardLabel()
        {
            var tmp = discardButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null) return;

            if (_discard.Count == 0) tmp.text = ""DESCARTE"";
            else
            {
                var top = _discard[^1];
                tmp.text = top.IsJoker ? ""DESCARTE\n(JOKER)"" : ""DESCARTE\n("" + top.ToString() + "")"";
            }
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    internal static class TMPHelpers
    {
        public static TextMeshProUGUI AddComponentInChildrenTMP(this GameObject parent, string text, int size)
        {
            var go = new GameObject(""TMP"", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1,1,1,0.9f);
            return tmp;
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
