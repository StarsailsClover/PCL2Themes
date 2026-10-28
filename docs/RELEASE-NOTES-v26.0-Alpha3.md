# PCL2Themes v26.0-Alpha 3

> 歌词覆盖层与播放器增强，面向官方 PCL2 自带音乐模块（NAudio）。
> Artifact: `pcl2themes.swapaddon` · 2026.10.29

| 项 | 值 |
|---|---|
| 文件 | `pcl2themes.swapaddon` |
| SHA-256 | `8A11D638540D61F8B94AC1570B259546451CF443E1F9C206EF1282DF2DFAE3B4` |
| 宿主范围 | 官方 PCL2 `>=2.99.0 <3.0`，形状 injected / patched |

## 新增

1. **歌词引擎**
   - 读取音频同目录同名 `.lrc`（宿主本身会跳过 `.lrc` 文件，两者互不干扰）；
   - 支持 `[mm:ss.xx]` / `[mm:ss.xxx]`、一行多时间戳、`[offset:±ms]`、同时间戳第二行自动作为翻译；
   - 播放位置来自宿主 `WaveOutEvent.GetPosition()` 字节数 ÷（采样率 × 声道 × 位深 ÷ 8），
     250 ms 轮询，无额外解码开销。
2. **歌词覆盖层**
   - 窗口底部居中的半透明歌词条（当前行 + 翻译），不拦截鼠标事件；
   - 设置页可开关。
3. **播放器增强**
   - 上一首（利用宿主等待列表插入实现）、播放/暂停、下一首；
   - 音量滑条（写入宿主 `UiMusicVolume`，播放线程每 100 ms 自动生效）；
   - **外部播放器桥接**：可指定任意播放器（例如 Splayer-Next）并一键用其打开当前曲目。

## 验证记录（官方 PCL 2.99.0.1，注入形状，隔离副本）

```
music.track=...\PCL\Musics\probe-tone.wav
music.position=6.75 lyrics=4 current=probe line 4
music.position=1.83 lyrics=4 current=probe line 1
```

（8 秒测试音频：第一次采样在第 4 行歌词处，循环重播后第二次采样回到第 1 行，
歌词索引与播放位置严格同步；同一轮自检中主题/宿主路径/亚克力/字体全部 PASS。）

## 已知限制

- 宿主播放器不暴露 `AudioFileReader`，因此**暂不支持进度跳转/拖动**（Alpha 4 备选：重建播放入口）；
- Splayer-Next 是 Electron + AGPL-3.0 应用，无法内嵌到 WPF 宿主；本模块采用进程外桥接，
  不引入其代码。

<!-- Git Hub @S tar sa ils Clo ver -->
