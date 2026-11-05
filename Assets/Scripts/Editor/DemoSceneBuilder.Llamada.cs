using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Sexta parte del montador: la LLAMADA DEL PSIQUIATRA (después de la tienda, cada noche).
// Uso: menú  Tools → The House Special → Montar llamada del psiquiatra
// - Rehace la escena Assets/Escenas/Llamada: a la izquierda el trastero (de momento un círculo que "respira",
//   hasta tener la animación de la camarera) y a la derecha el teléfono con la pregunta y el minijuego.
// - La añade a Build Profiles (la tienda lleva a la llamada y la llamada al día siguiente).
// Las preguntas, frases y dificultad se editan en el componente PhoneCall del Canvas.
public static partial class DemoSceneBuilder
{
    private const string CallScenePath = ScenesFolder + "/Llamada.unity";

    [UnityEditor.MenuItem("Tools/The House Special/Montar llamada del psiquiatra")]
    public static void BuildPhoneCall()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        uiLayer = LayerMask.NameToLayer("UI");
        BuildPhoneCallScene();
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(CallScenePath);
        EditorUtility.DisplayDialog("¡Listo!",
            "Llamada del psiquiatra montada (Assets/Escenas/Llamada).\n\n" +
            "Va después de la tienda y antes del día siguiente.\n" +
            "Preguntas, frases y dificultad: componente PhoneCall del Canvas.", "Vale");
    }

    private static void BuildPhoneCallScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.05f, 0.07f);
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
        Transform c = canvasGo.transform;
        var center = new Vector2(0.5f, 0.5f);

        TMP_FontAsset bold = GetFontAsset(TpvFontBold, TpvFontBoldAsset, "Outfit-Bold");
        TMP_FontAsset regular = GetFontAsset(TpvFontRegular, TpvFontRegularAsset, "Outfit-Regular");
        Sprite circle = AssetImporter.GetAtPath(MenuArtPath("ui_circulo")) != null ? LoadMenuSprite("ui_circulo", false) : null;
        Sprite uiButton = LoadUiSprite("ui_boton", 32f);
        Sprite sq = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesFolder + "/Cuadrado.png");

        TextMeshProUGUI Txt(string name, Transform parent, string content, float size, TMP_FontAsset font, Color color,
                            Vector2 pos, Vector2 box, TextAlignmentOptions align)
        {
            var t = MakeText(name, parent, content, size, color);
            if (font != null) t.font = font;
            t.alignment = align;
            Place(t.gameObject, center, pos, box);
            return t;
        }
        Image Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = UI(name, parent);
            Place(go, center, pos, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
        Button Btn(string name, Transform parent, string label, Vector2 pos, Vector2 size, Color color, float fontSize)
        {
            var b = MakeButton(name, parent, label, center, pos, size, color, fontSize);
            var img = b.GetComponent<Image>();
            if (uiButton != null) { img.sprite = uiButton; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.6f; }
            var l = b.GetComponentInChildren<TMP_Text>();
            if (bold != null) l.font = bold;
            l.rectTransform.offsetMin = new Vector2(12f, 8f);
            l.rectTransform.offsetMax = new Vector2(-12f, -4f);
            return b;
        }

        var bg = UI("Fondo", c);
        Stretch(bg, 0f);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.07f, 0.05f, 0.07f);
        bgImg.raycastTarget = false;

        // ---- Izquierda: el trastero (provisional hasta tener la animación de la camarera)
        var room = Box("Trastero", c, new Vector2(-470f, 0f), new Vector2(820f, 900f), new Color(0.16f, 0.09f, 0.11f));
        Txt("TituloTrastero", room.transform, "En el trastero...", 30f, regular, new Color(0.85f, 0.75f, 0.75f),
            new Vector2(0f, 400f), new Vector2(760f, 50f), TextAlignmentOptions.Center);
        var breath = Box("Respiracion", room.transform, new Vector2(0f, 30f), new Vector2(380f, 380f), new Color(0.55f, 0.22f, 0.28f));
        if (circle != null) breath.sprite = circle;
        Txt("Provisional", room.transform, "(Aquí irá la animación de la camarera agarrándose el pecho y respirando)", 24f, regular,
            new Color(0.85f, 0.75f, 0.75f, 0.7f), new Vector2(0f, -300f), new Vector2(700f, 90f), TextAlignmentOptions.Center);

        // ---- Derecha: el teléfono
        var phone = Box("Telefono", c, new Vector2(440f, 0f), new Vector2(880f, 900f), ShopCream);
        Transform p = phone.transform;
        var header = Box("Cabecera", p, new Vector2(0f, 395f), new Vector2(880f, 110f), ShopTeal);
        Txt("Titulo", header.transform, "Llamada del psiquiatra", 42f, bold, Color.white, Vector2.zero, new Vector2(820f, 70f), TextAlignmentOptions.Center);
        var dayText = Txt("Dia", p, "Noche antes del día 2", 26f, regular, ShopInk, new Vector2(0f, 310f), new Vector2(800f, 40f), TextAlignmentOptions.Center);
        var doctor = Txt("Psiquiatra", p, "Buenas noches.", 30f, regular, ShopTeal, new Vector2(0f, 235f), new Vector2(800f, 100f), TextAlignmentOptions.Center);
        doctor.fontStyle = FontStyles.Italic;

        // Pregunta
        var qPanel = UI("Pregunta", p);
        Stretch(qPanel, 0f);
        var qText = Txt("TextoPregunta", qPanel.transform, "¿Cómo te has sentido hoy?", 38f, bold, ShopInk, new Vector2(0f, 120f), new Vector2(800f, 110f), TextAlignmentOptions.Center);
        var buttons = new Button[4];
        var labels = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            buttons[i] = Btn("Respuesta_" + (i + 1), qPanel.transform, "Respuesta", new Vector2(0f, 10f - i * 105f), new Vector2(760f, 90f), ShopTeal, 28f);
            labels[i] = buttons[i].GetComponentInChildren<TMP_Text>();
        }

        // Minijuego
        var mPanel = UI("Minijuego", p);
        Stretch(mPanel, 0f);
        Transform m = mPanel.transform;
        var track = Box("Barra", m, new Vector2(-250f, -100f), new Vector2(130f, 560f), new Color(0.2f, 0.17f, 0.2f));
        var zoneGo = UI("ZonaVerde", track.transform);
        var zrt = zoneGo.GetComponent<RectTransform>();
        zrt.anchorMin = new Vector2(0f, 0f); zrt.anchorMax = new Vector2(1f, 0f); zrt.pivot = new Vector2(0.5f, 0f);
        zrt.offsetMin = new Vector2(8f, 0f); zrt.offsetMax = new Vector2(-8f, 0f);
        zrt.sizeDelta = new Vector2(-16f, 160f);
        var zImg = zoneGo.AddComponent<Image>();
        zImg.color = new Color(0.3f, 0.6f, 0.36f);
        zImg.raycastTarget = false;
        var pulseGo = UI("Pulso", track.transform);
        var prt = pulseGo.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f); prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(64f, 64f);
        var pImg = pulseGo.AddComponent<Image>();
        pImg.color = new Color(0.88f, 0.2f, 0.25f);
        pImg.raycastTarget = false;
        if (circle != null) pImg.sprite = circle;

        var calmBg = Box("CalmaFondo", m, new Vector2(-90f, -100f), new Vector2(56f, 560f), new Color(0.2f, 0.17f, 0.2f));
        var fillGo = UI("CalmaRelleno", calmBg.transform);
        Stretch(fillGo, 6f);
        var fill = fillGo.AddComponent<Image>();
        fill.sprite = sq;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0.3f;
        fill.color = new Color(0.98f, 0.78f, 0.35f);
        fill.raycastTarget = false;
        var calmText = Txt("TextoCalma", m, "Calma 30 %", 26f, bold, ShopInk, new Vector2(-170f, 205f), new Vector2(300f, 40f), TextAlignmentOptions.Center);
        Txt("Instrucciones", m,
            "Mantén pulsado el clic (o la barra espaciadora) para subir la zona verde. Suelta para que baje.\n\n" +
            "Mantén el pulso rojo dentro de la zona: así la camarera conserva la calma.\n\n" +
            "Si la calma llega a cero, el psiquiatra se dará cuenta.",
            25f, regular, ShopInk, new Vector2(190f, -100f), new Vector2(400f, 520f), TextAlignmentOptions.MidlineLeft);

        // Resultado
        var rPanel = UI("Resultado", c);
        Stretch(rPanel, 0f);
        var dim = rPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        var rBox = Box("Caja", rPanel.transform, Vector2.zero, new Vector2(900f, 540f), ShopCream);
        var rTitle = Txt("Titulo", rBox.transform, "Todo en orden", 58f, bold, ShopInk, new Vector2(0f, 170f), new Vector2(820f, 80f), TextAlignmentOptions.Center);
        var rText = Txt("Texto", rBox.transform, "", 30f, regular, ShopInk, new Vector2(0f, 20f), new Vector2(800f, 200f), TextAlignmentOptions.Center);
        var rBtn = Btn("Boton", rBox.transform, "Empezar el día 2", new Vector2(0f, -180f), new Vector2(480f, 90f), ShopOrange, 32f);
        var rLabel = rBtn.GetComponentInChildren<TMP_Text>();
        rPanel.SetActive(false);
        mPanel.SetActive(false);

        var call = canvasGo.AddComponent<PhoneCall>();
        var so = new SerializedObject(call);
        so.FindProperty("dayText").objectReferenceValue = dayText;
        so.FindProperty("doctorText").objectReferenceValue = doctor;
        so.FindProperty("questionPanel").objectReferenceValue = qPanel;
        so.FindProperty("questionText").objectReferenceValue = qText;
        var bArr = so.FindProperty("answerButtons");
        var lArr = so.FindProperty("answerLabels");
        bArr.arraySize = 4; lArr.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            bArr.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            lArr.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
        }
        so.FindProperty("minigamePanel").objectReferenceValue = mPanel;
        so.FindProperty("track").objectReferenceValue = track.rectTransform;
        so.FindProperty("zone").objectReferenceValue = zrt;
        so.FindProperty("pulse").objectReferenceValue = prt;
        so.FindProperty("calmFillImage").objectReferenceValue = fill;
        so.FindProperty("calmText").objectReferenceValue = calmText;
        so.FindProperty("breathing").objectReferenceValue = breath.rectTransform;
        so.FindProperty("resultPanel").objectReferenceValue = rPanel;
        so.FindProperty("resultTitle").objectReferenceValue = rTitle;
        so.FindProperty("resultText").objectReferenceValue = rText;
        so.FindProperty("resultButton").objectReferenceValue = rBtn;
        so.FindProperty("resultButtonLabel").objectReferenceValue = rLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, CallScenePath);
    }
}
