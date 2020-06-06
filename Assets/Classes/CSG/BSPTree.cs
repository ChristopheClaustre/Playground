using UnityEngine;
using System.Collections.Generic;
using System.Linq;

using IndicesList = System.Collections.Generic.List<System.Collections.Generic.List<int>>;

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

            public T Value<T>(List<T> ts)
            {
                return Value(ts[index0], ts[index1]);
            }

            public T Value<T>(T v0, T v1)
            {
                var dv0 = v0 as dynamic;
                var dv1 = v1 as dynamic;
                return dv0 * (1 - delta01) + dv1 * delta01;
            }
        }

        BSPNode rootNode;
        List<Material> materials;

        List<Vector3> vertices;
        List<Vector3> normals;
        List<Vector4> tangents;
        List<Vector2> uvs;

        public List<Material> Materials => materials;
        public BSPNode RootNode => rootNode;
        public int Depth => rootNode.Depth;

        public BSPTree(Mesh inMesh, List<Material> inMaterials, int maxTrianglesInLeaves = 1)
        {
            materials = inMaterials;

            vertices = new List<Vector3>();
            normals = new List<Vector3>();
            tangents = new List<Vector4>();
            uvs = new List<Vector2>();

            inMesh.GetVertices(vertices);
            inMesh.GetNormals(normals);
            inMesh.GetTangents(tangents);
            inMesh.GetUVs(0, uvs);

            // retrieve indices by submesh index
            IndicesList subMeshIndices = new IndicesList(inMesh.subMeshCount);
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
            IndicesList indices = rootNode.GetAllIndices();

            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0,uvs);
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
            Split(node.SubMeshIndices, plane, out var zeros, out var plus, out var minus, out var newVertices);

            node.Plane = plane;
            node.Plus = new BSPNode(plus);
            node.Minus = new BSPNode(minus);
            node.SubMeshIndices = zeros;

            // Create the NewVertices
            if (newVertices.Count > 0)
            {
                int futureCount = vertices.Count + newVertices.Count;
                vertices.Capacity = Mathf.Max(vertices.Capacity, futureCount);
                normals.Capacity = Mathf.Max(normals.Capacity, futureCount);
                if (tangents.Count > 0)
                    tangents.Capacity = Mathf.Max(tangents.Capacity, futureCount);
                if (uvs.Count > 0)
                    uvs.Capacity = Mathf.Max(uvs.Capacity, futureCount);
                
                foreach (var newVertex in newVertices)
                {
                    vertices.Add(newVertex.Value(vertices));
                    normals.Add(newVertex.Value(normals).normalized);
                    if (tangents.Count > 0)
                        tangents.Add(newVertex.Value(tangents).normalized);
                    if (uvs.Count > 0)
                        uvs.Add(newVertex.Value(uvs));
                }
            }
        }

        private void Split(IndicesList originalIndices, Plane plane, out IndicesList zeroIndices, out IndicesList plusIndices, out IndicesList minusIndices, out List<NewVertex> newVertices)
        {
            zeroIndices = new IndicesList(originalIndices.Count);
            plusIndices = new IndicesList(originalIndices.Count);
            minusIndices = new IndicesList(originalIndices.Count);
            newVertices = new List<NewVertex>();

            float[] distances = new float[] { 0, 0, 0 };
            // Now Split !!!
            for (int i = 0; i < originalIndices.Count; i++)
            {
                var subMesh = originalIndices[i];

                var plusSubInd = new List<int>(subMesh.Count);
                var zeroSubInd = new List<int>(subMesh.Count);
                var minusSubInd = new List<int>(subMesh.Count);

                for (int j = 0; j < subMesh.Count; j+=3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        distances[k] = plane.GetDistanceToPoint(vertices[subMesh[j + k]]);
                    }

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
                        // SPLIT IN 2 TRIANGLES
                        if (distances[0] == 0 || distances[1] == 0 || distances[2] == 0)
                        {
                            int newIndex = vertices.Count + newVertices.Count;

                            // which vert is alone on the plane ?
                            // (At this point, there is one and only one vertex on plane !)
                            int vertexOnPlane = distances[2] == 0 ? 2 : (distances[1] == 0 ? 1 : 0);
                            int otherVertex1 = ((vertexOnPlane + 1) % 3);
                            int otherVertex2 = ((vertexOnPlane + 2) % 3);

                            float absDistanceOtherVertex1 = Mathf.Abs(distances[otherVertex1]);
                            float delta = absDistanceOtherVertex1 / (absDistanceOtherVertex1 + Mathf.Abs(distances[otherVertex2]));
                            newVertices.Add(new NewVertex(subMesh[j + otherVertex1], subMesh[j + otherVertex2], delta));

                            //      |  <- plane splitting here
                            //      A
                            //    / | \
                            //   /  |  \
                            //  /   |   \
                            // B ___|___ C
                            //      |
                            //
                            //      |
                            //      v
                            //
                            //      A
                            //    / | \
                            //   /  |  \
                            //  /   |   \
                            // B ___n___ C

                            if (distances[otherVertex1] > 0)
                            {
                                plusSubInd.Add(subMesh[j + vertexOnPlane]);
                                plusSubInd.Add(subMesh[j + otherVertex1]);
                                plusSubInd.Add(newIndex);

                                minusSubInd.Add(subMesh[j + vertexOnPlane]);
                                minusSubInd.Add(newIndex);
                                minusSubInd.Add(subMesh[j + otherVertex2]);
                            }
                            else
                            {
                                minusSubInd.Add(subMesh[j + vertexOnPlane]);
                                minusSubInd.Add(subMesh[j + otherVertex1]);
                                minusSubInd.Add(newIndex);

                                plusSubInd.Add(subMesh[j + vertexOnPlane]);
                                plusSubInd.Add(newIndex);
                                plusSubInd.Add(subMesh[j + otherVertex2]);
                            }
                        }
                        else // SPLIT IN 3 TRIANGLES
                        {
                            int[] newIndices = new int[2];
                            newIndices[0] = vertices.Count + newVertices.Count;
                            newIndices[1] = newIndices[0] + 1;

                            // which vert is alone on one side of plane ?
                            // (At this point, there is an alone vertex !)
                            int aloneVertex = 0;
                            if ((distances[0] > 0 && distances[1] > 0)
                                || (distances[0] < 0 && distances[1] < 0))
                            {
                                aloneVertex = 2;
                            }
                            else
                            if ((distances[0] > 0 && distances[2] > 0)
                                || (distances[0] < 0 && distances[2] < 0))
                            {
                                aloneVertex = 1;
                            }
                            int otherVertex1 = ((aloneVertex + 1) % 3);
                            int otherVertex2 = ((aloneVertex + 2) % 3);

                            // prepare the two new vertices
                            float absDistanceAloneVertex = Mathf.Abs(distances[aloneVertex]);
                            float delta1 = absDistanceAloneVertex / (absDistanceAloneVertex + Mathf.Abs(distances[otherVertex1]));
                            float delta2 = absDistanceAloneVertex / (absDistanceAloneVertex + Mathf.Abs(distances[otherVertex2]));
                            newVertices.Capacity = System.Math.Max(newVertices.Count + 2, newVertices.Capacity);
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

                                minusSubInd.Add(newIndices[1]);
                                minusSubInd.Add(newIndices[0]);
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

                                plusSubInd.Add(newIndices[1]);
                                plusSubInd.Add(newIndices[0]);
                                plusSubInd.Add(subMesh[j + otherVertex2]);

                                plusSubInd.Add(subMesh[j + otherVertex2]);
                                plusSubInd.Add(newIndices[0]);
                                plusSubInd.Add(subMesh[j + otherVertex1]);
                            }
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
            var v0 = vertices[subMesh[firstIndexInSubMesh]];
            var v1 = vertices[subMesh[firstIndexInSubMesh+1]];
            var v2 = vertices[subMesh[firstIndexInSubMesh+2]];

            Plane plane = new Plane();
            plane.Set3Points(v0, v1, v2);
            return plane;
        }
    }
}