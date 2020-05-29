using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CSGTest : MonoBehaviour
{
    public CSG.BSPTree tree;
    [Range(1, 2000)]
    public int maxTrianglesInLeaves = 1;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    // Use this for initialization
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        testBSPTree();
    }

    public void testBSPTree()
    {
        Mesh mesh = (meshFilter.mesh.vertexCount > 0) ? meshFilter.mesh : meshFilter.sharedMesh;

        PrintMesh(mesh);

        var start = System.DateTime.Now;
        tree = new CSG.BSPTree(mesh, meshRenderer.sharedMaterials.ToList(), maxTrianglesInLeaves);
        Debug.Log("BSPTree created in: " + (System.DateTime.Now - start).TotalMilliseconds + " ms");
        Debug.Log(tree);
        Mesh computedMesh = tree.ComputeMesh();
        Mesh computedMesh2 = tree.ComputeMesh();

        PrintMesh(computedMesh);
        PrintMesh(computedMesh2);

        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>().sharedMesh = computedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = tree.Materials.ToArray();
        go.transform.localPosition = transform.localPosition;
        go.transform.localRotation = transform.localRotation;
        go.transform.localScale = transform.localScale;
        go.transform.Translate(1.5f, 0, 0);

        GameObject go2 = new GameObject();
        go2.AddComponent<MeshFilter>().sharedMesh = computedMesh2;
        go2.AddComponent<MeshRenderer>().sharedMaterials = tree.Materials.ToArray();
        go2.transform.localPosition = transform.localPosition;
        go2.transform.localRotation = transform.localRotation;
        go2.transform.localScale = transform.localScale;
        go2.transform.Translate(3.0f, 0, 0);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public static void PrintMesh(Mesh mesh)
    {
        Debug.Log("indices count : " + mesh.triangles.Length
            + "\nsubMesh count : " + mesh.subMeshCount
            + "\nvertices count : " + mesh.vertices.Length
            + "\nnormals count : " + mesh.normals.Length
            + "\ntangents count : " + mesh.tangents.Length
            + "\nboneWeights count : " + mesh.boneWeights.Length
            + "\nuv count : " + mesh.uv.Length
            + "\nuv2 count : " + mesh.uv2.Length
            + "\nuv3 count : " + mesh.uv3.Length
            + "\nuv4 count : " + mesh.uv4.Length
            + "\nuv5 count : " + mesh.uv5.Length
            + "\nuv6 count : " + mesh.uv6.Length
            + "\nuv7 count : " + mesh.uv7.Length
            + "\nuv8 count : " + mesh.uv8.Length
            );
    }
}
