using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 床をマス目に分けて、家具をよけて歩く道を探す（A*法）。
    /// </summary>
    public class GridNav
    {
        public readonly float cell;
        public readonly Vector2 min;
        public readonly int width, height;
        readonly bool[] blocked;

        public GridNav(Vector2 min, Vector2 max, float cell)
        {
            this.min = min;
            this.cell = cell;
            width = Mathf.CeilToInt((max.x - min.x) / cell);
            height = Mathf.CeilToInt((max.y - min.y) / cell);
            blocked = new bool[width * height];
        }

        /// <summary>x,z の範囲（Rect の x=X, y=Z）を通れなくする。</summary>
        public void Block(Rect r)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 p = ToWorld(x, y);
                    if (p.x >= r.xMin && p.x <= r.xMax && p.z >= r.yMin && p.z <= r.yMax)
                        blocked[y * width + x] = true;
                }
            }
        }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }

        public bool IsFree(int x, int y) { return InBounds(x, y) && !blocked[y * width + x]; }

        public bool IsFree(Vector3 p) { Vector2Int c = ToCell(p); return IsFree(c.x, c.y); }

        public Vector2Int ToCell(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - min.x) / cell);
            int y = Mathf.FloorToInt((p.z - min.y) / cell);
            return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
        }

        public Vector3 ToWorld(int x, int y)
        {
            return new Vector3(min.x + (x + 0.5f) * cell, 0f, min.y + (y + 0.5f) * cell);
        }

        public Vector2Int NearestFree(Vector2Int c)
        {
            if (IsFree(c.x, c.y)) return c;
            for (int r = 1; r < 30; r++)
            {
                Vector2Int best = new Vector2Int(-1, -1);
                float bestD = float.MaxValue;
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!IsFree(x, y)) continue;
                        float d = dx * dx + dy * dy;
                        if (d < bestD) { bestD = d; best = new Vector2Int(x, y); }
                    }
                }
                if (best.x >= 0) return best;
            }
            return c;
        }

        public Vector3 NearestFreePoint(Vector3 p)
        {
            Vector2Int c = ToCell(p);
            if (IsFree(c.x, c.y)) return new Vector3(p.x, 0f, p.z);
            Vector2Int n = NearestFree(c);
            return ToWorld(n.x, n.y);
        }

        public Vector3 RandomFreePoint()
        {
            for (int i = 0; i < 200; i++)
            {
                int x = Random.Range(0, width);
                int y = Random.Range(0, height);
                if (IsFree(x, y)) return ToWorld(x, y);
            }
            return ToWorld(width / 2, height / 2);
        }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>道を探す。見つからなければ null。</summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            Vector2Int s = NearestFree(ToCell(from));
            Vector2Int g = NearestFree(ToCell(to));
            bool exactGoal = IsFree(ToCell(to).x, ToCell(to).y);

            int n = width * height;
            var gScore = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { gScore[i] = float.MaxValue; came[i] = -1; }
            int si = s.y * width + s.x, gi = g.y * width + g.x;
            gScore[si] = 0f;
            var open = new List<int> { si };
            var fScore = new Dictionary<int, float> { { si, Heuristic(s, g) } };

            bool found = false;
            while (open.Count > 0)
            {
                int bestIdx = 0;
                float bestF = fScore[open[0]];
                for (int i = 1; i < open.Count; i++)
                {
                    float f = fScore[open[i]];
                    if (f < bestF) { bestF = f; bestIdx = i; }
                }
                int cur = open[bestIdx];
                open.RemoveAt(bestIdx);
                if (cur == gi) { found = true; break; }
                if (closed[cur]) continue;
                closed[cur] = true;
                int cx = cur % width, cy = cur / width;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (!IsFree(nx, ny)) continue;
                    if (k >= 4 && (!IsFree(cx + DX[k], cy) || !IsFree(cx, cy + DY[k]))) continue; // 角をすり抜けない
                    int ni = ny * width + nx;
                    if (closed[ni]) continue;
                    float cost = gScore[cur] + (k >= 4 ? 1.4142f : 1f);
                    if (cost < gScore[ni])
                    {
                        gScore[ni] = cost;
                        came[ni] = cur;
                        fScore[ni] = cost + Heuristic(new Vector2Int(nx, ny), g);
                        open.Add(ni);
                    }
                }
            }
            if (!found) return null;

            var cells = new List<Vector3>();
            int c = gi;
            while (c != -1)
            {
                cells.Add(ToWorld(c % width, c / width));
                if (c == si) break;
                c = came[c];
            }
            cells.Reverse();
            if (exactGoal && cells.Count > 0) cells[cells.Count - 1] = new Vector3(to.x, 0f, to.z);

            // 見通しのよい所は直線にまとめる
            var smooth = new List<Vector3>();
            Vector3 anchor = new Vector3(from.x, 0f, from.z);
            int idx = 0;
            while (idx < cells.Count)
            {
                int far = idx;
                for (int j = cells.Count - 1; j > idx; j--)
                {
                    if (LineFree(anchor, cells[j])) { far = j; break; }
                }
                smooth.Add(cells[far]);
                anchor = cells[far];
                idx = far + 1;
            }
            return smooth;
        }

        float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
        }

        public bool LineFree(Vector3 a, Vector3 b)
        {
            float dist = Vector3.Distance(a, b);
            int steps = Mathf.CeilToInt(dist / (cell * 0.4f));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)steps);
                if (!IsFree(p)) return false;
            }
            return true;
        }
    }
}
