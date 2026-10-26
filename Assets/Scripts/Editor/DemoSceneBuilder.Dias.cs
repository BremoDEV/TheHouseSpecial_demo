using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tercera parte del montador: LOS 5 DÍAS y el TICKET de fin de día.
// Uso: menú  Tools → The House Special → Crear días 1-5 (ticket de fin de día)
// - Renombra Demo_Restaurante a Dia_3 (sin perder nada: Unity mueve el archivo con todas sus referencias).
// - Crea Dia_1, Dia_2, Dia_4 y Dia_5 copiando Dia_3 (solo los que no existan: si ya existen, NO se pisan).
//   Día 1 = 3 mesas, día 2 = 4 mesas, días 3-5 = 5 mesas (las mesas se recolocan centradas al crear la copia).
// - En los 5 días: pone el número de día y cuál es el siguiente, y rehace el ticket de fin de día.
// - Ordena las escenas en Build Profiles: Menú → Tutorial → Día 1 … Día 5.
// - El menú principal y el tutorial pasan a llevar al Día 1.
// Se puede lanzar las veces que haga falta: el dinero objetivo, la llegada de clientes, etc. de cada día se respetan.
public static partial class DemoSceneBuilder
{
    private const string OldDemoScenePath = "Assets/Escenas/Demo_Restaurante.unity";
    private const int DayCount = 5;

    private static string DayScenePath(int day) => $"{ScenesFolder}/Dia_{day}.unity";
    private static string DaySceneName(int day) => $"Dia_{day}";

    // Mesas por día y dónde van (días con menos mesas: repartidas por la sala)
    private static int TablesForDay(int day) => day == 1 ? 3 : day == 2 ? 4 : 5;

    private static readonly Dictionary<int, Vector3[]> TableLayouts = new Dictionary<int, Vector3[]>
    {
        { 3, new[] { new Vector3(-4.4f, 2.95f, 0f), new Vector3(1.8f, 2.95f, 0f), new Vector3(-1.3f, -3.5f, 0f) } },
        { 4, new[] { new Vector3(-4.4f, 2.95f, 0f), new Vector3(1.8f, 2.95f, 0f),
                     new Vector3(-4.4f, -3.5f, 0f), new Vector3(1.8f, -3.5f, 0f) } },
    };

    private static readonly Color TicketInk = new Color(0.16f, 0.12f, 0.12f);
    private static readonly Color StampRed = new Color(0.77f, 0.12f, 0.16f);

    [UnityEditor.MenuItem("Tools/The House Special/Crear días 1-5 (ticket de fin de día)")]
    public static void BuildDays()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        uiLayer = LayerMask.NameToLayer("UI");

        try
        {
            // 1. Demo_Restaurante → Dia_3
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // para poder mover la escena abierta
            if (Exists(OldDemoScenePath) && !Exists(DayScenePath(3)))
            {
                string error = AssetDatabase.MoveAsset(OldDemoScenePath, DayScenePath(3));
                if (!string.IsNullOrEmpty(error))
                {
                    EditorUtility.DisplayDialog("Crear días", "No he podido renombrar la demo a Dia_3:\n" + error, "Vale");
                    return;
                }
            }
            if (!Exists(DayScenePath(3)))
            {
                EditorUtility.DisplayDialog("Crear días", "No encuentro ni " + OldDemoScenePath + " ni " + DayScenePath(3) + ".", "Vale");
                return;
            }

            // 2. Primero el día 3 (es la base de las copias)
            EditorUtility.DisplayProgressBar("Crear días", "Día 3...", 0.1f);
            SetupDayScene(3, false);

            // 3. Resto de días
            var created = new List<int>();
            int step = 0;
            foreach (int day in new[] { 1, 2, 4, 5 })
            {
                step++;
                EditorUtility.DisplayProgressBar("Crear días", $"Día {day}...", 0.1f + step * 0.18f);
                bool isNew = !Exists(DayScenePath(day));
                if (isNew)
                {
                    AssetDatabase.CopyAsset(DayScenePath(3), DayScenePath(day));
                    created.Add(day);
                }
                SetupDayScene(day, isNew);
            }

            // 4. Build Profiles, menú y tutorial
            EditorUtility.DisplayProgressBar("Crear días", "Menú, tutorial y Build Profiles...", 0.9f);
            SetBuildScenes();
            PointSceneToDay1<MainMenu>(MenuScenePath);
            PointSceneToDay1<TutorialManager>(TutorialScenePath);
            AssetDatabase.SaveAssets();

            EditorSceneManager.OpenScene(DayScenePath(1));
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("¡Listo!",
                (created.Count > 0 ? "Días creados: " + string.Join(", ", created) + ".\n" : "No se ha pisado ningún día que ya existiera.\n") +
                "Ticket de fin de día actualizado en los 5 días.\n\n" +
                "Ahora pon en cada día su dinero objetivo (MoneyManager → Daily Goal).\n" +
                "La llegada de clientes, la hora punta, la paciencia por día y la ira por hora están en Assets/Datos/Dificultad " +
                "(un solo archivo para todos los días).", "Vale");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private const string DifficultyAssetPath = "Assets/Datos/Dificultad.asset";

    // Un único archivo de dificultad para los 5 días (si no existe, se crea con los valores por defecto)
    private static DifficultySettings GetOrCreateDifficulty()
    {
        var asset = AssetDatabase.LoadAssetAtPath<DifficultySettings>(DifficultyAssetPath);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<DifficultySettings>();
        AssetDatabase.CreateAsset(asset, DifficultyAssetPath);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static bool Exists(string assetPath) =>
        System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, assetPath));

