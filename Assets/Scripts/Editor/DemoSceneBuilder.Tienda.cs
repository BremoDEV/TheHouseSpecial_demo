using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Cuarta parte del montador: la TIENDA DE MEJORAS.
// Uso: menú  Tools → The House Special → Montar tienda de mejoras
// - Crea (o completa) el catálogo Assets/Resources/Mejoras.asset con las 6 mejoras del documento de diseño
//   (precios provisionales). Si ya existe, NO se pisan tus precios ni tus textos: solo se rellenan dibujos que falten.
// - Rehace la escena Assets/Escenas/Tienda (el tríptico con las 6 mejoras).
// - En los 5 días añade (si no están): la tele en la pared y el botón de la TPV portátil en el HUD.
// - Añade la tienda a Build Profiles.
public static partial class DemoSceneBuilder
{
    private const string ShopScenePath = ScenesFolder + "/Tienda.unity";
    // Dibujos PROVISIONALES de las mejoras (sacados de Pinterest y uno hecho por Claude) hasta que esté el arte del equipo
    private const string UpgradesArtFolder = "Assets/Arte/Provisional/Mejoras";
    private const string OldUpgradesArtFolder = "Assets/Arte/Mejoras";
    private const string UpgradeCatalogPath = "Assets/Resources/Mejoras.asset";

    private static readonly Color ShopCream = new Color(0.953f, 0.933f, 0.902f);
    private static readonly Color ShopTeal = new Color(0.247f, 0.373f, 0.431f);
    private static readonly Color ShopInk = new Color(0.16f, 0.14f, 0.14f);
    private static readonly Color ShopOrange = new Color(0.87f, 0.42f, 0.29f);

    [UnityEditor.MenuItem("Tools/The House Special/Montar tienda de mejoras")]
    public static void BuildShop()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        uiLayer = LayerMask.NameToLayer("UI");

        try
        {
            EditorUtility.DisplayProgressBar("Tienda", "Catálogo de mejoras...", 0.1f);
            UpgradeCatalog catalog = GetOrCreateUpgradeCatalog();

            EditorUtility.DisplayProgressBar("Tienda", "Escena de la tienda...", 0.3f);
            BuildShopScene();

            for (int d = 1; d <= DayCount; d++)
            {
                EditorUtility.DisplayProgressBar("Tienda", $"Día {d}: tele y TPV portátil...", 0.4f + d * 0.1f);
                AddUpgradeThingsToDay(d, catalog);
            }

            SetBuildScenes();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ShopScenePath);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("¡Listo!",
                "Tienda montada (Assets/Escenas/Tienda).\n\n" +
                "Precios, textos y valores de las mejoras: Assets/Resources/Mejoras.\n" +
                "En los 5 días: tele en la pared y botón de TPV portátil (solo se ven si los compras).", "Vale");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ------------------------------------------------------------------ CATÁLOGO

    private static UpgradeCatalog GetOrCreateUpgradeCatalog()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(UpgradeCatalogPath);
        bool isNew = catalog == null;
        if (isNew)
        {
            catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
            catalog.upgrades = new[]
            {
                Def(UpgradeId.Bandeja, "Bandeja ampliada", "Lleva más platos en un mismo viaje.",
                    new[] { 50, 100 }, new[] { "3 platos", "4 platos" }),
                Def(UpgradeId.Television, "Televisión", "Los clientes sentados pierden la paciencia más despacio.",
                    new[] { 40, 80 }, new[] { "-10 % de pérdida", "-20 % de pérdida" }),
                Def(UpgradeId.TpvPortatil, "TPV portátil", "Haz comandas y saca cuentas desde cualquier sitio.",
                    new[] { 120 }, new[] { "Acceso remoto a la TPV" }),
                Def(UpgradeId.Mostrador, "Mostrador ampliado", "Más huecos en la barra y más pedidos a la vez.",
                    new[] { 50, 100 }, new[] { "4 huecos (6 pedidos)", "6 huecos (8 pedidos)" }),
                Def(UpgradeId.Cocina, "Equipamiento de cocina", "El cocinero prepara cada plato más rápido.",
                    new[] { 80 }, new[] { "-20 % de tiempo por plato" }),
                Def(UpgradeId.Zapatos, "Zapatos cómodos", "La camarera se mueve más rápido.",
                    new[] { 60 }, new[] { "+15 % de velocidad" }),
            };
            AssetDatabase.CreateAsset(catalog, UpgradeCatalogPath);
        }

