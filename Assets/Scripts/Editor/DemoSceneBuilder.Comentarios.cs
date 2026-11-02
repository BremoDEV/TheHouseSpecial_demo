using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Quinta parte del montador: COMENTARIOS ALEATORIOS de los clientes.
// Uso: menú  Tools → The House Special → Añadir comentarios de clientes
// - Añade a cada cliente del prefab CustomerGroup un bocadillo de comentario (fondo blanco con texto)
//   y lo conecta con el Customer (commentBubble y commentLabel).
// - Añade el CustomerCommentManager a los días 1-5. El tutorial no lleva comentarios.
// - Crea el catálogo de tipos de cliente (Assets/Resources/Clientes.asset) si no existe.
// Se puede lanzar las veces que haga falta: el bocadillo se rehace y el manager no se duplica
// (si ya existe, se respetan sus frases y tiempos).
public static partial class DemoSceneBuilder
{
    private const string CommentBubbleName = "Comment";
    private const string CommentManagerName = "ComentariosClientes";
    private const string TraitsCatalogPath = "Assets/Resources/Clientes.asset";

    // Encima del aviso (¡PEDIR!, CARTA, CUENTA), que va a y = 2.0
    private static readonly Vector3 CommentBubblePosition = new Vector3(0f, 2.62f, 0f);
    private static readonly Vector2 CommentBubbleSize = new Vector2(2.5f, 0.66f);

    [UnityEditor.MenuItem("Tools/The House Special/Añadir comentarios de clientes")]
    public static void AddCustomerComments()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string prefabPath = PrefabsFolder + "/CustomerGroup.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            EditorUtility.DisplayDialog("Comentarios", "No encuentro " + prefabPath + ".", "Vale");
            return;
        }

        if (square == null) square = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesFolder + "/Cuadrado.png");
        if (spriteMaterial == null) LoadSpriteMaterial();

        // 1. Bocadillo en cada cliente del prefab
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        int bubbles = 0;
        foreach (Customer c in root.GetComponentsInChildren<Customer>(true))
        {
            AddCommentBubble(c.gameObject, c);
            bubbles++;
        }
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // 2. Catálogo de tipos de cliente
        bool catalogCreated = CreateTraitsCatalogIfMissing();

        // 3. Manager en cada día
        int days = 0;
        for (int day = 1; day <= DayCount; day++)
        {
            if (!Exists(DayScenePath(day))) continue;
            var scene = EditorSceneManager.OpenScene(DayScenePath(day));
            if (Object.FindAnyObjectByType<CustomerCommentManager>(FindObjectsInactive.Include) == null)
                new GameObject(CommentManagerName).AddComponent<CustomerCommentManager>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            days++;
        }

        EditorUtility.DisplayDialog("¡Listo!",
            $"Bocadillo de comentario añadido a {bubbles} clientes del prefab.\n" +
            $"Comentarios activados en {days} días (objeto '{CommentManagerName}').\n\n" +
            (catalogCreated ? "Creado el catálogo de clientes: " + TraitsCatalogPath + ".\n" : "") +
            "\nFrases generales y tiempos: objeto '" + CommentManagerName + "' de cada día.\n" +
            "Frases y comportamiento de cada tipo de cliente: " + TraitsCatalogPath + ".", "Vale");
    }

    [UnityEditor.MenuItem("Tools/The House Special/Crear catálogo de tipos de cliente")]
    public static void CreateTraitsCatalogMenu()
    {
        bool created = CreateTraitsCatalogIfMissing();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<CustomerTraitsCatalog>(TraitsCatalogPath);
        EditorUtility.DisplayDialog("Catálogo de clientes",
            (created ? "Creado " : "Ya existía ") + TraitsCatalogPath + ".\n\n" +
            "Ahí se edita lo que hace cada tipo de cliente (Pájaro azul, Hámster, Zorra). " +
            "Los tipos que no salen en la lista, como la Loba, son clientes base.", "Vale");
    }

    [UnityEditor.MenuItem("Tools/The House Special/Bajar mesas de arriba (comentarios visibles)")]
    public static void LowerTopTables()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scenes = new System.Collections.Generic.List<string>();
        for (int day = 1; day <= DayCount; day++) scenes.Add(DayScenePath(day));
        scenes.Add("Assets/Escenas/Tutorial.unity");

        int moved = 0;
        foreach (string path in scenes)
        {
            if (!Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path);
            // Las mesas de arriba son las que están por encima de la mitad de la sala;
            // los asientos y el punto de interacción son hijos de la mesa y bajan con ella
            foreach (Table t in Object.FindObjectsByType<Table>(FindObjectsInactive.Include))
            {
                Vector3 p = t.transform.position;
                if (p.y <= 0.5f || Mathf.Approximately(p.y, TopTablesY)) continue;
                t.transform.position = new Vector3(p.x, TopTablesY, p.z);
                moved++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (Exists(DayScenePath(3))) EditorSceneManager.OpenScene(DayScenePath(3));

        EditorUtility.DisplayDialog("¡Listo!",
            $"Mesas de arriba bajadas a y = {TopTablesY}: {moved} mesas en los días y el tutorial.\n\n" +
            "Así los comentarios de los clientes sentados ya no quedan debajo de la barra superior.", "Vale");
    }

    // Crea el catálogo con los valores por defecto (CustomerTraitsCatalog.Defaults). Si ya existe, no lo toca.
    private static bool CreateTraitsCatalogIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<CustomerTraitsCatalog>(TraitsCatalogPath) != null) return false;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CustomerTraitsCatalog>(), TraitsCatalogPath);
        AssetDatabase.SaveAssets();
        return true;
    }

    // Crea (o rehace) el bocadillo de comentario de un cliente: borde oscuro, fondo blanco, piquito y texto
    private static void AddCommentBubble(GameObject customerGo, Customer customer)
    {
        Transform old = customerGo.transform.Find(CommentBubbleName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var bubble = Obj(CommentBubbleName, customerGo.transform, CommentBubblePosition, Vector3.one);

        var border = Obj("Borde", bubble.transform, Vector3.zero,
            new Vector3(CommentBubbleSize.x + 0.06f, CommentBubbleSize.y + 0.06f, 1f));
        AddSprite(border, square, new Color(0.15f, 0.12f, 0.1f), 7);

        var bg = Obj("Bg", bubble.transform, Vector3.zero, new Vector3(CommentBubbleSize.x, CommentBubbleSize.y, 1f));
        AddSprite(bg, square, Color.white, 8);

        // Piquito apuntando al cliente (un cuadrado girado 45º, medio escondido debajo del fondo)
        var tail = Obj("Pico", bubble.transform, new Vector3(0f, -CommentBubbleSize.y / 2f, 0f), new Vector3(0.18f, 0.18f, 1f));
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        AddSprite(tail, square, Color.white, 8);

        var label = WorldText("Label", bubble.transform, Vector3.zero, "Comentario", 2.2f,
            new Color(0.15f, 0.12f, 0.1f), 9, CommentBubbleSize.x - 0.15f);
        label.rectTransform.sizeDelta = new Vector2(CommentBubbleSize.x - 0.15f, CommentBubbleSize.y - 0.08f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1.4f;
        label.fontSizeMax = 2.2f;
        TMP_FontAsset font = GetFontAsset(TpvFontRegular, TpvFontRegularAsset, "Outfit-Regular");
        if (font != null) label.font = font;

        bubble.SetActive(false);

        Set(customer, "commentBubble", bubble);
        Set(customer, "commentLabel", label);
    }
}
