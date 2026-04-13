# 📋 VERİTABANI KURULUM TALİMATLARI

## Arkadaşın İçin Adım Adım Kurulum

### ✅ GEREKSINIMLER
- Neon.tech hesabı ✓
- PostgreSQL Database: belekuni ✓
- Kullanıcı: belek04 ✓

---

## 🚀 KURULUM ADIMLARI

### 1️⃣ Neon.tech'e Giriş Yap
- Neon.tech dashboard'una git
- `belekuni` veritabanını aç

### 2️⃣ SQL Editor'ü Aç
- Sol menüden **SQL Editor** seç
- Veya **Query** butonuna tıkla

### 3️⃣ SQL Script'i Çalıştır

**OPSİYON A - Dosyadan:**
- `database-setup.sql` dosyasını aç
- Tüm içeriği kopyala
- SQL Editor'e yapıştır
- **Run** butonuna tıkla

**OPSİYON B - Manuel:**
```sql
-- Veritabanına bağlan
\c belekuni

-- Sonra database-setup.sql içindeki tüm komutları çalıştır
```

### 4️⃣ Kontrol Et

Tablolar oluştu mu kontrol et:
```sql
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
ORDER BY table_name;
```

**Görmesi Gerekenler:**
- ✅ Repositories
- ✅ Scans
- ✅ Vulnerabilities
- ✅ __EFMigrationsHistory

### 5️⃣ Bize Bildir

"Tamam, tabloları oluşturdum" de :)

---

## 🔧 SORUN ÇÖZÜMLER

### Hata: "permission denied"
```sql
-- Yetkileri kontrol et
\du belek04

-- Eğer roller eksikse:
GRANT ALL PRIVILEGES ON DATABASE belekuni TO belek04;
GRANT ALL PRIVILEGES ON SCHEMA public TO belek04;
```

### Hata: "relation already exists"
```sql
-- Tabloları sil ve tekrar oluştur
DROP TABLE IF EXISTS "Vulnerabilities" CASCADE;
DROP TABLE IF EXISTS "Scans" CASCADE;
DROP TABLE IF EXISTS "Repositories" CASCADE;
DROP TABLE IF EXISTS "__EFMigrationsHistory" CASCADE;

-- Sonra database-setup.sql'i tekrar çalıştır
```

---

## 📞 YARDIM

Sorun olursa bana yaz, birlikte hallederiz!
