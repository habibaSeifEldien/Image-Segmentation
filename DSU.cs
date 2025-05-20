using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageTemplate
{
    internal class DSU
    {
        List<int> id, sz;
        int n;
        public DSU(int n)
        {
            this.n = n;
            id = new List<int>(n);
            sz = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                id.Add(i);
                sz.Add(1);
            }
        }
        public int Find(int x)
        {
            return id[x] == x ? x : id[x] = Find(id[x]);
        }
        public void union(int u, int v)
        {
            u = Find(u);
            v = Find(v);
            if (sz[u] < sz[v])
            {
                id[u] = v;
                sz[v] += sz[u];
            }
            else
            {
                id[v] = u;
                sz[u] += sz[v];
            }
        }
        public bool same(int u, int v)
        {
            int fu = Find(u);
            int fv = Find(v);
            return fu == fv;
        }
        public int size(int x)
        {
            return sz[Find(x)];
        }
    }

}