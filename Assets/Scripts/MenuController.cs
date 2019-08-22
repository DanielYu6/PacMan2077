using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    public Slider size, enemies;
    public Text sizeText, enemyText;    
    public static void LoadMenuStatic() {
        SceneManager.LoadScene(0);
    }
    public static void LoadMazeStatic() {
        SceneManager.LoadScene(1);
    }
    public void StartGame() {
        PlayerPrefs.SetInt("Size", (int)size.value);
        PlayerPrefs.SetInt("Enemies", (int)enemies.value);
        SceneManager.LoadScene(1);
    }
    public void LoadMenu() {
        SceneManager.LoadScene(0);
    }
    public void SetSizeText() {
        sizeText.text = size.value.ToString();
    }
    public void SetEnemyText() {
        enemyText.text = enemies.value.ToString();
    }
    
}
