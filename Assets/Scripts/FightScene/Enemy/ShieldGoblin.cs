using UnityEngine;

public class ShieldGoblin : MonoBehaviour
{
    [Header("���m���A")]
    public bool isBlocking = true;   // �O�_�B�󨾿m��
    public bool isBroken = false;    // �O�_�w�Q�}��

    private CharacterData charData;

    void Start()
    {
        charData = GetComponent<CharacterData>();
        if (charData == null)
        {
            Debug.LogWarning("ShieldGoblin �ʤ� CharacterData �ե�C");
            return;
        }

        // �Ұʥä[����
        BattleEffectManager.Instance.ActivateInfiniteBlock(gameObject, charData);
        Debug.Log("�iShieldGoblin�j�Ұʥä[���ɪ��A");
    }

    // �Q�������R���ɩI�s
    public void BreakShield()
    {
        if (isBroken) return;

        isBroken = true;
        isBlocking = false;

        // �������ɯS��
        BattleEffectManager.Instance.RemoveBlockEffect(gameObject);

        // �i��G����}���S�ĩΰʵe
        Debug.Log("�iShieldGoblin�j���m�Q�������}�a�I");
    }

    // �i��G���ѥ~���d��
    public bool IsBlocking()
    {
        return isBlocking && !isBroken;
    }
}
