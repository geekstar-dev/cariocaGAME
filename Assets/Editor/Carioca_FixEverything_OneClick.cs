// Assets/Editor/Carioca_FixEverything_OneClick.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Carioca_FixEverything_OneClick
{
    private const string MenuPath = "Tools/CARIOCA/Fix Everything (One Click)";

    private const string RuntimeDir = "Assets/Scripts/CariocaRuntime";
    private const string PrefabCardUIPath = "Assets/Prefabs/CardUI.prefab";
    private const string SceneGameTablePath = "Assets/Scenes/GameTable.unity";

    [MenuItem(MenuPath)]
    public static void FixAll()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Sal de Play Mode antes de parchar.");
            return;
        }

        try
        {
            EnsureFolder(RuntimeDir);

            // 1) Write runtime scripts (safe + compatible)
            WriteWithBackup($"{RuntimeDir}/CariocaDragBus.cs", GetCariocaDragBus());
            WriteWithBackup($"{RuntimeDir}/DropZone.cs", GetDropZone());
            WriteWithBackup($"{RuntimeDir}/CardDragHandler.cs", GetCardDragHandler());
            WriteWithBackup($"{RuntimeDir}/CardView.cs", GetCardViewStable());

            // V2 controller: minimal stable, no DOTween, no crashes
            WriteWithBackup($"{RuntimeDir}/GameTableControllerV2.cs", GetGameTableControllerV2Stable());

            AssetDatabase.Refresh();

            // 2) Patch prefab
            PatchCardUIPrefab();

            // 3) Patch scene GameTable
            PatchGameTableScene();

            AssetDatabase.SaveAssets();
            Debug.Log("✅ FIX EVERYTHING terminado. Si ves algo raro en escena, dime y ajustamos heurísticas de auto-asignación.");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ FixEverything falló:\n" + e);
        }
    }

    // -----------------------------
    // Prefab patch
    // -----------------------------
    private static void PatchCardUIPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCardUIPath);
        if (prefab == null)
        {
            Debug.LogWarning($"⚠️ No encontré prefab {PrefabCardUIPath}. Saltando patch prefab.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabCardUIPath);
        bool dirty = false;

        if (root.GetComponent<CanvasGroup>() == null)
        {
            root.AddComponent<CanvasGroup>();
            dirty = true;
        }
        if (root.GetComponent(TypeByName("CariocaRuntime.CardDragHandler")) == null)
        {
            var t = TypeByName("CariocaRuntime.CardDragHandler");
            if (t != null) { root.AddComponent(t); dirty = true; }
        }

        // Ensure Glow exists
        if (root.transform.Find("Glow") == null)
        {
            var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(root.transform, false);

            var rt = glowGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6, 6);
            rt.offsetMax = new Vector2(-6, -6);

            var img = glowGo.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.85f, 0.2f, 0.20f);

            dirty = true;
        }

        if (dirty)
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabCardUIPath);
            Debug.Log("✅ Prefab CardUI reparado (CanvasGroup + Drag + Glow).");
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    // -----------------------------
    // Scene patch
    // -----------------------------
    private static void PatchGameTableScene()
    {
        if (!File.Exists(SceneGameTablePath))
        {
            Debug.LogWarning($"⚠️ No encontré {SceneGameTablePath}. Abre tu escena GameTable y vuelve a correr el fix.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(SceneGameTablePath, OpenSceneMode.Single);

        // Disable old controller if present
        foreach (var c in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null) continue;
            var tn = c.GetType().Name;
            if (tn == "GameTableController")
            {
                c.enabled = false;
                EditorUtility.SetDirty(c);
                Debug.Log("✅ Desactivé GameTableController (viejo) para evitar duplicados.");
            }
        }

        // Find or create V2
        var v2 = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(m => m != null && m.GetType().Name == "GameTableControllerV2");

        if (v2 == null)
        {
            var go = new GameObject("GameTableControllerV2");
            var t = TypeByName("CariocaRuntime.GameTableControllerV2");
            if (t == null)
            {
                Debug.LogWarning("⚠️ No encontré tipo GameTableControllerV2 (¿no compiló todavía?). Guarda y espera compile, luego re-ejecuta Fix Everything.");
                return;
            }
            v2 = (MonoBehaviour)go.AddComponent(t);
        }

        AutoAssignV2References(v2);

        // Add DropZones to discard and bank (best effort)
        var discardBtn = FindButtonByName("discard", "descarte");
        if (discardBtn != null) EnsureDropZone(discardBtn.gameObject, "Discard");

        var bankLayout = FindTransformByName("bank", "banca");
        if (bankLayout != null) EnsureDropZone(bankLayout.gameObject, "Bank");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Escena GameTable reparada (auto-referencias + DropZones).");
    }

    private static void AutoAssignV2References(MonoBehaviour v2)
    {
        // Heurística por nombres. Si algo no se asigna, igual compila y te avisamos.
        AssignField(v2, "deckButton", FindButtonByName("deck", "mazo"));
        AssignField(v2, "discardButton", FindButtonByName("discard", "descarte"));

        AssignField(v2, "handLayout", FindTransformByName("hand", "mano"));
        AssignField(v2, "bankLayout", FindTransformByName("bank", "banca"));

        AssignField(v2, "dropButton", FindButtonByName("drop", "bajar"));
        // En nuestro V2 estable, addButton funciona como DESCARTAR (más útil que “agregar a banca” por ahora)
        AssignField(v2, "addButton", FindButtonByName("add", "agregar", "descartar", "discard"));
        AssignField(v2, "sortButton", FindButtonByName("sort", "ordenar"));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCardUIPath);
        AssignField(v2, "cardPrefab", prefab);

        // Optional label
        var roundLabel = FindTMPByName("round", "ronda");
        AssignField(v2, "roundLabel", roundLabel);

        // Rename add button text to DESCARTAR if it looks like "AGREGAR A BANCA"
        var btn = GetFieldValue<Button>(v2, "addButton");
        if (btn != null)
        {
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null && tmp.text.ToLower().Contains("agregar"))
            {
                tmp.text = "DESCARTAR";
                EditorUtility.SetDirty(tmp);
            }
        }

        EditorUtility.SetDirty(v2);
    }

    // -----------------------------
    // Helpers
    // -----------------------------
    private static void EnsureDropZone(GameObject go, string zoneName)
    {
        var dzType = TypeByName("CariocaRuntime.DropZone");
        if (dzType == null) return;

        var dz = go.GetComponent(dzType) ?? go.AddComponent(dzType);

        // set enum field by index: Discard=0, Bank=1
        var f = dzType.GetField("zoneType");
        if (f != null)
        {
            int val = (zoneName == "Bank") ? 1 : 0;
            f.SetValue(dz, Enum.ToObject(f.FieldType, val));
        }

        EditorUtility.SetDirty(go);
    }

    private static T GetFieldValue<T>(object obj, string fieldName) where T : class
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f == null) return null;
        return f.GetValue(obj) as T;
    }

    private static void AssignField(object obj, string fieldName, UnityEngine.Object value)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f == null) return;

        if (value != null && f.FieldType.IsAssignableFrom(value.GetType()))
        {
            f.SetValue(obj, value);
            EditorUtility.SetDirty((UnityEngine.Object)obj);
        }
        else
        {
            // Only warn when it matters
            if (value == null)
                Debug.LogWarning($"⚠️ No pude auto-asignar {fieldName} (no encontré objeto por nombre).");
        }
    }

    private static Button FindButtonByName(params string[] keys)
    {
        var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var k in keys)
        {
            var b = buttons.FirstOrDefault(x => x != null && x.name.ToLower().Contains(k));
            if (b != null) return b;
            b = buttons.FirstOrDefault(x => x != null && x.GetComponentInChildren<TMPro.TextMeshProUGUI>(true)?.text?.ToLower().Contains(k) == true);
            if (b != null) return b;
        }
        return null;
    }

    private static Transform FindTransformByName(params string[] keys)
    {
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var k in keys)
        {
            var t = all.FirstOrDefault(x => x != null && x.name.ToLower().Contains(k));
            if (t != null) return t;
        }
        return null;
    }

    private static TMPro.TextMeshProUGUI FindTMPByName(params string[] keys)
    {
        var tmps = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var k in keys)
        {
            var t = tmps.FirstOrDefault(x => x != null && x.name.ToLower().Contains(k));
            if (t != null) return t;

            t = tmps.FirstOrDefault(x => x != null && (x.text ?? "").ToLower().Contains(k));
            if (t != null) return t;
        }
        return null;
    }

    private static Type TypeByName(string fullName)
    {
        var t = Type.GetType(fullName + ",Assembly-CSharp");
        if (t != null) return t;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    private static void EnsureFolder(string unityPath)
    {
        if (AssetDatabase.IsValidFolder(unityPath)) return;
        var parts = unityPath.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static void WriteWithBackup(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(path))
        {
            var backup = path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
            File.Copy(path, backup, true);
        }
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    // -----------------------------
    // Runtime code templates
    // -----------------------------
    private static string GetCariocaDragBus() => @"
namespace CariocaRuntime
{
    public static class CariocaDragBus
    {
        public struct DropInfo
        {
            public CardView view;
            public DropZone zone;
        }

        public static DropInfo? LastDrop;
        public static void Clear() => LastDrop = null;
    }
}
".Trim();

    private static string GetDropZone() => @"
using UnityEngine;

namespace CariocaRuntime
{
    [DisallowMultipleComponent]
    public sealed class DropZone : MonoBehaviour
    {
        public enum ZoneType { Discard, Bank }
        public ZoneType zoneType = ZoneType.Discard;
    }
}
".Trim();

    private static string GetCardDragHandler() => @"
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CariocaRuntime
{
    [DisallowMultipleComponent]
    public sealed class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action OnBegin;
        public event Action<Vector2> OnDragDelta;
        public event Action<PointerEventData> OnEnd;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData) => OnBegin?.Invoke();

        public void OnDrag(PointerEventData eventData)
        {
            float scale = _canvas ? _canvas.scaleFactor : 1f;
            var delta = eventData.delta / Mathf.Max(0.0001f, scale);
            OnDragDelta?.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData eventData) => OnEnd?.Invoke(eventData);
    }
}
".Trim();

    private static string GetCardViewStable() => @"
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CariocaRuntime
{
    [DisallowMultipleComponent]
    public sealed class CardView : MonoBehaviour
    {
        [Header(""UI refs (optional)"")]
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private GameObject glow;

        private RectTransform _rt;
        private CanvasGroup _cg;
        private Vector2 _homePos;

        private Card _card;
        private Action<CardView> _onClick;

        private CardDragHandler _drag;

        public Card Card => _card;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
            if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();

            _drag = GetComponent<CardDragHandler>();
            if (_drag == null) _drag = gameObject.AddComponent<CardDragHandler>();

            _drag.OnBegin += HandleBeginDrag;
            _drag.OnDragDelta += HandleDragDelta;
            _drag.OnEnd += HandleEndDrag;

            if (_rt != null) _homePos = _rt.anchoredPosition;

            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _onClick?.Invoke(this));
        }

        // ✅ Compatibilidad total (2/3/4 params)
        public void Bind(Card card, Action<CardView> onClick) => Bind(card, onClick, false, false);
        public void Bind(Card card, Action<CardView> onClick, bool selected) => Bind(card, onClick, selected, false);

        public void Bind(Card card, Action<CardView> onClick, bool selected, bool hint)
        {
            _card = card;
            _onClick = onClick;

            if (label != null)
                label.text = card.IsJoker ? ""JOKER"" : card.ToString();

            SetSelected(selected);
            SetHint(hint);

            if (_rt != null) _homePos = _rt.anchoredPosition;
        }

        public void SetSelected(bool selected)
        {
            if (_rt != null)
                _rt.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }

        public void SetHint(bool on)
        {
            if (glow != null) glow.SetActive(on);
        }

        private void HandleBeginDrag()
        {
            if (_cg != null) _cg.blocksRaycasts = false;
        }

        private void HandleDragDelta(Vector2 delta)
        {
            if (_rt != null) _rt.anchoredPosition += delta;
        }

        private void HandleEndDrag(PointerEventData eventData)
        {
            if (_cg != null) _cg.blocksRaycasts = true;

            var zones = RaycastDropZones(eventData);
            if (zones.Count == 0)
            {
                SnapHome();
                return;
            }

            var dz = zones.Find(z => z.zoneType == DropZone.ZoneType.Discard) ?? zones[0];
            CariocaDragBus.LastDrop = new CariocaDragBus.DropInfo { view = this, zone = dz };
            SnapHome();
        }

        private void SnapHome()
        {
            if (_rt != null) _rt.anchoredPosition = _homePos;
        }

        private static List<DropZone> RaycastDropZones(PointerEventData eventData)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            var zones = new List<DropZone>();
            foreach (var r in results)
            {
                var dz = r.gameObject.GetComponentInParent<DropZone>();
                if (dz != null && !zones.Contains(dz))
                    zones.Add(dz);
            }
            return zones;
        }
    }
}
".Trim();

    private static string GetGameTableControllerV2Stable() => @"
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CariocaRuntime
{
    public sealed class GameTableControllerV2 : MonoBehaviour
    {
        [Header(""UI — piles/hand"")]
        [SerializeField] private Button deckButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Transform handLayout;
        [SerializeField] private GameObject cardPrefab;

        [Header(""UI — bank/actions"")]
        [SerializeField] private Transform bankLayout;
        [SerializeField] private Button dropButton; // BAJAR
        [SerializeField] private Button addButton;  // lo usaremos como DESCARTAR (1)
        [SerializeField] private Button sortButton;

        [Header(""UI (optional) — labels"")]
        [SerializeField] private TextMeshProUGUI roundLabel;

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
                Debug.LogError(""GameTableControllerV2: faltan referencias UI (deck/discard/handLayout/cardPrefab/bank/buttons)."");
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
            addButton.onClick.AddListener(DiscardSelectedOne);
            sortButton.onClick.AddListener(() => { SmartSort(); RefreshHandUI(); });

            NewRound();
        }

        private void Update()
        {
            // Drag&Drop handler
            if (CariocaDragBus.LastDrop.HasValue)
            {
                var info = CariocaDragBus.LastDrop.Value;
                CariocaDragBus.Clear();

                if (info.view == null || info.zone == null) return;

                if (info.zone.zoneType == DropZone.ZoneType.Discard)
                {
                    SelectOnlyCard(info.view.Card);
                    DiscardSelectedOne();
                }
                else
                {
                    // MVP: for now, bank drop = same as pressing BAJAR
                    if (_selectedHandIndices.Count == 0)
                        SelectOnlyCard(info.view.Card);

                    OnDropPressed();
                }
            }

            // Show BAJAR only if selection forms a valid group
            if (dropButton != null)
                dropButton.gameObject.SetActive(CanDropSelection());
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

            if (roundLabel) roundLabel.text = ""Ronda 1 (MVP)"";

            RefreshDiscardLabel();
            RefreshBankUI();
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

        private void OnCardClicked(int index, CardView view)
        {
            if (_selectedHandIndices.Contains(index)) _selectedHandIndices.Remove(index);
            else _selectedHandIndices.Add(index);

            view.SetSelected(_selectedHandIndices.Contains(index));
        }

        private void SelectOnlyCard(Card card)
        {
            _selectedHandIndices.Clear();
            int idx = _hand.FindIndex(c => c.Rank == card.Rank && c.Suit == card.Suit);
            if (idx >= 0) _selectedHandIndices.Add(idx);
        }

        private void DiscardSelectedOne()
        {
            if (!_hasDrawnThisTurn)
            {
                Debug.LogWarning(""Primero roba (MAZO o DESCARTE). Luego descarta 1."");
                return;
            }

            var selected = GetSelectedCardsSafe();
            if (selected.Count != 1)
            {
                Debug.LogWarning(""Selecciona EXACTAMENTE 1 carta para DESCARTAR."");
                return;
            }

            // Remove that card
            var c = selected[0];
            int idx = _hand.FindIndex(x => x.Rank == c.Rank && x.Suit == c.Suit);
            if (idx < 0) return;

            _hand.RemoveAt(idx);
            _discard.Add(c);

            _hasDrawnThisTurn = false;
            _selectedHandIndices.Clear();

            RefreshDiscardLabel();
            RefreshHandUI();
        }

        private void OnDropPressed()
        {
            var selectedCards = GetSelectedCardsSafe();
            if (selectedCards.Count == 0) return;

            // Validate as Trio or Run
            string reason;
            if (RulesValidator.TryAsTrio(selectedCards, out reason))
            {
                _bank.Add(new BankGroup(BankGroupType.Trio, selectedCards));
                RemoveSelectedFromHand();
            }
            else
            {
                List<int> resolved;
                if (RulesValidator.TryAsRun(selectedCards, out reason, out resolved))
                {
                    _bank.Add(new BankGroup(BankGroupType.Run, selectedCards));
                    RemoveSelectedFromHand();
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

        private bool CanDropSelection()
        {
            var selectedCards = GetSelectedCardsSafe(silent:true);
            if (selectedCards.Count < 3) return false;

            string reason;
            if (RulesValidator.TryAsTrio(selectedCards, out reason)) return true;

            List<int> resolved;
            if (RulesValidator.TryAsRun(selectedCards, out reason, out resolved)) return true;

            return false;
        }

        private List<Card> GetSelectedCardsSafe(bool silent=false)
        {
            if (_selectedHandIndices.Count == 0) return new List<Card>();

            var valid = _selectedHandIndices.Where(i => i >= 0 && i < _hand.Count).Distinct().ToList();
            if (valid.Count == 0)
            {
                if (!silent) Debug.LogWarning(""No hay selección válida (índices fuera de rango)."");
                _selectedHandIndices.Clear();
                return new List<Card>();
            }

            return valid.Select(i => _hand[i]).ToList();
        }

        private void RemoveSelectedFromHand()
        {
            var sorted = _selectedHandIndices.Where(i => i >= 0 && i < _hand.Count).OrderByDescending(i => i).ToList();
            foreach (var i in sorted) _hand.RemoveAt(i);
        }

        private void SmartSort()
        {
            // MVP: helpful for Round 1 (trios) => group by Rank, jokers last
            _hand.Sort((a, b) =>
            {
                if (a.IsJoker && !b.IsJoker) return 1;
                if (!a.IsJoker && b.IsJoker) return -1;
                if (a.IsJoker && b.IsJoker) return 0;

                int r = ((int)a.Rank).CompareTo((int)b.Rank);
                if (r != 0) return r;

                return a.Suit.Value.CompareTo(b.Suit.Value);
            });

            _selectedHandIndices.Clear();
        }

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
        }

        private void RefreshHandUI()
        {
            ClearChildren(handLayout);

            for (int i = 0; i < _hand.Count; i++)
            {
                var c = _hand[i];
                var go = Object.Instantiate(cardPrefab, handLayout);
                var view = go.GetComponent<CardView>();

                bool selected = _selectedHandIndices.Contains(i);
                bool hint = HintForCard(c);

                view.Bind(c, (v) => OnCardClicked(i, v), selected, hint);
            }
        }

        private bool HintForCard(Card c)
        {
            // MVP hint for trios: glow if at least 2 same rank exist
            if (c.IsJoker) return true;
            int same = 0;
            for (int i = 0; i < _hand.Count; i++)
                if (!_hand[i].IsJoker && _hand[i].Rank == c.Rank) same++;
            return same >= 2;
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
                vlg.padding = new RectOffset(10, 10, 10, 10);

                var titleGO = new GameObject(""Title"", typeof(RectTransform));
                titleGO.transform.SetParent(panel.transform, false);
                var tmp = titleGO.AddComponent<TextMeshProUGUI>();
                tmp.text = (group.Type == BankGroupType.Trio ? ""TRÍO"" : ""ESCALA"");
                tmp.fontSize = 20;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(1, 1, 1, 0.9f);

                var row = new GameObject(""Cards"", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(panel.transform, false);
                var h = row.GetComponent<HorizontalLayoutGroup>();
                h.spacing = 8;
                h.childAlignment = TextAnchor.MiddleCenter;

                foreach (var c in group.Cards)
                {
                    var mini = Object.Instantiate(cardPrefab, row.transform);
                    var rt = mini.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(80, 110);

                    var view = mini.GetComponent<CardView>();
                    view.Bind(c, null, false, false);
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
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
    }
}
".Trim();
}
#endif
