# Veritabanı Kurulum Rehberi - belek04 Kullanıcısı İçin

## 📋 Dosyalar

1. **0-delete-old-tables.sql** → Eski tabloları siler (ÖNCE BU)
2. **database-setup.sql** → Yeni tabloları oluşturur (SONRA BU)

## 🚀 Adım Adım Kurulum

### ADIM 1: Eski Tabloları Sil

Arkadaşına `0-delete-old-tables.sql` dosyasını gönder ve şunu söyle:

```
1. Neon.tech SQL Editor'ü aç
2. belek04 kullanıcısı ile giriş yap
3. 0-delete-old-tables.sql dosyasının içeriğini kopyala
4. SQL Editor'e yapıştır
5. "Run" butonuna tıkla
6. Kontrol sorgusunun sonucu BOŞ olmalı (0 satır)
```

**Yapılan işlemler:**
- ❌ belek_appsec."Vulnerabilities" SİLİNDİ
- ❌ belek_appsec."Scans" SİLİNDİ
- ❌ belek_appsec."Repositories" SİLİNDİ
- ❌ belek_appsec."__EFMigrationsHistory" SİLİNDİ
- ❌ public schema'daki tüm tablolar SİLİNDİ (varsa)
- ♻️ belek_hangfire schema'sı YENİDEN OLUŞTURULDU

---

### ADIM 2: Yeni Tabloları Oluştur

Arkadaşına `database-setup.sql` dosyasını gönder ve şunu söyle:

```
1. Aynı SQL Editor'de
2. database-setup.sql dosyasının içeriğini kopyala
3. SQL Editor'e yapıştır
4. "Run" butonuna tıkla
5. Kontrol sorgularını çalıştır (dosyanın sonunda)
```

**Oluşturulan tablolar (belek_appsec schema'sında):**
- ✅ __EFMigrationsHistory (Migration kayıtları)
- ✅ Repositories (Repository bilgileri)
- ✅ Scans (Tarama kayıtları)
- ✅ Vulnerabilities (Bulunan zafiyetler)

**Oluşturulan index'ler:**
- ✅ IX_Repositories_Url
- ✅ IX_Scans_RepositoryId
- ✅ IX_Scans_StartedAt
- ✅ IX_Scans_Status
- ✅ IX_Vulnerabilities_ScanId
- ✅ IX_Vulnerabilities_DetectedAt
- ✅ IX_Vulnerabilities_Severity

---

### ADIM 3: Uygulamayı Test Et

Uygulamayı başlat:

```powershell
cd SecurityScanDashboard
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet run --no-launch-profile
```

**Beklenen log çıktısı:**
```
info: Hosting environment: Production
info: PostgreSQL Server: Host: ep-morning-math-ag237as9-pooler.c-2.eu-central-1.aws.neon.tech, DB: belekuni, Schema: belek_appsec
info: Hangfire Server started: Worker count: 1
```

**Test adımları:**
1. http://localhost:5000 adresine git
2. Dashboard açılmalı
3. Repository ekle (DVWA: https://github.com/digininja/DVWA)
4. SAST taraması başlat
5. Tarama bittiğinde sonuçları kontrol et

---

## ✅ Doğrulama

Arkadaşın şu sorguyla kontrol etmeli:

```sql
-- 1. Tablolar doğru schema'da mı?
SELECT table_schema, table_name
FROM information_schema.tables 
WHERE table_name IN ('Repositories', 'Scans', 'Vulnerabilities', '__EFMigrationsHistory')
ORDER BY table_schema, table_name;

-- SONUÇ:
-- belek_appsec | __EFMigrationsHistory
-- belek_appsec | Repositories
-- belek_appsec | Scans
-- belek_appsec | Vulnerabilities
-- (Toplam 4 satır, HEPSİ belek_appsec'te)
```

```sql
-- 2. public schema'da tablo var mı?
SELECT table_name
FROM information_schema.tables 
WHERE table_schema = 'public' 
  AND table_name IN ('Repositories', 'Scans', 'Vulnerabilities');

-- SONUÇ: BOŞ (0 satır) olmalı!
```

---

## 🎯 Özet

**NE YAPILDI:**
- ✅ Eski tablolar belek_appsec ve public'ten silindi
- ✅ belek_hangfire schema yeniden oluşturuldu
- ✅ Yeni tablolar SADECE belek_appsec'te oluşturuldu
- ✅ Migration kayıtları eklendi
- ✅ Index'ler oluşturuldu

**ÖNEMLİ:**
- ✅ public schema'ya HİÇBİR ŞEY oluşturulmadı
- ✅ Sadece belek04 kullanıcısı belek_appsec'e erişebilir
- ✅ Diğer öğrencilerle hiçbir çakışma yok
- ✅ ApplicationDbContext ve Migration'lar schema belirtiyor

**SONRAKİ ADIMLAR:**
1. İlk taramayı yap
2. Sonuçları kontrol et
3. Arkadaşına veritabanında verileri görmesini söyle
4. Her şey çalışıyorsa bitirme projesine devam et! 🎉
