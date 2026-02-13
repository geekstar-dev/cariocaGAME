#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Ajusta el namespace si tu script está en otro.
// Esto asume que YA tienes el archivo anterior (CariocaPrototypeBootstrap.cs) en el proyecto.
using CariocaPrototype;

public static class CariocaAutoSceneBuilder
{
    private const string MenuPath = "Tools/CARIOCA/Build UI Prototype (Deck + Deal + Hands)";

    [MenuItem(MenuPath)]
    public static void Build()
    {
        // 1) Asegurar Canvas + EventSystem
        var canvas = EnsureCanvas();
        EnsureEventSystem();

        // 2) Crear Prefab de carta (en Assets/_Project/Prefabs si existe, sino lo crea)
        var cardPrefab = CreateOrUpdateCardPrefab();

        // 3) Crear GameRoot y componentes
        var gameRoot = GameObject.Find("GameRoot");
        if (gameRoot == null) gameRoot = new GameObject("GameRoot");

        EnsureComponent<SkinManager>(gameRoot, "SkinManager");
        EnsureComponent<SteamBootstrap>(gameRoot, "SteamBootstrap");
        var controller = EnsureComponent<GameSequenceController>(gameRoot, "GameSequenceController");

        // 4) Crear “Deck” visual
        var deckGO = GameObject.Find("Deck");
        if (deckGO == null)
        {
            deckGO = CreateDeckVisual(canvas.transform);
            deckGO.name = "Deck";
        }

        controller.deckTransform = deckGO.transform;
        controller.cardPrefab = cardPrefab;

        // 5) Crear seats 2–4 (dejamos 4 preparados)
        var seatsRoot = GameObject.Find("Seats");
        if (seatsRoot == null)
        {
            seatsRoot = new GameObject("Seats");
            seatsRoot.transform.SetParent(canvas.transform, false);
        }

        var seats = new List<PlayerSeat>();
        for (int i = 0; i < 4; i++)
        {
            var seat = EnsureSeat(seatsRoot.transform, i);
            seats.Add(seat);
        }

        controller.seats = seats;

        // 6) Marcar escena dirty
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = gameRoot;
        Debug.Log("CARIOCA: escena armada (Deck + Deal + Hands + CardPrefab).");
    }

    // -----------------------------
    // Canvas / EventSystem
    // -----------------------------
    private static Canvas EnsureCanvas()
{
#if UNITY_2023_1_OR_NEWER
    var canvas = Object.FindFirstObjectByType<Canvas>();
#else
    var canvas = Object.FindObjectOfType<Canvas>();
#endif
    if (canvas != null) return canvas;

    var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
    canvas = go.GetComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;

    var scaler = go.GetComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920, 1080);
    scaler.matchWidthOrHeight = 0.5f;

    return canvas;
}

