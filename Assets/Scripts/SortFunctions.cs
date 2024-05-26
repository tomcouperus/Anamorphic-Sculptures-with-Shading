using System.Collections.Generic;

public static class SortFunctions {

    // KeyValuePair KEY SORTERS
    public static int smallToLargeKeySorter(KeyValuePair<int, float> pair1, KeyValuePair<int, float> pair2) {
        return pair1.Key.CompareTo(pair2.Key);
    }

    public static int largeToSmallKeySorter(KeyValuePair<int, float> pair1, KeyValuePair<int, float> pair2) {
        return pair2.Key.CompareTo(pair1.Key);
    }
    public static int smallToLargeKeySorter(KeyValuePair<float, float> pair1, KeyValuePair<float, float> pair2) {
        return pair1.Key.CompareTo(pair2.Key);
    }

    public static int largeToSmallKeySorter(KeyValuePair<float, float> pair1, KeyValuePair<float, float> pair2) {
        return pair2.Key.CompareTo(pair1.Key);
    }

    // KeyValuePair VALUE SORTERS
    public static int smallToLargeValueSorter(KeyValuePair<int, float> pair1, KeyValuePair<int, float> pair2) {
        return pair1.Value.CompareTo(pair2.Value);
    }

    public static int largeToSmallValueSorter(KeyValuePair<int, float> pair1, KeyValuePair<int, float> pair2) {
        return pair2.Value.CompareTo(pair1.Value);
    }
    public static int smallToLargeValueSorter(KeyValuePair<float, float> pair1, KeyValuePair<float, float> pair2) {
        return pair1.Value.CompareTo(pair2.Value);
    }

    public static int largeToSmallValueSorter(KeyValuePair<float, float> pair1, KeyValuePair<float, float> pair2) {
        return pair2.Value.CompareTo(pair1.Value);
    }
}
