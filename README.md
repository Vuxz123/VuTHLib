# VuTH Lib — Publishing Guide

## Workflow

```
1. Open Unity project
2. Menu: Window > VuTH > Export Package
3. Commit + tag + push
4. GitHub Release tự tạo
```

---

## Publish (Local Build)

### Bước 1: Export Package

Mở Unity → **Window → VuTH → Export Package**

Package sẽ được export ra: `<repo-root>/VuTH.Lib.unitypackage`

### Bước 2: Commit và Tag

```bash
git add VuTH.Lib.unitypackage
git commit -m "Release v1.0.0"
git tag v1.0.0
git push origin main
git push origin v1.0.0
```

### Bước 3: GitHub Release

Sau khi push tag → **Actions** tab → workflow `Create Release` chạy → tự tạo GitHub Release với file `.unitypackage` đính kèm.

---

## Consume (Project Khác)

### Cách 1: Import .unitypackage
1. Tải `VuTH.Lib.x.x.x.unitypackage` từ GitHub Releases
2. Unity → **Assets → Import Package → Custom Package**
3. Chọn file → Import

### Cách 2: Git Submodule / Clone
```bash
# Clone as submodule
git submodule add https://github.com/<user>/VuTH-Lib.git Assets/VuTH-Lib
```

---

## File Structure

```
VuTH-Lib/
├── Assets/_VuTH/               ← source code
│   ├── Editor/PackageExporter.cs
│   ├── Common/
│   └── Core/
├── VuTH.Lib.unitypackage       ← built artifact (committed)
├── .github/workflows/release.yml
└── README.md
```

---

## CI Workflow

`.github/workflows/release.yml` chạy khi có tag `v*`:

1. Checkout code (đã có `.unitypackage` committed)
2. Tạo GitHub Release với file `.unitypackage` đính kèm

---

## Troubleshooting

### Không thấy menu "Window > VuTH"
→ Đợi Unity recompile assemblies. Kiểm tra Console có lỗi không.

### .unitypackage lớn quá cho git
→ Nên dùng **Git LFS** (Large File Storage):

```bash
git lfs install
git lfs track "*.unitypackage"
git add .gitattributes
```

Sau đó commit như bình thường. GitHub Free đã có 2GB storage + 10GB bandwidth/month cho LFS.

### Quên commit .unitypackage trước khi tag
```bash
# Tag đã push, quên commit package
git add VuTH.Lib.x.x.x.unitypackage
git commit --amend --no-edit
git push --force origin v1.0.0
# ⚠️ Force push tag — cẩn thận nếu đã share
```
