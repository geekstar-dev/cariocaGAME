#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Carioca_ResetAndFix_OneClick
{
    private const string MenuPath = "Tools/CARIOCA/RESET + FIX (Clean backups, Fix Deck/Card, Remove junk)";

    // Carpetas / rutas Unity
    private const string RuntimeDir = "Assets/Scripts/CariocaRuntime";
    private const string EditorDir  = "Assets/Editor";

    private const string TrashBackupInAssets = "Assets/_CariocaTrashBackup";

    // Scripts editor “problemáticos” típicos (ajusta si quieres, pero así está bien)
    private static readonly string[] KnownJunkEditorScripts =
    {
        "Assets/Editor/Carioca_OneClickInstaller.cs",
        "Assets/Editor/Carioca_UXPatch_OneClick.cs",
        "Assets/Editor/Carioca_BancaInstaller.cs",
        "Assets/Editor/Carioca_TablePrototypeInstaller.cs",
        "Assets/Editor/Carioca_DeckFixAndTest.cs",
        "Assets/Editor/Carioca_FixEverything_OneClick.cs",
    };

    [MenuItem(MenuPath)]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Sal de Play Mode antes de ejecutar el RESET + FIX.");
            return;
        }

        try
        {
            EnsureFolder(EditorDir);
            EnsureFolder(RuntimeDir);

            // 1) Crear backup FUERA de Assets (para que Unity NO lo compile)
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string externalBackupRoot = Path.Combine(projectRoot, $"_CariocaBackup_{stamp}");
            Directory.CreateDirectory(externalBackupRoot);

            Debug.Log($"🧰 Backup externo: {externalBackupRoot}");

            // 2) Si existe Assets/_CariocaTrashBackup -> copiar afuera y borrar dentro de Assets
            if (AssetDatabase.IsValidFolder(TrashBackupInAssets))
            {
                string absTrash = Path.Combine(projectRoot, "Assets", "_CariocaTrashBackup");
                string destTrash = Path.Combine(externalBackupRoot, "_CariocaTrashBackup_FROM_ASSETS");
                CopyDirectory(absTrash, destTrash);

                AssetDatabase.DeleteAsset(TrashBackupInAssets);
                Debug.Log("🧹 Eliminado Assets/_CariocaTrashBackup (esto era lo que Unity seguía compilando).");
            }

            // 3) Borrar .bak y “.cs.<timestamp>.bak” dentro de Assets/Scripts/CariocaRuntime
            DeleteAllBakFiles(RuntimeDir);

            // 4) Borrar scripts editor basura (opcional pero recomendado)
            foreach (var p in KnownJunkEditorScripts)
            {
                if (File.Exists(p))
                {
                    string abs = Path.Combine(projectRoot, p.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    string dest = Path.Combine(externalBackupRoot, "DeletedEditorScripts");
                    Directory.CreateDirectory(dest);

                    File.Copy(abs, Path.Combine(dest, Path.GetFileName(abs)), true);
                    string meta = abs + ".meta";
                    if (File.Exists(meta)) File.Copy(meta, Path.Combine(dest, Path.GetFileName(meta)), true);

                    AssetDatabase.DeleteAsset(p);
                    Debug.Log($"🧹 Borrado junk editor: {p}");
                }
            }

            // 5) Reescribir core estable (CardModel + Deck + Drag/Drop + CardView)
            WriteTextWithExternalBackup(projectRoot, externalBackupRoot,
                $"{RuntimeDir}/CardModel.cs", GetCardModelStable());

            WriteTextWithExternalBackup(projectRoot, externalBackupRoot,
                $"{RuntimeDir}/Deck.cs", GetDeckStable());

            WriteTextWithExternalBackup(projectRoot, externalBackupRoot,
                $"{RuntimeDir}/DropZone.cs", GetDropZoneStable());

            WriteTextWithExternalBackup(projectRoot, externalBackupRoot,
                $"{RuntimeDir}/CariocaDragBus.cs", GetDragBusStable());

            WriteTextWithExternalBackup(projectRoot, externalBackupRoot,
                $"{RuntimeDir}/CardDragHandler.cs", GetCardDragHandlerStable());

            WriteTextWithExternalBackup(projectRoot, externalBackupRoot,
                $"{RuntimeDir}/CardView.cs", GetCardViewStable());

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            Debug.Log("✅ RESET + FIX terminado. Ahora deja que Unity compile (arriba a la derecha). Luego dale Play.");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ RESET + FIX falló:\n" + e);
        }
    }

    // -------------------- helpers --------------------

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

    private static void DeleteAllBakFiles(string unityFolder)
    {
        string[] guids = AssetDatabase.FindAssets("", new[] { unityFolder });
        int deleted = 0;

        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);

            // Borra *.bak y también *.cs.<algo>.bak
            if (path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            {
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }
        }

        Debug.Log($"🧹 Borrados {deleted} archivos .bak dentro de {unityFolder}");
    }

    private static void WriteTextWithExternalBackup(string projectRoot, string externalBackupRoot, string unityPath, string content)
    {
        string absPath = Path.Combine(projectRoot, unityPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Directory.CreateDirectory(Path.GetDirectoryName(absPath) ?? projectRoot);

        // backup del archivo anterior (si existe)
        if (File.Exists(absPath))
        {
            string destDir = Path.Combine(externalBackupRoot, "OverwrittenFiles");
            Directory.CreateDirectory(destDir);
            File.Copy(absPath, Path.Combine(destDir, Path.GetFileName(absPath)), true);

            string meta = absPath + ".meta";
            if (File.Exists(meta))
                File.Copy(meta, Path.Combine(destDir, Path.GetFileName(meta)), true);
        }

        File.WriteAllText(absPath, content, new UTF8Encoding(false));
        Debug.Log($"✍️ Escrito: {unityPath}");
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(sourceDir.Length).TrimStart('\\', '/');
            string dst = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? destDir);
            File.Copy(file, dst, true);
        }
    }

    // -------------------- stable code --------------------

    private static string GetCardModelStable() => @"
