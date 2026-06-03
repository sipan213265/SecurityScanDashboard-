# Security Scan Dashboard

> **Güvenlik Tarama Panosu** — Yazılım projelerinin güvenlik açıklarını otomatik olarak tespit eden, SAST ve DAST araçlarını tek bir web arayüzünde birleştiren platform.

**Antalya Belek Üniversitesi — Yazılım Mühendisliği Bölümü**  
Mezuniyet Projesi | Öğrenci: Sipan Medeni (220007016) | Danışman: Dr. Öğr. Üyesi Adem ŞİMŞEK

---

## İçindekiler

- [Proje Amacı](#proje-amacı)
- [Özellikler](#özellikler)
- [Teknoloji Yığını](#teknoloji-yığını)
- [Sistem Mimarisi](#sistem-mimarisi)
- [Ön Gereksinimler](#ön-gereksinimler)
- [Kurulum ve Çalıştırma](#kurulum-ve-çalıştırma)
- [Proje Yapısı](#proje-yapısı)
- [Yapılandırma](#yapılandırma)
- [Kullanım](#kullanım)
- [API Belgelendirmesi](#api-belgelendirmesi)
- [Güvenlik Notları](#güvenlik-notları)
- [Lisans](#lisans)

---

## Proje Amacı

Security Scan Dashboard, yazılım geliştirme sürecinde güvenliği erken aşamaya taşımak amacıyla geliştirilmiştir. Platform iki temel güvenlik test yöntemini entegre eder:

- **SAST (Static Application Security Testing)** — Semgrep ile kaynak kod analizi: kodu çalıştırmadan kaynak kodundaki güvenlik açıklarını tespit eder.
- **DAST (Dynamic Application Security Testing)** — Nuclei ile dinamik tarama: çalışan uygulamaya HTTP istekleri göndererek gerçek güvenlik açıklarını bulur.

Kullanıcılar tek bir web arayüzünden proje ve depolarını yönetebilir, tarama başlatabilir, sonuçları görüntüleyebilir ve PDF rapor üretebilir.

---

## Özellikler

| Özellik | Açıklama |
|---------|----------|
| **Proje Yönetimi** | Depolar projeler altında gruplandırılır; çok kullanıcılı RLS korumalı izolasyon |
| **SAST Taraması** | Semgrep ile GitHub depoları Docker container içinde taranır (+400 kural) |
| **DAST Taraması** | Nuclei ile hedef URL taraması; rate-limited güvenli çalışma (-rl 50 -c 10) |
| **Gerçek Zamanlı Bildirim** | SignalR WebSocket ile tarama ilerlemesi anlık olarak iletilir |
| **Açık Yönetimi** | Doğrulandı / Yanlış Pozitif / Kabul Edildi durum iş akışı |
| **PDF Raporlama** | iTextSharp ile tarama sonuçlarından otomatik PDF rapor üretimi |
| **E-posta Bildirimi** | Tarama tamamlandığında kullanıcıya otomatik bildirim gönderilir |
| **REST API** | Swagger UI ile belgelenmiş 15+ endpoint |
| **Yönetici Paneli** | Kullanıcı/rol yönetimi, Serilog günlükleri, Hangfire dashboard |
| **OWASP Top 10** | 10/10 kategori Semgrep + Nuclei kombinasyonuyla taranır |

---

## Teknoloji Yığını

| Katman | Teknoloji | Versiyon |
|--------|-----------|---------|
| Back-End Framework | ASP.NET Core MVC | 8.0 |
| Veritabanı | PostgreSQL | 16 |
| ORM | Entity Framework Core | 8.0 |
| Arka Plan Görevleri | Hangfire | 1.8 |
| Gerçek Zamanlı | SignalR | (ASP.NET Core dahili) |
| SAST Aracı | Semgrep | latest (Docker) |
| DAST Aracı | Nuclei | latest (Docker) |
| Konteyner | Docker + Docker Compose | - |
| Kimlik Doğrulama | BCrypt + Cookie Auth | - |
| Raporlama | iTextSharp | 5.5 |
| UI | Bootstrap 5 + Chart.js | 5.3 |
| Günlükleme | Serilog | 3.x |

---

## Sistem Mimarisi

```
┌────────────────────────────────────────────────────┐
│               SUNUM KATMANI                        │
│   MVC Views (Razor) │ SignalR Client │ Swagger     │
└────────────────────┬───────────────────────────────┘
                     │ HTTP / WebSocket
┌────────────────────▼───────────────────────────────┐
│              İŞ MANTIĞI KATMANI                    │
│  ASP.NET Core 8  │  Hangfire  │  AuthService       │
│  SemgrepService  │  NucleiService  │  EmailService  │
└────────────────────┬───────────────────────────────┘
                     │ Entity Framework Core
┌────────────────────▼───────────────────────────────┐
│               VERİ KATMANI                         │
│   PostgreSQL + RLS Politikaları │ Docker Volumes   │
└────────────────────────────────────────────────────┘
```

---

## Ön Gereksinimler

Projeyi çalıştırabilmek için aşağıdaki araçların sisteminizde kurulu olması gerekir:

| Araç | Versiyon | İndirme Linki |
|------|---------|--------------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| Docker Desktop | 24.0+ | https://www.docker.com/products/docker-desktop |
| Git | 2.x+ | https://git-scm.com/downloads |

> **Not:** PostgreSQL, Semgrep ve Nuclei Docker üzerinden otomatik olarak ayağa kalkar; manuel kurulum gerekmez.

---

## Kurulum ve Çalıştırma

### 1. Depoyu Klonlayın

```bash
git clone https://github.com/sipan213265/SecurityScanDashboard-.git
cd SecurityScanDashboard-
```

### 2. Yapılandırma Dosyasını Hazırlayın

`appsettings.json` dosyası varsayılan olarak **localhost PostgreSQL** bağlantısı içerir.  
Üretim ortamı için `appsettings.Production.json.example` dosyasını kopyalayıp doldurun:

```bash
cp SecurityScanDashboard/appsettings.Production.json.example SecurityScanDashboard/appsettings.Production.json
# Dosyayı açıp YOUR_DB_HOST, YOUR_DB_USER, YOUR_DB_PASSWORD alanlarını doldurun
```

### 3. Docker Servislerini Başlatın

```bash
docker-compose up -d
```

Bu komut şunları başlatır:
- **PostgreSQL** (port 5432) — ana veritabanı
- Gerekli ağ ve volume yapısı

### 4. Veritabanını Oluşturun

```bash
cd SecurityScanDashboard
dotnet ef database update
```

> İlk kullanımda tüm tablolar, şemalar ve RLS politikaları otomatik oluşturulur.

### 5. Uygulamayı Çalıştırın

```bash
dotnet run
```

Uygulama aşağıdaki adreslerde erişilebilir olur:

| Adres | İçerik |
|-------|--------|
| `http://localhost:5297` | Ana uygulama |
| `http://localhost:5297/swagger` | REST API belgesi |
| `http://localhost:5297/hangfire` | Arka plan görev paneli |

### 6. İlk Giriş

Uygulama başlarken otomatik olarak bir **admin hesabı** oluşturulur:

```
Kullanıcı adı: admin@securityscan.com
Şifre: Admin@123
```

> Güvenlik için ilk girişte şifreyi değiştirmeniz önerilir.

---

## Proje Yapısı

```
SecrityScanDashboard/
├── docker-compose.yml             # Docker servis tanımları
├── requirements.txt               # Python bağımlılıkları (belgeleme scriptleri)
├── SecurityScanDashboard/
│   ├── Program.cs                 # Uygulama giriş noktası, DI kayıtları
│   ├── SecurityScanDashboard.csproj  # NuGet bağımlılıkları
│   ├── appsettings.json           # Geliştirme ortamı yapılandırması
│   ├── appsettings.Production.json.example  # Üretim örnek yapılandırması
│   │
│   ├── Controllers/               # MVC Controller'lar
│   │   ├── AccountController.cs   # Kimlik doğrulama (giriş/çıkış/kayıt)
│   │   ├── AdminController.cs     # Yönetici işlemleri
│   │   ├── HomeController.cs      # Dashboard
│   │   ├── ProjectController.cs   # Proje CRUD
│   │   ├── RepositoryController.cs # Depo CRUD
│   │   ├── ScanController.cs      # Tarama başlatma/görüntüleme
│   │   └── Api/
│   │       └── ScansController.cs # REST API endpoint'leri
│   │
│   ├── Models/                    # Veri modelleri
│   │   ├── AppSetting.cs
│   │   ├── Project.cs
│   │   ├── Repository.cs
│   │   ├── Scan.cs
│   │   ├── User.cs
│   │   ├── UserRole.cs
│   │   └── Vulnerability.cs
│   │
│   ├── Services/                  # İş mantığı servisleri
│   │   ├── SemgrepService.cs      # SAST — Semgrep Docker entegrasyonu
│   │   ├── NucleiService.cs       # DAST — Nuclei Docker entegrasyonu
│   │   ├── AuthenticationService.cs # BCrypt + Cookie kimlik doğrulama
│   │   ├── EmailService.cs        # SMTP e-posta bildirimi
│   │   ├── PdfReportService.cs    # iTextSharp PDF rapor üretimi
│   │   ├── ReportService.cs       # Raporlama yardımcıları
│   │   └── SettingsService.cs     # Dinamik uygulama ayarları
│   │
│   ├── Jobs/                      # Hangfire arka plan görevleri
│   │   ├── ScanJob.cs             # Tarama yürütme görevi
│   │   └── CleanupJob.cs          # Geçici dosya temizleme
│   │
│   ├── Hubs/
│   │   └── ScanHub.cs             # SignalR gerçek zamanlı hub
│   │
│   ├── Data/
│   │   ├── ApplicationDbContext.cs # EF Core DbContext + RLS politikaları
│   │   └── CustomHistoryRepository.cs # Hangfire özel geçmiş
│   │
│   ├── Attributes/                # Özel doğrulama nitelikleri
│   │   ├── GitHubUrlAttribute.cs
│   │   └── UrlValidAttribute.cs
│   │
│   ├── DTOs/                      # Veri transfer nesneleri
│   │   ├── ApiResponse.cs
│   │   ├── DashboardDto.cs
│   │   ├── RepositoryDto.cs
│   │   ├── ScanDto.cs
│   │   └── VulnerabilityDto.cs
│   │
│   ├── Migrations/                # EF Core veritabanı göç dosyaları
│   ├── Views/                     # Razor (.cshtml) görünümler
│   └── wwwroot/                   # Statik dosyalar (CSS, JS, resimler)
│
└── init-db.sh/                    # PostgreSQL şema başlatma scripti
```

---

## Yapılandırma

### `appsettings.json` Referansı

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=securityscandb;Username=postgres;Password=postgres"
  },
  "ScanSettings": {
    "MaxConcurrentScans": 2,
    "ScanTimeoutMinutes": 30,
    "MaxRepoSizeMB": 500,
    "TempDirectory": "./temp"
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "noreply@securityscan.com",
    "SendEmailOnScanComplete": true
  }
}
```

### Ortam Değişkenleri

Hassas bilgileri (şifreler, API anahtarları) `appsettings.Production.json` dosyasına koyun.  
Bu dosya `.gitignore` ile versiyon kontrolünden hariç tutulmuştur.  
Şablonu kopyalayın: `appsettings.Production.json.example` → `appsettings.Production.json`

---

## Kullanım

### Temel İş Akışı

```
Proje Oluştur → Depo Ekle → Tarama Başlat → Sonuçları İncele → PDF Rapor Al
```

1. **Proje Oluştur**: Projeler > Yeni Proje
2. **Depo Ekle**: Proje detayı > Depo Ekle (GitHub URL girin)
3. **SAST Taraması**: Depo detayı > "SAST Taraması Başlat" — Semgrep kaynak kodu analiz eder
4. **DAST Taraması**: Depo detayı > "DAST Taraması Başlat" — Nuclei hedef URL'yi tarar
5. **Sonuçlar**: Tarama detayı > Güvenlik açıkları severity'e göre listelenir
6. **Doğrulama**: Her açığı "Doğrulandı", "Yanlış Pozitif" veya "Kabul Edildi" olarak işaretleyin
7. **Rapor**: Tarama detayı > "PDF Rapor İndir"

---

## API Belgelendirmesi

Swagger UI: `http://localhost:5297/swagger`

Temel endpoint'ler:

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| `GET` | `/api/scans` | Tüm taramaları listele |
| `GET` | `/api/scans/{id}` | Tarama detayı |
| `GET` | `/api/scans/{id}/vulnerabilities` | Tarama açıkları |
| `POST` | `/Scan/StartSast/{repoId}` | SAST taraması başlat |
| `POST` | `/Scan/StartDast/{repoId}` | DAST taraması başlat |

---

## Güvenlik Notları

- `appsettings.Production.json` — **git ile takip edilmez** (`.gitignore` ile hariç tutulmuştur)
- `appsettings.Development.json` — **git ile takip edilmez**
- Şifreler BCrypt ile hashlenip saklanır; açık metin şifre veritabanında tutulmaz
- Her kullanıcı yalnızca kendi verilerine erişir (PostgreSQL Row-Level Security)
- Nuclei taramaları rate-limited çalışır (`-rl 50 -c 10`) — hedef sisteme aşırı yük bindirmez
- Tüm URL girişleri `GitHubUrlAttribute` ve `UrlValidAttribute` ile doğrulanır (SSRF koruması)

---

## .NET Bağımlılıkları

.NET bağımlılıkları `SecurityScanDashboard.csproj` üzerinden NuGet ile yönetilir.  
Aşağıdaki komut tüm paketleri otomatik indirir:

```bash
dotnet restore
```

Başlıca NuGet paketleri:

| Paket | Amaç |
|-------|------|
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL EF Core sağlayıcısı |
| Hangfire.AspNetCore + Hangfire.PostgreSql | Arka plan görev sistemi |
| Microsoft.AspNetCore.SignalR | Gerçek zamanlı WebSocket |
| BCrypt.Net-Next | Şifre hashleme |
| iTextSharp | PDF rapor üretimi |
| Serilog.AspNetCore | Yapılandırılmış günlükleme |

---

## Lisans

MIT License — Bkz. [LICENSE](LICENSE) dosyası.
