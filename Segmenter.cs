using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml;

namespace ImageTemplate
{

    public class Segmenter
    {
        public RGBPixel[,] ImageMatrix, output;
        private int n, m;
        bool[,] vis;
        int[] di = { 1, 1, 1, 0, 0, -1, -1, -1 };
        int[] dj = { 1, 0, -1, 1, -1, 1, 0, -1 };
        public void preProcessing(RGBPixel[,] imageMatrix)
        {
            if (imageMatrix == null)
                throw new ArgumentNullException("Image matrix cannot be null");

            this.ImageMatrix = imageMatrix;
            n = ImageOperations.GetHeight(ImageMatrix);
            m = ImageOperations.GetWidth(ImageMatrix);
            vis = new bool[n, m];
            for(int i=0;i<n;i++)
            {
                for(int j=0;j<m;j++)
                {
                    vis[i, j] = false;
                }
            }
        }

        public RGBPixel[,] segmentImage(int k, string outputFolder)
        {
            byte[,] red = ExtractChannel(ImageMatrix, 'r');
            byte[,] green = ExtractChannel(ImageMatrix, 'g');
            byte[,] blue = ExtractChannel(ImageMatrix, 'b');

            int[,] labelsR = SegmentChannel(red, k);
            int[,] labelsG = SegmentChannel(green, k);
            int[,] labelsB = SegmentChannel(blue, k);

            int[,] finalLabels = IntersectRGBLabels(labelsR, labelsG, labelsB);
             output = AssignColors(finalLabels);
            SaveSegmentInfoToFile(finalLabels, Path.Combine(outputFolder, "segments.txt"));

            return output;
        }

        private byte[,] ExtractChannel(RGBPixel[,] imageMatrix, char color)
        {
            byte[,] channel = new byte[n, m];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                {
                    switch (color)
                    {
                        case 'r': channel[i, j] = imageMatrix[i, j].red; break;
                        case 'g': channel[i, j] = imageMatrix[i, j].green; break;
                        case 'b': channel[i, j] = imageMatrix[i, j].blue; break;
                    }
                }
            return channel;
        }

        private int[,] SegmentChannel(byte[,] channel, int k)
        {
            DSU dsu = new DSU(n * m);
            List<Edge> edges = new List<Edge>();
            ConstructGraph(channel, ref edges);

            int[] internalIntensity = new int[n * m];

            for (int i = 0; i < edges.Count; i++)
            {
                int u = edges[i].node1;
                int v = edges[i].node2;

                int rootU = dsu.Find(u);
                int rootV = dsu.Find(v);

                double tauU = Tau(dsu.size(rootU), k);
                double tauV = Tau(dsu.size(rootV), k);

                double mintU = internalIntensity[rootU] + tauU;
                double mintV = internalIntensity[rootV] + tauV;

                if (rootU != rootV)
                {
                    if (edges[i].weight <= Math.Min(mintU, mintV))
                    {
                        dsu.union(rootU, rootV);
                        int newRoot = dsu.Find(rootU);
                        internalIntensity[newRoot] = Math.Max(Math.Max(internalIntensity[rootU], internalIntensity[rootV]), edges[i].weight);
                    }
                }
            }

            int[,] labels = new int[n, m];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    labels[i, j] = dsu.Find(i * m + j);

            return labels;
        }

        private double Tau(int size, int k)
        {
            return k / (double)size;
        }

        //private int[,] IntersectRGBLabels(int[,] labelsR, int[,] labelsG, int[,] labelsB)
        //{
        //    int[,] finalLabels = new int[n, m];
        //    Dictionary<(int, int, int), int> combine = new Dictionary<(int, int, int), int>();
        //    int nextId = 1;
        //    for (int i = 0; i < n; i++)
        //        for (int j = 0; j < m; j++)
        //        { var key = (labelsR[i, j], labelsG[i, j], labelsB[i, j]);
        //            if (!combine.ContainsKey(key))
        //                combine[key] = nextId++;
        //            finalLabels[i, j] = combine[key];
        //        }
        //    return finalLabels;
        //}


        //int[,] IntersectRGBLabels(int[,] labelsR, int[,] labelsG, int[,] labelsB)
        //{
        //    int id = 0;
        //    int[,] rt = new int[n, m];
        //    Queue<(int, int)> q = new Queue<(int, int)>();

