using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CSG
{
    [System.Serializable]
    public class BSPTree
    {
        // Struct to create later a new vertex
        private struct NewVertex
        {
            int index0;
            int index1;
            float delta01;

            public NewVertex(int inIndex0, int inIndex1, float inDelta01)
            {
                index0 = inIndex0;
                index1 = inIndex1;
                delta01 = inDelta01;
            }

            public T Value<T>(IEnumerable<T> ts)
            {
                return Value(ts.ElementAt(index0), ts.ElementAt(index1));
            }

            public T Value<T>(T v0, T v1)
            {
                var dv0 = v0 as dynamic;
                var dv1 = v1 as dynamic;
                return dv0 * (1 - delta01) + dv1 * delta01;
            }
        }

        Mesh originalMesh;
        BSPNode rootNode;
        List<Material> materials;

        public List<Material> Materials => materials;
        public BSPNode RootNode => rootNode;
        public int Depth => rootNode.Depth;

        public BSPTree(Mesh inMesh, List<Material> inMaterials, int maxTrianglesInLeaves = 1)
        {
            originalMesh = inMesh;
            materials = inMaterials;

            // retrieve indices by submesh index
            List<List<int>> subMeshIndices = new List<List<int>>();
            for (int i = 0; i < inMesh.subMeshCount; i++)
            {
                Debug.Assert(inMesh.GetTopology(i) == MeshTopology.Triangles, "Only triangle topology is supported by the BSP tree currently.");
                subMeshIndices.Add(inMesh.GetTriangles(i).ToList());
            }

            rootNode = new BSPNode(subMeshIndices);

            GenerateBSPTree(rootNode, maxTrianglesInLeaves);
        }

        public Mesh ComputeMesh()
        {
            List<List<int>> indices = rootNode.GetAllIndices();

            Mesh mesh = new Mesh();
            mesh.vertices = originalMesh.vertices;
            mesh.normals = originalMesh.normals;
            mesh.tangents = originalMesh.tangents;
            mesh.uv = originalMesh.uv;
            for (int i = 0; i < indices.Count; i++)
                mesh.SetTriangles(indices[i], i);

            return mesh;
        }

        public override string ToString()
        {
            return "Depth: " + Depth + "\n" + rootNode.ToString();
        }

        // -- UTILS' METHODS FOR THE GENERATION OF A BSPTREE

        private void GenerateBSPTree(BSPNode node, int maxTrianglesInLeaves)
        {
            if (node == null || node.TrianglesCount <= maxTrianglesInLeaves)
            {
                return; // return without splitting, it's a leaf
            }

            // Split the node
            Split(node);

            // recurse
            GenerateBSPTree(node.Plus, maxTrianglesInLeaves);
            GenerateBSPTree(node.Minus, maxTrianglesInLeaves);
        }

        private void Split(BSPNode node)
        {
            if (node == null)
                return;

            Plane plane = ChooseSplittingPlane(node);
            Split(node, plane, out var zeros, out var plus, out var minus, out var newVertices);

            node.Plane = plane;
            var nPlus = new BSPNode(plus);
            node.Plus = (nPlus.IndicesCount > 0)? nPlus : null;
            var nMinus = new BSPNode(minus);
            node.Minus = (nMinus.IndicesCount > 0) ? nMinus : null;
            node.SubMeshIndices = zeros;

            // Create the NewVertices
            if (newVertices.Count > 0)
            {
                var vertices = originalMesh.vertices.ToList();
                var normals = originalMesh.normals.ToList();
                var tangents = originalMesh.tangents.ToList();
                var uvs = originalMesh.uv.ToList();
                foreach (var newVertex in newVertices)
                {
                    vertices.Add(newVertex.Value(vertices));
                    normals.Add(newVertex.Value(normals).normalized);
                    if (tangents.Count > 0)
                        tangents.Add(newVertex.Value(tangents).normalized);
                    if (uvs.Count > 0)
                        uvs.Add(newVertex.Value(uvs));
                }
                originalMesh.SetVertices(vertices);
                originalMesh.SetNormals(normals);
                originalMesh.SetTangents(tangents);
                originalMesh.SetUVs(0, uvs);
            }
        }

        private void Split(BSPNode node, Plane plane, out List<List<int>> zeroIndices, out List<List<int>> plusIndices, out List<List<int>> minusIndices, out List<NewVertex> newVertices)
        {
            zeroIndices = new List<List<int>>();
            plusIndices = new List<List<int>>();
            minusIndices = new List<List<int>>();
            newVertices = new List<NewVertex>();

            // Now Split !!!
            for (int i = 0; i < node.SubMeshCount; i++)
            {
                var plusSubInd = new List<int>();
                var zeroSubInd = new List<int>();
                var minusSubInd = new List<int>();

                var subMesh = node[i];

                for (int j = 0; j < subMesh.Count; j+=3)
                {
                    float[] distances = new float[3];
                    distances[0] = plane.GetDistanceToPoint(originalMesh.vertices[subMesh[j]]);
                    distances[1] = plane.GetDistanceToPoint(originalMesh.vertices[subMesh[j+1]]);
                    distances[2] = plane.GetDistanceToPoint(originalMesh.vertices[subMesh[j+2]]);

                    // ZERO
                    if (distances[0] == 0
                        && distances[1] == 0
                        && distances[2] == 0)
                    {
                        zeroSubInd.Add(subMesh[j]);
                        zeroSubInd.Add(subMesh[j + 1]);
                        zeroSubInd.Add(subMesh[j + 2]);
                    }
                    else // PLUS
                    if (distances[0] >= 0 && distances[1] >= 0 && distances[2] >= 0)
                    {
                        plusSubInd.Add(subMesh[j]);
                        plusSubInd.Add(subMesh[j + 1]);
                        plusSubInd.Add(subMesh[j + 2]);
                    }
                    else // MINUS
                    if (distances[0] <= 0 && distances[1] <= 0 && distances[2] <= 0)
                    {
                        minusSubInd.Add(subMesh[j]);
                        minusSubInd.Add(subMesh[j + 1]);
                        minusSubInd.Add(subMesh[j + 2]);
                    }
                    else // SPLITTING
                    {
                        int[] newIndices = new int[2];
                        newIndices[0] = originalMesh.vertices.Length + newVertices.Count;
                        newIndices[1] = newIndices[0]+1;

                        // TODO: si 0 est sur le plan, alors il devient le "aloneVertex" alors qu'il n'est fonctionne en tant que le aloneVertex
                        // TODO: Sign retourne 1 si 0 -> à checker

                        // which vert is alone on one side of plane ?
                        int aloneVertex = 2;
                        if ((Mathf.Sign(distances[0]) != Mathf.Sign(distances[1]))
                            && Mathf.Sign(distances[0]) != Mathf.Sign(distances[2]))
                        {
                            aloneVertex = 0;
                        }
                        else
                        if ((Mathf.Sign(distances[1]) != Mathf.Sign(distances[0]))
                            && Mathf.Sign(distances[1]) != Mathf.Sign(distances[2]))
                        {
                            aloneVertex = 1;
                        }
                        int otherVertex1 = ((aloneVertex + 1) % 3);
                        int otherVertex2 = ((aloneVertex + 2) % 3);

                        // prepare the two new vertices
                        float delta1 = Mathf.Abs(distances[aloneVertex]) / (Mathf.Abs(distances[aloneVertex]) + Mathf.Abs(distances[otherVertex1]));
                        float delta2 = Mathf.Abs(distances[aloneVertex]) / (Mathf.Abs(distances[aloneVertex]) + Mathf.Abs(distances[otherVertex2]));
                        newVertices.Add(new NewVertex(subMesh[j + aloneVertex], subMesh[j + otherVertex1], delta1));
                        newVertices.Add(new NewVertex(subMesh[j + aloneVertex], subMesh[j + otherVertex2], delta2));


                        //      A
                        //    /   \
                        // - / --- \ ---- <- plane splitting here
                        //  /       \
                        // B _______ C
                        //
                        //      |
                        //      v
                        //
                        //      A
                        //    /   \
                        //   n0 _ n1
                        //  /   \   \
                        // B _______ C

                        if (distances[aloneVertex] >= 0)
                        {
                            plusSubInd.Add(subMesh[j + aloneVertex]);
                            plusSubInd.Add(newIndices[0]);
                            plusSubInd.Add(newIndices[1]);

                            minusSubInd.Add(newIndices[0]);
                            minusSubInd.Add(newIndices[1]);
                            minusSubInd.Add(subMesh[j + otherVertex2]);

                            minusSubInd.Add(subMesh[j + otherVertex2]);
                            minusSubInd.Add(newIndices[0]);
                            minusSubInd.Add(subMesh[j + otherVertex1]);
                        }
                        else
                        {
                            minusSubInd.Add(subMesh[j + aloneVertex]);
                            minusSubInd.Add(newIndices[0]);
                            minusSubInd.Add(newIndices[1]);

                            plusSubInd.Add(newIndices[0]);
                            plusSubInd.Add(newIndices[1]);
                            plusSubInd.Add(subMesh[j + otherVertex2]);

                            plusSubInd.Add(subMesh[j + otherVertex2]);
                            plusSubInd.Add(newIndices[0]);
                            plusSubInd.Add(subMesh[j + otherVertex1]);
                        }
                    }
                }

                zeroIndices.Add(zeroSubInd);
                plusIndices.Add(plusSubInd);
                minusIndices.Add(minusSubInd);
            }
        }

        private Plane ChooseSplittingPlane(BSPNode node)
        {
            int nbCandidates = 5;

            Plane bestCandidate = new Plane();
            int bestResult = int.MaxValue;
            for(int i = 0; i < nbCandidates && bestResult > 0; i++)
            {
                Plane candidate = ComputeSplittingPlane(node, Random.Range(0, node.TrianglesCount));
                Split(node, candidate, out var zero, out var plus, out var minus, out var newVertices);
                int result = newVertices.Count;

                if (result < bestResult)
                {
                    bestCandidate = candidate;
                    bestResult = result;
                }
            }
            return bestCandidate;
        }

        private Plane ComputeSplittingPlane(BSPNode node, int triangle)
        {
            int subMeshIndex = 0;
            int firstIndexInSubMesh = triangle * 3;
            Debug.Assert(firstIndexInSubMesh < node.IndicesCount);
            while (subMeshIndex < node.SubMeshCount && firstIndexInSubMesh > node[subMeshIndex].Count)
            {
                firstIndexInSubMesh -= node[subMeshIndex].Count;
                subMeshIndex++;
            }

            var subMesh = node[subMeshIndex];
            var v0 = originalMesh.vertices[subMesh[firstIndexInSubMesh]];
            var v1 = originalMesh.vertices[subMesh[firstIndexInSubMesh+1]];
            var v2 = originalMesh.vertices[subMesh[firstIndexInSubMesh+2]];

            Plane plane = new Plane();
            plane.Set3Points(v0, v1, v2);
            return plane;
        }
    }
}