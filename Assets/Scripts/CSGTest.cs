using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.Profiling;
using System.Collections.Generic;
using UnityEditor;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CSGTest : MonoBehaviour
{
    public CSG.BSPTree tree;
    [Range(1, 2000)]
    public int maxTrianglesInLeaves = 1;
    [Range(5, 50)]
    public int nbCandidates = 5;
    [Range(1E-01f, 1E-08f)]
    public float precision = 1E-06f;

    public bool testBSPTreeTrigger = false;
    public bool testProfileTrigger = false;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    // Use this for initialization
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

    }

    public void testBSPTree()
    {
        Mesh mesh = (meshFilter.mesh.vertexCount > 0) ? meshFilter.mesh : meshFilter.sharedMesh;

        PrintMesh(mesh);

        var materials = new List<Material>(); meshRenderer.GetSharedMaterials(materials);
        var start = System.DateTime.Now;
        tree = new CSG.BSPTree(mesh, materials, maxTrianglesInLeaves, nbCandidates, precision);
        Debug.Log("BSPTree created in: " + (System.DateTime.Now - start).TotalMilliseconds + " ms");
        Debug.Log(tree);
        Mesh computedMesh = tree.ComputeMesh();

        PrintMesh(computedMesh);

        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>().sharedMesh = computedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = tree.Materials.ToArray();
        go.transform.localPosition = transform.localPosition;
        go.transform.localRotation = transform.localRotation;
        go.transform.localScale = transform.localScale;
        go.transform.Translate(1.5f, 0, 0);

    }

    public void testProfile()
    {
        Mesh mesh = (meshFilter.mesh.vertexCount > 0) ? meshFilter.mesh : meshFilter.sharedMesh;

        Profiler.BeginSample(gameObject.name + ".testProfile()", gameObject);
        var materials = new List<Material>(); meshRenderer.GetSharedMaterials(materials);
        var tree = new CSG.BSPTree(mesh, materials, maxTrianglesInLeaves, nbCandidates, precision);
        Profiler.EndSample();

        EditorApplication.ExitPlaymode();
    }

    // Update is called once per frame
    void Update()
    {
        if (testBSPTreeTrigger)
        {
            testBSPTreeTrigger = false;

            testBSPTree();
        }

        if (testProfileTrigger)
        {
            testProfileTrigger = false;

            testProfile();
        }
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
