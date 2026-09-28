# 随机漂浮物

打开 `Assets/Scenes/PlatformerLevel.unity` 并进入 Play 模式。关卡前、中、后三个
`FloatArea_*` 区域各维持 8 个漂浮物。碰到物体后效果才揭晓，物体缩小消失，约 2 秒后补充。
碰撞使用触发器，漂浮物不能站立。

## 调整玩法

- 选中 `FloatArea_*`，打开 Scene 视图的 Gizmos，调整 `Size` 和位置。
  `Platform Root` 指向关卡的 `Platforms`；仅在区域内的平台表面附近生成。
  建议保持区域 Transform 的缩放为 `(1,1,1)`，使用 `Size` 改变范围。
- `Target Count`、`Replenish Delay`、`Diameter Range`、`Height Above Platform` 控制数量、补充时间和大小。
  无合法位置时会延后重试，因此极小或拥挤区域允许暂时不足目标数量。
- `Neighbor Distance` 内不生成相同尺寸档与形态档组合。两种档位都优先补足当前数量最少的一档。
- 展开 `Effects` 调整两种弹射速度、低重力倍率与持续时间、浮力加速度与持续时间。
  重力效果相互替换；弹射替换外部速度，可以与重力效果共存。
- 玩家 `PlayerMovement` 上的 `External Horizontal Deceleration` 默认 6 米/秒²，
  `External Speed Max` 默认 40 米/秒，`Buoyancy Up Speed Max` 默认 3 米/秒。
  上浮效果会先制止下坠，之后施加向上加速度。
- 每区 `Seed` 控制外观与位置抽样；玩家 `FloatExperience.Seed` 控制效果袋与参数。
  相同种子和相同调用顺序可复现随机结果；玩家路径、消耗时刻和空间占用不同会改变生成结果。

四种效果各出现一次后重新洗牌，袋子交界不会连续重复；最近四次效果跨区域、跨重生保留。
水平方向分八个区，连续两次弹射不使用同一区；靠近区域边界时只从偏向区域中心的方向中抽取。
这不保证安全落地，玩家仍需空中操作。

## 测试

在 Unity 的 `Window > General > Test Runner` 中运行：

- **EditMode / FloatRandomTests**：随机袋、历史窗口、外形配额、方向防重复、固定种子。
- **PlayMode / FloatSystemTests**：三个区域的生成与占位、冷却后持续接触触发、消耗和对象池补充、
  重力替换与到期、重生保留历史、弹射衰减、浮力离地、撞顶、拥挤时重试。

PlayMode 测试会加载 `PlatformerLevel`，使用临时测试会话运行；编辑场景中的未保存更改应先保存。
测试程序集通过反射访问现有 `Assembly-CSharp`，无需重组原有玩家和关卡脚本程序集。

手动体验时检查前、中、后三段都能正常触碰，WASD 空中操作仍有效，提示不遮挡视野，
熔岩重生后速度和效果恢复。参数的最终手感仍需在实际游玩中微调。
