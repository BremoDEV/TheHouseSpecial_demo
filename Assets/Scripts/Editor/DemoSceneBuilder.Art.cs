using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arte de comida y bebida (carpeta Assets/Arte).
// Uso: menú  Tools → The House Special → Aplicar arte de comida y bebida  (con Demo_Restaurante abierta)
// - Ajusta la importación de los PNG (pixel art: sin suavizado, 128 píxeles = 1 unidad).
// - Carta de la demo: Agua, Vino, Brochetas de carne y Pollo asado, cada uno con su dibujo.
// - Platos especiales: Pájaro azul → Pollo con plumas azules; Perro → Brocheta de la casa.
// - Mesas: plato limpio y plato sucio. Camarera: pila de platos sucios al recoger una mesa.
// - El tipo de cliente "Gato" pasa a llamarse "Perro".
// Para añadir más arte: mete el PNG en Assets/Arte y añade una línea en la lista 'Products' de abajo.
public static partial class DemoSceneBuilder
{
    private const string ArtFolder = "Assets/Arte";

    // Producto → archivo de su dibujo. El orden es el de los botones del TPV.
    private static readonly (string name, ItemCategory category, string sprite, Color color)[] Products =
    {
        ("Agua", ItemCategory.Drink, "sprite_Agua", new Color(0.55f, 0.85f, 1f)),
        ("Refresco", ItemCategory.Drink, "sprite_Refresco", new Color(0.95f, 0.25f, 0.25f)),
        ("Cerveza", ItemCategory.Drink, "sprite_Cerveza", new Color(1f, 0.8f, 0.2f)),
        ("Vino", ItemCategory.Drink, "sprite_Vino", new Color(0.6f, 0.2f, 0.55f)),
        ("Brochetas de carne", ItemCategory.Food, "sprite_carne1", new Color(0.6f, 0.3f, 0.2f)),
        ("Pollo asado", ItemCategory.Food, "sprite_pollo1", new Color(0.85f, 0.5f, 0.2f)),
    };

    // Platos especiales: (tipo de cliente, plato normal, nombre del especial, dibujo)
    private static readonly (string customer, string original, string special, string sprite)[] Specials =
    {
        ("Pájaro azul", "Pollo asado", "Pollo con plumas azules", "sprite_pollo2"),
        ("Perro", "Brochetas de carne", "Brocheta de la casa", "sprite_carne2"),
    };

    private const string CleanPlateSprite = "sprite_platoLimpio";
    private const string DirtyPlateSprite = "sprite_platoSucio";
    private const string DirtyStackSprite = "sprite_platosSucios";
    private const string CloudSprite = "sprite_bocadillo_nube";
    private const string CutleryBoxSprite = "sprite_Caja_Cubiertos";   // en la esquina de la barra
    private const string CutlerySprite = "sprite_Cubiertos";           // en la mano y en la mesa
    private const string MenuBoxSprite = "sprite_Caja_Cartas";         // al final de la barra larga
    private const string MenuSprite = "sprite_Carta";                  // en la mano
    // Centro del cuerpo de la nube dentro del dibujo (para que el producto quede en medio y las burbujitas debajo)
    private static readonly Vector2 CloudPivot = new Vector2(0.5f, 0.5625f);

