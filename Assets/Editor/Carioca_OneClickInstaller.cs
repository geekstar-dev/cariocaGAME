#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Carioca_OneClickInstaller
{
    private const string ScenesFolder = "Assets/Scenes";
    private const string ScriptsFolder = "Assets/Scripts";

    private const string Scene_Home = "Home";
    private const string Scene_SoloSetup = "SoloSetup";
    private const string Scene_MultiplayerMenu = "MultiplayerMenu";
    private const string Scene_Options = "Options";
    private const string Scene_GameTable = "GameTable";

    [MenuItem("Tools/CARIOCA/One Click Install (Scenes + UI)")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Sal de Play Mode antes de instalar.");
            return;
        }

        try
        {
            EnsureFolders();
            WriteSceneRouterScript();
            AssetDatabase.Refresh();

            BuildAllScenes();
            SetBuildScenes();

            Debug.Log("✅ CARIOCA instalado: carpetas + scripts + escenas + UI + EventSystem (Input System friendly) + Build list.");
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ CARIOCA One Click Install falló:\n" + ex);
        }
    }

    // ---------------- folders & scripts ----------------

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Scripts");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "UI");
        EnsureFolder("Assets", "Art");
    }

    private static void EnsureFolder(string parent, string name)
    {
        var path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static void WriteSceneRouterScript()
    {
        var path = $"{ScriptsFolder}/SceneRouter.cs";
        if (File.Exists(path)) return; // no lo pisamos si ya lo editaste

        var code =
@"using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRouter : MonoBehaviour
{
    public void GoHome() => Load(""Home"");
    public void GoSoloSetup() => Load(""SoloSetup"");
    public void GoMultiplayerMenu() => Load(""MultiplayerMenu"");
    public void GoOptions() => Load(""Options"");
    public void GoGameTable() => Load(""GameTable"");

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Load(string sceneName) => SceneManager.LoadScene(sceneName);
}";
        File.WriteAllText(path, code);
    }

    // ---------------- build scenes ----------------

    private static void BuildAllScenes()
    {
        BuildScene_Home();
        BuildScene_SoloSetup();
        BuildScene_MultiplayerMenu();
        BuildScene_Options();
        BuildScene_GameTable();
    }

    private static void BuildScene_Home()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EnsureEventSystem_InputSystemFriendly();
        var routerComp = CreateRouterComponent();

        var canvas = CreateCanvas("Canvas");
        CreateTitle(canvas.transform, "CARIOCA");

        var panel = CreateVerticalMenuPanel(canvas.transform);

        var bSolo = CreateButton(panel, "Solo");
        BindButton(bSolo, routerComp, "GoSoloSetup");

        var bMulti = CreateButton(panel, "Multijugador");
        BindButton(bMulti, routerComp, "GoMultiplayerMenu");

        var bOpt = CreateButton(panel, "Opciones");
        BindButton(bOpt, routerComp, "GoOptions");

        var bExit = CreateButton(panel, "Salir");
        BindButton(bExit, routerComp, "Quit");

        SaveScene(scene, Scene_Home);
    }

    private static void BuildScene_SoloSetup()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EnsureEventSystem_InputSystemFriendly();
        var routerComp = CreateRouterComponent();

        var canvas = CreateCanvas("Canvas");
        CreateTitle(canvas.transform, "SOLITARIO");
        CreateSubtitle(canvas.transform, "Aquí se eligen reglas/presets (Chile varía por casa).");

        var bottom = CreateBottomButtons(canvas.transform);

        var back = CreateButton(bottom, "Volver");
        BindButton(back, routerComp, "GoHome");

        var play = CreateButton(bottom, "Jugar");
        BindButton(play, routerComp, "GoGameTable");

        SaveScene(scene, Scene_SoloSetup);
    }

    private static void BuildScene_MultiplayerMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EnsureEventSystem_InputSystemFriendly();
        var routerComp = CreateRouterComponent();

        var canvas = CreateCanvas("Canvas");
        CreateTitle(canvas.transform, "MULTIJUGADOR");
        CreateSubtitle(canvas.transform, "Después conectamos Steam: Host / Join / Invite / Room Code.");

        var panel = CreateVerticalMenuPanel(canvas.transform);

        var host = CreateButton(panel, "Crear sala (Host)");
        BindButton(host, routerComp, "GoGameTable"); // placeholder

        var join = CreateButton(panel, "Unirse (Código)");
        BindButton(join, routerComp, "GoGameTable"); // placeholder

        var invite = CreateButton(panel, "Invitar (Steam)");
        BindButton(invite, routerComp, "GoGameTable"); // placeholder

        var back = CreateButton(panel, "Volver");
        BindButton(back, routerComp, "GoHome");

        SaveScene(scene, Scene_MultiplayerMenu);
    }

    private static void BuildScene_Options()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EnsureEventSystem_InputSystemFriendly();
        var routerComp = CreateRouterComponent();

        var canvas = CreateCanvas("Canvas");
        CreateTitle(canvas.transform, "OPCIONES");
        CreateSubtitle(canvas.transform, "Audio / Controles / Ordenar mano: manual o automático (luego).");

        var bottom = CreateBottomButtons(canvas.transform);
        var back = CreateButton(bottom, "Volver");
        BindButton(back, routerComp, "GoHome");

        SaveScene(scene, Scene_Options);
    }

    private static void BuildScene_GameTable()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EnsureEventSystem_InputSystemFriendly();
        var routerComp = CreateRouterComponent();

        var canvas = CreateCanvas("Canvas");
        CreateTitle(canvas.transform, "MESA (PROTOTIPO)");
        CreateSubtitle(canvas.transform, "Aquí irá: mazo, descarte, banca, mano, turnos.");

        var bottom = CreateBottomButtons(canvas.transform);
        var back = CreateButton(bottom, "Volver al menú");
        BindButton(back, routerComp, "GoHome");

        SaveScene(scene, Scene_GameTable);
    }

    private static void SaveScene(Scene scene, string sceneName)
    {
        var path = $"{ScenesFolder}/{sceneName}.unity";
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void SetBuildScenes()
    {
        var scenePaths = new[]
        {
            $"{ScenesFolder}/{Scene_Home}.unity",
            $"{ScenesFolder}/{Scene_SoloSetup}.unity",
            $"{ScenesFolder}/{Scene_MultiplayerMenu}.unity",
            $"{ScenesFolder}/{Scene_Options}.unity",
            $"{ScenesFolder}/{Scene_GameTable}.unity",
        };

        var list = EditorBuildSettings.scenes.ToList();
        list.RemoveAll(s => scenePaths.Contains(s.path));

        // Orden recomendado
        var ordered = scenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        ordered.AddRange(list);

        EditorBuildSettings.scenes = ordered
            .GroupBy(s => s.path)
            .Select(g => g.First())
            .ToArray();
    }

    // ---------------- Router + binding (FIXED) ----------------

    private static Component CreateRouterComponent()
    {
        var go = new GameObject("Router");
        var routerType = FindType("SceneRouter");
        if (routerType == null)
        {
            Debug.LogWarning("SceneRouter aún no compiló. Espera a que Unity compile y vuelve a ejecutar Tools → CARIOCA → One Click Install.");
            return null;
        }

        // ✅ FIX: NO usar MonoBehaviour como tipo. Usar el tipo real.
        return go.AddComponent(routerType);
    }

    private static void BindButton(Button button, Component routerComponent, string methodName)
    {
        if (button == null || routerComponent == null) return;

        var mi = routerComponent.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        if (mi == null)
        {
            Debug.LogWarning($"No encontré método {methodName} en {routerComponent.GetType().Name}");
            return;
        }

        button.onClick.RemoveAllListeners();

        var action = Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), routerComponent, mi) as UnityEngine.Events.UnityAction;
        if (action == null)
        {
            Debug.LogWarning($"No pude crear delegate para {methodName}");
            return;
        }

        // Binding persistente para que quede grabado en la escena
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    // ---------------- EventSystem (Input System friendly) ----------------

    private static void EnsureEventSystem_InputSystemFriendly()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();

        // Preferimos InputSystemUIInputModule si está instalado
        var inputModuleType = FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        if (inputModuleType != null)
            esGO.AddComponent(inputModuleType);
        else
            esGO.AddComponent<StandaloneInputModule>();
    }

    // ---------------- UI helpers (TMP if available, else legacy Text) ----------------

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

    private static void CreateTitle(Transform parent, string text)
    {
        var title = CreateText(parent, "Title", text, 80, TextAnchor.UpperCenter);
        var rt = title.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.78f);
        rt.anchorMax = new Vector2(0.9f, 0.95f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CreateSubtitle(Transform parent, string text)
    {
        var sub = CreateText(parent, "Subtitle", text, 26, TextAnchor.UpperCenter);
        var rt = sub.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.66f);
        rt.anchorMax = new Vector2(0.9f, 0.78f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var g = sub.GetComponent<Graphic>();
        if (g != null) g.color = new Color(g.color.r, g.color.g, g.color.b, 0.85f);
    }

    private static Transform CreateVerticalMenuPanel(Transform parent)
    {
        var panel = new GameObject("MenuPanel", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.35f, 0.25f);
        rt.anchorMax = new Vector2(0.65f, 0.65f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 14;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return panel.transform;
    }

    private static Transform CreateBottomButtons(Transform parent)
    {
        var holder = new GameObject("BottomButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(parent, false);

        var rt = holder.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.08f);
        rt.anchorMax = new Vector2(0.8f, 0.18f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var hlg = holder.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 20;
        hlg.padding = new RectOffset(10, 10, 10, 10);

        return holder.transform;
    }

    private static Button CreateButton(Transform parent, string label)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.92f);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(520, 90);

        // Texto del botón (TMP si está, si no legacy)
        var textGO = CreateText(go.transform, "Text", label, 30, TextAnchor.MiddleCenter);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        // Color texto si es legacy
        var legacyText = textGO.GetComponent<Text>();
        if (legacyText != null) legacyText.color = Color.black;

        return go.GetComponent<Button>();
    }

    private static GameObject CreateText(Transform parent, string name, string text, int size, TextAnchor anchor)
    {
        // TMP si existe
        var tmpType = FindType("TMPro.TextMeshProUGUI");
        if (tmpType != null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var comp = go.AddComponent(tmpType);

            tmpType.GetProperty("text")?.SetValue(comp, text);
            tmpType.GetProperty("fontSize")?.SetValue(comp, (float)size);

            var alignType = FindType("TMPro.TextAlignmentOptions");
            if (alignType != null)
            {
                object align = anchor switch
                {
                    TextAnchor.UpperCenter => Enum.Parse(alignType, "Top", true),
                    TextAnchor.MiddleCenter => Enum.Parse(alignType, "Center", true),
                    TextAnchor.LowerCenter => Enum.Parse(alignType, "Bottom", true),
                    _ => Enum.Parse(alignType, "Center", true)
                };
                tmpType.GetProperty("alignment")?.SetValue(comp, align);
            }

            tmpType.GetProperty("color")?.SetValue(comp, Color.white);
            return go;
        }

        // Fallback: UI Text (Unity 6 usa LegacyRuntime.ttf)
        var legacy = new GameObject(name, typeof(RectTransform), typeof(Text));
        legacy.transform.SetParent(parent, false);
        var t = legacy.GetComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return legacy;
    }

    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
#endif
