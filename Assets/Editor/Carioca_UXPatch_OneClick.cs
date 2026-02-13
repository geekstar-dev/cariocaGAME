// Assets/Editor/Carioca_UXPatch_OneClick.cs
// One-click patcher (safe): creates scripts first, then (after compile) patches prefab + scene using reflection.
// It avoids compile-time references to runtime classes (CardDragHandler / DropZone).

#if UNITY_EDITOR
using System.Linq;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UI;

public static class Carioca_UXPatch_OneClick
{
    private const string MenuPath = "Tools/CARIOCA/Patch UX (Drag, Glow, SmartSort)";

    private const string ScriptsDir = "Assets/Scripts/CariocaRuntime";
    private const string PrefabPath = "Assets/Prefabs/CardUI.prefab";

    private const string ControllerPath = "Assets/Scripts/CariocaRuntime/GameTableControllerV2.cs";
    private const string CardViewPath   = "Assets/Scripts/CariocaRuntime/CardView.cs";

    private const string PendingKey = "CARIOCA_UX_PATCH_PENDING";

    [MenuItem(MenuPath)]
    public static void Patch()
    {
        try
        {
            EnsureDir(ScriptsDir);

            // 1) Write/overwrite runtime scripts
            WriteFileWithBackup(Path.Combine(ScriptsDir, "CardDragHandler.cs"), GetCardDragHandler());
            WriteFileWithBackup(Path.Combine(ScriptsDir, "DropZone.cs"), GetDropZone());
            WriteFileWithBackup(Path.Combine(ScriptsDir, "RoundConfig.cs"), GetRoundConfig());

            WriteFileWithBackup(CardViewPath, GetCardView());
            WriteFileWithBackup(ControllerPath, GetGameTableControllerV2());

            // 2) Ask Unity to reimport + compile
            EditorPrefs.SetBool(PendingKey, true);

            // Hook compilation finished ONCE
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            AssetDatabase.Refresh();
            Debug.Log("✅ Scripts creados/actualizados. Unity compilará y luego se parchea Prefab/Escena automáticamente.");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Patch falló:\n" + e);
        }
    }

    private static void OnCompilationFinished(object _)
    {
        // Only run if pending
        if (!EditorPrefs.GetBool(PendingKey, false))
            return;

        // Unhook (important)
        CompilationPipeline.compilationFinished -= OnCompilationFinished;

        EditorPrefs.SetBool(PendingKey, false);

        try
        {
            PatchCardUIPrefab_Reflection();
            TryPatchOpenSceneDropZones_Reflection();

            AssetDatabase.SaveAssets();
            Debug.Log("✅ CARIOCA UX Patch FINALIZADO: Prefab + escena parcheados.");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Post-compile patch falló:\n" + e);
        }
    }

    // -------------------------
    // Prefab patch (Reflection)
    // -------------------------
    private static void PatchCardUIPrefab_Reflection()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"⚠️ No encontré el prefab en {PrefabPath}. Saltando patch de prefab.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool dirty = false;

        // CanvasGroup
        if (root.GetComponent<CanvasGroup>() == null)
        {
            root.AddComponent<CanvasGroup>();
            dirty = true;
        }

        // Add CardDragHandler by name (no compile-time dependency)
        var dragType = GetRuntimeType("CariocaRuntime.CardDragHandler");
        if (dragType != null && root.GetComponent(dragType) == null)
        {
            root.AddComponent(dragType);
            dirty = true;
        }
        else if (dragType == null)
        {
            Debug.LogWarning("⚠️ No pude encontrar el tipo CariocaRuntime.CardDragHandler (¿compiló?).");
        }

        // Glow child
        var glowTf = root.transform.Find("Glow");
        if (glowTf == null)
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
            img.color = new Color(1f, 0.85f, 0.2f, 0.18f);

