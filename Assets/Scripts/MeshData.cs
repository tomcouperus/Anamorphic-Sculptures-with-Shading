using UnityEngine;

public class MeshData {
    Vector3[] vertices;
    Vector2[] uvs;

    int[] triangles;
    int triangleIndex;

    public MeshData(int x, int y) {
        vertices = new Vector3[x * y];
        uvs = new Vector2[vertices.Length];

        triangles = new int[(x - 1) * (y - 1) * 6];
        triangleIndex = 0;
    }

    public void AddVertex(Vector3 vertex, Vector2 uv, int vertexIndex) {
        vertices[vertexIndex] = vertex;
        uvs[vertexIndex] = uv;
    }

    public void AddTriangle(int a, int b, int c) {
        triangles[triangleIndex] = a;
        triangles[triangleIndex + 1] = b;
        triangles[triangleIndex + 2] = c;
        triangleIndex += 3;
    }

    public Mesh CreateMesh() {
        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        return mesh;
    }
}
