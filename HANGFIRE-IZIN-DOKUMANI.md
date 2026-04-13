# HANGFIRE İZİN MİMARİSİ - DETAYLI AÇIKLAMA

## 📊 VERİTABANI YAPISI

```
NEON.TECH PostgreSQL
├── belekuni (Database)
│   ├── belek_appsec (Schema)
│   │   ├── Repositories (Tablo)         ← Hangfire buraya yazmalı!
│   │   ├── Scans (Tablo)                ← Hangfire buraya yazmalı!
│   │   ├── Vulnerabilities (Tablo)      ← Hangfire buraya yazmalı!
│   │   ├── AspNetUsers (Tablo)
│   │   ├── AspNetRoles (Tablo)
│   │   └── ... (diğer Identity tabloları)
│   └── public (Schema - kullanılmıyor)
│
└── hangfire (Database)
    ├── hangfire (Schema) veya public
    │   ├── hangfire.job (Tablo)          ← Hangfire kendi tabloları
    │   ├── hangfire.state (Tablo)
    │   ├── hangfire.server (Tablo)
    │   └── ... (diğer Hangfire tabloları)
```

---

## 🔑 KULLANICI: belek04

**belek04** kullanıcısı iki database'e de bağlanıyor:
- `DefaultConnection` → **belekuni** database, **belek_appsec** schema
- `HangfireConnection` → **hangfire** database

---

## ⚠️ SORUNUN KAYNAĞI

### Hangfire Job Çalışma Akışı:
```
1. Hangfire Job başlatılır (ScanJob.ExecuteSastScanAsync)
   ↓
2. ApplicationDbContext'i kullanır (DI ile inject edilmiş)
   ↓
3. ApplicationDbContext → DefaultConnection kullanır
   ↓
4. DefaultConnection → belekuni database, belek_appsec schema
   ↓
5. Scans tablosuna INSERT/UPDATE yapmaya çalışır
   ↓
6. HATA: "permission denied for table Scans"
```

### Neden?
**Tablolar neondb_owner'a ait, belek04 kullanıcısı sadece okuma yetkisine sahip (veya hiç yetkisi yok)**

---

## ✅ GEREKLİ İZİNLER

### DATABASE 1: belekuni (Schema: belek_appsec)

**belek04 kullanıcısının sahip olması gereken izinler:**

#### A. Schema İzinleri
```sql
GRANT USAGE ON SCHEMA belek_appsec TO belek04;
```

#### B. Tablo İzinleri (ÇOK ÖNEMLİ!)
```sql
-- TÜM tablolara SELECT, INSERT, UPDATE, DELETE
GRANT SELECT, INSERT, UPDATE, DELETE 
ON ALL TABLES IN SCHEMA belek_appsec 
TO belek04;
```

**Hangi tablolara?**
- ✅ `Repositories` - Tarama yapılacak repoları saklıyor
- ✅ `Scans` - Tarama durumunu güncelliyor (HATA BURDA!)
- ✅ `Vulnerabilities` - Bulunan zafiyetleri kaydediyor
- ✅ `AspNetUsers`, `AspNetRoles`, vb. - Kullanıcı bilgileri için

#### C. Sequence İzinleri (ZORUNLU!)
```sql
-- ID üretimi için sequence'lere erişim
GRANT USAGE, SELECT, UPDATE 
ON ALL SEQUENCES IN SCHEMA belek_appsec 
TO belek04;
```

**Hangi sequence'ler?**
- `Repositories_Id_seq` - Yeni repository ID'si üretir
- `Scans_Id_seq` - Yeni scan ID'si üretir
- `Vulnerabilities_Id_seq` - Yeni vulnerability ID'si üretir

#### D. Gelecekteki Tablolar İçin Otomatik İzin
```sql
-- Yeni migration çalıştırıldığında otomatik izin
ALTER DEFAULT PRIVILEGES FOR ROLE neondb_owner 
IN SCHEMA belek_appsec 
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO belek04;

ALTER DEFAULT PRIVILEGES FOR ROLE neondb_owner 
IN SCHEMA belek_appsec 
GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO belek04;
```

---

### DATABASE 2: hangfire

**belek04 kullanıcısının sahip olması gereken izinler:**