    [UnityEditor.MenuItem("Tools/The House Special/Aplicar arte de comida y bebida")]
    public static void ApplyArtMenu()
    {
        if (Object.FindAnyObjectByType<Kitchen>() == null)
        {
            EditorUtility.DisplayDialog("Aplicar arte", "Abre primero la escena Demo_Restaurante.", "Vale");
            return;
        }
        if (!AssetDatabase.IsValidFolder(ArtFolder))
        {
            EditorUtility.DisplayDialog("Aplicar arte", "No encuentro la carpeta " + ArtFolder + ".", "Vale");
            return;
        }

        ApplyArtToScene();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!",
            "Arte aplicado: carta de 4 productos con dibujo, platos en las mesas y especiales del Pájaro azul y del Perro.\n\n" +
            "Guarda la escena con Ctrl+S y dale a Play.\n\n" +
            "Después vuelve a lanzar 'Montar tutorial y menú principal' para que el tutorial también lo tenga.", "Vale");
    }

    private static void ApplyArtToScene()
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder)) return;

        // 0) Borrar copias repetidas ("sprite_X 1.png" idéntico a "sprite_X.png"). Van a la papelera.
        RemoveDuplicateArt();

        // 1) Importación de todos los PNG de la carpeta de arte
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
            ConfigurePixelArt(AssetDatabase.GUIDToAssetPath(guid));

        // 2) Productos de la carta
        var menu = new List<Object>();
        var byName = new Dictionary<string, MenuItem>();
        foreach (var p in Products)
        {
            MenuItem item = GetOrCreateProduct(p.name, p.category, p.color);
            item.icon = LoadArt(p.sprite);
            item.replaces = null;
            item.priceMultiplier = 1f;
            EditorUtility.SetDirty(item);
            menu.Add(item);
            byName[p.name] = item;
        }

        // 3) Platos especiales
        var kitchen = Object.FindAnyObjectByType<Kitchen>();
        var so = new SerializedObject(kitchen);
        var arr = so.FindProperty("specialVariants");
        arr.arraySize = Specials.Length;
        for (int i = 0; i < Specials.Length; i++)
        {
            var s = Specials[i];
            byName.TryGetValue(s.original, out MenuItem original);
            MenuItem special = GetOrCreateProduct(s.special, ItemCategory.Food, new Color(0.3f, 0.45f, 0.95f));
            special.icon = LoadArt(s.sprite);
            special.replaces = original;
            special.priceMultiplier = 1.5f;
            EditorUtility.SetDirty(special);

            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("customerType").stringValue = s.customer;
            el.FindPropertyRelative("original").objectReferenceValue = original;
            el.FindPropertyRelative("variant").objectReferenceValue = special;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        SetArray(kitchen, "menu", menu.ToArray());
        EditorUtility.SetDirty(kitchen);
        AssetDatabase.SaveAssets();

        // 4) Platos de las mesas
        Sprite clean = LoadArt(CleanPlateSprite), dirty = LoadArt(DirtyPlateSprite);
        foreach (Table t in Object.FindObjectsByType<Table>())
        {
            Set(t, "cleanPlateSprite", clean);
            Set(t, "dirtyPlateSprite", dirty);
            Set(t, "cutlerySprite", LoadArt(CutlerySprite));
            // Sitios de cada cosa en la mesa: cubiertos por fuera, plato en medio, bebida detrás
            SetVector2Array(t, "plateSpots", new Vector2(-0.3f, -0.1f), new Vector2(0.3f, -0.1f));
            SetVector2Array(t, "cutlerySpots", new Vector2(-0.8f, -0.08f), new Vector2(0.8f, -0.08f));
            SetVector2Array(t, "drinkSpots", new Vector2(-0.42f, 0.16f), new Vector2(0.42f, 0.16f));
            Set(t, "cutlerySize", 0.6f);
            Set(t, "itemSize", 0.75f);
        }

        // 5) Pila de platos sucios en la mano de la camarera
        var hands = Object.FindAnyObjectByType<WaitressHands>();
        if (hands != null)
        {
            Set(hands, "dirtyDishesIcon", LoadArt(DirtyStackSprite));
            Set(hands, "cutleryIcon", LoadArt(CutlerySprite));
            Set(hands, "menuIcon", LoadArt(MenuSprite));
        }

        // 5b) Caja de cubiertos (esquina de la L) y caja de cartas (final de la barra larga)
        SetObjectArt("Cubiertos", LoadArt(CutleryBoxSprite), 0.85f);
        SetObjectArt("Cartas", LoadArt(MenuBoxSprite), 0.8f);

        // 6) Bocadillos de pedido en forma de nube (en el prefab de los clientes)
        Sprite cloud = LoadArt(CloudSprite);
        string groupPrefabPath = PrefabsFolder + "/CustomerGroup.prefab";
        if (cloud != null && AssetDatabase.LoadAssetAtPath<GameObject>(groupPrefabPath) != null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(groupPrefabPath);
            foreach (OrderBubbleDistortion b in root.GetComponentsInChildren<OrderBubbleDistortion>(true))
            {
                Set(b, "cloudSprite", cloud);
                Set(b, "cloudWidth", 1.4f);
                Set(b, "artIconSize", 0.88f);
            }
            PrefabUtility.SaveAsPrefabAsset(root, groupPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 7) El Gato pasa a ser el Perro
        var spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner != null)
        {
            var sso = new SerializedObject(spawner);
            var looks = sso.FindProperty("looks");
            for (int i = 0; i < looks.arraySize; i++)
            {
                var typeName = looks.GetArrayElementAtIndex(i).FindPropertyRelative("typeName");
                if (typeName.stringValue == "Gato") typeName.stringValue = "Perro";
            }
            sso.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetVector2Array(Object target, string field, params Vector2[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).vector2Value = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Cambia el dibujo de un objeto de la escena (p. ej. la caja de cubiertos) y lo deja de 'width' unidades de ancho
    private static void SetObjectArt(string objectName, Sprite sprite, float width)
    {
        var go = GameObject.Find(objectName);
        if (go == null || sprite == null) return;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Undo.RecordObject(sr, "Aplicar arte");
        Undo.RecordObject(go.transform, "Aplicar arte");
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = 1;
        float size = Mathf.Max(0.01f, sprite.bounds.size.x);
        go.transform.localScale = new Vector3(width / size, width / size, 1f);
        // El collider se queda del tamaño del dibujo (era un cuadrado de 1x1)
        var box = go.GetComponent<BoxCollider2D>();
        if (box != null) box.size = sprite.bounds.size;
    }

    // Borra "nombre 1.png" si es exactamente igual que "nombre.png" (copias al arrastrar dos veces)
    private static void RemoveDuplicateArt()
    {
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(" 1.png")) continue;
            string original = path.Substring(0, path.Length - " 1.png".Length) + ".png";
            string fullCopy = System.IO.Path.Combine(projectRoot, path);
            string fullOriginal = System.IO.Path.Combine(projectRoot, original);
            if (!System.IO.File.Exists(fullOriginal)) continue;
            byte[] a = System.IO.File.ReadAllBytes(fullCopy), b = System.IO.File.ReadAllBytes(fullOriginal);
            if (a.Length != b.Length || !System.Linq.Enumerable.SequenceEqual(a, b)) continue;
            AssetDatabase.MoveAssetToTrash(path);
            Debug.Log("[Arte] Copia repetida enviada a la papelera: " + path);
        }
    }

    // Pixel art: un solo sprite por archivo, sin suavizado ni compresión, centrado, 128 píxeles = 1 unidad
    private static void ConfigurePixelArt(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 128;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (path.EndsWith(CloudSprite + ".png"))
        {
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = CloudPivot;
        }
        else settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static Sprite LoadArt(string fileName)
    {
        string path = ArtFolder + "/" + fileName + ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning("[Arte] No encuentro el dibujo " + path);
        return sprite;
    }

    private static MenuItem GetOrCreateProduct(string name, ItemCategory category, Color color)
    {
        string path = DataFolder + "/" + name + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<MenuItem>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<MenuItem>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.displayName = name;
        item.category = category;
        item.placeholderColor = color;
        return item;
    }
}
