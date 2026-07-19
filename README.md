# lllj-composite

**Q1：这是什么？**

A1：适用于柚子社（YUZU SOFT）GalGame游戏的立绘合成工具。

<img src="https://github.com/MLChinoo/lllj-composite/blob/master/screenshot.jpg" style="zoom:50%;" />

当前支持所有国际中文版（ Steam 及 Hikari Field 发行版本）游戏，均已经过测试：

- 千恋＊万花 Steam 版
- Riddle Joker Steam 版
- 星光咖啡馆与死神之蝶 Steam 版（`hashed`, HxNames.lst）
- 魔女的夜宴 Steam 版（`hashed`, [HxNames.lst](https://github.com/MLChinoo/sanoba_hxnames)）
- 天使☆嚣嚣 RE-BOOT!  Hikari Field 版（`hashed`, [HxNames.lst](https://github.com/MLChinoo/ten_sz_hxnames)）
- DRACU-RIOT! Steam 版（`hashed`, [HxNames.lst](https://github.com/2778995958/gal_tachie_ai/blob/main/yuzu/HxNames-DR.lst)）

理论上支持日文原版游戏，未作全量测试：

- 魔女的夜宴 日文原版
- Limelight Lemonade Jam 日文原版（`hashed`, [HxNames.lst](https://github.com/MLChinoo/lllj_hxnames)）
- ……………………

致力于支持所有使用 Krkr 引擎的游戏。

**Q2：为什么选择这个工具？**

A2：这个工具的目标是尽可能地简化立绘合成的工作，优点包括但不限于：

1. **还原准确**
   图层合成逻辑参照 KrkrZ 源码实现，力求让合成效果尽可能贴近游戏中的真实立绘。
2. **简单易用**
   界面直观、操作简单，无需手动转换、定位和排序图层，轻松完成立绘组合。
3. **高效导出**
   支持立绘批量导出，大幅减少重复操作，最大限度提升处理效率。

**Q3：如何使用？**

A3：使用如下，请自行尝试：

1. 使用 [GARbro](https://github.com/crskycode/GARbro) 解包 `fgimage.xp3` ；
   -  A1 中标注 `hashed` 的游戏，其内部资源的文件名经过哈希处理，需要加载特定的哈希表 `HxNames.lst` 来还原文件名。详细操作步骤参见 A1 附出的仓库。
   - 有些分包较为奇葩的游戏（如天使☆嚣嚣 RE-BOOT!  Hikari Field 版），其立绘文件并不全部包含在 `fgimage.xp3` 中。你需要多解一些其他相关的包，如 `upgrade.xp3` , `adult.xp3` 。若不确定，你也可以解包所有 xp3 并全部加载。
2. 打开此工具，选择解包后的 `fgimage` 目录，再选择所需的 `Character` , `Pose` , `Dress` , `Addition` , `Face` 即可显示单张立绘；
3. 点击 `Export` 可导出单张立绘；使用 `Batch Export` 菜单可按层级范围批量导出。