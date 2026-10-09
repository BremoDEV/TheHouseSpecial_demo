using System.Collections.Generic;
using UnityEngine;

// Rejilla de casillas sobre el suelo para que los personajes rodeen los obstáculos.
// Al empezar la partida mira qué casillas tocan un obstáculo (objetos con NavObstacle)
// y las marca como bloqueadas. Después, FindPath busca el camino más corto con A*
// y lo "suaviza" para que no vaya en zigzag de casilla en casilla.
// Lo que quede fuera de la zona caminable (las paredes) nunca se puede pisar.
public class WalkGrid : MonoBehaviour
{
    public static WalkGrid Instance { get; private set; }

    [Header("Zona caminable (el suelo sin las paredes)")]
    [SerializeField] private Vector2 areaMin = new Vector2(-10.2f, -5.7f);
    [SerializeField] private Vector2 areaMax = new Vector2(10.2f, 5.7f);

    [Header("Precisión (editable)")]
    [Tooltip("Tamaño de cada casilla. Más pequeño = más preciso pero más lento")]
    [SerializeField] private float cellSize = 0.25f;
    [Tooltip("Cuánto se separan los personajes de los obstáculos")]
    [SerializeField] private float agentRadius = 0.2f;

    private int width, height;
    private bool[] blocked;

    private void Awake()
    {
        Instance = this;
        Build();
    }

    // ------------------------------------------------------------- CONSTRUIR LA REJILLA