            dirty = true;
        }

        if (dirty)
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("✅ Prefab CardUI parcheado (CanvasGroup + Drag + Glow).");
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    // -------------------------
    // Scene patch (Reflection)
    // -------------------------
    private static void TryPatchOpenSceneDropZones_Reflection()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        var dropZoneType = GetRuntimeType("CariocaRuntime.DropZone");
        if (dropZoneType == null)
        {
            Debug.LogWarning("⚠️ No pude encontrar el tipo CariocaRuntime.DropZone (¿compiló?).");
            return;
        }

        // Best-effort detection by name
        var all = UnityEngine.Object.FindObjectsOfType<Transform>(true);

        Transform discardCandidate = all.FirstOrDefault(t =>
            t.name.ToLower().Contains("discard") || t.name.ToLower().Contains("descarte"));

        Transform bankCandidate = all.FirstOrDefault(t =>
            t.name.ToLower().Contains("bank") || t.name.ToLower().Contains("banca"));

        if (discardCandidate != null)
        {
            var dz = discardCandidate.GetComponent(dropZoneType) ?? discardCandidate.gameObject.AddComponent(dropZoneType);

            // Set enum field: zoneType = Discard (0)
            var field = dropZoneType.GetField("zoneType");
            if (field != null)
                field.SetValue(dz, Enum.ToObject(field.FieldType, 0));

            EditorUtility.SetDirty(discardCandidate.gameObject);
        }

        if (bankCandidate != null)
        {
            var dz = bankCandidate.GetComponent(dropZoneType) ?? bankCandidate.gameObject.AddComponent(dropZoneType);

            // zoneType = Bank (1)
            var field = dropZoneType.GetField("zoneType");
            if (field != null)
                field.SetValue(dz, Enum.ToObject(field.FieldType, 1));

            EditorUtility.SetDirty(bankCandidate.gameObject);
        }

        if (discardCandidate == null || bankCandidate == null)
        {
            Debug.LogWarning("⚠️ No detecté automáticamente Descarte/Banca por nombre. Puedes agregar DropZone manual desde Inspector.");
        }
        else
        {
            Debug.Log("✅ DropZones agregados (best-effort) en la escena.");
        }
    }

    private static Type GetRuntimeType(string fullName)
    {
        // Try common runtime assemblies
        var t = Type.GetType(fullName + ",Assembly-CSharp");
        if (t != null) return t;

        t = Type.GetType(fullName);
        return t;
    }

    // -------------------------
    // IO helpers
    // -------------------------
    private static void EnsureDir(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;

        var parts = dir.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static void WriteFileWithBackup(string path, string content)
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

    // -------------------------
    // Generated runtime scripts
    // -------------------------
    private static string GetCardDragHandler() => @"
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CariocaRuntime
{
    public sealed class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action OnBegin;
        public Action<Vector2> OnDragDelta;
        public Action<PointerEventData> OnEnd;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData) => OnBegin?.Invoke();

        public void OnDrag(PointerEventData eventData)
        {
            var scale = _canvas ? _canvas.scaleFactor : 1f;
            var delta = eventData.delta / Mathf.Max(0.0001f, scale);
            OnDragDelta?.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData eventData) => OnEnd?.Invoke(eventData);
    }
}
".Trim();

    private static string GetDropZone() => @"
using UnityEngine;

namespace CariocaRuntime
{
    public sealed class DropZone : MonoBehaviour
    {
        public enum ZoneType { Discard, Bank }
        public ZoneType zoneType = ZoneType.Discard;
    }
}
".Trim();

    private static string GetRoundConfig() => @"
