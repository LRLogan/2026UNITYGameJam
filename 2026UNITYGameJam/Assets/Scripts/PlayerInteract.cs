using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerCore))]
public class PlayerInteract : MonoBehaviour
{
    private PlayerCore player;
    private LevelManager level;

    private void Awake()
    {
        player = GetComponent<PlayerCore>();
    }

    internal void Bind(LevelManager levelManager)
    {
        level = levelManager;
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed && isActiveAndEnabled && level != null)
            level.TryCollect(player);
    }
}
