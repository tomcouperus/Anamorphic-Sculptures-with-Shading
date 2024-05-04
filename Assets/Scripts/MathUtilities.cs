using UnityEngine;

public static class MathUtilities {

    /// <summary>
    /// Uses Cramer's rule to get the intersection of two vectors
    /// </summary>
    /// <param name="intersection"></param>
    /// <param name="point1"></param>
    /// <param name="direction1">Normalized direction vector</param>
    /// <param name="point2"></param>
    /// <param name="direction2">Normalized direction vector</param>
    /// <returns></returns>
    /// https://math.stackexchange.com/questions/406864/intersection-of-two-lines-in-vector-form
    /// https://en.wikipedia.org/wiki/Cramer%27s_rule
    public static bool LineLineIntersection(out Vector2 intersection, Vector2 point1, Vector2 direction1, Vector2 point2, Vector2 direction2) {
        float determinantDenominatorMatrix = direction1.x * -direction2.y + direction2.x * direction1.y;
        if (determinantDenominatorMatrix == 0) {
            intersection = Vector2.zero;
            return false;
        }

        float determinantNumeratorMatrix = (point2.x - point1.x) * -direction2.y + (point2.y - point1.y) * direction2.x;
        float a = determinantNumeratorMatrix / determinantDenominatorMatrix;

        intersection = point1 + a * direction1;
        return true;
    }

    // Possible 3D equivalent: https://stackoverflow.com/questions/59449628/check-when-two-vector3-lines-intersect-unity3d
}
