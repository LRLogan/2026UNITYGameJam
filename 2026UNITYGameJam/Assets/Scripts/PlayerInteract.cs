using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerCore))]
public class PlayerInteract : MonoBehaviour
{
    private LevelManager level;

    internal void Bind(LevelManager levelManager)
    {
        level = levelManager;
    }

    public void OnInteract(InputAction.CallbackContext ctx)
    {
        // Pick up on the press, including when the input action has a Hold interaction.
        if (ctx.started && isActiveAndEnabled && level != null)
            level.TryCollect();
    }
}
