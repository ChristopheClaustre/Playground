using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using IndicesList = System.Collections.Generic.List<System.Collections.Generic.List<int>>;

namespace CSG
{
    [System.Serializable]
    public class BSPNode
    {
        Plane plane;
        IndicesList subMeshIndices; // indices organized by group of three representing a triangle
        BSPNode parent;
        BSPNode plus;
        BSPNode minus;
        MeshData meshData;

        internal MeshData MeshData
        {
            get => meshData;
        }

        public Plane Plane
        {
            get => plane;
            internal set => plane = value;
        }

        public List<Material> Materials => meshData.materials;

        public BSPNode()
        {
            meshData = new MeshData();
            subMeshIndices = null;

            plane = new Plane();
            parent = null;
            plus = null;
            minus = null;
        }

        internal BSPNode(BSPNode inParent, MeshData inMeshData, IndicesList inSubMeshIndices)
        {
            meshData = inMeshData;
            subMeshIndices = inSubMeshIndices;

            plane = new Plane();
            parent = inParent;
            plus = null;
            minus = null;
        }

        public bool IsLeaf => plus == null && minus == null;
        public bool IsRoot => parent == null;

        public int Depth
        {
            get
            {
                if (IsLeaf)
                {
                    return 1;
                }

                return 1 + Mathf.Max(plus == null? 0 : plus.Depth, minus == null ? 0 : minus.Depth);
            }
        }

        public int DepthFromRoot
        {
            get
            {
                if (IsRoot)
                {
                    return 1;
                }

                return 1 + parent.DepthFromRoot;
            }
        }

        public new string ToString()
        {
            string result = "Depth: " + Depth;
            result += " | Inds: " + IndicesCount;
            result += " | Tris: " + TrianglesCount;

            if (! IsLeaf)
            {
                result += " | Plane: " + plane;
            }

            return result;
        }

        public string PrintString(string prefix = "", string childPrefix = "")
        {
            string currentLine = prefix + ToString();
            if (IsLeaf)
                return currentLine;
            
            return currentLine
                + "\n" +  plus.PrintString(childPrefix + "|-> ", childPrefix + "|   ")
                + "\n" + minus.PrintString(childPrefix + "+-> ", childPrefix + "    ");
        }

        // -- CHILDREN & PARENT

        public BSPNode Parent
        {
            get => parent;
            internal set => parent = value;
        }
        public BSPNode Plus
        {
            get => plus;
            internal set => plus = value;
        }
        public BSPNode Minus
        {
            get => minus;
            internal set => minus = value;
        }

        // -- CURRENT VALUE

        public int IndicesCount
        {
            get
            {
                int count = 0;
                foreach (var list in subMeshIndices)
                {
                    count += list.Count;
                }
                return count;
            }
        }

        public int TrianglesCount
        {
            get
            {
                Debug.Assert(IndicesCount % 3 == 0);
                return IndicesCount / 3;
            }
        }

        public int SubMeshCount => subMeshIndices.Count;

        public List<int> this[int i] => subMeshIndices[i];

        public IndicesList SubMeshIndices
        {
            get => subMeshIndices;
            internal set => subMeshIndices = value;
        }

        // UTILS METHODS

        public Mesh ComputeMesh()
        {
            IndicesList indices = GetAllIndices();

            Mesh mesh = new Mesh();
            mesh.SetVertices(meshData.vertices);
            mesh.SetNormals(meshData.normals);
            mesh.SetTangents(meshData.tangents);
            mesh.SetUVs(0, meshData.uvs);
            for (int i = 0; i < indices.Count; i++)
                mesh.SetTriangles(indices[i], i);

            return mesh;
        }

        // Get all indices lists including children's indices
        public IndicesList GetAllIndices()
        {
            IndicesList copy = new IndicesList();
            for (int i = 0; i < subMeshIndices.Count; i++)
            {
                copy.Add(new List<int>());
                copy[i].AddRange(subMeshIndices[i]);
            }

            if (!IsLeaf)
            {
                var plusInd = plus?.GetAllIndices();
                var minusInd = minus?.GetAllIndices();

                Debug.Assert(plus == null || copy.Count == plusInd.Count);
                Debug.Assert(minus == null || copy.Count == minusInd.Count);

                for (int i = 0; i < copy.Count; i++)
                {
                    if (plusInd != null)
                        copy[i].AddRange(plusInd[i]);
                    if (minusInd != null)
                        copy[i].AddRange(minusInd[i]);
                }
            }

            return copy;
        }
    }
}
