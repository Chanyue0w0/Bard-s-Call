using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterComboState : MonoBehaviour
{
    [Tooltip("�ثe�����q�ơ]1~4�^")]
    public int currentPhase = 1;

    [Tooltip("�W�������ɶ��A�Ω�P�_�O�_���m�s�q")]
    public float lastAttackTime = 0f;

    [Tooltip("�֭p������������")]
    public int comboCount = 0; // �� �֭p������������
}
