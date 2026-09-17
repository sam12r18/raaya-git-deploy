# راه‌اندازی محلی Raaya Git Deploy

این راهنما برای اجرای شاخه توسعه فعلی پروژه روی Windows نوشته شده است.

## پیش‌نیازها

- Windows 10 نسخه 1809 یا جدیدتر؛ Windows 11 پیشنهاد می‌شود.
- Git for Windows و در دسترس بودن دستور `git` از PowerShell.
- .NET SDK مطابق `global.json`. در وضعیت فعلی پروژه SDK پایه `10.0.301` است و `latestFeature` مجاز است.
- برای توسعه UI، Visual Studio 2022/نسخه‌ای که .NET 10 و WinUI 3 / Windows App SDK را پشتیبانی کند پیشنهاد می‌شود.
- معماری اجرای فعلی برنامه `x64` است.

## 1. دریافت سورس و انتخاب شاخه

```powershell
git clone https://github.com/sam12r18/raaya-git-deploy.git
cd raaya-git-deploy
git fetch --all --prune
git switch feat/foundation-git-review
git pull --ff-only
```

اگر repository از قبل روی سیستم وجود دارد:

```powershell
cd <path-to-raaya-git-deploy>
git fetch origin
git switch feat/foundation-git-review
git pull --ff-only origin feat/foundation-git-review
```

وضعیت را کنترل کنید:

```powershell
git status
git branch --show-current
git log -1 --oneline
```

## 2. کنترل ابزارها

```powershell
git --version
dotnet --version
dotnet --info
```

`dotnet --version` باید SDK سازگار با `global.json` را نشان دهد. اگر خطای SDK دریافت شد، .NET 10 SDK موردنیاز پروژه را نصب/به‌روزرسانی کنید.

## 3. Restore و Build

از ریشه repository اجرا کنید:

```powershell
dotnet restore RaayaGitDeploy.slnx
dotnet build RaayaGitDeploy.slnx -c Debug --no-restore
```

Build باید بدون error تمام شود. پروژه warnings را به‌عنوان errors در نظر می‌گیرد، بنابراین warningهای کامپایل را نادیده نگیرید.

## 4. اجرای تست‌ها

برای نزدیک بودن به CI، پروژه‌های تست را جداگانه اجرا کنید:

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug --no-build
```

فقط تستی را موفق در نظر بگیرید که واقعاً اجرا شده و نتیجه Passed داشته باشد.

## 5. اجرای برنامه

برنامه WinUI از پروژه زیر اجرا می‌شود:

```powershell
dotnet run --project src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj -c Debug
```

پروژه App یک WinUI 3 unpackaged application با target فعلی `net10.0-windows10.0.26100.0` و `x64` است.

در Visual Studio نیز می‌توانید `RaayaGitDeploy.slnx` را باز کنید، `RaayaGitDeploy.App` را Startup Project قرار دهید و با Debug/x64 اجرا کنید.

## 6. Smoke Test دستی پس از اجرا

بعد از باز شدن برنامه این موارد را دستی بررسی کنید:

1. پنجره اصلی بدون crash باز شود.
2. `Open Repository` را بزنید و یک Git repository واقعی را انتخاب کنید.
3. نام branch و HEAD نمایش داده شود.
4. Working Tree / Changes نمایش داده شود.
5. یک فایل تغییرکرده را انتخاب و Diff را بررسی کنید.
6. Compare را با یک ref معتبر مثل `HEAD~1` امتحان کنید.
7. Review state و انتخاب فایل برای deploy مستقل از هم رفتار کنند.
8. repository دیگری را باز کنید و بررسی کنید اطلاعات repository قبلی در UI باقی نماند.

این موارد تست تعاملی هستند و CI جایگزین آن‌ها نیست.

## 7. دستور سریع برای بروزرسانی روزهای بعد

```powershell
git switch feat/foundation-git-review
git pull --ff-only origin feat/foundation-git-review
dotnet restore RaayaGitDeploy.slnx
dotnet build RaayaGitDeploy.slnx -c Debug --no-restore
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug --no-build
dotnet run --project src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj -c Debug
```

## خطاهای متداول

### .NET SDK پیدا نمی‌شود

ابتدا اجرا کنید:

```powershell
dotnet --list-sdks
```

و مطمئن شوید SDK سازگار با `global.json` نصب است.

### `git` شناخته نمی‌شود

Git for Windows را نصب کنید و PowerShell/Terminal را دوباره باز کنید. سپس `git --version` را اجرا کنید.

### Build مربوط به Windows/WinUI خطا می‌دهد

پروژه UI فقط برای Windows ساخته شده است. Windows SDK و workload/componentهای لازم برای WinUI/Windows App SDK را در Visual Studio Installer کنترل کنید.

### برنامه build می‌شود ولی باز نمی‌شود

برنامه را یک بار از PowerShell با `dotnet run` اجرا کنید تا exception یا startup error در خروجی قابل مشاهده باشد و همان خروجی کامل را برای بررسی نگه دارید.