#### A. Schema İzinleri
```sql
-- Eğer "hangfire" schema kullanılıyorsa:
GRANT USAGE ON SCHEMA hangfire TO belek04;

-- Eğer "public" schema kullanılıyorsa:
GRANT USAGE ON SCHEMA public TO belek04;
```

#### B. Tablo İzinleri
```sql
-- Hangfire'ın kendi tablolarına tam erişim
GRANT SELECT, INSERT, UPDATE, DELETE 
ON ALL TABLES IN SCHEMA hangfire  -- veya public
TO belek04;
```

**Hangi tablolara?**
- `hangfire.job` - Job kayıtları
- `hangfire.state` - Job durumları
- `hangfire.server` - Hangfire server bilgileri
- `hangfire.hash`, `hangfire.list`, `hangfire.set`, vb.

#### C. Sequence İzinleri
```sql
GRANT USAGE, SELECT, UPDATE 
ON ALL SEQUENCES IN SCHEMA hangfire  -- veya public
TO belek04;
```

---

## 🚀 İZİNLERİ VERME ADAMLARI

### ADIM 1: Mevcut Durumu Kontrol Edin

**Neon.tech Console:**
1. https://console.neon.tech/ → Projeniz → **SQL Editor**
2. Database: **belekuni** seçin
3. Şu komutu çalıştırın:

```sql
-- belek04'ün mevcut izinlerini göster
SELECT 
    table_name,
    privilege_type
FROM information_schema.table_privileges 
WHERE grantee = 'belek04' 
AND table_schema = 'belek_appsec'
ORDER BY table_name, privilege_type;
```

**Sonuç boş mu?** → belek04'ün HİÇBİR izni yok!
**Sonuç sadece SELECT?** → Yazma izni yok!
**DELETE, INSERT, SELECT, UPDATE gösteriyor mu?** → TAM YETKİ var ✓

---

### ADIM 2: İzinleri Verin

**check-and-fix-permissions.sql** dosyasını hazırladım. İçindeki BÖLÜM 2'yi çalıştırın:

```sql
-- SCHEMA izni
GRANT USAGE ON SCHEMA belek_appsec TO belek04;

-- TABLO izinleri
GRANT SELECT, INSERT, UPDATE, DELETE 
ON ALL TABLES IN SCHEMA belek_appsec TO belek04;

-- SEQUENCE izinleri
GRANT USAGE, SELECT, UPDATE 
ON ALL SEQUENCES IN SCHEMA belek_appsec TO belek04;

-- Otomatik izinler (neondb_owner tabloları için)
ALTER DEFAULT PRIVILEGES FOR ROLE neondb_owner 
IN SCHEMA belek_appsec 
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO belek04;

ALTER DEFAULT PRIVILEGES FOR ROLE neondb_owner 
IN SCHEMA belek_appsec 
GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO belek04;
```

---

### ADIM 3: Hangfire Database İzinleri (Opsiyonel)

**SQL Editor'de database değiştirin: hangfire**

```sql
-- Schema'yı kontrol et
SELECT nspname FROM pg_catalog.pg_namespace 
WHERE nspname NOT LIKE 'pg_%';

-- Eğer "hangfire" schema varsa:
GRANT USAGE ON SCHEMA hangfire TO belek04;
GRANT SELECT, INSERT, UPDATE, DELETE 
ON ALL TABLES IN SCHEMA hangfire TO belek04;
GRANT USAGE, SELECT, UPDATE 
ON ALL SEQUENCES IN SCHEMA hangfire TO belek04;

-- Eğer sadece "public" schema varsa:
GRANT SELECT, INSERT, UPDATE, DELETE 
ON ALL TABLES IN SCHEMA public TO belek04;
```

---

### ADIM 4: İzinleri Doğrulayın

```sql
-- Kritik tabloları kontrol et
SELECT 
    t.table_name,
    CASE 
        WHEN COUNT(tp.privilege_type) >= 4 THEN '✓ TAM YETKİ'
        WHEN COUNT(tp.privilege_type) > 0 THEN '⚠ KISMI YETKİ'
        ELSE '✗ YETKİ YOK'
    END as status,
    string_agg(tp.privilege_type, ', ') as privileges
FROM (
    VALUES ('Repositories'), ('Scans'), ('Vulnerabilities')
) AS t(table_name)
LEFT JOIN information_schema.table_privileges tp 
    ON tp.table_name = t.table_name 
    AND tp.table_schema = 'belek_appsec'
    AND tp.grantee = 'belek04'
GROUP BY t.table_name;
```

