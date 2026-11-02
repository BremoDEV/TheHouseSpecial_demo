using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Quinta parte del montador: COMENTARIOS ALEATORIOS de los clientes.
// Uso: menú  Tools → The House Special → Añadir comentarios de clientes
// - Añade a cada cliente del prefab CustomerGroup un bocadillo de comentario (fondo blanco con texto)
//   y lo conecta con el Customer (commentBubble y commentLabel).
// - Añade el CustomerCommentManager a los días 1-5. El tutorial no lleva comentarios.
// Se puede lanzar las veces que haga falta: el bocadillo se rehace y el manager no se duplica
// (si ya existe, se respetan sus frases y tiempos).
public static partial class DemoSceneBuilder
{
    private const string CommentBubbleName = "Comment";
    private const string CommentManagerName = "ComentariosClientes";

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

        // 2. Manager en cada día
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
            "Las frases y los tiempos se editan en ese objeto, en cada día.", "Vale");
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
