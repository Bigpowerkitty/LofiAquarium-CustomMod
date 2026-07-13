# 洛菲水族馆 Mod · Lofi Aquarium Custom Mod

适用于 Steam 游戏《洛菲水族馆 / Lofi Aquarium》的 BepInEx 自定义 Mod。关键词：洛菲水族馆mod、洛菲水族馆修改器、Lofi Aquarium mod、Lofi Aquarium trainer、Steam Lofi Aquarium、BepInEx mod。

This is a Windows BepInEx mod for **Lofi Aquarium** on Steam, featuring configurable currencies, submarine level, aquarium capacity, and targeted fish/quality purchases.

## 功能

- 自定义购买鱼苗金币（`money`），支持游戏计数单位 `M/MM/B/BB/BBB/T/TT/TTT/P/pp/ppp/Q`
- 自定义星星币（`coin_1`）
- 自定义彩鱼币 / 幻彩币（`coin_3`）
- 自定义潜艇等级，饲料 XP 自动设为潜艇等级 + 2
- 自定义当前鱼缸显示容量
- 锁定商城鱼苗的指定鱼种（1-100）与品级（普彩 / 金彩 / 幻彩）
- 获得卡片、鱼缸实体、存档与卖出任务使用同一份指定鱼数据
- F5 打开设置窗口，F7 切换自动补充

## 当前版本

`v2.6.0`

适配 Steam App `3051380`、Windows x64、Unity Mono 版游戏。开发时测试的游戏文件版本日期为 2026-06-24。

## 下载与安装

1. 下载仓库中的 [`LofiAquarium_CustomMod_v2.6.0.zip`](LofiAquarium_CustomMod_v2.6.0.zip)。
2. 完全退出游戏。
3. 解压后，把压缩包内的全部内容复制到游戏根目录：

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\LofiAquarium
   ```

4. 确认以下文件存在：

   ```text
   LofiAquarium\winhttp.dll
   LofiAquarium\BepInEx\plugins\LofiAquariumCoinMod.dll
   ```

5. 从 Steam 正常启动游戏。首次启动 BepInEx 可能稍慢。

压缩包校验值 SHA256：

```text
B19C34FD5464F2C5E312C38A1A843F36C78C9479594D86E49B7348EF1A23DED7
```

## 使用方法

按 `F5` 打开 Mod 窗口，每项设置都有独立的“应用”按钮。

指定购买鱼苗示例：

1. 在 F5 窗口输入鱼种序号 `96`。
2. 选择“普彩”。
3. 点击“锁定购买”，状态应显示 `10960-1`。
4. 在商城购买该鱼所属的【观赏鱼苗5】档位。
5. 完成购买后，点击“解除锁定”恢复原始随机池。

配置文件：

```text
BepInEx\config\cn.codex.lofiaquarium.coinmod.cfg
```

日志文件：

```text
BepInEx\LogOutput.log
```

## 适用搜索关键词

洛菲水族馆mod、洛菲水族馆修改器、洛菲水族馆金币mod、洛菲水族馆鱼苗mod、Lofi Aquarium mod、Lofi Aquarium BepInEx、Steam Lofi Aquarium mod。

## 源码与构建

核心源码位于 [`Source/LofiAquariumCoinMod.cs`](Source/LofiAquariumCoinMod.cs)。项目面向 BepInEx 5，并引用游戏的 Unity/Assembly-CSharp 程序集进行编译。

## 卸载

删除：

```text
BepInEx\plugins\LofiAquariumCoinMod.dll
```

## 说明

这是玩家制作的非官方 Mod，与游戏开发者及 Steam 无隶属关系。修改存档前建议自行备份。
