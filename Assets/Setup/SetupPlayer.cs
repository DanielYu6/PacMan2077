using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class SetupPlayer : MonoBehaviour
{
    public  static Mesh mesh;
    public  static Material playerMaterial;
    public Mesh setMesh;
    public Material setMaterial;
    public static List<float3> startPositions = new List<float3>();
    // Start is called before the first frame update
    void Start()
    {
        mesh = setMesh;
        playerMaterial = setMaterial;
    }
}
public struct Player : IComponentData {}
