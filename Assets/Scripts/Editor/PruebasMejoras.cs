using System.Reflection;
using UnityEditor;
using UnityEngine;

// Atajos para probar las mejoras sin pasar por la tienda. Solo funcionan en Play.
public static class PruebasMejoras
{
    [UnityEditor.MenuItem("Tools/The House Special/Pruebas: dar TPV portátil (en Play)")]
    public static void GivePortablePos()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Pruebas", "Dale primero a Play y luego vuelve a usar esta opción.", "Vale");
            return;
        }

        // RunState guarda los niveles en un array privado; lo tocamos solo para probar
        FieldInfo field = typeof(RunState).GetField("levels", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null && field.GetValue(null) is int[] levels)
            levels[(int)UpgradeId.TpvPortatil] = Mathf.Max(levels[(int)UpgradeId.TpvPortatil], 1);

        PortablePosButton button = Object.FindAnyObjectByType<PortablePosButton>(FindObjectsInactive.Include);
        if (button != null) button.Refresh();
        Debug.Log(button != null
            ? "TPV portátil activada para esta partida (botón del HUD y barra espaciadora)."
            : "TPV portátil activada, pero esta escena no tiene el botón del HUD (Montar tienda de mejoras).");
    }
}
