using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PiUIButton : MonoBehaviour
{
    public TextMeshProUGUI ipText; 
    public Button connectButton; 
    public Button selectButton;
    public Image background; 
    public TextMeshProUGUI selectButtonText;
    public Image connectionIndicator; 

    public void Select()
    {
        background.color = Color.green;
    }

    public void Deselect()
    {
        background.color = Color.white;
    }

    public void UpdateButtonText(bool isSelected)
    {
        selectButtonText.text = isSelected ? "Unselect" : "Select";
    }

    public void UpdateConnectionStatus(bool isConnected)
    {
        Debug.Log($"Updating connection status: {isConnected}");
        connectionIndicator.color = isConnected ? Color.green : Color.red;
    }
}