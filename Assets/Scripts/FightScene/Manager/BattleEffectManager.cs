using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleEffectManager : MonoBehaviour
{
    public static BattleEffectManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [Header("玩家統一血條（場景中已有，不要生成）")]
    public TotalPlayerHealthBarUI playerTotalHPUI;

    [Header("共用 Shield 特效（當角色未指定 ShieldEffectPrefab 時使用）")]
    public GameObject shieldVfxPrefab;
    private GameObject[] blockEffects = new GameObject[3];
    [Header("格檔成功特效")]
    public GameObject blockSuccessVfxPrefab;

    [Header("Priest 回復特效")]
    public GameObject healVfxPrefab;

    // 每位角色的格檔狀態與協程追蹤
    private bool[] isBlocking = new bool[3];
    private Coroutine[] blockCoroutines = new Coroutine[3];

    public bool isHeavyAttack = false;

    [Header("Paladin 嘲諷效果特效")]
    public GameObject tauntVfxPrefab;
    // -------------------------
    // 法師充電特效管理
    // -------------------------
    [Header("Mage 充電特效")]
    public GameObject mageChargeVfxPrefab; // 指定法師身上的充電特效Prefab
    private Dictionary<BattleManager.TeamSlotInfo, GameObject> mageChargeEffects = new();
    // -------------------------
    // 法師充電層數紀錄
    // -------------------------
    private Dictionary<BattleManager.TeamSlotInfo, int> mageChargeStacks = new Dictionary<BattleManager.TeamSlotInfo, int>();

    public int GetChargeStacks(BattleManager.TeamSlotInfo mage)
    {
        if (mage == null) return 0;
        return mageChargeStacks.ContainsKey(mage) ? mageChargeStacks[mage] : 0;
    }

    public void AddChargeStack(BattleManager.TeamSlotInfo mage)
    {
        if (mage == null) return;
        if (!mageChargeStacks.ContainsKey(mage))
            mageChargeStacks[mage] = 0;

        mageChargeStacks[mage]++;
        // 限制上限為6層
        mageChargeStacks[mage] = Mathf.Min(mageChargeStacks[mage], 6);

        // 若首次充能，生成特效
        if (mage.Actor != null && mageChargeVfxPrefab != null)
        {
            // 若特效不存在或已被銷毀，重新生成
            if (!mageChargeEffects.ContainsKey(mage) || mageChargeEffects[mage] == null)
            {
                Vector3 spawnPos = mage.Actor.transform.position;
                var effect = Instantiate(mageChargeVfxPrefab, spawnPos, Quaternion.identity);
                mageChargeEffects[mage] = effect;
                Debug.Log($"【充電特效生成】於 {mage.UnitName} 位置 {spawnPos}");
            }
        }

        // 更新 HeavyAttackBarUI 顯示
        var bar = mage.Actor.GetComponentInChildren<HeavyAttackBarUI>();
        if (bar != null)
        {
            bar.UpdateComboCount(mageChargeStacks[mage]);
        }

        // 同步 comboState，讓 UI 的 Update() 下一幀維持亮燈
        var combo = mage.Actor.GetComponent<CharacterComboState>();
        if (combo != null)
        {
            combo.comboCount = mageChargeStacks[mage];
            combo.currentPhase = mageChargeStacks[mage]; // 若你 UI 依 phase 顯示可一併更新
        }


        Debug.Log($"【充電增加】{mage.UnitName} 現在 {mageChargeStacks[mage]} 層。");

    }

    public void ResetChargeStacks(BattleManager.TeamSlotInfo mage)
    {
        if (mage == null) return;

        // 歸零層數
        if (mageChargeStacks.ContainsKey(mage))
            mageChargeStacks[mage] = 0;

        // 移除特效
        if (mageChargeEffects.ContainsKey(mage) && mageChargeEffects[mage] != null)
        {
            Destroy(mageChargeEffects[mage]);
            mageChargeEffects.Remove(mage);
        }

        // 更新 HeavyAttackBarUI 顯示歸零
        if (mage.Actor != null)
        {
            var bar = mage.Actor.GetComponentInChildren<HeavyAttackBarUI>();
            if (bar != null)
                bar.UpdateComboCount(0);

            // 同步 comboState，確保下一幀不被 Update() 蓋回亮燈
            var combo = mage.Actor.GetComponent<CharacterComboState>();
            if (combo != null)
            {
                combo.comboCount = 0;
                combo.currentPhase = 0;
            }
        }


        Debug.Log($"【充電清除】{mage.UnitName} 充電歸零並移除特效。");
    }


    public void ActivateBlock(int index, float beatDuration, CharacterData charData, GameObject actor)
    {
        if (index < 0 || index >= isBlocking.Length) return;

        // 若已有格檔 → 先停止舊的
        if (blockCoroutines[index] != null)
        {
            StopCoroutine(blockCoroutines[index]);
            blockCoroutines[index] = null;
        }

        // 清理殘留特效
        if (blockEffects[index] != null)
        {
            Destroy(blockEffects[index]);
            blockEffects[index] = null;
        }

        // ===========================================
        // ★ 核心更新：改用 FMODBeatListener2
        // ===========================================
        float secondsPerBeat = 0.5f;

        if (FMODBeatListener2.Instance != null)
            secondsPerBeat = FMODBeatListener2.Instance.SecondsPerBeat;

        // beatDuration 是「幾拍」
        float durationSec = beatDuration * secondsPerBeat;

        // 額外保護（避免跨拍過長）
        float adjustedDuration = durationSec * 0.95f;

        blockCoroutines[index] = StartCoroutine(BlockRoutine(index, adjustedDuration, charData, actor));
    }


    private IEnumerator BlockRoutine(int index, float duration, CharacterData charData, GameObject actor)
    {
        isBlocking[index] = true;
        Debug.Log($"【格檔啟動】角色 {actor.name} 進入無敵狀態 ({duration:F2}s)");

        // -----------------------------
        // 1. 生成位置（改用角色座標）
        // -----------------------------
        Vector3 spawnPos = actor.transform.position;
        Vector3 offset = new Vector3(0f, 0f, 0f);
        spawnPos += offset;

        // -----------------------------
        // 2. 選擇特效
        // -----------------------------
        GameObject effectPrefab = (charData != null && charData.ShieldEffectPrefab != null)
            ? charData.ShieldEffectPrefab
            : shieldVfxPrefab;

        if (effectPrefab != null)
        {
            // -----------------------------
            // ★★★ 生成後立刻 SetParent(actor)
            // -----------------------------
            GameObject effectObj = Instantiate(effectPrefab, spawnPos, Quaternion.identity);

            effectObj.transform.SetParent(actor.transform, worldPositionStays: true);

            // 確保特效位置永遠對齊角色
            effectObj.transform.localPosition = offset;

            blockEffects[index] = effectObj;
        }

        // -----------------------------
        // 3. 持續 duration 秒後結束
        // -----------------------------
        yield return new WaitForSeconds(duration);

        isBlocking[index] = false;

        if (blockEffects[index] != null)
        {
            Destroy(blockEffects[index]);
            blockEffects[index] = null;
        }

        blockCoroutines[index] = null;
        Debug.Log($"【格檔結束】角色 {actor.name} 恢復可受傷");
    }



    // =======================
    // 傷害判定（含重攻擊判定與 ShieldGoblin 破防邏輯）
    // =======================
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect, bool isHeavyAttack = false, int overrideDamage = -1)
    {
        if (attacker == null || target == null) return;

        // =======================================
        // ShieldGoblin 永久防禦邏輯
        // =======================================
        if (target.Actor != null)
        {
            var goblin = target.Actor.GetComponent<ShieldGoblin>();
            if (goblin != null)
            {
                // 若格檔中且未破防
                if (goblin.IsBlocking())
                {
                    // 若是重攻擊 → 破防
                    if (isHeavyAttack)
                    {
                        goblin.BreakShield();
                        Debug.Log($"【破防成功】{attacker.UnitName} 的重攻擊打破 {target.UnitName} 的防禦！");
                    }
                    else
                    {
                        Debug.Log($"【格檔成功】{target.UnitName} 擋下 {attacker.UnitName} 的攻擊！");
                        return; // 不受傷害
                    }
                }
            }
            var darkKnight = target.Actor.GetComponent<DarkLongSwordKnight>();
            if (darkKnight != null)
            {
                // 若 Boss 有護盾且未破壞
                if (darkKnight.isShieldActive && !darkKnight.isShieldBroken)
                {
                    if (isHeavyAttack)
                    {
                        darkKnight.BreakShield();
                        Debug.Log($"【破防成功】{attacker.UnitName} 的重攻擊打破 DarkLongSwordKnight 的防禦！");
                    }
                    else
                    {
                        Debug.Log($"【格檔成功】DarkLongSwordKnight 擋下 {attacker.UnitName} 的攻擊！");
                        return; // 擋下攻擊，不造成傷害
                    }
                }
            }
        }

        // =======================================
        // 玩家方格檔判定（原有機制）
        // =======================================
        int targetIndex = System.Array.FindIndex(BattleManager.Instance.CTeamInfo, t => t == target);
        if (targetIndex >= 0 && isBlocking[targetIndex])
        {
            Vector3 spawnPos = target.Actor.transform.position;
            Vector3 offset = new Vector3(0f, 1.0f, 0f); // 可調整高度

            GameObject fx = Instantiate(blockSuccessVfxPrefab, spawnPos + offset, Quaternion.identity);

            // 讓特效跟著角色移動
            fx.transform.SetParent(target.Actor.transform, worldPositionStays: true);
            fx.transform.localPosition = offset;

            VibrationManager.Instance.Vibrate("Block");
            Debug.Log($"【格檔成功】{target.UnitName} 格檔 {attacker.UnitName} 的攻擊！");
            return;
        }

        // =======================================
        // 一般傷害計算
        // =======================================
        int finalDamage = (overrideDamage >= 0)
            ? overrideDamage
            : Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * (isPerfect ? 1f : 0f)));

        // ===============================
        // 若是玩家 → 扣全隊共用血量
        // ===============================
        if (targetIndex >= 0)
        {
            GlobalIndex.CurrentTotalHP = Mathf.Max(
                0,
                GlobalIndex.CurrentTotalHP - finalDamage
            );

            Debug.Log($"【玩家受傷】-{finalDamage} → 全隊剩餘 {GlobalIndex.CurrentTotalHP}/{GlobalIndex.MaxTotalHP}");

            // 更新統一血條 UI
            playerTotalHPUI.SetHP(GlobalIndex.CurrentTotalHP, GlobalIndex.MaxTotalHP);

            // 顯示傷害數字
            if (target.Actor != null && DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowDamage(target.Actor.transform, finalDamage);
            }

            // 法師中斷充電
            var data = target.Actor.GetComponent<CharacterData>();
            if (data != null && data.ClassType == BattleManager.UnitClass.Mage)
            {
                ResetChargeStacks(target);
                Debug.Log($"【充電中斷】{target.UnitName} 被攻擊 → 清除層數");
            }

            // 判斷是否全隊死亡
            BattleManager.Instance.CheckPlayerDefeat();
            return;
        }

        // ===============================
        // 若是敵人 → 使用原本邏輯
        // ===============================
        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，傷害={finalDamage} 剩餘HP={target.HP} (Perfect={isPerfect})");

        // Perfect 回魔
        if (isPerfect)
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10);

        // 敵人血條 UI 更新
        var enemyHB = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (enemyHB != null) enemyHB.ForceUpdate();

        // 顯示傷害數字
        if (target.Actor != null && DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowDamage(target.Actor.transform, finalDamage);
        }

        // 檢查死亡
        if (target.HP <= 0)
        {
            HandleUnitDefeated(target);
        }


        BattleManager.Instance.CheckPlayerDefeat();

    }


    private void HandleUnitDefeated(BattleManager.TeamSlotInfo target)
    {
        Debug.Log($"{target.UnitName} 已被擊敗！");

        // 確認是否為敵人
        int enemyIndex = System.Array.FindIndex(BattleManager.Instance.EnemyTeamInfo, t => t == target);
        if (enemyIndex >= 0)
        {
            BattleManager.Instance.OnEnemyDeath(enemyIndex);
            return;
        }

        // 若為我方角色死亡
        int allyIndex = System.Array.FindIndex(BattleManager.Instance.CTeamInfo, t => t == target);
        if (allyIndex >= 0)
        {
            // 之後可加上我方死亡處理（例如解除格檔、播放動畫等）
            if (target.Actor != null)
                Destroy(target.Actor);

            Debug.Log($"我方角色 {target.UnitName} 陣亡！");
        }
    }


    //public void HealTeam(int healAmount)
    //{
    //    var team = BattleManager.Instance.CTeamInfo;
    //    foreach (var ally in team)
    //    {
    //        if (ally != null && ally.Actor != null)
    //        {
    //            ally.HP = Mathf.Min(ally.MaxHP, ally.HP + healAmount);

    //            var hb = ally.Actor.GetComponentInChildren<HealthBarUI>();
    //            if (hb != null) hb.ForceUpdate();

    //            Debug.Log($"{ally.UnitName} 回復 {healAmount} → 現在 HP={ally.HP}");
    //        }
    //    }
    //}

    // =======================
    // 永久格檔支援（敵人專用）
    // =======================
    public void ActivateInfiniteBlock(GameObject actor, CharacterData charData)
    {
        Vector3 spawnPos = actor.transform.position ; //+Vector3.up * 1.3f
        GameObject effectPrefab = (charData != null && charData.ShieldEffectPrefab != null)
            ? charData.ShieldEffectPrefab
            : shieldVfxPrefab;

        if (effectPrefab != null)
        {
            // 生成持續存在的格檔特效
            GameObject effect = Instantiate(effectPrefab, spawnPos, Quaternion.identity);
            effect.transform.SetParent(actor.transform, false);
            effect.transform.localPosition = new Vector3(-0.7f, 1.3f, 0f);


            // 若該特效含 Explosion 腳本，延長壽命為 9999 秒
            var explosion = effect.GetComponent<Explosion>();
            if (explosion != null)
            {
                explosion.SetLifeTime(9999f);
                explosion.SetUseUnscaledTime(true);
                explosion.Initialize();
            }


            Debug.Log($"【永久格檔啟動】{actor.name} 進入防禦狀態（特效維持 9999 秒）");
        }
    }

    // -------------------------
    // 全隊回復（附帶治癒特效）
    // -------------------------
    public void HealTeamWithEffect(int healAmount)
    {
        // 1. 更新共用血量
        GlobalIndex.CurrentTotalHP = Mathf.Min(
            GlobalIndex.MaxTotalHP,
            GlobalIndex.CurrentTotalHP + healAmount
        );

        Debug.Log($"【全隊回復】+{healAmount} → 全隊 {GlobalIndex.CurrentTotalHP}/{GlobalIndex.MaxTotalHP}");

        // 2. 更新統一血條 UI
        if (playerTotalHPUI != null)
            playerTotalHPUI.SetHP(GlobalIndex.CurrentTotalHP, GlobalIndex.MaxTotalHP);

        // 3. 個別角色顯示回復特效 & 綠色數字
        var team = BattleManager.Instance.CTeamInfo;
        foreach (var ally in team)
        {
            if (ally == null || ally.Actor == null) continue;

            // 3-1 顯示綠色 +healAmount
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowHeal(ally.Actor.transform, healAmount);
            }

            // 3-2 生成治癒特效
            if (healVfxPrefab != null)
            {
                Vector3 healPos = ally.Actor.transform.position;
                GameObject healVfx = Instantiate(healVfxPrefab, healPos, Quaternion.identity);
                healVfx.transform.SetParent(ally.Actor.transform, worldPositionStays: true);

                // Explosion 系統自動管理壽命
                var exp = healVfx.GetComponent<Explosion>();
                if (exp != null)
                {
                    exp.SetUseUnscaledTime(true);
                    exp.Initialize();
                }
                else
                {
                    Destroy(healVfx, 1.5f);
                }
            }
        }
    }

    // 手動移除格檔特效（用於破防）
    public void RemoveBlockEffect(GameObject actor)
    {
        var effects = actor.GetComponentsInChildren<Explosion>();
        foreach (var e in effects)
        {
            Destroy(e.gameObject);
        }
        Debug.Log($"【格檔特效解除】{actor.name}");
    }

    // -------------------------
    // 嘲諷效果系統（Paladin 專用）
    // -------------------------
    private class TauntInfo
    {
        public GameObject enemyObj;
        public GameObject paladinObj;
        public int remainingBeats;
    }
    private List<TauntInfo> activeTaunts = new List<TauntInfo>();

    // 新增或刷新嘲諷
    public void ApplyTaunt(GameObject enemyObj, GameObject paladinObj, int durationBeats)
    {
        if (enemyObj == null || paladinObj == null) return;

        // 嘲諷列表檢查
        var existing = activeTaunts.Find(t => t.enemyObj == enemyObj);
        if (existing != null)
        {
            existing.remainingBeats = durationBeats;
            existing.paladinObj = paladinObj;
            Debug.Log($"【嘲諷刷新】{enemyObj.name} 再次被 {paladinObj.name} 嘲諷 ({durationBeats} 拍)");
        }
        else
        {
            activeTaunts.Add(new TauntInfo
            {
                enemyObj = enemyObj,
                paladinObj = paladinObj,
                remainingBeats = durationBeats
            });
            Debug.Log($"【嘲諷施加】{paladinObj.name} 嘲諷 {enemyObj.name} 持續 {durationBeats} 拍");
        }

        // ★ 新增：讓敵人知道自己被誰嘲諷
        var enemyBase = enemyObj.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            // 我們不再用 TeamSlotInfo，直接傳物件進去
            enemyBase.tauntedByObj = paladinObj;
            enemyBase.tauntBeatsRemaining = durationBeats;
            Debug.Log($"【嘲諷指定】{enemyObj.name} 現在鎖定 {paladinObj.name} (持續 {durationBeats} 拍)");
        }

    }

    // 每拍倒數（請由 BeatManager 呼叫）
    public void TickTauntBeats()
    {
        for (int i = activeTaunts.Count - 1; i >= 0; i--)
        {
            activeTaunts[i].remainingBeats--;
            if (activeTaunts[i].remainingBeats <= 0)
            {
                Debug.Log($"【嘲諷結束】{activeTaunts[i].enemyObj.name} 嘲諷結束");
                activeTaunts.RemoveAt(i);
            }
        }
    }

    // 檢查敵人是否被嘲諷中，若是則回傳該 Paladin
    public GameObject GetTaunter(GameObject enemyObj)
    {
        var data = activeTaunts.Find(t => t.enemyObj == enemyObj);
        return data != null ? data.paladinObj : null;
    }
    
}
