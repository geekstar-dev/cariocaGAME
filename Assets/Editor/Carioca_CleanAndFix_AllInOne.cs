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

public static class Carioca_CleanAndFix_AllInOne
{
    private const string MenuPath = "Tools/CARIOCA/CLEAN + FIX (Delete Junk, Rebuild Core)";
    private const string BackupRoot = "Assets/_CariocaTrashBackup";

    private const string RuntimeDir = "Assets/Scripts/CariocaRuntime";
    private const string EditorDir  = "Assets/Editor";
    private const string PrefabCardUIPath = "Assets/Prefabs/CardUI.prefab";
    private const string SceneGameTablePath = "Assets/Scenes/GameTable.unity";

    // "Basura" típica (ajustada a lo que has mostrado)
    private static readonly string[] JunkEditorFiles =
    {
        "Assets/Editor/Carioca_OneClickInstaller.cs",
        "Assets/Editor/Carioca_UXPatch_OneClick.cs",
        "Assets/Editor/Carioca_BancaInstaller.cs",
        "Assets/Editor/Carioca_TablePrototypeInstaller.cs",
        "Assets/Editor/Carioca_DeckFixAndTest.cs",
        "Assets/Editor/Carioca_FixEverything_OneClick.cs",
    };

    private static readonly string[] JunkRuntimeFiles =
    {
        "Assets/Scripts/CariocaRuntime/GameTableController.cs",
        "Assets/Scripts/CariocaRuntime/GameTableControllerV3.cs",
    };

