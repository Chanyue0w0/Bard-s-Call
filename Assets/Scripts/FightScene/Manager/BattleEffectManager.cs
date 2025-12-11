using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class BattleEffectManager : MonoBehaviour
{
    public static BattleEffectManager Instance { get; private set; }

    [Header("玩家統一血條（場景中已有，不要生成）")]
    public Image playerTotalHPUI_filledImg;
    public TotalPlayerHealthBarUI playerTotalHPUI;

    [Header("敵人統一血條（場景中已有，不要生成）")]
    public TotalEnemyHealthBarUI enemyTotalHPUI;

    private int enemyTotalMaxHP = 1;

    [Header("血量危險提示 UI")]
    public GameObject lowHPWarningObj;

    [Header("共用 Shield 特效（當角色未指定 ShieldEffectPrefab 時使用）")]
    public GameObject shieldVfxPrefab;
    private GameObject[] blockEffects = new GameObject[3];
    [Header("格檔成功特效")]
    public GameObject blockSuccessVfxPrefab;

    [Header("敵方格擋管理")] 
    private Dictionary<GameObject, bool> enemyBlocking = new();
    private Dictionary<GameObject, Coroutine> enemyBlockCoroutines = new();
    private Dictionary<GameObject, GameObject> enemyBlockEffects = new();

    [Header("Priest 回復特效")]
    public GameObject healVfxPrefab;

    // 每位角色的格檔狀態與協程追蹤
    private bool[] isBlocking = new bool[3];
    private Coroutine[] blockCoroutines = new Coroutine[3];
    private bool[] isHeavyBlocking = new bool[3];

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

    

    [Header("Holy CounterAttack（完美格檔反擊）")]
    public GameObject holyCounterattackPrefab;
    // -------------------------
    // HolyEffect（對拍共鳴 BUFF）
    // -------------------------
    [Header("Holy Effect（對拍共鳴 BUFF）")]
    public GameObject holyEffectPrefab; // Holy 特效 Prefab

    private bool isHolyActive = false;
    private int holyRemainingBeats = 0;
    private List<GameObject> activeHolyEffects = new List<GameObject>();


    // ======================
    // 中毒 UI 顏色系統
    // ======================
    [Header("中毒 UI 顏色設定")]
    public Color poisonColor = new Color(0.7f, 0.2f, 0.9f); // 紫色

    private Color baseHPColor; // 原始顏色
    private bool isPoisonUIRoutineRunning = false;
    private Coroutine poisonUICoroutine = null;

    private class PoisonInfo
    {
        public BattleManager.TeamSlotInfo target;
        public int damagePerBeat;
        public int remainingBeats;
    }

    private List<PoisonInfo> activePoisons = new List<PoisonInfo>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerTotalHPUI_filledImg != null)
            baseHPColor = playerTotalHPUI_filledImg.color;
    }

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


    public void ActivateBlock(int index, float beatDuration, CharacterData charData, GameObject actor, bool isHeavyBlock)
    {
        if (index < 0 || index >= isBlocking.Length) return;

        isHeavyBlocking[index] = isHeavyBlock;

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

    public void ActivateEnemyBlock(GameObject enemyObj, CharacterData charData, float durationBeats)
    {
        if (enemyObj == null) return;

        // 1. 註冊
        if (!enemyBlocking.ContainsKey(enemyObj))
            enemyBlocking[enemyObj] = true;

        // 2. 特效
        Vector3 pos = enemyObj.transform.position;
        GameObject fxPrefab = charData != null && charData.ShieldEffectPrefab != null
            ? charData.ShieldEffectPrefab
            : shieldVfxPrefab;

        GameObject fx = Instantiate(fxPrefab, pos, Quaternion.identity);
        fx.transform.SetParent(enemyObj.transform, worldPositionStays: true);

        enemyBlockEffects[enemyObj] = fx;

        // 3. 持續時間（算拍）
        float sec = FMODBeatListener2.Instance.SecondsPerBeat * durationBeats;
        if (enemyBlockCoroutines.ContainsKey(enemyObj) && enemyBlockCoroutines[enemyObj] != null)
            StopCoroutine(enemyBlockCoroutines[enemyObj]);

        enemyBlockCoroutines[enemyObj] = StartCoroutine(EnemyBlockRoutine(enemyObj, sec));
    }

    private IEnumerator EnemyBlockRoutine(GameObject enemyObj, float sec)
    {
        yield return new WaitForSeconds(sec);

        // 關閉格擋
        enemyBlocking[enemyObj] = false;

        if (enemyBlockEffects.ContainsKey(enemyObj) && enemyBlockEffects[enemyObj] != null)
            Destroy(enemyBlockEffects[enemyObj]);

        enemyBlockEffects.Remove(enemyObj);
        enemyBlockCoroutines.Remove(enemyObj);
    }


    // =======================
    // 傷害判定（含重攻擊判定與 ShieldGoblin 破防邏輯）
    // =======================
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect, bool isHeavyAttack = false, int overrideDamage = -1)
    {
        if (target == null) return;
        // attacker 為 null 表示「環境傷害」或「狀態效果」，應允許繼續執行


        // =======================================
        // ShieldGoblin 永久防禦邏輯
        // =======================================
        if (target.Actor != null)
        {
            var shieldGoblin = target.Actor.GetComponent<ShieldGoblin>();
            if (shieldGoblin != null && shieldGoblin.isBlocking)
            {
                Debug.Log("【敵方格檔成功】");

                // 1. 實際傷害（30%）
                int reducedDamage = Mathf.RoundToInt(overrideDamage * 0.3f);

                // 2. 顯示格擋數字
                if (DamageNumberManager.Instance != null && attacker != null)
                    DamageNumberManager.Instance.ShowBlocked(target.Actor.transform, reducedDamage);

                // 3. ★★★ 真正扣血（缺少的部分）★★★
                target.HP -= reducedDamage;
                if (target.HP < 0) target.HP = 0;

                UpdateEnemyTotalHPUI();

                // 4. 呼叫敵人受傷反應
                var enemyBaseTMP = target.Actor.GetComponent<EnemyBase>();
                if (enemyBaseTMP != null)
                    enemyBaseTMP.OnDamaged(reducedDamage, isHeavyAttack);

                // 5. 更新血條
                var enemyHBTMP = target.Actor?.GetComponentInChildren<HealthBarUI>();
                if (enemyHBTMP != null) enemyHBTMP.ForceUpdate();

                // 6. 檢查死亡
                if (target.HP <= 0)
                    HandleUnitDefeated(target);

                return;
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
        // 玩家方格檔判定（含 Paladin 輕拍/重拍邏輯）
        // =======================================
        int targetIndex = System.Array.FindIndex(BattleManager.Instance.CTeamInfo, t => t == target);

        if (targetIndex >= 0 && isBlocking[targetIndex])
        {
            var data = target.Actor.GetComponent<CharacterData>();
            bool isPaladin = (data != null && data.ClassType == BattleManager.UnitClass.Paladin);

            // --- 取出完整傷害（後面敵人/玩家分支要用）---
            int rawDamage = (overrideDamage >= 0)
                ? overrideDamage
                : Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * (isPerfect ? 1f : 0f)));

            // ----------- Paladin 特殊格檔 -----------
            if (isPaladin)
            {
                bool heavyBlock = isHeavyBlocking[targetIndex];

                FMODAudioPlayer.Instance.PlayPaladinBlocked(); //播放聖騎士格擋音效

                if (heavyBlock)
                {
                    ShowBlockEffectPaladin(target);
                    Debug.Log("【Paladin 重拍格檔】100% 免傷（觸發神聖反擊）");

                    // ★★★ 反擊生成 HolyCounterattack ★★★
                    if (attacker != null && holyCounterattackPrefab != null)
                    {
                        Vector3 spawnPos = target.Actor.transform.position + new Vector3(0, 0.5f, 0);
                        GameObject obj = GameObject.Instantiate(holyCounterattackPrefab, spawnPos, Quaternion.identity);

                        FireBallSkill fire = obj.GetComponent<FireBallSkill>();
                        if (fire != null)
                        {
                            fire.attacker = target;        // paladin 是反擊者
                            fire.target = attacker;        // 攻擊者成為反擊目標
                            fire.damage = 40 + GlobalIndex.RythmResonanceBuff;              // 自訂反擊傷害
                            fire.isPerfect = true;         // 設定為 perfect hit
                            fire.isHeavyAttack = true;     // 重擊屬性
                        }
                    }

                    // 顯示格擋數字
                    DamageNumberManager.Instance.ShowBlocked(target.Actor.transform, 0);

                    return;
                }
                else
                {
                    // ==============================
                    // ★ 輕拍格檔 → 扣 30% 傷害
                    // ==============================
                    int reducedDamage = Mathf.RoundToInt(rawDamage * 0.3f);   // 玩家實際承受
                    int blockedDamage = rawDamage - reducedDamage;            // 被格檔掉的量（70%）

                    ShowBlockEffectPaladin(target);
                    //ActivateHolyEffect();
                    Debug.Log($"【Paladin 輕拍格檔】受到 {reducedDamage} 傷害（格擋 {blockedDamage}）");

                    // ========================================================
                    // ★ 數字顯示：兩個數字
                    //   1. 格擋的傷害（藍/灰） → ShowBlocked()
                    //   2. 實際扣血（紅）→ ShowDamage()
                    // ========================================================
                    if (DamageNumberManager.Instance != null)
                    {
                        // Blocked 數字：顯示被減掉後的傷害量（70%）
                        DamageNumberManager.Instance.ShowBlocked(target.Actor.transform, reducedDamage); //blockedDamage

                        // 拿到的傷害：顯示實際扣的 30%
                        //DamageNumberManager.Instance.ShowDamage(target.Actor.transform, reducedDamage);
                    }

                    // ===== 套用傷害至全隊共用 HP =====
                    GlobalIndex.CurrentTotalHP = Mathf.Max(0, GlobalIndex.CurrentTotalHP - reducedDamage);
                    playerTotalHPUI.SetHP(GlobalIndex.CurrentTotalHP, GlobalIndex.MaxTotalHP);

                    // 更新低血量檢查
                    UpdateLowHPWarning();

                    BattleManager.Instance.CheckPlayerDefeat();
                    return;

                }

            }

            // ----------- 其他職業：沿用舊版完全格檔 -----------
            ShowBlockEffectPaladin(target);

            //ActivateHolyEffect();
            Debug.Log($"【格檔成功】{target.UnitName} 擋下 {attacker?.UnitName} 的攻擊！");
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
            if (BattleManager.Instance.GetFeverInputMode()) //Fever狀態不受傷
                return;

            GlobalIndex.CurrentTotalHP = Mathf.Max(
                0,
                GlobalIndex.CurrentTotalHP - finalDamage
            );

            Debug.Log($"【玩家受傷】-{finalDamage} → 全隊剩餘 {GlobalIndex.CurrentTotalHP}/{GlobalIndex.MaxTotalHP}");

            // 更新統一血條 UI
            playerTotalHPUI.SetHP(GlobalIndex.CurrentTotalHP, GlobalIndex.MaxTotalHP);
            // 更新低血量檢查
            UpdateLowHPWarning();

            // 顯示傷害數字
            if (target.Actor != null && DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowDamage(target.Actor.transform, finalDamage);
            }

            // 法師中斷充電
            //var data = target.Actor.GetComponent<CharacterData>();
            //if (data != null && data.ClassType == BattleManager.UnitClass.Mage)
            //{
            //    ResetChargeStacks(target);
            //    Debug.Log($"【充電中斷】{target.UnitName} 被攻擊 → 清除層數");
            //}

            // 判斷是否全隊死亡
            BattleManager.Instance.CheckPlayerDefeat();
            return;
        }

        // ===============================
        // 若是敵人 → 使用原本邏輯
        // ===============================
        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        // ★★★ 新增：通知敵人受傷 ★★★
        var enemyBase = target.Actor.GetComponent<EnemyBase>();
        if (enemyBase != null)
            enemyBase.OnDamaged(finalDamage, isHeavyAttack);

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

        UpdateEnemyTotalHPUI();

        // 檢查死亡
        if (target.HP <= 0)
        {
            HandleUnitDefeated(target);
        }


        BattleManager.Instance.CheckPlayerDefeat();

    }

    private void ShowBlockEffectPaladin(BattleManager.TeamSlotInfo target)
    {
        Vector3 spawnPos = target.Actor.transform.position;
        Vector3 offset = new Vector3(0f, 1.0f, 0f);

        GameObject fx = Instantiate(blockSuccessVfxPrefab, spawnPos + offset, Quaternion.identity);
        fx.transform.SetParent(target.Actor.transform, worldPositionStays: true);
        fx.transform.localPosition = offset;

        VibrationManager.Instance.Vibrate("Block");
    }


    // ======================================================
    // HolyEffect 系統（格檔成功時啟動）
    // ======================================================

    public void ActivateHolyEffect()
    {
        int durationBeats = 5;

        // 若已啟動 → 只刷新持續拍
        if (isHolyActive)
        {
            holyRemainingBeats = durationBeats;
            Debug.Log("【HolyEffect 刷新】持續拍已更新為 4 拍");
            return;
        }

        isHolyActive = true;
        holyRemainingBeats = durationBeats;

        // 設定對拍共鳴加乘
        GlobalIndex.RythmResonanceBuff = 10;
        Debug.Log("【HolyEffect 啟動】全隊獲得 對拍共鳴 +10 持續 4 拍");

        // 生成 Holy 特效（每位角色腳下）
        SpawnHolyEffectsForTeam();
    }


    // 生成全隊 Holy 特效
    private void SpawnHolyEffectsForTeam()
    {
        ClearHolyEffects();

        var team = BattleManager.Instance.CTeamInfo;
        foreach (var ally in team)
        {
            if (ally == null || ally.Actor == null) continue;

            Vector3 pos = ally.Actor.transform.position;

            if (holyEffectPrefab != null)
            {
                GameObject fx = Instantiate(holyEffectPrefab, pos, Quaternion.identity);

                fx.transform.SetParent(ally.Actor.transform, worldPositionStays: true);

                // ★★★★★ 加這行，一定要重設位置 ★★★★★
                fx.transform.localPosition = Vector3.zero;

                activeHolyEffects.Add(fx);
            }
        }
    }


    // 每拍倒數 → 由 BeatManager 每拍呼叫
    public void TickHolyEffect()
    {
        if (!isHolyActive) return;

        holyRemainingBeats--;

        if (holyRemainingBeats <= 0)
        {
            EndHolyEffect();
        }
    }


    // HolyEffect 結束
    private void EndHolyEffect()
    {
        isHolyActive = false;
        GlobalIndex.RythmResonanceBuff = 0;

        ClearHolyEffects();

        Debug.Log("【HolyEffect 結束】對拍共鳴加乘移除");
    }


    private void ClearHolyEffects()
    {
        foreach (var fx in activeHolyEffects)
        {
            if (fx != null)
                Destroy(fx);
        }
        activeHolyEffects.Clear();
    }


    public void ApplyPoison(BattleManager.TeamSlotInfo target, int damagePerBeat, int durationBeats)
    {
        if (target == null) return;

        // 若已有毒，刷新持續拍
        var existing = activePoisons.Find(p => p.target == target);
        if (existing != null)
        {
            existing.remainingBeats = durationBeats;
            Debug.Log($"【中毒刷新】{target.UnitName}：再次中毒，持續 {durationBeats} 拍");
            return;
        }

        // 新增一個中毒
        activePoisons.Add(new PoisonInfo
        {
            target = target,
            damagePerBeat = damagePerBeat,
            remainingBeats = durationBeats
        });

        // ★ 啟動 UI 中毒顏色協程（若沒有啟動）
        if (!isPoisonUIRoutineRunning)
        {
            poisonUICoroutine = StartCoroutine(PoisonColorRoutine());
            isPoisonUIRoutineRunning = true;
        }

        Debug.Log($"【中毒施加】{target.UnitName}：每拍 {damagePerBeat}，持續 {durationBeats} 拍");
    }

    public void TickPoison()
    {
        // ★ 只有中毒時才閃紫色
        if (activePoisons.Count > 0)
            PoisonFlashOnce();

        for (int i = activePoisons.Count - 1; i >= 0; i--)
        {
            var p = activePoisons[i];
            if (p.target == null || p.target.Actor == null)
            {
                activePoisons.RemoveAt(i);
                continue;
            }

            // 扣血（使用 OnHit 會觸發所有 UI/傷害效果）
            OnHit(
                attacker: null,               // 毒不算攻擊者
                target: p.target,
                isPerfect: true,              // 不需要 Perfect
                isHeavyAttack: false,
                overrideDamage: p.damagePerBeat
            );

            p.remainingBeats--;

            if (p.remainingBeats <= 0)
            {
                Debug.Log($"【中毒結束】{p.target.UnitName}");
                activePoisons.RemoveAt(i);
            }
        }
    }

    private IEnumerator PoisonColorRoutine()
    {
        // 持續等待直到全部中毒消失
        while (activePoisons.Count > 0)
            yield return null;

        // 所有毒解除 → 恢復原色
        if (playerTotalHPUI_filledImg != null)
            playerTotalHPUI_filledImg.color = baseHPColor;

        isPoisonUIRoutineRunning = false;
        poisonUICoroutine = null;
    }


    private void PoisonFlashOnce()
    {
        if (playerTotalHPUI_filledImg == null) return;

        // 每拍：從 base → purple → base
        StartCoroutine(FadeOnceRoutine());
    }

    private IEnumerator FadeOnceRoutine()
    {
        float t = 0f;
        float duration = 0.15f;

        // base -> purple
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            playerTotalHPUI_filledImg.color = Color.Lerp(baseHPColor, poisonColor, t);
            yield return null;
        }

        // purple -> base
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            playerTotalHPUI_filledImg.color = Color.Lerp(poisonColor, baseHPColor, t);
            yield return null;
        }
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
        {
            playerTotalHPUI.SetHP(GlobalIndex.CurrentTotalHP, GlobalIndex.MaxTotalHP);
            // 更新低血量檢查
            UpdateLowHPWarning();
        }

        // 3. 個別角色顯示回復特效 & 綠色數字

        // ============================
        // ★ 顯示一次治療數字（中間角色）
        // ============================
        var mid = BattleManager.Instance.CTeamInfo[1];
        Transform centerTransform = mid?.Actor?.transform;
        // 3-1 顯示綠色 +healAmount
        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowHeal(centerTransform, healAmount);
        }

        var team = BattleManager.Instance.CTeamInfo;
        foreach (var ally in team)
        {
            if (ally == null || ally.Actor == null) continue;

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

    // ---------------------------------------------------------
    // ★ 新增：治療單一敵人（顯示綠色數字，播放 healVFX）
    // ---------------------------------------------------------
    public void HealEnemy(BattleManager.TeamSlotInfo enemy, int healAmount)
    {
        if (enemy == null || enemy.Actor == null) return;

        // 1. 回復 HP
        enemy.HP = Mathf.Min(enemy.MaxHP, enemy.HP + healAmount);
        Debug.Log($"【敵人回復】{enemy.UnitName} +{healAmount} → {enemy.HP}/{enemy.MaxHP}");

        // 2. 顯示數字
        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowHeal(enemy.Actor.transform, healAmount);
        }

        // 3. 顯示治療特效
        if (healVfxPrefab != null)
        {
            Vector3 pos = enemy.Actor.transform.position;
            GameObject fx = Instantiate(healVfxPrefab, pos, Quaternion.identity);
            fx.transform.SetParent(enemy.Actor.transform, worldPositionStays: true);

            var exp = fx.GetComponent<Explosion>();
            if (exp != null)
            {
                exp.SetUseUnscaledTime(true);
                exp.Initialize();
            }
            else
            {
                Destroy(fx, 1.5f);
            }
        }

        // 4. 更新敵人血條
        var hb = enemy.Actor.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        UpdateEnemyTotalHPUI();

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

    public void UpdateLowHPWarning()
    {
        if (lowHPWarningObj == null) return;

        float ratio = (float)GlobalIndex.CurrentTotalHP / GlobalIndex.MaxTotalHP;

        if (ratio < 0.3f)
            lowHPWarningObj.SetActive(true);
        else
            lowHPWarningObj.SetActive(false);
    }

    // ================================================================
    // 敵方總血量初始化（在開場 BattleTeamManager 設定完敵人隊伍後呼叫）
    // ================================================================
    public void InitEnemyTotalHP()
    {
        enemyTotalMaxHP = 0;
        int current = 0;

        var enemies = BattleManager.Instance.EnemyTeamInfo;

        foreach (var e in enemies)
        {
            // ★ 這兩行是關鍵：沒 Actor 的格子視為沒敵人，完全不算血量
            if (e == null || e.Actor == null)
                continue;

            enemyTotalMaxHP += e.MaxHP;
            current += Mathf.Clamp(e.HP, 0, e.MaxHP);
        }

        if (enemyTotalHPUI != null)
        {
            enemyTotalHPUI.SetHP(current, enemyTotalMaxHP);
        }

        Debug.Log($"【EnemyTotalHP 初始化】總血量 = {current}/{enemyTotalMaxHP}");
    }

    // ================================================================
    // 敵方總血量更新（每次敵人扣血或回復後呼叫）
    // ================================================================
    public void UpdateEnemyTotalHPUI()
    {
        int current = 0;
        var enemies = BattleManager.Instance.EnemyTeamInfo;

        foreach (var e in enemies)
        {
            if (e == null || e.Actor == null)
                continue;

            current += Mathf.Clamp(e.HP, 0, e.MaxHP);
        }

        // 更新數值
        if (enemyTotalHPUI != null)
            enemyTotalHPUI.SetHP(current, enemyTotalMaxHP);

        // ★★★ 關鍵：自動控制 CanvasGroup Alpha ★★★
        if (enemyTotalHPUI != null)
        {
            CanvasGroup cg = enemyTotalHPUI.GetComponent<CanvasGroup>();

            if (cg != null)
            {
                cg.alpha = (current > 0) ? 1f : 0f;
            }
        }
    }


}