namespace CariocaRuntime
{
    public enum RoundType
    {
        TwoTrios = 1,
        TrioAndRun = 2,
        TwoRuns = 3,
        ThreeTrios = 4,
        TwoTriosAndRun = 5,
        TrioAndTwoRuns = 6,
        ThreeRuns = 7,
        FourTrios = 8,
        TwoTriosAndTwoRuns = 9,
        FourRuns = 10,
    }
}
".Trim();

    // NOTE: CardView/GameTableControllerV2 content is long; keeping same as prior patch.
    // (This file compiles because it does NOT reference these types at compile-time in the editor script.)
    private static string GetCardView() => /* same content you already got in the patch */ @"
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace CariocaRuntime
{
    [DisallowMultipleComponent]
    public sealed class CardView : MonoBehaviour
    {
        [Header(""UI refs (optional)"")]
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image background;
        [SerializeField] private GameObject glow;

        private RectTransform _rt;
        private CanvasGroup _cg;
        private Vector2 _homePos;

        private Card _card;
        private System.Action<CardView> _onClick;

        private CardDragHandler _drag;

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

            _homePos = _rt.anchoredPosition;

            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _onClick?.Invoke(this));
        }

        public Card Card => _card;

        public void Bind(Card card, System.Action<CardView> onClick, bool selected, bool hint)
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

    private static string GetGameTableControllerV2() => /* same as prior patch */ @"
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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

        [Header(""UI (optional) — labels"")]
        [SerializeField] private TextMeshProUGUI roundLabel;

        [Header(""Game"")]
        [SerializeField] private RoundType currentRound = RoundType.TwoTrios;

        private readonly List<Card> _hand = new();
        private readonly List<Card> _discard = new();
        private readonly List<BankGroup> _bank = new();

        private readonly HashSet<Card> _selected = new();

        private Deck _deck;
        private bool _hasDrawnThisTurn;

        private void Start()
        {
            if (!AllRefsOk())
            {
                Debug.LogError(""GameTableControllerV2: referencias UI incompletas."");
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
            sortButton.onClick.AddListener(() => { SmartSortHand(); RefreshHandUI(true); });

            NewRound();
        }

        private void Update()
        {
            if (CariocaDragBus.LastDrop.HasValue)
            {
                var info = CariocaDragBus.LastDrop.Value;
                CariocaDragBus.Clear();

                if (info.view == null || info.zone == null) return;

                if (info.zone.zoneType == DropZone.ZoneType.Discard)
                {
                    SelectOnly(info.view.Card);
                    DiscardSelectedOne();
                }
                else
                {
                    if (_selected.Count == 0) SelectOnly(info.view.Card);

                    if (CanDropSelectionNow()) OnDropPressed();
                    else if (_selected.Count == 1) TryAddSelectedOneToExistingBank();
                }
            }
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
            _selected.Clear();

            ClearChildren(handLayout);
            ClearChildren(bankLayout);

            for (int i = 0; i < 12; i++) _hand.Add(_deck.Draw());

            Card first = _deck.Draw();
            int safety = 0;
            var temp = new List<Card>();
            while (first.IsJoker && safety < 200)
            {
                temp.Add(first);
                first = _deck.Draw();
                safety++;
            }
            if (temp.Count > 0) { _deck.AddRange(temp); _deck.Shuffle(); }

            _discard.Add(first);
            _hasDrawnThisTurn = false;

            if (roundLabel) roundLabel.text = ""Ronda "" + (int)currentRound;

            RefreshDiscardLabel();
            RefreshBankUI();
            RefreshHandUI(false);
            UpdateDropButtonVisibility();
        }

        private void DrawFromDeck()
        {
            if (_hasDrawnThisTurn) return;
            EnsureDeckHasCards();
            _hand.Add(_deck.Draw());
            _hasDrawnThisTurn = true;
            RefreshHandUI(true);
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
            RefreshHandUI(true);
        }

        private void OnCardClicked(CardView view)
        {
            var c = view.Card;
            if (_selected.Contains(c)) _selected.Remove(c);
            else _selected.Add(c);
            view.SetSelected(_selected.Contains(c));
            UpdateDropButtonVisibility();
        }

        private void OnDropPressed()
        {
            if (_selected.Count == 0) return;
            var selectedCards = _selected.ToList();

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

            _selected.Clear();
            RefreshBankUI();
            RefreshHandUI(true);
            UpdateDropButtonVisibility();
        }

        private bool CanDropSelectionNow()
        {
            if (_selected.Count == 0) return false;
            var selectedCards = _selected.ToList();
            string reason;
            List<int> resolved;
            return RulesValidator.TryAsTrio(selectedCards, out reason) ||
                   RulesValidator.TryAsRun(selectedCards, out reason, out resolved);
        }

        private void UpdateDropButtonVisibility()
        {
            if (!dropButton) return;
            dropButton.gameObject.SetActive(CanDropSelectionNow());
        }

        private void DiscardSelectedOne()
        {
            if (!_hasDrawnThisTurn)
            {
                Debug.LogWarning(""Primero roba (MAZO o DESCARTE). Luego descarta 1."");
                return;
            }

            if (_selected.Count != 1)
            {
                Debug.LogWarning(""Selecciona EXACTAMENTE 1 carta para DESCARTAR."");
                return;
            }

            var card = _selected.First();
            if (!_hand.Contains(card))
            {
                _selected.Clear();
                RefreshHandUI(true);
                return;
            }

            _hand.Remove(card);
            _discard.Add(card);

            _selected.Clear();
            _hasDrawnThisTurn = false;

            RefreshDiscardLabel();
            RefreshHandUI(true);
            UpdateDropButtonVisibility();
        }

        private void TryAddSelectedOneToExistingBank()
        {
            if (_selected.Count != 1) return;
            if (_bank.Count == 0) return;

            var card = _selected.First();
            if (!_hand.Contains(card)) return;

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
                    _hand.Remove(card);
                    _selected.Clear();
                    RefreshBankUI();
                    RefreshHandUI(true);
                    return;
                }
            }
        }

        private void SelectOnly(Card card)
        {
            _selected.Clear();
            _selected.Add(card);
        }

        private void RemoveSelectedFromHand()
        {
            foreach (var c in _selected)
                _hand.Remove(c);
        }

        private void SmartSortHand()
        {
            // Simple MVP: sort for triads
            _hand.Sort((a, b) =>
            {
                if (a.IsJoker && !b.IsJoker) return 1;
                if (!a.IsJoker && b.IsJoker) return -1;
                if (a.IsJoker && b.IsJoker) return 0;

                int r = ((int)a.Rank).CompareTo((int)b.Rank);
                if (r != 0) return r;

                return a.Suit.Value.CompareTo(b.Suit.Value);
            });

            _selected.Clear();
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

        private void RefreshHandUI(bool animate)
        {
            ClearChildren(handLayout);

            for (int i = 0; i < _hand.Count; i++)
            {
                var c = _hand[i];
                var go = Object.Instantiate(cardPrefab, handLayout);
                var view = go.GetComponent<CardView>();

                bool selected = _selected.Contains(c);
                view.Bind(c, OnCardClicked, selected, false);

                if (animate)
                {
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var start = rt.anchoredPosition + new Vector2(0, -70);
                        rt.anchoredPosition = start;
                        rt.DOAnchorPosY(start.y + 70, 0.16f);
                    }
                }
            }
        }

        private void RefreshBankUI()
        {
            ClearChildren(bankLayout);
            // Mantén tu UI de banca actual si ya la tienes, aquí está mínimo.
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
}
".Trim();
}
#endif