private static void EnsureEventSystem()
{
#if UNITY_2023_1_OR_NEWER
    if (Object.FindFirstObjectByType<EventSystem>() != null) return;
#else
    if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
    new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
}


    // -----------------------------
    // Prefab card
    // -----------------------------
    private static GameObject CreateOrUpdateCardPrefab()
    {
        const string folderRoot = "Assets/_Project/Prefabs";
        EnsureFolder("Assets/_Project");
        EnsureFolder(folderRoot);

        string prefabPath = $"{folderRoot}/CardPrefab_Text.prefab";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
        {
            // Si ya existe, lo devolvemos sin tocarlo (para no romper tu layout si lo editaste a mano).
            return existing;
        }

        // Creamos un GO temporal, lo guardamos como prefab, luego lo destruimos.
        var go = BuildCardPrefabGameObject();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefab;
    }

    private static GameObject BuildCardPrefabGameObject()
    {
        var root = new GameObject("CardPrefab_Text", typeof(RectTransform), typeof(CanvasGroup));
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 220);

        var view = root.AddComponent<CardViewText>();
        view.rect = rt;

        // FrontRoot
        var front = new GameObject("FrontRoot", typeof(RectTransform));
        front.transform.SetParent(root.transform, false);
        var frt = front.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;

        // Fondo carta
        var bgGO = new GameObject("FrontBG", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(front.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        var bg = bgGO.GetComponent<Image>();
        bg.color = new Color(0.95f, 0.95f, 0.95f);
        view.frontBg = bg;

        // Rank (grande)
        var rank = CreateTMP("Rank", front.transform, "A", 64, TextAlignmentOptions.Center);
        rank.rectTransform.anchoredPosition = new Vector2(0, 20);
        view.rankText = rank;

        // Suit (grande/medio)
        var suit = CreateTMP("Suit", front.transform, "♠", 56, TextAlignmentOptions.Center);
        suit.rectTransform.anchoredPosition = new Vector2(0, -35);
        view.suitText = suit;

        // Corner (pequeño)
        var corner = CreateTMP("Corner", front.transform, "A♠", 24, TextAlignmentOptions.TopLeft);
        corner.rectTransform.anchorMin = new Vector2(0, 1);
        corner.rectTransform.anchorMax = new Vector2(0, 1);
        corner.rectTransform.pivot = new Vector2(0, 1);
        corner.rectTransform.anchoredPosition = new Vector2(10, -10);
        corner.rectTransform.sizeDelta = new Vector2(140, 40);
        view.cornerText = corner;

        // BackRoot
        var back = new GameObject("BackRoot", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(root.transform, false);
        var brt = back.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;

        var backImg = back.GetComponent<Image>();
        backImg.color = new Color(0.15f, 0.2f, 0.3f);

        var backText = CreateTMP("BackText", back.transform, "CARIOCA", 36, TextAlignmentOptions.Center);
        backText.color = Color.white;

        // Hook roots
        view.frontRoot = front;
        view.backRoot = back;

        // Por defecto mostrar front
        view.SetFace(true);

        return root;
    }

    private static TMP_Text CreateTMP(string name, Transform parent, string text, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.color = Color.black;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(160, 80);

        return tmp;
    }

    // -----------------------------
    // Deck visual
    // -----------------------------
    private static GameObject CreateDeckVisual(Transform canvas)
    {
        var deck = new GameObject("Deck", typeof(RectTransform), typeof(Image));
        deck.transform.SetParent(canvas, false);

        var rt = deck.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-420, 40);
        rt.sizeDelta = new Vector2(120, 170);

        var img = deck.GetComponent<Image>();
        img.color = new Color(0.15f, 0.2f, 0.3f);

        var txt = CreateTMP("Label", deck.transform, "DECK", 28, TextAlignmentOptions.Center);
        txt.color = Color.white;

        return deck;
    }

    // -----------------------------
    // Seats
    // -----------------------------
    private static PlayerSeat EnsureSeat(Transform seatsRoot, int index)
    {
        string seatName = $"Seat_{index}";
        var seatGO = GameObject.Find(seatName);
        if (seatGO == null)
        {
            seatGO = new GameObject(seatName, typeof(RectTransform));
            seatGO.transform.SetParent(seatsRoot, false);
        }

        // dealTarget
        var dealTarget = seatGO.transform.Find("DealTarget");
        if (dealTarget == null)
        {
            var dt = new GameObject("DealTarget", typeof(RectTransform));
            dt.transform.SetParent(seatGO.transform, false);
            dealTarget = dt.transform;
        }

        // hand container
        var handGO = seatGO.transform.Find("Hand");
        if (handGO == null)
        {
            var h = new GameObject("Hand", typeof(RectTransform));
            h.transform.SetParent(seatGO.transform, false);
            handGO = h.transform;
        }

        var hand = handGO.GetComponent<HandView>();
        if (hand == null) hand = handGO.gameObject.AddComponent<HandView>();

        // Layout posiciones (4 jugadores)
        LayoutSeat((RectTransform)seatGO.transform, (RectTransform)dealTarget, (RectTransform)handGO, index);

        // Construir PlayerSeat
        var ps = new PlayerSeat
        {
            isLocalPlayer = (index == 0),  // Seat_0 local
            dealTarget = dealTarget,
            hand = hand
        };

        return ps;
    }

    private static void LayoutSeat(RectTransform seatRT, RectTransform dealTargetRT, RectTransform handRT, int i)
    {
        seatRT.anchorMin = seatRT.anchorMax = new Vector2(0.5f, 0.5f);
        seatRT.sizeDelta = Vector2.zero;

        // Seat 0: abajo (local)
        // Seat 1: arriba
        // Seat 2: izquierda
        // Seat 3: derecha
        Vector2 handPos = Vector2.zero;
        Vector2 dealPos = Vector2.zero;

        switch (i)
        {
            case 0: handPos = new Vector2(0, -360); dealPos = new Vector2(-140, -220); break;
            case 1: handPos = new Vector2(0, 360); dealPos = new Vector2(-140, 220); break;
            case 2: handPos = new Vector2(-760, 0); dealPos = new Vector2(-520, 40); break;
            case 3: handPos = new Vector2(760, 0); dealPos = new Vector2(520, 40); break;
        }

        // dealTarget
        dealTargetRT.anchorMin = dealTargetRT.anchorMax = new Vector2(0.5f, 0.5f);
        dealTargetRT.pivot = new Vector2(0.5f, 0.5f);
        dealTargetRT.anchoredPosition = dealPos;
        dealTargetRT.sizeDelta = new Vector2(10, 10);

        // hand container (donde se parentan cartas)
        handRT.anchorMin = handRT.anchorMax = new Vector2(0.5f, 0.5f);
        handRT.pivot = new Vector2(0.5f, 0.5f);
        handRT.anchoredPosition = handPos;
        handRT.sizeDelta = new Vector2(900, 260);

        // Tuning por orientación
        var hv = handRT.GetComponent<HandView>();
        if (hv != null)
        {
            hv.maxAngle = 36f;
            hv.spacing = 90f;
            hv.verticalCurve = 40f;

            // Para manos laterales (izq/der) se ve mejor con menos abanico
            if (i == 2 || i == 3)
            {
                hv.maxAngle = 18f;
                hv.spacing = 70f;
                hv.verticalCurve = 18f;
            }
        }
    }

    // -----------------------------
    // Helpers
    // -----------------------------
    private static T EnsureComponent<T>(GameObject root, string childName) where T : Component
    {
        var t = root.transform.Find(childName);
        GameObject go;
        if (t == null)
        {
            go = new GameObject(childName);
            go.transform.SetParent(root.transform, false);
        }
        else go = t.gameObject;

        var comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        // Crear recursivo
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
