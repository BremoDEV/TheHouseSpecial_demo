using UnityEngine;

// Reloj del juego que se puede "congelar".
// Cuando la camarera se lleva a un cliente al trastero, Frozen = true:
// los clientes, la cocina, la barra y el reloj del día se paran,
// pero la camarera (y el cliente que se lleva) siguen moviéndose.
//
// Regla para programar: en todo lo que deba pararse en ese momento,
// usa GameTime.Delta en vez de Time.deltaTime.
// (El menú de pausa es distinto: pone Time.timeScale = 0 y lo para TODO.)
public static class GameTime
{
    public static bool Frozen { get; set; }

    public static float Delta => Frozen ? 0f : Time.deltaTime;
}
