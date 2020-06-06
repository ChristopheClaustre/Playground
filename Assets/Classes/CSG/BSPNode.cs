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
        BSPNode plus;
        BSPNode minus;

        public Plane Plane
        {
            get => plane;
            internal set => plane = value;
        }

        // Leaf constructor
        public BSPNode(IndicesList inSubMeshIndices)
        {
            subMeshIndices = inSubMeshIndices;

            plane = new Plane();
            plus = null;
            minus = null;
        }

        public bool IsLeaf => plus == null && minus == null;

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

        public string ToString(string prefix = "")
        {
            string result = "";

            result += "+ Depth: " + Depth;
            result += " | Inds: " + IndicesCount;
            result += " | Tris: " + TrianglesCount;

            if (IsLeaf)
            {
                return prefix + result;
            }

            result += " | Plane: " + plane;

            string subprefix = prefix + "  ";
            return prefix + result
                + "\n" + (plus != null? plus.ToString(subprefix) : subprefix + "+ null")
                + "\n" + (minus != null? minus.ToString(subprefix) : subprefix + "+ null");
        }

        // -- CHILDREN

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