        // Dibujos provisionales (solo si faltan)
        var art = new Dictionary<UpgradeId, string[]>
        {
            { UpgradeId.Bandeja, new[] { "mejora_bandeja" } },
            { UpgradeId.Television, new[] { "mejora_tele_1", "mejora_tele_2" } },
            { UpgradeId.TpvPortatil, new[] { "mejora_tpv_portatil" } },
            { UpgradeId.Mostrador, new[] { "mejora_mostrador" } },
            { UpgradeId.Cocina, new[] { "mejora_cocina" } },
            { UpgradeId.Zapatos, new[] { "mejora_zapatos" } },
        };
        foreach (UpgradeDef def in catalog.upgrades)
        {
            // Textos antiguos del mostrador (antes de que limitara los pedidos): se actualizan
            if (def != null && def.id == UpgradeId.Mostrador && def.levelEffects != null && def.levelEffects.Length == 2
                && def.levelEffects[0] == "4 huecos")
            {
                def.levelEffects = new[] { "4 huecos (6 pedidos)", "6 huecos (8 pedidos)" };
                def.description = "Más huecos en la barra y más pedidos a la vez.";
            }
            if (def == null || !art.TryGetValue(def.id, out string[] files)) continue;
            bool missing = def.icons == null || def.icons.Length == 0;
            if (!missing) foreach (Sprite s in def.icons) if (s == null) missing = true;
            if (!missing) continue;
            var sprites = new List<Sprite>();
            foreach (string f in files)
            {
                Sprite s = LoadUpgradeSprite(f);
                if (s != null) sprites.Add(s);
            }
            def.icons = sprites.ToArray();
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    // Mueve el arte provisional a Arte/Provisional/Mejoras y el de la interfaz de la tienda a Arte/UI/Tienda.
    // Se mueve con Unity: las escenas y el catálogo no pierden los dibujos.
    [UnityEditor.MenuItem("Tools/The House Special/Ordenar: arte provisional y de la tienda")]
    public static void OrganizeShopArt()
    {
        EnsureFolder("Assets/Arte", "Provisional");
        EnsureFolder("Assets/Arte/Provisional", "Mejoras");
        EnsureFolder("Assets/Arte/UI", "Tienda");

        int moved = 0;
        if (AssetDatabase.IsValidFolder(OldUpgradesArtFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { OldUpgradesArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                string target = UpgradesArtFolder + "/" + System.IO.Path.GetFileName(path);
                if (string.IsNullOrEmpty(AssetDatabase.MoveAsset(path, target))) moved++;
            }
            if (AssetDatabase.FindAssets("", new[] { OldUpgradesArtFolder }).Length == 0)
                AssetDatabase.DeleteAsset(OldUpgradesArtFolder); // la carpeta vieja ya está vacía
        }
        foreach (string file in new[] { "ui_circulo" })
        {
            string from = UiArtFolder + "/" + file + ".png";
            if (AssetImporter.GetAtPath(from) != null &&
                string.IsNullOrEmpty(AssetDatabase.MoveAsset(from, UiArtFolder + "/Tienda/" + file + ".png"))) moved++;
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Ordenar arte", $"Archivos movidos: {moved}.\n\n" +
            "Arte provisional de las mejoras: Assets/Arte/Provisional/Mejoras\n" +
            "Interfaz de la tienda: Assets/Arte/UI/Tienda", "Vale");
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    private static UpgradeDef Def(UpgradeId id, string name, string desc, int[] prices, string[] effects) =>
        new UpgradeDef { id = id, displayName = name, description = desc, prices = prices, levelEffects = effects };

    // Dibujo pixel art de Assets/Arte/Mejoras (sin suavizar)
    private static Sprite LoadUpgradeSprite(string fileName)
    {
        string path = UpgradesArtFolder + "/" + fileName + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("[Tienda] No encuentro " + path); return null; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ------------------------------------------------------------------ ESCENA DE LA TIENDA

    private static void BuildShopScene()
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
        Transform c = canvasGo.transform;
        var center = new Vector2(0.5f, 0.5f);

        TMP_FontAsset bold = GetFontAsset(TpvFontBold, TpvFontBoldAsset, "Outfit-Bold");
        TMP_FontAsset regular = GetFontAsset(TpvFontRegular, TpvFontRegularAsset, "Outfit-Regular");
        Sprite circle = LoadMenuSprite("ui_circulo", false);
        Sprite uiButton = LoadUiSprite("ui_boton", 32f);

        // Fondo: el mismo papel pintado del menú, oscurecido
        var bg = UI("Fondo", c);
        Stretch(bg, 0f);
        var bgImg = bg.AddComponent<Image>();
        bgImg.raycastTarget = false;
        if (AssetImporter.GetAtPath(MenuArtPath("menu_fondo")) != null) bgImg.sprite = LoadMenuSprite("menu_fondo", false);
        else bgImg.color = new Color(0.55f, 0.25f, 0.3f);
        var dim = UI("Oscurecer", c);
        Stretch(dim, 0f);
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.08f, 0.05f, 0.06f, 0.55f);
        dimImg.raycastTarget = false;

        TextMeshProUGUI Txt(string name, Transform parent, string content, float size, TMP_FontAsset font, Color color,
                            Vector2 pos, Vector2 box, TextAlignmentOptions align)
        {
            var t = MakeText(name, parent, content, size, color);
            if (font != null) t.font = font;
            t.alignment = align;
            Place(t.gameObject, center, pos, box);
            return t;
        }

        // Cabecera
        Txt("Titulo", c, "TIENDA DE MEJORAS", 64f, bold, ShopCream, new Vector2(0f, 470f), new Vector2(1200f, 80f), TextAlignmentOptions.Center);
        var dayText = Txt("Dia", c, "Antes del día 2", 30f, regular, ShopCream, new Vector2(-640f, 470f), new Vector2(500f, 50f), TextAlignmentOptions.MidlineLeft);
        var savings = Txt("Ahorros", c, "Ahorros: 0 €", 38f, bold, new Color(1f, 0.85f, 0.45f), new Vector2(640f, 470f), new Vector2(500f, 60f), TextAlignmentOptions.MidlineRight);

        // El tríptico: tres caras (crema, azul petróleo, crema) con dos mejoras en cada una
        UpgradeId[][] faces =
        {
            new[] { UpgradeId.Bandeja, UpgradeId.Television },
            new[] { UpgradeId.TpvPortatil, UpgradeId.Mostrador },
            new[] { UpgradeId.Cocina, UpgradeId.Zapatos },
        };
        var cards = new List<ShopCard>();
        for (int f = 0; f < 3; f++)
        {
            bool dark = f == 1;
            Color paper = dark ? ShopTeal : ShopCream;
            Color ink = dark ? Color.white : ShopInk;
            Color soft = dark ? new Color(1f, 1f, 1f, 0.75f) : new Color(ShopInk.r, ShopInk.g, ShopInk.b, 0.75f);

            var face = UI("Cara_" + (f + 1), c);
            Place(face, center, new Vector2((f - 1) * 540f, -20f), new Vector2(540f, 800f));
            var faceImg = face.AddComponent<Image>();
            faceImg.color = paper;
            faceImg.raycastTarget = false;
            var shadow = face.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(6f, -8f);

            for (int k = 0; k < 2; k++)
            {
                UpgradeId id = faces[f][k];
                var cardGo = UI("Mejora_" + id, face.transform);
                Place(cardGo, center, new Vector2(0f, k == 0 ? 195f : -195f), new Vector2(500f, 380f));
                Transform t = cardGo.transform;
                var card = cardGo.AddComponent<ShopCard>();
                card.upgrade = id;

                // Círculo con el dibujo (como las fotos redondas del folleto)
                var disc = UI("Circulo", t);
                Place(disc, center, new Vector2(-150f, 60f), new Vector2(170f, 170f));
                var discImg = disc.AddComponent<Image>();
                discImg.sprite = circle;
                discImg.color = dark ? new Color(0.36f, 0.5f, 0.56f) : new Color(0.91f, 0.87f, 0.81f);
                discImg.raycastTarget = false;
                var iconGo = UI("Dibujo", disc.transform);
                Place(iconGo, center, Vector2.zero, new Vector2(120f, 120f));
                var icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var name = Txt("Nombre", t, "Mejora", 32f, bold, ink, new Vector2(85f, 105f), new Vector2(300f, 80f), TextAlignmentOptions.BottomLeft);
                name.enableAutoSizing = true; name.fontSizeMin = 22f; name.fontSizeMax = 32f;
                var desc = Txt("Descripcion", t, "", 21f, regular, soft, new Vector2(85f, 10f), new Vector2(300f, 100f), TextAlignmentOptions.TopLeft);
                var level = Txt("Nivel", t, "", 21f, bold, dark ? new Color(1f, 0.82f, 0.55f) : ShopOrange,
                    new Vector2(0f, -78f), new Vector2(460f, 32f), TextAlignmentOptions.Center);

                var buy = MakeButton("BtnComprar", t, "Comprar", center, new Vector2(0f, -135f), new Vector2(320f, 66f), TpvGreen, 28f);
                var buyImg = buy.GetComponent<Image>();
                if (uiButton != null) { buyImg.sprite = uiButton; buyImg.type = Image.Type.Sliced; buyImg.pixelsPerUnitMultiplier = 1.8f; }
                var buyLabel = buy.GetComponentInChildren<TMP_Text>();
                if (bold != null) buyLabel.font = bold;
                buyLabel.rectTransform.offsetMin = new Vector2(4f, 8f);
                var colors = buy.colors;
                colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
                buy.colors = colors;

                var so = new SerializedObject(card);
                so.FindProperty("icon").objectReferenceValue = icon;
                so.FindProperty("nameText").objectReferenceValue = name;
                so.FindProperty("descriptionText").objectReferenceValue = desc;
                so.FindProperty("levelText").objectReferenceValue = level;
                so.FindProperty("buyButton").objectReferenceValue = buy;
                so.FindProperty("buyLabel").objectReferenceValue = buyLabel;
                so.ApplyModifiedPropertiesWithoutUndo();
                cards.Add(card);
            }

            // Línea de puntos entre las dos mejoras de cada cara
            Txt("Separador", face.transform, "· · · · · · · · · · · · · · · · · · · ·", 22f, regular,
                new Color(ink.r, ink.g, ink.b, 0.3f), Vector2.zero, new Vector2(480f, 30f), TextAlignmentOptions.Center);
        }

        // Botón para seguir
        var next = MakeButton("BtnEmpezar", c, "Empezar el día 2", center, new Vector2(0f, -475f), new Vector2(460f, 84f), ShopOrange, 34f);
        var nextImg = next.GetComponent<Image>();
        if (uiButton != null) { nextImg.sprite = uiButton; nextImg.type = Image.Type.Sliced; nextImg.pixelsPerUnitMultiplier = 1.6f; }
        var nextLabel = next.GetComponentInChildren<TMP_Text>();
        if (bold != null) nextLabel.font = bold;
        nextLabel.rectTransform.offsetMin = new Vector2(4f, 8f);

        var shop = canvasGo.AddComponent<ShopMenu>();
        var sso = new SerializedObject(shop);
        var arr = sso.FindProperty("cards");
        arr.arraySize = cards.Count;
        for (int i = 0; i < cards.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        sso.FindProperty("savingsText").objectReferenceValue = savings;
        sso.FindProperty("dayText").objectReferenceValue = dayText;
        sso.FindProperty("continueButton").objectReferenceValue = next;
        sso.FindProperty("continueLabel").objectReferenceValue = nextLabel;
        sso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ShopScenePath);
    }

    // ------------------------------------------------------------------ EN CADA DÍA

    // La tele: en la pared de arriba, en la zona libre entre el dinero y la puerta del trastero
    private static readonly Vector3 TvPosition = new Vector3(2.4f, 4.85f, 0f);

    private static void PlacePortablePosButton(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(30f, 30f);
        rt.sizeDelta = new Vector2(150f, 150f);
    }

    private static void AddUpgradeThingsToDay(int day, UpgradeCatalog catalog)
    {
        if (!Exists(DayScenePath(day))) return;
        var scene = EditorSceneManager.OpenScene(DayScenePath(day));

        // La tele en la pared de arriba (oculta hasta que se compra)
        var existingTv = Object.FindAnyObjectByType<UpgradeVisual>(FindObjectsInactive.Include);
        if (existingTv != null) existingTv.transform.position = TvPosition; // por si se ha cambiado el sitio
        else
        {
            var tv = new GameObject("Tele");
            tv.transform.position = TvPosition;
            var sr = tv.AddComponent<SpriteRenderer>();
            UpgradeDef def = catalog.Get(UpgradeId.Television);
            sr.sprite = def != null ? def.IconFor(1) : null;
            sr.sortingOrder = -30; // por encima de la pared, por detrás de todo lo demás
            tv.AddComponent<UpgradeVisual>();
        }

        // Botón de la TPV portátil abajo a la izquierda del HUD (sobre la calle, que está vacía)
        var existingBtn = Object.FindAnyObjectByType<PortablePosButton>(FindObjectsInactive.Include);
        if (existingBtn != null) PlacePortablePosButton((RectTransform)existingBtn.transform);
        else
        {
            var dm = Object.FindAnyObjectByType<DayManager>();
            var clock = dm != null ? new SerializedObject(dm).FindProperty("clockText").objectReferenceValue as Component : null;
            Canvas hud = clock != null ? clock.GetComponentInParent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
            if (hud != null)
            {
                var go = UI("BtnTpvPortatil", hud.rootCanvas.transform);
                PlacePortablePosButton((RectTransform)go.transform);
                var img = go.AddComponent<Image>();
                Sprite uiButton = LoadUiSprite("ui_boton", 32f);
                if (uiButton != null) { img.sprite = uiButton; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.6f; }
                img.color = TpvBlue;
                var button = go.AddComponent<Button>();
                button.targetGraphic = img;

                var iconGo = UI("Dibujo", go.transform);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(0f, 14f);
                irt.sizeDelta = new Vector2(90f, 90f);
                var icon = iconGo.AddComponent<Image>();
                UpgradeDef def = catalog.Get(UpgradeId.TpvPortatil);
                icon.sprite = def != null ? def.IconFor(1) : null;
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var label = MakeText("Texto", go.transform, "TPV", 26f, Color.white);
                TMP_FontAsset bold = GetFontAsset(TpvFontBold, TpvFontBoldAsset, "Outfit-Bold");
                if (bold != null) label.font = bold;
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.anchoredPosition = new Vector2(0f, 12f);
                lrt.sizeDelta = new Vector2(0f, 34f);

                var ppb = go.AddComponent<PortablePosButton>();
                var pso = new SerializedObject(ppb);
                pso.FindProperty("button").objectReferenceValue = button;
                pso.FindProperty("panel").objectReferenceValue = Object.FindAnyObjectByType<TpvPanel>(FindObjectsInactive.Include);
                pso.FindProperty("waitress").objectReferenceValue = Object.FindAnyObjectByType<WaitressController>();
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
