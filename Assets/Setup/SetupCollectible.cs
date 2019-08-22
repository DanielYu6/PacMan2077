using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class SetupCollectible : MonoBehaviour
{
    public static Mesh mesh;
    public static Material playerMaterial;
    public Mesh setMesh;
    public Material setMaterial;
    public static List<float3> startPositions = new List<float3>();
    public static AudioSource staticASource;
    void Start() {
        mesh = setMesh;
        playerMaterial = setMaterial;
        staticASource = GetComponent<AudioSource>();
    }
    public static void PlayCollect() {
        staticASource.Play();
    }
}
public struct Collectible: IComponentData {}