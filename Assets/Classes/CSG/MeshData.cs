using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace CSG
{
    public struct MeshData
    {
        // Struct to create later a new vertex
        public struct NewVertex
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

        internal List<Material> materials;

        internal List<Vector3> vertices;
        internal List<Vector3> normals;
        internal List<Vector4> tangents;
        internal List<Vector2> uvs;

        public MeshData(List<Vector3> inVertices, List<Vector3> inNormals, List<Vector4> inTangents, List<Vector2> inUVs, List<Material> inMaterials)
        {
            vertices = inVertices;
            normals = inNormals;
            tangents = inTangents;
            uvs = inUVs;

            materials = inMaterials;
        }

        public MeshData(Mesh inMesh, List<Material> inMaterials)
        {
            vertices = new List<Vector3>();
            normals = new List<Vector3>();
            tangents = new List<Vector4>();
            uvs = new List<Vector2>();

            inMesh.GetVertices(vertices);
            inMesh.GetNormals(normals);
            inMesh.GetTangents(tangents);
            inMesh.GetUVs(0, uvs);

            materials = inMaterials;
        }

        public void AddData(List<NewVertex> newVertices)
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
}
