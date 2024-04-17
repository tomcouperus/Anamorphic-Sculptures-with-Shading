using UnityEngine;

public class CurvedSquareMirror : ProceduralMirror {
    [Header("Mirror resolution")]
    [SerializeField]
    [Range(3, 256)]
    private int xSize = 10;

    [SerializeField]
    [Range(3, 256)]
    private int ySize = 10;

    private enum CurveDirection { Horizontal, Vertical };
    [Header("Curvature")]
    [SerializeField]
    private CurveDirection curveDirection = CurveDirection.Horizontal;
    [SerializeField]
    [Min(1)]
    private float radiusOfCurvature = 1.5f;

    protected override MeshData GenerateMeshData() {
        MeshData data = new(xSize, ySize);
        int vertexIndex = 0;
        for (int y = 0; y < ySize; y++) {
            for (int x = 0; x < xSize; x++) {
                float uCoord = (float) x / (xSize - 1);
                float vCoord = (float) y / (ySize - 1);
                float xCoord = uCoord * 2 - 1;
                float yCoord = vCoord * 2 - 1;

                float theta = Mathf.Asin(1 / radiusOfCurvature);
                float distOpticalCentre = radiusOfCurvature * Mathf.Cos(theta);

                float alpha;
                if (curveDirection == CurveDirection.Horizontal) {
                    yCoord = radiusOfCurvature * Mathf.Sin(-theta + (theta * 2 / (ySize - 1) * y));
                    alpha = Mathf.Asin(yCoord / radiusOfCurvature);
                } else {
                    xCoord = radiusOfCurvature * Mathf.Sin(-theta + (theta * 2 / (xSize - 1) * x));
                    alpha = Mathf.Asin(xCoord / radiusOfCurvature);
                }

                float zCoord = radiusOfCurvature * Mathf.Cos(alpha) - distOpticalCentre;

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