**BEKLENEN SONUÇ:**
```
 table_name      | status        | privileges
-----------------+---------------+--------------------------------
 Repositories    | ✓ TAM YETKİ   | DELETE, INSERT, SELECT, UPDATE
 Scans           | ✓ TAM YETKİ   | DELETE, INSERT, SELECT, UPDATE
 Vulnerabilities | ✓ TAM YETKİ   | DELETE, INSERT, SELECT, UPDATE
```

---

## 🆘 SORUN DEVAM EDİYORSA

Eğer izin vermeniz çalışmazsa, tablolar **neondb_owner**'a ait olduğu için şu çözümü deneyin:

### ÇÖZÜM: Tabloların Owner'ını Değiştirin

```sql
-- Ana tablolar
ALTER TABLE belek_appsec."Repositories" OWNER TO belek04;
ALTER TABLE belek_appsec."Scans" OWNER TO belek04;
ALTER TABLE belek_appsec."Vulnerabilities" OWNER TO belek04;

-- Identity tabloları
ALTER TABLE belek_appsec."AspNetRoles" OWNER TO belek04;
ALTER TABLE belek_appsec."AspNetUsers" OWNER TO belek04;
ALTER TABLE belek_appsec."AspNetUserRoles" OWNER TO belek04;
-- ... (diğer Identity tabloları)

-- Sequence'ler
ALTER SEQUENCE belek_appsec."Repositories_Id_seq" OWNER TO belek04;
ALTER SEQUENCE belek_appsec."Scans_Id_seq" OWNER TO belek04;
ALTER SEQUENCE belek_appsec."Vulnerabilities_Id_seq" OWNER TO belek04;
```

**Bu çözüm %100 çalışır çünkü belek04 artık tabloların sahibi olur.**

---

## 📝 ÖZETİN ÖZETİ

### belek04 kullanıcısının SAHİP OLMASI GEREKEN İZİNLER:

#### 1. belekuni database → belek_appsec schema
- ✅ SCHEMA USAGE izni
- ✅ TÜM TABLOLARA: SELECT, INSERT, UPDATE, DELETE
- ✅ TÜM SEQUENCE'LERE: USAGE, SELECT, UPDATE

#### 2. hangfire database → hangfire/public schema
- ✅ SCHEMA USAGE izni
- ✅ Hangfire tablolarına: SELECT, INSERT, UPDATE, DELETE
- ✅ Hangfire sequence'lerine: USAGE, SELECT, UPDATE

### NEDEN GEREKLİ?

**Hangfire Job'ı (ScanJob) çalıştığında:**
1. ✅ Hangfire kendi durumunu `hangfire.job` tablosuna kaydeder (hangfire DB)
2. ✅ Tarama sonuçlarını `belek_appsec.Scans` tablosuna kaydeder (belekuni DB) ← BURDA PATLADI!
3. ✅ Zafiyetleri `belek_appsec.Vulnerabilities` tablosuna kaydeder (belekuni DB)

**Her iki database'e de yazma izni şart!**

---

## 🎯 HIZLI ÇÖZÜM

**Tek komut ile tüm izinleri vermek için:**

```bash
# PowerShell'de
cd C:\Users\erasdfghjk\OneDrive\Masaüstü\SecrityScanDashboard
code check-and-fix-permissions.sql
```

Sonra:
1. Neon.tech Console → SQL Editor
2. Database: **belekuni** seç
3. BÖLÜM 2'yi kopyala-yapıştır → Run
4. Database: **hangfire** seç
5. BÖLÜM 3'ü kopyala-yapıştır → Run
6. Uygulamayı yeniden başlat
7. Test et!

---

## ✨ TEST

İzinleri verdikten sonra:

```powershell
# Uygulamayı yeniden başlat
taskkill /F /IM dotnet.exe
cd SecurityScanDashboard
dotnet run
```

1. http://localhost:5297 adresine git
2. Yeni bir Repository ekle
3. SAST taraması başlat
4. Console'da "permission denied" hatası gitmeli ✓
5. Tarama ilerleme çubuğu görünmeli (SignalR) ✓
6. Tarama başarıyla tamamlanmalı ✓