    [MenuItem(MenuPath)]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Sal de Play Mode antes de ejecutar CLEAN + FIX.");
            return;
        }

        try
        {
            EnsureFolder(EditorDir);
            EnsureFolder(RuntimeDir);
            EnsureFolder(BackupRoot);

            // 0) Backup folder
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = $"{BackupRoot}/{stamp}";
            EnsureFolder(backupDir);

            // 1) Move junk to backup
            MoveToBackup(backupDir, JunkEditorFiles);
            MoveToBackup(backupDir, JunkRuntimeFiles);

            // 2) Write stable core scripts (overwrite with backup)
            WriteWithBackup(backupDir, $"{RuntimeDir}/Deck.cs", GetDeckForYourModel());
            WriteWithBackup(backupDir, $"{RuntimeDir}/CardDragHandler.cs", GetCardDragHandler());
            WriteWithBackup(backupDir, $"{RuntimeDir}/DropZone.cs", GetDropZone());
            WriteWithBackup(backupDir, $"{RuntimeDir}/CariocaDragBus.cs", GetDragBus());
            WriteWithBackup(backupDir, $"{RuntimeDir}/CardView.cs", GetCardViewStable());

            AssetDatabase.Refresh();

            // 3) Patch prefab CardUI
            PatchCardUIPrefab();

            // 4) Patch scene GameTable
            PatchGameTableScene();

            AssetDatabase.SaveAssets();
            Debug.Log($"✅ CLEAN + FIX terminado.\nBackup guardado en: {backupDir}\nSi algo no te gusta, puedes restaurar desde esa carpeta.");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ CLEAN + FIX falló:\n" + e);
        }
    }

    // ---------------------------
    // Backup / move utilities
    // ---------------------------
    private static void MoveToBackup(string backupDir, string[] paths)
    {
        foreach (var path in paths)
        {
            MoveFileAndMeta(path, backupDir);
        }
    }

    // ✅ Esta versión NO usa File.Move con 3 parámetros (compatibilidad Unity/.NET)
    private static void MoveFileAndMeta(string assetPath, string backupDir)
    {
        if (!File.Exists(assetPath)) return;

        string fileName = Path.GetFileName(assetPath);
        string dest = $"{backupDir}/{fileName}";

        // Move .cs
        SafeMoveOverwrite(assetPath, dest);

        // Move .meta if exists
        string meta = assetPath + ".meta";
        if (File.Exists(meta))
        {
            SafeMoveOverwrite(meta, $"{backupDir}/{fileName}.meta");
        }

        Debug.Log($"🧹 Movido a backup: {assetPath}");
    }

    private static void SafeMoveOverwrite(string source, string dest)
    {
        if (File.Exists(dest))
            File.Delete(dest);

        File.Move(source, dest);
    }

    private static void WriteWithBackup(string backupDir, string path, string content)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) EnsureFolder(dir);

        if (File.Exists(path))
        {
            string fileName = Path.GetFileName(path);
            File.Copy(path, $"{backupDir}/{fileName}.preoverwrite.bak", true);

            string meta = path + ".meta";
            if (File.Exists(meta))
                File.Copy(meta, $"{backupDir}/{fileName}.meta.preoverwrite.bak", true);
        }

        File.WriteAllText(path, content, new UTF8Encoding(false));
        Debug.Log($"✍️ Escrito: {path}");
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

    // ---------------------------
    // Prefab patch
    // ---------------------------
    private static void PatchCardUIPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCardUIPath);
        if (prefab == null)
        {
            Debug.LogWarning($"⚠️ No encontré prefab: {PrefabCardUIPath}. Saltando patch prefab.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabCardUIPath);
        bool dirty = false;

        if (root.GetComponent<CanvasGroup>() == null)
        {
            root.AddComponent<CanvasGroup>();
            dirty = true;
        }

        // Add CardDragHandler if missing
        var dragType = TypeByName("CariocaRuntime.CardDragHandler");
        if (dragType != null && root.GetComponent(dragType) == null)
        {
            root.AddComponent(dragType);
            dirty = true;
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

            glowGo.SetActive(false);
            dirty = true;
        }

        if (dirty)
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabCardUIPath);
            Debug.Log("✅ Prefab CardUI parcheado (CanvasGroup + Drag + Glow).");
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    // ---------------------------
    // Scene patch
    // ---------------------------
    private static void PatchGameTableScene()
    {
        if (!File.Exists(SceneGameTablePath))
        {
            Debug.LogWarning($"⚠️ No encontré escena: {SceneGameTablePath}. Abre tu GameTable y vuelve a correr el fix.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(SceneGameTablePath, OpenSceneMode.Single);

        // Disable any old GameTableController
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            if (mb.GetType().Name == "GameTableController")
            {
                mb.enabled = false;
                EditorUtility.SetDirty(mb);
                Debug.Log("✅ Desactivé GameTableController (viejo).");
            }
        }

        // Ensure V2 exists
        MonoBehaviour v2 = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(m => m != null && m.GetType().FullName == "CariocaRuntime.GameTableControllerV2");

        if (v2 == null)
        {
            var t = TypeByName("CariocaRuntime.GameTableControllerV2");
            if (t == null)
            {
                Debug.LogWarning("⚠️ No encontré tipo GameTableControllerV2 (¿no compiló todavía?). Espera compile y re-ejecuta.");
                return;
            }

            var go = new GameObject("GameTableControllerV2");
            v2 = (MonoBehaviour)go.AddComponent(t);
        }

        AutoAssignV2(v2);

        // DropZones
        var discardBtn = FindButtonByName("discard", "descarte");
        if (discardBtn != null) EnsureDropZone(discardBtn.gameObject, "Discard");

        var bankLayout = FindTransformByName("bank", "banca");
        if (bankLayout != null) EnsureDropZone(bankLayout.gameObject, "Bank");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ GameTable reparada (refs + DropZones).");
    }

    private static void AutoAssignV2(MonoBehaviour v2)
    {
        AssignField(v2, "deckButton", FindButtonByName("deck", "mazo"));
        AssignField(v2, "discardButton", FindButtonByName("discard", "descarte"));
        AssignField(v2, "handLayout", FindTransformByName("hand", "mano"));
        AssignField(v2, "bankLayout", FindTransformByName("bank", "banca"));

        AssignField(v2, "dropButton", FindButtonByName("drop", "bajar"));
        AssignField(v2, "addButton", FindButtonByName("add", "agregar", "descartar", "discard"));
        AssignField(v2, "sortButton", FindButtonByName("sort", "ordenar"));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCardUIPath);
        AssignField(v2, "cardPrefab", prefab);

        EditorUtility.SetDirty(v2);
    }

    private static void EnsureDropZone(GameObject go, string zone)
    {
        var dzType = TypeByName("CariocaRuntime.DropZone");
        if (dzType == null) return;

        var dz = go.GetComponent(dzType) ?? go.AddComponent(dzType);

        var f = dzType.GetField("zoneType");
        if (f != null)
        {
            int val = zone == "Bank" ? 1 : 0; // Discard=0, Bank=1
            f.SetValue(dz, Enum.ToObject(f.FieldType, val));
        }

        EditorUtility.SetDirty(go);
    }

    private static void AssignField(object obj, string fieldName, UnityEngine.Object value)
    {
        var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f == null) return;

        if (value != null && f.FieldType.IsAssignableFrom(value.GetType()))
        {
            f.SetValue(obj, value);
        }
        else
        {
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

            var tmpMatch = buttons.FirstOrDefault(x =>
                x != null &&
                x.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) != null &&
                (x.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text ?? "").ToLower().Contains(k));

            if (tmpMatch != null) return tmpMatch;
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

    private static Type TypeByName(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    // ---------------------------
    // Stable core code (YOUR MODEL)
    // ---------------------------
    private static string GetDeckForYourModel() => @"
using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    // Deck estable para tu modelo actual:
    // Card(Suit? suit, Rank rank)
    // Joker => suit = null, rank = Rank.Joker
    public sealed class Deck
    {
        private readonly List<Card> _cards = new();
        private readonly Random _rng = new();

        public int Count => _cards.Count;

        public Deck() { }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (int r = 1; r <= 13; r++)
                {
                    var rank = (Rank)r;
                    _cards.Add(new Card((Suit?)suit, rank));
                }
            }

            _cards.Add(new Card((Suit?)null, Rank.Joker));
            _cards.Add(new Card((Suit?)null, Rank.Joker));
        }

        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        public Card Draw()
        {
            if (_cards.Count == 0) throw new InvalidOperationException(""Deck vacío"");

            var top = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return top;
        }

        public void AddRange(IEnumerable<Card> cards) => _cards.AddRange(cards);
    }
}
".Trim();

    private static string GetDragBus() => @"
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

        // Compatibilidad (2/3/4 parámetros)
        public void Bind(Card card, Action<CardView> onClick) => Bind(card, onClick, false, false);
        public void Bind(Card card, Action<CardView> onClick, bool selected) => Bind(card, onClick, selected, false);

        public void Bind(Card card, Action<CardView> onClick, bool selected, bool hint)
        {
            _card = card;
            _onClick = onClick;

            if (label != null)
                label.text = card.ToString();

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
}
#endif
