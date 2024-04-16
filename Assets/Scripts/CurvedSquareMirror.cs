using UnityEngine;

public class CurvedSquareMirror : ProceduralMirror {
    [Header("Mirror resolution")]
    [SerializeField]
    [Range(3, 256)]
    private int xSize = 3;

    [SerializeField]
    [Range(3, 256)]
    private int ySize = 3;

    private enum CurveDirection { Horizontal, Vertical };
    [Header("Curvature")]
    [SerializeField]
    private CurveDirection curveDirection = CurveDirection.Horizontal;
    [SerializeField]
    [Min(1)]
    private float radiusOfCurvature = 1;

    protected override MeshData GenerateMeshData() {
        MeshData data = new(xSize, ySize);
        int vertexIndex = 0;
        for (int y = 0; y < ySize; y++) {
            for (int x = 0; x < xSize; x++) {
                // For now, generate a plane
                float uCoord = (float) x / (xSize - 1);
                float vCoord = (float) y / (ySize - 1);
                float xCoord = uCoord * 2 - 1;
                float yCoord = vCoord * 2 - 1;
                float zCoord;

                if (curveDirection == CurveDirection.Horizontal) {
                    float distOpticalCentre = radiusOfCurvature * Mathf.Cos(Mathf.Asin(1 / radiusOfCurvature));

                    float alpha = Mathf.Asin(yCoord / radiusOfCurvature);
                    zCoord = radiusOfCurvature * Mathf.Cos(alpha) - distOpticalCentre;
                } else {
                    float distOpticalCentre = radiusOfCurvature * Mathf.Sin(Mathf.Acos(1 / radiusOfCurvature));

                    float alpha = Mathf.Acos(xCoord / radiusOfCurvature);
                    zCoord = radiusOfCurvature * Mathf.Sin(alpha) - distOpticalCentre;
                }

                Vector3 vertex = new(xCoord, yCoord, zCoord);
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