    public void Build()
    {
        width = Mathf.Max(1, Mathf.CeilToInt((areaMax.x - areaMin.x) / cellSize));
        height = Mathf.Max(1, Mathf.CeilToInt((areaMax.y - areaMin.y) / cellSize));
        blocked = new bool[width * height];

        Physics2D.SyncTransforms();
        var colliders = new List<Collider2D>();
        foreach (NavObstacle o in FindObjectsByType<NavObstacle>())
            colliders.AddRange(o.GetComponentsInChildren<Collider2D>());

        float r2 = agentRadius * agentRadius;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 c = CellCenter(x, y);
                foreach (Collider2D col in colliders)
                {
                    if (col == null || !col.enabled) continue;
                    // ClosestPoint devuelve el propio punto si está dentro del collider
                    if ((col.ClosestPoint(c) - c).sqrMagnitude <= r2)
                    {
                        blocked[Index(x, y)] = true;
                        break;
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------- BUSCAR CAMINO

    // Devuelve los puntos por los que pasar para ir de 'from' a 'to' (sin incluir 'from').
    // exactTarget = true: el último punto es exactamente 'to' (puntos de interacción, asientos).
    // exactTarget = false: si 'to' cae en un obstáculo o fuera del suelo, se va a la casilla libre más cercana.
    public List<Vector2> FindPath(Vector2 from, Vector2 to, bool exactTarget)
    {
        var result = new List<Vector2>();

        int start = NearestFree(WorldToCell(from));
        int goalRaw = WorldToCell(to);
        int goal = NearestFree(goalRaw);

        bool targetIsFree = Inside(to) && !blocked[goalRaw];
        Vector2 finalPoint = (exactTarget || targetIsFree) ? to : CellCenterIndex(goal);

        if (start < 0 || goal < 0)
        {
            result.Add(finalPoint);
            return result;
        }

        List<Vector2> cells = AStar(start, goal);
        if (cells == null)
        {
            result.Add(finalPoint); // no hay camino: va en línea recta
            return result;
        }

        // Suavizado: desde cada punto, saltar al más lejano que se vea en línea recta
        cells.Add(finalPoint);
        Vector2 current = from;
        int i = 0;
        while (i < cells.Count)
        {
            int farthest = i;
            for (int j = cells.Count - 1; j > i; j--)
            {
                if (LineIsFree(current, cells[j])) { farthest = j; break; }
            }
            current = cells[farthest];
            result.Add(current);
            i = farthest + 1;
        }
        return result;
    }

    private List<Vector2> AStar(int start, int goal)
    {
        int n = width * height;
        var gCost = new float[n];
        var parent = new int[n];
        var closed = new bool[n];
        for (int k = 0; k < n; k++) { gCost[k] = float.MaxValue; parent[k] = -1; }

        var open = new MinHeap();
        gCost[start] = 0f;
        open.Push(start, Heuristic(start, goal));

        while (open.Count > 0)
        {
            int current = open.Pop();
            if (current == goal) return Reconstruct(parent, goal);
            if (closed[current]) continue;
            closed[current] = true;

            int cx = current % width, cy = current / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int ni = Index(nx, ny);
                    if (blocked[ni] || closed[ni]) continue;
                    // En diagonal, no cortar esquinas de obstáculos
                    if (dx != 0 && dy != 0 && (blocked[Index(cx + dx, cy)] || blocked[Index(cx, cy + dy)])) continue;

                    float cost = gCost[current] + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    if (cost < gCost[ni])
                    {
                        gCost[ni] = cost;
                        parent[ni] = current;
                        open.Push(ni, cost + Heuristic(ni, goal));
                    }
                }
            }
        }
        return null;
    }

    private List<Vector2> Reconstruct(int[] parent, int goal)
    {
        var cells = new List<Vector2>();
        for (int c = goal; c != -1; c = parent[c]) cells.Add(CellCenterIndex(c));
        cells.Reverse();
        return cells;
    }

    private float Heuristic(int a, int b)
    {
        float dx = Mathf.Abs(a % width - b % width), dy = Mathf.Abs(a / width - b / width);
        return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
    }

    // ¿Se puede ir en línea recta de a a b sin pisar casillas bloqueadas?
    private bool LineIsFree(Vector2 a, Vector2 b)
    {
        float dist = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / (cellSize * 0.5f)));
        for (int s = 1; s < steps; s++)
        {
            Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
            if (!Inside(p)) return false;
            if (blocked[WorldToCell(p)]) return false;
        }
        return true;
    }

    // Casilla libre más cercana (búsqueda en anchura). -1 si no hay ninguna.
    private int NearestFree(int cell)
    {
        if (!blocked[cell]) return cell;
        var visited = new bool[width * height];
        var queue = new Queue<int>();
        queue.Enqueue(cell);
        visited[cell] = true;
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            if (!blocked[c]) return c;
            int cx = c % width, cy = c / width;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0);
                int ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int ni = Index(nx, ny);
                if (visited[ni]) continue;
                visited[ni] = true;
                queue.Enqueue(ni);
            }
        }
        return -1;
    }

    // ------------------------------------------------------------- UTILIDADES

    private int Index(int x, int y) => y * width + x;

    private bool Inside(Vector2 p) =>
        p.x >= areaMin.x && p.x <= areaMax.x && p.y >= areaMin.y && p.y <= areaMax.y;

    private int WorldToCell(Vector2 p)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((p.x - areaMin.x) / cellSize), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((p.y - areaMin.y) / cellSize), 0, height - 1);
        return Index(x, y);
    }

    private Vector2 CellCenter(int x, int y) =>
        new Vector2(areaMin.x + (x + 0.5f) * cellSize, areaMin.y + (y + 0.5f) * cellSize);

    private Vector2 CellCenterIndex(int i) => CellCenter(i % width, i / width);

    // Ver la rejilla en la vista Scene durante el Play: rojo = bloqueado
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 size = areaMax - areaMin;
        Gizmos.DrawWireCube((Vector3)(areaMin + size * 0.5f), (Vector3)size);
        if (blocked == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        for (int i = 0; i < blocked.Length; i++)
            if (blocked[i]) Gizmos.DrawCube((Vector3)CellCenterIndex(i), Vector3.one * cellSize * 0.9f);
    }

    // Montículo mínimo sencillo para A* (saca siempre la casilla con menor coste)
    private class MinHeap
    {
        private readonly List<int> items = new List<int>();
        private readonly List<float> keys = new List<float>();
        public int Count => items.Count;

        public void Push(int item, float key)
        {
            items.Add(item);
            keys.Add(key);
            int i = items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (keys[p] <= keys[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        public int Pop()
        {
            int top = items[0];
            int last = items.Count - 1;
            items[0] = items[last];
            keys[0] = keys[last];
            items.RemoveAt(last);
            keys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < items.Count && keys[l] < keys[m]) m = l;
                if (r < items.Count && keys[r] < keys[m]) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }
}
