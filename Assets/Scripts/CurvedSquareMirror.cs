using UnityEngine;

public class CurvedSquareMirror : ProceduralMirror {
    [Header("Mirror resolution")]
    [SerializeField]
    [Range(3, 256)]
    private int xSize = 3;

    [SerializeField]
    [Range(3, 256)]
    private int ySize = 3;

    protected override MeshData GenerateMeshData() {
        MeshData data = new(xSize, ySize);
        int vertexIndex = 0;
        for (int y = 0; y < ySize; y++) {
            for (int x = 0; x < xSize; x++) {
                // For now, generate a plane
                float uCoord = (float) x / (xSize - 1);
                float vCoord = (float) y / (ySize - 1);
                float zCoord = 0;
                Vector3 vertex = new(uCoord * 2 - 1, vCoord * 2 - 1, zCoord);
                Vector2 uv = new(uCoord, vCoord);

                data.AddVertex(vertex, uv, vertexIndex);
                bool makeTriangle = x < xSize - 1 && y < ySize - 1;
                if (makeTriangle) {
                    int a = vertexIndex;
                    int b = vertexIndex + 1;
                    int c = vertexIndex + ySize;
                    int d = vertexIndex + ySize + 1;
                    data.AddTriangle(a, c, b);
                    data.AddTriangle(b, c, d);
                }

                vertexIndex++;
            }
        }
        return data;
    }
}
