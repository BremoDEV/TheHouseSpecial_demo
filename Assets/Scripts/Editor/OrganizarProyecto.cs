using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Ordena la carpeta Assets por tipo de cosa (escenas, prefabs, datos, scripts por sistemas...).
// Uso: menú  Tools → The House Special → Ordenar: carpetas del proyecto
// Mueve los archivos CON UNITY, así no se rompe ninguna referencia (escenas, prefabs, scripts).
// Se puede lanzar más de una vez: lo que ya está en su sitio se salta.
public static class OrganizarProyecto
{
    // Scripts del juego → subcarpeta de Assets/Scripts
    private static readonly Dictionary<string, string> ScriptFolders = new Dictionary<string, string>
    {
        // La camarera
        { "WaitressController", "Camarera" }, { "WaitressHands", "Camarera" },
        // Los clientes
        { "Customer", "Clientes" }, { "CustomerGroup", "Clientes" }, { "CustomerQueue", "Clientes" },
        { "CustomerSpawner", "Clientes" }, { "OrderBubbleDistortion", "Clientes" },
        // El restaurante: cosas con las que se interactúa
        { "Interactable", "Restaurante" }, { "Table", "Restaurante" }, { "Kitchen", "Restaurante" },
        { "CounterItem", "Restaurante" }, { "DishReturn", "Restaurante" }, { "ItemSource", "Restaurante" },
        { "Tpv", "Restaurante" }, { "Storeroom", "Restaurante" }, { "StoreroomManager", "Restaurante" },
        { "CookWander", "Restaurante" }, { "MenuItem", "Restaurante" },
        // Reglas generales de la partida
        { "ClickManager", "Juego" }, { "DayManager", "Juego" }, { "GameTime", "Juego" },
        { "MoneyManager", "Juego" }, { "RageMeter", "Juego" }, { "GameMessages", "Juego" },
        { "WalkGrid", "Juego" }, { "NavObstacle", "Juego" },
        // Interfaz
        { "TpvPanel", "UI" }, { "PauseMenu", "UI" }, { "MainMenu", "UI" }, { "TicketButton", "UI" },
        // Tutorial
        { "TutorialManager", "Tutorial" },
    };

    private static readonly string[] FontFiles =
    {
        "NothingYouCouldDo-Regular.ttf", "NothingYouCouldDo SDF.asset", "NothingYouCouldDo-OFL.txt",
        "Outfit-Bold.ttf", "Outfit-Bold SDF.asset", "Outfit-Regular.ttf", "Outfit-Regular SDF.asset", "Outfit-OFL.txt",
    };

    private static int moved;
    private static readonly List<string> problems = new List<string>();

    [UnityEditor.MenuItem("Tools/The House Special/Ordenar: carpetas del proyecto")]
    public static void Organize()
    {
        if (!EditorUtility.DisplayDialog("Ordenar el proyecto",
                "Se van a mover las carpetas a este orden:\n\n" +
                "Escenas/  (Menú, Tutorial y Demo_Restaurante)\n" +
                "Prefabs/\n" +
                "Datos/Productos/  (los productos de la carta)\n" +
                "Scripts/  Camarera, Clientes, Restaurante, Juego, UI, Tutorial, Editor\n" +
                "Arte/Fuentes/  y  Arte/Generados/  (formas básicas y recortes automáticos)\n\n" +
                "La carpeta Demo desaparece y la escena de ejemplo vacía de Unity (Scenes/SampleScene) va a la papelera.\n" +
                "No se rompe nada: Unity actualiza las referencias solo. ¿Seguimos?", "Ordenar", "Cancelar"))
            return;

        UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        moved = 0;
        problems.Clear();

        {
            // Escenas
            Move("Assets/Demo/Demo_Restaurante.unity", "Assets/Escenas/Demo_Restaurante.unity");
            // Prefabs, productos y sprites generados
            MoveFolderContents("Assets/Demo/Prefabs", "Assets/Prefabs");
            MoveFolderContents("Assets/Demo/Data", "Assets/Datos/Productos");
            MoveFolderContents("Assets/Demo/Sprites", "Assets/Arte/Generados");
            Move("Assets/Escenas/Flecha.png", "Assets/Arte/Generados/Flecha.png");
            // Fuentes
            foreach (string f in FontFiles) Move("Assets/Arte/Menu/" + f, "Assets/Arte/Fuentes/" + f);
            // Scripts por sistemas
            foreach (var pair in ScriptFolders)
                Move("Assets/Scripts/" + pair.Key + ".cs", "Assets/Scripts/" + pair.Value + "/" + pair.Key + ".cs");
        }

        // Carpetas que se quedan vacías, y la escena vacía de ejemplo de Unity (a la papelera, se puede recuperar)
        DeleteIfEmpty("Assets/Demo/Prefabs");
        DeleteIfEmpty("Assets/Demo/Data");
        DeleteIfEmpty("Assets/Demo/Sprites");
        DeleteIfEmpty("Assets/Demo");
        if (AssetImporter.GetAtPath("Assets/Scenes/SampleScene.unity") != null)
            AssetDatabase.MoveAssetToTrash("Assets/Scenes/SampleScene.unity");
        DeleteIfEmpty("Assets/Scenes");

        // Orden de las escenas en la build: Menú → Tutorial → Demo
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string s in new[] { "Assets/Escenas/MenuPrincipal.unity", "Assets/Escenas/Tutorial.unity", "Assets/Escenas/Demo_Restaurante.unity" })
            if (AssetImporter.GetAtPath(s) != null) scenes.Add(new EditorBuildSettingsScene(s, true));
        if (scenes.Count > 0) EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("¡Ordenado!",
            $"Movidos {moved} archivos." +
            (problems.Count > 0 ? "\n\nProblemas:\n" + string.Join("\n", problems) : "") +
            "\n\nLa escena del restaurante ahora está en Assets/Escenas/Demo_Restaurante.", "Vale");
    }

    private static void Move(string from, string to)
    {
        if (AssetImporter.GetAtPath(from) == null && !AssetDatabase.IsValidFolder(from)) return; // ya movido
        if (AssetImporter.GetAtPath(to) != null) { problems.Add("Ya existe " + to); return; }
        EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
        string error = AssetDatabase.MoveAsset(from, to);
        if (string.IsNullOrEmpty(error)) moved++;
        else problems.Add(Path.GetFileName(from) + ": " + error);
    }

    private static void MoveFolderContents(string fromFolder, string toFolder)
    {
        if (!AssetDatabase.IsValidFolder(fromFolder)) return;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { fromFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)) continue;
            if (Path.GetDirectoryName(path).Replace('\\', '/') != fromFolder) continue; // solo lo de primer nivel
            Move(path, toFolder + "/" + Path.GetFileName(path));
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void DeleteIfEmpty(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;
        if (AssetDatabase.FindAssets("", new[] { folder }).Length == 0) AssetDatabase.DeleteAsset(folder);
    }
}