using System;

namespace CariocaRuntime
{
    // ✅ Modelo estable (simple)
    public enum Suit { Clubs, Diamonds, Hearts, Spades }

    public enum Rank
    {
        Joker = 0,
        Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
        Jack, Queen, King
    }

    public readonly struct Card
    {
        public readonly Suit? Suit;   // null = Joker
        public readonly Rank Rank;

        public bool IsJoker => Rank == Rank.Joker;

        public Card(Suit? suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public override string ToString()
        {
            if (IsJoker) return ""JOKER"";

            string rankStr = Rank switch
            {
                Rank.Ace => ""A"",
                Rank.Jack => ""J"",
                Rank.Queen => ""Q"",
                Rank.King => ""K"",
                _ => ((int)Rank).ToString()
            };

            string suitStr = Suit switch
            {
                CariocaRuntime.Suit.Clubs => ""♣"",
                CariocaRuntime.Suit.Diamonds => ""♦"",
                CariocaRuntime.Suit.Hearts => ""♥"",
                CariocaRuntime.Suit.Spades => ""♠"",
                _ => """"
            };

            return $""{rankStr}{suitStr}"";
        }
    }
}
".Trim();

    private static string GetDeckStable() => @"
using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    // ✅ Deck estable: 52 + 2 jokers = 54
    public sealed class Deck
    {
        private readonly List<Card> _cards = new List<Card>();
        private readonly System.Random _rng = new System.Random();

        public int Count => _cards.Count;

        // ✅ Constructor vacío (necesario)
        public Deck() { }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (int r = 1; r <= 13; r++)
                {
                    var rank = (Rank)r;
                    _cards.Add(new Card(suit, rank));
                }
            }

            _cards.Add(new Card(null, Rank.Joker));
            _cards.Add(new Card(null, Rank.Joker));
        }

        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var tmp = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = tmp;
            }
        }

        public Card Draw()
        {
            if (_cards.Count == 0) throw new InvalidOperationException(""Deck vacío"");
            int last = _cards.Count - 1;
            var top = _cards[last];
            _cards.RemoveAt(last);
            return top;
        }

        public void AddRange(IEnumerable<Card> cards)
        {
            _cards.AddRange(cards);
        }
    }
}
".Trim();

    private static string GetDropZoneStable() => @"
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

    private static string GetDragBusStable() => @"
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

    private static string GetCardDragHandlerStable() => @"
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

        public void OnBeginDrag(PointerEventData eventData)
        {
            OnBegin?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = _canvas ? _canvas.scaleFactor : 1f;
            var delta = eventData.delta / Mathf.Max(0.0001f, scale);
            OnDragDelta?.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnEnd?.Invoke(eventData);
        }
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
        [Header(""UI refs (auto si no se asignan)"")]
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

            if (label == null)
                label = GetComponentInChildren<TextMeshProUGUI>(true);

            if (glow == null)
            {
                var t = transform.Find(""Glow"");
                if (t != null) glow = t.gameObject;
            }

            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

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

        // ✅ Overloads para compatibilidad
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
