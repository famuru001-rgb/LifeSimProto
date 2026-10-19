using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 1つの階の床をマス目に分けて、壁や家具をよけて歩く道を探す（A*法）。
    /// 最初は全部「通れない」にしておき、部屋の中や出入口を Open で通れるようにする。
    /// </summary>
    public class GridNav
    {
        public readonly float cell;
        public readonly Vector2 min;
        public readonly int width, height;
        public readonly float floorY;
        readonly bool[] blocked;

        public GridNav(Vector2 min, Vector2 max, float cell, float floorY)
        {
            this.min = min;
            this.cell = cell;
            this.floorY = floorY;
            width = Mathf.CeilToInt((max.x - min.x) / cell);
            height = Mathf.CeilToInt((max.y - min.y) / cell);
            blocked = new bool[width * height];
            for (int i = 0; i < blocked.Length; i++) blocked[i] = true;
        }

        void SetRect(Rect r, bool value)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 p = ToWorld(x, y);
                    if (p.x >= r.xMin && p.x <= r.xMax && p.z >= r.yMin && p.z <= r.yMax)
                        blocked[y * width + x] = value;
                }
            }
        }

        /// <summary>x,z の範囲（Rect の x=X, y=Z）を通れるようにする。</summary>
        public void Open(Rect r) { SetRect(r, false); }

        /// <summary>x,z の範囲を通れなくする。</summary>
        public void Block(Rect r) { SetRect(r, true); }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }

        public bool IsFree(int x, int y) { return InBounds(x, y) && !blocked[y * width + x]; }

        public bool IsFree(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - min.x) / cell);
            int y = Mathf.FloorToInt((p.z - min.y) / cell);
            return IsFree(x, y);
        }

        public Vector2Int ToCell(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - min.x) / cell);
            int y = Mathf.FloorToInt((p.z - min.y) / cell);
            return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
        }

        public Vector3 ToWorld(int x, int y)
        {
            return new Vector3(min.x + (x + 0.5f) * cell, floorY, min.y + (y + 0.5f) * cell);
        }

        public Vector2Int NearestFree(Vector2Int c)
        {
            if (IsFree(c.x, c.y)) return c;
            for (int r = 1; r < 40; r++)
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
            if (IsFree(p)) return new Vector3(p.x, floorY, p.z);
            Vector2Int n = NearestFree(ToCell(p));
            return ToWorld(n.x, n.y);
        }

        /// <summary>指定した範囲の中から、通れるマスをランダムに1つ選ぶ。</summary>
        public Vector3 RandomFreePoint(Rect area)
        {
            for (int i = 0; i < 300; i++)
            {
                Vector3 p = new Vector3(Random.Range(area.xMin, area.xMax), floorY, Random.Range(area.yMin, area.yMax));
                if (IsFree(p)) return p;
            }
            return NearestFreePoint(new Vector3(area.center.x, floorY, area.center.y));
        }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        // A* 用の作業領域（毎回作り直さない）
        float[] gScore;
        int[] came;
        int[] stamp;
        bool[] closed;
        int search;

        /// <summary>道を探す。見つからなければ null。返す点の高さはこの階の床の高さ。</summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            Vector2Int s = NearestFree(ToCell(from));
            Vector2Int g = NearestFree(ToCell(to));
            bool exactGoal = IsFree(to);

            int n = width * height;
            if (gScore == null)
            {
                gScore = new float[n];
                came = new int[n];
                stamp = new int[n];
                closed = new bool[n];
            }
            search++;
            int si = s.y * width + s.x, gi = g.y * width + g.x;
            Touch(si);
            gScore[si] = 0f;
            var open = new MinHeap();
            open.Push(si, Heuristic(s.x, s.y, g.x, g.y));

            bool found = false;
            while (open.Count > 0)
            {
                int cur = open.Pop();
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
                    Touch(ni);
                    if (closed[ni]) continue;
                    float cost = gScore[cur] + (k >= 4 ? 1.4142f : 1f);
                    if (cost < gScore[ni])
                    {
                        gScore[ni] = cost;
                        came[ni] = cur;
                        open.Push(ni, cost + Heuristic(nx, ny, g.x, g.y));
                    }
                }
            }
            if (!found) return null;

            var cells = new List<Vector3>();
            int c = gi;
            int guard = 0;
            while (c != -1 && guard++ < n)
            {
                cells.Add(ToWorld(c % width, c / width));
                if (c == si) break;
                c = came[c];
            }
            cells.Reverse();
            if (exactGoal && cells.Count > 0) cells[cells.Count - 1] = new Vector3(to.x, floorY, to.z);

            // 見通しのよい所は直線にまとめる
            var smooth = new List<Vector3>();
            Vector3 anchor = new Vector3(from.x, floorY, from.z);
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

        void Touch(int i)
        {
            if (stamp[i] == search) return;
            stamp[i] = search;
            gScore[i] = float.MaxValue;
            came[i] = -1;
            closed[i] = false;
        }

        static float Heuristic(int ax, int ay, int bx, int by)
        {
            int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
            return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
        }

        public bool LineFree(Vector3 a, Vector3 b)
        {
            float dist = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            int steps = Mathf.CeilToInt(dist / (cell * 0.4f));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)steps);
                if (!IsFree(p)) return false;
            }
            return true;
        }

        /// <summary>小さい順に取り出せる入れ物（A* の高速化用）。</summary>
        class MinHeap
        {
            readonly List<int> items = new List<int>();
            readonly List<float> keys = new List<float>();
            public int Count { get { return items.Count; } }

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

            void Swap(int a, int b)
            {
                int ti = items[a]; items[a] = items[b]; items[b] = ti;
                float tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
            }
        }
    }
}
