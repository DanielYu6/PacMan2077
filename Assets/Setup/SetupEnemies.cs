using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class SetupEnemies : MonoBehaviour {
    public static Mesh enemyMesh;
    public static Material enemyMaterial;
    public Mesh setMesh;
    public Material setMaterial;
    public static List<float3> startPositions = new List<float3>();
    // Start is called before the first frame update
    void Start() {
        enemyMesh = setMesh;
        enemyMaterial = setMaterial;
    }
}
public struct Enemy : IComponentData {
    public bool isForward;
    public float3 forward;
    public float3 targetPosition;
}