        //    for (int l = 0; l < n; l++)
        //    {
        //        for (int h = 0; h < m; h++)
        //        {
        //            if (vis[l,h]) continue;
        //            q.Enqueue((l, h));
        //            vis[l, h] = true;
        //            while (q.Count > 0)
        //            {
        //                var node = q.Dequeue();
        //                rt[node.Item1, node.Item2] = id ;
        //                for (int i = 0; i < 8; i++)
        //                {
        //                    int nx = node.Item1 + di[i];
        //                    int ny = node.Item2 + dj[i];

        //                    if (Valid(nx, ny)&& !vis[nx,ny]&& labelsR[nx, ny] == labelsR[node.Item1, node.Item2] &&
        //                            labelsG[nx, ny] == labelsG[node.Item1, node.Item2] &&
        //                            labelsB[nx, ny] == labelsB[node.Item1, node.Item2])
        //                    {
        //                        vis[nx, ny] = true;
        //                        q.Enqueue((nx, ny));
        //                    }
        //                }
        //            }
        //            id++;


        //        }
        //    }
        //    return rt;
        //}
        private int[,] IntersectRGBLabels(int[,] labelsR, int[,] labelsG, int[,] labelsB)
        {
            int[,] finalLabels = new int[n, m];
            DSU dsu = new DSU(n * m);
            int[] dx = { 0, 1, 1, 1 };
            int[] dy = { 1, 1, 0, -1 };
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = i + dx[k];
                        int ny = j + dy[k];
                        if (Valid(nx, ny) && labelsR[nx, ny] == labelsR[i, j] &&
                                    labelsG[nx, ny] == labelsG[i, j] &&
                                    labelsB[nx, ny] == labelsB[i, j]) dsu.union(i * m + j, nx * m + ny);
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    finalLabels[i, j] = dsu.Find(i * m + j);
                }
            }
            return finalLabels;

        }
        public RGBPixel[,] AssignColors(int[,] finalLabels)
        {
            int height = finalLabels.GetLength(0);
            int width = finalLabels.GetLength(1);
            RGBPixel[,] output = new RGBPixel[height, width];
            Random rand = new Random();
            Dictionary<int, RGBPixel> colorsSegment = new Dictionary<int, RGBPixel>();

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    int label = finalLabels[i, j];
                    if (!colorsSegment.ContainsKey(label))
                    {
                        colorsSegment[label] = new RGBPixel
                        {
                            red = (byte)rand.Next(0, 256),
                            green = (byte)rand.Next(0, 256),
                            blue = (byte)rand.Next(0, 256)
                        };
                    }
                    output[i, j] = colorsSegment[label];
                }

            return output;
        }

        private bool Valid(int x, int y)
        {
            return x >= 0 && x < n && y >= 0 && y < m;
        }

        private void ConstructGraph(byte[,] channel, ref List<Edge> edges)
        {
           

            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    for (int k = 0; k < 8; k++)
                    {
                        int ni = i + di[k];
                        int nj = j + dj[k];

                        if (Valid(ni, nj))
                        {
                            if (i < ni || (i == ni && j < nj))
                            {
                                int n1 = i * m + j;
                                int n2 = ni * m + nj;
                                edges.Add(new Edge(n1, n2, CalculateWeight(channel[i, j], channel[ni, nj])));
                            }
                        }
                    }

            SortEdgesByWeight(edges);
        }

        void SortEdgesByWeight(List<Edge> edges)
        {
            List<Edge>[] buckets = new List<Edge>[256];
            for (int i = 0; i < 256; i++)
                buckets[i] = new List<Edge>();

            foreach (var edge in edges)
                buckets[edge.weight].Add(edge);

            edges.Clear();
            for (int i = 0; i < 256; i++)
                edges.AddRange(buckets[i]);
        }

        private int CalculateWeight(byte pixel1, byte pixel2)
        {
            return Math.Abs(pixel1 - pixel2);
        }

        public void SaveSegmentInfoToFile(int[,] finalLabels, string filePath)
        {
            Dictionary<int, int> segmentSizes = new Dictionary<int, int>();

            int height = finalLabels.GetLength(0);
            int width = finalLabels.GetLength(1);

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    int label = finalLabels[i, j];
                    if (!segmentSizes.ContainsKey(label))
                        segmentSizes[label] = 0;
                        segmentSizes[label]++;
                }
            var sortedSegments = segmentSizes.OrderByDescending(pair => pair.Value).ToList();
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine($"Number of segments: {sortedSegments.Count}");
                foreach (var segment in sortedSegments)
                {
                    writer.WriteLine(segment.Value);
                }
            }
        }
    }
}