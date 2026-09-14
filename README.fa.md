# Raaya Git Deploy

**Raaya Git Deploy** یک ابزار تخصصی ویندوزی برای بررسی تغییرات Git و انتقال امن فایل‌های تأییدشده به هاست یا سرور است.

سناریوی اصلی محصول این است:

```text
AI / Codex / Developer
        ↓
تغییر Repository
        ↓
بررسی Git Changes
        ↓
مشاهده Diff
        ↓
اجرای Build / Test / Command
        ↓
تأیید فایل‌ها
        ↓
Deployment Queue
        ↓
Preview مسیر Local → Remote
        ↓
Dry Run
        ↓
Deploy
        ↓
Deployment History
```

هدف اصلی این برنامه ساخت یک FTP Client جدید نیست؛ بلکه ایجاد یک **Git Review & Deploy Workbench** است که مخصوص جریان توسعه مدرن و به‌خصوص تغییرات تولیدشده توسط هوش مصنوعی طراحی شده است.

## اصول اصلی

- بررسی و تأیید انسانی قبل از Deploy اهمیت اصلی دارد.
- تأیید یک تغییر با انتخاب آن برای Deploy یک مفهوم نیست.
- Git مرجع تشخیص تغییرات Repository است.
- قبل از هر Deploy باید مسیر Local → Remote کاملاً قابل مشاهده باشد.
- عملیات حذف Remote باید تأیید صریح داشته باشد.
- Credentialها هرگز داخل Repository ذخیره نمی‌شوند.
- معماری باید برای Transportها و Workflowهای آینده قابل توسعه باشد.
- پروژه از ابتدا Public و Contributor-friendly طراحی می‌شود.

## تکنولوژی پیشنهادی

- .NET 10
- WinUI 3 + Windows App SDK
- MVVM
- `git.exe`
- ConPTY برای Terminal داخلی
- SFTP/SSH به‌عنوان اولین Transport
- SSH.NET
- Windows DPAPI برای Credentialها
- SQLite برای داده‌های Local غیرمحرمانه
- WebView2 فقط در بخش‌هایی مانند Diff Viewer که استفاده از آن مزیت واقعی دارد

## مدل توسعه

این پروژه معماری و Roadmap را به مرزبندی ثابت `V1 / V2 / V3` محدود نمی‌کند.

قابلیت‌ها در یکی از این وضعیت‌ها قرار می‌گیرند:

- **Core Baseline** — قابلیت پایه و بنیادی
- **Active Development** — در حال توسعه
- **Extension Candidate** — قابلیت قابل پیشنهاد و توسعه
- **Experimental** — قابلیت آزمایشی که هنوز باید اعتبارسنجی شود

شماره نسخه همچنان می‌تواند برای Releaseهای واقعی نرم‌افزار استفاده شود، اما شماره نسخه نباید معماری یا امکان مشارکت توسعه‌دهندگان دیگر را محدود کند.

برای جزئیات بیشتر:

- `docs/architecture/PRODUCT_ARCHITECTURE.md`
- `docs/ROADMAP.md`
- `CONTRIBUTING.md`
- `docs/adr/`
