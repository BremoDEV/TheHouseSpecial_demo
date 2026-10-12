using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arte de comida y bebida (carpeta Assets/Arte).
// Uso: menú  Tools → The House Special → Aplicar arte de comida y bebida  (con Demo_Restaurante abierta)
// - Ajusta la importación de los PNG (pixel art: sin suavizado, 128 píxeles = 1 unidad).
// - Carta de la demo: Agua, Vino, Brochetas de carne y Pollo asado, cada uno con su dibujo.
// - Platos especiales: Pájaro azul → Pollo con plumas azules; Loba → Brocheta de la casa.
// - Mesas: plato limpio y plato sucio. Camarera: pila de platos sucios al recoger una mesa.
// - Clientes con dibujo (Assets/Arte/PJ): Pájaro azul, Loba, Hámster y Zorra. Camarera: Haiki.
// Los dibujos se buscan por nombre en Assets/Arte y TODAS sus subcarpetas (da igual cómo las ordenéis).
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
        ("Loba", "Brochetas de carne", "Brocheta de la casa", "sprite_carne2"),
    };

    // Tipos de cliente: (nombre, dibujo). El nombre es el que usan los platos especiales y el tutorial.
    private static readonly (string name, string sprite)[] CustomerArt =
    {
        ("Pájaro azul", "NPC_1_PajaroAzul"),
        ("Loba", "NPC_2_Loba"),
        ("Hámster", "NPC_3_hamster"),
        ("Zorra", "NPC_4_Zorra"),
    };
    private const string WaitressSprite = "MainChar_Haiki";
    private const string CookSprite = "NPC_Especial_Cocinero";
    // Alto del dibujo de los personajes en unidades (PNG de 128x256 contando el hueco transparente)
    private const float CharacterArtHeight = 1.6f;

    private const string CleanPlateSprite = "sprite_platoLimpio";
    private const string DirtyPlateSprite = "sprite_platoSucio";
    private const string DirtyStackSprite = "sprite_platosSucios";
    private const string CloudSprite = "sprite_bocadillo_nube";
    private const string CutleryBoxSprite = "sprite_Caja_Cubiertos";   // en la esquina de la barra
    private const string CutlerySprite = "sprite_Cubiertos";           // en la mano y en la mesa
    private const string MenuBoxSprite = "sprite_Caja_Cartas";         // al final de la barra larga
    private const string MenuSprite = "sprite_Carta";                  // en la mano
    private const string TpvSprite = "sprite_TPV";                     // arriba a la derecha
    private const float TpvWidth = 1.6f;
    private const string StoreroomDoorSprite = "sprite_Puerta_trastero"; // arriba, al lado de la TPV
    private const float StoreroomDoorWidth = 1.7f; // ancho del PNG entero (la puerta es la mitad: 0.85)
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
            "Arte aplicado: carta de 4 productos con dibujo, platos en las mesas y especiales del Pájaro azul y de la Loba, clientes y camarera con dibujo.\n\n" +
            "Guarda la escena con Ctrl+S y dale a Play.\n\n" +
            "Después vuelve a lanzar 'Montar tutorial y menú principal' para que el tutorial también lo tenga.", "Vale");
    }

    private static void ApplyArtToScene()
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder)) return;

        // 0) Borrar copias repetidas ("sprite_X 1.png" idéntico a "sprite_X.png"). Van a la papelera.
        RemoveDuplicateArt();

        // 1) Importación de todos los PNG de la carpeta de arte (menos el arte del menú, que no es pixel art)
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(ArtFolder + "/Menu/") || path.StartsWith(ArtFolder + "/UI/")) continue;
            ConfigurePixelArt(path);
        }

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
            SetVector2Array(t, "plateSpots", new Vector2(-0.3f, -0.05f), new Vector2(0.3f, -0.05f));
            SetVector2Array(t, "cutlerySpots", new Vector2(-0.64f, -0.05f), new Vector2(0.64f, -0.05f));
            SetVector2Array(t, "drinkSpots", new Vector2(-0.42f, 0.22f), new Vector2(0.42f, 0.22f));
            Set(t, "cutlerySize", 0.5f);
            Set(t, "itemSize", 0.7f);
            ApplyTableArt(t);
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
        // TPV con dibujo y sin la etiqueta de texto
        if (LoadArt(TpvSprite) != null)
        {
            SetObjectArt("TPV", LoadArt(TpvSprite), TpvWidth);
            // Que se ordene por altura como los personajes: la camarera, delante de la TPV (no detrás)
            var tpvSr = GameObject.Find("TPV").GetComponent<SpriteRenderer>();
            if (tpvSr != null) tpvSr.sortingOrder = 0;
            HideLabel("TPV");
        }
        // Puerta del trastero con dibujo y sin la etiqueta de texto
        if (LoadArt(StoreroomDoorSprite) != null)
        {
            SetObjectArt("Trastero", LoadArt(StoreroomDoorSprite), StoreroomDoorWidth);
            // La puerta ocupa la mitad central del PNG: el clic solo en la puerta, no en lo transparente de los lados
            var door = GameObject.Find("Trastero");
            var doorBox = door != null ? door.GetComponent<BoxCollider2D>() : null;
            if (doorBox != null) doorBox.size = new Vector2(doorBox.size.x * 0.5f, doorBox.size.y);
            // El borde de abajo de la puerta, justo donde acaba la pared (donde empieza el suelo)
            if (door != null)
            {
                var p = door.transform.position;
                float height = LoadArt(StoreroomDoorSprite).bounds.size.y * door.transform.localScale.y;
                door.transform.position = new Vector3(p.x, TopWallBottom + height / 2f, p.z);
            }
            HideLabel("TRASTERO");
        }

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
            // Los personajes con dibujo son más anchos: más hueco entre los dos de un grupo en la cola
            foreach (CustomerGroup g in root.GetComponentsInChildren<CustomerGroup>(true))
                Set(g, "queueMemberOffset", new Vector2(0.85f, 0f));
            Transform memberB = root.transform.Find("Customer_B");
            if (memberB != null) memberB.localPosition = new Vector3(0.85f, 0f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, groupPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 7) Clientes con dibujo (sin tinte de color)
        var spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner != null)
        {
            var sso = new SerializedObject(spawner);
            var looks = sso.FindProperty("looks");
            looks.arraySize = CustomerArt.Length;
            for (int i = 0; i < CustomerArt.Length; i++)
            {
                var el = looks.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("typeName").stringValue = CustomerArt[i].name;
                el.FindPropertyRelative("sprite").objectReferenceValue = LoadArt(CustomerArt[i].sprite);
                el.FindPropertyRelative("tint").colorValue = Color.white;
            }
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 7a) Barra en L y suelo de la cocina con dibujo
        ApplyBarArt();
        // 7a2) Suelo y pared de arriba con los tiles (Assets/Arte/Tiles)
        ApplyTileArt();

        // 7a3) Fuera todas las etiquetas de texto del suelo (CUBIERTOS, CARTAS, PLATOS SUCIOS...): ya se ve todo con dibujos
        foreach (TMPro.TextMeshPro t in Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Include))
            if (t.name.StartsWith("Etiqueta_")) t.gameObject.SetActive(false);

        // 7b) Cartel "PLS WAIT" al principio de la cola (sustituye a la etiqueta COLA)
        PlaceWaitSign();

        // 7c) Cocinero con dibujo, de espaldas a la barra (la sartén mira a la derecha, hacia la cocina)
        var cook = GameObject.Find("Cocinero");
        Sprite cookSprite = LoadArt(CookSprite);
        if (cook != null && cookSprite != null)
        {
            var csr = cook.GetComponent<SpriteRenderer>();
            if (csr != null)
            {
                Undo.RecordObject(csr, "Aplicar arte");
                Undo.RecordObject(cook.transform, "Aplicar arte");
                csr.sprite = cookSprite;
                csr.color = Color.white;
                csr.flipX = true; // el dibujo mira a la izquierda: volteado mira a la derecha
                float s = CharacterArtHeight / Mathf.Max(0.01f, cookSprite.bounds.size.y);
                cook.transform.localScale = new Vector3(s, s, 1f);
            }
            var wander = cook.GetComponent<CookWander>();
            if (wander != null)
            {
                var wso = new SerializedObject(wander);
                var flip = wso.FindProperty("flipWithMovement");
                if (flip != null) flip.boolValue = false;
                wso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 8) Camarera: Haiki
        var waitress = Object.FindAnyObjectByType<WaitressController>();
        Sprite haiki = LoadArt(WaitressSprite);
        if (waitress != null && haiki != null)
        {
            var wso = new SerializedObject(waitress);
            var sr = wso.FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer;
            wso.FindProperty("flipWithMovement").boolValue = false; // siempre mira al frente
            wso.ApplyModifiedPropertiesWithoutUndo();
            if (sr != null)
            {
                Undo.RecordObject(sr, "Aplicar arte");
                Undo.RecordObject(sr.transform, "Aplicar arte");
                sr.sprite = haiki;
                sr.color = Color.white;
                sr.flipX = false;
                float scale = CharacterArtHeight / Mathf.Max(0.01f, haiki.bounds.size.y);
                sr.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                var col = sr.GetComponent<Collider2D>();
                if (col is CapsuleCollider2D cap)
                {
                    cap.size = new Vector2(haiki.bounds.size.x * 0.6f, haiki.bounds.size.y * 0.8f);
                    cap.offset = Vector2.zero;
                }
            }
            // Lo que lleva en las manos, por encima de la cabeza
            SetChildY(waitress.transform, "Hand_L", 1.75f);
            SetChildY(waitress.transform, "Hand_R", 1.75f);
            SetChildY(waitress.transform, "HandLabel_1", 2.15f);
            SetChildY(waitress.transform, "HandLabel_2", 2.45f);
        }
    }

    private const string WaitSignSprite = "Sprite_WaitSign";
    // Encima del primer sitio de la cola (y = 0.2), entre las dos personas del grupo
    private static readonly Vector3 WaitSignPosition = new Vector3(-9.2f, 2.5f, 0f);
    private const float WaitSignHeight = 1.3f;

    private static void PlaceWaitSign()
    {
        Sprite sign = LoadArt(WaitSignSprite);
        if (sign == null) return;

        GameObject go = GameObject.Find("CartelEspera");
        if (go == null)
        {
            go = new GameObject("CartelEspera");
            Undo.RegisterCreatedObjectUndo(go, "Cartel de espera");
            go.AddComponent<SpriteRenderer>();
        }
        Undo.RecordObject(go.transform, "Cartel de espera");
        go.transform.position = WaitSignPosition;
        float scale = WaitSignHeight / Mathf.Max(0.01f, sign.bounds.size.y);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sign;
        sr.color = Color.white;
        sr.sortingOrder = 0;

        // Fuera la etiqueta de texto "COLA"
        HideLabel("COLA");
    }

    // Oculta una etiqueta de texto del suelo (Etiqueta_XXX). No la borra, por si se quiere recuperar.
    private static void HideLabel(string key)
    {
        foreach (TMPro.TextMeshPro t in Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Include))
        {
            if (!t.name.StartsWith("Etiqueta_")) continue;
            if (t.name.ToUpper() == "ETIQUETA_" + key || t.text.ToUpper() == key)
                t.gameObject.SetActive(false);
        }
    }

    // Mesa con dibujo: sustituye el rectángulo blanco. No cambia de color; la suciedad se ve por los platos sucios.
    private const string TableSprite = "sprite_MesaCliente";
    private const float TableArtWidth = 1.6f;
    // El tablero ocupa la parte de arriba del dibujo (el mantel y las patas van debajo):
    // se baja el dibujo para que el tablero quede donde van los platos y las bebidas.
    private const float TableArtOffsetY = -0.3f;

    private static void ApplyTableArt(Table t)
    {
        Sprite mesa = LoadArt(TableSprite);
        if (mesa == null) return;
        var so = new SerializedObject(t);
        var sr = so.FindProperty("tableRenderer").objectReferenceValue as SpriteRenderer;
        so.FindProperty("tintByState").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (sr == null) return;

        Undo.RecordObject(sr, "Aplicar arte");
        Undo.RecordObject(sr.transform, "Aplicar arte");
        sr.sprite = mesa;
        sr.color = Color.white;
        float scale = TableArtWidth / Mathf.Max(0.01f, mesa.bounds.size.x);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.localPosition = new Vector3(0f, TableArtOffsetY, 0f);

        // El collider (clic y obstáculo) cubre el tablero, sin las patas
        var box = t.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Undo.RecordObject(box, "Aplicar arte");
            box.size = new Vector2(TableArtWidth, 1.0f);
            box.offset = new Vector2(0f, -0.1f);
        }
    }

    // ---------- Barra en L con dibujo
    // Los tres PNG son de 384x384 (una cuadrícula de 3x3 trozos) y cada uno tiene dibujado solo su trozo:
    //   sprite_barra_comandas = parte LARGA (vertical, donde sale la comida), con la esquina arriba
    //   sprite_barraSucios    = parte CORTA (horizontal, sucios y sobras): encimera + frente
    //   Sprite_barra_suelo    = suelo de la cocina
    // Se recorta cada trozo (lo transparente fuera) y se estira al tamaño de la barra del juego.
    // Escala: el ancho de la barra larga (64 píxeles del dibujo) = BarWidth del juego.
    private const string BarLongSprite = "sprite_barra_comandas";
    private const string BarShortSprite = "sprite_barraSucios";
    private const string KitchenFloorSprite = "Sprite_barra_suelo";
    private const float BarArtLongWidthPx = 64f;
    private const float BarArtCornerPx = 64f; // la esquina de arriba de la barra larga (no se repite)

    private static void ApplyBarArt()
    {
        float ppu = BarArtLongWidthPx / BarWidth;
        Sprite longBar = CroppedSprite(BarLongSprite, ppu, new Vector4(0f, 0f, 0f, BarArtCornerPx));
        Sprite shortBar = CroppedSprite(BarShortSprite, ppu, Vector4.zero);
        Sprite floor = CroppedSprite(KitchenFloorSprite, ppu, Vector4.zero);
        if (longBar == null || shortBar == null || floor == null) return;

        float left = BarX - BarWidth / 2f;   // borde izquierdo de la barra larga
        float right = BarX + BarWidth / 2f;  // donde empieza la cocina
        const float screenRight = 10.67f, screenBottom = -6f;

        // Parte larga: de arriba de la L hasta abajo. La esquina se queda arriba y el resto se repite.
        ArtPiece("Arte_BarraLarga", longBar, new Vector2(left, BarTop),
            new Vector2(BarWidth, BarTop - screenBottom), SpriteDrawMode.Tiled, -5);
        // Parte corta: desde la barra larga hasta el borde de la pantalla (encimera + frente)
        float shortHeight = shortBar.bounds.size.y;
        ArtPiece("Arte_BarraCorta", shortBar, new Vector2(right, BarTop),
            new Vector2(screenRight - right, shortHeight), SpriteDrawMode.Tiled, -5);
        // Suelo de la cocina: debajo del frente de la barra corta
        float floorTop = BarTop - shortHeight;
        ArtPiece("Arte_SueloCocina", floor, new Vector2(right, floorTop),
            new Vector2(screenRight - right, floorTop - screenBottom), SpriteDrawMode.Sliced, -90);

        // Los rectángulos de color de antes se esconden (sus colliders se quedan: siguen chocando y se pueden clicar)
        foreach (string n in new[] { "Barra", "Barra_Sucios", "SueloCocina" })
        {
            var go = GameObject.Find(n);
            var sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
            if (sr == null) continue;
            Undo.RecordObject(sr, "Aplicar arte");
            sr.enabled = false;
        }
    }

    // ---------- Tiles (Assets/Arte/Tiles): baldosas de 64x64 que se repiten
    //   tileset_suelo                   = suelo del restaurante (todo el fondo)
    //   tiles_pared                     = papel pintado de la pared de arriba
    //   tilese_pared_base(la roja)      = la fila de abajo de la pared, con el rodapié rojo (toca el suelo)
    private const string FloorTile = "tileset_suelo";
    private const string WallTile = "tiles_pared";
    private const string WallBaseTile = "tilese_pared_base(la roja)";
    private const float BenchTintAlpha = 0.45f; // lo opaco que es el rojo del banco encima de la pared (0 = nada, 1 = rojo liso)
    private const float TileSize = 0.8f; // tamaño de cada baldosa en el juego (la pared de arriba mide 3 baldosas)

    private static void ApplyTileArt()
    {
        float ppu = 64f / TileSize;
        Sprite floorTile = CroppedSprite(FloorTile, ppu, Vector4.zero);
        Sprite wallTile = CroppedSprite(WallTile, ppu, Vector4.zero);
        Sprite baseTile = CroppedSprite(WallBaseTile, ppu, Vector4.zero);
        const float screenLeft = -10.7f, screenRight = 10.7f, screenTop = 6f, screenBottom = -6f;

        // Suelo: toda la pantalla (la calle, la cocina, etc. se dibujan encima)
        var floor = GameObject.Find("Suelo");
        if (floorTile != null && floor != null)
        {
            ArtPiece("Arte_Suelo", floorTile, new Vector2(screenLeft, screenTop),
                new Vector2(screenRight - screenLeft, screenTop - screenBottom), SpriteDrawMode.Tiled, -100);
            HideRenderer(floor);
        }

        // Pared de arriba: una fila de rodapié abajo (justo donde empieza el suelo) y papel pintado hasta arriba
        var wall = GameObject.Find("Pared_Arriba");
        if (wallTile != null && baseTile != null && wall != null)
        {
            float left = LeftWallRight;
            ArtPiece("Arte_ParedBase", baseTile, new Vector2(left, TopWallBottom + TileSize),
                new Vector2(screenRight - left, TileSize), SpriteDrawMode.Tiled, -50);
            ArtPiece("Arte_Pared", wallTile, new Vector2(left, screenTop),
                new Vector2(screenRight - left, screenTop - TopWallBottom - TileSize), SpriteDrawMode.Tiled, -50);
            HideRenderer(wall); // el collider se queda: sigue siendo pared

            // El banco rojo: la misma pared, con el rectángulo rojo por encima medio transparente (como un filtro)
            var bench = GameObject.Find("Banco");
            var benchSr = bench != null ? bench.GetComponent<SpriteRenderer>() : null;
            if (benchSr != null)
            {
                Undo.RecordObject(benchSr, "Aplicar arte");
                benchSr.color = new Color(BenchColor.r, BenchColor.g, BenchColor.b, BenchTintAlpha);
            }
        }
    }

    private static void HideRenderer(GameObject go)
    {
        var sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
        if (sr == null) return;
        Undo.RecordObject(sr, "Aplicar arte");
        sr.enabled = false;
    }

    // Crea (o actualiza) un objeto con el dibujo, con su esquina de arriba a la izquierda en 'topLeft'
    private static void ArtPiece(string name, Sprite sprite, Vector2 topLeft, Vector2 size, SpriteDrawMode mode, int order)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Aplicar arte");
            go.AddComponent<SpriteRenderer>();
        }
        go.transform.position = new Vector3(topLeft.x, topLeft.y, 0f);
        go.transform.localScale = Vector3.one;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.drawMode = mode;
        if (mode == SpriteDrawMode.Tiled) sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.sortingOrder = order;
        if (spriteMaterial == null) LoadSpriteMaterial();
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
    }

    // Recorta la parte dibujada de un PNG y la guarda como sprite propio (en Assets/Demo/Sprites),
    // con el punto de anclaje arriba a la izquierda y bordes para la esquina.
    private static Sprite CroppedSprite(string fileName, float ppu, Vector4 border)
    {
        Sprite source = LoadArt(fileName);
        if (source == null) return null;
        string texPath = AssetDatabase.GetAssetPath(source.texture);
        var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) return null;

        // Rectángulo con píxeles visibles (Unity cuenta desde abajo a la izquierda)
        Color32[] px = tex.GetPixels32();
        int w = tex.width, h = tex.height, minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 10)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < 0) return null;
        var rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);

        var sprite = Sprite.Create(tex, rect, new Vector2(0f, 1f), ppu, 0, SpriteMeshType.FullRect, border);
        sprite.name = fileName + "_recorte";
        string outPath = SpritesFolder + "/" + fileName + "_recorte.asset";
        var old = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
        if (old != null)
        {
            EditorUtility.CopySerialized(sprite, old);
            EditorUtility.SetDirty(old);
            AssetDatabase.SaveAssets();
            return old;
        }
        AssetDatabase.CreateAsset(sprite, outPath);
        AssetDatabase.SaveAssets();
        return sprite;
    }

    private static void SetChildY(Transform parent, string child, float y)
    {
        Transform t = parent.Find(child);
        if (t == null) return;
        Undo.RecordObject(t, "Aplicar arte");
        t.localPosition = new Vector3(t.localPosition.x, y, t.localPosition.z);
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

    // Busca "nombre.png" en Assets/Arte y en todas sus subcarpetas
    // (mira todos los dibujos y compara el nombre, así funcionan también nombres con espacios o paréntesis)
    private static Sprite LoadArt(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != fileName) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }
        Debug.LogWarning("[Arte] No encuentro el dibujo " + fileName + ".png en " + ArtFolder);
        return null;
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
