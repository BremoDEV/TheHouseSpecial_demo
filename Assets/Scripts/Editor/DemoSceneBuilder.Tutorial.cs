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
// - Copia la escena Dia_3 tal y como la tengas guardada (con tus cambios) a Assets/Escenas/Tutorial,
//   le deja una sola mesa, apaga el reloj y añade el TutorialManager con su panel, botones y flecha.
// - Crea Assets/Escenas/MenuPrincipal con los botones Tutorial / Jugar / Salir.
// - Ordena las escenas en Build Profiles: Menú → Tutorial → Demo.
// Se puede lanzar las veces que haga falta: rehace las dos escenas desde cero (la demo no se toca).
public static partial class DemoSceneBuilder
{
    private const string ScenesFolder = "Assets/Escenas";
    private const string TutorialScenePath = ScenesFolder + "/Tutorial.unity";
    private const string MenuScenePath = ScenesFolder + "/MenuPrincipal.unity";
    private const string ArrowSpritePath = "Assets/Arte/Generados/Flecha.png";
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
                "Para seguir editando el restaurante, vuelve a abrir Assets/Escenas/Dia_3.", "Vale");
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

        // Panel: una libreta como la del menú, con letra hecha a mano
        var panel = UI("PanelTutorial", canvas);
        Place(panel, bottom, new Vector2(-100f, 160f), new Vector2(1200f, 270f)); // un poco a la izquierda: no tapa la etiqueta CARTAS
        var bg = panel.AddComponent<Image>();
        bg.sprite = LoadMenuSprite("menu_libreta_tutorial", false);
        bg.color = Color.white;
        bg.raycastTarget = false; // se puede clicar el juego a través del panel

        TMP_FontAsset hand = GetHandwritingFont();
        var ink = new Color32(40, 38, 72, 255);   // azul tinta, como las comandas
        var pink = new Color32(196, 70, 100, 255); // rosa de la libreta

        var text = MakeText("Texto", panel.transform, "", 36f, ink);
        Stretch(text.gameObject, 0f);
        // Dentro del papel: a la derecha del margen rojo y por encima de la fila de "Continuar"
        text.rectTransform.offsetMin = new Vector2(96f, 60f);
        text.rectTransform.offsetMax = new Vector2(-50f, -72f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true; // los textos largos encogen un poco para caber
        text.fontSizeMin = 24f;
        text.fontSizeMax = 36f;
        if (hand != null) text.font = hand;

        // "Continuar >>" escrito en la libreta (sin botón)
        var contGo = UI("BtnContinuar", panel.transform);
        Place(contGo, new Vector2(1f, 0f), new Vector2(-150f, 42f), new Vector2(230f, 46f));
        var hit = contGo.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f); // invisible, solo para poder clicar
        var continueButton = contGo.AddComponent<Button>();
        var contText = MakeText("Texto", contGo.transform, "Continuar >>", 34f, Color.white);
        Stretch(contText.gameObject, 0f);
        contText.alignment = TextAlignmentOptions.MidlineRight;
        contText.textWrappingMode = TextWrappingModes.NoWrap;
        if (hand != null) contText.font = hand;
        continueButton.targetGraphic = contText;
        var colors = continueButton.colors;
        colors.normalColor = pink;
        colors.highlightedColor = new Color32(140, 30, 70, 255);
        colors.pressedColor = new Color32(100, 20, 50, 255);
        colors.selectedColor = pink;
        continueButton.colors = colors;

        // "Saltar tutorial": una comanda suelta arriba (como las del menú, sin libreta detrás)
        var skipImg = MenuImage("BtnSaltarTutorial", canvas, "menu_comanda_saltar", new Vector2(0.5f, 1f),
            new Vector2(250f, -62f), new Vector2(360f, 109f), 1.5f, true);
        var skipButton = skipImg.gameObject.AddComponent<Button>();
        skipButton.targetGraphic = skipImg;
        skipButton.transition = Selectable.Transition.None; // la animación la hace TicketButton
        skipImg.gameObject.AddComponent<TicketButton>();

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

        // Arte del menú (Assets/Arte/Menu): fondo, logo, libreta y una comanda por botón
        var fondo = MenuImage("Fondo", c, "menu_fondo", center, Vector2.zero, new Vector2(1920f, 1080f), 0f);
        fondo.raycastTarget = false;
        fondo.preserveAspect = false;
        Stretch(fondo.gameObject, 0f); // ocupa toda la pantalla, sea cual sea su forma
        var logo = MenuImage("Titulo", c, "menu_titulo", center, new Vector2(-370f, 305f), new Vector2(900f, 347f), 0f); // arriba a la izquierda
        logo.raycastTarget = false;
        // La camarera debajo del logo (la cabeza asoma un poco por delante del borde del logo)
        var camarera = MenuImage("Camarera", c, "menu_camarera", center, new Vector2(-300f, -180f), new Vector2(323f, 680f), 0f);
        camarera.raycastTarget = false;
        var libreta = MenuImage("Libreta", c, "menu_libreta", center, new Vector2(390f, -84f), new Vector2(648f, 713f), 0f); // a la derecha
        libreta.raycastTarget = false;

        var tutorial = MenuTicket("BtnTutorial", c, "menu_comanda_tutorial", new Vector2(401f, -14f), -2f);
        var play = MenuTicket("BtnJugar", c, "menu_comanda_jugar", new Vector2(401f, -149f), 1.5f);
        var quit = MenuTicket("BtnSalir", c, "menu_comanda_salir", new Vector2(401f, -284f), -1f);

        var menu = canvasGo.AddComponent<MainMenu>();
        Set(menu, "tutorialButton", tutorial);
        Set(menu, "playButton", play);
        Set(menu, "quitButton", quit);

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    // ------------------------------------------------------------------ ARTE DEL MENÚ

    private const string MenuArtFolder = "Assets/Arte/Menu";
    // Botones, libretas, caja de diálogo, tablet de la TPV... (todo lo que es interfaz)
    private const string UiArtFolder = "Assets/Arte/UI";

    // Dibujos de interfaz que van en Assets/Arte/UI (antes estaban en Assets/Arte/Menu)
    private static readonly string[] UiArtFiles =
    {
        "menu_comanda_tutorial", "menu_comanda_jugar", "menu_comanda_salir",
        "menu_comanda_continuar", "menu_comanda_reiniciar", "menu_comanda_saltar",
        "menu_libreta", "menu_libreta_pausa", "menu_libreta_tutorial",
        "tpv_tablet", "tpv_brillo", "ui_boton", "ui_panel",
    };

    // Ruta de un dibujo del menú o de la interfaz: primero mira en Arte/UI y si no, en Arte/Menu
    private static string MenuArtPath(string fileName)
    {
        string ui = UiArtFolder + "/" + fileName + ".png";
        if (AssetImporter.GetAtPath(ui) != null) return ui;
        return MenuArtFolder + "/" + fileName + ".png";
    }

    // Mueve los dibujos de interfaz a Assets/Arte/UI. Se mueven con Unity, así las escenas no pierden nada.
    [UnityEditor.MenuItem("Tools/The House Special/Ordenar: mover arte de interfaz a Arte-UI")]
    public static void MoveUiArtMenu()
    {
        if (!AssetDatabase.IsValidFolder(UiArtFolder)) AssetDatabase.CreateFolder("Assets/Arte", "UI");
        int moved = 0;
        var errors = new List<string>();
        foreach (string f in UiArtFiles)
        {
            string from = MenuArtFolder + "/" + f + ".png";
            string to = UiArtFolder + "/" + f + ".png";
            if (AssetImporter.GetAtPath(from) == null) continue; // ya movido o no existe
            string error = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(error)) moved++;
            else errors.Add(f + ": " + error);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Arte de interfaz",
            $"Movidos {moved} dibujos a {UiArtFolder}." +
            (errors.Count > 0 ? "\n\nProblemas:\n" + string.Join("\n", errors) : "") +
            "\n\nLas escenas siguen funcionando igual (Unity actualiza las referencias solo).", "Vale");
    }

    // Letra "Nothing You Could Do" (la de la libreta del menú) como fuente de TextMeshPro.
    // La crea la primera vez a partir del .ttf de Assets/Arte/Fuentes.
    private const string FontsFolder = "Assets/Arte/Fuentes";
    private const string HandFontFile = FontsFolder + "/NothingYouCouldDo-Regular.ttf";
    private const string HandFontAssetPath = FontsFolder + "/NothingYouCouldDo SDF.asset";

    private static TMP_FontAsset GetHandwritingFont()
    {
        return GetFontAsset(HandFontFile, HandFontAssetPath, "NothingYouCouldDo");
    }

    // Crea (la primera vez) una fuente de TextMeshPro a partir de un .ttf del proyecto
    private static TMP_FontAsset GetFontAsset(string ttfPath, string assetPath, string baseName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogWarning("[Fuentes] No encuentro " + ttfPath + ". Uso la letra normal.");
            return null;
        }
        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, false);
        if (asset == null) return null;
        asset.name = baseName + " SDF";
        AssetDatabase.CreateAsset(asset, assetPath);
        asset.atlasTextures[0].name = baseName + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = baseName + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        // Todas las letras del castellano ya guardadas (por si en la build no se pudieran generar)
        asset.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                               "¡¿áéíóúüñÁÉÍÓÚÜÑ€ºª«»…·×");
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // Dibujo de interfaz que se estira sin deformar las esquinas (9-slice): 'border' píxeles por cada lado
    private static Sprite LoadUiSprite(string fileName, float border)
    {
        string path = MenuArtPath(fileName);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite LoadMenuSprite(string fileName, bool readable)
    {
        string path = MenuArtPath(fileName);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Menú] No encuentro " + path);
            return null;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = readable; // las comandas lo necesitan para clicar solo sobre el papel
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Image MenuImage(string name, Transform parent, string file, Vector2 anchor, Vector2 pos, Vector2 size, float angle, bool readable = false)
    {
        var go = UI(name, parent);
        Place(go, anchor, pos, size);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.AddComponent<Image>();
        img.sprite = LoadMenuSprite(file, readable);
        img.preserveAspect = true;
        if (img.sprite == null) img.color = new Color(1f, 1f, 1f, 0.2f);
        return img;
    }

    // Botón con forma de comanda (486 de ancho; el alto sale del dibujo)
    private static Button MenuTicket(string name, Transform parent, string file, Vector2 pos, float angle)
    {
        var img = MenuImage(name, parent, file, new Vector2(0.5f, 0.5f), pos, new Vector2(486f, 201f), angle, true);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None; // la animación la hace TicketButton
        img.gameObject.AddComponent<TicketButton>();
        return button;
    }

    // ------------------------------------------------------------------ MENÚ DE PAUSA (libreta con comandas)

    // Cambia el menú de pausa de la escena ABIERTA por la libreta con comandas (Continuar / Reiniciar / Salir)
    [UnityEditor.MenuItem("Tools/The House Special/Actualizar menú de pausa")]
    public static void UpdatePauseMenuMenu()
    {
        if (Object.FindAnyObjectByType<PauseMenu>() == null)
        {
            EditorUtility.DisplayDialog("Menú de pausa", "Abre primero la escena Dia_3.", "Vale");
            return;
        }
        uiLayer = LayerMask.NameToLayer("UI");
        UpdatePausePanelInScene();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!",
            "Menú de pausa actualizado.\n\nGuarda la escena con Ctrl+S.\n\n" +
            "Después vuelve a lanzar 'Montar tutorial y menú principal' para que el tutorial también lo tenga.", "Vale");
    }

    private static void UpdatePausePanelInScene()
    {
        var pause = Object.FindAnyObjectByType<PauseMenu>();
        if (pause == null) return;
        var so = new SerializedObject(pause);
        var panel = so.FindProperty("pausePanel").objectReferenceValue as GameObject;
        if (panel == null) return;

        // Fuera lo de antes (título y botones), se queda el fondo oscuro del panel
        for (int i = panel.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);
        var dim = panel.GetComponent<Image>();
        if (dim != null) dim.color = new Color(0.12f, 0.04f, 0.07f, 0.65f);

        var center = new Vector2(0.5f, 0.5f);
        var libreta = MenuImage("Libreta", panel.transform, "menu_libreta_pausa", center, Vector2.zero, new Vector2(648f, 713f), 0f);
        libreta.raycastTarget = false;
        var resume = MenuTicket("BtnContinuar", panel.transform, "menu_comanda_continuar", new Vector2(11f, 70f), -2f);
        var restart = MenuTicket("BtnReiniciar", panel.transform, "menu_comanda_reiniciar", new Vector2(11f, -65f), 1.5f);
        var quit = MenuTicket("BtnSalir", panel.transform, "menu_comanda_salir", new Vector2(11f, -200f), -1f);

        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("restartButton").objectReferenceValue = restart;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ BUILD PROFILES

    // Orden: Menú (el que sale al abrir el juego) → Tutorial → Demo → el resto
    private static void AddMenuAndTutorialToBuild()
    {
        SetBuildScenes(); // Menú → Tutorial → Día 1 … Día 5
    }
}
