using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Hace la build para pasar el juego a otra gente, sin tener que elegir carpetas.
// Uso: menú  Tools → The House Special → Build: Windows
// Sale en la carpeta Builds/Windows del proyecto (al lado de Assets). Para pasarla, comprime esa carpeta ENTERA en un .zip.
public static class BuildHelper
{
    private const string ExeName = "TheHouseSpecial.exe";

    [UnityEditor.MenuItem("Tools/The House Special/Build: Windows")]
    public static void BuildWindows()
    {
        // Las escenas marcadas en Build Profiles, en su orden (Menú → Tutorial → Demo)
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build", "No hay escenas en Build Profiles. Lanza antes 'Montar tutorial y menú principal'.", "Vale");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, "Builds", "Windows");
        if (Directory.Exists(folder)) Directory.Delete(folder, true); // build limpia, sin restos de la anterior
        Directory.CreateDirectory(folder);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(folder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("¡Build lista!",
                $"Hecha en {folder}\n({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s)\n\n" +
                "Escenas: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)) + "\n\n" +
                "Para pasarla: comprime la carpeta Windows ENTERA en un .zip. " +
                "Tus amigos la descomprimen y abren " + ExeName + ".", "Vale");
            EditorUtility.RevealInFinder(Path.Combine(folder, ExeName));
        }
        else
        {
            EditorUtility.DisplayDialog("Build", $"La build ha fallado ({summary.result}, {summary.totalErrors} errores). Mira la consola.", "Vale");
        }
    }
}
