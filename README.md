# Billease Pro - Modern Enterprise Billing & ERP Suite 🚀

> **Engineered & Automated by NexaAutomate**  
> GitHub: [@thevirajdev](https://github.com/thevirajdev) | Social: [@thevirajrealm](https://instagram.com/thevirajrealm)

---

## 🌟 Overview

**Billease Pro** is a high-performance, enterprise-grade Desktop Billing, Inventory, and Accounting ERP solution built with **C# .NET 8** and **Windows Forms (with WPF Hardware Acceleration)**. Designed for modern retail, wholesale, and service enterprises, Billease Pro provides real-time multi-terminal database synchronization (powered by Supabase Cloud PostgreSQL), offline fallback, AI-assisted purchase invoice extraction, borderless animated splash screens, and customizable invoice templates.

---

## 📸 Key Highlights

- **⚡ Borderless Video Splash Screen**: Smooth MP4 startup animation powered by WPF hardware-accelerated rendering.
- **☁️ Supabase Cloud & Local SQLite Dual-Engine**: Secure direct PostgreSQL cloud connection pooling with automatic local SQLite offline caching.
- **🤖 AI-Powered Purchase Import**: Upload PDF invoices directly to auto-extract items, quantities, rates, tax splits, and seller details without manual data entry.
- **⚙️ Enterprise Centralized Configuration System**: 60+ configurable app options spanning Company Branding, Invoicing, Tax, Printing, Database, Security, Theme, and Notifications.
- **📄 Advanced Invoice Customizer**: Real-time toggling of Terms & Conditions, Bank Details, QR Codes, Tax Summaries, and Logo positioning across PDF generation, Editor, and Thermal/A4 Printing.
- **♻️ Detailed Audit & Recycle Bin**: Human-readable soft-delete recovery system showing item names, deleted timestamp, category, and restored attributes instead of raw JSON.
- **📦 Legacy Data Importer & Full Data Backup System**: Full `.bak` snapshot export/import engine with structural version migration.
- **📖 Embedded In-App Manual**: Built-in User Manual & Developer Guide accessible directly from the Help menu (`F1`).

---

## 🛠️ Technology Stack

- **Framework**: .NET 8.0 (C# 12)
- **UI Framework**: WinForms + WPF `ElementHost` (for hardware-accelerated media & SVG rendering)
- **Database Engine**: 
  - **Cloud**: Supabase PostgreSQL (via Npgsql Connection Pooling)
  - **Local**: SQLite / EF Core 8.0
- **PDF & Reporting**: QuestPDF / iTextSharp / System.Drawing.Printing
- **Installer**: Inno Setup 6 (Custom Win32 Setup Wizard)

---

## ⌨️ Global Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `F1` | Open User Manual & Documentation |
| `F2` | New POS Invoice / Quick Sale |
| `F3` | Item Master / Inventory Search |
| `F4` | Customer Ledger & Quick Add |
| `F5` | Refresh Live Cloud Data |
| `Ctrl + P` | Print Current Invoice |
| `Ctrl + S` | Save / Finalize Bill |
| `Ctrl + Shift + B` | System Data Backup (`.bak`) |
| `Escape` | Close Modal / Cancel Action |

---

## ⚙️ Configuration & Database Setup

Billease Pro utilizes an encrypted enterprise `AppSettings.cs` architecture stored locally in `%AppData%/BilleasePro/config.json`.

### Supabase Cloud Connection String
To configure your cloud database, set the connection string in your deployment environment or `config.json`:

```text
postgresql://postgres.biptbwyfuzcuzufhustj:[PASSWORD]@aws-0-ap-southeast-1.pooler.supabase.com:5432/postgres
```

> **Security Note**: Database connection secrets are compiled into protected binary memory and handled via environment variables / user-restricted AppData storage to prevent reverse engineering.

---

## 🚀 Building & Publishing

### 1. Prerequisites
- Windows 10/11 x64
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (for building installer executable)

### 2. Build Release Executable
```powershell
dotnet clean
dotnet build -c Release
```

### 3. Run Self-Diagnostic Tests
```powershell
dotnet run -c Release --project BillingSuite.App/BillingSuite.App.csproj -- --settings-selftest
```

### 4. Publish Standalone Win-x64 Application
```powershell
dotnet publish BillingSuite.App/BillingSuite.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false
```

### 5. Generate Installer Package (`BilleasePro_Setup.exe`)
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```
*Outputs installer to `./publish_output/BilleasePro_Setup.exe`*

---

## 👨‍💻 Author & Credits

Designed, Developed & Maintained by **NexaAutomate**.

- **Lead Developer**: [@thevirajdev](https://github.com/thevirajdev)
- **Social Profile**: [@thevirajrealm](https://instagram.com/thevirajrealm)
- **Repository**: [https://github.com/thevirajdev/Billease-pro](https://github.com/thevirajdev/Billease-pro)

*Copyright © 2026 NexaAutomate. All rights reserved.*
