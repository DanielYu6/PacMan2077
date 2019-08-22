using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class PlayerMoveSystem : ComponentSystem {
    private int enemySize;
    private float3 player;
    private Communicator com;
    private EndSimulationEntityCommandBufferSystem endSimCommandBufferSystem;
    private Entity PlayerEntity;
    private float3 direction = float3.zero;
    private float startTime;
    void StartSystem() {
        startTime = Time.time;
        endSimCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        List<Translation> entities = new List<Translation>();
        SpawnCollectibles();
        SpawnPlayer();
    }

    void SpawnCollectibles() {
        SetupCollectible.startPositions.Clear();
        EntityManager entityManager = World.Active.EntityManager;
        EntityArchetype collectibleArchetype = entityManager.CreateArchetype(
           typeof(Translation),
           typeof(RenderMesh),
           typeof(LocalToWorld),
           typeof(Collectible)
           );
        //randompositions check if they are overlapping with the wall if not fill up positions
        while (SetupCollectible.startPositions.Count < com.width * 2) {
            enemySize = com.numberOfEnemies;
            float3 pos = new float3(UnityEngine.Random.Range(com.width / 20, com.width - com.width / 20), 0, UnityEngine.Random.Range(com.width / 20, com.width - com.width / 20));
            int index = (int)(pos.z) * com.width + (int)(pos.x);
            if (!RecursiveBacktracking.hashGrid.ContainsKey(index)) {
                SetupCollectible.startPositions.Add(pos);
                Entity playerEntity = entityManager.CreateEntity(collectibleArchetype);
                entityManager.SetComponentData(playerEntity, new Translation { Value = pos });
                entityManager.SetSharedComponentData(playerEntity, new RenderMesh { mesh = SetupCollectible.mesh, material = SetupCollectible.playerMaterial });

            }
        }
    }
    void SpawnPlayer() {
        SetupPlayer.startPositions.Clear();
        //randompositions check if they are overlapping with the wall if not fill up positions
        while (SetupPlayer.startPositions.Count < 1) {
            float3 pos = new float3(UnityEngine.Random.Range(com.width / 20, com.width - com.width / 20), 0, UnityEngine.Random.Range(com.width / 20, com.width - com.width / 20));
            int index = (int)(pos.z) * com.width + (int)(pos.x);
            if (!RecursiveBacktracking.hashGrid.ContainsKey(index)) {
                SetupPlayer.startPositions.Add(pos);
                EntityManager entityManager = World.Active.EntityManager;
                EntityArchetype playerArchetype = entityManager.CreateArchetype(
                   typeof(Translation),
                   typeof(RenderMesh),
                   typeof(LocalToWorld),
                   typeof(Player)
                   );
                Entity playerEntity = entityManager.CreateEntity(playerArchetype);
                entityManager.SetComponentData(playerEntity, new Translation { Value = pos });
                entityManager.SetSharedComponentData(playerEntity, new RenderMesh { mesh = SetupPlayer.mesh, material = SetupPlayer.playerMaterial });
            }
        }
    }
    protected override void OnUpdate() {
        Entities.ForEach((Entity e, ref PlayerControl pc) => {
            if (!pc.initialized) {
                Entities.ForEach((ref Communicator Com) => {
                    com = Com;
                });
                StartSystem();
                EntityManager.SetComponentData(e, new PlayerControl { run = true, initialized = true });
            }
            if (pc.run) {
                //player movement
                if (Input.GetKeyDown(KeyCode.W)) {
                    direction = Vector3.forward;
                }
                if (Input.GetKeyDown(KeyCode.A)) {
                    direction = -Vector3.right;
                }
                if (Input.GetKeyDown(KeyCode.D)) {
                    direction = Vector3.right;
                }
                if (Input.GetKeyDown(KeyCode.S)) {
                    direction = -Vector3.forward;
                }
                if (Input.GetKeyDown(KeyCode.Backspace)) {
                    BackToMenu();
                }
                //check if can move to the position if so move and check if it has a collectible as well
                Entities.ForEach((ref Player p, ref Translation playerTranslation) => {
                    SmoothFollow.target = playerTranslation.Value;
                    if (!(direction.x == 0 && direction.z == 0)) {
                        int index = (int)(playerTranslation.Value.z + direction.z) * com.width + (int)(playerTranslation.Value.x + direction.x);
                        if (!RecursiveBacktracking.hashGrid.ContainsKey(index)) {
                            player = playerTranslation.Value + direction;
                            playerTranslation.Value = player;
                        }
                        NativeArray<bool> result = new NativeArray<bool>(1, Allocator.TempJob);
                        Collect collect = new Collect {
                            CommandBuffer = endSimCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                            result = result,
                            x = (int)player.x,
                            z = (int)player.z
                        };
                        JobHandle collectJobHandle = collect.Schedule(this);
                        collectJobHandle.Complete();
                        if (result[0]) {
                            SetupCollectible.PlayCollect();
                        }
                        result.Dispose();
                    }
                    direction = float3.zero;
                    if (Time.time - startTime > 3) {
                        //don't let the player die in the first 3 sec so if it spawn in a bad position it has a chance
                        CheckEnemy();
                    }
                });
            }
        });
    }
    void CheckEnemy() {
        NativeArray<bool> result = new NativeArray<bool>(1, Allocator.TempJob);
        result[0] = false;
        CatchJob catchJob = new CatchJob {
            x = (int)player.x,
            z = (int)player.z,
            result = result
        };
        JobHandle jobHandle = catchJob.Schedule(this);
        jobHandle.Complete();
        if (result[0]) {
            DestroyThemAll destroyJob = new DestroyThemAll {
                CommandBuffer = endSimCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
            };
            JobHandle destroyJobHandle = destroyJob.Schedule(this);
            destroyJobHandle.Complete();
            RecursiveBacktracking.StaticReset();
            MenuController.LoadMazeStatic();
        }
        result.Dispose();
    }
    void BackToMenu() {
        DestroyThemAll destroyJob = new DestroyThemAll {
            CommandBuffer = endSimCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };
        JobHandle destroyJobHandle = destroyJob.Schedule(this);
        destroyJobHandle.Complete();
        RecursiveBacktracking.StaticReset();
        MenuController.LoadMenuStatic();
    }
    [BurstCompile]
    private struct DestroyThemAll : IJobForEachWithEntity<Translation> {
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public void Execute(Entity entity, int index, [ReadOnly]ref Translation trans) {
            CommandBuffer.DestroyEntity(index, entity);
        }
    }

    [BurstCompile]
    private struct CatchJob : IJobForEachWithEntity<Translation, Enemy> {
        [ReadOnly] public int x, z;
        [NativeDisableParallelForRestriction] public NativeArray<bool> result;
        public void Execute(Entity entity, int index, [ReadOnly]ref Translation translation, [ReadOnly]ref Enemy enemy) {
            //float dist = math.sqrt(math.exp2(translation.Value.x - x) + math.exp2(translation.Value.z - z)); this would not work for some reason idk
            if (math.abs(translation.Value.x - x) < 1 && math.abs(translation.Value.z - z) < 1) {
                result[0] = true;
            }

        }
    }

    [BurstCompile]
    private struct Collect : IJobForEachWithEntity<Translation, Collectible> {
        public EntityCommandBuffer.Concurrent CommandBuffer;
        [NativeDisableParallelForRestriction] public NativeArray<bool> result;
        [ReadOnly] public int x, z;
        public void Execute(Entity entity, int index, ref Translation translation, [ReadOnly] ref Collectible collectible) {
            if (translation.Value.x == x && translation.Value.z == z) {
                CommandBuffer.DestroyEntity(index, entity);
                result[0] = true;

            }
        }
    }
    [BurstCompile]
    public struct CanMoveToJob : IJobParallelFor {
        public NativeArray<float3> positionArray;
        public int x, z;
        [NativeDisableParallelForRestriction] public NativeArray<bool> result;
        public void Execute(int index) {
            if (positionArray[index].x == x && positionArray[index].z == z) {
                result[0] = false;
                return;
            }
        }
    }

}
