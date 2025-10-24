using UnityEngine;

public class ShieldGoblin : EnemyBase
{
    [Header("���m���A")]
    public bool isBlocking = true;
    public bool isBroken = false;

    private CharacterData charData;

    protected override void Awake()
    {
        base.Awake(); // �� �۰ʰt�����
    }

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

    public void BreakShield()
    {
        if (isBroken) return;

        isBroken = true;
        isBlocking = false;
        BattleEffectManager.Instance.RemoveBlockEffect(gameObject);
        Debug.Log("�iShieldGoblin�j���m�Q�������}�a�I");
    }

    public bool IsBlocking()
    {
        return isBlocking && !isBroken;
    }

    //protected override void OnBeat()
    //{
    //    if (forceMove) return; // �� �|�a�����ʧ@
    //    // �i�[�I�l�ʵe�ίS��
    //}
}