    // ------------------------------------------------------------------ CADA DÍA

    private static void SetupDayScene(int day, bool arrangeTables)
    {
        var scene = EditorSceneManager.OpenScene(DayScenePath(day));

        var dm = Object.FindAnyObjectByType<DayManager>();
        if (dm == null)
        {
            Debug.LogError($"[Días] {DaySceneName(day)} no tiene DayManager.");
            return;
        }
        SetIntField(dm, "dayNumber", day);
        SetStringField(dm, "nextDayScene", day < DayCount ? DaySceneName(day + 1) : "");
        SetStringField(dm, "firstDayScene", DaySceneName(1));
        SetStringField(dm, "menuScene", "MenuPrincipal");
        var dso = new SerializedObject(dm);
        var dif = dso.FindProperty("difficulty");
        if (dif != null && dif.objectReferenceValue == null)
        {
            dif.objectReferenceValue = GetOrCreateDifficulty();
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        if (arrangeTables) ArrangeTables(TablesForDay(day));
        BuildTicketPanel(dm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Deja 'count' mesas (Table_1, Table_2… en orden) y las coloca repartidas; el resto se borran
    private static void ArrangeTables(int count)
    {
        if (!TableLayouts.TryGetValue(count, out Vector3[] positions)) return; // 5 mesas: tal cual

        List<Table> tables = Object.FindObjectsByType<Table>()
            .OrderBy(t => t.name.StartsWith("Table_") && int.TryParse(t.name.Substring(6), out int n) ? n : 99)
            .ThenBy(t => t.name)
            .ToList();

        for (int i = 0; i < tables.Count; i++)
        {
            if (i < count) tables[i].transform.position = positions[i]; // el punto de interacción y los asientos van con ella
            else Object.DestroyImmediate(tables[i].gameObject);
        }
    }

    // ------------------------------------------------------------------ TICKET

    private static void BuildTicketPanel(DayManager dm)
    {
        var so = new SerializedObject(dm);
        var oldPanel = so.FindProperty("endPanel").objectReferenceValue as GameObject;

        // El ticket va en el Canvas donde estaba el panel viejo (o en el primero que haya)
        Transform canvas = oldPanel != null ? oldPanel.transform.parent : null;
        if (canvas == null)
        {
            var c = Object.FindAnyObjectByType<Canvas>();
            if (c == null) { Debug.LogError("[Días] No hay Canvas en la escena."); return; }
            canvas = c.transform;
        }
        int sibling = oldPanel != null ? oldPanel.transform.GetSiblingIndex() : canvas.childCount;
        if (oldPanel != null) Object.DestroyImmediate(oldPanel);

        TMP_FontAsset bold = GetFontAsset(TpvFontBold, TpvFontBoldAsset, "Outfit-Bold");
        TMP_FontAsset regular = GetFontAsset(TpvFontRegular, TpvFontRegularAsset, "Outfit-Regular");
        var center = new Vector2(0.5f, 0.5f);

        // Fondo oscuro que tapa el juego (y bloquea los clics)
        var panel = UI("PanelFinDia", canvas);
        panel.transform.SetSiblingIndex(sibling);
        Stretch(panel, 0f);
        panel.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.06f, 0.8f);

        // El ticket (papel con bordes en zigzag). Si no está el dibujo, un rectángulo color papel.
        var ticket = UI("Ticket", panel.transform);
        Place(ticket, center, new Vector2(0f, 48f), new Vector2(600f, 809f));
        var ticketImg = ticket.AddComponent<Image>();
        ticketImg.raycastTarget = false;
        Sprite paper = AssetImporter.GetAtPath(MenuArtPath("ui_ticket")) != null ? LoadMenuSprite("ui_ticket", false) : null;
        if (paper != null) ticketImg.sprite = paper;
        else ticketImg.color = new Color(0.98f, 0.96f, 0.92f);
        Transform t = ticket.transform;

        // Medidas útiles dentro del papel: x de -255 a 255, y de -360 a 360
        TextMeshProUGUI AddText(string name, string content, float size, TMP_FontAsset font, Vector2 pos, Vector2 box,
                             TextAlignmentOptions align, Color color)
        {
            var txt = MakeText(name, t, content, size, color);
            if (font != null) txt.font = font;
            txt.alignment = align;
            Place(txt.gameObject, center, pos, box);
            return txt;
        }
        const string dashes = "- - - - - - - - - - - - - - - - - - - - - - - - - - - -";
        var faint = new Color(TicketInk.r, TicketInk.g, TicketInk.b, 0.35f);

        AddText("Restaurante", "THE HOUSE SPECIAL", 34f, bold, new Vector2(0f, 330f), new Vector2(520f, 44f), TextAlignmentOptions.Center, TicketInk);
        var dayText = AddText("Dia", "DÍA 1", 24f, regular, new Vector2(0f, 293f), new Vector2(520f, 32f), TextAlignmentOptions.Center, TicketInk);
        AddText("Linea1", dashes, 22f, regular, new Vector2(0f, 263f), new Vector2(520f, 26f), TextAlignmentOptions.Center, faint);
        var title = AddText("Titulo", "¡Felicidades! Has sobrevivido al día", 34f, bold, new Vector2(0f, 214f), new Vector2(510f, 72f),
            TextAlignmentOptions.Center, TicketInk);
        title.enableAutoSizing = true;
        title.fontSizeMin = 20f;
        title.fontSizeMax = 34f;
        AddText("Linea2", dashes, 22f, regular, new Vector2(0f, 164f), new Vector2(520f, 26f), TextAlignmentOptions.Center, faint);

        // Dos columnas con las mismas líneas: nombres a la izquierda, números a la derecha
        var labels = AddText("Etiquetas", "", 22f, regular, new Vector2(0f, -52f), new Vector2(510f, 420f), TextAlignmentOptions.TopLeft, TicketInk);
        var values = AddText("Valores", "", 22f, regular, new Vector2(0f, -52f), new Vector2(510f, 420f), TextAlignmentOptions.TopRight, TicketInk);
        values.fontStyle = FontStyles.Bold; // misma letra que la izquierda (en negrita) para que las líneas cuadren
        labels.lineSpacing = values.lineSpacing = 0f;

        AddText("Linea3", "= = = = = = = = = = = = = = = = = = = = = = =", 22f, regular, new Vector2(0f, -283f),
            new Vector2(520f, 26f), TextAlignmentOptions.Center, faint);
        AddText("TotalEtiqueta", "TOTAL", 32f, bold, new Vector2(0f, -318f), new Vector2(510f, 42f), TextAlignmentOptions.MidlineLeft, TicketInk);
        var total = AddText("TotalValor", "0 €", 32f, bold, new Vector2(0f, -318f), new Vector2(510f, 42f), TextAlignmentOptions.MidlineRight, TicketInk);
        var thanks = AddText("Gracias", "¡Gracias por su visita!", 20f, regular, new Vector2(0f, -360f), new Vector2(510f, 28f),
            TextAlignmentOptions.Center, new Color(TicketInk.r, TicketInk.g, TicketInk.b, 0.7f));
        thanks.fontStyle = FontStyles.Italic;

        // Sello rojo de DESPEDIDA (encima de los datos, torcido; solo sale si fallas)
        var stamp = UI("SelloDespedida", t);
        Place(stamp, center, new Vector2(10f, 20f), new Vector2(440f, 144f));
        stamp.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        var stampImg = stamp.AddComponent<Image>();
        stampImg.raycastTarget = false;
        Sprite stampSprite = AssetImporter.GetAtPath(MenuArtPath("ui_sello")) != null ? LoadMenuSprite("ui_sello", false) : null;
        if (stampSprite != null) { stampImg.sprite = stampSprite; stampImg.color = new Color(1f, 1f, 1f, 0.9f); }
        else stampImg.color = new Color(0f, 0f, 0f, 0f);
        var stampText = MakeText("Texto", stamp.transform, "DESPEDIDA", 66f, new Color(StampRed.r, StampRed.g, StampRed.b, 0.9f));
        if (bold != null) stampText.font = bold;
        stampText.characterSpacing = 6f;
        Stretch(stampText.gameObject, 10f);
        stamp.SetActive(false);

        // Botones debajo del ticket: el principal (Siguiente día / Volver a empezar) y Menú (solo si fallas)
        var main = MakeButton("BtnPrincipal", panel.transform, "Siguiente día", center, new Vector2(-200f, -440f), new Vector2(360f, 84f), TpvGreen, 36f);
        var menu = MakeButton("BtnMenu", panel.transform, "Menú", center, new Vector2(200f, -440f), new Vector2(360f, 84f), TpvGrey, 36f);
        Sprite uiButton = LoadUiSprite("ui_boton", 32f);
        foreach (Button b in new[] { main, menu })
        {
            var img = b.GetComponent<Image>();
            if (uiButton != null) { img.sprite = uiButton; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.6f; }
            var label = b.GetComponentInChildren<TMP_Text>();
            if (bold != null) label.font = bold;
            label.rectTransform.offsetMin = new Vector2(4f, 8f); // el canto del botón ocupa abajo
        }

        so.FindProperty("endPanel").objectReferenceValue = panel;
        so.FindProperty("endTitleText").objectReferenceValue = title;
        so.FindProperty("endDetailText").objectReferenceValue = labels;
        so.FindProperty("endValuesText").objectReferenceValue = values;
        so.FindProperty("endDayText").objectReferenceValue = dayText;
        so.FindProperty("endTotalText").objectReferenceValue = total;
        so.FindProperty("failStamp").objectReferenceValue = stamp;
        so.FindProperty("endFooterText").objectReferenceValue = thanks;
        so.FindProperty("retryButton").objectReferenceValue = main;
        so.FindProperty("retryButtonLabel").objectReferenceValue = main.GetComponentInChildren<TMP_Text>();
        so.FindProperty("menuButton").objectReferenceValue = menu;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
    }

    // ------------------------------------------------------------------ BUILD PROFILES, MENÚ Y TUTORIAL

    // Orden: Menú → Tutorial → Día 1 … Día 5 (y detrás cualquier otra escena que hubiera)
    private static void SetBuildScenes()
    {
        var ordered = new List<string> { MenuScenePath, TutorialScenePath };
        for (int d = 1; d <= DayCount; d++) ordered.Add(DayScenePath(d));
        ordered.Add(ScenesFolder + "/Tienda.unity"); // la tienda entre días

        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string p in ordered)
            if (Exists(p)) scenes.Add(new EditorBuildSettingsScene(p, true));
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (!ordered.Contains(s.path) && s.path != OldDemoScenePath && Exists(s.path)) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // El menú (botón Jugar) y el tutorial (al acabar) llevan al Día 1
    private static void PointSceneToDay1<T>(string scenePath) where T : Object
    {
        if (!Exists(scenePath)) return;
        var scene = EditorSceneManager.OpenScene(scenePath);
        foreach (T o in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
            SetStringField(o, "gameSceneName", DaySceneName(1));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetIntField(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[Días] {target.GetType().Name} no tiene '{field}'"); return; }
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStringField(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[Días] {target.GetType().Name} no tiene '{field}'"); return; }
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
