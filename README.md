# 调休日历

一个适用于 Windows 10 的日历工具。左键点任务栏右下角的时间，打开带农历和「休／班」标记的日历。

## 使用

- 滚轮每格移动一周，左右按钮翻月，「今天」返回当前月份。
- 左键点两个不同的日期，统计含首尾的总天数、工作日和休息日。补班算工作日，放假算休息日。点「开始」所在日期可取消选区。
- 右键点日期可添加日程。待办显示在日历上方，日期格带「待」徽标。点垃圾桶直接删除对应条目。
- 点击外部或按 Esc 收起日历，并清空日期选区。待办保存在本机。

## 编译和运行

在项目目录打开 PowerShell，使用 Windows 上的 .NET Framework 4.x 编译器：

```powershell
& "$env:windir\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /out:RestWorkCalendar.exe /reference:System.Web.Extensions.dll Calendar.cs
.\RestWorkCalendar.exe
```

使用 `--background` 可后台启动，点击任务栏时钟才打开日历。后台运行期间，系统托盘没有额外图标。退出程序后，任务栏时钟恢复使用 Windows 自带日历。

## 登录 Windows 后自动启动

在编译好的程序目录运行：

```powershell
$exe = (Resolve-Path .\RestWorkCalendar.exe).Path
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Set-ItemProperty -Path $runKey -Name RestWorkCalendar -Value ('"{0}" --background' -f $exe)
```

下次登录当前 Windows 用户时生效。移动程序后重新运行上面的配置命令。取消自启动或退出当前实例：

```powershell
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name RestWorkCalendar
Stop-Process -Name RestWorkCalendar -ErrorAction SilentlyContinue
```

## 数据和许可

节假日和调休来自 [Timor API](https://timor.tech/api/holiday/year/2027/)。缓存与待办保存在 `%LOCALAPPDATA%\RestWorkCalendar`。

本项目采用 [MIT 许可证](LICENSE)。调休数据和统计结果的使用限制见[免责声明](DISCLAIMER.md)。
