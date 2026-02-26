using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRShelfSpawner : MonoBehaviour
{
    public GameObject itemPrefab;
    public Transform spawnPoint;

    private GameObject currentItem;

    void Start()
    {
        SpawnItem();
    }



    void SpawnItem()
    {
        currentItem = Instantiate(itemPrefab, spawnPoint.position, spawnPoint.rotation);

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = currentItem.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.selectEntered.AddListener(OnItemGrabbed);
    }


    void OnItemGrabbed(SelectEnterEventArgs args)
    {
        SpawnItem();
    }
}

