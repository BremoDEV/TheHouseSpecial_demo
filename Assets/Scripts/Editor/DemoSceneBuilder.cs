using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Monta automáticamente una escena jugable de la demo con formas de colores.
// Uso: menú  Tools → The House Special → Montar escena demo
// Crea (o reemplaza) la carpeta Assets/Demo con: sprites básicos, los 9 productos,
// los prefabs (cliente, producto en barra, botón del TPV) y la escena Demo_Restaurante.
// No toca tus otras escenas ni carpetas.
public static class DemoSceneBuilder
{
    private const string Root = "Assets/Demo";
    private const string SpritesFolder = Root + "/Sprites";
    private const string DataFolder = Root + "/Data";
    private const string PrefabsFolder = Root + "/Prefabs";
    private const string ScenePath = Root + "/Demo_Restaurante.unity";

    private static Sprite square, circle, capsule;
    private static Kitchen builtKitchen;
    private static readonly string[] MenuNames =
        { "Agua", "Refresco", "Cerveza", "Vino", "Pollo asado", "Hamburguesa", "Ensalada", "Pasta" };
    private static Material spriteMaterial;
    private static int uiLayer;

    // ------------------------------------------------------------------ MENÚ

    [UnityEditor.MenuItem("Tools/The House Special/Montar escena demo")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Montar escena demo",
                "Se va a crear (o REEMPLAZAR) la carpeta Assets/Demo con una escena jugable de la demo.\n\n" +
                "Tus otras escenas y carpetas no se tocan. ¿Seguimos?", "Montar", "Cancelar"))
            return;

        if (Resources.Load<TMP_Settings>("TMP Settings") == null)
        {
            EditorUtility.DisplayDialog("Falta TextMeshPro",
                "Antes hay que importar los recursos de TextMeshPro:\n\n" +
                "Window → TextMeshPro → Import TMP Essential Resources\n\n" +
                "Cuando termine, vuelve a lanzar Tools → The House Special → Montar escena demo.", "Vale");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            EditorUtility.DisplayProgressBar("Montar escena demo", "Preparando carpetas...", 0.05f);
            PrepareFolders();

            EditorUtility.DisplayProgressBar("Montar escena demo", "Creando sprites...", 0.15f);
            CreateSprites();
            LoadSpriteMaterial();
            uiLayer = LayerMask.NameToLayer("UI");

            EditorUtility.DisplayProgressBar("Montar escena demo", "Creando productos...", 0.25f);
            var products = CreateProducts(out MenuItem polloAsado, out MenuItem polloPlumas);

            EditorUtility.DisplayProgressBar("Montar escena demo", "Creando escena...", 0.35f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSorting();

            CreateCameraAndFloor();

            EditorUtility.DisplayProgressBar("Montar escena demo", "Creando prefabs...", 0.45f);
            CounterItem counterItemPrefab = CreateCounterItemPrefab();
            CustomerGroup groupPrefab = CreateCustomerGroupPrefab();
            Button productButtonPrefab = CreateProductButtonPrefab();

            EditorUtility.DisplayProgressBar("Montar escena demo", "Montando el restaurante...", 0.6f);
            BuildWorld(products, polloAsado, polloPlumas, counterItemPrefab, groupPrefab, productButtonPrefab);
            AddCollisionsToScene();
            UpdateTpvPanelInScene();

            EditorUtility.DisplayProgressBar("Montar escena demo", "Guardando...", 0.9f);
            AssetDatabase.SaveAssets();
            WireProductsFromDisk();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("¡Listo!",
                "Escena creada en Assets/Demo/Demo_Restaurante.\n\n" +
                "Dale a Play. Mira la Console si algo no funciona.\n\n" +
                "Los productos están en Assets/Demo/Data y los prefabs en Assets/Demo/Prefabs: " +
                "ahí es donde se cambia el arte.", "A jugar");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Error al montar la escena",
                "Algo ha fallado. Copia el error rojo de la Console y pásaselo a Claude.\n\n" + e.Message, "Vale");
        }
    }

    // ------------------------------------------------------------------ CARPETAS Y SPRITES

    private static void PrepareFolders()
    {
        if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
        AssetDatabase.CreateFolder("Assets", "Demo");
        AssetDatabase.CreateFolder(Root, "Sprites");
        AssetDatabase.CreateFolder(Root, "Data");
        AssetDatabase.CreateFolder(Root, "Prefabs");
    }

    private static void CreateSprites()
    {
        square = MakeSprite("Cuadrado", 64, 64, (x, y) => 1f);
        circle = MakeSprite("Circulo", 64, 64, (x, y) =>
        {
            float dx = x + 0.5f - 32f, dy = y + 0.5f - 32f;
            return 32f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f;
        });
        capsule = MakeSprite("Capsula", 64, 128, (x, y) =>
        {
            float cy = Mathf.Clamp(y + 0.5f, 32f, 96f);
            float dx = x + 0.5f - 32f, dy = y + 0.5f - cy;
            return 32f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f;
        });
    }

    private static Sprite MakeSprite(string name, int w, int h, System.Func<int, int, float> alpha)
    {
        string assetPath = SpritesFolder + "/" + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
        tex.SetPixels32(pixels);
        tex.Apply();

        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    // Material sin luces, para que todo se vea aunque no haya luces 2D en la escena
    private static void LoadSpriteMaterial()
    {
        spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (spriteMaterial == null)
            spriteMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
    }

    // Lo de más abajo en pantalla se dibuja delante
    private static void ConfigureSorting()
    {
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = Vector3.up;

        foreach (string guid in AssetDatabase.FindAssets("t:Renderer2DData"))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            var so = new SerializedObject(asset);
            var mode = so.FindProperty("m_TransparencySortMode");
            var axis = so.FindProperty("m_TransparencySortAxis");
            if (mode != null) mode.intValue = (int)TransparencySortMode.CustomAxis;
            if (axis != null) axis.vector3Value = Vector3.up;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ------------------------------------------------------------------ PRODUCTOS

    private static List<MenuItem> CreateProducts(out MenuItem polloAsado, out MenuItem polloPlumas)
    {
        var list = new List<MenuItem>
        {
            Product("Agua", ItemCategory.Drink, new Color(0.55f, 0.85f, 1f)),
            Product("Refresco", ItemCategory.Drink, new Color(0.95f, 0.25f, 0.25f)),
            Product("Cerveza", ItemCategory.Drink, new Color(1f, 0.8f, 0.2f)),
            Product("Vino", ItemCategory.Drink, new Color(0.6f, 0.2f, 0.55f)),
        };
        polloAsado = Product("Pollo asado", ItemCategory.Food, new Color(0.85f, 0.5f, 0.2f));
        list.Add(polloAsado);
        list.Add(Product("Hamburguesa", ItemCategory.Food, new Color(0.45f, 0.28f, 0.15f)));
        list.Add(Product("Ensalada", ItemCategory.Food, new Color(0.35f, 0.8f, 0.3f)));
        list.Add(Product("Pasta", ItemCategory.Food, new Color(0.98f, 0.95f, 0.6f)));

        // El especial NO va en la carta: entra al llevarse a un Pájaro azul
        polloPlumas = Product("Pollo con plumas azules", ItemCategory.Food, new Color(0.3f, 0.45f, 0.95f), polloAsado, 1.5f);
        return list;
    }

    private static MenuItem Product(string name, ItemCategory category, Color color, MenuItem replaces = null, float multiplier = 1f)
    {
        var item = ScriptableObject.CreateInstance<MenuItem>();
        item.displayName = name;
        item.category = category;
        item.placeholderColor = color;
        item.replaces = replaces;
        item.priceMultiplier = multiplier;
        AssetDatabase.CreateAsset(item, DataFolder + "/" + name + ".asset");
        return item;
    }

    // ------------------------------------------------------------------ CÁMARA Y SUELO

    private static void CreateCameraAndFloor()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.1f, 0.1f);
        camGo.AddComponent<AudioListener>();

        var floor = Obj("Suelo", null, Vector3.zero, new Vector3(21f, 12f, 1f));
        AddSprite(floor, square, new Color(0.82f, 0.76f, 0.66f), -100);
    }

    // ------------------------------------------------------------------ PREFABS

    private static CounterItem CreateCounterItemPrefab()
    {
        var root = Obj("CounterItem", null, Vector3.zero, new Vector3(0.5f, 0.5f, 1f));
        var sr = AddSprite(root, square, Color.white, 0);
        root.AddComponent<BoxCollider2D>();
        root.AddComponent<SortingGroup>();
        var item = root.AddComponent<CounterItem>();

        var timer = Obj("Timer", root.transform, new Vector3(0f, -0.75f, 0f), new Vector3(1f, 0.2f, 1f));
        AddSprite(timer, square, new Color(0.3f, 0.85f, 0.3f), 1);

        var label = WorldText("Label", root.transform, new Vector3(0f, 0.9f, 0f), "Producto", 3.6f, new Color(0.1f, 0.08f, 0.06f), 6, 2.8f);

        Set(item, "iconRenderer", sr);
        Set(item, "timerBar", timer.transform);
        Set(item, "label", label);

        var asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabsFolder + "/CounterItem.prefab");
        Object.DestroyImmediate(root);
        return asset.GetComponent<CounterItem>();
    }

    private static CustomerGroup CreateCustomerGroupPrefab()
    {
        var root = new GameObject("CustomerGroup");
        var group = root.AddComponent<CustomerGroup>();
        var a = CreateCustomer("Customer_A", root.transform, Vector3.zero);
        var b = CreateCustomer("Customer_B", root.transform, new Vector3(0.6f, 0f, 0f));
        SetArray(group, "members", new Object[] { a, b });

        var asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabsFolder + "/CustomerGroup.prefab");
        Object.DestroyImmediate(root);
        return asset.GetComponent<CustomerGroup>();
    }

    private static Customer CreateCustomer(string name, Transform parent, Vector3 localPos)
    {
        var go = Obj(name, parent, localPos, Vector3.one);
        go.AddComponent<SortingGroup>();
        var customer = go.AddComponent<Customer>();

        var body = Obj("Body", go.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.45f, 0.55f, 1f));
        var bodySr = AddSprite(body, capsule, Color.white, 0);
        body.AddComponent<CapsuleCollider2D>();

        var face = Obj("Face", go.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.35f, 0.35f, 1f));
        var faceSr = AddSprite(face, circle, new Color(0.4f, 0.85f, 0.4f), 1);

        var selected = Obj("Selected", go.transform, Vector3.zero, new Vector3(0.9f, 0.3f, 1f));
        AddSprite(selected, circle, new Color(1f, 0.9f, 0.2f), -1);
        selected.SetActive(false);

        // Aviso: rectángulo de color con texto (¡PEDIR!, CARTA, CUENTA)
        var alert = Obj("Alert", go.transform, new Vector3(0f, 2.0f, 0f), Vector3.one);
        var alertBg = Obj("Bg", alert.transform, Vector3.zero, new Vector3(1.2f, 0.42f, 1f));
        var alertIconSr = AddSprite(alertBg, square, Color.yellow, 2);
        var alertLabel = WorldText("Label", alert.transform, Vector3.zero, "¡PEDIR!", 2.6f, Color.black, 4, 1.2f);
        alert.SetActive(false);

        // Bocadillo de pedido: fondo blanco, cuadrado de color y nombre del producto
        var order = Obj("Order", go.transform, new Vector3(0f, 1.45f, 0f), Vector3.one);
        var orderBg = Obj("Bg", order.transform, Vector3.zero, new Vector3(1.6f, 0.42f, 1f));
        AddSprite(orderBg, square, Color.white, 2);
        var orderIcon = Obj("OrderIcon", order.transform, new Vector3(-0.6f, 0f, 0f), new Vector3(0.3f, 0.3f, 1f));
        var orderIconSr = AddSprite(orderIcon, square, Color.white, 3);
        var orderLabel = WorldText("Label", order.transform, new Vector3(0.15f, 0f, 0f), "Producto", 2.2f, Color.black, 4, 1.25f);
        var distortion = order.AddComponent<OrderBubbleDistortion>();
        Set(distortion, "iconRenderer", orderIconSr);
        Set(distortion, "label", orderLabel);
        order.SetActive(false);

        Set(customer, "bodyRenderer", bodySr);
        Set(customer, "faceRenderer", faceSr);
        Set(customer, "alertBubble", alert);
        Set(customer, "alertIconRenderer", alertIconSr);
        Set(customer, "orderBubble", order);
        Set(customer, "orderIconRenderer", orderIconSr);
        Set(customer, "selectionMarker", selected);
        Set(customer, "alertLabel", alertLabel);
        Set(customer, "orderLabel", orderLabel);
        return customer;
    }

    private static Button CreateProductButtonPrefab()
    {
        var go = UI("BtnProducto", null);
        Place(go, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 80f));
        var img = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        var label = MakeText("Text", go.transform, "Producto", 28f, Color.black);
        Stretch(label.gameObject, 4f);

        var asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabsFolder + "/BtnProducto.prefab");
        Object.DestroyImmediate(go);
        return asset.GetComponent<Button>();
    }

    // ------------------------------------------------------------------ EL RESTAURANTE

    private static void BuildWorld(List<MenuItem> products, MenuItem polloAsado, MenuItem polloPlumas,
        CounterItem counterItemPrefab, CustomerGroup groupPrefab, Button productButtonPrefab)
    {
        // --- Barra en L: parte larga (cocina) ---
        var barra = Obj("Barra", null, BarraPos, BarraScale);
        AddSprite(barra, square, new Color(0.55f, 0.35f, 0.2f), 0);

        var kitchenGo = new GameObject("Kitchen");
        var kitchen = kitchenGo.AddComponent<Kitchen>();
        builtKitchen = kitchen;
        var slots = new List<Object>();
        for (int i = 0; i < 6; i++)
            slots.Add(Obj("Slot_" + (i + 1), kitchenGo.transform, new Vector3(-2.5f + i * 1.4f, 3.7f, 0f), Vector3.one).transform);
        SetArray(kitchen, "menu", products.ConvertAll(p => (Object)p).ToArray());
        SetArray(kitchen, "counterSlots", slots.ToArray());
        Set(kitchen, "counterItemPrefab", counterItemPrefab);
        Set(kitchen, "waitressStandOffset", new Vector2(0f, -0.75f));
        SetSpecialVariant(kitchen, "Pájaro azul", polloAsado, polloPlumas);

        // --- Barra en L: parte corta (platos sucios) ---
        var sucios = Obj("Barra_Sucios", null, new Vector3(5.75f, 2.9f, 0f), new Vector3(0.8f, 1.6f, 1f));
        AddSprite(sucios, square, new Color(0.45f, 0.28f, 0.16f), 0);
        sucios.AddComponent<BoxCollider2D>();
        var dishReturn = sucios.AddComponent<DishReturn>();
        Set(dishReturn, "interactionPoint", Point("InteractionPoint", null, new Vector3(4.9f, 2.6f, 0f)));
        Label("SUCIOS Y SOBRAS", new Vector3(5.75f, 1.85f, 0f));

        // --- Cocinero ---
        var cook = Obj("Cocinero", null, new Vector3(1f, 4.75f, 0f), new Vector3(0.5f, 0.6f, 1f));
        var cookSr = AddSprite(cook, capsule, new Color(0.95f, 0.95f, 0.95f), 0);
        var wander = cook.AddComponent<CookWander>();
        var cookPoints = new GameObject("PuntosCocinero");
        SetArray(wander, "points", new Object[]
        {
            Obj("P1", cookPoints.transform, new Vector3(-2f, 4.75f, 0f), Vector3.one).transform,
            Obj("P2", cookPoints.transform, new Vector3(1f, 4.75f, 0f), Vector3.one).transform,
            Obj("P3", cookPoints.transform, new Vector3(4f, 4.75f, 0f), Vector3.one).transform,
        });
        Set(wander, "spriteRenderer", cookSr);

        // --- Mesas ---
        var tablesRoot = new GameObject("Mesas");
        Vector3[] tablePositions =
        {
            new Vector3(-5f, 1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(3f, 1f, 0f),
            new Vector3(-3f, -2.6f, 0f), new Vector3(1f, -2.6f, 0f),
        };
        for (int i = 0; i < tablePositions.Length; i++)
            CreateTable("Table_" + (i + 1), tablesRoot.transform, tablePositions[i]);

        // --- Cola, entrada y salida ---
        var queueGo = new GameObject("Queue");
        var queue = queueGo.AddComponent<CustomerQueue>();
        var queueSlots = new List<Object>();
        for (int i = 0; i < 4; i++)
            queueSlots.Add(Obj("Slot_" + (i + 1), queueGo.transform, new Vector3(-9.2f, 3f - i * 1.4f, 0f), Vector3.one).transform);
        SetArray(queue, "slots", queueSlots.ToArray());
        Label("COLA", new Vector3(-8.9f, 4.4f, 0f));
        var entrance = Obj("Entrance", null, new Vector3(-10.4f, -4.5f, 0f), Vector3.one);
        var exit = Obj("Exit", null, new Vector3(-10.4f, -4.5f, 0f), Vector3.one);

        // --- Stand del TPV ---
        var cartas = Obj("Cartas", null, new Vector3(9.2f, 1.4f, 0f), new Vector3(0.7f, 0.7f, 1f));
        AddSprite(cartas, square, new Color(0.9f, 0.6f, 0.2f), 0);
        cartas.AddComponent<BoxCollider2D>();
        var cartasSource = cartas.AddComponent<ItemSource>();
        Set(cartasSource, "itemType", (int)HeldItemType.Menu);
        Set(cartasSource, "interactionPoint", Point("IP_Cartas", null, new Vector3(8.2f, 1.4f, 0f)));
        Label("CARTAS", new Vector3(9.2f, 2.0f, 0f));

        // Cubiertos: en la punta izquierda de la barra larga
        var cubiertos = Obj("Cubiertos", null, CubiertosPos, CubiertosScale);
        AddSprite(cubiertos, square, new Color(0.75f, 0.75f, 0.8f), 1);
        cubiertos.AddComponent<BoxCollider2D>();
        var cubiertosSource = cubiertos.AddComponent<ItemSource>();
        Set(cubiertosSource, "itemType", (int)HeldItemType.Cutlery);
        Set(cubiertosSource, "interactionPoint", Point("IP_Cubiertos", null, CubiertosIP));
        Label("CUBIERTOS", CubiertosLabelPos);

        var tpvGo = Obj("TPV", null, new Vector3(9.2f, -1.1f, 0f), new Vector3(0.9f, 0.7f, 1f));
        AddSprite(tpvGo, square, new Color(0.15f, 0.15f, 0.2f), 0);
        tpvGo.AddComponent<BoxCollider2D>();
        var tpv = tpvGo.AddComponent<Tpv>();
        Set(tpv, "interactionPoint", Point("IP_TPV", null, new Vector3(8.2f, -1.1f, 0f)));
        Label("TPV", new Vector3(9.2f, -0.5f, 0f));

        // --- Trastero ---
        var door = Obj("Trastero", null, new Vector3(9.3f, -4.4f, 0f), new Vector3(1f, 1.6f, 1f));
        AddSprite(door, square, new Color(0.25f, 0.15f, 0.12f), 0);
        door.AddComponent<BoxCollider2D>();
        var storeroom = door.AddComponent<Storeroom>();
        Set(storeroom, "interactionPoint", Point("IP_Trastero", null, new Vector3(8.3f, -4.6f, 0f)));
        Label("TRASTERO", new Vector3(9.3f, -3.3f, 0f));

        // --- Camarera ---
        var waitressGo = new GameObject("Waitress");
        waitressGo.transform.position = new Vector3(5f, -3.5f, 0f);
        waitressGo.AddComponent<SortingGroup>();
        var waitress = waitressGo.AddComponent<WaitressController>(); // añade WaitressHands solo
        var hands = waitressGo.GetComponent<WaitressHands>();
        var wBody = Obj("Body", waitressGo.transform, new Vector3(0f, 0.65f, 0f), new Vector3(0.5f, 0.6f, 1f));
        var wBodySr = AddSprite(wBody, capsule, new Color(0.9f, 0.3f, 0.45f), 0);
        var handL = Obj("Hand_L", waitressGo.transform, new Vector3(-0.3f, 1.55f, 0f), new Vector3(0.3f, 0.3f, 1f));
        var handR = Obj("Hand_R", waitressGo.transform, new Vector3(0.3f, 1.55f, 0f), new Vector3(0.3f, 0.3f, 1f));
        var handLSr = AddSprite(handL, square, Color.white, 5);
        var handRSr = AddSprite(handR, square, Color.white, 5);
        var handLabel0 = WorldText("HandLabel_1", waitressGo.transform, new Vector3(0f, 1.95f, 0f), "", 2.2f, new Color(0.1f, 0.08f, 0.06f), 6, 2f);
        var handLabel1 = WorldText("HandLabel_2", waitressGo.transform, new Vector3(0f, 2.25f, 0f), "", 2.2f, new Color(0.1f, 0.08f, 0.06f), 6, 2f);
        Set(waitress, "spriteRenderer", wBodySr);
        SetArray(hands, "handIcons", new Object[] { handLSr, handRSr });
        SetArray(hands, "handLabels", new Object[] { handLabel0, handLabel1 });

        // --- Interfaz ---
        var ui = BuildUI(productButtonPrefab);
        Set(tpv, "panel", ui.tpvPanel);

        // --- Managers ---
        var managers = new GameObject("Managers");
        var click = managers.AddComponent<ClickManager>();
        Set(click, "waitress", waitress);

        var spawner = managers.AddComponent<CustomerSpawner>();
        Set(spawner, "groupPrefab", groupPrefab);
        Set(spawner, "queue", queue);
        Set(spawner, "entrancePoint", entrance.transform);
        Set(spawner, "exitPoint", exit.transform);
        SetLooks(spawner);

        var rage = managers.AddComponent<RageMeter>();
        Set(rage, "rageSlider", ui.rageSlider);

        var money = managers.AddComponent<MoneyManager>();
        Set(money, "moneyText", ui.moneyText);

        var storeroomManager = managers.AddComponent<StoreroomManager>();
        Set(storeroomManager, "waitress", waitress);
        Set(storeroomManager, "storeroom", storeroom);
        Set(storeroomManager, "blackoutPanel", ui.blackoutPanel);

        var day = managers.AddComponent<DayManager>();
        Set(day, "clockText", ui.clockText);
        Set(day, "endPanel", ui.endPanel);
        Set(day, "endTitleText", ui.endTitle);
        Set(day, "endDetailText", ui.endDetail);
        Set(day, "retryButton", ui.retryButton);

        Selection.activeGameObject = managers;
    }

    private static void CreateTable(string name, Transform parent, Vector3 position)
    {
        var go = Obj(name, parent, position, Vector3.one);
        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.4f, 0.9f);
        go.AddComponent<SortingGroup>();
        var table = go.AddComponent<Table>();

        var visual = Obj("Visual", go.transform, Vector3.zero, new Vector3(1.4f, 0.9f, 1f));
        var visualSr = AddSprite(visual, square, Color.white, 0);

        var cutlery = Obj("Cutlery", go.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.15f, 1f));
        AddSprite(cutlery, square, new Color(0.5f, 0.5f, 0.55f), 1);
        cutlery.SetActive(false);

        Set(table, "tableRenderer", visualSr);
        Set(table, "cutleryVisual", cutlery);
        Set(table, "defaultSeatOffset", 1f);
        Set(table, "interactionPoint", Point("InteractionPoint", go.transform, position + new Vector3(0f, -0.95f, 0f)));
    }

    private static void SetSpecialVariant(Kitchen kitchen, string customerType, MenuItem original, MenuItem variant)
    {
        var so = new SerializedObject(kitchen);
        var arr = so.FindProperty("specialVariants");
        arr.arraySize = 1;
        var el = arr.GetArrayElementAtIndex(0);
        el.FindPropertyRelative("customerType").stringValue = customerType;
        el.FindPropertyRelative("original").objectReferenceValue = original;
        el.FindPropertyRelative("variant").objectReferenceValue = variant;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLooks(CustomerSpawner spawner)
    {
        string[] names = { "Pájaro azul", "Gato", "Rana", "Cerdo" };
        Color[] colors =
        {
            new Color(0.3f, 0.5f, 1f), new Color(1f, 0.6f, 0.2f),
            new Color(0.35f, 0.75f, 0.35f), new Color(1f, 0.65f, 0.75f),
        };
        var so = new SerializedObject(spawner);
        var arr = so.FindProperty("looks");
        arr.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("typeName").stringValue = names[i];
            el.FindPropertyRelative("tint").colorValue = colors[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ INTERFAZ

    private class UIRefs
    {
        public TpvPanel tpvPanel;
        public Slider rageSlider;
        public TextMeshProUGUI moneyText, clockText, endTitle, endDetail;
        public GameObject blackoutPanel, endPanel;
        public Button retryButton;
    }

    private static UIRefs BuildUI(Button productButtonPrefab)
    {
        var refs = new UIRefs();

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
        var topLeft = new Vector2(0f, 1f);
        var topRight = new Vector2(1f, 1f);
        var center = new Vector2(0.5f, 0.5f);

        // --- Barra superior ---
        var pauseButton = MakeButton("BtnPausa", c, "II", topLeft, new Vector2(60f, -50f), new Vector2(80f, 60f), new Color(0.2f, 0.2f, 0.25f), 32f);
        var rageLabel = MakeText("TextoIra", c, "IRA", 30f, Color.white);
        Place(rageLabel.gameObject, topLeft, new Vector2(150f, -50f), new Vector2(80f, 50f));
        refs.rageSlider = MakeSlider("BarraIra", c, topLeft, new Vector2(400f, -50f), new Vector2(400f, 30f));
        refs.moneyText = MakeText("TextoDinero", c, "0 € / 100 €", 34f, Color.white);
        Place(refs.moneyText.gameObject, topLeft, new Vector2(800f, -50f), new Vector2(320f, 50f));
        refs.clockText = MakeText("TextoReloj", c, "12:00", 40f, Color.white);
        Place(refs.clockText.gameObject, topRight, new Vector2(-110f, -50f), new Vector2(180f, 60f));

        // --- Mensajes en pantalla (abajo en el centro) ---
        var messages = MakeText("Mensajes", c, "", 42f, new Color(0.15f, 0.1f, 0.08f));
        Place(messages.gameObject, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1500f, 80f));
        var gm = messages.gameObject.AddComponent<GameMessages>();
        Set(gm, "text", messages);

        // --- Panel del TPV ---
        var tpvRoot = UI("TpvPanel", c);
        Place(tpvRoot, center, Vector2.zero, new Vector2(900f, 600f));
        tpvRoot.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.95f);
        var tpvTitle = MakeText("Titulo", tpvRoot.transform, "TPV", 44f, Color.white);
        Place(tpvTitle.gameObject, center, new Vector2(0f, 250f), new Vector2(400f, 70f));
        var status = MakeText("EstadoBarra", tpvRoot.transform, "Barra: 0/6", 30f, new Color(0.8f, 0.8f, 0.8f));
        Place(status.gameObject, center, new Vector2(0f, 190f), new Vector2(400f, 50f));
        var buttons = UI("Buttons", tpvRoot.transform);
        Place(buttons, center, new Vector2(0f, 40f), new Vector2(860f, 180f));
        var grid = buttons.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200f, 80f);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.MiddleCenter;
        var billButton = MakeButton("BtnCuenta", tpvRoot.transform, "Cuenta", center, new Vector2(-150f, -200f), new Vector2(260f, 80f), new Color(0.25f, 0.6f, 0.3f), 34f);
        var closeButton = MakeButton("BtnCerrar", tpvRoot.transform, "Cerrar", center, new Vector2(150f, -200f), new Vector2(260f, 80f), new Color(0.5f, 0.2f, 0.2f), 34f);

        refs.tpvPanel = canvasGo.AddComponent<TpvPanel>();
        Set(refs.tpvPanel, "root", tpvRoot);
        Set(refs.tpvPanel, "buttonContainer", buttons.transform);
        Set(refs.tpvPanel, "productButtonPrefab", productButtonPrefab);
        Set(refs.tpvPanel, "billButton", billButton);
        Set(refs.tpvPanel, "closeButton", closeButton);
        Set(refs.tpvPanel, "statusText", status);
        tpvRoot.SetActive(false);

        // --- Pantalla en negro ---
        refs.blackoutPanel = UI("PanelNegro", c);
        Stretch(refs.blackoutPanel, 0f);
        refs.blackoutPanel.AddComponent<Image>().color = Color.black;
        var dots = MakeText("Texto", refs.blackoutPanel.transform, "...", 90f, Color.white);
        Stretch(dots.gameObject, 0f);
        refs.blackoutPanel.SetActive(false);

        // --- Fin de día ---
        refs.endPanel = UI("PanelFinDia", c);
        Stretch(refs.endPanel, 0f);
        refs.endPanel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        refs.endTitle = MakeText("TituloFin", refs.endPanel.transform, "Fin del día", 72f, Color.white);
        Place(refs.endTitle.gameObject, center, new Vector2(0f, 160f), new Vector2(1200f, 120f));
        refs.endDetail = MakeText("DetalleFin", refs.endPanel.transform, "", 40f, new Color(0.85f, 0.85f, 0.85f));
        Place(refs.endDetail.gameObject, center, new Vector2(0f, 10f), new Vector2(1200f, 160f));
        refs.retryButton = MakeButton("BtnReintentar", refs.endPanel.transform, "Reintentar", center, new Vector2(0f, -170f), new Vector2(340f, 90f), new Color(0.25f, 0.5f, 0.8f), 40f);
        refs.endPanel.SetActive(false);

        // --- Pausa (la última: se dibuja encima de todo) ---
        var pausePanel = UI("PanelPausa", c);
        Stretch(pausePanel, 0f);
        pausePanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var pauseTitle = MakeText("TituloPausa", pausePanel.transform, "PAUSA", 72f, Color.white);
        Place(pauseTitle.gameObject, center, new Vector2(0f, 230f), new Vector2(600f, 120f));
        var resume = MakeButton("BtnContinuar", pausePanel.transform, "Continuar", center, new Vector2(0f, 90f), new Vector2(360f, 80f), new Color(0.25f, 0.5f, 0.3f), 36f);
        var restart = MakeButton("BtnReiniciar", pausePanel.transform, "Reiniciar", center, new Vector2(0f, -10f), new Vector2(360f, 80f), new Color(0.3f, 0.3f, 0.4f), 36f);
        var quit = MakeButton("BtnSalir", pausePanel.transform, "Salir", center, new Vector2(0f, -110f), new Vector2(360f, 80f), new Color(0.5f, 0.2f, 0.2f), 36f);
        var pause = canvasGo.AddComponent<PauseMenu>();
        Set(pause, "pausePanel", pausePanel);
        Set(pause, "pauseButton", pauseButton);
        Set(pause, "resumeButton", resume);
        Set(pause, "restartButton", restart);
        Set(pause, "quitButton", quit);
        pausePanel.SetActive(false);

        return refs;
    }

    private static Slider MakeSlider(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = UI(name, parent);
        Place(go, anchor, pos, size);

        var bg = UI("Background", go.transform);
        Stretch(bg, 0f);
        bg.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

        var fillArea = UI("Fill Area", go.transform);
        Stretch(fillArea, 3f);
        var fill = UI("Fill", fillArea.transform);
        Stretch(fill, 0f);
        fill.AddComponent<Image>().color = new Color(0.85f, 0.15f, 0.15f);

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = null;
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 0f;
        return slider;
    }

    private static Button MakeButton(string name, Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color, float fontSize)
    {
        var go = UI(name, parent);
        Place(go, anchor, pos, size);
        var img = go.AddComponent<Image>();
        img.color = color;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        var text = MakeText("Text", go.transform, label, fontSize, Color.white);
        Stretch(text.gameObject, 4f);
        return button;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string content, float size, Color color)
    {
        var go = UI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static GameObject UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (uiLayer >= 0) go.layer = uiLayer;
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(GameObject go, float padding)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    // ------------------------------------------------------------------ AYUDANTES DEL MUNDO

    private static GameObject Obj(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        return go;
    }

    // Punto vacío en una posición del MUNDO (aunque tenga padre)
    private static Transform Point(string name, Transform parent, Vector3 worldPosition)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        return go.transform;
    }

    private static SpriteRenderer AddSprite(GameObject go, Sprite sprite, Color color, int order)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        return sr;
    }

    private static TextMeshPro WorldText(string name, Transform parent, Vector3 localPos, string content, float size, Color color, int order, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(width, 0.6f);
        t.sortingOrder = order;
        go.transform.localPosition = localPos;
        return t;
    }

    private static void Label(string content, Vector3 position)
    {
        var go = new GameObject("Etiqueta_" + content);
        var t = go.AddComponent<TextMeshPro>();
        t.text = content;
        t.fontSize = 2.2f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.15f, 0.12f, 0.1f);
        t.rectTransform.sizeDelta = new Vector2(4f, 0.8f);
        t.sortingOrder = 50;
        go.transform.position = position;
    }

    // ------------------------------------------------------------------ CONECTAR CAMPOS DEL INSPECTOR

    private static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = Find(so, target, field);
        if (p == null) return;
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        var p = Find(so, target, field);
        if (p == null) return;
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Para enums
    private static void Set(Object target, string field, int enumIndex)
    {
        var so = new SerializedObject(target);
        var p = Find(so, target, field);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = enumIndex;
        else p.intValue = enumIndex;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string field, Vector2 value)
    {
        var so = new SerializedObject(target);
        var p = Find(so, target, field);
        if (p == null) return;
        p.vector2Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var p = Find(so, target, field);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty Find(SerializedObject so, Object target, string field)
    {
        var p = so.FindProperty(field);
        if (p == null)
            Debug.LogError($"[Demo] No encuentro el campo '{field}' en {target.GetType().Name}. ¿Has cambiado el script?");
        return p;
    }

    // Conecta la carta y el plato especial cargando los productos desde disco,
    // justo antes de guardar, para que la escena no pierda las referencias.
    private static void WireProductsFromDisk()
    {
        var menu = new List<Object>();
        foreach (string n in MenuNames)
        {
            var item = AssetDatabase.LoadAssetAtPath<MenuItem>(DataFolder + "/" + n + ".asset");
            if (item == null) Debug.LogError("[Demo] No encuentro el producto " + n);
            else menu.Add(item);
        }
        var pollo = AssetDatabase.LoadAssetAtPath<MenuItem>(DataFolder + "/Pollo asado.asset");
        var plumas = AssetDatabase.LoadAssetAtPath<MenuItem>(DataFolder + "/Pollo con plumas azules.asset");
        if (plumas != null && pollo != null && plumas.replaces != pollo)
        {
            plumas.replaces = pollo;
            EditorUtility.SetDirty(plumas);
            AssetDatabase.SaveAssets();
        }

        SetArray(builtKitchen, "menu", menu.ToArray());
        SetSpecialVariant(builtKitchen, "Pájaro azul", pollo, plumas);
        EditorUtility.SetDirty(builtKitchen);
    }

    // ------------------------------------------------------------------ COLISIONES Y PAREDES

    // Añade paredes, marca los obstáculos y crea la WalkGrid en la escena ABIERTA.
    // Sirve para escenas ya montadas: no borra nada ni toca otros cambios.
    [UnityEditor.MenuItem("Tools/The House Special/Añadir colisiones y paredes")]
    public static void AddCollisionsMenu()
    {
        if (GameObject.Find("Suelo") == null)
        {
            EditorUtility.DisplayDialog("Añadir colisiones", "Abre primero la escena del restaurante (no encuentro el objeto 'Suelo').", "Vale");
            return;
        }
        if (square == null) square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesFolder + "/Cuadrado.png");
        if (spriteMaterial == null) LoadSpriteMaterial();

        AddCollisionsToScene();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!",
            "Paredes, colisiones y rejilla de caminos añadidas.\n\nGuarda la escena con Ctrl+S y dale a Play.", "Vale");
    }

    private static void AddCollisionsToScene()
    {
        var floor = GameObject.Find("Suelo");
        if (floor == null) return;
        Bounds b = floor.GetComponent<SpriteRenderer>().bounds;
        const float wall = 0.3f;

        // La barra larga no tenía collider: se lo ponemos para poder chocar con ella
        var barra = GameObject.Find("Barra");
        if (barra != null && barra.GetComponent<Collider2D>() == null) barra.AddComponent<BoxCollider2D>();

        // Obstáculos: barra, barra de sucios, stand, trastero y todas las mesas
        foreach (string n in new[] { "Barra", "Barra_Sucios", "Cartas", "Cubiertos", "TPV", "Trastero" })
            MarkObstacle(GameObject.Find(n));
        foreach (Table t in Object.FindObjectsByType<Table>())
            MarkObstacle(t.gameObject);

        // Paredes alrededor del suelo, con un hueco en la puerta (junto a la entrada)
        var old = GameObject.Find("Paredes");
        if (old != null) Object.DestroyImmediate(old);
        var walls = new GameObject("Paredes");
        float doorY = -4.5f;
        var entrance = GameObject.Find("Entrance");
        if (entrance != null) doorY = entrance.transform.position.y;
        const float doorHalf = 0.8f;

        Wall(walls.transform, "Pared_Arriba", new Vector2(b.center.x, b.max.y - wall / 2f), new Vector2(b.size.x, wall));
        Wall(walls.transform, "Pared_Abajo", new Vector2(b.center.x, b.min.y + wall / 2f), new Vector2(b.size.x, wall));
        Wall(walls.transform, "Pared_Derecha", new Vector2(b.max.x - wall / 2f, b.center.y), new Vector2(wall, b.size.y));
        float lowTop = doorY - doorHalf, highBottom = doorY + doorHalf;
        if (lowTop > b.min.y)
            Wall(walls.transform, "Pared_Izquierda_Abajo", new Vector2(b.min.x + wall / 2f, (b.min.y + lowTop) / 2f), new Vector2(wall, lowTop - b.min.y));
        if (highBottom < b.max.y)
            Wall(walls.transform, "Pared_Izquierda_Arriba", new Vector2(b.min.x + wall / 2f, (highBottom + b.max.y) / 2f), new Vector2(wall, b.max.y - highBottom));

        // Rejilla de caminos: el suelo menos el grosor de las paredes
        var oldGrid = GameObject.Find("WalkGrid");
        if (oldGrid != null) Object.DestroyImmediate(oldGrid);
        var gridGo = new GameObject("WalkGrid");
        var grid = gridGo.AddComponent<WalkGrid>();
        var so = new SerializedObject(grid);
        so.FindProperty("areaMin").vector2Value = new Vector2(b.min.x + wall, b.min.y + wall);
        so.FindProperty("areaMax").vector2Value = new Vector2(b.max.x - wall, b.max.y - wall);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MarkObstacle(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<NavObstacle>() == null) go.AddComponent<NavObstacle>();
    }

    private static void Wall(Transform parent, string name, Vector2 center, Vector2 size)
    {
        var go = Obj(name, parent, center, new Vector3(size.x, size.y, 1f));
        AddSprite(go, square, new Color(0.35f, 0.24f, 0.18f), -50);
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<NavObstacle>();
    }

    // ------------------------------------------------------------------ CUBIERTOS EN LA BARRA

    // La barra se alarga por la izquierda y los cubiertos van en esa punta,
    // así no se quita ningún hueco de platos y bebidas.
    private static readonly Vector3 BarraPos = new Vector3(0.4f, 3.9f, 0f);
    private static readonly Vector3 BarraScale = new Vector3(9.8f, 0.8f, 1f);
    private static readonly Vector3 CubiertosPos = new Vector3(-3.85f, 3.9f, 0f);
    private static readonly Vector3 CubiertosScale = new Vector3(0.6f, 0.6f, 1f);
    private static readonly Vector3 CubiertosIP = new Vector3(-3.85f, 2.95f, 0f);
    private static readonly Vector3 CubiertosLabelPos = new Vector3(-3.85f, 4.55f, 0f);

    // Mueve los cubiertos a la barra en la escena ABIERTA, sin tocar nada más
    [UnityEditor.MenuItem("Tools/The House Special/Mover cubiertos a la barra")]
    public static void MoveCutleryMenu()
    {
        var barra = GameObject.Find("Barra");
        var cubiertos = GameObject.Find("Cubiertos");
        if (barra == null || cubiertos == null)
        {
            EditorUtility.DisplayDialog("Mover cubiertos", "Abre primero la escena del restaurante (no encuentro 'Barra' o 'Cubiertos').", "Vale");
            return;
        }

        Undo.RecordObject(barra.transform, "Mover cubiertos");
        barra.transform.position = BarraPos;
        barra.transform.localScale = BarraScale;

        Undo.RecordObject(cubiertos.transform, "Mover cubiertos");
        cubiertos.transform.position = CubiertosPos;
        cubiertos.transform.localScale = CubiertosScale;
        var sr = cubiertos.GetComponent<SpriteRenderer>();
        if (sr != null) { Undo.RecordObject(sr, "Mover cubiertos"); sr.sortingOrder = 1; } // por encima de la barra

        var ip = GameObject.Find("IP_Cubiertos");
        if (ip != null) { Undo.RecordObject(ip.transform, "Mover cubiertos"); ip.transform.position = CubiertosIP; }

        var label = GameObject.Find("Etiqueta_CUBIERTOS");
        if (label != null) { Undo.RecordObject(label.transform, "Mover cubiertos"); label.transform.position = CubiertosLabelPos; }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!", "Cubiertos movidos a la punta izquierda de la barra.\n\nGuarda la escena con Ctrl+S y dale a Play.", "Vale");
    }

    // ------------------------------------------------------------------ PANEL DEL TPV (versión con comanda)

    private static readonly Color TpvGreen = new Color(0.25f, 0.65f, 0.3f);
    private static readonly Color TpvBlue = new Color(0.25f, 0.5f, 0.95f);
    private static readonly Color TpvRed = new Color(0.75f, 0.2f, 0.2f);
    private static readonly Color TpvGrey = new Color(0.4f, 0.4f, 0.45f);

    // Rehace los botones del panel del TPV en la escena ABIERTA (Pedir, Cuenta, Borrar, X),
    // lo hace más translúcido y pone en azul el aviso de cuenta del cliente.
    [UnityEditor.MenuItem("Tools/The House Special/Actualizar panel del TPV")]
    public static void UpdateTpvPanelMenu()
    {
        if (Object.FindObjectsByType<TpvPanel>().Length == 0)
        {
            EditorUtility.DisplayDialog("Actualizar TPV", "Abre primero la escena del restaurante (no encuentro el TpvPanel).", "Vale");
            return;
        }
        uiLayer = LayerMask.NameToLayer("UI");
        UpdateTpvPanelInScene();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!", "Panel del TPV actualizado.\n\nGuarda la escena con Ctrl+S y dale a Play.", "Vale");
    }

    private static void UpdateTpvPanelInScene()
    {
        TpvPanel[] panels = Object.FindObjectsByType<TpvPanel>();
        if (panels.Length == 0) return;
        TpvPanel tpv = panels[0];

        var so = new SerializedObject(tpv);
        var root = so.FindProperty("root").objectReferenceValue as GameObject;
        if (root == null) return;
        Transform r = root.transform;
        var center = new Vector2(0.5f, 0.5f);

        // Fondo más translúcido
        var bg = root.GetComponent<Image>();
        if (bg != null) bg.color = new Color(0.08f, 0.08f, 0.12f, 0.72f);

        // Fuera los botones viejos (y los nuevos si ya se había actualizado antes)
        foreach (string n in new[] { "BtnCuenta", "BtnCerrar", "BtnPedir", "BtnBorrar", "Comanda" })
        {
            Transform old = r.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var selection = MakeText("Comanda", r, "Pulsa los productos para apuntarlos en la comanda", 30f, Color.white);
        Place(selection.gameObject, center, new Vector2(0f, -105f), new Vector2(840f, 60f));

        var bill = MakeButton("BtnCuenta", r, "Cuenta", center, new Vector2(-300f, -215f), new Vector2(200f, 70f), TpvBlue, 30f);
        var clear = MakeButton("BtnBorrar", r, "Borrar", center, new Vector2(-70f, -215f), new Vector2(160f, 70f), TpvGrey, 28f);
        var order = MakeButton("BtnPedir", r, "Pedir", center, new Vector2(225f, -215f), new Vector2(320f, 90f), TpvGreen, 42f);
        var close = MakeButton("BtnCerrar", r, "X", new Vector2(1f, 1f), new Vector2(-45f, -45f), new Vector2(70f, 70f), TpvRed, 40f);

        so.FindProperty("billButton").objectReferenceValue = bill;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("orderButton").objectReferenceValue = order;
        so.FindProperty("clearButton").objectReferenceValue = clear;
        so.FindProperty("selectionText").objectReferenceValue = selection;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Cuenta en azul también en la mano de la camarera
        foreach (WaitressHands hands in Object.FindObjectsByType<WaitressHands>())
        {
            var hso = new SerializedObject(hands);
            hso.FindProperty("billColor").colorValue = TpvBlue;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }

        // Y en el aviso de cuenta de los clientes (prefab)
        string prefabPath = PrefabsFolder + "/CustomerGroup.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var group = contents.GetComponent<CustomerGroup>();
            if (group != null)
            {
                var gso = new SerializedObject(group);
                gso.FindProperty("billAlertColor").colorValue = TpvBlue;
                gso.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void AddSceneToBuild()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
