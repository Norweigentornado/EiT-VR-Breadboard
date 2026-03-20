using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SocketHoverHighlight : MonoBehaviour
{
    [SerializeField] private Color highlightColor = Color.cyan;
    [SerializeField] private Color occupiedColor = Color.red;
    [SerializeField] private Color defaultColor = Color.white;

    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;
    private Renderer socketRenderer;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        socketRenderer = GetComponentInChildren<Renderer>();

        if (socketRenderer == null)
        {
            Debug.LogError($"No Renderer found on {gameObject.name} or its children!", gameObject);
            return; // Stop here to avoid further null errors
        }

        propBlock = new MaterialPropertyBlock();
        socket.hoverEntered.AddListener(OnHoverEnter);
        socket.hoverExited.AddListener(OnHoverExit);
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        // Don't highlight if socket is already occupied
        if (socket.hasSelection)
        {
            SetColor(occupiedColor);
        }
        else
        {
            SetColor(highlightColor);
        }
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        if (!socket.hasSelection)
            SetColor(defaultColor);
    }

    private void SetColor(Color color)
    {
        socketRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", color); // URP
        // propBlock.SetColor("_Color", color);  // Built-in RP
        socketRenderer.SetPropertyBlock(propBlock);
    }
}