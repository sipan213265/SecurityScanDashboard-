# PostgreSQL Schema Kullanımı - ÖNEMLİ!

## 🔴 KRİTİK BİLGİ - TÜM SINIF İÇİN

### Sorun Neydi?

Önceki versiyonda **schema tanımı yoktu**. Bu ciddi problemlere yol açabilirdi:

1. ❌ Migration dosyaları schema belirtmiyordu (varsayılan `public` kullanıyordu)
2. ❌ ApplicationDbContext schema bilmiyordu
3. ⚠️ Sadece connection string'deki `Search Path` sayesinde çalışıyordu
4. ⚠️ Farklı öğrencilerin tabloları karışabilirdi

### Çözüm Ne Yapıldı?

✅ **ApplicationDbContext.cs** düzenlendi:
```csharp
modelBuilder.HasDefaultSchema("belek_appsec");
```

✅ **Migration dosyaları** düzenlendi:
- Tüm `CreateTable`, `CreateIndex`, `DropTable` işlemlerine `schema: "belek_appsec"` eklendi
- `migrationBuilder.EnsureSchema(name: "belek_appsec")` eklendi

✅ **ApplicationDbContextModelSnapshot.cs** düzenlendi:
```csharp
modelBuilder.HasDefaultSchema("belek_appsec")
```

---

## 📋 Her Öğrenci İçin Kurulum Adımları

### 1. Veritabanı Yöneticisi (Arkadaşın) Yapacak:

#### A) Her öğrenci için AYRI SCHEMA oluştur:

```sql
-- Örnek: Belek için
CREATE SCHEMA IF NOT EXISTS belek_appsec;
CREATE SCHEMA IF NOT EXISTS belek_hangfire;
GRANT ALL PRIVILEGES ON SCHEMA belek_appsec TO belek04;
GRANT ALL PRIVILEGES ON SCHEMA belek_hangfire TO belek04;

-- Örnek: Ahmet için
CREATE SCHEMA IF NOT EXISTS ahmet_appsec;
CREATE SCHEMA IF NOT EXISTS ahmet_hangfire;
GRANT ALL PRIVILEGES ON SCHEMA ahmet_appsec TO ahmet_kullanici;
GRANT ALL PRIVILEGES ON SCHEMA ahmet_hangfire TO ahmet_kullanici;

-- Örnek: Mehmet için
CREATE SCHEMA IF NOT EXISTS mehmet_appsec;
CREATE SCHEMA IF NOT EXISTS mehmet_hangfire;
GRANT ALL PRIVILEGES ON SCHEMA mehmet_appsec TO mehmet_kullanici;
GRANT ALL PRIVILEGES ON SCHEMA mehmet_hangfire TO mehmet_kullanici;
```

#### B) Her öğrenciye kendi kullanıcısını ver:

- ✅ Her öğrenci farklı kullanıcı adı/şifre almalı
- ✅ Her öğrenci sadece kendi schema'sına erişebilmeli
- ✅ **İsimlendirme**: `{isim}_appsec` ve `{isim}_hangfire` formatında

---

### 2. Öğrenci (Her Kişi) Yapacak:

#### A) Kendi projesinde **ApplicationDbContext.cs** dosyasını düzenle:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // ÖNEMLİ: Kendi schema adını buraya yaz!
    modelBuilder.HasDefaultSchema("KENDI_ADIN_appsec"); // Örn: ahmet_appsec

    // ... geri kalan kod aynı
}
```

#### B) **appsettings.Production.json** dosyasını düzenle:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=VERITABANI_SUNUCUSU;Port=5432;Database=VERITABANI_ADI;Username=KENDI_KULLANICI_ADIN;Password=KENDI_SIFREIN;Search Path=KENDI_ADIN_appsec;SSL Mode=Require;Trust Server Certificate=true;..."
  }
}
```

**Önemli Noktalar:**
- `Username=`: Veritabanı yöneticisinin verdiği kullanıcı adı
- `Password=`: Kendi şifren
- `Search Path=`: **KENDI_ADIN_appsec** formatında
- `Database=`: Ortak veritabanı adı (örn: `belekuni`)

#### C) **Program.cs** dosyasını düzenle (Hangfire için):

```csharp
.UsePostgreSqlStorage(options =>
{
    options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
}, new Hangfire.PostgreSql.PostgreSqlStorageOptions
{
    SchemaName = "KENDI_ADIN_hangfire" // Örn: ahmet_hangfire
}))
```

#### D) **Migration dosyalarını** düzenle:

Her migration dosyasında (`Migrations/` klasöründeki tüm `.cs` dosyaları):

```csharp
// Up metodunda
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.EnsureSchema(name: "KENDI_ADIN_appsec");
    
    migrationBuilder.CreateTable(
        name: "Repositories",
        schema: "KENDI_ADIN_appsec", // BURAYA EKLE
        columns: table => new
        { ... }
    );
}

// Down metodunda
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropTable(
        name: "Repositories",
        schema: "KENDI_ADIN_appsec"); // BURAYA EKLE
}
```

