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
        internal struct NewVertex
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

        internal List<Vector3> vertices;
        List<Vector3> normals;
        List<Vector4> tangents;
        List<Vector2> uvs;

        public List<Material> Materials => materials;
        public BSPNode RootNode => rootNode;
        public int Depth => rootNode == null ? 0 : rootNode.Depth;

        public BSPTree()
        {
            materials = null;
            vertices = null;
            normals = null;
            tangents = null;
            uvs = null;

            rootNode = null;
        }

        internal void SetData(List<Vector3> inVertices, List<Vector3> inNormals, List<Vector4> inTangents, List<Vector2> inUVs, BSPNode inRootNode, List<Material> inMaterials)
        {
            vertices = inVertices;
            normals = inNormals;
            tangents = inTangents;
            uvs = inUVs;

            materials = inMaterials;

            rootNode = inRootNode;
        }

        internal void AddData(List<NewVertex> newVertices)
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
    }
}