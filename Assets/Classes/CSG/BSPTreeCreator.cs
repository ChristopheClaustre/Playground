using UnityEngine;
using System.Collections.Generic;
using System.Linq;

using IndicesList = System.Collections.Generic.List<System.Collections.Generic.List<int>>;
using static CSG.BSPTree;

namespace CSG
{
    public static class BSPTreeCreator
    {
        public static BSPTree Construct(Mesh inMesh, List<Material> inMaterials, int maxTrianglesInLeaves = 1, int nbCandidates = 5, float precision = 1E-06f)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();

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

            var rootNode = new BSPNode(subMeshIndices);

            BSPTree tree = new BSPTree();
            tree.SetData(vertices, normals, tangents, uvs, rootNode, inMaterials);
            tree.ConstructInternal(rootNode, maxTrianglesInLeaves, nbCandidates, precision);

            return tree;
        }

        // -- UTILS' METHODS FOR THE GENERATION OF A BSPTREE

        private static bool ConstructInternal(this BSPTree tree, BSPNode node, int maxTrianglesInLeaves, int nbCandidates, float precision)
        {
            if (node == null || node.TrianglesCount <= maxTrianglesInLeaves)
            {
                return true; // return without splitting, it's a leaf
            }

            // Compute split
            Plane plane = node.ChooseSplittingPlane(tree.vertices, nbCandidates, precision);
            if (plane.normal == Vector3.zero)
            {
                Debug.Log("Impossible to compute a plane at depth " + tree.Depth + ". Try to increase precision or number of candidates.");
                return false;
            }
            Split(node.SubMeshIndices, plane, tree.vertices, precision, out var zeros, out var plus, out var minus, out var newVertices);

            // Apply split
            node.Plane = plane;
            node.Plus = new BSPNode(plus);
            node.Minus = new BSPNode(minus);
            node.SubMeshIndices = zeros;
            tree.AddData(newVertices);

            // recurse
            bool result = true;
            result &= tree.ConstructInternal(node.Plus, maxTrianglesInLeaves, nbCandidates, precision);
            result &= tree.ConstructInternal(node.Minus, maxTrianglesInLeaves, nbCandidates, precision);
            return result;
        }

        private static void Split(IndicesList originalIndices, Plane plane, List<Vector3> vertices, float precision, out IndicesList zeroIndices, out IndicesList plusIndices, out IndicesList minusIndices, out List<NewVertex> newVertices)
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
                        distances[k] = (Mathf.Abs(distances[k]) <= precision) ? 0 : distances[k];
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

        private static int SplitCost(IndicesList originalIndices, Plane plane, List<Vector3> vertices, float precision)
        {
            int cost = 0;
            float[] distances = new float[] { 0, 0, 0 };
            // Test Split !!!
            for (int i = 0; i < originalIndices.Count; i++)
            {
                var subMesh = originalIndices[i];

                for (int j = 0; j < subMesh.Count; j += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        distances[k] = plane.GetDistanceToPoint(vertices[subMesh[j + k]]);
                        distances[k] = (Mathf.Abs(distances[k]) <= precision) ? 0 : distances[k];
                    }

                    if (!(distances[0] >= 0 && distances[1] >= 0 && distances[2] >= 0)
                        && !(distances[0] <= 0 && distances[1] <= 0 && distances[2] <= 0))
                    {
                        if (distances[0] == 0 || distances[1] == 0 || distances[2] == 0)
                            cost += 1;
                        else
                            cost += 2;
                    }
                }
            }

            return cost;
        }

        private static Plane ChooseSplittingPlane(this BSPNode node, List<Vector3> vertices, int nbCandidates, float precision)
        {
            Plane bestCandidate = new Plane();
            int bestResult = int.MaxValue;
            for(int i = 0; i < nbCandidates && bestResult > 0; i++)
            {
                Plane candidate = node.ComputeSplittingPlane(vertices, Random.Range(0, node.TrianglesCount));
                if (candidate.normal != Vector3.zero)
                {
                    int result = SplitCost(node.SubMeshIndices, candidate, vertices, precision);

                    if (result < bestResult)
                    {
                        bestCandidate = candidate;
                        bestResult = result;
                    }
                }
            }

            return bestCandidate;
        }

        private static Plane ComputeSplittingPlane(this BSPNode node, List<Vector3> vertices, int triangle)
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