using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VertexNormalOptimizer : MonoBehaviour {
    private const string NORMAL_SHADER_FLIP_PROP_NAME = "_FlipNormalsZ";

    private enum DeformationMethod { Random, Mirror };

    [Header("General Settings")]
    [SerializeField]
    private MeshFilter[] originalObjects;
    [SerializeField]
    private int originalObjectIndex;
    [SerializeField]
    private bool showOriginalObject;
    [SerializeField]
    private bool useSmoothShading;
    [SerializeField]
    private Observer observer;
    [SerializeField]
    private int seed = 0;
    [SerializeField]
    private Material defaultMaterial;
    [SerializeField]
    private Material normalDiffMaterial;

    [Header("Deformation Settings")]
    [SerializeField]
    private DeformationMethod deformationMethod = DeformationMethod.Random;
    [SerializeField]
    private Mirror mirror;
    [SerializeField]
    private float maxRaycastDistance = 20;
    [SerializeField]
    private float minDeformationOffset = -1;
    [SerializeField]
    private float maxDeformationOffset = 1;

    private enum OptimizerMethod { Iterative, Annealing };
    [Header("Optimizer Settings")]
    [SerializeField]
    private OptimizerMethod optimizerMethod = OptimizerMethod.Iterative;
    [SerializeField]
    private int iterations = 1;
    public bool ManualOptimizeSteps = false;
    private int currentIteration = 0;
    private const int MAX_ITERATIONS = 1000000;
    [SerializeField]
    private float minOptimizeOffset = -5;
    [SerializeField]
    private float maxOptimizeOffset = 5;
    [SerializeField]
    private float optimizeOffsetStep = 0.1f;
    private const float MINIMUM_OPTIMIZE_OFFSET_STEP = 0.001f;
    [SerializeField]
    private float minTemperature = 0.01f;
    [SerializeField]
    private float maxTemperature = 100f;
    [SerializeField]
    private AnimationCurve temperatureCurve;

    private enum SmoothingMethod { Laplacian };
    [Header("Smoothing Settings")]
    [SerializeField]
    private SmoothingMethod smoothingMethod = SmoothingMethod.Laplacian;

    [Header("Status: Initialized -- variables")]
    private Mesh originalMesh;
    [SerializeField]
    private Vector3[] originalVertices;
    private Vector3[] originalNormals;
    private Vector3[] adjustmentRayOrigins;
    private Vector3[] adjustmentRays;
    private float[] originalAdjustmentDistances;
    private readonly Color GIZMOS_INITIALIZED_COLOR = Color.white;

    [Header("Status: Deformed -- variables")]
    private Mesh deformedMesh;
    private Vector3[] deformedVertices;
    private Vector3[] deformedNormals;
    private float[] deformedAdjustmentDistances;
    private float[] deformedAngularDeviations;
    private readonly Color GIZMOS_DEFORMED_COLOR = Color.blue;

    [Header("Status: Optimized -- variables")]
    private Mesh optimizedMesh;
    private Vector3[] optimizedVertices;
    private Vector3[] optimizedNormals;
    private float[] optimizedAdjustmentDistances;
    private float[] optimizedAngularDeviations;
    private Dictionary<float, float> offsetTotalDeviationMap;
    private Func<int, int> optimizerStepFunction;
    private readonly Color GIZMOS_OPTIMIZED_COLOR = Color.magenta;

    [Header("Status: Smoothed -- variables")]
    private Mesh smoothedMesh;
    private Vector3[] smoothedVertices;
    private Vector3[] smoothedNormals;
    private int smoothingSteps;
    private float[] smoothenedAngularDeviations;
    private Func<int, int> smoothingStepFunction;

    [Header("Save information")]
    private VertexNormalOptimizerData saveData;



    private const float GIZMO_SPHERE_RADIUS = 0.05f;
    [Header("Debug")]
    [SerializeField]
    private bool useNormalDiffShader = false;
    [SerializeField]
    private int selectedVertex = 0;
    private readonly Color GIZMOS_SELECTED_COLOR = Color.green;
    [SerializeField]
    private bool showOriginalVertices = false;
    [SerializeField]
    private bool showOriginalNormals = false;
    [SerializeField]
    private bool showAdjustmentRays = false;
    [SerializeField]
    private float adjustmentRaysScale = 10;

    [SerializeField]
    private bool showDeformedVertices = false;
    [SerializeField]
    private bool showDeformedNormals = false;

    [SerializeField]
    private bool showOptimizedVertices = false;
    [SerializeField]
    private bool showOptimizedNormals = false;
    [SerializeField]
    private bool showOffsetDeviationMap = false;


    public enum OptimizerStatus { None, Initialized, Deformed, OptimizingManual, OptimizingAll, Optimized, SmoothingLaplacian, Smoothed };
    public OptimizerStatus Status { get; private set; } = OptimizerStatus.None;

    // MAIN METHODS
    public void Initialize() {
        Debug.Log("Initializing");
        // Initialize the random number generator
        UnityEngine.Random.InitState(seed);

        // Translate the original points to global space and obtain the adjustment rays
        MeshFilter originalObject = originalObjects[originalObjectIndex];
        originalMesh = originalObject.mesh;
        Vector3[] localOriginalVertices = originalMesh.vertices;
        originalVertices = new Vector3[localOriginalVertices.Length];
        Vector3 viewPosition = observer.transform.position;
        adjustmentRayOrigins = new Vector3[originalVertices.Length];
        adjustmentRays = new Vector3[originalVertices.Length];
        originalAdjustmentDistances = new float[originalVertices.Length];
        for (int i = 0; i < localOriginalVertices.Length; i++) {
            originalVertices[i] = originalObject.transform.TransformPoint(localOriginalVertices[i]);
            Vector3 ray = originalVertices[i] - viewPosition;
            adjustmentRayOrigins[i] = observer.transform.position;
            adjustmentRays[i] = ray.normalized;
            originalAdjustmentDistances[i] = ray.magnitude;
        }

        // Recalculate normals to use smooth shading or not
        RecalculateNormals(originalMesh, useSmoothShading);
        originalNormals = originalMesh.normals;
        originalMesh.SetUVs(3, originalNormals);

        // Update status
        Status = OptimizerStatus.Initialized;
        SwitchMesh();
    }

    public void Deform() {
        // if (Status != OptimizerStatus.Initialized) return;
        Debug.Log("Deforming mesh");
        deformedVertices = new Vector3[adjustmentRays.Length];
        deformedAdjustmentDistances = new float[originalVertices.Length];

        switch (deformationMethod) {
            case DeformationMethod.Random:
                DeformRandom();
                break;
            case DeformationMethod.Mirror:
                DeformMirror();
                break;
        }

        // Calculate deviation
        deformedAngularDeviations = CalculateAngularDeviation(originalNormals, deformedNormals, deformationMethod == DeformationMethod.Mirror);
        Debug.Log("Angular deviation: " + Enumerable.Sum(deformedAngularDeviations));

        // Update status
        currentIteration = 0;
        Status = OptimizerStatus.Deformed;
        SwitchMesh();
    }

    private void DeformMirror() {
        // Deform the mesh through a mirror
        // First calculate the intersection points with a mirror to obtain reflection rays as the relevant adjustment rays
        Vector3 viewPosition = observer.transform.position;
        for (int i = 0; i < originalVertices.Length; i++) {
            // Raycast from observer to each vertex on the original object to find intersection with mirror.
            Vector3 direction = (originalVertices[i] - viewPosition).normalized;
            RaycastHit[] hits = Physics.RaycastAll(viewPosition, direction, maxRaycastDistance, LayerMask.GetMask("Mirror"));

            // Use hit as origin
            if (hits.Length == 0) {
                Debug.LogError("At least one ray from the observer to the original object does not hit the mirror.");
                return;
            }
            // Set these as the new adjustment rays
            adjustmentRayOrigins[i] = hits[0].point;
            adjustmentRays[i] = Vector3.Reflect(direction, hits[0].normal);
            // adjustmentRays[i].z *= -1;

            // Determine the distance from each vertex to their respective mirror intersection
            // And place the deformed vertex
            deformedAdjustmentDistances[i] = Vector3.Distance(originalVertices[i], adjustmentRayOrigins[i]);
            deformedVertices[i] = adjustmentRayOrigins[i] + adjustmentRays[i] * deformedAdjustmentDistances[i];
        }

        // Apply it to a new mesh
        deformedMesh = new();
        deformedMesh.SetVertices(deformedVertices);
        int[] deformedTriangles = new int[originalMesh.triangles.Length];
        for (int i = 0; i < deformedTriangles.Length; i += 3) {
            deformedTriangles[i] = originalMesh.triangles[i + 2];
            deformedTriangles[i + 1] = originalMesh.triangles[i + 1];
            deformedTriangles[i + 2] = originalMesh.triangles[i];
        }
        deformedMesh.SetTriangles(deformedTriangles, 0);
        // deformedMesh.SetTriangles(originalMesh.triangles, 0);
        RecalculateNormals(deformedMesh, useSmoothShading);
        GetComponent<MeshFilter>().sharedMesh = deformedMesh;
        deformedNormals = deformedMesh.normals;
        // Upload the original normals to the shader as uv coordinates
        deformedMesh.SetUVs(3, originalNormals);
    }

    private void DeformRandom() {
        // Apply a deformation to the original mesh by adjusting the distance along the rays
        Vector3 viewPosition = observer.transform.position;
        // But only deform the unique vertices
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(originalVertices);
        foreach ((Vector3 position, List<int> identicalVertices) in verticesByPosition) {
            // Determine the new distance
            float offset = UnityEngine.Random.Range(minDeformationOffset, maxDeformationOffset);
            float newDistance = originalAdjustmentDistances[identicalVertices[0]] + offset;
            // Apply it to all identical vertices
            foreach (int vi in identicalVertices) {
                deformedVertices[vi] = viewPosition + adjustmentRays[vi] * newDistance;
                deformedAdjustmentDistances[vi] = newDistance;
            }
        }

        // Apply it to a new mesh
        deformedMesh = new();
        deformedMesh.SetVertices(deformedVertices);
        deformedMesh.SetTriangles(originalMesh.triangles, 0);
        RecalculateNormals(deformedMesh, useSmoothShading);
        GetComponent<MeshFilter>().sharedMesh = deformedMesh;
        deformedNormals = deformedMesh.normals;
        // Upload the original normals to the shader as uv coordinates
        deformedMesh.SetUVs(3, originalNormals);
    }

    public void Optimize() {
        // if (Status != OptimizerStatus.Deformed) return;
        // If doing manual stepping and optimization is set up, just execute one step, until you reach the maximum specified
        if (ManualOptimizeSteps && currentIteration > 0 && Status == OptimizerStatus.OptimizingManual) {
            int result = optimizerStepFunction(currentIteration);
            // If the optimization can't go further, pretend this was the last iteration
            if (result == 2) currentIteration = iterations - 1;
            // On the last iteration, print the nice message
            if (currentIteration >= iterations - 1) {
                Debug.Log("Angular deviation: " + Enumerable.Sum(optimizedAngularDeviations));
                Status = OptimizerStatus.Optimized;
            }
            currentIteration++;
            return;
        }
        // Otherwise, if not manually stepping, execute all steps.
        // If manual stepping, do all initialization, and do 1 step.
        Debug.Log("Optimizing vertex normals");

        // Optimize the deformed mesh by adjusting the distance along the rays
        // Initialize variables
        optimizedVertices = (Vector3[]) deformedVertices.Clone();
        optimizedNormals = (Vector3[]) deformedNormals.Clone();
        optimizedAdjustmentDistances = (float[]) deformedAdjustmentDistances.Clone();
        optimizedAngularDeviations = (float[]) deformedAngularDeviations.Clone();
        Vector3 viewPosition = observer.transform.position;

        // Initialize mesh
        optimizedMesh = new();
        optimizedMesh.SetVertices(optimizedVertices);
        optimizedMesh.SetTriangles(deformedMesh.triangles, 0);
        optimizedMesh.SetUVs(3, originalNormals);

        // If having the saving enabled, make the save data
        saveData = new() {
            ObjectName = originalObjects[originalObjectIndex].gameObject.name,
            Seed = seed,
            VertexCount = originalVertices.Length,
            DeformedAngularDeviation = Enumerable.Sum(deformedAngularDeviations),
            DeformationMethod = deformationMethod,
            OptimizerMethod = optimizerMethod,
            IdealNormalAnglesFromRay = new float[originalNormals.Length],
            Triangles = optimizedMesh.triangles
        };
        for (int i = 0; i < originalNormals.Length; i++) {
            saveData.IdealNormalAnglesFromRay[i] = Vector3.Angle(adjustmentRays[i], originalNormals[i]);
        }

        switch (optimizerMethod) {
            case OptimizerMethod.Iterative:
                optimizerStepFunction = OptimizeIterative();
                break;
            case OptimizerMethod.Annealing:
                optimizerStepFunction = OptimizeAnnealing();
                break;
        }

        if (ManualOptimizeSteps) {
            Status = OptimizerStatus.OptimizingManual;
            optimizerStepFunction(currentIteration);
            currentIteration++;
        } else {
            Status = OptimizerStatus.OptimizingAll;
            StartCoroutine(OptimizeAllIterations());
        }
        // Update status
        SwitchMesh();
    }

    private IEnumerator OptimizeAllIterations() {
        float timeStart = Time.time;
        for (int i = 0; i < iterations; i++) {
            int result = optimizerStepFunction(i);
            if (result == 1) break;
            yield return null;
        }
        float timeEnd = Time.time;
        int deltaTimeMillis = (int) ((timeEnd - timeStart) * 1000);
        Debug.Log("Angular deviation: " + Enumerable.Sum(optimizedAngularDeviations));
        Debug.Log("Time (ms): " + deltaTimeMillis);
        saveData.TimeMilliseconds = deltaTimeMillis;
        saveData.FinalVertices = optimizedVertices;
        Status = OptimizerStatus.Optimized;
    }

    // Creates a function that outputs 0 if no problem was in the iteration, 1 if a vertex was skipped, and 2 if the process should halt.
    private Func<int, int> OptimizeIterative() {
        // Make a list of various offsets
        offsetTotalDeviationMap = new();
        List<float> offsets = new();
        for (float offset = minOptimizeOffset; offset <= maxOptimizeOffset; offset += optimizeOffsetStep) {
            offsets.Add(offset);
            offsetTotalDeviationMap.Add(offset, -1);
        }

        // Add method specific savedata
        saveData.VertexSelectionMethod = "Maximum local angular deviation, skipping if no decrease in total deviation";
        saveData.Offsets.AddRange(offsets);
        saveData.SamplingRate = optimizeOffsetStep;

        // Only optimize the unique vertices
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(originalVertices);

        // Sort the optimized angular deviations to keep track of the largest
        Dictionary<int, float> optimizedAngularDeviationsMap = new();
        for (int i = 0; i < optimizedVertices.Length; i++) {
            optimizedAngularDeviationsMap.Add(i, deformedAngularDeviations[i]);
        }
        List<KeyValuePair<int, float>> sortedOptimizedAngularDeviations = optimizedAngularDeviationsMap.ToList();
        sortedOptimizedAngularDeviations.Sort(SortFunctions.largeToSmallValueSorter);
        // foreach ((int v, float deviation) in sortedOptimizedAngularDeviations) {
        //     Debug.Log(v + ": " + deviation);
        // }

        // For a number of iterations, optimize the vertex that has the largest angular deviation
        int skipAmount = 0;

        // Returns 0 if no problem in the iteration, 1 if all vertices have been skipped and loop should halt, 2 if a vertex was skipped.
        return (int i) => {
            if (skipAmount >= sortedOptimizedAngularDeviations.Count) {
                Debug.LogWarning("Skipped all vertices. Halting optimization");
                return 1;
            }
            int v = sortedOptimizedAngularDeviations[skipAmount].Key;
            Debug.Log("Iteration: " + i + ", vertex: " + v);
            Vector3[] newVertices = (Vector3[]) optimizedVertices.Clone();

            // Find the set of identical vertices containing v
            List<int> identicalVertices = null;
            foreach ((Vector3 _, List<int> ivs) in verticesByPosition) {
                if (ivs.Contains(v)) {
                    identicalVertices = ivs;
                    break;
                }
            }

            // Go over all the offsets
            foreach (float offset in offsets) {
                // Determine the new distance and position
                float newDistance = optimizedAdjustmentDistances[v] + offset;
                Vector3 newVertexPosition = adjustmentRayOrigins[v] + adjustmentRays[v] * newDistance;
                // Update all identical vertices
                foreach (int vi in identicalVertices) {
                    newVertices[vi] = newVertexPosition;
                }

                // Recalculate the normals for this offset
                optimizedMesh.SetVertices(newVertices);
                RecalculateNormals(optimizedMesh, useSmoothShading);
                // Calculate the new total deviation and store it
                float[] deviations = CalculateAngularDeviation(originalNormals, optimizedMesh.normals, reflectedNormal: deformationMethod == DeformationMethod.Mirror);
                offsetTotalDeviationMap[offset] = Enumerable.Sum(deviations);
            }

            // Determine the offset with minimum total deviation
            List<KeyValuePair<float, float>> sortedOffsetTotalDeviations = offsetTotalDeviationMap.ToList();
            sortedOffsetTotalDeviations.Sort(SortFunctions.smallToLargeValueSorter);

            // Apply this optimal offset to all identical vertices
            float optimalOffset = sortedOffsetTotalDeviations[0].Key;

            // Save the deviations of this iteration sorted by offset
            List<KeyValuePair<float, float>> deviationsSortedByOffset = offsetTotalDeviationMap.ToList();
            deviationsSortedByOffset.Sort(SortFunctions.smallToLargeKeySorter);
            List<float> sortedDeviations = new();
            foreach ((float _, float deviation) in deviationsSortedByOffset) {
                sortedDeviations.Add(deviation);
            }
            saveData.OptimizedDeviations.AddRange(sortedDeviations);
            // Save the current vertex
            saveData.ChosenVertices.Add(v);
            // Save this iteration's initial total deviation that is has to decrease
            saveData.CurrentDeviations.Add(Enumerable.Sum(optimizedAngularDeviations));

            // Check if the vertex changes or not, and skip move the next iteration forward if it doesn't.
            bool skip = false;
            float effectivelyZeroOffset = optimizeOffsetStep - 0.0001f;
            if (optimalOffset > -effectivelyZeroOffset && optimalOffset < effectivelyZeroOffset) {
                // Debug.Log("Optimal position already attained");
                skip = true;
            } else {
                skip = false;
                float optimalDistance = optimizedAdjustmentDistances[v] + optimalOffset;
                Vector3 optimalVertexPosition = adjustmentRayOrigins[v] + adjustmentRays[v] * optimalDistance;
                foreach (int vi in identicalVertices) {
                    optimizedVertices[vi] = optimalVertexPosition;
                    optimizedAdjustmentDistances[vi] = optimalDistance;
                }
            }

            saveData.AcceptedIterations.Add(!skip);

            optimizedMesh.SetVertices(optimizedVertices);
            RecalculateNormals(optimizedMesh, useSmoothShading);
            if (skip) {
                skipAmount++;
                return 2;
            } else {
                skipAmount = 0;
            }
            // If not skipped, finish the iteration
            optimizedNormals = optimizedMesh.normals;
            // Resort the vertices according to their new deviations
            optimizedAngularDeviations = CalculateAngularDeviation(originalNormals, optimizedNormals, reflectedNormal: deformationMethod == DeformationMethod.Mirror);
            for (int vi = 0; vi < optimizedVertices.Length; vi++) {
                optimizedAngularDeviationsMap[vi] = optimizedAngularDeviations[vi];
            }
            sortedOptimizedAngularDeviations = optimizedAngularDeviationsMap.ToList();
            sortedOptimizedAngularDeviations.Sort(SortFunctions.largeToSmallValueSorter);
            return 0;
        };
    }

    // Creates a function that returns 0 if no problems in an iteration, and 1 if the process should halt.
    private Func<int, int> OptimizeAnnealing() {
        // Only optimize the unique vertices
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(originalVertices);

        // Initialize a proposed mesh to calculate normal deviations with before accepting
        Mesh proposedMesh = new();
        proposedMesh.SetVertices(optimizedVertices);
        proposedMesh.SetTriangles(optimizedMesh.triangles, 0);

        // Sort the optimized angular deviations to keep track of the largest
        // TODO don't know if with annealing this is the best, but can always switch to random selection
        Dictionary<int, float> optimizedAngularDeviationsMap = new();
        for (int i = 0; i < optimizedVertices.Length; i++) {
            optimizedAngularDeviationsMap.Add(i, deformedAngularDeviations[i]);
        }
        List<KeyValuePair<int, float>> sortedOptimizedAngularDeviations = optimizedAngularDeviationsMap.ToList();
        sortedOptimizedAngularDeviations.Sort(SortFunctions.largeToSmallValueSorter);
        int skipAmount = 0;

        // Do some save data
        saveData.VertexSelectionMethod = "Random";

        // Initialize time fraction
        float temperature;

        float currentTotalDeviation = Enumerable.Sum(deformedAngularDeviations);
        // Create optimizer step function
        return (int i) => {
            // TODO only used in the maximum first mode
            if (skipAmount >= sortedOptimizedAngularDeviations.Count) {
                Debug.LogWarning("Skipped all vertices. Halting optimization");
                return 1;
            }
            // Pick a vertex and its equivalents
            // With maximum deviation
            // int v = sortedOptimizedAngularDeviations[skipAmount].Key;
            // Randomly
            int v = UnityEngine.Random.Range(0, originalVertices.Length);

            List<int> identicalVertices = null;
            foreach ((Vector3 _, List<int> ivs) in verticesByPosition) {
                if (ivs.Contains(v)) {
                    identicalVertices = ivs;
                    break;
                }
            }

            float currentLocalDeviation = optimizedAngularDeviationsMap[v];
            // Calculate temperature
            temperature = temperatureCurve.Evaluate(1 - i / (float) (iterations - 1));
            temperature = Mathf.Lerp(minTemperature, maxTemperature, temperature);

            Debug.Log("Iteration: " + i + ", vertex: " + v + ", temperature: " + temperature);

            // Initialize the new vertices by cloning from the last optimized version
            Vector3[] proposedVertices = (Vector3[]) optimizedMesh.vertices.Clone();

            // Pick a random offset that lies in the range specified in the settings
            float proposedOffset = UnityEngine.Random.Range(minOptimizeOffset, maxOptimizeOffset);
            // print("Offset: " + proposedOffset);
            // Apply offset to chosen vertices
            float proposedDistance = optimizedAdjustmentDistances[v] + proposedOffset;
            Vector3 proposedPosition = adjustmentRayOrigins[v] + adjustmentRays[v] * proposedDistance;
            foreach (int vi in identicalVertices) {
                proposedVertices[vi] = proposedPosition;
            }

            // Update proposed mesh
            proposedMesh.SetVertices(proposedVertices);
            RecalculateNormals(proposedMesh, useSmoothShading);
            // Calculate proposed total deviation
            float[] proposedDeviations = CalculateAngularDeviation(originalNormals, proposedMesh.normals, reflectedNormal: deformationMethod == DeformationMethod.Mirror);
            float proposedTotalDeviation = Enumerable.Sum(proposedDeviations);
            float proposedLocalDeviation = proposedDeviations[v];

            // print("Total " + currentTotalDeviation + " --> " + proposedTotalDeviation);
            // print("Local " + currentLocalDeviation + " --> " + proposedLocalDeviation);

            // Determine acceptance of proposed change
            float acceptProbability = 1;
            if (proposedTotalDeviation > currentTotalDeviation) {
                acceptProbability = Mathf.Exp(-(proposedTotalDeviation - currentTotalDeviation) / temperature);
            }
            // if (proposedLocalDeviation > currentLocalDeviation) {
            //     acceptProbability = Mathf.Exp(-(proposedLocalDeviation - currentLocalDeviation) / temperature);
            // }
            bool accept = acceptProbability > UnityEngine.Random.Range(0.0f, 1.0f);
            // print("Accept: " + accept);
            if (accept) {
                // Update the mesh
                optimizedMesh.SetVertices(proposedVertices);
                optimizedMesh.SetNormals(proposedMesh.normals);
                currentTotalDeviation = proposedTotalDeviation;

                // Update relative vertices
                foreach (int vi in identicalVertices) {
                    optimizedVertices[vi] = proposedVertices[vi];
                    optimizedAdjustmentDistances[vi] = proposedDistance;
                    optimizedAngularDeviationsMap[vi] = proposedDeviations[vi];
                }
                optimizedAngularDeviations = proposedDeviations;
                // Resort the deviations
                sortedOptimizedAngularDeviations = optimizedAngularDeviationsMap.ToList();
                sortedOptimizedAngularDeviations.Sort(SortFunctions.largeToSmallValueSorter);

                // Reset the skipping
                // skipAmount = 0;
            } else {
                // skipAmount++;
            }

            // Savedata
            saveData.AcceptedIterations.Add(accept);
            saveData.Offsets.Add(proposedOffset);
            saveData.Temperatures.Add(temperature);
            saveData.CurrentDeviations.Add(currentTotalDeviation);
            saveData.OptimizedDeviations.Add(proposedTotalDeviation);
            saveData.ChosenVertices.Add(v);

            return 0;
        };
    }

    public void Smoothen() {
        Debug.Log("Smoothing");
        // Initialize the smooth mesh
        smoothedMesh = new Mesh();
        smoothedVertices = (Vector3[]) optimizedVertices.Clone();
        smoothedNormals = (Vector3[]) optimizedNormals.Clone();
        smoothedMesh.SetVertices(smoothedVertices);
        smoothedMesh.SetTriangles(optimizedMesh.triangles, 0);
        smoothedMesh.SetUVs(3, originalNormals);


        // Initialize the smoothing function
        switch (smoothingMethod) {
            case SmoothingMethod.Laplacian:
                Status = OptimizerStatus.SmoothingLaplacian;
                smoothingStepFunction = SmoothLaplacian();
                break;
        }

        // Save data
        saveData.Smoothened = true;
        saveData.SmoothingMethod = smoothingMethod;

        // Start the enumerator
        StartCoroutine(SmoothingIterative());
    }

    private IEnumerator SmoothingIterative() {
        for (int i = 0; i < smoothingSteps; i++) {
            smoothingStepFunction(i);
            yield return null;
        }
        // Recalculate normals after smoothing
        RecalculateNormals(smoothedMesh, useSmoothShading);

        // Calculate final deviation and save it
        smoothenedAngularDeviations = CalculateAngularDeviation(smoothedNormals, originalNormals, reflectedNormal: deformationMethod == DeformationMethod.Mirror);
        saveData.SmoothenedAngularDeviation = Enumerable.Sum(smoothenedAngularDeviations);
        Debug.Log("Angular deviation: " + saveData.SmoothenedAngularDeviation);

        // Set the status
        Status = OptimizerStatus.Smoothed;
    }

    private Func<int, int> SmoothLaplacian() {
        smoothingSteps = smoothedVertices.Length;
        return (int i) => {
            Debug.Log("Smoothing vertex " + i);
            return 0;
        };
    }

    public void Save() {
        if (Status != OptimizerStatus.None && saveData != null) {
            Debug.Log("Saved as " + saveData.FileName());
            saveData.Save();
        } else {
            Debug.LogError("Failed to save. Status: " + Status.ToString());
        }
    }

    public void Reset() {
        Debug.Log("Resetting");
        // Save settings
        saveData = null;

        // Status: Initialized -- variables
        originalMesh = null;
        originalVertices = null;
        originalNormals = null;
        adjustmentRayOrigins = null;
        adjustmentRays = null;
        originalAdjustmentDistances = null;

        // Status: Deformed -- variables
        deformedMesh = null;
        GetComponent<MeshFilter>().sharedMesh = null;
        deformedVertices = null;
        deformedNormals = null;
        deformedAdjustmentDistances = null;
        deformedAngularDeviations = null;

        // Status: Optimized -- variables
        optimizedMesh = null;
        optimizedVertices = null;
        optimizedNormals = null;
        optimizedAdjustmentDistances = null;
        optimizedAngularDeviations = null;
        offsetTotalDeviationMap = null;
        optimizerStepFunction = null;

        // Status: Smoothed -- variables
        smoothedMesh = null;
        smoothedVertices = null;
        smoothedNormals = null;
        smoothingStepFunction = null;
        smoothingSteps = 0;
        smoothenedAngularDeviations = null;

        // Update status
        Status = OptimizerStatus.None;
        SwitchMesh();
    }

    public void SwitchMesh() {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        switch (Status) {
            case OptimizerStatus.None:
                meshFilter.sharedMesh = null;
                break;
            case OptimizerStatus.Initialized:
                meshFilter.sharedMesh = originalMesh;
                break;
            case OptimizerStatus.Deformed:
                meshFilter.sharedMesh = deformedMesh;
                break;
            case OptimizerStatus.Optimized:
            case OptimizerStatus.OptimizingManual:
            case OptimizerStatus.OptimizingAll:
                meshFilter.sharedMesh = optimizedMesh;
                break;
            case OptimizerStatus.SmoothingLaplacian:
            case OptimizerStatus.Smoothed:
                meshFilter.sharedMesh = smoothedMesh;
                break;
        }
    }

    // HELPER METHODS
    private static void RecalculateNormals(Mesh mesh, bool useSmoothShading) {

        if (!useSmoothShading) {
            mesh.RecalculateNormals();
        } else {
            // Calculate triangle normals and add them to the vertices that make it up (and their identicals)
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            List<int>[] vertexIdentityMap = CreateVertexIdentityMap(vertices);
            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < triangles.Length / 3; i++) {
                int a = triangles[3 * i];
                int b = triangles[3 * i + 1];
                int c = triangles[3 * i + 2];
                Vector3 va = vertices[a];
                Vector3 vb = vertices[b];
                Vector3 vc = vertices[c];
                // Unity has clockwise winding order, so to get the normal facing outward, take the cross product of AB and AC
                Vector3 normal = Vector3.Cross(vb - va, vc - va).normalized;

                // Find all the equivs of each vertex
                HashSet<int> verticesToAddNormalTo = new();
                verticesToAddNormalTo.AddRange(vertexIdentityMap[a]);
                verticesToAddNormalTo.AddRange(vertexIdentityMap[b]);
                verticesToAddNormalTo.AddRange(vertexIdentityMap[c]);
                foreach (int vi in verticesToAddNormalTo) {
                    normals[vi] += normal;
                }

            }
            // Normalize the normals
            for (int i = 0; i < normals.Length; i++) {
                normals[i].Normalize();
            }
            mesh.SetNormals(normals);
        }
    }

    private static Dictionary<Vector3, List<int>> GroupVerticesByLocation(Vector3[] vertices) {
        // Group vertices by position
        Dictionary<Vector3, List<int>> verticesByPosition = new();
        for (int i = 0; i < vertices.Length; i++) {
            Vector3 position = vertices[i];
            // If position is new, add it to the map
            if (!verticesByPosition.ContainsKey(position)) {
                verticesByPosition.Add(position, new());
            }
            // Add vertex index to grouping
            verticesByPosition[position].Add(i);
        }
        return verticesByPosition;
    }

    private static List<int>[] CreateVertexIdentityMap(Vector3[] vertices) {
        // Group vertices by position
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(vertices);

        // Assign the list of identical vertices to each vertex in the list
        List<int>[] vertexIdentityMap = new List<int>[vertices.Length];
        foreach (List<int> identicalVertices in verticesByPosition.Values) {
            foreach (int i in identicalVertices) {
                vertexIdentityMap[i] = new List<int>(identicalVertices);
            }
        }
        return vertexIdentityMap;
    }

    private static float[] CalculateAngularDeviation(Vector3[] normals1, Vector3[] normals2, bool reflectedNormal = false) {
        if (normals1.Length != normals2.Length) {
            throw new ArgumentException("Arrays should have same length");
        }
        float[] angularDeviations = new float[normals1.Length];
        for (int i = 0; i < angularDeviations.Length; i++) {
            Vector3 normal1 = normals1[i];
            if (reflectedNormal) normal1.z *= -1;
            angularDeviations[i] = Vector3.Angle(normal1, normals2[i]);
        }
        return angularDeviations;
    }

    // DEBUG METHODS
