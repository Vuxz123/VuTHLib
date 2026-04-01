# VuTH Lib — CI/CD Setup Guide

## Overview

GitHub Actions tự động export từng module Unity package và tạo GitHub Release khi có tag `v*`.

## Workflows

| File | Trigger | Purpose |
|------|---------|---------|
| `export-packages.yml` | Push to `main`, PR, tag, manual | Export packages + optional release |
| `activate-license.yml` | Manual / monthly cron | Renew Unity license |

## Setup Steps

### 1. Unity License

Unity trên CI runner cần license. Có 2 cách:

#### Cách 1: Manual Activation (miễn phí, cần renew hàng tháng)

1. Chạy workflow `Activate Unity License` (workflow_dispatch)
2. Tải artifact `unity-license-2022.3.x.ulf` từ Actions tab
3. Decode:
   ```bash
   base64 -d unity_lic.ulf > decoded.ulf
   ```
4. Copy nội dung `decoded.ulf` (dạng text)
5. Vào **GitHub repo → Settings → Secrets → Actions → New repository secret**
6. Tạo secret `UNITY_LICENSE` với nội dung vừa decode

#### Cách 2: Unity Subscription (khuyến nghị cho production)

1. Mua **Unity Plus or Pro** subscription
2. Dùng `game-ci/unity-actions` với credentials:
   - `UNITY_EMAIL`
   - `UNITY_PASSWORD`
   - `UNITY_SERIAL` (optional)

### 2. Push to GitHub

```bash
cd "C:\Users\DPC00176\VuTH Lib"
git init
git add .
git commit -m "Initial VuTH Lib"
git branch -M main
git remote add origin https://github.com/<your-username>/<your-repo>.git
git push -u origin main
```

### 3. Verify CI

1. Mở **GitHub repo → Actions** tab
2. Thấy workflow `Build & Export Unity Packages` chạy
3. Kiểm tra artifact: vào run → Artifacts → tải `.unitypackage`

## Trigger Package Export

### Tự động
- Push lên `main`/`master`
- Pull request vào `main`/`master`
- Tạo tag: `git tag v1.0.0 && git push origin v1.0.0`

### Thủ công
1. Repo → Actions → `Build & Export Unity Packages`
2. Click **Run workflow**
3. Chọn branch + các options

## Packages Export Format

| Module | Package File |
|--------|-------------|
| Audio | `VuTH.Audio.unitypackage` |
| Window | `VuTH.Window.unitypackage` |
| Window.Transition | `VuTH.Window.Transition.unitypackage` |
| Screen | `VuTH.Screen.unitypackage` |
| ScreenFlow | `VuTH.ScreenFlow.unitypackage` |
| Pool | `VuTH.Pool.unitypackage` |
| Bootstrap | `VuTH.Bootstrap.unitypackage` |
| GameCycle | `VuTH.GameCycle.unitypackage` |
| Persistant | `VuTH.Persistant.unitypackage` |

## Adding New Package

Sửa `Assets/_VuTH/Editor/PackageExporter.cs`:

```csharp
var packages = new[]
{
    // ... existing packages ...
    ("Assets/_VuTH/Core/NewPackage", "VuTH.NewPackage"),  // ← thêm vào đây
};
```

Và sửa `export-packages.yml` matrix `package` list.

## Troubleshooting

### "License is invalid"
→ License hết hạn hoặc sai. Chạy lại `Activate Unity License` workflow.

### "Library folder is missing"
→ Unity chưa build lần đầu. CI sẽ tự cache Library qua `actions/cache`.

### Build timeout
→ Tăng `timeout-minutes` trong workflow hoặc dùng `runs-on: self-hosted`.

## Self-Hosted Runner (nhanh hơn)

Nếu máy mạnh, cài runner trên máy local:

```bash
# Trên máy Windows
cd C:\actions-runner
./run.cmd
```

Rồi sửa workflow:
```yaml
runs-on: self-hosted
```
