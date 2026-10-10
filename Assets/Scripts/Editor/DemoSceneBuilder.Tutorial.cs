using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Segunda parte del montador: el TUTORIAL y el MENÚ PRINCIPAL.
// Uso: menú  Tools → The House Special → Montar tutorial y menú principal
// - Copia la escena Demo_Restaurante tal y como la tengas guardada (con tus cambios) a Assets/Escenas/Tutorial,
//   le deja una sola mesa, apaga el reloj y añade el TutorialManager con su panel, botones y flecha.
// - Crea Assets/Escenas/MenuPrincipal con los botones Tutorial / Jugar / Salir.
// - Ordena las escenas en Build Profiles: Menú → Tutorial → Demo.
// Se puede lanzar las veces que haga falta: rehace las dos escenas desde cero (la demo no se toca).
public static partial class DemoSceneBuilder
{
    private const string ScenesFolder = "Assets/Escenas";
    private const string TutorialScenePath = ScenesFolder + "/Tutorial.unity";
    private const string MenuScenePath = ScenesFolder + "/MenuPrincipal.unity";
    private const string ArrowSpritePath = ScenesFolder + "/Flecha.png";
    private const string TutorialTableName = "Table_2";

    [UnityEditor.MenuItem("Tools/The House Special/Montar tutorial y menú principal")]
    public static void BuildTutorialAndMenu()
    {
        if (!System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, ScenePath)))
        {
            EditorUtility.DisplayDialog("Montar tutorial", "No encuentro la escena " + ScenePath + ". Monta primero la escena demo.", "Vale");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            EditorUtility.DisplayProgressBar("Montar tutorial", "Preparando...", 0.1f);
            if (!AssetDatabase.IsValidFolder(ScenesFolder)) AssetDatabase.CreateFolder("Assets", "Escenas");
            square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesFolder + "/Cuadrado.png");
            LoadSpriteMaterial();
            uiLayer = LayerMask.NameToLayer("UI");
            Sprite arrowSprite = CreateArrowSprite();

            EditorUtility.DisplayProgressBar("Montar tutorial", "Creando la escena del tutorial...", 0.3f);
            BuildTutorialScene(arrowSprite);

            EditorUtility.DisplayProgressBar("Montar tutorial", "Creando el menú principal...", 0.7f);
            BuildMenuScene();

            AddMenuAndTutorialToBuild();
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("¡Listo!",
                "Creadas Assets/Escenas/MenuPrincipal y Assets/Escenas/Tutorial.\n\n" +
                "Está abierto el menú principal: dale a Play para probarlo todo desde el principio.\n\n" +
                "Para seguir editando el restaurante, vuelve a abrir Assets/Demo/Demo_Restaurante.", "Vale");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Error al montar el tutorial",
                "Algo ha fallado. Copia el error rojo de la Console y pásaselo a Claude.\n\n" + e.Message, "Vale");
        }
    }

    // Triángulo que apunta hacia abajo
    private static Sprite CreateArrowSprite()
    {
        return MakeSpriteAt(ArrowSpritePath, 64, 64, (x, y) =>
        {
            float halfWidth = (y + 0.5f) / 64f * 32f; // abajo en punta, arriba ancho
            return halfWidth - Mathf.Abs(x + 0.5f - 32f) + 0.5f;
        });
    }

    // ------------------------------------------------------------------ ESCENA DEL TUTORIAL

    private static void BuildTutorialScene(Sprite arrowSprite)
    {
        // Copia de la demo tal y como está guardada
        var demo = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(demo, TutorialScenePath, true);
        var scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

        // Una sola mesa
        Table keep = null;
        foreach (Table t in Object.FindObjectsByType<Table>())
            if (t.name == TutorialTableName) keep = t;
        foreach (Table t in Object.FindObjectsByType<Table>())
        {
            if (keep == null) keep = t; // por si se ha cambiado el nombre: nos quedamos la primera
            if (t != keep) Object.DestroyImmediate(t.gameObject);
        }

        // Sin clientes automáticos y sin reloj
        var spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner != null)
        {
            var so = new SerializedObject(spawner);
            so.FindProperty("spawningEnabled").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var day = Object.FindAnyObjectByType<DayManager>();
        if (day != null) day.enabled = false;
        var clock = GameObject.Find("TextoReloj");
        if (clock != null && clock.GetComponent<TMP_Text>() != null)
        {
            var clockText = clock.GetComponent<TMP_Text>();
            clockText.text = "TUTORIAL";
            clockText.fontSize = 32f;
            clockText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // La camarera empieza más arriba, para que no la tape el panel del tutorial
        var waitressGo = GameObject.Find("Waitress");
        if (waitressGo != null) waitressGo.transform.position = new Vector3(4f, -1.5f, 0f);

        // Interfaz del tutorial
        var tpvPanel = Object.FindAnyObjectByType<TpvPanel>();
        Transform canvas = tpvPanel.transform;
        var bottom = new Vector2(0.5f, 0f);

        var panel = UI("PanelTutorial", canvas);
        Place(panel, bottom, new Vector2(0f, 180f), new Vector2(1400f, 140f));
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        bg.raycastTarget = false; // se puede clicar el juego a través del panel

        var text = MakeText("Texto", panel.transform, "", 32f, Color.white);
        Stretch(text.gameObject, 14f);
        text.rectTransform.offsetMax = new Vector2(-270f, -14f); // sitio para el botón Continuar
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;

        var continueButton = MakeButton("BtnContinuar", panel.transform, "Continuar", new Vector2(1f, 0.5f),
            new Vector2(-135f, 0f), new Vector2(230f, 90f), new Color(0.25f, 0.65f, 0.3f), 34f);
        var skipButton = MakeButton("BtnSaltarTutorial", canvas, "Saltar tutorial >>", new Vector2(0.5f, 1f),
            new Vector2(250f, -50f), new Vector2(300f, 60f), new Color(0.35f, 0.35f, 0.42f), 30f);

        // Que se dibujen debajo de la pantalla en negro y de la pausa
        var blackout = canvas.Find("PanelNegro");
        if (blackout != null)
        {
            int index = blackout.GetSiblingIndex();
            skipButton.transform.SetSiblingIndex(index);
            panel.transform.SetSiblingIndex(index);
        }

        // Flecha
        var arrow = Obj("FlechaTutorial", null, Vector3.zero, new Vector3(0.6f, 0.5f, 1f));
        var arrowSr = AddSprite(arrow, arrowSprite, new Color(1f, 0.45f, 0.1f), 100);
        arrowSr.sortingOrder = 100;
        arrow.SetActive(false);

        // El director del tutorial
        var go = new GameObject("Tutorial");
        var tm = go.AddComponent<TutorialManager>();
        Set(tm, "panel", panel);
        Set(tm, "messageText", text);
        Set(tm, "continueButton", continueButton);
        Set(tm, "skipButton", skipButton);
        Set(tm, "arrow", arrow.transform);
        Set(tm, "spawner", spawner);
        Set(tm, "table", keep);
        Set(tm, "tpv", FindTransform("TPV"));
        Set(tm, "menus", FindTransform("Cartas"));
        Set(tm, "cutlery", FindTransform("Cubiertos"));
        Set(tm, "dishReturn", FindTransform("Barra_Sucios"));
        Set(tm, "storeroom", FindTransform("Trastero"));
        Set(tm, "waitress", Object.FindAnyObjectByType<WaitressController>());
        Set(tm, "tpvPanel", tpvPanel);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform FindTransform(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) Debug.LogWarning($"Tutorial: no encuentro '{name}' en la escena.");
        return go != null ? go.transform : null;
    }

    // ------------------------------------------------------------------ MENÚ PRINCIPAL

    private static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.1f, 0.11f);
        camGo.AddComponent<AudioListener>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var canvasGo = new GameObject("Canvas");
        canvasGo.layer = uiLayer;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var c = canvasGo.transform;
        var center = new Vector2(0.5f, 0.5f);

        var title = MakeText("Titulo", c, "THE HOUSE SPECIAL", 120f, new Color(1f, 0.85f, 0.6f));
        Place(title.gameObject, center, new Vector2(0f, 270f), new Vector2(1600f, 160f));
        var subtitle = MakeText("Subtitulo", c, "Demo · Día 1", 44f, new Color(0.85f, 0.8f, 0.75f));
        Place(subtitle.gameObject, center, new Vector2(0f, 170f), new Vector2(800f, 70f));
        var hint = MakeText("Pista", c, "¿Primera vez? Empieza por el tutorial", 30f, new Color(0.7f, 0.65f, 0.6f));
        Place(hint.gameObject, center, new Vector2(0f, 60f), new Vector2(900f, 50f));

        var tutorial = MakeButton("BtnTutorial", c, "Tutorial", center, new Vector2(0f, -30f), new Vector2(440f, 95f), new Color(0.25f, 0.65f, 0.3f), 42f);
        var play = MakeButton("BtnJugar", c, "Jugar", center, new Vector2(0f, -145f), new Vector2(440f, 95f), new Color(0.25f, 0.5f, 0.95f), 42f);
        var quit = MakeButton("BtnSalir", c, "Salir", center, new Vector2(0f, -260f), new Vector2(440f, 80f), new Color(0.35f, 0.35f, 0.42f), 36f);

        var menu = canvasGo.AddComponent<MainMenu>();
        Set(menu, "tutorialButton", tutorial);
        Set(menu, "playButton", play);
        Set(menu, "quitButton", quit);

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    // ------------------------------------------------------------------ BUILD PROFILES

    // Orden: Menú (el que sale al abrir el juego) → Tutorial → Demo → el resto
    private static void AddMenuAndTutorialToBuild()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == MenuScenePath || s.path == TutorialScenePath || s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(TutorialScenePath, true));
        scenes.Insert(2, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
