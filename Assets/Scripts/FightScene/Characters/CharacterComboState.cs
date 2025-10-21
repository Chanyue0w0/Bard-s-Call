using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterComboState : MonoBehaviour
{
    [Tooltip("目前攻擊段數（1~4）")]
    public int currentPhase = 1;

    [Tooltip("上次攻擊時間，用於判斷是否重置連段")]
    public float lastAttackTime = 0f;

    [Tooltip("累計完美攻擊次數")]
    public int comboCount = 0; // ★ 累計完美攻擊次數
}
