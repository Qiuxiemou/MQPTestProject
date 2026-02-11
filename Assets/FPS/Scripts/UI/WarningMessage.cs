//using UnityEngine;
//using Unity.FPS.AI;
//using TMPro;

//public class WarningMessage : MonoBehaviour
//{
//    public TextMeshProUGUI message;
//    public EnemyController enemyConrtoller;

//    void Start()
//    {
//        HideText();
//    }

//    void FixedUpdate()
//    {
//        if (enemyConrtoller.IsSeeingTarget)
//        {
//            ShowText();
//        }
//        else
//        {
//            HideText();
//        }
//    }

//    void HideText()
//    {
//        Color c = tmpText.color;
//        c.a = 0f; // fully transparent
//        message.color = c;
//    }

//    void ShowText()
//    {
//        Color c = tmpText.color;
//        c.a = 255f; // fully opaque
//        message.color = c;
//    }
//}