#if UNITY_EDITOR
    private void DrawInitializedGizmos() {
        // Every debug feature that requires Initialized or higher status
        if (Status < OptimizerStatus.Initialized) return;
        Gizmos.color = GIZMOS_INITIALIZED_COLOR;

        if (showOriginalVertices) {
            for (int i = 0; i < originalVertices.Length; i++) {
                if (i == selectedVertex) continue;
                Gizmos.DrawSphere(originalVertices[i], GIZMO_SPHERE_RADIUS);
            }
            Gizmos.color = GIZMOS_SELECTED_COLOR;
            Gizmos.DrawSphere(originalVertices[selectedVertex], GIZMO_SPHERE_RADIUS);
            Gizmos.color = GIZMOS_INITIALIZED_COLOR;
        }
        if (showOriginalNormals) {
            for (int i = 0; i < originalNormals.Length; i++) {
                Gizmos.DrawLine(originalVertices[i], originalVertices[i] + originalNormals[i]);
            }
        }
        if (showAdjustmentRays) {
            for (int i = 0; i < adjustmentRays.Length; i++) {
                Gizmos.DrawLine(adjustmentRayOrigins[i], adjustmentRayOrigins[i] + (adjustmentRaysScale * adjustmentRays[i]));
            }
        }
    }

    private void DrawDeformedGizmos() {
        // Every debug feature that requires Deformed or higher status
        if (Status < OptimizerStatus.Deformed) return;
        Gizmos.color = GIZMOS_DEFORMED_COLOR;
        if (showDeformedVertices) {
            for (int i = 0; i < deformedVertices.Length; i++) {
                if (i == selectedVertex) continue;
                Gizmos.DrawSphere(deformedVertices[i], GIZMO_SPHERE_RADIUS * 1.5f);
            }
            Gizmos.color = GIZMOS_SELECTED_COLOR;
            Gizmos.DrawSphere(deformedVertices[selectedVertex], GIZMO_SPHERE_RADIUS * 1.5f);
            Gizmos.color = GIZMOS_DEFORMED_COLOR;
        }
        if (showDeformedNormals) {
            for (int i = 0; i < deformedNormals.Length; i++) {
                Gizmos.DrawLine(deformedVertices[i], deformedVertices[i] + deformedNormals[i]);
            }
        }
    }

    private void DrawOptimizedGizmos() {
        // Every debug feature that requires Optimized or higher status
        if (Status < OptimizerStatus.Optimized) return;
        Gizmos.color = GIZMOS_OPTIMIZED_COLOR;
        if (showOptimizedVertices) {
            for (int i = 0; i < optimizedVertices.Length; i++) {
                if (i == selectedVertex) continue;
                Gizmos.DrawSphere(optimizedVertices[i], GIZMO_SPHERE_RADIUS * 1.5f);
            }
            Gizmos.color = GIZMOS_SELECTED_COLOR;
            Gizmos.DrawSphere(optimizedVertices[selectedVertex], GIZMO_SPHERE_RADIUS * 1.5f);
            Gizmos.color = GIZMOS_OPTIMIZED_COLOR;
        }
        if (showOptimizedNormals) {
            for (int i = 0; i < optimizedNormals.Length; i++) {
                Gizmos.DrawLine(optimizedVertices[i], optimizedVertices[i] + optimizedNormals[i]);
            }
        }
        if (showOffsetDeviationMap) {
            foreach ((float offset, float deviation) in offsetTotalDeviationMap) {
                Gizmos.DrawSphere(new Vector3(offset, deviation / 20, 10), GIZMO_SPHERE_RADIUS * 3);
            }
        }
    }

    private void OnDrawGizmos() {
        DrawInitializedGizmos();
        DrawDeformedGizmos();
        DrawOptimizedGizmos();
    }

    // INPUT CHECKER
    private void OnValidate() {
        // Object selection
        if (originalObjectIndex < 0) originalObjectIndex = 0;
        if (originalObjectIndex >= originalObjects.Length) originalObjectIndex = originalObjects.Length - 1;
        for (int i = 0; i < originalObjects.Length; i++) {
            bool activate = showOriginalObject && i == originalObjectIndex;
            originalObjects[i].gameObject.SetActive(activate);
        }
        // Iteration selection
        if (iterations < 1) iterations = 1;
        if (iterations > MAX_ITERATIONS) iterations = MAX_ITERATIONS;
        // Vertex selection
        if (originalMesh != null && selectedVertex >= originalMesh.vertexCount) selectedVertex = originalMesh.vertexCount - 1;
        if (selectedVertex < 0) selectedVertex = 0;
        // Optimization offset range selection
        if (minOptimizeOffset >= maxOptimizeOffset) minOptimizeOffset = maxOptimizeOffset - optimizeOffsetStep;
        // Optimization offset step selection
        if (optimizeOffsetStep < MINIMUM_OPTIMIZE_OFFSET_STEP) optimizeOffsetStep = MINIMUM_OPTIMIZE_OFFSET_STEP;

        // Switch material
        if (useNormalDiffShader) {
            GetComponent<MeshRenderer>().material = normalDiffMaterial;
        } else {
            GetComponent<MeshRenderer>().material = defaultMaterial;
        }

        // Change whether the normal diff shader uses flipped normals or not
        if (deformationMethod == DeformationMethod.Mirror) {
            normalDiffMaterial.SetInteger(NORMAL_SHADER_FLIP_PROP_NAME, 1);
        } else {
            normalDiffMaterial.SetInteger(NORMAL_SHADER_FLIP_PROP_NAME, 0);
        }
    }
