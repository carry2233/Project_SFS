/*using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Inventory/Slot/Resource Slot")]
public class Slot_Resource : MonoBehaviour
{
    [SerializeField] private Image iconImage;                       // 아이콘
    [SerializeField] private Text nameText;                         // 이름

    public void Bind(ItemViewData v)                                // 바인딩
    {
        if (iconImage) iconImage.sprite = v.icon;
        if (nameText)  nameText.text  = string.IsNullOrEmpty(v.name) ? "Resource" : v.name;
    }
}
*/