using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Distribución del restaurante (versión 2, según el esquema del equipo):
// - Cola FUERA, a la izquierda. Se entra por el hueco de la pared izquierda.
// - Arriba: 3 mesas pegadas a la pared (al banco). Abajo: 2 mesas.
// - Arriba a la derecha: puerta del trastero, TPV y cartas.
// - Derecha: barra en L con la cocina detrás. Parte LARGA (vertical) = donde sale la comida.
//   Parte CORTA (horizontal) = sucios y sobras. Cubiertos en la esquina de la L.
// Uso: menú  Tools → The House Special → Reorganizar restaurante (esquema nuevo)
// Mueve los objetos de la escena ABIERTA (no borra tus cambios de textos ni de valores).
// Todas las posiciones están aquí arriba para poder retocarlas fácilmente.
public static partial class DemoSceneBuilder
{
    // ---------- Zonas (en unidades del mundo; la cámara ve de x -10.67 a 10.67 y de y -6 a 6)
    private const float OutsideRight = -8.0f;     // donde acaba la calle y empieza la pared izquierda
    private const float LeftWallRight = -7.44f;   // borde interior de la pared izquierda
    private const float DoorTop = 1.7f;           // hueco de la entrada (pared izquierda)
    private const float DoorBottom = -1.4f;
    private const float TopWallBottom = 3.6f;     // donde empieza la pared de arriba (banco)
    private const float BenchRight = 5.1f;        // hasta dónde llega el banco rojo

    // ---------- Barra en L
    private const float BarX = 7.95f;             // centro de la parte larga (vertical)
    private const float BarWidth = 0.9f;
    private const float BarTop = 0.1f;            // borde de arriba de la L
    private const float ShortBarHeight = 0.8f;

    private static readonly Vector3[] LayoutTables =
    {
        new Vector3(-5.4f, 2.95f, 0f), new Vector3(-1.2f, 2.95f, 0f), new Vector3(3.15f, 2.95f, 0f), // arriba
        new Vector3(-5.0f, -3.5f, 0f), new Vector3(1.8f, -3.5f, 0f),                                  // abajo
    };

    private static readonly Color FloorColor = new Color(0.8f, 0.69f, 0.57f);
    private static readonly Color OutsideColor = new Color(0.9f, 0.87f, 0.74f);
    private static readonly Color WallColor = new Color(0.66f, 0.45f, 0.43f);
    private static readonly Color TopWallColor = new Color(0.78f, 0.63f, 0.57f);
    private static readonly Color BenchColor = new Color(0.56f, 0.23f, 0.23f);
    private static readonly Color KitchenColor = new Color(0.7f, 0.52f, 0.48f);

    [UnityEditor.MenuItem("Tools/The House Special/Reorganizar restaurante (esquema nuevo)")]
    public static void ApplyLayoutMenu()
    {
        if (GameObject.Find("Suelo") == null || GameObject.Find("Barra") == null)
        {
            EditorUtility.DisplayDialog("Reorganizar", "Abre primero la escena Demo_Restaurante.", "Vale");
            return;
        }
        if (square == null) square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesFolder + "/Cuadrado.png");
        if (spriteMaterial == null) LoadSpriteMaterial();

