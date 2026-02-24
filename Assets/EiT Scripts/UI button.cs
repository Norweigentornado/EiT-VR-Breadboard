using UnityEngine;

public class UIbutton : MonoBehaviour
{
    public GameObject uiPanel;

    private void OnTriggerEnter(Collider other)
    {
        
            uiPanel.SetActive(true);
       
    }
}