#endif

    // Save data class
    [Serializable]
    private class VertexNormalOptimizerData {
        // Static data
        public string ObjectName;
        public int TimeMilliseconds;
        public int Seed;
        public int VertexCount;
        public float DeformedAngularDeviation;
        public DeformationMethod DeformationMethod;
        public string VertexSelectionMethod;
        public OptimizerMethod OptimizerMethod;
        public float SamplingRate;
        public bool Smoothened;
        public SmoothingMethod SmoothingMethod;
        public float SmoothenedAngularDeviation;
        // Iteration data
        public List<int> ChosenVertices;
        public List<float> Temperatures;
        public List<float> Offsets;
        public List<bool> AcceptedIterations;
        // Deviations
        public List<float> OptimizedDeviations;
        public List<float> CurrentDeviations;
        // Misc info
        public float[] IdealNormalAnglesFromRay;
        // Final mesh
        public Vector3[] FinalVertices;
        public int[] Triangles;

        public VertexNormalOptimizerData() {
            Offsets = new();
            Temperatures = new();
            OptimizedDeviations = new();
            CurrentDeviations = new();
            ChosenVertices = new();
            AcceptedIterations = new();
            TimeMilliseconds = 0;
        }

        public string FileName() {
            string filename = ObjectName.Replace(' ', '_');
            filename += "_" + DeformationMethod.ToString();
            filename += "_" + ChosenVertices.Count + "-" + OptimizerMethod.ToString();
            if (OptimizerMethod == OptimizerMethod.Iterative) {
                filename += "_at_" + SamplingRate.ToString("0.000");
            }
            filename += "_in_" + Enumerable.Min(Offsets).ToString("0.00") + "_to_" + Enumerable.Max(Offsets).ToString("0.00");
            if (OptimizerMethod == OptimizerMethod.Annealing) {
                filename += "_temp_" + Temperatures[0].ToString("0.00") + "_to_" + Temperatures[^1].ToString("0.00");
            }
            if (Smoothened) {
                filename += "_" + SmoothingMethod.ToString() + "_";
            }
            return filename;
        }

        public void Save() {
            string jsonString = JsonUtility.ToJson(this);
            string path = "./" + FileName() + ".json";
            System.IO.File.WriteAllText(path, jsonString);
        }
    }
}