        ApplyLayoutToScene();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!",
            "Restaurante reorganizado.\n\nGuarda la escena con Ctrl+S y dale a Play.\n\n" +
            "Después vuelve a lanzar 'Montar tutorial y menú principal' para que el tutorial use la nueva distribución.", "Vale");
    }

    private static void ApplyLayoutToScene()
    {
        // --- Fondo: suelo, calle, banco y cocina (solo colores) ---
        var floor = GameObject.Find("Suelo");
        if (floor != null)
        {
            floor.GetComponent<SpriteRenderer>().color = FloorColor;
            Move(floor.transform, Vector3.zero, new Vector3(21.4f, 12f, 1f)); // que llegue hasta los bordes de la pantalla
        }
        ReplaceDecor("Calle", new Vector2((-10.67f + OutsideRight) / 2f, 0f), new Vector2(OutsideRight + 10.67f, 12f), OutsideColor, -99);
        ReplaceDecor("Banco", new Vector2((LeftWallRight + BenchRight) / 2f, 4.25f), new Vector2(BenchRight - LeftWallRight, 1.3f), BenchColor, -40);
        float kitchenLeft = BarX + BarWidth / 2f;
        float kitchenTop = BarTop - ShortBarHeight;
        ReplaceDecor("SueloCocina", new Vector2((kitchenLeft + 10.67f) / 2f, (kitchenTop - 6f) / 2f),
            new Vector2(10.67f - kitchenLeft, kitchenTop + 6f), KitchenColor, -90);

        // --- Mesas ---
        int i = 0;
        foreach (Table t in Object.FindObjectsByType<Table>())
        {
            // Table_1..Table_5 en orden; si tienen otro nombre, en el orden en que salgan
            int index = i;
            if (t.name.StartsWith("Table_") && int.TryParse(t.name.Substring(6), out int n) && n >= 1 && n <= LayoutTables.Length)
                index = n - 1;
            if (index < LayoutTables.Length) Move(t.transform, LayoutTables[index]);
            i++;
        }

        // --- Barra en L ---
        float longTop = BarTop - ShortBarHeight;
        var barra = GameObject.Find("Barra");
        Move(barra.transform, new Vector3(BarX, (longTop - 6f) / 2f, 0f), new Vector3(BarWidth, longTop + 6f, 1f));

        SendBehind(barra); // si no, la barra (vertical) taparía los platos que salen encima

        var sucios = GameObject.Find("Barra_Sucios");
        SendBehind(sucios);
        float shortLeft = BarX - BarWidth / 2f;
        if (sucios != null)
        {
            Move(sucios.transform, new Vector3((shortLeft + 10.67f) / 2f, BarTop - ShortBarHeight / 2f, 0f),
                new Vector3(10.67f - shortLeft, ShortBarHeight, 1f));
            MoveInteractionPoint(sucios.GetComponent<Interactable>(), new Vector3(9.4f, BarTop + 0.75f, 0f));
        }

        // Huecos de la barra larga (de arriba abajo) y dónde se pone la camarera para recoger
        var kitchen = Object.FindAnyObjectByType<Kitchen>();
        if (kitchen != null)
        {
            for (int s = 0; s < kitchen.transform.childCount; s++)
                Move(kitchen.transform.GetChild(s), new Vector3(BarX, longTop - 0.6f - s * 0.8f, 0f));
            Set(kitchen, "waitressStandOffset", new Vector2(-0.85f, 0f));
        }

        // Cocinero detrás de la barra
        var cook = GameObject.Find("Cocinero");
        if (cook != null) Move(cook.transform, new Vector3(9.6f, -3f, 0f));
        var cookPoints = GameObject.Find("PuntosCocinero");
        if (cookPoints != null)
        {
            float[] ys = { -1.6f, -3.3f, -5.0f };
            for (int p = 0; p < cookPoints.transform.childCount && p < ys.Length; p++)
                Move(cookPoints.transform.GetChild(p), new Vector3(9.6f, ys[p], 0f));
        }

        // --- Cubiertos en la esquina de la L ---
        var cubiertos = GameObject.Find("Cubiertos");
        if (cubiertos != null)
        {
            Move(cubiertos.transform, new Vector3(BarX, BarTop - ShortBarHeight / 2f, 0f), new Vector3(0.6f, 0.6f, 1f));
            var sr = cubiertos.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 1;
            MoveInteractionPoint(cubiertos.GetComponent<Interactable>(), new Vector3(BarX - 1f, BarTop - ShortBarHeight / 2f, 0f));
        }

        // --- Arriba a la derecha: trastero, TPV y cartas ---
        var door = GameObject.Find("Trastero");
        if (door != null)
        {
            Move(door.transform, new Vector3(6.4f, 4.3f, 0f), new Vector3(1.3f, 1.6f, 1f));
            MoveInteractionPoint(door.GetComponent<Interactable>(), new Vector3(6.4f, 2.75f, 0f));
        }
        var tpv = GameObject.Find("TPV");
        if (tpv != null)
        {
            Move(tpv.transform, new Vector3(8.6f, 3.95f, 0f));
            MoveInteractionPoint(tpv.GetComponent<Interactable>(), new Vector3(8.6f, 3.0f, 0f));
        }
        var cartas = GameObject.Find("Cartas");
        if (cartas != null)
        {
            Move(cartas.transform, new Vector3(9.9f, 3.95f, 0f));
            MoveInteractionPoint(cartas.GetComponent<Interactable>(), new Vector3(9.9f, 3.0f, 0f));
        }

        // --- Cola fuera, entrada y salida por abajo a la izquierda ---
        var queue = Object.FindAnyObjectByType<CustomerQueue>();
        if (queue != null)
            for (int s = 0; s < queue.transform.childCount; s++)
                Move(queue.transform.GetChild(s), new Vector3(-9.6f, 0.2f - s * 1.5f, 0f));
        var entrance = GameObject.Find("Entrance");
        if (entrance != null) Move(entrance.transform, new Vector3(-9.6f, -6.6f, 0f));
        var exit = GameObject.Find("Exit");
        if (exit != null) Move(exit.transform, new Vector3(-9.6f, -6.6f, 0f));

        // --- Camarera ---
        var waitress = GameObject.Find("Waitress");
        if (waitress != null) Move(waitress.transform, new Vector3(0f, 0f, 0f));

        // --- Carteles (se mueven, no se cambia el texto que hayáis puesto) ---
        MoveLabel("COLA", new Vector3(-9.3f, 1.3f, 0f), 4f);
        MoveLabel("CARTAS", new Vector3(9.9f, 4.65f, 0f), 2f);
        MoveLabel("TPV", new Vector3(8.6f, 4.65f, 0f), 2f);
        MoveLabel("TRASTERO", new Vector3(6.4f, 5.4f, 0f), 3f);
        MoveLabel("CUBIERTOS", new Vector3(6.55f, -0.3f, 0f), 2.2f);
        MoveLabel("SUCIOS", new Vector3(9.45f, 0.75f, 0f), 2.4f); // estrecho: se parte en dos líneas

        // --- Paredes, colisiones y rejilla de caminos ---
        AddCollisionsToScene();
    }

    // Paredes de la distribución nueva: pared izquierda con hueco de entrada y bloque de pared arriba.
    // La rejilla de caminos cubre toda la pantalla (también la calle, para que los clientes lleguen a la cola).
    private static void BuildLayoutWalls()
    {
        var old = GameObject.Find("Paredes");
        if (old != null) Object.DestroyImmediate(old);
        var walls = new GameObject("Paredes");

        float wallX = (OutsideRight + LeftWallRight) / 2f;
        float wallW = LeftWallRight - OutsideRight;
        WallColored(walls.transform, "Pared_Izquierda_Arriba", new Vector2(wallX, (DoorTop + 6f) / 2f), new Vector2(wallW, 6f - DoorTop), WallColor);
        WallColored(walls.transform, "Pared_Izquierda_Abajo", new Vector2(wallX, (DoorBottom - 6f) / 2f), new Vector2(wallW, DoorBottom + 6f), WallColor);
        WallColored(walls.transform, "Pared_Arriba", new Vector2((LeftWallRight + 10.67f) / 2f, (TopWallBottom + 6f) / 2f),
            new Vector2(10.67f - LeftWallRight, 6f - TopWallBottom), TopWallColor);

        var oldGrid = GameObject.Find("WalkGrid");
        if (oldGrid != null) Object.DestroyImmediate(oldGrid);
        var grid = new GameObject("WalkGrid").AddComponent<WalkGrid>();
        var so = new SerializedObject(grid);
        so.FindProperty("areaMin").vector2Value = new Vector2(-10.6f, -5.85f);
        so.FindProperty("areaMax").vector2Value = new Vector2(10.6f, 5.9f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ AYUDANTES

    private static void Move(Transform t, Vector3 position, Vector3? scale = null)
    {
        if (t == null) return;
        Undo.RecordObject(t, "Reorganizar restaurante");
        t.position = position;
        if (scale.HasValue) t.localScale = scale.Value;
    }

    // Las barras se dibujan por debajo de lo que se pone encima (platos, cubiertos)
    private static void SendBehind(GameObject go)
    {
        if (go == null) return;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Undo.RecordObject(sr, "Reorganizar restaurante");
        sr.sortingOrder = -5;
    }

    private static void MoveInteractionPoint(Interactable target, Vector3 position)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var p = so.FindProperty("interactionPoint");
        if (p != null && p.objectReferenceValue is Transform t) Move(t, position);
    }

    // Busca un cartel cuyo nombre o texto contenga 'key' y lo mueve
    private static void MoveLabel(string key, Vector3 position, float width)
    {
        foreach (TextMeshPro t in Object.FindObjectsByType<TextMeshPro>())
        {
            if (!t.name.StartsWith("Etiqueta_")) continue;
            if (!t.name.ToUpper().Contains(key) && !t.text.ToUpper().Contains(key)) continue;
            Move(t.transform, position);
            t.rectTransform.sizeDelta = new Vector2(width, 0.8f);
            t.textWrappingMode = TextWrappingModes.Normal;
            return;
        }
    }

    private static void ReplaceDecor(string name, Vector2 center, Vector2 size, Color color, int order)
    {
        var old = GameObject.Find(name);
        if (old != null) Object.DestroyImmediate(old);
        var go = Obj(name, null, center, new Vector3(size.x, size.y, 1f));
        AddSprite(go, square, color, order);
    }

    private static void WallColored(Transform parent, string name, Vector2 center, Vector2 size, Color color)
    {
        var go = Obj(name, parent, center, new Vector3(size.x, size.y, 1f));
        AddSprite(go, square, color, -50);
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<NavObstacle>();
    }
}
