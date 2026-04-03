using UnityEngine;

/// <summary>
/// 씬 로드 시 플레이어를 SpawnPoint 위치로 이동
/// </summary>
public class PlayerSpawn : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject player;

    private void Start()
    {
        if (spawnPoint == null || player == null) return;

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = spawnPoint.position;
        player.transform.rotation = spawnPoint.rotation;

        if (cc != null) cc.enabled = true;
    }
}