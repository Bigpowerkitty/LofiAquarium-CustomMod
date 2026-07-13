using System;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LofiAquariumCoinMod
{
    [BepInPlugin("cn.codex.lofiaquarium.coinmod", "Lofi Aquarium Custom Mod", "2.6.0")]
    public sealed class CoinModPlugin : BaseUnityPlugin
    {
        private ConfigEntry<string> targetAmount;
        private ConfigEntry<string> starAmount;
        private ConfigEntry<string> rainbowAmount;
        private ConfigEntry<string> submarineLevel;
        private ConfigEntry<string> tankCapacity;
        private ConfigEntry<string> targetFishSpecies;
        private ConfigEntry<int> targetFishQuality;
        private ConfigEntry<KeyCode> toggleHotkey;
        private ConfigEntry<bool> autoTopUp;
        private float nextTopUp;
        private bool diagnosticDone;
        private bool showWindow;
        private string amountText;
        private string starText;
        private string rainbowText;
        private string levelText;
        private string tankText;
        private string fishSpeciesText;
        private bool fishPurchaseLocked;
        private int lockedFishId;
        private int lockedFishLevel;
        private int targetedFishCount;
        private readonly Dictionary<UnityEngine.Object, List<string>> fishPoolBackups =
            new Dictionary<UnityEngine.Object, List<string>>();
        private Harmony harmony;
        private static CoinModPlugin instance;
        private Rect windowRect = new Rect(30f, 45f, 540f, 420f);

        private void Awake()
        {
            targetAmount = Config.Bind("General", "TargetAmount", "1Q",
                "F6 补充普通金币（购买鱼苗使用）时的目标数量。");
            starAmount = Config.Bind("General", "StarAmount", "999999",
                "F6 补充星星币时的目标数量（内部变量 coin_1）。");
            rainbowAmount = Config.Bind("General", "RainbowAmount", "999999",
                "F6 补充彩鱼币/幻彩币时的目标数量（内部变量 coin_3）。");
            submarineLevel = Config.Bind("General", "SubmarineLevel", "1354",
                "潜艇保姆实际等级（submarine_SO.lv）。饲料/每次喂鱼 XP 自动等于等级 + 2。");
            tankCapacity = Config.Bind("General", "TankCapacity", "100",
                "当前鱼缸在 HUD 显示的最大容量。游戏存档值会自动保存为显示容量减 1。");
            targetFishSpecies = Config.Bind("TargetFish", "Species", "1",
                "指定购买的鱼种序号 1-100，也可填写基础鱼 ID（10010-11000）。");
            targetFishQuality = Config.Bind("TargetFish", "Quality", 1,
                "指定品级：1=普彩，2=金彩，3=幻彩。");
            toggleHotkey = Config.Bind("Hotkeys", "ToggleAutoTopUp", KeyCode.F7,
                "开启/关闭自动补充金币。");
            autoTopUp = Config.Bind("General", "AutoTopUp", false,
                "开启后每秒把普通金币补充到目标数量。游戏内按 F7 切换。");
            amountText = targetAmount.Value;
            starText = starAmount.Value;
            rainbowText = rainbowAmount.Value;
            levelText = submarineLevel.Value;
            tankText = tankCapacity.Value;
            fishSpeciesText = targetFishSpecies.Value;
            instance = this;
            try
            {
                Type customEvent = FindType("Unity.VisualScripting.CustomEvent");
                MethodInfo trigger = customEvent == null ? null : customEvent.GetMethod("Trigger",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[] { typeof(GameObject), typeof(string), typeof(object[]) }, null);
                MethodInfo prefix = typeof(CoinModPlugin).GetMethod("CustomEventTriggerPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (trigger == null || prefix == null) throw new MissingMethodException("CustomEvent.Trigger");
                harmony = new Harmony("cn.codex.lofiaquarium.coinmod.targetfishcard");
                harmony.Patch(trigger, new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                Logger.LogError("指定鱼购买事件挂钩失败：" + ex);
            }
            Logger.LogInfo("自定义 Mod 已加载：F5 单项修改；支持指定鱼种与品级锁定商城购买。");
        }

        private void Update()
        {
            if (!diagnosticDone && Time.unscaledTime > 5f)
            {
                diagnosticDone = true;
                try { DumpEs3Currencies(); }
                catch (Exception ex) { Logger.LogError("ES3_SCAN_FAILED " + ex); }
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                showWindow = !showWindow;
                amountText = targetAmount.Value;
                starText = starAmount.Value;
                rainbowText = rainbowAmount.Value;
                levelText = submarineLevel.Value;
                tankText = tankCapacity.Value;
                fishSpeciesText = targetFishSpecies.Value;
            }

            if (Input.GetKeyDown(toggleHotkey.Value))
            {
                autoTopUp.Value = !autoTopUp.Value;
                Logger.LogInfo("自动补充金币：" + (autoTopUp.Value ? "开启" : "关闭"));
                if (autoTopUp.Value) SetCurrencies();
            }

            if (autoTopUp.Value && Time.unscaledTime >= nextTopUp)
            {
                nextTopUp = Time.unscaledTime + 1f;
                SetCurrencies(false);
            }

        }

        private void OnGUI()
        {
            if (!showWindow) return;
            windowRect = GUI.Window(3051380, windowRect, DrawWindow, "Lofi Aquarium 自定义 Mod v2.6.0");
        }

        private void DrawWindow(int id)
        {
            DrawSettingRow(36f, "购买鱼苗金币：", ref amountText, delegate { ApplyCurrencySetting("money", targetAmount, amountText, "金币"); });
            DrawSettingRow(78f, "星星币：", ref starText, delegate { ApplyCurrencySetting("coin_1", starAmount, starText, "星星币"); });
            DrawSettingRow(120f, "彩鱼币 / 幻彩币：", ref rainbowText, delegate { ApplyCurrencySetting("coin_3", rainbowAmount, rainbowText, "彩鱼币"); });
            DrawSettingRow(162f, "潜艇等级：", ref levelText, ApplySubmarineSetting);
            DrawSettingRow(204f, "鱼缸显示容量：", ref tankText, ApplyTankSetting);
            GUI.Label(new Rect(18f, 248f, 150f, 26f), "指定鱼种(1-100)：");
            fishSpeciesText = GUI.TextField(new Rect(175f, 246f, 105f, 30f), fishSpeciesText ?? "");
            if (GUI.Button(new Rect(292f, 246f, 105f, 30f), QualityLabel()))
            {
                targetFishQuality.Value = targetFishQuality.Value % 3 + 1;
            }
            if (GUI.Button(new Rect(409f, 246f, 111f, 30f), fishPurchaseLocked ? "解除锁定" : "锁定购买"))
                ToggleFishPurchaseLock();

            GUI.Label(new Rect(18f, 288f, 500f, 22f), fishPurchaseLocked
                ? "真实购买源已锁定：" + lockedFishId + "-" + lockedFishLevel + "；已生成：" + targetedFishCount
                : "选择鱼种与品级并锁定，然后购买对应档位鱼苗");
            GUI.Label(new Rect(18f, 314f, 500f, 22f), "饲料XP=潜艇等级+2；鱼缸容量修改当前鱼缸");
            GUI.Label(new Rect(18f, 338f, 150f, 20f), "Mod 版本：v2.6.0");

            if (GUI.Button(new Rect(180f, 370f, 180f, 38f), "关闭"))
                showWindow = false;

            GUI.DragWindow(new Rect(0f, 0f, 540f, 28f));
        }

        private void DrawSettingRow(float y, string label, ref string text, Action apply)
        {
            GUI.Label(new Rect(18f, y, 155f, 26f), label);
            text = GUI.TextField(new Rect(175f, y - 2f, 205f, 30f), text ?? "");
            if (GUI.Button(new Rect(392f, y - 2f, 88f, 30f), "应用")) apply();
        }

        private void ApplyCurrencySetting(string key, ConfigEntry<string> setting, string text, string label)
        {
            float value;
            if (!TryAmount(text, out value))
            {
                Logger.LogWarning(label + "数值格式无效");
                return;
            }
            setting.Value = text.Trim();
            Type variables = FindType("Unity.VisualScripting.Variables");
            int changed = SetEs3Currency(key, value);
            if (variables != null)
            {
                changed += SetStaticScope(variables, "Saved", key, value);
                changed += SetStaticScope(variables, "Application", key, value);
                changed += SetStaticScope(variables, "ActiveScene", key, value);
                changed += SetObjectScopes(variables, key, value);
            }
            TriggerRefreshEvents();
            Logger.LogInfo(label + "已单独应用为 " + value.ToString("G9") + "，写入位置 " + changed);
        }

        private void ApplySubmarineSetting()
        {
            int level;
            if (!TryLevel(levelText, out level))
            {
                Logger.LogWarning("潜艇等级必须是 0 到 2147483647 的整数");
                return;
            }
            submarineLevel.Value = levelText.Trim();
            float feed = (float)level + 2f;
            Type variables = FindType("Unity.VisualScripting.Variables");
            int changed = SetSubmarineLevel(level);
            if (variables != null)
            {
                changed += SetStaticScope(variables, "Application", "foodxp", feed);
                changed += SetStaticScope(variables, "ActiveScene", "foodxp", feed);
                changed += SetObjectScopes(variables, "foodxp", feed);
            }
            TriggerRefreshEvents();
            Logger.LogInfo("潜艇等级已单独应用为 " + level + "，饲料XP自动为 " + feed.ToString("G9") + "，写入位置 " + changed);
        }

        private void ApplyTankSetting()
        {
            int capacity;
            if (!TryLevel(tankText, out capacity) || capacity < 1)
            {
                Logger.LogWarning("鱼缸容量必须是 1 到 2147483647 的整数");
                return;
            }
            tankCapacity.Value = tankText.Trim();
            int changed = SetTankCapacity(capacity);
            TriggerRefreshEvents();
            Logger.LogInfo("当前鱼缸显示容量已单独应用为 " + capacity + "，写入位置 " + changed);
        }

        private string QualityLabel()
        {
            switch (targetFishQuality.Value)
            {
                case 2: return "品级：金彩";
                case 3: return "品级：幻彩";
                default: return "品级：普彩";
            }
        }

        private void ToggleFishPurchaseLock()
        {
            if (fishPurchaseLocked)
            {
                RestoreFishPurchasePools();
                fishPurchaseLocked = false;
                Logger.LogInfo("指定鱼购买已手动解除锁定；商城原始鱼苗随机池已恢复。");
                return;
            }

            int requested;
            if (!int.TryParse((fishSpeciesText ?? "").Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out requested))
            {
                Logger.LogWarning("鱼种必须填写 1-100，或基础鱼 ID 10010-11000。");
                return;
            }

            int baseId;
            if (requested >= 1 && requested <= 100) baseId = 10000 + requested * 10;
            else if (requested >= 10010 && requested <= 11000 && requested % 10 == 0) baseId = requested;
            else
            {
                Logger.LogWarning("鱼种超出范围：请输入 1-100，或基础鱼 ID 10010-11000。");
                return;
            }

            targetFishSpecies.Value = fishSpeciesText.Trim();
            lockedFishId = baseId;
            lockedFishLevel = Math.Max(1, Math.Min(3, targetFishQuality.Value));
            targetedFishCount = 0;
            int lockedPools = LockFishPurchasePools();
            if (lockedPools == 0)
            {
                Logger.LogError("没有找到商城 list_string_p* 鱼苗池；请进入商城鱼苗页面后重新锁定。");
                return;
            }
            fishPurchaseLocked = true;
            Logger.LogInfo("已锁定商城真实鱼苗生成源：fish_id=" + lockedFishId + "，lv=" + lockedFishLevel +
                "（" + QualityLabel() + "），覆盖 list_string 鱼池 " + lockedPools +
                " 个。获得卡片、鱼缸实体、卖出任务将使用同一鱼种；点击解除锁定才恢复随机。");
        }

        private int LockFishPurchasePools()
        {
            Type listType = FindType("list_string_SO");
            if (listType == null) return 0;
            FieldInfo listField = listType.GetField("list",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField == null) return 0;

            fishPoolBackups.Clear();
            int changed = 0;
            string target = lockedFishId.ToString(CultureInfo.InvariantCulture);
            foreach (UnityEngine.Object asset in Resources.FindObjectsOfTypeAll(listType))
            {
                if (asset == null || !asset.name.StartsWith("list_string_p", StringComparison.OrdinalIgnoreCase))
                    continue;
                IList list = listField.GetValue(asset) as IList;
                if (list == null) continue;
                try
                {
                    List<string> backup = new List<string>();
                    foreach (object value in list) backup.Add(value == null ? null : value.ToString());
                    fishPoolBackups[asset] = backup;
                    list.Clear();
                    list.Add(target);
                    changed++;
                    Logger.LogInfo("TARGET_FISH_POOL " + asset.name + " => " + target);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("锁定鱼苗池失败 " + asset.name + "：" + ex.Message);
                }
            }
            return changed;
        }

        private void RestoreFishPurchasePools()
        {
            Type listType = FindType("list_string_SO");
            FieldInfo listField = listType == null ? null : listType.GetField("list",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField != null)
            {
                foreach (KeyValuePair<UnityEngine.Object, List<string>> pair in fishPoolBackups)
                {
                    if (pair.Key == null) continue;
                    try
                    {
                        IList list = listField.GetValue(pair.Key) as IList;
                        if (list == null) continue;
                        list.Clear();
                        foreach (string value in pair.Value) list.Add(value);
                    }
                    catch { }
                }
            }
            fishPoolBackups.Clear();
        }

        private UnityEngine.Object FindTargetFishAsset()
        {
            Type fishType = FindType("fish_SO");
            if (fishType == null) return null;
            UnityEngine.Object loaded = Resources.Load("fish_SO/" +
                lockedFishId.ToString(CultureInfo.InvariantCulture), fishType);
            if (loaded != null) return loaded;

            FieldInfo idField = fishType.GetField("id",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (idField == null) return null;
            foreach (UnityEngine.Object candidate in Resources.FindObjectsOfTypeAll(fishType))
            {
                if (candidate == null) continue;
                try
                {
                    if (Convert.ToInt32(idField.GetValue(candidate), CultureInfo.InvariantCulture) == lockedFishId)
                        return candidate;
                }
                catch { }
            }
            return null;
        }

        private static void CustomEventTriggerPrefix(GameObject __0, string __1, object[] __2)
        {
            CoinModPlugin plugin = instance;
            if (plugin == null || !plugin.fishPurchaseLocked || __2 == null || __2.Length < 2 ||
                __2[0] == null || __2[0].GetType().FullName != "fish_SO" || !(__2[1] is int))
                return;

            string targetName = __0 == null ? "" : (__0.name ?? "");
            if (targetName.IndexOf("fish", StringComparison.OrdinalIgnoreCase) < 0 &&
                targetName.IndexOf("card", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            UnityEngine.Object exactFish = plugin.FindTargetFishAsset();
            if (exactFish == null)
            {
                plugin.Logger.LogError("无法加载目标 fish_SO/" + plugin.lockedFishId);
                return;
            }
            __2[0] = exactFish;
            __2[1] = plugin.lockedFishLevel;
            plugin.targetedFishCount++;
            plugin.Logger.LogInfo("TARGET_FISH_CARD #" + plugin.targetedFishCount + " event=" + __1 +
                " target=" + targetName + " fish=" + plugin.lockedFishId + "-" + plugin.lockedFishLevel);
        }

        private void OnDestroy()
        {
            RestoreFishPurchasePools();
            fishPurchaseLocked = false;
            if (harmony != null) harmony.UnpatchSelf();
            if (instance == this) instance = null;
        }

        private static bool TryAmount(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string input = text.Trim();
            string[] suffixes = { "BBB", "TTT", "ppp", "MM", "BB", "TT", "pp", "M", "B", "T", "P", "Q" };
            float multiplier = 1f;
            foreach (string suffix in suffixes)
            {
                if (!input.EndsWith(suffix, StringComparison.Ordinal)) continue;
                input = input.Substring(0, input.Length - suffix.Length).Trim();
                switch (suffix)
                {
                    case "M": multiplier = 1e7f; break;
                    case "MM": multiplier = 1e8f; break;
                    case "B": multiplier = 1e9f; break;
                    case "BB": multiplier = 1e10f; break;
                    case "BBB": multiplier = 1e11f; break;
                    case "T": multiplier = 1e12f; break;
                    case "TT": multiplier = 1e13f; break;
                    case "TTT": multiplier = 1e14f; break;
                    case "P": multiplier = 1e15f; break;
                    case "pp": multiplier = 1e16f; break;
                    case "ppp": multiplier = 1e17f; break;
                    case "Q": multiplier = 1e18f; break;
                }
                break;
            }
            float number;
            if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false;
            value = number * multiplier;
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryLevel(string text, out int value)
        {
            value = 0;
            long parsed;
            if (string.IsNullOrWhiteSpace(text) || !long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return false;
            if (parsed < 0 || parsed > int.MaxValue) return false;
            value = (int)parsed;
            return true;
        }

        private void SetCurrencies(bool writeLog = true)
        {
            try
            {
                Type variables = FindType("Unity.VisualScripting.Variables");
                if (variables == null)
                    throw new InvalidOperationException("未找到 Unity.VisualScripting.Variables");

                float money;
                float stars;
                float rainbow;
                int level;
                int capacity;
                if (!TryAmount(targetAmount.Value, out money) || !TryAmount(starAmount.Value, out stars) ||
                    !TryAmount(rainbowAmount.Value, out rainbow) || !TryLevel(submarineLevel.Value, out level) ||
                    !TryLevel(tankCapacity.Value, out capacity) || capacity < 1)
                    throw new InvalidOperationException("配置中的货币、等级或鱼缸容量格式无效");

                float feed = (float)level + 2f;

                int changed = 0;
                changed += SetEs3Currency("money", money);
                changed += SetEs3Currency("coin_1", stars);
                changed += SetEs3Currency("coin_3", rainbow);
                changed += SetStaticScope(variables, "Saved", "money", money);
                changed += SetStaticScope(variables, "Application", "money", money);
                changed += SetStaticScope(variables, "ActiveScene", "money", money);
                changed += SetStaticScope(variables, "Saved", "coin_1", stars);
                changed += SetStaticScope(variables, "Application", "coin_1", stars);
                changed += SetStaticScope(variables, "ActiveScene", "coin_1", stars);
                changed += SetStaticScope(variables, "Saved", "coin_3", rainbow);
                changed += SetStaticScope(variables, "Application", "coin_3", rainbow);
                changed += SetStaticScope(variables, "ActiveScene", "coin_3", rainbow);
                changed += SetObjectScopes(variables, "money", money);
                changed += SetObjectScopes(variables, "coin_1", stars);
                changed += SetObjectScopes(variables, "coin_3", rainbow);
                changed += SetStaticScope(variables, "Application", "foodxp", feed);
                changed += SetStaticScope(variables, "ActiveScene", "foodxp", feed);
                changed += SetObjectScopes(variables, "foodxp", feed);
                changed += SetSubmarineLevel(level);
                changed += SetTankCapacity(capacity);
                changed += SetScriptableAssets("money", money);
                changed += SetScriptableAssets("coin_1", stars);
                changed += SetScriptableAssets("coin_3", rainbow);

                if (changed == 0)
                    throw new InvalidOperationException("未找到任何货币变量作用域");

                TriggerRefreshEvents();

                if (writeLog)
                    Logger.LogInfo("已写入 " + changed + " 个位置：金币 " + money.ToString("G9") + "，星星币 " + stars.ToString("G9") +
                        "，彩鱼币 " + rainbow.ToString("G9") + "，潜艇等级 " + level + "，自动饲料XP " + feed.ToString("G9") +
                        "，鱼缸显示容量 " + capacity + "。");
            }
            catch (Exception ex)
            {
                Logger.LogError("补充金币失败：" + ex);
            }
        }

        private static int SetStaticScope(Type variables, string propertyName, string key, float value)
        {
            PropertyInfo property = variables.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property == null) return 0;

            object declarations;
            try { declarations = property.GetValue(null, null); }
            catch { return 0; }
            return SetDeclaration(declarations, key, value, false);
        }

        private static int SetObjectScopes(Type variables, string key, float value)
        {
            int changed = 0;
            UnityEngine.Object[] instances;
            try { instances = Resources.FindObjectsOfTypeAll(variables); }
            catch { return 0; }

            PropertyInfo declarationsProperty = variables.GetProperty("declarations",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (declarationsProperty == null) return 0;

            foreach (UnityEngine.Object instance in instances)
            {
                if (instance == null) continue;
                try
                {
                    object declarations = declarationsProperty.GetValue(instance, null);
                    changed += SetDeclaration(declarations, key, value, true);
                }
                catch { }
            }
            return changed;
        }

        private static int SetDeclaration(object declarations, string key, float value, bool onlyWhenDefined)
        {
            if (declarations == null) return 0;

            Type type = declarations.GetType();
            MethodInfo isDefined = FindInstanceMethod(type, "IsDefined", typeof(string));
            if (onlyWhenDefined && (isDefined == null || !(bool)isDefined.Invoke(declarations, new object[] { key })))
                return 0;

            object converted = value;
            MethodInfo get = FindInstanceMethod(type, "Get", typeof(string));
            if (get != null && isDefined != null && (bool)isDefined.Invoke(declarations, new object[] { key }))
            {
                object current = get.Invoke(declarations, new object[] { key });
                if (current is int) converted = (int)Math.Min(value, int.MaxValue);
                else if (current is long) converted = (long)value;
                else if (current is double) converted = (double)value;
                else if (current is string) converted = value.ToString();
            }

            MethodInfo set = FindInstanceMethod(type, "Set", typeof(string), typeof(object));
            if (set == null) return 0;

            set.Invoke(declarations, new object[] { key, converted });
            return 1;
        }

        private static MethodInfo FindInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name != name || method.IsGenericMethodDefinition) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length) continue;
                bool matches = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != parameterTypes[i])
                    { matches = false; break; }
                }
                if (matches) return method;
            }
            return null;
        }

        private static void TriggerRefreshEvents()
        {
            Type customEvent = FindType("Unity.VisualScripting.CustomEvent");
            if (customEvent == null) return;

            MethodInfo trigger = null;
            foreach (MethodInfo method in customEvent.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] p = method.GetParameters();
                if (method.Name == "Trigger" && p.Length >= 2 && p[0].ParameterType == typeof(GameObject) && p[1].ParameterType == typeof(string))
                { trigger = method; break; }
            }
            if (trigger == null) return;

            string[] names = { "_set_money", "_set_coin1", "_set_coin3", "事件_金币更改", "_size", "_set_tanknum" };
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject go in objects)
            {
                if (go == null || !go.scene.IsValid()) continue;
                foreach (string name in names)
                {
                    try
                    {
                        ParameterInfo[] p = trigger.GetParameters();
                        object[] args = p.Length == 2
                            ? new object[] { go, name }
                            : new object[] { go, name, new object[0] };
                        trigger.Invoke(null, args);
                    }
                    catch { }
                }
            }
        }

        private static int SetEs3Currency(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            ES3.Save(key, (object)value);
            return 1;
        }

        private int SetSubmarineLevel(int level)
        {
            Type submarineType = FindType("submarine_SO");
            if (submarineType == null) return 0;
            FieldInfo levelField = submarineType.GetField("lv", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo guidField = submarineType.GetField("GUID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (levelField == null) return 0;

            int changed = 0;
            UnityEngine.Object best = null;
            foreach (UnityEngine.Object item in Resources.FindObjectsOfTypeAll(submarineType))
            {
                if (item == null) continue;
                string guid = guidField == null ? null : guidField.GetValue(item) as string;
                bool isTarget = string.Equals(guid, "submarine_A", StringComparison.OrdinalIgnoreCase) ||
                    item.name.IndexOf("submarine_A", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isTarget) continue;
                levelField.SetValue(item, level);
                changed++;
                if (best == null || item.name.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase)) best = item;
            }

            try
            {
                object saved = ES3.KeyExists("submarine_A") ? ES3.Load("submarine_A") : null;
                if (saved != null && submarineType.IsInstanceOfType(saved))
                {
                    levelField.SetValue(saved, level);
                    ES3.Save("submarine_A", saved);
                    changed++;
                }
                else if (best != null)
                {
                    ES3.Save("submarine_A", (object)best);
                    changed++;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("潜艇存档同步失败，但运行时等级已修改：" + ex.Message);
            }

            Logger.LogInfo("潜艇等级已独立设为 " + level);
            return changed;
        }

        private int SetTankCapacity(int displayedCapacity)
        {
            string tankName = "tank1";
            try
            {
                if (ES3.KeyExists("main_tank_name"))
                {
                    object loaded = ES3.Load("main_tank_name");
                    string name = loaded as string;
                    if (!string.IsNullOrWhiteSpace(name)) tankName = name;
                }
            }
            catch { }

            string key = tankName + "_size";
            int storedSize = Math.Max(0, displayedCapacity - 1);
            PlayerPrefs.SetInt(key, storedSize);
            PlayerPrefs.Save();
            ES3.Save(key, (object)storedSize);

            int changed = 1;
            Type variables = FindType("Unity.VisualScripting.Variables");
            if (variables != null)
            {
                changed += SetStaticScope(variables, "Saved", key, storedSize);
                changed += SetStaticScope(variables, "Application", key, storedSize);
                changed += SetStaticScope(variables, "ActiveScene", key, storedSize);
                changed += SetObjectScopes(variables, key, storedSize);
            }
            Logger.LogInfo("鱼缸容量键 " + key + " 已保存为 " + storedSize + "（HUD 目标 " + displayedCapacity + "）");
            return changed;
        }

        private void DumpEs3Currencies()
        {
            foreach (string key in new[] { "money", "coin_1", "coin_2", "coin_3", "coin_4" })
            {
                bool exists = ES3.KeyExists(key);
                object value = exists ? ES3.Load(key) : null;
                float playerPrefsValue = PlayerPrefs.GetFloat(key, -987654f);
                Logger.LogInfo("ES3_CURRENCY key=" + key + " exists=" + exists + " value=" +
                    (value == null ? "null" : value.ToString()) + " playerPrefs=" + playerPrefsValue);
            }
        }

        private int SetScriptableAssets(string key, float value)
        {
            int changed = 0;
            foreach (ScriptableObject asset in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (asset == null) continue;
                Type type = asset.GetType();
                FieldInfo idField = type.GetField("id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string id = idField == null ? null : idField.GetValue(asset) as string;
                if (!string.Equals(asset.name, key, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(id, key, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.Name != "num" && field.Name != "value" && field.Name != "val") continue;
                    try
                    {
                        if (field.FieldType == typeof(float)) field.SetValue(asset, (float)value);
                        else if (field.FieldType == typeof(int)) field.SetValue(asset, value);
                        else if (field.FieldType == typeof(long)) field.SetValue(asset, (long)value);
                        else if (field.FieldType == typeof(double)) field.SetValue(asset, (double)value);
                        else if (field.FieldType == typeof(string)) field.SetValue(asset, value.ToString());
                        else continue;
                        changed++;
                    }
                    catch { }
                }
            }
            return changed;
        }

        private void DumpCurrencyAssets()
        {
            int found = 0;
            foreach (ScriptableObject asset in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (asset == null) continue;
                Type type = asset.GetType();
                FieldInfo idField = type.GetField("id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string id = idField == null ? null : idField.GetValue(asset) as string;
                string probe = (asset.name + " " + id).ToLowerInvariant();
                bool candidate = probe.Contains("money") || probe.Contains("coin") ||
                    type.Name == "inven_GUID" ||
                    type.GetField("num", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null ||
                    type.GetField("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;
                if (!candidate) continue;
                found++;
                string fields = "";
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object v = null;
                    try { v = field.GetValue(asset); } catch { }
                    fields += " " + field.Name + "=" + (v == null ? "null" : v.ToString()) + ";";
                }
                Logger.LogInfo("CURRENCY_ASSET name=" + asset.name + " type=" + type.FullName + " fields:" + fields);
            }
            Logger.LogInfo("CURRENCY_ASSET_SCAN count=" + found);
        }

        private void DumpVisualVariables()
        {
            Type variables = FindType("Unity.VisualScripting.Variables");
            if (variables == null) return;
            int found = 0;

            foreach (string scope in new[] { "Saved", "Application", "ActiveScene" })
            {
                PropertyInfo property = variables.GetProperty(scope, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property == null) continue;
                try { found += DumpDeclarations(property.GetValue(null, null), scope); } catch { }
            }

            PropertyInfo declarationsProperty = variables.GetProperty("declarations", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (declarationsProperty != null)
            {
                foreach (UnityEngine.Object instance in Resources.FindObjectsOfTypeAll(variables))
                {
                    if (instance == null) continue;
                    string owner = instance.name;
                    Component component = instance as Component;
                    if (component != null && component.gameObject != null) owner = component.gameObject.name;
                    try { found += DumpDeclarations(declarationsProperty.GetValue(instance, null), "Object:" + owner); } catch { }
                }
            }
            Logger.LogInfo("CURRENCY_VAR_SCAN numeric_count=" + found);
        }

        private int DumpDeclarations(object declarations, string scope)
        {
            if (declarations == null) return 0;
            System.Collections.IEnumerable enumerable = declarations as System.Collections.IEnumerable;
            if (enumerable == null) return 0;
            int count = 0;
            foreach (object declaration in enumerable)
            {
                if (declaration == null) continue;
                Type type = declaration.GetType();
                PropertyInfo nameProperty = type.GetProperty("name");
                PropertyInfo valueProperty = type.GetProperty("value");
                if (nameProperty == null || valueProperty == null) continue;
                string name = nameProperty.GetValue(declaration, null) as string;
                object value = valueProperty.GetValue(declaration, null);
                if (!(value is byte) && !(value is short) && !(value is int) && !(value is long) &&
                    !(value is float) && !(value is double) && !(value is decimal)) continue;
                count++;
                Logger.LogInfo("CURRENCY_VAR scope=" + scope + " name=" + name + " type=" + value.GetType().Name + " value=" + value);
            }
            return count;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
