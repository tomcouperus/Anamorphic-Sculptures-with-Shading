using UnityEngine;

public static class MathUtilities {

    /// <summary>
    /// Uses Cramer's rule to get the intersection of two vectors
    /// </summary>
    /// <param name="intersection"></param>
    /// <param name="point1"></param>
    /// <param name="direction1"></param>
    /// <param name="point2"></param>
    /// <param name="direction2"></param>
    /// <returns></returns>
    /// https://math.stackexchange.com/questions/406864/intersection-of-two-lines-in-vector-form
    /// https://en.wikipedia.org/wiki/Cramer%27s_rule
    public static bool LineLineIntersection(out Vector2 intersection, Vector2 point1, Vector2 direction1, Vector2 point2, Vector2 direction2) {
        float determinantDenominatorMatrix = direction1.x * -direction2.y + direction2.x * direction1.y;
        if (determinantDenominatorMatrix == 0) { //TODO floating point error?
            intersection = Vector2.zero;
            return false;
        }

        float determinantNumeratorMatrix = (point2.x - point1.x) * -direction2.y + (point2.y - point1.y) * direction2.x;
        float a = determinantNumeratorMatrix / determinantDenominatorMatrix;

        intersection = point1 + a * direction1;
        return true;
    }

    // Possible 3D equivalent: https://stackoverflow.com/questions/59449628/check-when-two-vector3-lines-intersect-unity3d


    /// <summary>
    /// Calculates the intersection point between a line and a plane.
    /// If false is returned, that means the line and plane are parallel; either the line misses the plane completely, or lies exactly in the plane.
    /// </summary>
    /// <param name="intersection"></param>
    /// <param name="point"></param>
    /// <param name="direction"></param>
    /// <param name="planeNormal"></param>
    /// <param name="planePoint"></param>
    /// <returns></returns>
    /// https://en.wikipedia.org/wiki/Line%E2%80%93plane_intersection
    public static bool LinePlaneIntersection(out Vector3 intersection, Vector3 point, Vector3 direction, Vector3 planeNormal, Vector3 planePoint) {
        float denominator = Vector3.Dot(direction, planeNormal);
        if (denominator == 0) { //TODO floating point error?
            intersection = Vector3.zero;
            return false;
        }

        float numerator = Vector3.Dot(planePoint - point, planeNormal);
        float d = numerator / denominator;
        intersection = point + direction * d;
        return true;
    }
}
