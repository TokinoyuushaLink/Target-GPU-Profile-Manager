# EXE-Target-GPU-Profile-Manager
Windows GPU 偏好管理器\
通过读写注册表 `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` 管理每个应用的 GPU 偏好设置（与系统「图形设置」同一来源）。

<img width="800" height="509" alt="图片" src="https://github.com/user-attachments/assets/bbb70784-4755-41bf-8c79-4871c600ddfd" />

## 功能
- 查看、添加、删除 GPU 偏好条目
- 支持拖入 .exe / .lnk 快捷方式添加
- 分类管理与备注
- 多选批量操作

## 要求
Windows 10/11
.NET 8 Runtime（桌面）

## 构建
```
.\build.ps1
```