**ÖNEMLİ**: Tüm `CreateTable`, `CreateIndex`, `AddColumn`, `DropTable`, `DropColumn` işlemlerine `schema: "KENDI_ADIN_appsec"` ekle!

#### E) **ApplicationDbContextModelSnapshot.cs** dosyasını düzenle:

```csharp
protected override void BuildModel(ModelBuilder modelBuilder)
{
#pragma warning disable 612, 618
    modelBuilder
        .HasDefaultSchema("KENDI_ADIN_appsec") // BURAYA EKLE
        .HasAnnotation("ProductVersion", "8.0.11")
        .HasAnnotation("Relational:MaxIdentifierLength", 63);
    // ... geri kalan kod aynı
}
```

---

## ✅ Doğrulama Nasıl Yapılır?

### Veritabanı Yöneticisi İçin:

```sql
-- Tüm schema'ları listele
SELECT schema_name 
FROM information_schema.schemata 
WHERE schema_name LIKE '%_appsec' 
   OR schema_name LIKE '%_hangfire'
ORDER BY schema_name;

-- Beklenen sonuç:
-- ahmet_appsec
-- ahmet_hangfire
-- belek_appsec
-- belek_hangfire
-- mehmet_appsec
-- mehmet_hangfire

-- Her schema'daki tabloları kontrol et
SELECT 
    table_schema,
    table_name
FROM information_schema.tables 
WHERE table_schema LIKE '%_appsec'
ORDER BY table_schema, table_name;
```

### Öğrenci İçin:

Uygulamayı başlattıktan sonra log'larda şunu görmeli:

```
PostgreSQL Server: Host: SUNUCU_ADI, DB: VERITABANI_ADI, Schema: KENDI_ADIN_appsec
```

Dashboard'da Repository, Scan, Vulnerability işlemleri sorunsuz çalışmalı.

---

## 🚨 Sık Yapılan Hatalar

### ❌ YANLIŞ: Connection string'de Search Path var ama kodda schema yok
```json
"Search Path=belek_appsec"
```
```csharp
// ApplicationDbContext.cs - Schema tanımı YOK!
modelBuilder.Entity<Repository>...
```
**Sonuç**: Migration'lar `public` schema'ya gider, veriler `belek_appsec` schema'da aranır → HATA!

---

### ✅ DOĞRU: Hem connection string hem kod içinde schema var
```json
"Search Path=belek_appsec"
```
```csharp
// ApplicationDbContext.cs
modelBuilder.HasDefaultSchema("belek_appsec");
```
```csharp
// Migration
migrationBuilder.CreateTable(
    name: "Repositories",
    schema: "belek_appsec",
    ...
```
**Sonuç**: Her şey `belek_appsec` schema'da çalışır → BAŞARILI!

---

## 📞 Sorun mu Var?

Eğer tablolar yanlış schema'da oluşturulduysa:

### Seçenek 1: Tabloları taşı
```sql
ALTER TABLE public."Repositories" SET SCHEMA belek_appsec;
ALTER TABLE public."Scans" SET SCHEMA belek_appsec;
ALTER TABLE public."Vulnerabilities" SET SCHEMA belek_appsec;
```

### Seçenek 2: Tabloları sil ve yeniden oluştur
```sql
DROP TABLE IF EXISTS public."Vulnerabilities";
DROP TABLE IF EXISTS public."Scans";
DROP TABLE IF EXISTS public."Repositories";
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
```
Sonra `database-setup.sql` dosyasını çalıştır.

---

## 🎯 Özet Kontrol Listesi

Her öğrenci için:
- [ ] Veritabanı yöneticisi `{isim}_appsec` ve `{isim}_hangfire` schema'larını oluşturdu
- [ ] Veritabanı yöneticisi kullanıcıya schema'lara GRANT verdi
- [ ] `ApplicationDbContext.cs`: `HasDefaultSchema("KENDI_ADIN_appsec")` eklendi
- [ ] `appsettings.Production.json`: `Search Path=KENDI_ADIN_appsec` ayarlandı
- [ ] `Program.cs`: `SchemaName = "KENDI_ADIN_hangfire"` ayarlandı
- [ ] Tüm migration dosyalarında `schema: "KENDI_ADIN_appsec"` eklendi
- [ ] `ApplicationDbContextModelSnapshot.cs`: `HasDefaultSchema("KENDI_ADIN_appsec")` eklendi
- [ ] Uygulama başlatıldı ve log'larda doğru schema görünüyor
- [ ] Dashboard çalışıyor ve CRUD işlemleri başarılı

---

**Son Güncelleme**: 16 Şubat 2026  
**Hazırlayan**: Belek (belek04)  
**Durum**: ✅ Production'da test edildi ve çalışıyor
